using MauiSkiaUi.Core;
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
        Assert.Equal(13, MacSkUiLook.Instance.DefaultFontSize);
        Assert.Equal(14, WindowsSkUiLook.Instance.DefaultFontSize);
        Assert.Null(DefaultSkUiLook.Instance.DefaultFontFamily);
        Assert.Equal("Segoe UI Variable", WindowsSkUiLook.Instance.DefaultFontFamily);
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
