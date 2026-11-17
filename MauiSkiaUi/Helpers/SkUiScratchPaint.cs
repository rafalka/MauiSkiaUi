using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A paint for one drawing call, from a small per-thread stack instead of a new <see cref="SKPaint"/> (NFR-2): looks and
/// painters record on the UI thread many times per frame, and pictures copy a paint's state when they record it, so the
/// paint can be reused as soon as the call returns. Rented with <c>using</c>; nested rentals (a look method that calls
/// another, or a subclass's override that calls back into the look while holding one) each get their own paint, so
/// nothing a caller set leaks into a callee. A paint comes back in its default state (<see cref="SKPaint.Reset"/>), which
/// also drops its shader, filters and path effect.
/// </summary>
/// <remarks>Never keep the paint past the <c>using</c> scope or hand it to anything that stores it.</remarks>
internal ref struct SkUiScratchPaint
{
    /// <summary>Paints per thread; deeper nesting than this gets a paint of its own (disposed on return).</summary>
    private const int Depth = 8;

    [ThreadStatic] private static SKPaint?[]? _paints;
    [ThreadStatic] private static int _rented;

    private readonly bool _pooled;

    private SkUiScratchPaint(SKPaint paint, bool pooled)
    {
        Paint = paint;
        _pooled = pooled;
    }

    /// <summary>The paint, in its default state when rented.</summary>
    public SKPaint Paint { get; }

    /// <summary>A paint in its default state until <see cref="Dispose"/>.</summary>
    public static SkUiScratchPaint Rent()
    {
        var paints = _paints ??= new SKPaint?[Depth];
        if (_rented == Depth)
            return new SkUiScratchPaint(new SKPaint(), pooled: false);
        var paint = paints[_rented] ??= new SKPaint();
        _rented++;
        return new SkUiScratchPaint(paint, pooled: true);
    }

    /// <summary>An antialiased fill in <paramref name="color"/>, the most common chrome paint.</summary>
    public static SkUiScratchPaint Rent(SKColor color)
    {
        var scratch = Rent();
        scratch.Paint.IsAntialias = true;
        scratch.Paint.Color = color;
        return scratch;
    }

    public void Dispose()
    {
        if (!_pooled)
        {
            Paint.Dispose();
            return;
        }
        Paint.Reset();
        _rented--;
    }
}
