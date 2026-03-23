# UI_REVAMP_SCROLL_CP: Cylinder Roll Idle Animation

> **Status:** ✅ COMPLETE (code-verified 2026-03-22: ROLL-1..10 all done, 1010 tests passing)
> **Date:** 2026-03-22
> **Parent:** UI Revamp Phase 4 (Control Panel dashboard)
> **Depends on:** CP Auto-Collapse (CP.1–CP.8) ✅

---

## 1. Executive Summary

When the control panel is collapsed to its slim 170px bar and idle (state = `READY`), the status area is static and lifeless — a green dot and "READY" text that never changes until the user speaks. This feature adds a **3-layer cylinder roll animation** that turns the idle status area into a living information ticker.

The status area smoothly rolls upward (as if painted on a rotating cylinder), revealing different content "slides" in sequence:

| Layer | Content | Data Source |
|---|---|---|
| **A** | `● READY` | `ControlPanelViewModel.StatusText` + state dot |
| **B** | `🅺 14:30:45` | App logo (icon.png) + live clock (HH:mm:ss, 24h) |
| **C** | `☀ 18°C` | Weather icon + temperature (Open-Meteo API) |

**Cycle:** `A (hold 3s) → roll → B (hold 3s) → roll → C (hold 3s) → roll → A → ...`

Each roll transition takes ~500ms with cubic ease-out. The animation stops instantly on any activity (recording, processing, hover, expand). Weather data comes from **Open-Meteo** (free, no API key) with IP geolocation via **ip-api.com**, refreshing every 30 minutes.

### 1.1 Future Extensibility

The roller architecture is generic — it animates any pair of departing/arriving layers via `AnimateRoll(departTransform, departLayer, arriveTransform, arriveLayer, progress)`. Adding future slides (wallet balance, session stats, provider status, stock ticker) requires only:
1. A new XAML StackPanel (Layer D, E, ...)
2. One more enum value in the state machine

This spec intentionally builds the N-layer infrastructure, not a hardcoded 3-layer solution.

---

## 2. Technical Design

### 2.1 XAML Structure — Two-Layer → Three-Layer Clipped Container

Replace the existing status StackPanel (`ControlPanelPage.xaml:57-73`) with a fixed-height Grid containing three overlapping layers, clipped to prevent overflow:

```
StatusRollContainer (Grid, Height=20, Clip=RectangleGeometry)
├── StatusLayerA (StackPanel) — Ellipse + TextBlock "READY"
│   └── CompositeTransform: LayerATransform (TranslateY, ScaleY)
├── StatusLayerB (StackPanel) — Image logo + TextBlock clock
│   └── CompositeTransform: LayerBTransform (TranslateY, ScaleY)
└── StatusLayerC (StackPanel) — TextBlock weather icon + TextBlock temp
    └── CompositeTransform: LayerCTransform (TranslateY, ScaleY)
```

All three layers occupy the same Grid cell. Only one is visible at a time via `Opacity`. The `RectangleGeometry` clip (set via `SizeChanged` handler, same pattern as `WaveformContainer:306-312`) prevents overflow during roll transitions.

### 2.2 State Machine

```
enum CylinderRollPhase
{
    Idle,         // Normal display, waiting to start
    RollingAtoB,  // A departs up, B arrives from below  (~15 ticks = 500ms)
    HoldB,        // Logo + live clock visible            (~90 ticks = 3s)
    RollingBtoC,  // B departs up, C arrives from below   (~15 ticks = 500ms)
    HoldC,        // Weather icon + temp visible           (~90 ticks = 3s)
    RollingCtoA,  // C departs up, A arrives from below   (~15 ticks = 500ms)
    HoldA,        // Back to READY, wait before next cycle (~90 ticks = 3s)
}
```

**Timing at 33ms/tick (30fps):**
- Roll transition: 15 ticks = ~500ms
- Hold phase: 90 ticks = ~3s
- Startup delay: 60 ticks = ~2s (after collapse completes, before first roll)
- Full cycle: ~21s (3 slides × 3s hold + 3 transitions × 0.5s)

**Preconditions (ALL must be true, checked every tick):**
- `_isBarCollapsed == true` (not collapsing, not expanding)
- `ViewModel.CurrentState == PipelineState.Idle`
- `_levelMonitor?.IsActive != true`
- `ViewModel.IdleRollEnabled == true`

