using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using MidFlow.Core.Gestures;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;
using Point = System.Windows.Point;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace MidFlow.App;

public partial class MainWindow : Window
{
    private const int TrailLifetimeMs = 260;
    private const int MaximumTrailPoints = 36;

    private readonly ConcurrentQueue<MouseSample> _samples = new();
    private readonly List<TrailPoint> _trail = new(MaximumTrailPoints);
    private LowLevelMouseHook? _hook;
    private bool _gestureActive;
    private Point _lastPoint;
    private int _pendingSamples;
    private long _lastRenderDiagnostic;
    private int _lastHookX;
    private int _lastHookY;
    private long _lastSampleTimestamp;
    private string _foregroundProcessName = string.Empty;
    private int _foregroundProcessId;
    private readonly Drawing.Icon _trayIconImage;
    private readonly System.Windows.Threading.DispatcherTimer _foregroundTimer;
    private readonly Forms.NotifyIcon _trayIcon;
    private readonly Forms.ToolStripMenuItem _enabledMenuItem;
    private readonly Forms.ToolStripMenuItem _trailMenuItem;

    public MainWindow()
    {
        InitializeComponent();
        DiagnosticLog.Start();
        _foregroundTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _foregroundTimer.Tick += (_, _) => UpdateForegroundProcess();

        _enabledMenuItem = new Forms.ToolStripMenuItem("Enable MMB gestures")
        {
            Checked = AppSettings.GesturesEnabled,
            CheckOnClick = true
        };
        _enabledMenuItem.CheckedChanged += (_, _) =>
        {
            AppSettings.GesturesEnabled = _enabledMenuItem.Checked;
            AppSettings.Save();
        };

        _trailMenuItem = new Forms.ToolStripMenuItem("Show cursor trail")
        {
            Checked = AppSettings.ShowTrail,
            CheckOnClick = true
        };
        _trailMenuItem.CheckedChanged += (_, _) =>
        {
            AppSettings.ShowTrail = _trailMenuItem.Checked;
            AppSettings.Save();
            ApplySettings();
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(_enabledMenuItem);
        menu.Items.Add(_trailMenuItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Settings...", null, (_, _) => OpenSettings());
        menu.Items.Add("Open log folder", null, (_, _) => AppSettings.OpenLogFolder());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit MidFlow", null, (_, _) => Application.Current.Shutdown());
        var iconPath = Path.Combine(AppContext.BaseDirectory, "MidFlow.ico");
        _trayIconImage = File.Exists(iconPath)
            ? new Drawing.Icon(iconPath)
            : (Drawing.Icon)Drawing.SystemIcons.Application.Clone();
        _trayIcon = new Forms.NotifyIcon
        {
            Icon = _trayIconImage,
            Text = "MidFlow — middle-button gestures",
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => OpenSettings();
        AppSettings.Changed += ApplySettings;

        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        SourceInitialized += (_, _) => MakeClickThrough();
        Loaded += OnLoaded;
        Loaded += (_, _) => { ApplySettings(); _foregroundTimer.Start(); UpdateForegroundProcess(); };
        Closed += (_, _) =>
        {
            CompositionTarget.Rendering -= OnCompositionRendering;
            _foregroundTimer.Stop();
            AppSettings.Changed -= ApplySettings;
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIconImage.Dispose();
            _hook?.Dispose();
        };
        Loaded += (_, _) => CompositionTarget.Rendering += OnCompositionRendering;
    }

    private void UpdateForegroundProcess()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;
        NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
        if (processId == 0 || processId == _foregroundProcessId) return;
        _foregroundProcessId = (int)processId;
        try
        {
            Volatile.Write(ref _foregroundProcessName, Process.GetProcessById((int)processId).ProcessName);
        }
        catch
        {
            Volatile.Write(ref _foregroundProcessName, string.Empty);
        }
    }

    private void ApplySettings()
    {
        TrailSurface.Visibility = AppSettings.ShowTrail ? Visibility.Visible : Visibility.Hidden;
        if (_enabledMenuItem.Checked != AppSettings.GesturesEnabled)
            _enabledMenuItem.Checked = AppSettings.GesturesEnabled;
        if (_trailMenuItem.Checked != AppSettings.ShowTrail)
            _trailMenuItem.Checked = AppSettings.ShowTrail;
    }

    private void OpenSettings()
    {
        var settings = new SettingsWindow();
        settings.ShowDialog();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _hook = new LowLevelMouseHook(sample =>
            {
                Interlocked.Increment(ref _pendingSamples);
                _samples.Enqueue(sample);
            }, () => AppSettings.IsEnabledForProcess(Volatile.Read(ref _foregroundProcessName)),
                process => AppSettings.ShortcutsForProcess(process),
                process => AppSettings.GesturesForProcess(process),
                () => Volatile.Read(ref _foregroundProcessName));
            _hook.Install();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"MidFlow could not install its mouse hook.\n\n{ex.Message}",
                "MidFlow", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
            Application.Current.Shutdown();
        }
    }

    private void MakeClickThrough()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GwlExStyle);
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GwlExStyle,
            exStyle | NativeMethods.WsExTransparent |
            NativeMethods.WsExToolWindow | NativeMethods.WsExNoActivate);
    }

    private void OnCompositionRendering(object? sender, EventArgs e)
    {
        var frameStarted = Stopwatch.GetTimestamp();
        var changed = false;
        while (_samples.TryDequeue(out var sample))
        {
            Interlocked.Decrement(ref _pendingSamples);
            _lastHookX = sample.X;
            _lastHookY = sample.Y;
            _lastSampleTimestamp = sample.Timestamp;
            var local = PointFromScreen(new Point(sample.X, sample.Y));
            switch (sample.Kind)
            {
                case MouseEventKind.MiddleDown:
                    _gestureActive = true;
                    TrailSurface.SetDirection(GestureDirection.None);
                    _lastPoint = local;
                    _trail.Clear();
                    _trail.Add(new TrailPoint(local, sample.Timestamp));
                    changed = true;
                    break;

                case MouseEventKind.Move when _gestureActive:
                    var dx = local.X - _lastPoint.X;
                    var dy = local.Y - _lastPoint.Y;
                    if ((dx * dx) + (dy * dy) >= 1.0)
                    {
                        _trail.Add(new TrailPoint(local, sample.Timestamp));
                        _lastPoint = local;
                        if (_trail.Count > MaximumTrailPoints)
                            _trail.RemoveAt(0);
                        changed = true;
                    }
                    break;

                case MouseEventKind.MiddleUp:
                    if (_gestureActive)
                    {
                        _trail.Add(new TrailPoint(local, sample.Timestamp));
                        while (_trail.Count > MaximumTrailPoints)
                            _trail.RemoveAt(0);
                    }
                    _gestureActive = false;
                    changed = true;
                    break;

                case MouseEventKind.GestureFeedback:
                    TrailSurface.SetDirection(sample.Direction);
                    changed = true;
                    break;
            }
        }

        var now = Stopwatch.GetTimestamp();
        var oldCount = _trail.Count;
        _trail.RemoveAll(p =>
            Stopwatch.GetElapsedTime(p.Timestamp, now).TotalMilliseconds > TrailLifetimeMs);
        if (_trail.Count != oldCount)
            changed = true;

        // Re-render while a trail exists so its opacity fades smoothly between samples.
        // With no trail, invalidate only when clearing stale pixels.
        if (_trail.Count > 0 || changed)
            TrailSurface.SetTrail(_trail, now);

        if (_trail.Count > 0)
            TrailSurface.RecordCompositionFrame(frameStarted);

        // Diagnostics are throttled and written by a dedicated logger thread.
        if (_gestureActive && Stopwatch.GetElapsedTime(_lastRenderDiagnostic).TotalMilliseconds >= 250)
        {
            _lastRenderDiagnostic = now;
            var sampleAge = _lastSampleTimestamp == 0 ? -1 :
                Stopwatch.GetElapsedTime(_lastSampleTimestamp, now).TotalMilliseconds;
            DiagnosticLog.Write(
                $"RENDER pos=({_lastHookX},{_lastHookY}) trailPoints={_trail.Count} " +
                $"queue={Volatile.Read(ref _pendingSamples)} lastSampleAgeMs={sampleAge:F1} " +
                $"uiThread={Environment.CurrentManagedThreadId}");
        }
    }

}

