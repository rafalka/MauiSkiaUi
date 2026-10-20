namespace MauiSkiaUi;

/// <summary>
/// A field's own idle chrome over its style's, as the text fields, search bars and pickers of both layers copy it in (their
/// <c>FillColor</c>, <c>BorderColor</c>, <c>BorderWidth</c> and <c>CornerRadii</c>; each <c>null</c>: the style's), handed to the
/// look as <see cref="SkUiFieldChrome"/>.
/// </summary>
internal struct SkUiFieldChromeState
{
    public Color? FillColor;
    public Color? BorderColor;
    public double? BorderWidth;
    public CornerRadius? CornerRadii;

    /// <summary>What the look draws with.</summary>
    public readonly SkUiFieldChrome ToPaint() => new(
        FillColor is { } fill ? SkUiToggleDrawing.ToSkColor(fill) : null,
        BorderColor is { } border ? SkUiToggleDrawing.ToSkColor(border) : null,
        BorderWidth is { } width ? (float)width : null,
        CornerRadii);
}
