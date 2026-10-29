using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// K3: plain Latin text is shaped with HarfBuzz as one run (kerning, as the platforms' text engines), and Apple's looks draw
/// fonts with an optical size axis at the optical size of their point size, as CoreText does.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class TextKerningAndOpticalSizeTests
{
    // A font whose shaping changes Latin text (kerning, contextual forms); the bundled Open Sans has no kerning.
    private static SKTypeface Lobster() =>
        SKTypeface.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "Lobster-Regular.ttf"));

    private static double Width(string text, SKTypeface typeface, SkUiTextRendering rendering, double size = 17) =>
        new SkUiTextLayout().Measure(text, new SkUiTextStyle(typeface, size, LineBreakMode.NoWrap, Rendering: rendering), default, double.PositiveInfinity).Width;

    [Fact]
    public void PlainTextIsShapedAsShapedTextAndTheSimplePathIsNot()
    {
        using var typeface = Lobster();
        const string text = "To Yo AV";
        var plain = Width(text, typeface, SkUiTextRendering.Auto);
        var shaped = Width(text, typeface, SkUiTextRendering.Shaped);
        var simple = Width(text, typeface, SkUiTextRendering.Simple);
        Assert.Equal(shaped, plain, 0.01);
        Assert.True(Math.Abs(simple - plain) > 1, $"simple {simple} plain {plain}");
        using var font = new SKFont(typeface, 17) { LinearMetrics = true };
        Assert.Equal(font.MeasureText(text), simple, 0.01);
    }

    [Fact]
    public void ShapedAdvancesAreNotRoundedToCoarseUnits()
    {
        // 1/512 em (SkiaSharp's shaper) would put a 77-character monospace line 0.75 DIPs off at 20.
        using var typeface = TextRenderingModeTests.Primary();
        var text = new string('a', 77);
        using var font = new SKFont(typeface, 20) { LinearMetrics = true };
        Assert.Equal(font.MeasureText(text), Width(text, typeface, SkUiTextRendering.Auto, 20), 0.05);
    }

    [Fact]
    public void AppleLooksDrawTheSystemFontAtTheOpticalSizeOfItsPointSize()
    {
        using var system = SKTypeface.FromFamilyName(".AppleSystemUIFont");
        if (system.FamilyName != ".AppleSystemUIFont")
            return; // no Apple system font on this host
        var opsz = SKFourByteTag.Parse("opsz");
        float OpticalSize(SKTypeface typeface) => typeface.VariationDesignPosition.FirstOrDefault(p => p.Axis == opsz).Value;

        var display = SkUiOpticalSize.At(system, 28);
        Assert.NotSame(system, display);
        Assert.Equal(28, OpticalSize(display));
        Assert.Same(display, SkUiOpticalSize.At(system, 28)); // one instance per size
        Assert.Same(system, SkUiOpticalSize.Original(display));
        Assert.Same(SkUiOpticalSize.At(system, 17), SkUiOpticalSize.At(system, 12)); // clamped to the axis' 17

        var previous = SkUiLook.Current;
        try
        {
            SkUiLook.Current = IosSkUiLook.Instance;
            var optical = new SkUiTextLayout().Measure("Sample text", new SkUiTextStyle(system, 28, LineBreakMode.NoWrap), default, double.PositiveInfinity).Width;
            SkUiLook.Current = DefaultSkUiLook.Instance;
            var fixedSize = new SkUiTextLayout().Measure("Sample text", new SkUiTextStyle(system, 28, LineBreakMode.NoWrap), default, double.PositiveInfinity).Width;
            Assert.True(fixedSize - optical > 10, $"fixed {fixedSize} optical {optical}"); // the display design is tighter
        }
        finally
        {
            SkUiLook.Current = previous;
        }
    }

    [Fact]
    public void OnlyTheAppleLooksApplyTheOpticalSizeAndCatalystTracksLargeTextAtItsPointSize()
    {
        Assert.True(IosSkUiLook.Instance.AppliesOpticalSize);
        Assert.True(MacSkUiLook.Instance.AppliesOpticalSize);
        Assert.False(DefaultSkUiLook.Instance.AppliesOpticalSize);
        Assert.False(AndroidSkUiLook.Instance.AppliesOpticalSize);
        Assert.Equal(20, MacSkUiLook.Instance.FontTrackingScaledBelow);
        Assert.Equal(double.PositiveInfinity, IosSkUiLook.Instance.FontTrackingScaledBelow);
    }
}
