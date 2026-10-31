using System.Diagnostics;

namespace MidFlow.Core.Gestures;

public enum GestureDirection { None, Left, Right }

public sealed class GestureSession
{
    private readonly int _startX;
    private readonly int _startY;
    private readonly long _startedAt;
    private int _x;
    private int _y;
    private bool _hasDragged;

    private readonly int _clickMovementThresholdPx;
    private readonly int _minimumHorizontalMovementPx;
    private readonly int _maximumVerticalMovementPx;
    private readonly int _maximumGestureDurationMs;
    private const double HorizontalDominanceRatio = 1.5;

    public bool HasDragged => _hasDragged;

    public GestureSession(int x, int y, int clickMovementThresholdPx,
        int minimumHorizontalMovementPx, int maximumVerticalMovementPx, int maximumGestureDurationMs)
    {
        _clickMovementThresholdPx = clickMovementThresholdPx;
        _minimumHorizontalMovementPx = minimumHorizontalMovementPx;
        _maximumVerticalMovementPx = maximumVerticalMovementPx;
        _maximumGestureDurationMs = maximumGestureDurationMs;
        _startX = _x = x;
        _startY = _y = y;
        _startedAt = Stopwatch.GetTimestamp();
    }

    public void Update(int x, int y)
    {
        _x = x;
        _y = y;
        var dx = x - _startX;
        var dy = y - _startY;
        if ((dx * dx) + (dy * dy) > _clickMovementThresholdPx * _clickMovementThresholdPx)
            _hasDragged = true;
    }

    public (GestureDirection Direction, int Dx, int Dy, double DurationMs, string Reason) Evaluate()
    {
        var dx = _x - _startX;
        var dy = _y - _startY;
        var absX = Math.Abs(dx);
        var absY = Math.Abs(dy);
        var duration = Stopwatch.GetElapsedTime(_startedAt).TotalMilliseconds;

        // Classify net displacement, allowing natural hand drift while requiring
        // a clearly horizontal gesture. HasDragged separately ensures rejected
        // drags are consumed rather than replayed as native middle-button clicks.
        if (duration > _maximumGestureDurationMs)
            return (GestureDirection.None, dx, dy, duration, "TooSlow");
        if (absX < _minimumHorizontalMovementPx)
            return (GestureDirection.None, dx, dy, duration, "HorizontalDistanceTooShort");
        if (absY > _maximumVerticalMovementPx)
            return (GestureDirection.None, dx, dy, duration, "VerticalDriftTooLarge");
        if (absX < absY * HorizontalDominanceRatio)
            return (GestureDirection.None, dx, dy, duration, "NotHorizontalDominant");

        return (dx < 0 ? GestureDirection.Left : GestureDirection.Right, dx, dy, duration, "Recognized");
    }
}
