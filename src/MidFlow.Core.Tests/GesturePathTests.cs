using MidFlow.Core.Gestures;
using Xunit;

namespace MidFlow.Core.Tests;

public class GesturePathTests
{
    [Fact]
    public void SamePathAtDifferentPositionAndScaleMatches()
    {
        var original = GesturePath.Create(Circle(120, 300, 200));
        var resized = GesturePath.Create(Circle(55, 900, 700));

        Assert.NotNull(original);
        Assert.NotNull(resized);
        Assert.True(original!.Matches(resized!));
    }

    [Fact]
    public void ReverseDirectionDoesNotMatchRecordedPath()
    {
        var path = new[]
        {
            new GesturePoint(0, 0), new GesturePoint(50, 0), new GesturePoint(90, 40),
            new GesturePoint(60, 90), new GesturePoint(0, 90)
        };
        var forward = GesturePath.Create(path);
        var reverse = GesturePath.Create(path.Reverse().ToArray());

        Assert.NotNull(forward);
        Assert.NotNull(reverse);
        Assert.False(forward!.Matches(reverse!));
    }

    [Fact]
    public void TooShortPathCannotBeSaved()
    {
        Assert.Null(GesturePath.Create([new GesturePoint(0, 0), new GesturePoint(2, 1), new GesturePoint(4, 0)]));
    }

    [Fact]
    public void AppProfileCanInheritOrReplaceGlobalMappings()
    {
        var global = Definition("global", Circle(100, 0, 0));
        var app = Definition("app", Zigzag());
        var profiles = new Dictionary<string, GestureProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["browser"] = new GestureProfile(true, [app])
        };

        Assert.Same(global, Assert.Single(GestureResolver.Resolve("BROWSER.exe", [global], profiles)));
        profiles["browser"] = new GestureProfile(false, [app]);
        Assert.Same(app, Assert.Single(GestureResolver.Resolve("browser", [global], profiles)));
    }

    [Fact]
    public void SimilarMappingsAreAmbiguousAndDoNotRun()
    {
        var first = Definition("first", Circle(100, 0, 0));
        var second = Definition("second", Circle(102, 0, 0));
        var input = GesturePath.Create(Circle(100, 0, 0));

        Assert.NotNull(input);
        Assert.Null(GestureResolver.Match(input!, [first, second]));
    }

    private static GestureDefinition Definition(string id, IReadOnlyList<GesturePoint> source) =>
        new(id, id, true, GesturePath.Create(source)!, new GestureActionDefinition(GestureActionKind.Back));

    private static GesturePoint[] Circle(double radius, double centerX, double centerY) =>
        Enumerable.Range(0, 80).Select(i =>
        {
            var angle = i * Math.PI * 2 / 79;
            return new GesturePoint(centerX + Math.Cos(angle) * radius, centerY + Math.Sin(angle) * radius);
        }).ToArray();

    private static GesturePoint[] Zigzag() =>
    [
        new(0, 0), new(70, 0), new(35, 45), new(90, 85), new(0, 85)
    ];
}
