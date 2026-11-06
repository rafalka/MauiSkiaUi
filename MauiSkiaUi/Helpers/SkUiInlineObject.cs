using MauiSkiaUi.Core;
using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// Something drawn in a line of text: it wraps, aligns and takes taps as a word of its size, and follows the line's bidi order
/// (<see cref="SkUiCoreSpan.InlineObject"/>, <see cref="SkUiSpan.InlineObjectProperty"/>: a formula, an icon, a small image). Its size is
/// fixed: to change it, set a new one on the span.
/// </summary>
/// <remarks>
/// <see cref="Paint"/> runs while the label records its content, on the UI thread, with the origin at the object's top-left. What it draws
/// is part of the label's picture, so nothing it creates may be kept for the render thread; when what it shows changes, call
/// <see cref="SkUiCoreNode.InvalidatePaint"/> on the label.
/// </remarks>
public sealed class SkUiInlineObject
{
    internal const char ReplacementCharacter = '￼';
    internal const string ReplacementText = "￼";

    /// <summary>Creates an object of <paramref name="width"/> × <paramref name="height"/> DIPs.</summary>
    /// <param name="width">Width in DIPs (finite, not negative).</param>
    /// <param name="height">Height in DIPs (finite, not negative).</param>
    /// <param name="baseline">Distance from the object's top to the line's baseline, in DIPs: <paramref name="height"/> stands the object on the baseline, less than that drops it below, and more raises it.</param>
    /// <param name="paint">Draws the object, with the origin at its top-left.</param>
    public SkUiInlineObject(double width, double height, double baseline, Action<SKCanvas> paint)
    {
        ArgumentNullException.ThrowIfNull(paint);
        SkUiValidate.ThrowIfNegativeOrNotFinite(width, nameof(width));
        SkUiValidate.ThrowIfNegativeOrNotFinite(height, nameof(height));
        SkUiValidate.ThrowIfNotFinite(baseline, nameof(baseline));
        Width = width;
        Height = height;
        Baseline = baseline;
        Paint = paint;
    }

    /// <summary>The object's width in DIPs.</summary>
    public double Width { get; }

    /// <summary>The object's height in DIPs.</summary>
    public double Height { get; }

    /// <summary>Distance from the object's top to the line's baseline, in DIPs.</summary>
    public double Baseline { get; }

    /// <summary>Draws the object, with the origin at its top-left.</summary>
    public Action<SKCanvas> Paint { get; }

    /// <summary>Lets go of what drawing made (a node's text blobs and clips), when the label holding the object does.</summary>
    internal Action? ReleaseDrawingResources { get; private init; }

    /// <summary>A label's spans were resolved again: <paramref name="current"/> holds the objects it shows now, and those it no longer shows let go.</summary>
    internal static void Replace(ref List<SkUiInlineObject>? held, List<SkUiInlineObject>? current)
    {
        if (held is not null)
            foreach (var inlineObject in held)
                if (current?.Contains(inlineObject) != true)
                    inlineObject.ReleaseDrawingResources?.Invoke();
        held = current;
    }

    /// <summary>The label lets go of what its objects drew; it may show them again.</summary>
    internal static void Release(List<SkUiInlineObject>? held)
    {
        if (held is not null)
            foreach (var inlineObject in held)
                inlineObject.ReleaseDrawingResources?.Invoke();
    }

    /// <summary>
    /// An object that draws <paramref name="node"/>, at the size it measures to now, standing on the baseline unless <paramref name="baseline"/>
    /// says otherwise. The node takes no input of its own and has no parent (use the span's <see cref="SkUiCoreSpan.Tapped"/>); its content is
    /// drawn into the label's picture. It must not be in a tree.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="node"/> already has a parent.</exception>
    public static SkUiInlineObject FromNode(SkUiCoreNode node, double? baseline = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.Parent is not null)
            throw new ArgumentException("An inline node must not have a parent.", nameof(node));
        var size = node.Measure(double.PositiveInfinity, double.PositiveInfinity);
        node.Arrange(new Rect(Point.Zero, size));
        return new SkUiInlineObject(size.Width, size.Height, baseline ?? size.Height, node.Paint) { ReleaseDrawingResources = () => SkUiRenderInvalidation.ResetSubtree(node) };
    }
}

/// <summary>What SkiaUi adds to MAUI's <see cref="Span"/> in a <see cref="SkUiLabel"/>'s <c>FormattedText</c>; Core spans have these as properties.</summary>
public static class SkUiSpan
{
    /// <summary>A superscript's shift, as <see cref="SetBaselineShift"/> takes it: what <c>sup</c> in HTML text uses.</summary>
    public const double SuperscriptShift = 0.45;

    /// <summary>A subscript's shift, as <see cref="SetBaselineShift"/> takes it: what <c>sub</c> in HTML text uses.</summary>
    public const double SubscriptShift = -0.2;

    /// <summary>How far the span sits above the line's baseline, as a fraction of its font size; see <see cref="SkUiCoreSpan.BaselineShift"/>.</summary>
    public static readonly BindableProperty BaselineShiftProperty = BindableProperty.CreateAttached("BaselineShift", typeof(double), typeof(SkUiSpan), 0.0,
        validateValue: SkUiValidate.Finite);

    /// <summary>What the span draws instead of its text; see <see cref="SkUiCoreSpan.InlineObject"/>.</summary>
    public static readonly BindableProperty InlineObjectProperty = BindableProperty.CreateAttached("InlineObject", typeof(SkUiInlineObject), typeof(SkUiSpan), null);

    /// <summary>Gets <see cref="BaselineShiftProperty"/>.</summary>
    public static double GetBaselineShift(Span span) => (double)span.GetValue(BaselineShiftProperty);

    /// <summary>Sets <see cref="BaselineShiftProperty"/>.</summary>
    public static void SetBaselineShift(Span span, double value) => span.SetValue(BaselineShiftProperty, value);

    /// <summary>Gets <see cref="InlineObjectProperty"/>.</summary>
    public static SkUiInlineObject? GetInlineObject(Span span) => (SkUiInlineObject?)span.GetValue(InlineObjectProperty);

    /// <summary>Sets <see cref="InlineObjectProperty"/>.</summary>
    public static void SetInlineObject(Span span, SkUiInlineObject? value) => span.SetValue(InlineObjectProperty, value);
}
