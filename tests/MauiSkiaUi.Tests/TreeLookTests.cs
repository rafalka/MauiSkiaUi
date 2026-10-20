using SkiaSharp;
using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

[Collection(GlobalStateCollection.Name)]
public class TreeLookTests
{
    [Fact]
    public void ASubtreeMeasuresWithItsOwnLook()
    {
        var inside = new SkUiSwitch();
        var scoped = new SkUiBorder { Content = inside, Look = WindowsSkUiLook.Instance };
        var outside = new SkUiSwitch();
        var root = new SkUiVerticalStackLayout { Children = { scoped, outside } };

        ((IView)root).Measure(500, 500);

        Assert.Equal(new Size(40, 40), ((IView)inside).DesiredSize);
        Assert.Equal(new Size(51, 31), ((IView)outside).DesiredSize);
        Assert.Same(DefaultSkUiLook.Instance, SkUiLook.Current);
    }

    [Fact]
    public void WindowsMinimumWidthsGiveWayToTheControlsOwn()
    {
        // WinUI's check box and radio button are at least 120 wide; MAUI's MinimumWidthRequest replaces that MinWidth.
        var box = new SkUiCheckBox();
        var styledBox = new SkUiCheckBox { MinimumWidthRequest = 44, MinimumHeightRequest = 44 };
        var radio = new SkUiRadioButton();
        var styledRadio = new SkUiRadioButton { MinimumWidthRequest = 44 };
        var root = new SkUiVerticalStackLayout { Look = WindowsSkUiLook.Instance, Children = { box, styledBox, radio, styledRadio } };

        ((IView)root).Measure(500, 500);

        Assert.Equal(new Size(120, 32), ((IView)box).DesiredSize);
        Assert.Equal(new Size(44, 44), ((IView)styledBox).DesiredSize);
        Assert.Equal(new Size(120, 32), ((IView)radio).DesiredSize);
        Assert.Equal(new Size(44, 32), ((IView)styledRadio).DesiredSize);
    }

    [Fact]
    public void ChangingTheLookRemeasuresTheSubtree()
    {
        var inside = new SkUiSwitch();
        var scoped = new SkUiBorder { Content = inside };
        ((IView)scoped).Measure(500, 500);
        Assert.Equal(new Size(51, 31), ((IView)inside).DesiredSize);

        scoped.Look = AndroidSkUiLook.Instance;
        ((IView)scoped).Measure(500, 500);
        Assert.Equal(new Size(48, 48), ((IView)inside).DesiredSize);

        scoped.Look = null;
        ((IView)scoped).Measure(500, 500);
        Assert.Equal(new Size(51, 31), ((IView)inside).DesiredSize);
    }

    [Fact]
    public void ANearerLookWinsAndCoreNodesTakeTheirAncestors()
    {
        var innerSwitch = new SkUiSwitch();
        var inner = new SkUiBorder { Content = innerSwitch, Look = AndroidSkUiLook.Instance };
        var outer = new SkUiBorder { Content = inner, Look = WindowsSkUiLook.Instance };
        ((IView)outer).Measure(500, 500);
        Assert.Equal(new Size(48, 48), ((IView)innerSwitch).DesiredSize);
    }

