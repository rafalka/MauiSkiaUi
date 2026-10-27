using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

[Collection(GlobalStateCollection.Name)]
public class PlatformLookTextTests
{
    [Fact]
    public void LooksHaveTheirPlatformsBodySize()
    {
        Assert.Equal(16, DefaultSkUiLook.Instance.DefaultFontSize);
        Assert.Equal(14, AndroidSkUiLook.Instance.DefaultFontSize);
        Assert.Equal(17, IosSkUiLook.Instance.DefaultFontSize);
        Assert.Equal(17, MacSkUiLook.Instance.DefaultFontSize); // scaled to 77% by Mac Catalyst's iPad idiom, as MAUI's labels
        Assert.Equal(14, AndroidMaterial3SkUiLook.Instance.DefaultFontSize);
        Assert.True(AndroidSkUiLook.Instance.TextIncludesFontPadding);
        Assert.True(IosSkUiLook.Instance.AppliesFontTracking);
        Assert.False(DefaultSkUiLook.Instance.AppliesFontTracking);
        Assert.Equal(14, WindowsSkUiLook.Instance.DefaultFontSize);
        Assert.Null(DefaultSkUiLook.Instance.DefaultFontFamily);
        Assert.Equal("Segoe UI Variable", WindowsSkUiLook.Instance.DefaultFontFamily);
    }

    [Fact]
    public void AndroidFontPaddingIsTheSameWithAndWithoutALineGap()
    {
        // The font padding above the first line counts from the glyphs' ascent in plain and span text alike: the half of a
        // line gap placed above the glyphs does not take its place, so a label measures as it did before the gap was split.
        using var _ = SkUiTestHelpers.UseBundledFontWithLineGap();
        var previous = SkUiLook.Current;
        try
        {
            SkUiLook.Current = AndroidSkUiLook.Instance;
            using var font = new SKFont(SKTypeface.FromData(SKData.CreateCopy(SkUiTestHelpers.BundledFontWithLineGap())), 16);
            var metrics = font.Metrics;
            var padded = font.Spacing + (metrics.Ascent - metrics.Top) + (metrics.Bottom - metrics.Descent);
            Assert.True(metrics.Ascent - metrics.Top > 0.5f, "the font has no padding above its ascent");

            var plain = new SkUiLabel { Text = "Hx", FontFamily = SkUiTestHelpers.LineGapFontFamily, FontSize = 16 };
            var spans = new SkUiLabel
            {
                FontFamily = SkUiTestHelpers.LineGapFontFamily,
                FontSize = 16,
                FormattedText = new FormattedString { Spans = { new Span { Text = "H" }, new Span { Text = "x" } } }
            };
            Assert.Equal(padded, ((IView)plain).Measure(500, double.PositiveInfinity).Height, 0.5);
            Assert.Equal(padded, ((IView)spans).Measure(500, double.PositiveInfinity).Height, 0.5);
        }
        finally
        {
            SkUiLook.Current = previous;
        }
    }

    [Fact]
    public void ControlsCreatedUnderALookTakeItsFontSizeUnlessTheySetOne()
    {
        var previous = SkUiLook.Current;
        try
        {
            SkUiLook.Current = IosSkUiLook.Instance;
            Assert.Equal(17, new SkUiLabel().FontSize);
            Assert.Equal(17, new SkUiButton().FontSize);
            Assert.Equal(17, new SkUiCoreLabel().FontSize);
            Assert.Equal(17, new SkUiRadioButton().FontSize);
            Assert.Equal(17, new SkUiEntry().FontSize);
            Assert.Equal(20, new SkUiLabel { FontSize = 20 }.FontSize);
            SkUiLook.Current = AndroidSkUiLook.Instance;
            Assert.Equal(14, new SkUiLabel().FontSize);
        }
        finally
        {
            SkUiLook.Current = previous;
        }
    }
}
