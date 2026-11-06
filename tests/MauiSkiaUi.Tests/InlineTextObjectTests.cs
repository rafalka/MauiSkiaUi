using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>Baseline shift and inline objects in rich text: both layers, wrapping, bidi, truncation, taps and accessibility.</summary>
public class InlineTextObjectTests
{
    private static SkUiCoreLabel Label(params SkUiCoreSpan[] spans) =>
        new SkUiCoreLabel().SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetFontSize(16).SetLineBreakMode(LineBreakMode.WordWrap).SetSpans(spans);

    private static SKBitmap Draw(SkUiCoreNode node, int width, int height)
    {
        node.Measure(width, height);
        node.Arrange(new Rect(0, 0, width, height));
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        node.Paint(canvas);
        return bitmap;
    }

    private static int TopOfInk(SKBitmap bitmap)
    {
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y).Red < 128)
                    return y;
        return -1;
    }

    [Fact]
    public void ABaselineShiftMovesTheGlyphsAndGrowsTheLine()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var plain = Label(new SkUiCoreSpan("X"));
        var raised = Label(new SkUiCoreSpan("X").SetBaselineShift(0.5));
        var plainSize = plain.Measure(200, 100);
        var raisedSize = raised.Measure(200, 100);
        Assert.True(raisedSize.Height > plainSize.Height);
        // The line grew by what the glyph rose, so the ink is where it was from the top; a span beside it shows the shift.
        var mixed = Label(new SkUiCoreSpan("X").SetBaselineShift(0.5), new SkUiCoreSpan("X"));
        using var mixedInk = Draw(mixed, 200, 100);
        var (first, second) = (RowOfInk(mixedInk, 0, 9), RowOfInk(mixedInk, 10, 19));
        Assert.True(second - first >= 6, $"{first} {second}");
    }

    private static int RowOfInk(SKBitmap bitmap, int left, int right)
    {
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = left; x <= right; x++)
                if (bitmap.GetPixel(x, y).Red < 128)
                    return y;
        return -1;
    }

    [Fact]
    public void HtmlSubAndSupAreShifted()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var html = new SkUiCoreLabel().SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetFontSize(16).SetTextType(TextType.Html).SetText("X<sup>2</sup>");
        var plain = new SkUiCoreLabel().SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetFontSize(16).SetTextType(TextType.Html).SetText("X<small>2</small>");
        Assert.True(html.Measure(200, 100).Height > plain.Measure(200, 100).Height);

        SkUiCoreLabel Html(string text) => new SkUiCoreLabel().SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetFontSize(16).SetTextType(TextType.Html).SetText(text);
        // The second glyph, about 10 DIPs in: above the X for a superscript, its bottom below the X's for a subscript.
        using var sup = Draw(Html("X<sup>2</sup>"), 40, 40);
        Assert.True(RowOfInk(sup, 11, 18) < RowOfInk(sup, 0, 8));
        using var sub = Draw(Html("X<sub>2</sub>"), 40, 40);
        Assert.True(BottomRowOfInk(sub, 11, 18) > BottomRowOfInk(sub, 0, 8));
    }

    private static int BottomRowOfInk(SKBitmap bitmap, int left, int right)
    {
        for (var y = bitmap.Height - 1; y >= 0; y--)
            for (var x = left; x <= right; x++)
                if (bitmap.GetPixel(x, y).Red < 128)
                    return y;
        return -1;
    }

    [Fact]
    public void NestedScriptsAddUp()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        SkUiCoreLabel Html(string text) => new SkUiCoreLabel().SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetFontSize(16).SetTextType(TextType.Html).SetText(text);
        Assert.True(Html("x<sup>a<sup>b</sup></sup>").Measure(200, 100).Height > Html("x<sup>ab</sup>").Measure(200, 100).Height);
        // A subscript of a superscript comes back toward the line, not below it.
        Assert.True(Html("x<sup>a<sub>b</sub></sup>").Measure(200, 100).Height <= Html("x<sup>a</sup><sub>b</sub>").Measure(200, 100).Height);
    }

    [Fact]
    public void TheSpanConvertersKeepTheShift()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        const string Markup = "x<sup>2</sup>y<sub>i</sub>";
        var core = SkUiHtml.ToCoreSpans(Markup);
        Assert.True(core[1].BaselineShift > 0);
        Assert.True(core[3].BaselineShift < 0);
        Assert.Equal(0, core[0].BaselineShift);
        var formatted = SkUiHtml.ToFormattedString(Markup);
        Assert.True(SkUiSpan.GetBaselineShift(formatted.Spans[1]) > 0);
        Assert.True(SkUiSpan.GetBaselineShift(formatted.Spans[3]) < 0);
        // As the same markup drawn as HTML.
        var html = new SkUiCoreLabel().SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetFontSize(16).SetTextType(TextType.Html).SetText(Markup).Measure(200, 100);
        Assert.Equal(html.Height, Label(core.ToArray()).Measure(200, 100).Height, 1);
        var label = new SkUiLabel { FontFamily = SkUiTestHelpers.BundledFontFamily, FontSize = 16, FormattedText = formatted };
        Assert.Equal(html.Height, label.Measure(200, 100).Height, 1);
    }

    [Fact]
    public void TheMauiSpanHasTheShiftToo()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var shifted = new Span { Text = "X" };
        SkUiSpan.SetBaselineShift(shifted, 0.5);
        var label = new SkUiLabel { FontFamily = SkUiTestHelpers.BundledFontFamily, FontSize = 16, FormattedText = new FormattedString { Spans = { shifted } } };
        var plain = new SkUiLabel { FontFamily = SkUiTestHelpers.BundledFontFamily, FontSize = 16, FormattedText = new FormattedString { Spans = { new Span { Text = "X" } } } };
        Assert.True(label.Measure(200, 100).Height > plain.Measure(200, 100).Height);
    }

    private static SkUiInlineObject Box(double width, double height, double? baseline = null, SKColor? color = null) =>
        new(width, height, baseline ?? height, canvas =>
        {
            using var paint = new SKPaint { Color = color ?? SKColors.Black };
            canvas.DrawRect(0, 0, (float)width, (float)height, paint);
        });

    [Fact]
    public void AnInlineObjectTakesItsWidthAndTheLineItsHeight()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var text = Label(new SkUiCoreSpan("a"), new SkUiCoreSpan("b"));
        var withObject = Label(new SkUiCoreSpan("a"), new SkUiCoreSpan("formula").SetInlineObject(Box(50, 40)), new SkUiCoreSpan("b"));
        var plain = text.Measure(500, 200);
        var measured = withObject.Measure(500, 200);
        Assert.Equal(plain.Width + 50, measured.Width, 1);
        Assert.True(measured.Height >= 40);
    }

    [Fact]
    public void ItDrawsOnTheBaselineInItsPlace()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = Label(new SkUiCoreSpan("a"), new SkUiCoreSpan("x").SetInlineObject(Box(20, 10)), new SkUiCoreSpan("b"));
        using var bitmap = Draw(label, 200, 40);
        // After "a" (about 10 wide): the object's columns are solid above the baseline.
        var dark = 0;
        for (var y = 0; y < 40; y++)
            if (bitmap.GetPixel(20, y).Red < 64)
                dark++;
        Assert.Equal(10, dark);
    }

    [Fact]
    public void ABaselineBelowItsHeightDropsItBelowTheLine()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var standing = Label(new SkUiCoreSpan("a"), new SkUiCoreSpan("x").SetInlineObject(Box(20, 40, 40)));
        var dropped = Label(new SkUiCoreSpan("a"), new SkUiCoreSpan("x").SetInlineObject(Box(20, 40, 20)));
        Assert.True(standing.Measure(200, 200).Height >= 40);
        Assert.True(dropped.Measure(200, 200).Height >= 40);
        // The box takes the top of both lines, so the text beside it, on the baseline, shows how far it hangs.
        using var standingInk = Draw(standing, 200, 80);
        using var droppedInk = Draw(dropped, 200, 80);
        Assert.True(RowOfInk(standingInk, 0, 8) - RowOfInk(droppedInk, 0, 8) >= 15);
    }

    [Fact]
    public void ItWrapsWithTheLine()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = Label(new SkUiCoreSpan("aaaa "), new SkUiCoreSpan("x").SetInlineObject(Box(60, 30)), new SkUiCoreSpan(" bbbb"));
        var one = label.Measure(500, 400);
        var wrapped = label.Measure(70, 400);
        Assert.True(wrapped.Height > one.Height);
        Assert.True(wrapped.Width <= 70);
    }

    [Fact]
    public void ItFollowsBidiOrder()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var (first, formula, last) = (new SkUiCoreSpan("אב"), new SkUiCoreSpan("x").SetInlineObject(Box(20, 10)), new SkUiCoreSpan("גד"));
        var label = Label(first, formula, last);
        using var bitmap = Draw(label, 200, 40);
        var columns = Enumerable.Range(0, 200).Where(x => ReferenceEquals(label.SpanAt(new Point(x, 10)), formula)).ToArray();
        Assert.InRange(columns.Length, 19, 21);
        // Right to left: what comes first in the text is to its right.
        Assert.Same(last, label.SpanAt(new Point(columns[0] - 2, 10)));
        Assert.Same(first, label.SpanAt(new Point(columns[^1] + 2, 10)));
    }

    private static bool IsRed(SKColor color) => color is { Red: > 200, Green: < 64, Blue: < 64 };

    private static bool HasRed(SKBitmap bitmap) => bitmap.Pixels.Any(IsRed);

    /// <summary>Columns of what is drawn and not the red object.</summary>
    private static IEnumerable<int> TextColumns(SKBitmap bitmap) => Enumerable.Range(0, bitmap.Width).Where(x =>
        Enumerable.Range(0, bitmap.Height).Any(y => bitmap.GetPixel(x, y) is { Red: < 128, Green: < 128 }));

    private static double TextWidth(string text) =>
        new SkUiCoreLabel().SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetFontSize(16).SetText(text).Measure(500, 40).Width;

    [Fact]
    public void ATruncatedLineKeepsWhatFitsAndDropsWhatDoesNot()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var keeps = Label(new SkUiCoreSpan("x").SetInlineObject(Box(30, 10, color: SKColors.Red)), new SkUiCoreSpan(new string('a', 100))).SetLineBreakMode(LineBreakMode.TailTruncation);
        Assert.True(keeps.Measure(100, 40).Width <= 100);
        using var kept = Draw(keeps, 100, 40);
        Assert.True(HasRed(kept));
        var drops = Label(new SkUiCoreSpan(new string('a', 100)), new SkUiCoreSpan("x").SetInlineObject(Box(30, 10, color: SKColors.Red))).SetLineBreakMode(LineBreakMode.TailTruncation);
        using var dropped = Draw(drops, 100, 40);
        Assert.False(HasRed(dropped));
    }

    [Fact]
    public void TheEllipsisDrawsWhenItFollowsAnObject()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var before = TextWidth("aa");
        // Room for "aa", the object and the ellipsis, and not for more: the cut comes right after the object.
        var width = (int)(before + 30 + TextWidth(SkUiTextLayout.DefaultEllipsis) + 2);
        var label = Label(new SkUiCoreSpan("aa"), new SkUiCoreSpan("x").SetInlineObject(Box(30, 10, color: SKColors.Red)), new SkUiCoreSpan(new string('b', 100)))
            .SetTextColor(Colors.Black).SetLineBreakMode(LineBreakMode.TailTruncation);
        using var bitmap = Draw(label, width, 40);
        Assert.True(HasRed(bitmap));
        Assert.Contains(TextColumns(bitmap), x => x >= before + 30);
    }

    [Fact]
    public void AnObjectWiderThanTheLineLeavesTheEllipsis()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var tail = Label(new SkUiCoreSpan("x").SetInlineObject(Box(300, 10, color: SKColors.Red)), new SkUiCoreSpan("aaa"))
            .SetTextColor(Colors.Black).SetLineBreakMode(LineBreakMode.TailTruncation);
        using var tailInk = Draw(tail, 100, 40);
        Assert.False(HasRed(tailInk));
        Assert.NotEmpty(TextColumns(tailInk));

        // Head truncation: the ellipsis takes the object's span, and shows as an ellipsis.
        var head = Label(new SkUiCoreSpan("aaa"), new SkUiCoreSpan("x").SetInlineObject(Box(300, 10, color: SKColors.Red)))
            .SetTextColor(Colors.Black).SetLineBreakMode(LineBreakMode.HeadTruncation);
        using var headInk = Draw(head, 100, 40);
        Assert.False(HasRed(headInk));
        Assert.NotEmpty(TextColumns(headInk));
    }

    [Fact]
    public void WhatAnObjectPaintsDoesNotReachItsSpansDecorations()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var inner = new SkUiCoreLabel().SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetFontSize(16).SetText("x").SetTextColor(Colors.Red);
        var inline = SkUiInlineObject.FromNode(inner);
        var label = Label(new SkUiCoreSpan("x").SetInlineObject(inline).SetTextDecorations(TextDecorations.Underline)).SetTextColor(Colors.Black);
        using var bitmap = Draw(label, 100, 40);
        var line = -1;
        for (var y = 39; y >= 0 && line < 0; y--)
            if (bitmap.GetPixel(2, y).Red < 128 || IsRed(bitmap.GetPixel(2, y)))
                line = y;
        Assert.NotEqual(-1, line);
        Assert.False(IsRed(bitmap.GetPixel(2, line)));
    }

    private sealed class ReleaseCountingNode : SkUiCoreBox
    {
        internal int Releases { get; private set; }

        internal override void ReleaseDrawingResources()
        {
            base.ReleaseDrawingResources();
            Releases++;
        }
    }

    [Fact]
    public void ANodeLetsGoWithItsLabel()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var core = new ReleaseCountingNode();
        var label = Label(new SkUiCoreSpan("x").SetInlineObject(SkUiInlineObject.FromNode(core)));
        using var bitmap = Draw(label, 100, 40);
        Rendering.SkUiRenderInvalidation.ResetSubtree(label);
        Assert.Equal(1, core.Releases);

        var drawn = new ReleaseCountingNode();
        var span = new Span { Text = "x" };
        SkUiSpan.SetInlineObject(span, SkUiInlineObject.FromNode(drawn));
        var view = new SkUiLabel { FormattedText = new FormattedString { Spans = { span } } };
        view.Measure(100, 40);
        Rendering.SkUiRenderInvalidation.ResetSubtree(view);
        Assert.Equal(1, drawn.Releases);
    }

    [Fact]
    public void ANodeLeavingItsLabelLetsGo()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var replaced = new ReleaseCountingNode();
        var changed = new ReleaseCountingNode();
        var kept = new ReleaseCountingNode();
        var changing = new SkUiCoreSpan("x").SetInlineObject(SkUiInlineObject.FromNode(changed));
        var keeping = new SkUiCoreSpan("x").SetInlineObject(SkUiInlineObject.FromNode(kept));
        var label = Label(new SkUiCoreSpan("x").SetInlineObject(SkUiInlineObject.FromNode(replaced)), changing, keeping);
        label.Measure(100, 40);
        label.SetSpans(changing, keeping);
        label.Measure(100, 40);
        Assert.Equal(1, replaced.Releases);
        Assert.Equal(0, changed.Releases);
        changing.SetInlineObject(null);
        label.Measure(100, 40);
        Assert.Equal(1, changed.Releases);
        Assert.Equal(0, kept.Releases);

        var removed = new ReleaseCountingNode();
        var other = new ReleaseCountingNode();
        var removing = new Span { Text = "x" };
        SkUiSpan.SetInlineObject(removing, SkUiInlineObject.FromNode(removed));
        var staying = new Span { Text = "x" };
        SkUiSpan.SetInlineObject(staying, SkUiInlineObject.FromNode(other));
        var formatted = new FormattedString { Spans = { removing, staying } };
        var view = new SkUiLabel { FormattedText = formatted };
        view.Measure(100, 40);
        formatted.Spans.Remove(removing);
        view.Measure(100, 40);
        Assert.Equal(1, removed.Releases);
        Assert.Equal(0, other.Releases);
        view.FormattedText = new FormattedString();
        view.Measure(100, 40);
        Assert.Equal(1, other.Releases);
    }

    [Theory]
    [InlineData(TextType.Text)]
    [InlineData(TextType.Html)]
    public void LeavingSpansForTextLetsTheObjectsGoAtOnce(TextType textType)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var core = new ReleaseCountingNode();
        var label = Label(new SkUiCoreSpan("x").SetInlineObject(SkUiInlineObject.FromNode(core))).SetTextType(textType);
        label.Measure(100, 40);
        label.SetText("plain");
        Assert.Equal(1, core.Releases);
        label.Measure(100, 40);
        Assert.Equal(1, core.Releases);

        var drawn = new ReleaseCountingNode();
        var view = new SkUiLabel { TextType = textType, FormattedText = Formatted(drawn) };
        view.Measure(100, 40);
        view.Text = "plain";
        Assert.Equal(1, drawn.Releases);
        view.Measure(100, 40);
        Assert.Equal(1, drawn.Releases);

        var cleared = new ReleaseCountingNode();
        var clearing = new SkUiLabel { TextType = textType, FormattedText = Formatted(cleared) };
        clearing.Measure(100, 40);
        clearing.FormattedText = null;
        Assert.Equal(1, cleared.Releases);
    }

    [Fact]
    public void ALabelLeftWithPlainTextHoldsNothingOfItsSpans()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = Label();
        var view = new SkUiLabel();
        var (coreNode, drawnNode) = ShowAndLeave(label, view);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(coreNode.IsAlive);
        Assert.False(drawnNode.IsAlive);
        GC.KeepAlive(label);
        GC.KeepAlive(view);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static (WeakReference Core, WeakReference Drawn) ShowAndLeave(SkUiCoreLabel label, SkUiLabel view)
    {
        var core = new ReleaseCountingNode();
        label.SetSpans(new SkUiCoreSpan("x").SetInlineObject(SkUiInlineObject.FromNode(core)));
        label.Measure(100, 40);
        label.SetText("plain");
        label.Measure(100, 40);
        var drawn = new ReleaseCountingNode();
        view.FormattedText = Formatted(drawn);
        view.Measure(100, 40);
        view.Text = "plain";
        view.Measure(100, 40);
        return (new WeakReference(core), new WeakReference(drawn));
    }

    private static FormattedString Formatted(SkUiCoreNode node)
    {
        var span = new Span { Text = "x" };
        SkUiSpan.SetInlineObject(span, SkUiInlineObject.FromNode(node));
        return new FormattedString { Spans = { span } };
    }

    [Fact]
    public void ATapOnItReachesItsSpan()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var span = new SkUiCoreSpan("x").SetInlineObject(Box(40, 20));
        var other = new SkUiCoreSpan("aaaa");
        var label = Label(other, span);
        using var bitmap = Draw(label, 200, 40);
        var width = new SkUiCoreLabel().SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetFontSize(16).SetText("aaaa").Measure(500, 40).Width;
        Assert.Same(span, label.SpanAt(new Point(width + 10, 10)));
        Assert.Same(other, label.SpanAt(new Point(5, 10)));
    }

    [Fact]
    public void ScreenReadersReadItsSpansText()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = Label(new SkUiCoreSpan("area "), new SkUiCoreSpan("pi r squared").SetInlineObject(Box(10, 10)));
        var info = new SkUiSemanticsInfo();
        ((ISkUiAccessibleNode)label).GetSemantics(info);
        Assert.Equal("area pi r squared", info.Text);
    }

    [Fact]
    public void ScreenReadersReadTheTextAsDrawn()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = Label(new SkUiCoreSpan("area "), new SkUiCoreSpan("kept").SetTextTransform(TextTransform.Lowercase), new SkUiCoreSpan("Pi r").SetInlineObject(Box(10, 10)));
        label.SetTextTransform(TextTransform.Uppercase);
        var info = new SkUiSemanticsInfo();
        ((ISkUiAccessibleNode)label).GetSemantics(info);
        Assert.Equal("AREA keptPi r", info.Text);

        var object_ = new Span { Text = "Pi r" };
        SkUiSpan.SetInlineObject(object_, Box(10, 10));
        var view = new SkUiLabel
        {
            TextTransform = TextTransform.Uppercase,
            FormattedText = new FormattedString { Spans = { new Span { Text = "area " }, new Span { Text = "kept", TextTransform = TextTransform.Lowercase }, object_ } }
        };
        var drawnInfo = new SkUiSemanticsInfo();
        ((ISkUiAccessibleNode)view).GetSemantics(drawnInfo);
        Assert.Equal("AREA keptPi r", drawnInfo.Text);
    }

    [Fact]
    public void AnObjectAlignsWithTheOthersText()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = Label(new SkUiCoreSpan("x").SetInlineObject(Box(10, 10)));
        label.SetHorizontalTextAlignment(TextAlignment.End);
        using var bitmap = Draw(label, 100, 20);
        Assert.True(bitmap.GetPixel(95, 10).Red < 64 || bitmap.GetPixel(95, 5).Red < 64 || bitmap.GetPixel(95, 15).Red < 64);
        Assert.True(bitmap.GetPixel(5, 10).Red > 200);
    }

    [Fact]
    public void ANodeCanStandInTheLine()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var box = new SkUiCoreBox().SetColor(Colors.Black);
        box.SetWidth(24);
        box.SetHeight(12);
        var inline = SkUiInlineObject.FromNode(box);
        Assert.Equal(24, inline.Width);
        Assert.Equal(12, inline.Height);
        Assert.Equal(12, inline.Baseline);
        var label = Label(new SkUiCoreSpan("x").SetInlineObject(inline));
        using var bitmap = Draw(label, 100, 20);
        Assert.NotEqual(-1, TopOfInk(bitmap));
    }

    [Fact]
    public void ANodeInATreeCannotStandInALine()
    {
        var stack = new SkUiCoreVerticalStackLayout();
        var box = new SkUiCoreBox();
        stack.Add(box);
        Assert.Throws<ArgumentException>(() => SkUiInlineObject.FromNode(box));
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    [InlineData(double.NaN, 1)]
    [InlineData(1, double.PositiveInfinity)]
    public void BadSizesThrow(double width, double height) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiInlineObject(width, height, 0, _ => { }));

    [Fact]
    public void TheMauiSpanHoldsObjectsToo()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var span = new Span { Text = "formula" };
        SkUiSpan.SetInlineObject(span, Box(50, 40));
        var label = new SkUiLabel { FontFamily = SkUiTestHelpers.BundledFontFamily, FontSize = 16, FormattedText = new FormattedString { Spans = { span } } };
        var size = label.Measure(200, 100);
        Assert.Equal(50, size.Width, 1);
        Assert.True(size.Height >= 40);
    }

    [Fact]
    public void AChangedObjectLaysOutAgain()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var span = new SkUiCoreSpan("x").SetInlineObject(Box(50, 10));
        var label = Label(span);
        Assert.Equal(50, label.Measure(200, 100).Width, 1);
        span.SetInlineObject(Box(80, 10));
        Assert.Equal(80, label.Measure(200, 100).Width, 1);
        span.SetInlineObject(null);
        Assert.True(label.Measure(200, 100).Width < 40);
    }

    [Fact]
    public void AChangedObjectOnAMauiSpanLaysOutAgain()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var span = new Span { Text = "x" };
        SkUiSpan.SetInlineObject(span, Box(50, 10));
        var label = new SkUiLabel { FontFamily = SkUiTestHelpers.BundledFontFamily, FontSize = 16, FormattedText = new FormattedString { Spans = { span } } };
        Assert.Equal(50, label.Measure(200, 100).Width, 1);
        SkUiSpan.SetInlineObject(span, Box(80, 10));
        Assert.Equal(80, label.Measure(200, 100).Width, 1);
        var height = label.Measure(200, 100).Height;
        SkUiSpan.SetInlineObject(span, null);
        SkUiSpan.SetBaselineShift(span, 0.5);
        var plain = new SkUiLabel { FontFamily = SkUiTestHelpers.BundledFontFamily, FontSize = 16, FormattedText = new FormattedString { Spans = { new Span { Text = "x" } } } };
        Assert.True(label.Measure(200, 100).Height > plain.Measure(200, 100).Height);
        Assert.True(height > 0);
    }
}