If any precondition fails mid-roll → `ResetCylinderRoll()` snaps instantly to Layer A visible (no graceful finish — responsiveness over elegance).

**Weather skip logic:** If `ViewModel.WeatherText` is empty when entering `RollingBtoC`, skip directly to `RollingCtoA` phase (roll from B straight back to A). This degrades the cycle to 2-layer when offline.

### 2.3 Animation Math — Cylinder Effect

Each roll involves exactly 2 layers (one departing, one arriving). A generic helper animates both:

```
AnimateRoll(departTransform, departLayer, arriveTransform, arriveLayer, progress):
    t = EaseOut(progress)       // reuses existing cubic: 1 - (1-t)³
    h = container height (20px)

    Departing layer:
        TranslateY = -h * t          (moves up and out of view)
        ScaleY     = 1.0 - 0.7 * t   (compresses to 30% — cylinder foreshortening)
        Opacity    = 1.0 - t          (fades as it curves away)

    Arriving layer:
        TranslateY = h * (1 - t)     (starts below, arrives at 0)
        ScaleY     = 0.3 + 0.7 * t   (expands from 30% to 100%)
        Opacity    = t                (fades in as it curves into view)
```

The vertical scale compression creates the "painted on a cylinder surface" illusion — content appears to curve away at the top and curve in from the bottom.

### 2.4 WeatherService — Open-Meteo + IP Geolocation

New file: `DiktaMe.Core/Weather/WeatherService.cs`

**Architecture:**
- Plain `HttpClient` + `System.Text.Json` (no new NuGet packages)
- IP geolocation: `GET http://ip-api.com/json/?fields=lat,lon` (one-time, cached)
- Weather: `GET https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current_weather=true`
- 30-minute refresh interval with in-memory cache
- `SemaphoreSlim` to prevent concurrent fetches
- 10s HTTP timeout
- All errors caught and logged — returns `null` on failure (never throws)

**Weather code → icon mapping (WMO standard):**

| WMO Code | Condition | Icon (Unicode) |
|---|---|---|
| 0 | Clear sky | ☀ |
| 1–3 | Partly cloudy | ⛅ |
| 45, 48 | Fog | 🌫 |
| 51–55 | Drizzle | 🌧 |
| 61–65 | Rain | 🌧 |
| 71–77 | Snow | ❄ |
| 80–82 | Showers | 🌧 |
| 95, 96, 99 | Thunderstorm | ⛈ |

Unicode emoji render well in WinUI 3 TextBlocks — no custom icon font needed.

**Data record:**
```csharp
public sealed record WeatherData(
    double TemperatureCelsius,
    int WeatherCode,
    string Icon,
    string Description);
```

### 2.5 Timer Integration

`TickCylinderRoll()` is called from `OnEffectTimerTick()` alongside `TickIdleBehavior()` and `TickCollapseAnimation()`, **before** the `VisualEffectsEnabled` early-return gate. This makes the roll independent of glow/shimmer effects — it has its own `IdleRollEnabled` toggle.

```
OnEffectTimerTick():
    TickIdleBehavior()
    TickCollapseAnimation()
    TickCylinderRoll()          ← NEW (independent of VisualEffectsEnabled)
    if (!VisualEffectsEnabled) { ... early return ... }
    // ... existing glow/shimmer/waveform logic
```

### 2.6 Settings Model

New property in `ControlPanelSettings` (`AppSettings.cs:217`):
```csharp
public bool IdleRollEnabled { get; init; } = true;
```

Default `true` — the animation is on by default, users can disable in Settings → Visual Effects → "Idle Branding Animation".

### 2.7 Interaction Matrix

| Feature | Conflict? | Resolution |
|---|---|---|
| Glow breathing (`StatusTextScale`) | No | Glow only during recording (not idle) — mutually exclusive |
| Shimmer sweep | No | Shimmer only during processing — mutually exclusive |
| Waveform | No | Waveform only during recording — mutually exclusive |
| Auto-collapse | Cooperative | Roll starts 2s AFTER collapse completes |
| Auto-hide (fade) | Cooperative | Roll continues during fade — invisible anyway |
| Hover → expand | Immediate | `RestoreBarWidth()` sets `_isBarExpanding`, precondition fails, snap-back |
| Hotkey press | Immediate | State changes to Recording, precondition fails, snap-back |
| Theme change | Transparent | Brushes are StaticResources mutated in-place by ThemeService |
| Tray hide → restore | Immediate | `ResetAutoHideState()` → `RestoreBarWidth()` → snap-back |
| Expand direction (up/down) | No effect | Roll is purely within the status area, direction doesn't matter |