internal readonly record struct TrailPoint(Point Position, long Timestamp);
internal enum MouseEventKind { Move, MiddleDown, MiddleUp, GestureFeedback }
internal readonly record struct MouseSample(
    MouseEventKind Kind, int X, int Y, long Timestamp, GestureDirection Direction = GestureDirection.None);
// One retained-mode WPF drawing surface. Pens/brushes are cached and frozen;
// a Catmull-Rom interpolation smooths the sampled pointer path without adding UI controls.
public sealed class TrailSurface : FrameworkElement
{
    private const int FadeBuckets = 16;
    private const int CurveStepsPerSegment = 4;
    private const int TrailLifetimeMs = 260;

    private static readonly Pen[] NeutralPens = CreatePens(Color.FromRgb(105, 170, 255), 220);
    private static readonly Pen[] BackPens = CreatePens(Color.FromRgb(183, 156, 255), 220);
    private static readonly Pen[] ForwardPens = CreatePens(Color.FromRgb(105, 190, 255), 220);
    private static readonly SolidColorBrush[] NeutralTips = CreateBrushes(Color.FromRgb(165, 205, 255), 235);
    private static readonly SolidColorBrush[] BackTips = CreateBrushes(Color.FromRgb(218, 199, 255), 235);
    private static readonly SolidColorBrush[] ForwardTips = CreateBrushes(Color.FromRgb(174, 220, 255), 235);

