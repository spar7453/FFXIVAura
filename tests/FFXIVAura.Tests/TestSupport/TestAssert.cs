using System.Numerics;

namespace FFXIVAura.Tests;

internal static class TestAssert
{
    public static void True(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"expected {expected}, got {actual}");
    }

    public static void Sequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual)
    {
        Equal(expected.Count, actual.Count);
        for (var index = 0; index < expected.Count; index++)
            Equal(expected[index], actual[index]);
    }

    public static void Vector(Vector2 expected, Vector2 actual)
    {
        Near(expected.X, actual.X);
        Near(expected.Y, actual.Y);
    }

    public static void Near(float expected, float actual, float tolerance = 0.001f)
    {
        if (Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException($"expected {expected}, got {actual}");
    }

    public static void Near(double expected, double actual, double tolerance = 0.001)
    {
        if (Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException($"expected {expected}, got {actual}");
    }
}
