using MauiSkiaUi.Core;

namespace MauiSkiaUi.Tests;

/// <summary>Drawn test child with a fixed intrinsic size (content, without margins).</summary>
internal sealed class FixedSkView(Size natural) : SkUiView
{
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => natural;
}

/// <summary>Core test child with a fixed intrinsic size.</summary>
internal sealed class FixedCoreNode(Size natural) : SkUiCoreNode
{
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => natural;
}

/// <summary>Text-like sizing: as wide as allowed up to <see cref="NaturalWidth"/>, one line per filled width.</summary>
internal static class WrappingSize
{
    public static Size Measure(double naturalWidth, double lineHeight, double widthConstraint)
    {
        var width = Math.Min(naturalWidth, widthConstraint);
        var lines = width <= 0 ? 1 : Math.Ceiling(naturalWidth / width - 1e-9);
        return new Size(width, lines * lineHeight);
    }
}

/// <summary>Drawn test child that wraps like a label.</summary>
internal sealed class WrappingSkView(double naturalWidth, double lineHeight) : SkUiView
{
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        WrappingSize.Measure(naturalWidth, lineHeight, widthConstraint);
}

/// <summary>Core test child that wraps like a label.</summary>
internal sealed class WrappingCoreNode(double naturalWidth, double lineHeight) : SkUiCoreNode
{
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        WrappingSize.Measure(naturalWidth, lineHeight, widthConstraint);
}