    private List<TrailPoint> _points = new();
    private long _now;
    private GestureDirection _direction;
    private long _lastFrameLog;
    private int _framesInWindow;
    private double _intervalSumMs;
    private double _intervalMaxMs;
    private long _lastFrameTimestamp;
    private double _renderDurationSumMs;
    private double _renderDurationMaxMs;
    private int _renderCount;

    internal void SetTrail(List<TrailPoint> points, long now)
    {
        // The surface and trail are owned by the WPF dispatcher thread, so retaining
        // the list avoids a per-frame ToList allocation/copy.
        _points = points;
        _now = now;
        InvalidateVisual();
    }

    internal void SetDirection(GestureDirection direction) => _direction = direction;

    protected override void OnRender(DrawingContext dc)
    {
        var renderStarted = Stopwatch.GetTimestamp();
        base.OnRender(dc);
        if (_points.Count == 0) return;

        var pens = _direction switch
        {
            GestureDirection.Left => BackPens,
            GestureDirection.Right => ForwardPens,
            _ => NeutralPens
        };
        var tips = _direction switch
        {
            GestureDirection.Left => BackTips,
            GestureDirection.Right => ForwardTips,
            _ => NeutralTips
        };

        for (var i = 0; i < _points.Count - 1; i++)
        {
            var p1 = _points[i].Position;
            var p2 = _points[i + 1].Position;
            var ageMs = Stopwatch.GetElapsedTime(_points[i + 1].Timestamp, _now).TotalMilliseconds;
            var penIndex = OpacityBucket(ageMs);
            if (penIndex == 0) continue;

            var p0 = i == 0 ? p1 : _points[i - 1].Position;
            var p3 = i + 2 < _points.Count ? _points[i + 2].Position : p2;
            var previous = p1;
            for (var step = 1; step <= CurveStepsPerSegment; step++)
            {
                var t = (double)step / CurveStepsPerSegment;
                var current = CatmullRom(p0, p1, p2, p3, t);
                dc.DrawLine(pens[penIndex], previous, current);
                previous = current;
            }
        }

        var last = _points[^1];
        var tipIndex = OpacityBucket(
            Stopwatch.GetElapsedTime(last.Timestamp, _now).TotalMilliseconds);
        if (tipIndex > 0)
            dc.DrawEllipse(tips[tipIndex], null, last.Position, 3.5, 3.5);

        RecordRenderDuration(Stopwatch.GetElapsedTime(renderStarted).TotalMilliseconds);
    }

