
using System;
using System.ComponentModel;
using DiktaMe.App.Services;
using DiktaMe.App.ViewModels;
using DiktaMe.Core.Audio;
using DiktaMe.Core.Input;
using DiktaMe.Core.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Serilog;
using Windows.Graphics;
using Windows.UI;

namespace DiktaMe.App.Views;
public sealed partial class ControlPanelPage : Page
{
    public ControlPanelViewModel ViewModel { get; }

    // ── Expand direction ────────────────────────────────────────────────
    private bool _expandUpward;
    private DispatcherQueueTimer? _holdTimer; // 500ms hold-click to flip direction
    private bool _holdFired; // true if hold-click triggered (suppress normal click)

    // ── Auto-hide (fade) ─────────────────────────────────────────────────
    private int _idleTicks;         // consecutive idle ticks (reset on activity)
    private byte _currentOpacity = 255; // current window alpha (0-255)
    private bool _isFadingOut;      // true during fade-out animation

    // ── Visual effects ────────────────────────────────────────────────────
    private DispatcherQueueTimer? _effectTimer;
    private AudioLevelMonitor? _levelMonitor;
    private LocalizationService? _loc;
    private double _shimmerPhase; // 0.0→1.0, loops
    private double _currentGlowLevel; // smoothed glow level for fade-out
    private int _tickCount; // debug: throttled logging counter
    private bool _wasMonitorActive; // edge detection for recording state transitions

    // Cached brush references from Application.Resources — mutating .Color updates ALL elements
    private SolidColorBrush? _bgBrush;       // AppBackgroundBrush
    private SolidColorBrush? _hdrBrush;      // AppSurfaceBrush
    private SolidColorBrush? _borderBrush;   // AppBorderBrush
    private SolidColorBrush? _textPrimary;   // AppTextBrush
    private SolidColorBrush? _textSecondary; // AppTextDimBrush
    private SolidColorBrush? _perfGreen;     // AppPerfGreenBrush

    // Dedicated brush for header-only glow — AppSurfaceBrush is shared across all rows,
    // so "Top Bar Only" needs a separate brush applied directly to HeaderBar.Background
    private SolidColorBrush? _headerBarBrush;
    private Services.ThemeService? _themeService;

    // ── Auto-collapse (bar width shrink) ─────────────────────────────────
    private const int FullWidth = 420;
    private const int CollapsedWidth = 170;
    private const int CollapseAnimationTicks = 12; // ~400ms at 33ms/tick
    private double _currentWidth = FullWidth;
    private bool _isBarCollapsed;
    private bool _isBarCollapsing;
    private bool _isBarExpanding;
    private double _collapseAnimProgress;

    // ── Waveform ──────────────────────────────────────────────────────
    private const int WaveformPoints = 40;
    private const int WaveformBarCount = 20;
    private const double WaveformFrequency = 2.5;
    private double _waveformPhase;
    private double _waveformAmplitude;
    private double _waveformTargetAmplitude;
    private double _waveformOpacity;
    private string _waveformStyleCached = "Wave";
    private Rectangle[]? _barElements;

    // Base colors (idle) → Bright colors (max glow) — derived from current theme palette
    private byte _baseBgR, _baseBgG, _baseBgB;
    private byte _brightBgR, _brightBgG, _brightBgB;
    private byte _baseHdrR, _baseHdrG, _baseHdrB;
    private byte _brightHdrR, _brightHdrG, _brightHdrB;
    private byte _baseBrdR, _baseBrdG, _baseBrdB;
    private byte _brightBrdR, _brightBrdG, _brightBrdB;
    private byte _baseTxtR, _baseTxtG, _baseTxtB;
    private byte _brightTxtR, _brightTxtG, _brightTxtB;
    private byte _baseTxt2R, _baseTxt2G, _baseTxt2B;
    private byte _brightTxt2R, _brightTxt2G, _brightTxt2B;
    private byte _baseGrnR, _baseGrnG, _baseGrnB;
    private byte _brightGrnR, _brightGrnG, _brightGrnB;