---

## 3. Files to Modify/Create

| # | File | Type | Change |
|---|---|---|---|
| 1 | `src/DiktaMe.Core/Config/AppSettings.cs` | Edit | Add `IdleRollEnabled` to `ControlPanelSettings` |
| 2 | `src/DiktaMe.Core/Weather/WeatherService.cs` | **NEW** | Weather data fetcher |
| 3 | `src/DiktaMe.App/App.xaml.cs` | Edit | Register `WeatherService` singleton |
| 4 | `src/DiktaMe.App/ViewModels/ControlPanelViewModel.cs` | Edit | Add `IdleRollEnabled`, weather properties, `RefreshWeatherAsync()` |
| 5 | `src/DiktaMe.App/ViewModels/Settings/GeneralSettingsViewModel.cs` | Edit | Add `IdleRollEnabled` toggle wiring |
| 6 | `src/DiktaMe.App/Views/ControlPanelPage.xaml` | Edit | 3-layer clipped Grid replacing status StackPanel |
| 7 | `src/DiktaMe.App/Views/ControlPanelPage.xaml.cs` | Edit | State machine, `AnimateRoll()`, weather refresh |
| 8 | `src/DiktaMe.App/Views/Settings/GeneralSettingsPage.xaml` | Edit | Toggle switch in Visual Effects card |
| 9 | `src/DiktaMe.App/Strings/en/Resources.resw` | Edit | 2 localization strings |
| 10 | `src/DiktaMe.App/Strings/es-MX/Resources.resw` | Edit | 2 localization strings |
| 11 | `src/DiktaMe.Core.Tests/Weather/WeatherServiceTests.cs` | **NEW** | Unit tests for WeatherService |

---

## 4. Task Log

### ROLL-1: Settings Model + ViewModel Wiring
**Status:** PENDING

Add `IdleRollEnabled` property to `ControlPanelSettings`, wire through `ControlPanelViewModel` and `GeneralSettingsViewModel`.

**Files:**
- `AppSettings.cs` — Add `public bool IdleRollEnabled { get; init; } = true;` after line 217
- `ControlPanelViewModel.cs` — Add `[ObservableProperty] private bool _idleRollEnabled;`, wire in `LoadFromSettings()` after line 950
- `GeneralSettingsViewModel.cs` — Add `[ObservableProperty] private bool _idleRollEnabled = true;`, change handler `partial void OnIdleRollEnabledChanged(bool value) => Save();`, load in `LoadFromSettings()` after line 220, save in `Save()` `ControlPanel = ... with { }` block after `BarPosition`

**Success Criteria:**
- `dotnet build DiktaMe.sln` — 0 errors
- `settings.json` persists `IdleRollEnabled` when toggled
- `ControlPanelViewModel.IdleRollEnabled` reflects saved value on restart

---

### ROLL-2: WeatherService Implementation
**Status:** PENDING

Create `WeatherService` in `DiktaMe.Core/Weather/` with Open-Meteo + IP geolocation.

**Files:**
- **NEW** `src/DiktaMe.Core/Weather/WeatherService.cs`

**Implementation details:**
- Namespace: `DiktaMe.Core.Weather`
- Constructor: `WeatherService(HttpClient? httpClient = null)` (matches provider pattern)
- `GetCurrentWeatherAsync(CancellationToken)` → `Task<WeatherData?>`
- IP geolocation: `http://ip-api.com/json/?fields=lat,lon` (one-time fetch, cache lat/lon)
- Weather: `https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current_weather=true`
- 30-minute refresh interval (`_lastFetch` + `TimeSpan` comparison)
- `SemaphoreSlim(1,1)` for concurrent fetch protection
- `MapWeatherCode(int code)` → `(string icon, string description)` static helper
- HttpClient timeout: 10 seconds
- Error handling: catch all, log via `Serilog.Log.Warning()`, return `_cachedData` or `null`
- Implements `IDisposable` (disposes HttpClient and SemaphoreSlim)
- Use `CultureInfo.InvariantCulture` for all number formatting in URLs

**Success Criteria:**
- `dotnet build DiktaMe.sln` — 0 errors
- Unit tests pass (ROLL-3)

---

### ROLL-3: WeatherService Unit Tests
**Status:** PENDING