    // Called from WPF's composition cycle. This measures frame delivery separately
    // from OnRender work, so a low drawing cost cannot hide a slow frame cadence.
    internal void RecordCompositionFrame(long frameStarted)
    {
        var finished = Stopwatch.GetTimestamp();
        if (_lastFrameTimestamp != 0)
        {
            var intervalMs = Stopwatch.GetElapsedTime(_lastFrameTimestamp, frameStarted).TotalMilliseconds;
            _intervalSumMs += intervalMs;
            _intervalMaxMs = Math.Max(_intervalMaxMs, intervalMs);
        }
        _lastFrameTimestamp = frameStarted;
        _framesInWindow++;

        if (_lastFrameLog == 0 || Stopwatch.GetElapsedTime(_lastFrameLog, finished).TotalSeconds >= 1)
        {
            var averageInterval = _framesInWindow > 1 ? _intervalSumMs / (_framesInWindow - 1) : 0;
            var averageRender = _renderCount > 0 ? _renderDurationSumMs / _renderCount : 0;
            DiagnosticLog.Write(
                $"TRAIL_FRAME compositionFrames={_framesInWindow} avgIntervalMs={averageInterval:F2} " +
                $"maxIntervalMs={_intervalMaxMs:F2} renderCount={_renderCount} " +
                $"avgRenderMs={averageRender:F3} maxRenderMs={_renderDurationMaxMs:F3} points={_points.Count}");
            _lastFrameLog = finished;
            _framesInWindow = 0;
            _intervalSumMs = _intervalMaxMs = 0;
            _renderDurationSumMs = _renderDurationMaxMs = 0;
            _renderCount = 0;
        }
    }

    private void RecordRenderDuration(double renderMs)
    {
        _renderCount++;
        _renderDurationSumMs += renderMs;
        _renderDurationMaxMs = Math.Max(_renderDurationMaxMs, renderMs);
    }

    private static int OpacityBucket(double ageMs)
    {
        var opacity = Math.Clamp(1.0 - ageMs / TrailLifetimeMs, 0.0, 1.0);
        return opacity <= 0.02 ? 0 : Math.Clamp((int)Math.Round(opacity * (FadeBuckets - 1)), 1, FadeBuckets - 1);
    }

