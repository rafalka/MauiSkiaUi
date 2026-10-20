namespace MauiSkiaUi;

/// <summary>Easing curves the platform looks use for their transitions.</summary>
public static class SkUiEasings
{
    private const double Tolerance = 1e-5;

    /// <summary>Material 3's emphasized curve, <c>cubic-bezier(0.2, 0, 0, 1)</c>.</summary>
    public static readonly Easing Emphasized = CubicBezier(0.2, 0, 0, 1);

    /// <summary>Material 3's emphasized decelerate curve, <c>cubic-bezier(0.05, 0.7, 0.1, 1)</c>.</summary>
    public static readonly Easing EmphasizedDecelerate = CubicBezier(0.05, 0.7, 0.1, 1);

    /// <summary>iOS's spring-like sheet curve, <c>cubic-bezier(0.32, 0.72, 0, 1)</c>.</summary>
    public static readonly Easing IosStandard = CubicBezier(0.32, 0.72, 0, 1);

    /// <summary>Fluent's decelerate (fast out, slow in) curve, <c>cubic-bezier(0, 0, 0, 1)</c>.</summary>
    public static readonly Easing FluentDecelerate = CubicBezier(0, 0, 0, 1);

    /// <summary>A CSS-style cubic Bézier easing through (0, 0) and (1, 1) with the control points (<paramref name="x1"/>, <paramref name="y1"/>) and (<paramref name="x2"/>, <paramref name="y2"/>).</summary>
    public static Easing CubicBezier(double x1, double y1, double x2, double y2) => new(t => Solve(t, x1, y1, x2, y2));

    private static double Solve(double x, double x1, double y1, double x2, double y2)
    {
        if (x <= 0) return 0;
        if (x >= 1) return 1;
        // Bisect for the curve parameter whose x is the input, then read y.
        double low = 0, high = 1, t = x;
        for (var i = 0; i < 24; i++)
        {
            var current = Bezier(t, x1, x2);
            if (Math.Abs(current - x) < Tolerance) break;
            if (current < x) low = t; else high = t;
            t = (low + high) / 2;
        }
        return Bezier(t, y1, y2);
    }

    private static double Bezier(double t, double p1, double p2)
    {
        var u = 1 - t;
        return 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t;
    }
}
