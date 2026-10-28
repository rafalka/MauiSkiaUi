using SkiaSharp;
using MauiSkiaUi.Core;
using MauiSkiaUi.Rendering;
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
    [Fact]
    public void ZeroMinimumWidthsReplaceTheLooksOnBothLayers()
    {
        var box = new SkUiCheckBox { MinimumWidthRequest = 0 };
        var coreBox = new SkUiCoreCheckBox();
        coreBox.SetMinimumWidth(0);
        var coreRadio = new SkUiCoreRadioButton();
        var coreRadioOwn = new SkUiCoreRadioButton();
        coreRadioOwn.SetMinimumWidth(0);
        var root = new SkUiVerticalStackLayout { Look = WindowsSkUiLook.Instance, Children = { box } };
        foreach (var node in new SkUiCoreNode[] { coreBox, coreRadio, coreRadioOwn })
            node.SetLook(WindowsSkUiLook.Instance);

        ((IView)root).Measure(500, 500);
        coreBox.Measure(500, 500);
        coreRadio.Measure(500, 500);
        coreRadioOwn.Measure(500, 500);

        Assert.True(((IView)box).DesiredSize.Width < 120);
        Assert.Equal(((IView)box).DesiredSize.Width, coreBox.DesiredSize.Width);
        Assert.Equal(120, coreRadio.DesiredSize.Width); // as SkUiRadioButton's: WinUI's minimum
        Assert.True(coreRadioOwn.DesiredSize.Width < 120);
    }

    [Fact]
    public void ARichLabelFollowsItsSubtreesLookFontSize()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        SkUiLabel Rich(double? size = null)
        {
            var span = new Span { Text = "Sample" };
            if (size is { } fontSize)
                span.FontSize = fontSize;
            return new SkUiLabel { FontFamily = SkUiTestHelpers.BundledFontFamily, FormattedText = new FormattedString { Spans = { span } } };
        }
        var rich = Rich();
        var scoped = new SkUiBorder { Content = rich };
        var sized = Rich(IosSkUiLook.Instance.DefaultFontSize);
        ((IView)scoped).Measure(500, 500);
        ((IView)sized).Measure(500, 500);
        Assert.NotEqual(((IView)sized).DesiredSize, ((IView)rich).DesiredSize);

        scoped.SetLook(IosSkUiLook.Instance); // built under the app's look: rebuilt with the subtree's
        ((IView)scoped).Measure(500, 500);
        Assert.Equal(((IView)sized).DesiredSize, ((IView)rich).DesiredSize);
    }

    [Fact]
    public void ANodeTakesItsNewParentsLookAndASeparateSurfaceLeavesTheScope()
    {
        var toggle = new SkUiSwitch();
        var windows = new SkUiBorder { Look = WindowsSkUiLook.Instance, Content = toggle };
        ((IView)windows).Measure(500, 500);
        Assert.Equal(new Size(40, 40), ((IView)toggle).DesiredSize);

        windows.Content = null;
        var android = new SkUiBorder { Look = AndroidSkUiLook.Instance, Content = toggle };
        ((IView)android).Measure(500, 500);
        Assert.Equal(new Size(48, 48), ((IView)toggle).DesiredSize);

        // A surface root measured inside another subtree's measure (native content hosting another surface) uses the app's look.
        android.Content = null;
        using (SkUiLookScope.Enter(WindowsSkUiLook.Instance))
        {
            ((IView)toggle).InvalidateMeasure();
            ((IView)toggle).Measure(500, 500);
            Assert.Same(WindowsSkUiLook.Instance, SkUiLook.Current);
        }
        Assert.Equal(new Size(51, 31), ((IView)toggle).DesiredSize);
    }
    [Fact]
    public void PlainTextFontPaddingCountsFallbackFontsAsRichTextDoes()
    {
        // With the system's fonts the emoji falls back to a taller font; without any (Linux CI) both measure the same way.
        const string text = "Sample \U0001F600 text";
        var plain = new SkUiLabel { Text = text, FontSize = 14 };
        var rich = new SkUiLabel { FontSize = 14, FormattedText = new FormattedString { Spans = { new Span { Text = text } } } };
        var android = new SkUiVerticalStackLayout { Look = AndroidSkUiLook.Instance, Children = { plain, rich } };

        ((IView)android).Measure(500, 500);

        Assert.Equal(((IView)rich).DesiredSize.Height, ((IView)plain).DesiredSize.Height, 3);
    }

    [Fact]
    public void AThinDraggerTakesPointersAndDrawsAcrossItsSubtreesTouchThickness()
    {
        // Hit tests and render props run outside the paint's look scope; they still use the subtree's 44 DIP touch area.
        var look = new WideDraggerLook();
        var skui = new SkUiGridSplitter { HeightRequest = 1, WidthRequest = 200, ResizeDirection = SkUiGridResizeDirection.Rows };
        var grid = new SkUiGrid { Look = look, RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) } };
        grid.Add(skui, 0, 1);
        SkUiTestHelpers.Arrange(grid, 200, 101);
        var core = new SkUiCoreDragger().SetDragAxis(SkUiPanAxis.Vertical).SetLook(look);
        core.Measure(200, 1);
        core.Arrange(new Rect(0, 0, 200, 1));

        foreach (var node in new ISkUiRenderable[] { skui, core })
        {
            var props = SkUiRenderProps.Default;
            node.GetRenderProps(ref props);
            Assert.Equal(new SKRect(0, 21.5f, 0, 21.5f), props.Overflow);
            Assert.True(((ISkUiInputNode)node).HitsOutsideBounds(new Point(100, -20)));
            Assert.False(((ISkUiInputNode)node).HitsOutsideBounds(new Point(100, -23)));
        }
    }

    private sealed class WideDraggerLook : DefaultSkUiLook
    {
        public override double DraggerTouchThickness => 44;
    }
}
