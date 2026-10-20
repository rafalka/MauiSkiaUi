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
}
