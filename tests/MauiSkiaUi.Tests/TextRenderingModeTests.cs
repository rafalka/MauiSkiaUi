using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary><see cref="SkUiTextRendering"/> modes and layout reuse.</summary>
public class TextRenderingModeTests
{
    private static SKTypeface Primary() =>
        SKTypeface.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "RobotoMono-Regular.ttf"));

    private static bool LayoutIsSimple(string text, SkUiTextRendering rendering, SkUiTextDirection direction = SkUiTextDirection.Auto)
    {
        var layout = new SkUiTextLayout();
        layout.Measure(text, Primary(), 16, default, 500, SkUiCoreTextLineBreakers.WordWrap, direction, rendering);
        return layout.LastLayoutSimple;
    }

    [Fact]
    public void AutoUsesSimplePathForPlainLatinAndShapesEverythingElse()
    {
        Assert.True(LayoutIsSimple("Item 0001 — €12.50 (zażółć)", SkUiTextRendering.Auto));
        Assert.False(LayoutIsSimple("مرحبا", SkUiTextRendering.Auto));
        Assert.False(LayoutIsSimple("ok 👍", SkUiTextRendering.Auto));
        Assert.False(LayoutIsSimple("Item 1", SkUiTextRendering.Auto, SkUiTextDirection.RightToLeft));
    }

    [Fact]
    public void SimpleNeverShapesAndShapedAlwaysDoes()
    {
        Assert.True(LayoutIsSimple("مرحبا 👍", SkUiTextRendering.Simple));
        Assert.False(LayoutIsSimple("Item 0001", SkUiTextRendering.Shaped));
    }

    [Fact]
    public void DefaultFollowsGlobalOption()
    {
        var previous = SkUiTextOptions.DefaultRendering;
        try
        {
            SkUiTextOptions.DefaultRendering = SkUiTextRendering.Shaped;
            Assert.False(LayoutIsSimple("Item 0001", SkUiTextRendering.Default));
            SkUiTextOptions.DefaultRendering = SkUiTextRendering.Default;
            Assert.Equal(SkUiTextRendering.Auto, SkUiTextOptions.DefaultRendering);
            Assert.True(LayoutIsSimple("Item 0001", SkUiTextRendering.Default));
        }
        finally
        {
            SkUiTextOptions.DefaultRendering = previous;
        }
    }

    [Fact]
    public void SimpleAndShapedMeasureLatinAlike()
    {
        var simple = new SkUiTextLayout().Measure("Item 0001", Primary(), 16, default, double.PositiveInfinity, SkUiCoreTextLineBreakers.NoWrap, rendering: SkUiTextRendering.Simple);
        var shaped = new SkUiTextLayout().Measure("Item 0001", Primary(), 16, default, double.PositiveInfinity, SkUiCoreTextLineBreakers.NoWrap, rendering: SkUiTextRendering.Shaped);
        Assert.InRange(simple.Width - shaped.Width, -0.5, 0.5);
        Assert.Equal(simple.Height, shaped.Height, 2);
    }

    [Fact]
    public void SimplePathWrapsAndSlicesLines()
    {
        using var font = new SKFont(Primary(), 16);
        var layout = new SkUiTextLayout();
        var size = layout.Measure("alpha beta gamma delta epsilon", Primary(), 16, default, 100, SkUiCoreTextLineBreakers.WordWrap, rendering: SkUiTextRendering.Simple);
        Assert.True(size.Width <= 100.5);
        Assert.True(size.Height > font.Spacing * 2);
    }

    [Fact]
    public void DrawingAtAWiderWidthReusesTheMeasuredLayout()
    {
        var layout = new SkUiTextLayout();
        var typeface = Primary();
        layout.Measure("Item 0001", typeface, 16, default, 300, SkUiCoreTextLineBreakers.WordWrap);
        using var bitmap = new SKBitmap(200, 30);
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint();
        layout.Draw(canvas, "Item 0001", typeface, 16, default, 200, 30, TextAlignment.Start, TextAlignment.Start, paint, SkUiCoreTextLineBreakers.WordWrap);
        Assert.Equal(1, layout.LayoutCount);
        // Narrower than the text forces a new wrap.
        layout.Draw(canvas, "Item 0001", typeface, 16, default, 30, 30, TextAlignment.Start, TextAlignment.Start, paint, SkUiCoreTextLineBreakers.WordWrap);
        Assert.Equal(2, layout.LayoutCount);
    }

    [Fact]
    public void LabelsExposeTextRendering()
    {
        var label = new SkUiLabel { Text = "Hi", TextRendering = SkUiTextRendering.Simple };
        Assert.Equal(SkUiTextRendering.Simple, label.TextRendering);
        var core = new SkUiCoreLabel().SetTextRendering(SkUiTextRendering.Shaped);
        Assert.Equal(SkUiTextRendering.Shaped, core.TextRendering);
    }
}
