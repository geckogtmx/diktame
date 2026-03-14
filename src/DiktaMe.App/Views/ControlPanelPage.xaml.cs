
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

    // Cached brush references from Page.Resources — mutating .Color updates ALL elements
    private SolidColorBrush? _bgBrush;       // V1BackgroundBrush    #002029
    private SolidColorBrush? _hdrBrush;      // V1HeaderBrush        #00303d
    private SolidColorBrush? _borderBrush;   // V1BorderBrush        #004052
    private SolidColorBrush? _textPrimary;   // V1TextPrimaryBrush   #e0e0e0
    private SolidColorBrush? _textSecondary; // V1TextSecondaryBrush #888888
    private SolidColorBrush? _perfGreen;     // V1PerfGreenBrush     #7aff9e

    // Dedicated brush for header-only glow — V1HeaderBrush is shared across all rows,
    // so "Top Bar Only" needs a separate brush applied directly to HeaderBar.Background
    private SolidColorBrush? _headerBarBrush;

    // Base colors (idle) → Bright colors (max glow)
    // V1BackgroundBrush: #002029 → #00A0C0 (dark teal → bright cyan)
    private const byte BaseBgR = 0, BaseBgG = 32, BaseBgB = 41;
    private const byte BrightBgR = 0, BrightBgG = 160, BrightBgB = 192;

    // V1HeaderBrush: #00303d → #00B8D8 (dark teal → vivid cyan)
    private const byte BaseHdrR = 0, BaseHdrG = 48, BaseHdrB = 61;
    private const byte BrightHdrR = 0, BrightHdrG = 184, BrightHdrB = 216;

    // V1BorderBrush: #004052 → #00D0F0 (medium teal → near-white cyan)
    private const byte BaseBrdR = 0, BaseBrdG = 64, BaseBrdB = 82;
    private const byte BrightBrdR = 0, BrightBrdG = 208, BrightBrdB = 240;

    // V1TextPrimaryBrush: #e0e0e0 → #ffffff (stays the same — already near white)
    private const byte BaseTxtR = 224, BaseTxtG = 224, BaseTxtB = 224;
    private const byte BrightTxtR = 255, BrightTxtG = 255, BrightTxtB = 255;

    // V1TextSecondaryBrush: #888888 → #ffffff (dim gray → full white)
    private const byte BaseTxt2R = 136, BaseTxt2G = 136, BaseTxt2B = 136;
    private const byte BrightTxt2R = 255, BrightTxt2G = 255, BrightTxt2B = 255;

    // V1PerfGreenBrush: #7aff9e → #ffffff (green → white-hot at peak)
    private const byte BaseGrnR = 122, BaseGrnG = 255, BaseGrnB = 158;
    private const byte BrightGrnR = 255, BrightGrnG = 255, BrightGrnB = 255;

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

        double scale = XamlRoot?.RasterizationScale ?? 1.0;
        int physicalWidth = (int)(420 * scale);
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

        // Cache brush references — changing .Color on these updates every element that uses them
        _bgBrush = (SolidColorBrush)this.Resources["V1BackgroundBrush"];
        _hdrBrush = (SolidColorBrush)this.Resources["V1HeaderBrush"];
        _borderBrush = (SolidColorBrush)this.Resources["V1BorderBrush"];
        _textPrimary = (SolidColorBrush)this.Resources["V1TextPrimaryBrush"];
        _textSecondary = (SolidColorBrush)this.Resources["V1TextSecondaryBrush"];
        _perfGreen = (SolidColorBrush)this.Resources["V1PerfGreenBrush"];

        // Dedicated brush for header-only glow (starts at same color as V1HeaderBrush)
        _headerBarBrush = new SolidColorBrush(Color.FromArgb(255, BaseHdrR, BaseHdrG, BaseHdrB));

        _effectTimer = DispatcherQueue.CreateTimer();
        _effectTimer.Interval = TimeSpan.FromMilliseconds(33); // ~30fps
        _effectTimer.Tick += OnEffectTimerTick;
        _effectTimer.Start();

        // Pause timer when page is not visible (e.g., window hidden to tray)
        this.Loaded += (_, _) => _effectTimer?.Start();
        this.Unloaded += (_, _) => _effectTimer?.Stop();

        // Auto-hide pop-back triggers
        RootGrid.PointerEntered += (_, _) => RestoreOpacity();
        var hotkeyMgr = App.Current.Services.GetService<HotkeyManager>();
        if (hotkeyMgr is not null)
        {
            hotkeyMgr.HotkeyPressed += (_, _) =>
            {
                DispatcherQueue.TryEnqueue(() => RestoreOpacity());
            };
        }
    }

    private void OnEffectTimerTick(DispatcherQueueTimer sender, object args)
    {
        _tickCount++;

        // Auto-hide: runs independently of visual effects
        TickAutoHide();

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
        }
        else if (state is PipelineState.Transcribing
            or PipelineState.Processing
            or PipelineState.Injecting
            or PipelineState.Speaking
            or PipelineState.Streaming)
        {
            FadeGlow(wholeApp);
            UpdateShimmer(wholeApp);
        }
        else
        {
            FadeGlow(wholeApp);
            FadeShimmer();
        }
    }

    // ── Auto-hide (window fade) ──────────────────────────────────────────

    private void TickAutoHide()
    {
        bool enabled = ViewModel.AutoHideEnabled;
        int delaySeconds = ViewModel.AutoHideDelaySeconds;

        // Disabled or "Never" (0) — ensure fully opaque
        if (!enabled || delaySeconds <= 0)
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

        // Threshold: delay in seconds × 30 ticks/second
        int threshold = delaySeconds * 30;
        if (_idleTicks >= threshold)
        {
            _isFadingOut = true;
        }

        if (_isFadingOut && _currentOpacity > 5)
        {
            // Fade out: decrement alpha by 8 per tick (~32 ticks = ~1s)
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
            _textPrimary.Color = LerpColor(BaseTxtR, BaseTxtG, BaseTxtB, BrightTxtR, BrightTxtG, BrightTxtB, t);
        }
        if (_textSecondary is not null)
        {
            _textSecondary.Color = LerpColor(BaseTxt2R, BaseTxt2G, BaseTxt2B, BrightTxt2R, BrightTxt2G, BrightTxt2B, t);
        }
        if (_perfGreen is not null)
        {
            _perfGreen.Color = LerpColor(BaseGrnR, BaseGrnG, BaseGrnB, BrightGrnR, BrightGrnG, BrightGrnB, t);
        }

        if (wholeApp)
        {
            // Modulate all three teal tones — every element using these brushes updates automatically
            if (_bgBrush is not null)
            {
                _bgBrush.Color = LerpColor(BaseBgR, BaseBgG, BaseBgB, BrightBgR, BrightBgG, BrightBgB, t);
            }
            if (_hdrBrush is not null)
            {
                _hdrBrush.Color = LerpColor(BaseHdrR, BaseHdrG, BaseHdrB, BrightHdrR, BrightHdrG, BrightHdrB, t);
            }
            if (_borderBrush is not null)
            {
                _borderBrush.Color = LerpColor(BaseBrdR, BaseBrdG, BaseBrdB, BrightBrdR, BrightBrdG, BrightBrdB, t);
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
                _bgBrush.Color = Color.FromArgb(255, BaseBgR, BaseBgG, BaseBgB);
            }
            if (_hdrBrush is not null)
            {
                _hdrBrush.Color = Color.FromArgb(255, BaseHdrR, BaseHdrG, BaseHdrB);
            }
            if (_borderBrush is not null)
            {
                _borderBrush.Color = Color.FromArgb(255, BaseBrdR, BaseBrdG, BaseBrdB);
            }
            // Modulate the dedicated header brush — only HeaderBar glows
            if (_headerBarBrush is not null)
            {
                _headerBarBrush.Color = LerpColor(BaseHdrR, BaseHdrG, BaseHdrB, BrightHdrR, BrightHdrG, BrightHdrB, t);
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
            _bgBrush.Color = Color.FromArgb(255, BaseBgR, BaseBgG, BaseBgB);
        }
        if (_hdrBrush is not null)
        {
            _hdrBrush.Color = Color.FromArgb(255, BaseHdrR, BaseHdrG, BaseHdrB);
        }
        if (_borderBrush is not null)
        {
            _borderBrush.Color = Color.FromArgb(255, BaseBrdR, BaseBrdG, BaseBrdB);
        }
        if (_textPrimary is not null)
        {
            _textPrimary.Color = Color.FromArgb(255, BaseTxtR, BaseTxtG, BaseTxtB);
        }
        if (_textSecondary is not null)
        {
            _textSecondary.Color = Color.FromArgb(255, BaseTxt2R, BaseTxt2G, BaseTxt2B);
        }
        if (_perfGreen is not null)
        {
            _perfGreen.Color = Color.FromArgb(255, BaseGrnR, BaseGrnG, BaseGrnB);
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
}
