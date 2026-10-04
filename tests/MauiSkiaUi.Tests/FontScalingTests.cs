using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// System text size on drawn text (P10 in ImplementationPlan.md): <c>FontAutoScalingEnabled</c> on labels, buttons, radio
/// button text, spans, HTML and font images, on both layers. In the global collection: the scale is process-wide.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class FontScalingTests : IDisposable
{
    private readonly IDisposable _font = SkUiTestHelpers.UseBundledFont();

    public void Dispose()
    {
        SkUiFontScaling.Factor = null;
        SkUiLook.Current.FontScale = 1;
        _font.Dispose();
    }

    private static Size Measure(IView view)
    {
        if (view is SkUiView drawn)
            Rendering.SkUiRenderInvalidation.MarkLookChanged(drawn);
        return view.Measure(double.PositiveInfinity, double.PositiveInfinity);
    }

    [Fact]
    public void TextFollowsTheScaleUnlessDisabled()
    {
        var label = new SkUiLabel { Text = "Scaled text", FontFamily = SkUiTestHelpers.BundledFontFamily };
        var fixedLabel = new SkUiLabel { Text = "Scaled text", FontFamily = SkUiTestHelpers.BundledFontFamily, FontAutoScalingEnabled = false };
        var button = new SkUiButton { Text = "Button", FontFamily = SkUiTestHelpers.BundledFontFamily, Padding = 0, MinimumHeightRequest = 0 };
        var radio = new SkUiRadioButton { Content = "Radio", FontFamily = SkUiTestHelpers.BundledFontFamily };
        var normal = (Label: Measure(label), Fixed: Measure(fixedLabel), Button: Measure(button), Radio: Measure(radio));

        SkUiFontScaling.Factor = 2;

        Assert.Equal(normal.Label.Width * 2, Measure(label).Width, 1);
        Assert.Equal(normal.Label.Height * 2, Measure(label).Height, 1);
        Assert.Equal(normal.Fixed, Measure(fixedLabel));
        Assert.Equal(normal.Button.Width * 2, Measure(button).Width, 1);
        Assert.True(Measure(radio).Width > normal.Radio.Width + 30);
    }

    [Fact]
    public void CoreLabelsFollowTheScale()
    {
        var label = new SkUiCoreLabel { Text = "Core text", FontFamily = SkUiTestHelpers.BundledFontFamily };
        var normal = label.Measure(double.PositiveInfinity, double.PositiveInfinity);

        SkUiFontScaling.Factor = 1.5;
        label.InvalidateMeasure();
        Assert.Equal(normal.Width * 1.5, label.Measure(double.PositiveInfinity, double.PositiveInfinity).Width, 1);

        label.SetFontAutoScalingEnabled(false);
        Assert.Equal(normal, label.Measure(double.PositiveInfinity, double.PositiveInfinity));
    }

    [Fact]
    public void SpansAndHtmlAreRebuiltAtTheNewScale()
    {
        var spans = new SkUiLabel { FontFamily = SkUiTestHelpers.BundledFontFamily };
        spans.FormattedText = new FormattedString { Spans = { new Span { Text = "Small " }, new Span { Text = "Big", FontSize = 30 } } };
        var html = new SkUiLabel { Text = "<b>Bold</b> text", TextType = TextType.Html, FontFamily = SkUiTestHelpers.BundledFontFamily };
        var normal = (Spans: Measure(spans), Html: Measure(html));

        SkUiFontScaling.Factor = 2;

        Assert.Equal(normal.Spans.Width * 2, Measure(spans).Width, 1);
        Assert.Equal(normal.Html.Width * 2, Measure(html).Width, 1);
    }

    [Fact]
    public void FontImagesUseTheScaleAtCreation()
    {
        SkUiFontScaling.Factor = 2;

        var scaled = new SkUiFontImageSource("A", size: 20);
        var fixedSize = new SkUiFontImageSource("A", size: 20, fontAutoScalingEnabled: false);

        Assert.Equal(40, scaled.DrawnSize);
        Assert.Equal(20, fixedSize.DrawnSize);
        Assert.NotEqual(scaled.CacheKey, fixedSize.CacheKey);
        var maui = (SkUiFontImageSource)SkUiMauiImageSources.Convert(new FontImageSource { Glyph = "A", Size = 20, FontAutoScalingEnabled = false })!;
        Assert.Equal(20, maui.DrawnSize);
    }

    [Fact]
    public void LookFontScalePrescalesAllText()
    {
        var auto = new SkUiLabel { Text = "Prescaled", FontFamily = SkUiTestHelpers.BundledFontFamily };
        var fixedLabel = new SkUiLabel { Text = "Prescaled", FontFamily = SkUiTestHelpers.BundledFontFamily, FontAutoScalingEnabled = false };
        var core = new SkUiCoreLabel { Text = "Prescaled", FontFamily = SkUiTestHelpers.BundledFontFamily, FontAutoScalingEnabled = false };
        var spans = new SkUiLabel { FontFamily = SkUiTestHelpers.BundledFontFamily, FontAutoScalingEnabled = false };
        spans.FormattedText = new FormattedString { Spans = { new Span { Text = "Span " }, new Span { Text = "text", FontSize = 24 } } };
        var normal = (Auto: Measure(auto), Fixed: Measure(fixedLabel), Core: core.Measure(double.PositiveInfinity, double.PositiveInfinity), Spans: Measure(spans));

        // Every text, auto scaling or not.
        SkUiLook.Current.FontScale = 1.5;
        core.InvalidateMeasure();
        Assert.Equal(normal.Auto.Width * 1.5, Measure(auto).Width, 1);
        Assert.Equal(normal.Fixed.Width * 1.5, Measure(fixedLabel).Width, 1);
        Assert.Equal(normal.Core.Width * 1.5, core.Measure(double.PositiveInfinity, double.PositiveInfinity).Width, 1);
        Assert.Equal(normal.Spans.Width * 1.5, Measure(spans).Width, 1);
        Assert.Equal(30, new SkUiFontImageSource("A", size: 20, fontAutoScalingEnabled: false).DrawnSize);

        // Prescale × system scale for text that auto scales; the prescale alone otherwise.
        SkUiFontScaling.Factor = 2;
        Assert.Equal(normal.Auto.Width * 3, Measure(auto).Width, 1);
        Assert.Equal(normal.Fixed.Width * 1.5, Measure(fixedLabel).Width, 1);
        Assert.Equal(48, SkUiFontScaling.ScaleFontSize(16));
        Assert.Equal(24, SkUiFontScaling.ScaleFontSize(16, autoScalingEnabled: false));
        Assert.Throws<ArgumentOutOfRangeException>(() => SkUiLook.Current.FontScale = 0);
    }

    [Fact]
    public void LookFontScaleChangesRaiseLookChanged()
    {
        var raised = 0;
        EventHandler handler = (_, _) => raised++;
        SkUiLook.CurrentChanged += handler;
        try
        {
            var version = SkUiFontScaling.Version;
            SkUiLook.Current.FontScale = 1.2;
            SkUiLook.Current.FontScale = 1.2;
            new DefaultSkUiLook().FontScale = 3; // not the current look: nothing to redraw
            Assert.Equal(1, raised);
            Assert.True(SkUiFontScaling.Version > version);
        }
        finally
        {
            SkUiLook.CurrentChanged -= handler;
        }
    }

    [Fact]
    public void ChangesAreRaised()
    {
        var raised = 0;
        EventHandler handler = (_, _) => raised++;
        SkUiFontScaling.Changed += handler;
        try
        {
            var version = SkUiFontScaling.Version;
            SkUiFontScaling.Factor = 1.25;
            SkUiFontScaling.Factor = 1.25;
            SkUiFontScaling.Factor = null;
            Assert.Equal(2, raised);
            Assert.Equal(version + 2, SkUiFontScaling.Version);
            Assert.Equal(16, SkUiFontScaling.ScaleFontSize(16)); // no system scale headless
            Assert.Throws<ArgumentOutOfRangeException>(() => SkUiFontScaling.Factor = 0);
        }
        finally
        {
            SkUiFontScaling.Changed -= handler;
        }
    }
}
