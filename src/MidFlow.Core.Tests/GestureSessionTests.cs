using MidFlow.Core.Gestures;
using Xunit;

namespace MidFlow.Core.Tests;

public class GestureSessionTests
{
    [Theory]
    [InlineData(-100, 10, GestureDirection.Left)]
    [InlineData(100, 10, GestureDirection.Right)]
    [InlineData(10, -100, GestureDirection.Up)]
    [InlineData(10, 100, GestureDirection.Down)]
    public void ClassifiesFourDirections(int dx, int dy, GestureDirection expected)
    {
        var session = CreateSession();
        session.Update(dx, dy);

        Assert.Equal(expected, session.Evaluate().Direction);
    }

    [Fact]
    public void RejectsAmbiguousDiagonalMovement()
    {
        var session = CreateSession();
        session.Update(100, 100);

        Assert.Equal(GestureDirection.None, session.Evaluate().Direction);
        Assert.Equal("NotAxisDominant", session.Evaluate().Reason);
    }

    [Fact]
    public void PreservesVerticalDriftToleranceForHorizontalGestures()
    {
        var session = CreateSession();
        session.Update(-150, 90);

        Assert.Equal(GestureDirection.Left, session.Evaluate().Direction);
    }

    private static GestureSession CreateSession() => new(0, 0, 5, 70, 120, 1000);
}
