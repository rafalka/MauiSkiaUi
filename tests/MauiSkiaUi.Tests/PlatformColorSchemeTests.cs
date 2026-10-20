using Xunit;

namespace MauiSkiaUi.Tests;

public class PlatformColorSchemeTests
{
    public static TheoryData<DefaultSkUiLook> Looks => new()
    {
        DefaultSkUiLook.Instance, AndroidSkUiLook.Instance, AndroidMaterial3SkUiLook.Instance, IosSkUiLook.Instance, MacSkUiLook.Instance,
        WindowsSkUiLook.Instance
    };

    [Fact]
    public void LooksPairWithTheirSchemes()
    {
        Assert.Same(LightSkUiColorScheme.Instance, DefaultSkUiLook.Instance.GetColorScheme(false));
        Assert.Same(DarkSkUiColorScheme.Instance, DefaultSkUiLook.Instance.GetColorScheme(true));
        Assert.Same(IosSkUiColorScheme.Light, IosSkUiLook.Instance.GetColorScheme(false));
        Assert.Same(MacSkUiColorScheme.Dark, MacSkUiLook.Instance.GetColorScheme(true));
        Assert.Same(AndroidSkUiColorScheme.Light, AndroidSkUiLook.Instance.GetColorScheme(false));
        Assert.Same(WindowsSkUiColorScheme.Dark, WindowsSkUiLook.Instance.GetColorScheme(true));
        Assert.Equal(Color.FromArgb("#007AFF"), IosSkUiColorScheme.Light.Accent);
        Assert.Same(AndroidMaterial3SkUiColorScheme.Light, AndroidMaterial3SkUiLook.Instance.GetColorScheme(false));
        Assert.Equal(Color.FromArgb("#6200EE"), AndroidSkUiColorScheme.Light.Accent);
        Assert.Equal(Color.FromArgb("#6750A4"), AndroidMaterial3SkUiColorScheme.Light.Accent);
    }

    [Theory]
    [MemberData(nameof(Looks))]
    public void LightSchemesAreLightAndDarkSchemesAreDark(DefaultSkUiLook look)
    {
        var light = look.GetColorScheme(false);
        var dark = look.GetColorScheme(true);
        Assert.True(light.DefaultBackground.GetLuminosity() > 0.5f);
        Assert.True(dark.DefaultBackground.GetLuminosity() < 0.2f);
        Assert.True(light.DefaultForeground.GetLuminosity() < 0.3f);
        Assert.True(dark.DefaultForeground.GetLuminosity() > 0.7f);
    }

    [Fact]
    public void WithAccentKeepsTheRestAndDoesNotTouchTheSharedInstance()
    {
        var custom = IosSkUiColorScheme.WithAccent(Colors.Red, true);
        Assert.Equal(Colors.Red, custom.Accent);
        Assert.Equal(IosSkUiColorScheme.Dark.DefaultBackground, custom.DefaultBackground);
        Assert.NotEqual(Colors.Red, IosSkUiColorScheme.Dark.Accent);
    }
}
