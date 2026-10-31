namespace MidFlow.Core.Gestures;

public readonly record struct GesturePoint(double X, double Y);

/// <summary>A bounded, normalized path captured while the middle button is held.</summary>
public sealed record GesturePath(IReadOnlyList<GesturePoint> Points)
{
    public const int PointCount = 24;
    private const double MatchDistance = 0.22;

    public bool IsValid => Points is { Count: PointCount } &&
        Points.All(p => double.IsFinite(p.X) && double.IsFinite(p.Y) && Math.Abs(p.X) <= 1 && Math.Abs(p.Y) <= 1);

    public static GesturePath? Create(IReadOnlyList<GesturePoint> source)
    {
        if (source.Count < 3) return null;
        var simplified = new List<GesturePoint>(source.Count);
        foreach (var point in source)
        {
            if (simplified.Count == 0 || Distance(simplified[^1], point) >= 3)
                simplified.Add(point);
        }
        if (simplified.Count < 3) return null;

        var length = 0d;
        for (var i = 1; i < simplified.Count; i++) length += Distance(simplified[i - 1], simplified[i]);
        if (length < 24) return null;

        var sampled = new GesturePoint[PointCount];
        var segment = 1;
        var traversed = 0d;
        for (var i = 0; i < PointCount; i++)
        {
            var target = length * i / (PointCount - 1);
            while (segment < simplified.Count - 1 && traversed + Distance(simplified[segment - 1], simplified[segment]) < target)
            {
                traversed += Distance(simplified[segment - 1], simplified[segment]);
                segment++;
            }
            var start = simplified[segment - 1];
            var end = simplified[segment];
            var segmentLength = Distance(start, end);
            var t = segmentLength == 0 ? 0 : Math.Clamp((target - traversed) / segmentLength, 0, 1);
            sampled[i] = new GesturePoint(start.X + ((end.X - start.X) * t), start.Y + ((end.Y - start.Y) * t));
        }

        var minX = sampled.Min(p => p.X); var maxX = sampled.Max(p => p.X);
        var minY = sampled.Min(p => p.Y); var maxY = sampled.Max(p => p.Y);
        var scale = Math.Max(maxX - minX, maxY - minY);
        if (scale < 1) return null;
        var centerX = (minX + maxX) / 2; var centerY = (minY + maxY) / 2;
        for (var i = 0; i < sampled.Length; i++)
            sampled[i] = new GesturePoint((sampled[i].X - centerX) / scale, (sampled[i].Y - centerY) / scale);
        return new GesturePath(sampled);
    }

    public double DistanceTo(GesturePath other)
    {
        if (!IsValid || !other.IsValid) return double.PositiveInfinity;
        var total = 0d;
        for (var i = 0; i < PointCount; i++) total += Distance(Points[i], other.Points[i]);
        return total / PointCount;
    }

    public bool Matches(GesturePath other) => DistanceTo(other) <= MatchDistance;
    private static double Distance(GesturePoint a, GesturePoint b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
}
