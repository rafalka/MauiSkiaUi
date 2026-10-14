using SkiaSharp;

namespace MauiSkiaUi.Rendering;

/// <summary>
/// Content the compositor draws itself each time it draws a node (<see cref="SkUiRenderProps.Drawable"/>), on the render
/// thread: what changes every frame with the shared clock (a shimmer's wave, a pulse) without recording or committing
/// anything per frame. The position follows <see cref="SkUiRenderDrawContext.Now"/> (<see cref="SkUiRenderClock"/>), not the
/// moment it was committed, so drawables with the same timing are in phase on every surface.
/// </summary>
/// <remarks>
/// Made on the UI thread and immutable once committed: <see cref="Draw"/> runs on the render thread (and on the UI thread for
/// the immediate painter, at time zero) and must not change its state. The compositor owns it once committed and disposes it
/// when a commit replaces it or its node goes (as clip paths and shadows); the UI side forgets it on reset.
/// </remarks>
internal abstract class SkUiRenderDrawable : IDisposable
{
    /// <summary>Whether it changes with the clock: a node drawing it requests frames while it is drawn, and only then.</summary>
    public abstract bool IsAnimated { get; }

    /// <summary>Draws in the node's local coordinates, inside its clips; leaves the canvas as it found it.</summary>
    public abstract void Draw(SKCanvas canvas, in SkUiRenderDrawContext context);

    /// <summary>Disposes the native objects it owns (shaders, paths).</summary>
    public virtual void Dispose() { }
}

/// <summary>What a <see cref="SkUiRenderDrawable"/> draws with.</summary>
/// <param name="bounds">The node's layout rectangle, local.</param>
/// <param name="now">The render clock (<see cref="TimeSpan.Zero"/> for the immediate painter's still frame).</param>
/// <param name="surface">Surface DIPs to device pixels.</param>
/// <param name="space">
/// The coordinates of the nearest ancestor (or the node itself) with <see cref="SkUiRenderProps.DrawableSpace"/> to device
/// pixels, wherever it moved, scrolled or animated to; <paramref name="surface"/> without one.
/// </param>
/// <param name="paint">A scratch paint, reset (antialiased) for each drawable; nothing to dispose or restore.</param>
internal readonly struct SkUiRenderDrawContext(SKRect bounds, TimeSpan now, in SKMatrix surface, in SKMatrix space, SKPaint paint)
{
    /// <summary>The node's layout rectangle, local.</summary>
    public SKRect Bounds { get; } = bounds;

    /// <summary>The render clock (<see cref="TimeSpan.Zero"/> for the immediate painter's still frame).</summary>
    public TimeSpan Now { get; } = now;

    /// <summary>Surface DIPs to device pixels.</summary>
    public SKMatrix Surface { get; } = surface;

    /// <summary>The nearest drawable space (<see cref="SkUiRenderProps.DrawableSpace"/>) to device pixels; else the surface.</summary>
    public SKMatrix Space { get; } = space;

    /// <summary>A scratch paint, reset (antialiased) for each drawable.</summary>
    public SKPaint Paint { get; } = paint;
}