Test weather data parsing, WMO code mapping, caching, and error resilience.

**Files:**
- **NEW** `src/DiktaMe.Core.Tests/Weather/WeatherServiceTests.cs`

**Test cases:**
1. `MapWeatherCode_ClearSky_ReturnsSunIcon` — WMO 0 → ☀
2. `MapWeatherCode_PartlyCloudy_ReturnsCloudIcon` — WMO 1-3 → ⛅
3. `MapWeatherCode_Rain_ReturnsRainIcon` — WMO 61-65 → 🌧
4. `MapWeatherCode_Snow_ReturnsSnowIcon` — WMO 71-77 → ❄
5. `MapWeatherCode_Thunderstorm_ReturnsStormIcon` — WMO 95 → ⛈
6. `MapWeatherCode_UnknownCode_ReturnsFallback` — WMO 999 → reasonable default
7. `GetCurrentWeather_ParsesValidResponse_ReturnsWeatherData` — Mock HttpClient returns valid Open-Meteo JSON → correct `WeatherData` record
8. `GetCurrentWeather_HttpError_ReturnsNull` — Mock HttpClient throws → returns null, no exception
9. `GetCurrentWeather_CachesFreshResult` — Two rapid calls → only one HTTP request
10. `GetCurrentWeather_RefreshesAfterInterval` — Advance time past 30min → new HTTP request

**Success Criteria:**
- All 10 tests pass
- `dotnet test DiktaMe.sln` — 0 failures

---

### ROLL-4: DI Registration
**Status:** PENDING

Register `WeatherService` as singleton in `App.xaml.cs`.

**Files:**
- `App.xaml.cs` — Add `services.AddSingleton<WeatherService>();` in `ConfigureServices()`

**Success Criteria:**
- `dotnet build DiktaMe.sln` — 0 errors
- `App.Current.Services.GetService<WeatherService>()` returns non-null at runtime

---

### ROLL-5: ControlPanelViewModel Weather Properties
**Status:** PENDING

Add weather data properties and refresh method to ViewModel.

**Files:**
- `ControlPanelViewModel.cs` — Add:
  - `[ObservableProperty] private string _weatherText = "";` (e.g. "18°C")
  - `[ObservableProperty] private string _weatherIcon = "";` (emoji glyph)
  - `RefreshWeatherAsync(WeatherService, CancellationToken)` method — calls service, updates properties via `_dispatcher.TryEnqueue()`

**Success Criteria:**
- `dotnet build DiktaMe.sln` — 0 errors
- Properties update when `RefreshWeatherAsync()` is called

---

### ROLL-6: XAML 3-Layer Structure
**Status:** PENDING

Replace the status StackPanel (lines 57-73) with a 3-layer clipped Grid container.

**Files:**
- `ControlPanelPage.xaml` — Replace lines 57-73

**Key elements:**
- `StatusRollContainer` (Grid, Height=20)
- `StatusLayerA` (StackPanel) — Ellipse + StatusTextBlock + StatusTextScale (preserved for glow)
- `StatusLayerB` (StackPanel, Opacity=0) — Image BrandingLogo + TextBlock BrandingTimeText
- `StatusLayerC` (StackPanel, Opacity=0) — TextBlock WeatherIconText + TextBlock WeatherTempText
- Each layer has `CompositeTransform` named `LayerATransform`, `LayerBTransform`, `LayerCTransform`
- Layer B/C start with `TranslateY="20"`, `ScaleY="0.3"`, `Opacity="0"`

