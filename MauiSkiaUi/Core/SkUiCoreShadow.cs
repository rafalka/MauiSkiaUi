namespace MauiSkiaUi.Core;

/// <summary>
/// An immutable drop shadow for <see cref="SkUiCoreNode.Shadow"/> (MAUI's <c>Shadow</c> as a value): to change a node's
/// shadow, set a new one. Any <see cref="IShadow"/> works too, including MAUI's <c>Shadow</c>, whose changes redraw.
/// </summary>
/// <param name="Paint">The shadow's color or gradient (a gradient maps onto the node's bounds); <c>null</c>: no shadow.</param>
/// <param name="Offset">Offset from the node in DIPs (positive: right and down).</param>
/// <param name="Radius">Blur radius in DIPs (10 by default, as MAUI; 0: a sharp shadow).</param>
/// <param name="Opacity">Opacity multiplied into the paint, 0–1 (1 by default, as MAUI).</param>
public sealed record SkUiCoreShadow(Paint? Paint, Point Offset, float Radius = 10, float Opacity = 1) : IShadow
{
    /// <summary>A solid shadow of <paramref name="color"/>.</summary>
    public SkUiCoreShadow(Color color, Point offset, float radius = 10, float opacity = 1)
        : this(new SolidPaint(color ?? throw new ArgumentNullException(nameof(color))), offset, radius, opacity)
    {
    }

    Paint IShadow.Paint => Paint!;
}