    public ControlPanelPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<ControlPanelViewModel>();
        this.InitializeComponent();
        RootGrid.SizeChanged += OnRootGridSizeChanged;
        InitializeVisualEffects();
        InitializeExpandDirection();
    }

    // ── Window auto-resize ────────────────────────────────────────────────

    private void OnRootGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var window = App.Current.MainWindow;
        if (window is null)
        {
            return;
        }

        // During collapse/expand animation, width is driven by TickCollapseAnimation.
        // Suppress height-change resizes to prevent the bar from drifting on screen.
        if (_isBarCollapsing || _isBarExpanding || _isBarCollapsed)
        {
            return;
        }

        double scale = XamlRoot?.RasterizationScale ?? 1.0;
        int physicalWidth = (int)(_currentWidth * scale);
        int physicalHeight = (int)(e.NewSize.Height * scale);

        var appWindow = window.AppWindow;
        var current = appWindow.Size;

        // Only resize if height actually changed (prevents infinite loop)
        if (Math.Abs(current.Height - physicalHeight) <= 1)
        {
            return;
        }

        if (_expandUpward)
        {
            // Bottom-anchored: shift Y so the header (at bottom) stays pinned
            int deltaH = physicalHeight - current.Height;
            var pos = appWindow.Position;
            int newY = Math.Max(0, pos.Y - deltaH);
            appWindow.MoveAndResize(new Windows.Graphics.RectInt32(pos.X, newY, physicalWidth, physicalHeight));
        }
        else
        {
            appWindow.Resize(new SizeInt32(physicalWidth, physicalHeight));
        }
    }

    // ── Expand direction ───────────────────────────────────────────────────

    private void InitializeExpandDirection()
    {
        // Apply initial direction
        _expandUpward = ViewModel.ExpandUpward;
        ApplyExpandDirection(_expandUpward);

        // React to VM changes (from settings or FlipExpandDirection command)
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Hold-click on collapse button → flip direction
        // Button.Click fires reliably; PointerPressed starts the hold timer.
        // If hold fires (500ms), we set _holdFired=true and suppress the next Click.
        _holdTimer = DispatcherQueue.CreateTimer();
        _holdTimer.Interval = TimeSpan.FromMilliseconds(500);
        _holdTimer.IsRepeating = false;
        _holdTimer.Tick += (_, _) =>
        {
            _holdFired = true;
            ViewModel.FlipExpandDirectionCommand.Execute(null);
        };

        CollapseButton.AddHandler(UIElement.PointerPressedEvent,
            new PointerEventHandler((_, _) =>
            {
                _holdFired = false;
                _holdTimer?.Start();
            }), true);

        CollapseButton.AddHandler(UIElement.PointerReleasedEvent,
            new PointerEventHandler((_, _) =>
            {
                _holdTimer?.Stop();
            }), true);

        CollapseButton.AddHandler(UIElement.PointerCaptureLostEvent,
            new PointerEventHandler((_, _) =>
            {
                _holdTimer?.Stop();
            }), true);

        CollapseButton.Click += (_, _) =>
        {
            if (!_holdFired)
            {
                ViewModel.ToggleExpandedCommand.Execute(null);
            }
        };

        // Double-click on header is handled at Win32 level (WM_NCLBUTTONDBLCLK)
        // in MainWindow.cs — SetTitleBar routes pointer events to the OS,
        // so XAML DoubleTapped never fires on the title bar element.
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(ControlPanelViewModel.ExpandUpward), StringComparison.Ordinal))
        {
            _expandUpward = ViewModel.ExpandUpward;
            ApplyExpandDirection(_expandUpward);
        }
        else if (string.Equals(e.PropertyName, nameof(ControlPanelViewModel.IsExpanded), StringComparison.Ordinal))
        {
            if (ViewModel.IsExpanded && (_isBarCollapsed || _isBarCollapsing))
            {
                // User expanded panel — restore full width immediately
                _isBarCollapsing = false;
                _isBarCollapsed = false;
                _isBarExpanding = false;
                _collapseAnimProgress = 0;
                _currentWidth = FullWidth;
                HeaderButtons.Visibility = Visibility.Visible;
                HeaderButtons.Opacity = 1.0;
                ApplyWindowWidth(FullWidth);
            }
        }
    }

    private void ApplyExpandDirection(bool expandUpward)
    {
        // Swap Grid.Row assignments: header stays at top (row 0) or moves to bottom (row 5)
        // Content rows fill the remaining positions in forward or reverse order
        if (expandUpward)
        {
            // Up mode: footer at top (row 0), content rows 1-4, header at bottom (row 5)
            Grid.SetRow(FooterRow, 0);
            Grid.SetRow(ModesRow, 1);
            Grid.SetRow(ActionsRow, 2);
            Grid.SetRow(SessionStatsRow, 3);
            Grid.SetRow(PerfStatsRow, 4);
            Grid.SetRow(HeaderBar, 5);

            // Flip border lines: content rows get top border, header gets top border
            ModesRow.BorderThickness = new Thickness(0, 0, 0, 1);
            ActionsRow.BorderThickness = new Thickness(0, 0, 0, 1);
            SessionStatsRow.BorderThickness = new Thickness(0, 0, 0, 1);
            PerfStatsRow.BorderThickness = new Thickness(0, 0, 0, 1);
            HeaderBar.BorderThickness = new Thickness(0, 1, 0, 0);
            HeaderBar.Padding = new Thickness(12, 4, 12, 11);

            // Edge glow: bottom edges when expanding upward
            EdgeGlow.BorderThickness = new Thickness(2, 0, 2, 3);

            // Shimmer header overlay follows header row
            Grid.SetRow(ShimmerOverlayHeader, 5);

            // Footer padding: modest top margin at window edge, tight against content below
            FooterRow.Padding = new Thickness(10, 8, 10, 4);
        }
        else
        {
            // Down mode (default): header at top (row 0), content rows 1-5
            Grid.SetRow(HeaderBar, 0);
            Grid.SetRow(ModesRow, 1);
            Grid.SetRow(ActionsRow, 2);
            Grid.SetRow(SessionStatsRow, 3);
            Grid.SetRow(PerfStatsRow, 4);
            Grid.SetRow(FooterRow, 5);

            // Default borders: content rows have bottom border, header has bottom border
            ModesRow.BorderThickness = new Thickness(0, 0, 0, 1);
            ActionsRow.BorderThickness = new Thickness(0, 0, 0, 1);
            SessionStatsRow.BorderThickness = new Thickness(0, 0, 0, 1);
            PerfStatsRow.BorderThickness = new Thickness(0, 0, 0, 1);
            HeaderBar.BorderThickness = new Thickness(0, 0, 0, 1);
            HeaderBar.Padding = new Thickness(12, 4, 12, 11);

            // Edge glow: top edges when expanding downward
            EdgeGlow.BorderThickness = new Thickness(2, 3, 2, 0);

            // Shimmer header overlay follows header row
            Grid.SetRow(ShimmerOverlayHeader, 0);

            // Footer padding: default (branding text at bottom edge)
            FooterRow.Padding = new Thickness(10, 4, 10, 16);
        }

        Log.Information("ControlPanel: ApplyExpandDirection expandUpward={ExpandUpward}", expandUpward);
    }

    // ── Visual effects engine ─────────────────────────────────────────────

    private void InitializeVisualEffects()
    {
        _levelMonitor = App.Current.Services.GetRequiredService<AudioLevelMonitor>();
        _loc = App.Current.Services.GetRequiredService<LocalizationService>();
        InitializeWaveformBars();

        // Keep the waveform container clipped so the Polyline's measured bounds
        // never influence the HeaderBar grid layout (prevents text displacement).
        WaveformContainer.SizeChanged += (_, e) =>
        {
            WaveformContainer.Clip = new Microsoft.UI.Xaml.Media.RectangleGeometry
            {
                Rect = new Windows.Foundation.Rect(0, 0, e.NewSize.Width, e.NewSize.Height),
            };
        };

        // Cache brush references from App-level resources — changing .Color updates every element
        var res = Application.Current.Resources;
        _bgBrush = (SolidColorBrush)res["AppBackgroundBrush"];
        _hdrBrush = (SolidColorBrush)res["AppSurfaceBrush"];
        _borderBrush = (SolidColorBrush)res["AppBorderBrush"];
        _textPrimary = (SolidColorBrush)res["AppTextBrush"];
        _textSecondary = (SolidColorBrush)res["AppTextDimBrush"];
        _perfGreen = (SolidColorBrush)res["AppPerfGreenBrush"];

        // Subscribe to theme changes to re-derive glow colors
        _themeService = App.Current.Services.GetRequiredService<Services.ThemeService>();
        _themeService.ThemeChanged += (_, _) => DispatcherQueue.TryEnqueue(LoadThemeColors);
        LoadThemeColors();

        // Dedicated brush for header-only glow (starts at same color as AppSurfaceBrush)
        _headerBarBrush = new SolidColorBrush(Color.FromArgb(255, _baseHdrR, _baseHdrG, _baseHdrB));

        _effectTimer = DispatcherQueue.CreateTimer();
        _effectTimer.Interval = TimeSpan.FromMilliseconds(33); // ~30fps
        _effectTimer.Tick += OnEffectTimerTick;
        _effectTimer.Start();

        // Pause timer when page is not visible (e.g., window hidden to tray)
        this.Loaded += (_, _) => _effectTimer?.Start();
        this.Unloaded += (_, _) => _effectTimer?.Stop();

        // Auto-hide + auto-collapse pop-back triggers
        RootGrid.PointerEntered += (_, _) =>
        {
            RestoreOpacity();
            RestoreBarWidth();
        };
        var hotkeyMgr = App.Current.Services.GetService<HotkeyManager>();
        if (hotkeyMgr is not null)
        {
            hotkeyMgr.HotkeyPressed += (_, _) =>
            {
                DispatcherQueue.TryEnqueue(() => RestoreOpacity());
            };
        }
    }

    /// <summary>
    /// Derives base/bright glow colors from the current theme palette.
    /// Called on init and whenever ThemeService applies a new theme.
    /// Bright colors are computed by lerping each base color 60% toward the accent color.
    /// </summary>
    private void LoadThemeColors()
    {
        var palette = Services.ThemeService.GetPalette(_themeService?.CurrentTheme ?? "Midnight");

        // Base colors from palette
        _baseBgR = palette.Background.R; _baseBgG = palette.Background.G; _baseBgB = palette.Background.B;
        _baseHdrR = palette.Surface.R; _baseHdrG = palette.Surface.G; _baseHdrB = palette.Surface.B;
        _baseBrdR = palette.Border.R; _baseBrdG = palette.Border.G; _baseBrdB = palette.Border.B;
        _baseTxtR = palette.Text.R; _baseTxtG = palette.Text.G; _baseTxtB = palette.Text.B;
        _baseTxt2R = palette.TextDim.R; _baseTxt2G = palette.TextDim.G; _baseTxt2B = palette.TextDim.B;
        _baseGrnR = palette.PerfGreen.R; _baseGrnG = palette.PerfGreen.G; _baseGrnB = palette.PerfGreen.B;

        // Bright colors: lerp 60% toward accent color for glow effect
        var a = palette.Accent;
        _brightBgR = Lerp(_baseBgR, a.R); _brightBgG = Lerp(_baseBgG, a.G); _brightBgB = Lerp(_baseBgB, a.B);
        _brightHdrR = Lerp(_baseHdrR, a.R); _brightHdrG = Lerp(_baseHdrG, a.G); _brightHdrB = Lerp(_baseHdrB, a.B);
        _brightBrdR = Lerp(_baseBrdR, a.R); _brightBrdG = Lerp(_baseBrdG, a.G); _brightBrdB = Lerp(_baseBrdB, a.B);
        // Text brightens toward white
        _brightTxtR = 255; _brightTxtG = 255; _brightTxtB = 255;
        _brightTxt2R = 255; _brightTxt2G = 255; _brightTxt2B = 255;
        _brightGrnR = 255; _brightGrnG = 255; _brightGrnB = 255;

        // Update header bar brush to match new surface color
        if (_headerBarBrush is not null)
        {
            _headerBarBrush.Color = Color.FromArgb(255, _baseHdrR, _baseHdrG, _baseHdrB);
        }

        // Update glassmorphic gradient overlay for light/dark theme
        if (palette.IsDark)
        {
            GlassStop1.Color = Color.FromArgb(0x00, 0x16, 0x15, 0x30);
            GlassStop2.Color = Color.FromArgb(0x30, 0x66, 0x33, 0x99);
            GlassStop3.Color = Color.FromArgb(0x20, 0x99, 0x33, 0x66);
            GlassStop4.Color = Color.FromArgb(0x00, 0x16, 0x15, 0x30);
        }
        else
        {
            GlassStop1.Color = Color.FromArgb(0x00, 0xC0, 0xD0, 0xFF);
            GlassStop2.Color = Color.FromArgb(0x18, 0x80, 0x90, 0xE0);
            GlassStop3.Color = Color.FromArgb(0x10, 0xA0, 0x80, 0xD0);
            GlassStop4.Color = Color.FromArgb(0x00, 0xC0, 0xD0, 0xFF);
        }

        static byte Lerp(byte from, byte to) => (byte)(from + (int)((to - from) * 0.6));
    }

    private void OnEffectTimerTick(DispatcherQueueTimer sender, object args)
    {
        _tickCount++;

        // Two-stage idle: collapse + hide. Runs independently of visual effects.
        TickIdleBehavior();
        TickCollapseAnimation();

        if (!ViewModel.VisualEffectsEnabled)
        {
            ResetBackgrounds();
            HideShimmer();
            return;
        }

        bool wholeApp = ViewModel.VisualEffectsWholeApp;
        double intensity = ViewModel.VisualEffectsIntensity;
        bool monitorActive = _levelMonitor?.IsActive == true;

        // Drive Recording state from AudioLevelMonitor (AudioRecorder is Transient,
        // so ControlPanelVM's own recorder events never fire)
        if (monitorActive && !_wasMonitorActive)
        {
            ViewModel.CurrentState = PipelineState.Recording;
            ViewModel.StatusText = _loc?.GetString("ControlPanel_State_Listening") ?? "LISTENING";
        }
        else if (!monitorActive && _wasMonitorActive && ViewModel.CurrentState == PipelineState.Recording)
        {
            // Only reset if pipeline hasn't already moved to a later state (Transcribing, etc.)
            ViewModel.CurrentState = PipelineState.Idle;
            ViewModel.StatusText = _loc?.GetString("ControlPanel_State_Ready") ?? "READY";
        }
        _wasMonitorActive = monitorActive;

        var state = ViewModel.CurrentState;
        bool isRecording = state == PipelineState.Recording || monitorActive;

        // Debug: log every ~1 second (30 ticks at 33ms)
        if (_tickCount % 30 == 0)
        {
            float rawLevel = _levelMonitor?.SmoothedLevel ?? -1f;
            Log.Debug("[VFX] Tick#{Tick}: state={State} monitorActive={MonitorActive} isRecording={IsRecording} rawLevel={RawLevel:F3} glowLevel={GlowLevel:F3} enabled={Enabled} wholeApp={WholeApp} intensity={Intensity:F2}",
                _tickCount, state, monitorActive, isRecording, rawLevel, _currentGlowLevel, ViewModel.VisualEffectsEnabled, wholeApp, intensity);
        }

        if (isRecording)
        {
            UpdateGlow(wholeApp, intensity);
            FadeShimmer();
            UpdateWaveform();
        }
        else if (state is PipelineState.Transcribing
            or PipelineState.Processing
            or PipelineState.Injecting
            or PipelineState.Speaking
            or PipelineState.Streaming)
        {
            FadeGlow(wholeApp);
            UpdateShimmer(wholeApp);
            UpdateWaveform();
        }
        else
        {
            FadeGlow(wholeApp);
            FadeShimmer();
            FadeWaveform();
        }
    }

    // ── Two-stage idle: collapse + hide ──────────────────────────────────

    private void TickIdleBehavior()
    {
        bool hideEnabled = ViewModel.AutoHideEnabled;
        int hideDelay = ViewModel.AutoHideDelaySeconds;
        bool collapseEnabled = ViewModel.AutoCollapseEnabled;
        int collapseDelay = ViewModel.AutoCollapseDelaySeconds;

        // If both features are completely disabled, reset state
        bool anyEnabled = (hideEnabled && hideDelay > 0) || collapseEnabled;
        if (!anyEnabled)
        {
            if (_currentOpacity < 255)
            {
                RestoreOpacity();
            }
            _idleTicks = 0;
            _isFadingOut = false;
            return;
        }

        // Activity detection: any non-idle state or active recording
        bool isActive = ViewModel.CurrentState != PipelineState.Idle
            || (_levelMonitor?.IsActive == true);

        if (isActive)
        {
            // Activity restores OPACITY only, NOT width (hover-only expands)
            if (_currentOpacity < 255)
            {
                RestoreOpacity();
            }
            _idleTicks = 0;
            _isFadingOut = false;
            return;
        }

        // Count idle ticks
        _idleTicks++;

        // Stage 1: Collapse (only when bar mode + collapse enabled)
        if (collapseEnabled && !ViewModel.IsExpanded)
        {
            int collapseThreshold = collapseDelay * 30;
            if (_idleTicks >= collapseThreshold && !_isBarCollapsed && !_isBarCollapsing)
            {
                _isBarCollapsing = true;
                _isBarExpanding = false;
            }
        }

        // Stage 2: Hide (existing fade logic)
        if (hideEnabled && hideDelay > 0)
        {
            int hideThreshold = hideDelay * 30;
            if (_idleTicks >= hideThreshold)
            {
                _isFadingOut = true;
            }
        }

        if (_isFadingOut && _currentOpacity > 5)
        {
            _currentOpacity = (byte)Math.Max(5, _currentOpacity - 8);
            var mainWindow = App.Current.MainWindow as MainWindow;
            mainWindow?.SetOpacity(_currentOpacity);
        }
    }

    private void RestoreOpacity()
    {
        if (_currentOpacity < 255)
        {
            _currentOpacity = 255;
            var mainWindow = App.Current.MainWindow as MainWindow;
            mainWindow?.SetOpacity(255);
        }
        _idleTicks = 0;
        _isFadingOut = false;
    }

    /// <summary>
    /// Resets auto-hide and auto-collapse state. Called when the window is shown
    /// from the system tray to ensure the bar is fully visible and not faded.
    /// </summary>
    public void ResetAutoHideState()
    {
        RestoreOpacity();
        RestoreBarWidth();
    }

    // ── Collapse/expand animation ─────────────────────────────────────

    private void TickCollapseAnimation()
    {
        if (_isBarCollapsing)
        {
            _collapseAnimProgress = Math.Min(1.0, _collapseAnimProgress + 1.0 / CollapseAnimationTicks);
            double t = EaseOut(_collapseAnimProgress);
            _currentWidth = FullWidth - (FullWidth - CollapsedWidth) * t;

            // Fade HeaderButtons (disappear in first 60% of animation)
            double fadeT = Math.Min(1.0, _collapseAnimProgress / 0.6);
            HeaderButtons.Opacity = 1.0 - fadeT;

            ApplyWindowWidth((int)_currentWidth);

            if (_collapseAnimProgress >= 1.0)
            {
                _isBarCollapsing = false;
                _isBarCollapsed = true;
                HeaderButtons.Visibility = Visibility.Collapsed;
            }
        }
        else if (_isBarExpanding)
        {
            _collapseAnimProgress = Math.Max(0.0, _collapseAnimProgress - 1.0 / CollapseAnimationTicks);
            double expandProgress = 1.0 - _collapseAnimProgress;
            double t = EaseOut(expandProgress);
            _currentWidth = CollapsedWidth + (FullWidth - CollapsedWidth) * t;

            // Fade HeaderButtons back in (appear after 40% of expand)
            double fadeT = Math.Max(0.0, (expandProgress - 0.4) / 0.6);
            HeaderButtons.Opacity = fadeT;

            ApplyWindowWidth((int)_currentWidth);

            if (_collapseAnimProgress <= 0.0)
            {
                _isBarExpanding = false;
                _isBarCollapsed = false;
            }
        }
    }

    private void ApplyWindowWidth(int widthDips)
    {
        var window = App.Current.MainWindow;
        if (window is null)
        {
            return;
        }

        double scale = XamlRoot?.RasterizationScale ?? 1.0;
        int physicalWidth = (int)(widthDips * scale);

        var appWindow = window.AppWindow;
        var current = appWindow.Size;

        if (Math.Abs(current.Width - physicalWidth) <= 1)
        {
            return;
        }

        appWindow.Resize(new SizeInt32(physicalWidth, current.Height));
    }

    private void RestoreBarWidth()
    {
        if (_isBarCollapsed || _isBarCollapsing)
        {
            _isBarExpanding = true;
            _isBarCollapsing = false;
            HeaderButtons.Visibility = Visibility.Visible;
            _idleTicks = 0;
        }
    }

    private static double EaseOut(double t) => 1.0 - Math.Pow(1.0 - t, 3);

    // ── Glow effects ─────────────────────────────────────────────────────

    private void UpdateGlow(bool wholeApp, double intensity)
    {
        float level = _levelMonitor?.SmoothedLevel ?? 0f;

        // Square-root curve compresses dynamic range so normal speech (~0.1–0.3 peak)
        // produces clearly visible brightness, while loud peaks still hit the ceiling.
        // sqrt(0.1)=0.32, sqrt(0.3)=0.55, sqrt(0.5)=0.71, sqrt(1.0)=1.0
        double curved = Math.Sqrt(level);

        // Intensity slider controls the ceiling:
        // 0% → 10% max brightness (subtle glow), 100% → 90% (hardcore)
        double maxBrightness = 0.10 + intensity * 0.80;
        _currentGlowLevel = Math.Clamp(curved * maxBrightness, 0.0, 1.0);

        ApplyGlowToBackground(wholeApp, _currentGlowLevel);

        // Edge glow: highlight proportional to voice level
        EdgeGlow.Opacity = _currentGlowLevel * 0.8;

        // Status text breathes with voice level (1.0 → 1.08 scale, GPU-composited)
        double scale = 1.0 + _currentGlowLevel * 0.08;
        StatusTextScale.ScaleX = scale;
        StatusTextScale.ScaleY = scale;
    }

    private void ApplyGlowToBackground(bool wholeApp, double t)
    {
        // Always modulate text brightness — text reacts regardless of scope
        if (_textPrimary is not null)
        {
            _textPrimary.Color = LerpColor(_baseTxtR, _baseTxtG, _baseTxtB, _brightTxtR, _brightTxtG, _brightTxtB, t);
        }
        if (_textSecondary is not null)
        {
            _textSecondary.Color = LerpColor(_baseTxt2R, _baseTxt2G, _baseTxt2B, _brightTxt2R, _brightTxt2G, _brightTxt2B, t);
        }
        if (_perfGreen is not null)
        {
            _perfGreen.Color = LerpColor(_baseGrnR, _baseGrnG, _baseGrnB, _brightGrnR, _brightGrnG, _brightGrnB, t);
        }

        if (wholeApp)
        {
            // Modulate all three teal tones — every element using these brushes updates automatically
            if (_bgBrush is not null)
            {
                _bgBrush.Color = LerpColor(_baseBgR, _baseBgG, _baseBgB, _brightBgR, _brightBgG, _brightBgB, t);
            }
            if (_hdrBrush is not null)
            {
                _hdrBrush.Color = LerpColor(_baseHdrR, _baseHdrG, _baseHdrB, _brightHdrR, _brightHdrG, _brightHdrB, t);
            }
            if (_borderBrush is not null)
            {
                _borderBrush.Color = LerpColor(_baseBrdR, _baseBrdG, _baseBrdB, _brightBrdR, _brightBrdG, _brightBrdB, t);
            }
            // Ensure HeaderBar uses the shared brush in whole-app mode
            HeaderBar.Background = _hdrBrush;
        }
        else
        {
            // Header only — keep shared brushes at base (affects all rows),
            // use dedicated brush for HeaderBar to glow only the top bar
            if (_bgBrush is not null)
            {
                _bgBrush.Color = Color.FromArgb(255, _baseBgR, _baseBgG, _baseBgB);
            }
            if (_hdrBrush is not null)
            {
                _hdrBrush.Color = Color.FromArgb(255, _baseHdrR, _baseHdrG, _baseHdrB);
            }
            if (_borderBrush is not null)
            {
                _borderBrush.Color = Color.FromArgb(255, _baseBrdR, _baseBrdG, _baseBrdB);
            }
            // Modulate the dedicated header brush — only HeaderBar glows
            if (_headerBarBrush is not null)
            {
                _headerBarBrush.Color = LerpColor(_baseHdrR, _baseHdrG, _baseHdrB, _brightHdrR, _brightHdrG, _brightHdrB, t);
                HeaderBar.Background = _headerBarBrush;
            }
        }
    }

    private static Color LerpColor(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2, double t)
    {
        return Color.FromArgb(
            255,
            (byte)(r1 + (r2 - r1) * t),
            (byte)(g1 + (g2 - g1) * t),
            (byte)(b1 + (b2 - b1) * t));
    }

    private void FadeGlow(bool wholeApp)
    {
        if (_currentGlowLevel < 0.001)
        {
            return;
        }

        _currentGlowLevel *= 0.85;
        if (_currentGlowLevel < 0.005)
        {
            _currentGlowLevel = 0;
        }

        ApplyGlowToBackground(wholeApp, _currentGlowLevel);

        EdgeGlow.Opacity *= 0.85;
        if (EdgeGlow.Opacity < 0.01)
        {
            EdgeGlow.Opacity = 0;
        }

        // Fade text scale back to 1.0
        double scale = 1.0 + _currentGlowLevel * 0.08;
        StatusTextScale.ScaleX = scale;
        StatusTextScale.ScaleY = scale;
    }

    private void ResetBackgrounds()
    {
        _currentGlowLevel = 0;
        if (_bgBrush is not null)
        {
            _bgBrush.Color = Color.FromArgb(255, _baseBgR, _baseBgG, _baseBgB);
        }
        if (_hdrBrush is not null)
        {
            _hdrBrush.Color = Color.FromArgb(255, _baseHdrR, _baseHdrG, _baseHdrB);
        }
        if (_borderBrush is not null)
        {
            _borderBrush.Color = Color.FromArgb(255, _baseBrdR, _baseBrdG, _baseBrdB);
        }
        if (_textPrimary is not null)
        {
            _textPrimary.Color = Color.FromArgb(255, _baseTxtR, _baseTxtG, _baseTxtB);
        }
        if (_textSecondary is not null)
        {
            _textSecondary.Color = Color.FromArgb(255, _baseTxt2R, _baseTxt2G, _baseTxt2B);
        }
        if (_perfGreen is not null)
        {
            _perfGreen.Color = Color.FromArgb(255, _baseGrnR, _baseGrnG, _baseGrnB);
        }
        // Restore header to shared brush
        HeaderBar.Background = _hdrBrush;
        EdgeGlow.Opacity = 0;
        StatusTextScale.ScaleX = 1.0;
        StatusTextScale.ScaleY = 1.0;
    }

    private void UpdateShimmer(bool wholeApp)
    {
        // Advance shimmer phase (one full sweep per ~0.7 seconds at 30fps = 21 ticks)
        _shimmerPhase += 1.0 / 21.0;
        if (_shimmerPhase > 1.0)
        {
            _shimmerPhase -= 1.0;
        }

        // Map phase to gradient stop offsets (narrow bright band sweeps left to right)
        double bandWidth = 0.2;
        double center = _shimmerPhase * (1.0 + bandWidth * 2) - bandWidth;

        double s1 = Math.Clamp(center - bandWidth, 0.0, 1.0);
        double s2 = Math.Clamp(center, 0.0, 1.0);
        double s3 = Math.Clamp(center + bandWidth, 0.0, 1.0);

        if (wholeApp)
        {
            ShimmerOverlayFull.Opacity = 1.0;
            ShimmerOverlayHeader.Opacity = 0;
            ShimmerStop1Full.Offset = s1;
            ShimmerStop2Full.Offset = s2;
            ShimmerStop3Full.Offset = s3;
        }
        else
        {
            ShimmerOverlayFull.Opacity = 0;
            ShimmerOverlayHeader.Opacity = 1.0;
            ShimmerStop1Header.Offset = s1;
            ShimmerStop2Header.Offset = s2;
            ShimmerStop3Header.Offset = s3;
        }
    }

    private void FadeShimmer()
    {
        ShimmerOverlayFull.Opacity *= 0.9;
        ShimmerOverlayHeader.Opacity *= 0.9;
        if (ShimmerOverlayFull.Opacity < 0.01)
        {
            ShimmerOverlayFull.Opacity = 0;
        }
        if (ShimmerOverlayHeader.Opacity < 0.01)
        {
            ShimmerOverlayHeader.Opacity = 0;
        }
    }

    private void HideShimmer()
    {
        ShimmerOverlayFull.Opacity = 0;
        ShimmerOverlayHeader.Opacity = 0;
    }

    // ── Waveform engine ────────────────────────────────────────────────

    private void InitializeWaveformBars()
    {
        var grid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        // Create columns for even spacing across full width
        for (int i = 0; i < WaveformBarCount; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }
        _barElements = new Rectangle[WaveformBarCount];
        var accentBrush = (SolidColorBrush)Application.Current.Resources["AppAccentBrush"];
        for (int i = 0; i < WaveformBarCount; i++)
        {
            _barElements[i] = new Rectangle
            {
                Width = 3,
                Height = 2,
                RadiusX = 1,
                RadiusY = 1,
                Fill = accentBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
            };
            Grid.SetColumn(_barElements[i], i);
            grid.Children.Add(_barElements[i]);
        }
        WaveformBars.Content = grid;
    }

    private void UpdateWaveform()
    {
        _waveformStyleCached = ViewModel.WaveformStyle;
        if (string.Equals(_waveformStyleCached, "Off", StringComparison.Ordinal))
        {
            return;
        }

        float level = _levelMonitor?.SmoothedLevel ?? 0f;
        _waveformTargetAmplitude = Math.Sqrt(level);

        // Smooth toward target — very fast attack for snappy voice reactivity, moderate decay
        if (_waveformTargetAmplitude > _waveformAmplitude)
        {
            _waveformAmplitude += (_waveformTargetAmplitude - _waveformAmplitude) * 0.75;
        }
        else
        {
            _waveformAmplitude += (_waveformTargetAmplitude - _waveformAmplitude) * 0.35;
        }

        // Fade in quickly
        _waveformOpacity = Math.Min(1.0, _waveformOpacity + 0.15);

        if (string.Equals(_waveformStyleCached, "Wave", StringComparison.Ordinal))
        {
            UpdateWaveformSine();
        }
        else if (string.Equals(_waveformStyleCached, "Bars", StringComparison.Ordinal))
        {
            UpdateWaveformBars();
        }
    }

    private void UpdateWaveformSine()
    {
        WaveformLine.Opacity = _waveformOpacity * 0.6;
        WaveformBars.Opacity = 0;

        _waveformPhase += 0.15; // faster phase advance for more visible movement

        // Use WaveformContainer dimensions — it spans the full header area (negative margin cancels padding)
        double width = WaveformContainer.ActualWidth > 0 ? WaveformContainer.ActualWidth : _currentWidth;
        double height = WaveformContainer.ActualHeight > 0 ? WaveformContainer.ActualHeight : 34;
        double centerY = height / 2.0;
        // Use 80% of half-height so the wave nearly touches top and bottom edges
        double maxHeight = centerY * 0.8;

        var points = WaveformLine.Points;
        points.Clear();
        for (int i = 0; i < WaveformPoints; i++)
        {
            double t = (double)i / (WaveformPoints - 1);
            double x = t * width;
            double sine = Math.Sin(t * WaveformFrequency * 2 * Math.PI + _waveformPhase);
            double y = centerY + sine * maxHeight * _waveformAmplitude;
            points.Add(new Windows.Foundation.Point(x, y));
        }
    }

    private void UpdateWaveformBars()
    {
        WaveformLine.Opacity = 0;
        WaveformBars.Opacity = _waveformOpacity * 0.7;

        if (_barElements is null)
        {
            return;
        }

        double containerHeight = WaveformContainer.ActualHeight > 0 ? WaveformContainer.ActualHeight : 34;
        double maxBarHeight = containerHeight * 0.85; // fill nearly all the bar height
        _waveformPhase += 0.12; // faster phase for snappier ripple

        for (int i = 0; i < WaveformBarCount; i++)
        {
            double barPhase = (double)i / WaveformBarCount * Math.PI * 2 + _waveformPhase;
            double barLevel = (Math.Sin(barPhase) * 0.5 + 0.5) * _waveformAmplitude;
            _barElements[i].Height = Math.Max(2, barLevel * maxBarHeight);
        }
    }

    private void FadeWaveform()
    {
        if (_waveformOpacity < 0.01)
        {
            WaveformLine.Opacity = 0;
            WaveformBars.Opacity = 0;
            return;
        }

        _waveformOpacity *= 0.85;
        _waveformAmplitude *= 0.8;

        if (string.Equals(_waveformStyleCached, "Wave", StringComparison.Ordinal))
        {
            UpdateWaveformSine();
        }
        else if (string.Equals(_waveformStyleCached, "Bars", StringComparison.Ordinal))
        {
            UpdateWaveformBars();
        }
    }
}
