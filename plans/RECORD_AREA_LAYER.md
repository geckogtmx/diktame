# RECORD_AREA_LAYER: Transparent Border Overlay for Video Region Recording

> **Status:** BLOCKED — WinUI 3 transparent window limitation
> **Related:** SPEC_002 (Vision), B7 bug
> **Date:** 2026-03-27
> **Sessions spent:** 1 session, 5 failed attempts

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

## 5. Approaches NOT Yet Tried

### A. Win2D CanvasControl on a Non-WinUI Window
Create a pure Win32 window (via `CreateWindowEx` with `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW`) and attach a Win2D `CanvasControl` or use `Direct2D` to render the dashed border. Win32 layered windows support true per-pixel alpha via `UpdateLayeredWindow`. This bypasses WinUI 3's opaque compositor entirely.

**Pros:** True transparency via Win32. Proven pattern (classic overlay apps use this).
**Cons:** Complex — requires CreateWindowEx, message pump, D2D rendering. No XAML support.
**Effort:** Medium-High

### B. DesktopAcrylicBackdrop with TintOpacity = 0
Use `DesktopAcrylicController` with `TintOpacity = 0`, `TintColor = Transparent`, `LuminosityOpacity = 0`. This might produce a nearly-transparent window (just the XAML content visible). Simpler than custom SystemBackdrop.

**Pros:** Uses official WinUI 3 API. Simple to implement.
**Cons:** May still have minimum opacity. Blur effect may be visible. Untested.
**Effort:** Low — worth trying first.

### C. Four Thin Opaque Windows (No Transparency Needed)
Create 4 narrow WinUI 3 windows (2px each), positioned at the top/bottom/left/right edges of the recording region. Each is a solid white bar. No transparency needed — they're just thin strips.

**Pros:** No transparency whatsoever — guaranteed to work visually. Each can have `WDA_EXCLUDEFROMCAPTURE`.
**Cons:** 4 windows for a border feels hacky. User previously expressed skepticism ("does NOT sound reasonable").
**Effort:** Low

### D. DirectComposition Visual Overlay (No Window)
Use `Compositor` + `ContainerVisual` + `ShapeVisual` to render a dashed rectangle directly via the composition layer, without a WinUI 3 window at all. Attach to the desktop via `DesktopWindowTarget`.

**Pros:** No window = no opacity problem. DirectComposition supports transparency natively.
**Cons:** Complex composition API. May still be captured by screen recording. No precedent in codebase.
**Effort:** High

### E. GDI Overlay (Pure Win32, No WinUI)
Classic Win32 `CreateWindowEx` with `WS_EX_LAYERED`, then `SetLayeredWindowAttributes` with `LWA_COLORKEY` and a GDI-painted border. This is the pre-DirectComposition approach used by many screen recording tools.

**Pros:** Proven Win32 pattern. True transparency. Click-through via `WS_EX_TRANSPARENT`.
**Cons:** No XAML. Requires GDI painting. DPI-awareness complexity.
**Effort:** Medium

### F. Accept No Border (Pragmatic)
Document B7 as a known WinUI 3 limitation. The user already knows where the region is after selecting it. Many professional screen recording tools (OBS, ShareX) don't show a border during recording.

**Pros:** Zero effort. Ship what works.
**Cons:** Missing a nice-to-have UX feature.

---

## 6. Recommended Next Steps

1. **Try B first** (DesktopAcrylicBackdrop TintOpacity=0) — lowest effort, may work
2. **If B fails, try C** (4 thin opaque strips) — guaranteed to work visually, test WDA_EXCLUDEFROMCAPTURE
3. **If WDA_EXCLUDEFROMCAPTURE fails on thin strips, try E** (pure Win32 GDI window)
4. **If all fail, accept F** (no border)

---

## 7. Key Files

| File | Role |
|------|------|
| `src/DiktaMe.App/ViewModels/LoadingViewModel.cs` | Video recording orchestrator (`HandleVideoCaptureViaCpAsync`) |
| `src/DiktaMe.App/ViewModels/VideoCapture.cs` | ScreenRecorderLib wrapper |
| `src/DiktaMe.App/Views/SnippingOverlayWindow.xaml.cs` | Selection overlay (dim + cutout + dashed border) |
| `src/DiktaMe.App/ViewModels/ControlPanelViewModel.cs` | Vision wizard state machine |
