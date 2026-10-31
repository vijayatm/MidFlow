using System.Diagnostics;

namespace MidFlow.Core.Gestures;

public enum GestureDirection { None, Left, Right, Up, Down }

public sealed class GestureSession
{
    private readonly int _startX;
    private readonly int _startY;
    private readonly long _startedAt;
    private int _x;
    private int _y;
    private bool _hasDragged;
    private readonly GesturePoint[] _pathPoints = new GesturePoint[128];
    private int _pathCount;

    private readonly int _clickMovementThresholdPx;
    private readonly int _minimumHorizontalMovementPx;
    private readonly int _maximumVerticalMovementPx;
    private readonly int _maximumGestureDurationMs;
    private const double AxisDominanceRatio = 1.5;

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
        _pathPoints[0] = new GesturePoint(x, y);
        _pathCount = 1;
        _startedAt = Stopwatch.GetTimestamp();
    }

    public void Update(int x, int y)
    {
        _x = x;
        _y = y;
        if (_pathCount < _pathPoints.Length)
        {
            var previous = _pathPoints[_pathCount - 1];
            var dxPath = x - previous.X;
            var dyPath = y - previous.Y;
            if ((dxPath * dxPath) + (dyPath * dyPath) >= 9)
                _pathPoints[_pathCount++] = new GesturePoint(x, y);
        }
        var dx = x - _startX;
        var dy = y - _startY;
        if ((dx * dx) + (dy * dy) > _clickMovementThresholdPx * _clickMovementThresholdPx)
            _hasDragged = true;
    }

    public GesturePoint[] GetRecordedPoints()
    {
        var points = new GesturePoint[_pathCount];
        Array.Copy(_pathPoints, points, _pathCount);
        return points;
    }

    public GesturePath? GetRecordedPath() => GesturePath.Create(GetRecordedPoints());

    public (GestureDirection Direction, int Dx, int Dy, double DurationMs, string Reason) Evaluate()
    {
        var dx = _x - _startX;
        var dy = _y - _startY;
        var absX = Math.Abs(dx);
        var absY = Math.Abs(dy);
        var duration = Stopwatch.GetElapsedTime(_startedAt).TotalMilliseconds;

        // Classify net displacement on either axis while allowing bounded cross-axis drift.
        // HasDragged separately ensures rejected drags remain consumed.
        if (duration > _maximumGestureDurationMs)
            return (GestureDirection.None, dx, dy, duration, "TooSlow");
        if (Math.Max(absX, absY) < _minimumHorizontalMovementPx)
            return (GestureDirection.None, dx, dy, duration, "HorizontalDistanceTooShort");
        if (absX >= _minimumHorizontalMovementPx && absY <= _maximumVerticalMovementPx && absX >= absY * AxisDominanceRatio)
            return (dx < 0 ? GestureDirection.Left : GestureDirection.Right, dx, dy, duration, "Recognized");
        if (absY >= _minimumHorizontalMovementPx && absX <= _maximumVerticalMovementPx && absY >= absX * AxisDominanceRatio)
            return (dy < 0 ? GestureDirection.Up : GestureDirection.Down, dx, dy, duration, "Recognized");
        if (absX > _maximumVerticalMovementPx && absY < _minimumHorizontalMovementPx ||
            absY > _maximumVerticalMovementPx && absX < _minimumHorizontalMovementPx)
            return (GestureDirection.None, dx, dy, duration, "CrossAxisDriftTooLarge");

        return (GestureDirection.None, dx, dy, duration, "NotAxisDominant");
    }
}
