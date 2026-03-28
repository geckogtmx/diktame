# RECORD_AREA_LAYER: Transparent Border Overlay for Video Region Recording

> **Status:** ✅ SOLVED — Pure Win32 layered window + GDI+ (Attempt 6)
> **Related:** SPEC_002 (Vision), B7 bug
> **Date:** 2026-03-27
> **Sessions spent:** 1 session, 6 attempts (5 failed, 1 succeeded)
> **Solution:** `src/DiktaMe.App/Views/RecordingBorderOverlay.cs` — pure Win32 `CreateWindowEx(WS_EX_LAYERED)` + `UpdateLayeredWindow` + GDI+ `System.Drawing`. Bypasses WinUI 3 compositor entirely.
> **Polish remaining:** Dim overlay outside the recording region (currently only the dashed border is shown, no dim coat)

---

## 1. Problem Statement

When recording a video of a screen region (not full-screen), the user selects a rectangular area via the SnippingOverlayWindow. After selection, recording starts immediately. There is **no visual indicator** showing the recording boundary on screen during recording — the user cannot see where the recording region is.

**Goal:** Show a dashed white rectangle border around the selected region during video recording. The border should be:
- Visible to the user on their monitor
- NOT visible in the recorded video (excluded from screen capture)
- Click-through (input passes to windows below)
- Not covering/freezing the screen content

---

## 2. Technical Context

### Stack
- **UI Framework:** WinUI 3 (Windows App SDK 1.6), .NET 8, C#
- **Recording Library:** ScreenRecorderLib 6.6.0 (NuGet)
- **Capture API:** `Windows.Graphics.Capture` via `DisplayRecordingSource.MainMonitor` with `SourceRect` crop
- **Overlay Window:** `SnippingOverlayWindow` — fullscreen, always-on-top, uses frozen screenshot + 4-rect dim cutout for selection UI