**WinUI 3 safety checks:**
- `CompositeTransform` goes in `StackPanel.RenderTransform` — safe, `RenderTransform` is on `UIElement`
- `RenderTransformOrigin="0.5,0.5"` on StackPanel — safe
- `x:Bind` on TextBlock `Text` — safe (it's `Run.Text` that crashes)
- No `FontFamily` on Grid/Panel — only on TextBlock/Page

**Success Criteria:**
- `dotnet build DiktaMe.sln` — 0 errors (no XAML compiler crash)
- App launches and shows `● READY` normally (Layer A visible, B/C hidden)
- No visual regression when bar is expanded

---

### ROLL-7: Code-Behind Animation Engine
**Status:** PENDING

Implement the state machine, animation helpers, logo initialization, clipping, and weather refresh in code-behind.

**Files:**
- `ControlPanelPage.xaml.cs`

**Implementation details:**

*Fields (after waveform fields ~line 79):*
- `CylinderRollPhase` enum (7 values)
- `_rollPhase`, `_rollTickCounter`, `_rollIdleWaitTicks`
- Constants: `RollTransitionTicks = 15`, `RollHoldTicks = 90`, `RollStartDelayTicks = 60`
- `_lastTimeUpdateSecond = -1`
- `_weatherService` (WeatherService?)
- `_lastWeatherRefresh` (DateTime)

*Initialization (end of `InitializeVisualEffects()`):*
- Load `BrandingLogo.Source` from `ms-appx:///Assets/icon.png`
- Clip `StatusRollContainer` via `SizeChanged` handler (same as WaveformContainer)
- Get `_weatherService` from DI
- Fire initial weather fetch (fire-and-forget)

*Timer integration (`OnEffectTimerTick()` line 408):*
- Add `TickCylinderRoll()` call after `TickCollapseAnimation()`, before `VisualEffectsEnabled` check

*Methods:*
- `TickCylinderRoll()` — precondition check + state machine switch
- `AnimateRoll(departTransform, departLayer, arriveTransform, arriveLayer, progress)` — generic roller
- `ResetCylinderRoll()` — snap to Layer A visible, B+C hidden, reset counters
- `UpdateBrandingTime()` — update clock text once per second (compare `DateTime.Now.Second`)
- `TryRefreshWeather()` — fire-and-forget if 30min elapsed

**Success Criteria:**
- `dotnet build DiktaMe.sln` — 0 errors
- App launches → enable auto-collapse → wait for bar to collapse → after ~2s the roll starts
- Roll cycle: READY → logo+clock → weather → READY (smooth transitions)
- Clock updates every second during Layer B hold
- Weather icon + temperature shown during Layer C hold
- Pressing dictation hotkey mid-roll → instant snap-back to ● READY
- Hovering mid-roll → instant snap-back + bar expansion
- No crashes, no exit 127

---

### ROLL-8: Settings UI Toggle
**Status:** PENDING

Add the "Idle Branding Animation" toggle to the Visual Effects card in General Settings.

**Files:**
- `GeneralSettingsPage.xaml` — Add Grid with label + description + ToggleSwitch after the waveform section (line 273), outside the `VisualEffectsEnabled` conditional wrapper but inside the card

**Success Criteria:**
- Toggle appears in Settings → General → Visual Effects section
- Toggle persists state across app restarts
- Toggling off mid-roll stops the animation immediately

---

### ROLL-9: Localization Strings
**Status:** PENDING

Add English and Spanish strings for the settings toggle.

**Files:**
- `Strings/en/Resources.resw`:
  - `Settings_IdleRoll_Enable_Label.Text` = `Idle Branding Animation`
  - `Settings_IdleRoll_Enable_Description.Text` = `When collapsed and idle, cycles between status, logo with clock, and weather`
- `Strings/es-MX/Resources.resw`:
  - `Settings_IdleRoll_Enable_Label.Text` = `Animación de marca en reposo`
  - `Settings_IdleRoll_Enable_Description.Text` = `Cuando está colapsado e inactivo, alterna entre estado, logo con reloj y clima`

**Success Criteria:**
- Strings display correctly in both languages
- No missing string warnings at runtime

---

### ROLL-10: Integration Test + Polish
**Status:** PENDING

Full end-to-end verification and any visual polish.

**Success Criteria:** See Section 5 (full success criteria checklist)

---

## 5. Success Criteria & Test Plan

### 5.1 Build Gate
- [ ] `dotnet build DiktaMe.sln` — 0 errors, 0 new warnings
- [ ] `dotnet test DiktaMe.sln` — all existing tests pass + new WeatherService tests pass

### 5.2 Functional Tests (Manual)

| # | Test | Expected Result | Pass? |
|---|---|---|---|
| T-1 | Enable auto-collapse, wait for bar to collapse, wait ~2s | Cylinder roll animation starts: READY rolls up, logo+clock rolls in | |
| T-2 | During Layer B hold, watch clock | Time updates every second (HH:mm:ss format) | |
| T-3 | During Layer C hold, verify weather | Weather emoji + temperature in °C displayed | |
| T-4 | Full cycle completes | A→B→C→A transitions are smooth, no jitter | |
| T-5 | Press dictation hotkey mid-roll (any phase) | Instant snap-back to ● READY/LISTENING | |
| T-6 | Hover over bar mid-roll | Instant snap-back, bar expands to full width | |
| T-7 | Expand bar (click collapse button) while roll is active | Instant snap-back, bar expands to show full CP | |
| T-8 | Disconnect internet, trigger roll | Weather layer (C) is skipped, cycle is A→B→A→B... | |
| T-9 | Settings → toggle "Idle Branding Animation" OFF | Roll stops immediately (snap to READY) | |
| T-10 | Settings → toggle "Idle Branding Animation" ON | Roll resumes after next collapse + 2s delay | |
| T-11 | Change theme mid-roll | Text colors update smoothly, no crash | |
| T-12 | Minimize to tray, restore | Bar resets to full width, roll resets cleanly | |
| T-13 | Roll active + auto-hide fade starts | Roll continues while fading (both run independently) | |
| T-14 | Fresh install (no settings.json) | `IdleRollEnabled` defaults to `true` | |
| T-15 | Expand direction "Up" with roll | Roll still works (purely within status area, direction irrelevant) | |
| T-16 | Weather data refreshes | After 30min, new weather data appears on next C hold | |

### 5.3 Edge Cases

| Scenario | Expected Behavior |
|---|---|
| `settings.json` has `"IdleRollEnabled": null` | `SanitizeNulls()` irrelevant (bool, not sub-object). Default `true` applies. |
| IP geolocation blocked by firewall | Lat/lon stays null → weather skipped → 2-layer mode |
| Open-Meteo returns 500 error | Cached data shown if available, otherwise weather layer skipped |
| Multiple rapid collapse/expand cycles | `_rollIdleWaitTicks` resets each time → no premature roll start |
| StatusTextScale glow breathing during roll | Impossible — glow only active during Recording (idle = no glow) |
| `SanitizeNulls()` and ControlPanelSettings | ControlPanelSettings is a record init'd by JSON. `null` for `IdleRollEnabled` (bool) uses default `true`. No SanitizeNulls issue. |

### 5.4 Performance
- Roll adds ~6 property writes per tick during transition (TranslateY + ScaleY + Opacity × 2 layers)
- During hold phases: 0 writes (clock = 1 string write/sec, weather = 0)
- Weather fetch: 1 HTTP call per 30 minutes (negligible)
- IP geolocation: 1 HTTP call per app lifetime (cached)
- No memory allocation per tick (no new objects, no string interpolation in hot path except clock once/sec)

---

## 6. Implementation Order

```
ROLL-1  Settings + ViewModel wiring
ROLL-2  WeatherService implementation
ROLL-3  WeatherService unit tests
ROLL-4  DI registration
ROLL-5  ViewModel weather properties
ROLL-6  XAML 3-layer structure
ROLL-7  Code-behind animation engine     ← largest task
ROLL-8  Settings UI toggle
ROLL-9  Localization strings
ROLL-10 Integration test + polish
```

Tasks ROLL-1 through ROLL-5 can be committed together as foundation.
ROLL-6 + ROLL-7 are the core animation (should be one commit).
ROLL-8 + ROLL-9 are settings UI (one commit).
ROLL-10 is verification + any fixes.

---

## 7. Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| XAML compiler crash from CompositeTransform on StackPanel | Low | High | `RenderTransform` is on `UIElement`, safe on all elements. Validated against MEMORY.md gotchas. |
| Weather API rate limiting | Very Low | Low | Open-Meteo has no rate limits. 1 call/30min is trivial. |
| ip-api.com down or blocked | Low | Low | Graceful degradation to 2-layer mode (A↔B only) |
| Unicode emoji rendering issues | Low | Medium | WinUI 3 renders emoji natively in TextBlock. Fallback: Segoe MDL2 glyphs. |
| NRE during roll animation (exit 127) | Low | High | All XAML elements are generated by InitializeComponent(). Timer runs on UI thread. No cross-thread access. |
| Roll feels janky at 30fps | Low | Medium | 15 ticks × 33ms = 500ms transition is plenty smooth. Cubic ease-out provides natural motion. |

---

## 8. Commit Plan

```
commit 1: feat(cp): idle roll infrastructure — settings, weather service, DI, tests [ROLL-1..5]
commit 2: feat(cp): 3-layer cylinder roll animation — XAML + code-behind [ROLL-6..7]
commit 3: feat(cp): idle roll settings toggle + localization [ROLL-8..9]
```
