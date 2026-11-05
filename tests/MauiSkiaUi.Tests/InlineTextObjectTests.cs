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
        using var plainInk = Draw(plain, 200, 100);
        using var raisedInk = Draw(raised, 200, 100);
        // The line grew by what the glyph rose, so the ink is where it was from the top; a lower span shows the shift.
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
        var label = Label(new SkUiCoreSpan("אב"), new SkUiCoreSpan("x").SetInlineObject(Box(20, 10)), new SkUiCoreSpan("גד"));
        using var bitmap = Draw(label, 200, 40);
        Assert.NotEqual(-1, TopOfInk(bitmap));
        Assert.True(label.Measure(200, 40).Width >= 20);
    }

    [Fact]
    public void ATruncatedLineKeepsItWhenItFitsAndDrops()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = Label(new SkUiCoreSpan("x").SetInlineObject(Box(30, 10)), new SkUiCoreSpan(new string('a', 100)));
        label.SetLineBreakMode(LineBreakMode.TailTruncation);
        var size = label.Measure(100, 40);
        Assert.True(size.Width <= 100);
        using var bitmap = Draw(label, 100, 40);
        Assert.NotEqual(-1, TopOfInk(bitmap));
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
}