    private static Point CatmullRom(Point p0, Point p1, Point p2, Point p3, double t)
    {
        var t2 = t * t;
        var t3 = t2 * t;
        return new Point(
            0.5 * ((2 * p1.X) + (-p0.X + p2.X) * t +
                   (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2 +
                   (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3),
            0.5 * ((2 * p1.Y) + (-p0.Y + p2.Y) * t +
                   (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2 +
                   (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3));
    }

    private static Pen[] CreatePens(Color color, byte maximumAlpha)
    {
        var result = new Pen[FadeBuckets];
        for (var i = 0; i < FadeBuckets; i++)
        {
            var alpha = (byte)(maximumAlpha * i / (FadeBuckets - 1));
            var brush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
            brush.Freeze();
            var pen = new Pen(brush, 4.0)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            pen.Freeze();
            result[i] = pen;
        }
        return result;
    }

    private static SolidColorBrush[] CreateBrushes(Color color, byte maximumAlpha)
    {
        var result = new SolidColorBrush[FadeBuckets];
        for (var i = 0; i < FadeBuckets; i++)
        {
            var alpha = (byte)(maximumAlpha * i / (FadeBuckets - 1));
            var brush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
            brush.Freeze();
            result[i] = brush;
        }
        return result;
    }
}

internal enum GestureAction { Back, Forward, ReplayMiddleClick, MatchCustom }

internal sealed class LowLevelMouseHook : IDisposable
{
    private readonly NativeMethods.LowLevelMouseProc _callback;
    private readonly Action<MouseSample> _publish;
    private readonly Func<bool> _isEnabled;
    private readonly Func<string, MidFlow.Core.Shortcuts.ShortcutPair> _getShortcuts;
    private readonly Func<string, IReadOnlyList<GestureDefinition>> _getGestures;
    private readonly ManualResetEventSlim _ready = new(false);
    private readonly Thread _thread;
    private readonly BlockingCollection<QueuedAction> _actions = new(new ConcurrentQueue<QueuedAction>());
    private readonly Thread _actionThread;
    private IntPtr _handle;
    private uint _threadId;
    private Exception? _startupError;
    private GestureSession? _session;
    private bool _recordGestureSession;
    private long _lastMoveLogTimestamp;
    private int _disposed;
    private readonly Func<string> _getForegroundProcess;

    internal LowLevelMouseHook(Action<MouseSample> publish, Func<bool> isEnabled,
        Func<string, MidFlow.Core.Shortcuts.ShortcutPair> getShortcuts,
        Func<string, IReadOnlyList<GestureDefinition>> getGestures,
        Func<string> getForegroundProcess)
    {
        _publish = publish;
        _isEnabled = isEnabled;
        _getShortcuts = getShortcuts;
        _getGestures = getGestures;
        _getForegroundProcess = getForegroundProcess;
        _callback = Callback;
        _thread = new Thread(HookThreadMain) { IsBackground = true, Name = "MidFlow.InputHook" };
        _actionThread = new Thread(ActionWorkerMain)
        {
            IsBackground = true,
            Name = "MidFlow.GestureActions"
        };
    }

    internal void Install()
    {
        _actionThread.Start();
        _thread.Start();
        _ready.Wait();
        if (_startupError is not null)
        {
            _actions.CompleteAdding();
            _actionThread.Join(1000);
            throw new InvalidOperationException("Mouse hook startup failed.", _startupError);
        }
        DiagnosticLog.Write("SYSTEM actionWorkerStarted=True");
    }

    private void HookThreadMain()
    {
        _threadId = NativeMethods.GetCurrentThreadId();
        NativeMethods.PeekMessage(out _, IntPtr.Zero, 0, 0, NativeMethods.PmNoRemove);
        try
        {
            _handle = NativeMethods.SetWindowsHookEx(NativeMethods.WhMouseLl,
                _callback, NativeMethods.GetModuleHandle(null), 0);
            if (_handle == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    "Unable to install the global mouse hook.");
        }
        catch (Exception ex)
        {
            _startupError = ex;
            _ready.Set();
            return;
        }

        _ready.Set();
        while (NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessage(ref msg);
        }

        if (_handle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_handle);
            _handle = IntPtr.Zero;
        }
    }

    private IntPtr Callback(int code, IntPtr wParam, IntPtr lParam)
    {
        var callbackStarted = Stopwatch.GetTimestamp();
        var result = CallbackCore(code, wParam, lParam);
        var elapsed = Stopwatch.GetElapsedTime(callbackStarted).TotalMilliseconds;
        if (elapsed >= 2.0)
            DiagnosticLog.Write($"HOOK_SLOW callbackMs={elapsed:F2}");
        return result;
    }

    private IntPtr CallbackCore(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code < 0)
            return NativeMethods.CallNextHookEx(_handle, code, wParam, lParam);

        var data = Marshal.PtrToStructure<NativeMethods.MsLlHookStruct>(lParam);
        if ((data.Flags & NativeMethods.LlmhfInjected) != 0)
            return NativeMethods.CallNextHookEx(_handle, code, wParam, lParam);

        // When disabled, pass all physical input through. If a gesture is already
        // active, finish consuming it to avoid an unmatched middle-button release.
        if (_session is null && !_isEnabled() && !AppSettings.GestureCapturePending)
            return NativeMethods.CallNextHookEx(_handle, code, wParam, lParam);

        var message = unchecked((uint)wParam.ToInt64());
        switch (message)
        {
            case NativeMethods.WmMButtonDown:
                if (_session is null)
                {
                    _session = new GestureSession(data.Point.X, data.Point.Y,
                        AppSettings.ClickMovementThresholdPx,
                        AppSettings.MinimumHorizontalMovementPx,
                        AppSettings.MaximumVerticalMovementPx,
                        AppSettings.MaximumGestureDurationMs);
                    _recordGestureSession = AppSettings.TryBeginGestureCaptureSession();
                    Publish(MouseEventKind.MiddleDown, data.Point.X, data.Point.Y);
                    return new IntPtr(1);
                }
                break;

            case NativeMethods.WmMouseMove:
                if (_session is not null)
                {
                    _session.Update(data.Point.X, data.Point.Y);
                    Publish(MouseEventKind.Move, data.Point.X, data.Point.Y);
                }
                break;

            case NativeMethods.WmMButtonUp:
                if (_session is not null)
                {
                    var session = _session;
                    session.Update(data.Point.X, data.Point.Y);
                    _session = null;
                    Publish(MouseEventKind.MiddleUp, data.Point.X, data.Point.Y);

                    if (_recordGestureSession)
                    {
                        _recordGestureSession = false;
                        AppSettings.CompleteGestureCapture(session.GetRecordedPoints());
                        return new IntPtr(1);
                    }

                    var result = session.Evaluate();
                    GestureAction? action = session.HasDragged
                        ? result.Reason == "TooSlow" ? null : GestureAction.MatchCustom
                        : GestureAction.ReplayMiddleClick;

                    if (result.Direction is GestureDirection.Left or GestureDirection.Right)
                        Publish(MouseEventKind.GestureFeedback, data.Point.X, data.Point.Y, result.Direction);

                    // Only a stationary MMB click is replayed. Any pointer movement
                    // beyond click jitter is treated as a drag and remains consumed,
                    // even when it is vertical, diagonal, too short, or too slow.
                    // Never call SendInput from WH_MOUSE_LL; queue actions and return.
                    if (action == GestureAction.MatchCustom)
                    {
                        _actions.TryAdd(new QueuedAction(action.Value,
                            MidFlow.Core.Shortcuts.KeyboardShortcut.Disabled,
                            RawPoints: session.GetRecordedPoints(),
                            ProcessName: _getForegroundProcess(),
                            FallbackDirection: result.Direction));
                    }
                    else if (action == GestureAction.ReplayMiddleClick)
                    {
                        _actions.TryAdd(new QueuedAction(action.Value, MidFlow.Core.Shortcuts.KeyboardShortcut.Disabled));
                    }

                    var actionName = action == GestureAction.MatchCustom ? "CustomMatchQueued" : action?.ToString() ?? "SuppressedDrag";
                    DiagnosticLog.Write(
                        $"GESTURE direction={result.Direction} dx={result.Dx} dy={result.Dy} " +
                        $"dragged={session.HasDragged} durationMs={result.DurationMs:F1} " +
                        $"reason={result.Reason} action={actionName}");
                    return new IntPtr(1);
                }
                break;
        }
        return NativeMethods.CallNextHookEx(_handle, code, wParam, lParam);
    }

    private void ActionWorkerMain()
    {
        foreach (var request in _actions.GetConsumingEnumerable())
        {
            var action = request.Action;
            var started = Stopwatch.GetTimestamp();
            var success = false;
            string error = "";
            try
            {
                success = action switch
                {
                    GestureAction.Back or GestureAction.Forward => NativeMethods.TrySendShortcut(request.Shortcut, out error),
                    GestureAction.ReplayMiddleClick => NativeMethods.TryReplayMiddleClick(out error),
                    GestureAction.MatchCustom => ExecuteCustomGesture(request, out error),
                    _ => false
                };
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            var duration = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            DiagnosticLog.Write(
                $"ACTION_COMPLETE action={action} success={success} durationMs={duration:F2}" +
                (string.IsNullOrWhiteSpace(error) ? "" : $" error={error}"));
        }
    }

    private bool ExecuteCustomGesture(QueuedAction request, out string error)
    {
        error = "No matching custom gesture.";
        var path = request.RawPoints is null ? null : GesturePath.Create(request.RawPoints);
        var processName = request.ProcessName ?? string.Empty;
        var match = path is null
            ? null
            : GestureResolver.Match(path, _getGestures(processName));
        if (match is null)
        {
            var shortcuts = _getShortcuts(processName);
            var fallback = request.FallbackDirection switch
            {
                GestureDirection.Left => shortcuts.Back,
                GestureDirection.Right => shortcuts.Forward,
                _ => null
            };
            if (fallback is { Enabled: true }) return NativeMethods.TrySendShortcut(fallback, out error);
            if (fallback is null) error = "";
            else error = "The resolved direction shortcut is disabled.";
            return false;
        }
        DiagnosticLog.Write($"GESTURE_MATCH id={match.Id} action={match.Action.Kind} process={processName}");
        switch (match.Action.Kind)
        {
            case GestureActionKind.Back:
                var back = _getShortcuts(processName).Back;
                if (!back.Enabled) { error = "Back shortcut is disabled."; return false; }
                return NativeMethods.TrySendShortcut(back, out error);
            case GestureActionKind.Forward:
                var forward = _getShortcuts(processName).Forward;
                if (!forward.Enabled) { error = "Forward shortcut is disabled."; return false; }
                return NativeMethods.TrySendShortcut(forward, out error);
            case GestureActionKind.LaunchApplication:
                var executable = match.Action.ApplicationPath;
                if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
                { error = "The application file no longer exists."; return false; }
                Process.Start(new ProcessStartInfo(executable) { UseShellExecute = false });
                error = "";
                return true;
            default:
                error = "Unsupported gesture action.";
                return false;
        }
    }

    private void Publish(MouseEventKind kind, int x, int y, GestureDirection direction = GestureDirection.None)
    {
        var timestamp = Stopwatch.GetTimestamp();
        _publish(new MouseSample(kind, x, y, timestamp, direction));

        if (kind != MouseEventKind.Move ||
            Stopwatch.GetElapsedTime(Interlocked.Read(ref _lastMoveLogTimestamp), timestamp).TotalMilliseconds >= 50)
        {
            Interlocked.Exchange(ref _lastMoveLogTimestamp, timestamp);
            DiagnosticLog.Write(
                $"HOOK {kind} pos=({x},{y}) thread={Environment.CurrentManagedThreadId} " +
                $"ticks={timestamp}");
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        if (_thread.IsAlive && _threadId != 0)
            NativeMethods.PostThreadMessage(_threadId, NativeMethods.WmQuit, IntPtr.Zero, IntPtr.Zero);
        if (_thread.IsAlive && Thread.CurrentThread != _thread)
            _thread.Join(1000);

        if (_actionThread.IsAlive)
        {
            _actions.CompleteAdding();
            if (Thread.CurrentThread != _actionThread)
                _actionThread.Join(1000);
        }

        _ready.Dispose();
        _actions.Dispose();
        GC.SuppressFinalize(this);
    }
}

internal sealed record QueuedAction(GestureAction Action, MidFlow.Core.Shortcuts.KeyboardShortcut Shortcut,
    IReadOnlyList<GesturePoint>? RawPoints = null, string? ProcessName = null,
    GestureDirection FallbackDirection = GestureDirection.None);

internal static class DiagnosticLog
{
    private static readonly BlockingCollection<string> Queue = new(new ConcurrentQueue<string>());
    private static int _started;
    private static string _logPath = string.Empty;

    internal static void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0) return;
        try
        {
            Directory.CreateDirectory(AppSettings.LogDirectory);
            _logPath = Path.Combine(AppSettings.LogDirectory,
                $"MidFlow-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        }
        catch { _logPath = Path.Combine(Path.GetTempPath(), "MidFlow.log"); }

        var logger = new Thread(() =>
        {
            try
            {
                using var writer = new StreamWriter(_logPath, append: true) { AutoFlush = true };
                writer.WriteLine($"{DateTime.Now:O} SYSTEM MidFlow Phase 7 diagnostics started");
                foreach (var line in Queue.GetConsumingEnumerable())
                    writer.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {line}");
            }
            catch { /* Logging must never interfere with input handling. */ }
        })
        {
            IsBackground = true,
            Name = "MidFlow.Diagnostics"
        };
        logger.Start();
    }

    internal static void Write(string message)
    {
        if (Volatile.Read(ref _started) == 0) return;
        try { Queue.TryAdd(message); } catch { }
    }
}

internal static class NativeMethods
{
    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    internal const int WhMouseLl = 14;
    internal const uint WmMouseMove = 0x0200, WmMButtonDown = 0x0207, WmMButtonUp = 0x0208;
    internal const uint LlmhfInjected = 1;
    internal const uint WmQuit = 0x0012, PmNoRemove = 0;
    internal const int GwlExStyle = -20;
    internal const int WsExTransparent = 0x20, WsExToolWindow = 0x80, WsExNoActivate = 0x08000000;

    internal delegate IntPtr LowLevelMouseProc(int code, IntPtr wParam, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct MsLlHookStruct
    {
        public Point Point; public uint MouseData, Flags, Time; public UIntPtr ExtraInfo;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct Message
    {
        public IntPtr HWnd; public uint MessageId; public UIntPtr WParam; public IntPtr LParam;
        public uint Time; public Point Point; public uint Private;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AllocConsole();

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc callback, IntPtr module, uint threadId);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] internal static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr GetModuleHandle(string? moduleName);
    [DllImport("kernel32.dll")] internal static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool PeekMessage(out Message msg, IntPtr hwnd, uint min, uint max, uint remove);
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern int GetMessage(out Message msg, IntPtr hwnd, uint min, uint max);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool TranslateMessage(ref Message msg);
    [DllImport("user32.dll")] internal static extern IntPtr DispatchMessage(ref Message msg);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    internal static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    internal static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    private const uint InputKeyboard = 1, InputMouse = 0, KeyeventfKeyup = 2;
    private const uint MouseeventfMiddleDown = 0x20, MouseeventfMiddleUp = 0x40;
    internal const ushort VkMenu = 0x12, VkLeft = 0x25, VkRight = 0x27;
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput
    { public ushort VirtualKey, ScanCode; public uint Flags, Time; public UIntPtr ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput
    { public int Dx, Dy; public uint MouseData, Flags, Time; public UIntPtr ExtraInfo; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion
    { [FieldOffset(0)] public MouseInput Mouse; [FieldOffset(0)] public KeyboardInput Keyboard; }
    [StructLayout(LayoutKind.Sequential)] private struct Input
    { public uint Type; public InputUnion Data; }

    internal static bool TrySendShortcut(MidFlow.Core.Shortcuts.KeyboardShortcut shortcut, out string error)
    {
        var inputs = new List<Input>(8);
        var modifiers = new List<ushort>(4);
        if ((shortcut.Modifiers & MidFlow.Core.Shortcuts.ShortcutModifiers.Ctrl) != 0) modifiers.Add(0x11);
        if ((shortcut.Modifiers & MidFlow.Core.Shortcuts.ShortcutModifiers.Alt) != 0) modifiers.Add(0x12);
        if ((shortcut.Modifiers & MidFlow.Core.Shortcuts.ShortcutModifiers.Shift) != 0) modifiers.Add(0x10);
        if ((shortcut.Modifiers & MidFlow.Core.Shortcuts.ShortcutModifiers.Win) != 0) modifiers.Add(0x5B);
        foreach (var modifier in modifiers) inputs.Add(Key(modifier, false));
        inputs.Add(Key(shortcut.Key, false));
        inputs.Add(Key(shortcut.Key, true));
        for (var index = modifiers.Count - 1; index >= 0; index--) inputs.Add(Key(modifiers[index], true));
        var inputArray = inputs.ToArray();
        var sent = SendInput((uint)inputArray.Length, inputArray, Marshal.SizeOf<Input>());
        error = sent == inputArray.Length ? "" : new Win32Exception(Marshal.GetLastWin32Error()).Message;
        return sent == inputArray.Length;
    }
    internal static bool TryReplayMiddleClick(out string error)
    {
        var inputs = new[] { Mouse(MouseeventfMiddleDown), Mouse(MouseeventfMiddleUp) };
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        error = sent == inputs.Length ? "" : new Win32Exception(Marshal.GetLastWin32Error()).Message;
        return sent == inputs.Length;
    }
    private static Input Key(ushort key, bool up) => new()
    { Type = InputKeyboard, Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = key, Flags = up ? KeyeventfKeyup : 0 } } };
    private static Input Mouse(uint flags) => new()
    { Type = InputMouse, Data = new InputUnion { Mouse = new MouseInput { Flags = flags } } };
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);
}