### Key APIs Available
- `SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)` — excludes a window from `Windows.Graphics.Capture` at the OS level. **Confirmed working** by [Win32CaptureSample](https://github.com/robmikh/Win32CaptureSample) (Microsoft's reference implementation).
- `OverlappedPresenter.IsAlwaysOnTop` + `SetBorderAndTitleBar(false, false)` — borderless topmost window
- `GraphicsCaptureSession.IsBorderRequired` — controls Windows' yellow capture border (unrelated but noted)
- ScreenRecorderLib has NO `ExcludeWindow` API — exclusion must happen at the Win32/OS level

### The Core Problem
**WinUI 3 windows are ALWAYS opaque.** The compositor renders a solid background regardless of XAML `Background="Transparent"`. This is confirmed by the WinUI team:
- [GitHub #2956](https://github.com/microsoft/microsoft-ui-xaml/issues/2956) — "At this time we don't support transparency in WinUI 3's background"
- [GitHub #7276](https://github.com/microsoft/microsoft-ui-xaml/issues/7276) — Open feature request for transparent window support

---

## 3. Failed Attempts

### Attempt 1: XAML Background="Transparent"
**Approach:** New WinUI 3 Window with `Canvas Background="Transparent"` containing a dashed Rectangle.
**Result:** Black opaque window covering the recording region.
**Why:** WinUI 3 compositor ignores XAML transparency — the window is always opaque.

### Attempt 2: Win32 Color-Key Transparency (LWA_COLORKEY)
**Approach:** Set canvas to magenta (#FF00FF), use `SetLayeredWindowAttributes(hwnd, magentaRef, 0, LWA_COLORKEY)` to make magenta pixels transparent.
**Result:** Magenta opaque window — color key had no effect.
**Why:** `LWA_COLORKEY` works with GDI rendering. WinUI 3 uses DirectComposition, which bypasses GDI entirely. The color-key transparency mechanism is incompatible.

### Attempt 3: Keep SnippingOverlay Alive During Recording
**Approach:** Keep the existing `SnippingOverlayWindow` open after selection (it already shows the perfect dim + cutout + dashed border visual). Apply `WDA_EXCLUDEFROMCAPTURE`.
**Result:** Dim overlay recorded in video, screen frozen for user.
**Why:** The overlay is a full-screen window with a **frozen screenshot background**. Even with `WDA_EXCLUDEFROMCAPTURE`, the overlay covers the live screen — user sees a frozen image. And the dim coat appeared in the recording (WDA may not have taken effect, or it shows the frozen screenshot underneath).

### Attempt 4: SwitchToBorderOnlyMode (Hide Background + Dim)
**Approach:** After selection, hide the screenshot background, all dim rectangles, hint text. Only the dashed Rectangle remains. Set `WS_EX_TRANSPARENT` for click-through.
**Result:** Black window covering the recording region.
**Why:** Even with all XAML children hidden and `Background="Transparent"`, the WinUI 3 window background is ALWAYS opaque black. The window itself blocks the view.

### Attempt 5: TransparentBackdrop (Custom SystemBackdrop)
**Approach:** Custom `SystemBackdrop` subclass using `Compositor.CreateColorBrush(Color.FromArgb(0,255,255,255))` — fully transparent color brush as the window backdrop. Combined with `WS_EX_LAYERED`. Pattern from [GuildOfCalamity/Transparency](https://github.com/GuildOfCalamity/Transparency).
**Result:** Black opaque window covering the recording region.
**Why:** Unknown. The GuildOfCalamity project claims this works, but in our case the window still rendered opaque. Possible causes: OS version difference, missing DWM setup, or the technique only works for main app windows (not secondary overlay windows).

### Attempt 6: Pure Win32 Layered Window + GDI+ (SUCCESS ✅)
**Approach:** Bypass WinUI 3 entirely. Create a pure Win32 window via `CreateWindowEx` with `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`. Render dashed border with `System.Drawing` GDI+ into a DIB section, then blit via `UpdateLayeredWindow` with per-pixel alpha (`AC_SRC_ALPHA`). Apply `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` to the Win32 HWND.
**Result:** Transparent background, dashed white border with marching ants animation, click-through, NOT captured in recording.
**Why it worked:** `UpdateLayeredWindow` with `ULW_ALPHA` uses per-pixel alpha from the GDI+ bitmap — no WinUI 3 compositor involved. The Win32 window is a true layered window with alpha channel support, which is the same mechanism used by OBS, ShareX, and other screen recording tools.
**File:** `src/DiktaMe.App/Views/RecordingBorderOverlay.cs`
**Credit:** Cross-model consultation — Sonnet 4.6 proposed the winning approach (pure Win32 + GDI+), Gemini proposed similar (Win32 + GDI color-key), Nemotron proposed DirectComposition (viable but incomplete). All three converged on "bypass WinUI 3 entirely."

---

## 4. Reference Implementations Studied

### [robmikh/Win32CaptureSample](https://github.com/robmikh/Win32CaptureSample) (C++ / Microsoft)
- **Most valuable reference.** Confirms `WDA_EXCLUDEFROMCAPTURE` is the official mechanism.
- Uses `ICompositorDesktopInterop::CreateDesktopWindowTarget` to attach Composition visual tree to an HWND.
- `DirtyRegionVisualizer.cpp` renders D2D overlays directly onto captured frames (not what we need — we need on-screen border).
- Does NOT create transparent overlay windows — uses standard opaque windows.

### [renanalencar/SimpleRecorderWinUI3](https://github.com/renanalencar/SimpleRecorderWinUI3)
- Basic WinUI 3 screen recorder. Uses `GraphicsCapturePicker` for selection.
- **No custom overlay, no transparency, no border indicator.** Not useful for this problem.
- Uses `SharpDX` with `AlphaMode.Premultiplied` for swap chains — potentially relevant for D3D-based transparency.

### [microsoft/WinUI-Gallery](https://github.com/microsoft/WinUI-Gallery)
- Demonstrates `DesktopAcrylicBackdrop`, `MicaBackdrop`, `CompactOverlayPresenter`, `OverlappedPresenter`.
- No transparent window examples. `DesktopAcrylicBackdrop` gives semi-transparent blur, not full transparency.
- Win32 interop patterns for WndProc subclassing (but not for `WS_EX_TRANSPARENT`/`WS_EX_LAYERED`).

### [GuildOfCalamity/Transparency](https://github.com/GuildOfCalamity/Transparency)
- Claims working WinUI 3 transparent window via `TransparentBackdrop` + `WS_EX_LAYERED`.
- Our attempt to replicate this (Attempt 5) failed — window was opaque.
- May require additional DWM setup we missed, or may only work in specific OS builds.

### Windows Snipping Tool
- **IS WinUI 3** (confirmed — ported from Snip & Sketch in Windows 11).
- Shows dim overlay with live (non-frozen) background, resizable selection, then recording with frozen border.
- Has OS-level privileges and likely uses internal APIs not available to third-party apps.

---

## 5. Resolution

**The answer: bypass WinUI 3 entirely for the overlay.** Use pure Win32 `CreateWindowEx(WS_EX_LAYERED)` + `UpdateLayeredWindow` with GDI+ per-pixel alpha. This is the same mechanism used by OBS, ShareX, Greenshot, and every other screen overlay tool.

### Why Win32 layered windows work where WinUI 3 doesn't
- WinUI 3 windows use DirectComposition — the compositor always renders an opaque root. No XAML property, SystemBackdrop, or Win32 extended style can override this.
- Win32 layered windows (`WS_EX_LAYERED`) predate DirectComposition. `UpdateLayeredWindow` with `ULW_ALPHA` blends a GDI bitmap with per-pixel alpha directly with the desktop. The window has no compositor — it IS the bitmap.
- `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` works on Win32 HWNDs (confirmed by Microsoft's Win32CaptureSample).

### Other viable approaches (not needed but documented)
- **4 thin opaque WinUI 3 windows** (2px strips at edges) — would work, no transparency needed. More complex lifecycle but avoids Win32/GDI entirely.
- **DirectComposition visual overlay** (no window) — theoretically sound but `ICompositorDesktopInterop` has no C# interface definition. Would need manual COM interop.
- **DesktopAcrylicBackdrop with TintOpacity=0** — untested, may have minimum opacity floor.

---

## 7. Key Files

| File | Role |
|------|------|
| `src/DiktaMe.App/ViewModels/LoadingViewModel.cs` | Video recording orchestrator (`HandleVideoCaptureViaCpAsync`) |
| `src/DiktaMe.App/ViewModels/VideoCapture.cs` | ScreenRecorderLib wrapper |
| `src/DiktaMe.App/Views/SnippingOverlayWindow.xaml.cs` | Selection overlay (dim + cutout + dashed border) |
| `src/DiktaMe.App/ViewModels/ControlPanelViewModel.cs` | Vision wizard state machine |