    [Fact]
    public void TextTakesItsSubtreesLookFontSize()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var scopedLabel = new SkUiLabel { Text = "Sample", FontFamily = SkUiTestHelpers.BundledFontFamily };
        var scoped = new SkUiBorder { Content = scopedLabel, Look = IosSkUiLook.Instance };
        var sized = new SkUiLabel { Text = "Sample", FontFamily = SkUiTestHelpers.BundledFontFamily, FontSize = IosSkUiLook.Instance.DefaultFontSize };
        ((IView)scoped).Measure(500, 500);
        ((IView)sized).Measure(500, 500);
        // The bundled font has no tracking table, so only the size differs from the app's look.
        Assert.Equal(((IView)sized).DesiredSize, ((IView)scopedLabel).DesiredSize);
        Assert.Equal(DefaultSkUiLook.Instance.DefaultFontSize, scopedLabel.FontSize); // the getter reports the app look's
    }
    [Fact]
    public void AButtonTakesItsSubtreesLookPaddingCornersAndBorder()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        // Created under the app's look, then put under a subtree's (as XAML builds children before parenting them).
        var button = new SkUiButton { Text = "OK", FontFamily = SkUiTestHelpers.BundledFontFamily };
        var windows = new SkUiButton
        {
            Text = "OK", FontFamily = SkUiTestHelpers.BundledFontFamily,
            Padding = WindowsSkUiLook.Instance.DefaultButtonPadding, CornerRadius = 4, BorderWidth = 1
        };
        var own = new SkUiButton { Text = "OK", FontFamily = SkUiTestHelpers.BundledFontFamily, Padding = new Thickness(30) };
        var root = new SkUiVerticalStackLayout { Children = { button, windows, own } };
        root.Look = WindowsSkUiLook.Instance;

        ((IView)root).Measure(500, 500);

        Assert.Equal(((IView)windows).DesiredSize, ((IView)button).DesiredSize);
        Assert.Equal(new Microsoft.Maui.CornerRadius(4), button.PressEffectCornerRadii);
        Assert.True(((IView)own).DesiredSize.Height >= 60 + 1); // its own padding wins
        Assert.Equal(new Thickness(30), own.Padding);

        root.Look = null;
        ((IView)root).Measure(500, 500);
        Assert.Equal(new Microsoft.Maui.CornerRadius(DefaultSkUiLook.Instance.DefaultButtonCornerRadius), button.PressEffectCornerRadii);
    }

    [Fact]
    public void CoreButtonsTakeTheLooksPaddingAndBorderUnlessSet()
    {
        var button = new SkUiCoreButton();
        Assert.Equal(DefaultSkUiLook.Instance.DefaultButtonPadding, button.Padding);
        Assert.Equal(0, button.BorderWidth);

        button.SetLook(WindowsSkUiLook.Instance);
        button.Measure(500, 500);
        Assert.Equal(WindowsSkUiLook.Instance.DefaultButtonPadding, button.Padding);
        Assert.Equal(1, button.BorderWidth);

        var own = new SkUiCoreButton();
        own.SetPadding(new Thickness(6)).SetBorderWidth(2);
        own.SetLook(WindowsSkUiLook.Instance);
        own.Measure(500, 500);
        Assert.Equal(new Thickness(6), own.Padding);
        Assert.Equal(2, own.BorderWidth);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RichTextIncludesAndroidsFontPaddingAsPlainTextDoes(bool bundledFont)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        // Roboto Mono's top is its ascent (no padding to add); the system font, where there is one, has some.
        var family = bundledFont ? SkUiTestHelpers.BundledFontFamily : null;
        const string first = "Sample";
        const string second = "Second line";
        SkUiLabel Rich() => new()
        {
            FontFamily = family, FontSize = 14,
            FormattedText = new FormattedString { Spans = { new Span { Text = first + "\n" }, new Span { Text = second } } }
        };
        var plain = new SkUiLabel { Text = first + "\n" + second, FontFamily = family, FontSize = 14 };
        var rich = Rich();
        var unpadded = Rich();
        var android = new SkUiVerticalStackLayout { Look = AndroidSkUiLook.Instance, Children = { plain, rich } };

        ((IView)android).Measure(500, 500);
        ((IView)unpadded).Measure(500, 500);

        Assert.Equal(((IView)plain).DesiredSize.Height, ((IView)rich).DesiredSize.Height, 3);
        using var font = new SKFont(SKTypeface.Default, 14);
        if (!bundledFont && font.Metrics.Top < font.Metrics.Ascent)
            Assert.True(((IView)rich).DesiredSize.Height > ((IView)unpadded).DesiredSize.Height);
    }
}
