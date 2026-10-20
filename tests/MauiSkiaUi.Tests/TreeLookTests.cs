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

        Assert.Equal(new Size(40, 20), ((IView)inside).DesiredSize);
        Assert.Equal(new Size(51, 31), ((IView)outside).DesiredSize);
        Assert.Same(DefaultSkUiLook.Instance, SkUiLook.Current);
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
        Assert.Equal(new Size(52, 32), ((IView)inside).DesiredSize);

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
        Assert.Equal(new Size(52, 32), ((IView)innerSwitch).DesiredSize);
    }
}
