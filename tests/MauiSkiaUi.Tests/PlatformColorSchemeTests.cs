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

    [Fact]
    public void ControlTokensFollowTheirSchemesUntilSet()
    {
        // The default: white on the accent, no border, a muted spinner.
        var scheme = new LightSkUiColorScheme { Accent = Colors.Red };
        Assert.Equal(Colors.Red, scheme.ButtonBackground);
        Assert.Equal(Colors.White, scheme.ButtonForeground);
        Assert.Equal(Colors.Transparent, scheme.ButtonBorder);
        Assert.Equal(scheme.Muted, scheme.ActivityIndicator);
        scheme.ButtonBackground = Colors.Green;
        scheme.Accent = Colors.Blue;
        Assert.Equal(Colors.Green, scheme.ButtonBackground);

        // iOS and the Mac: a system button (no fill, tinted text); Android and Windows spin in the accent.
        var ios = IosSkUiColorScheme.WithAccent(Colors.Orange, false);
        Assert.Equal(Colors.Transparent, ios.ButtonBackground);
        Assert.Equal(Colors.Orange, ios.ButtonForeground);
        Assert.Equal(Colors.Transparent, MacSkUiColorScheme.Light.ButtonBackground);
        Assert.Equal(IosSkUiColorScheme.Light.Muted, IosSkUiColorScheme.Light.ActivityIndicator);
        Assert.Equal(AndroidSkUiColorScheme.Light.Accent, AndroidSkUiColorScheme.Light.ActivityIndicator);
        Assert.Equal(AndroidMaterial3SkUiColorScheme.Dark.Accent, AndroidMaterial3SkUiColorScheme.Dark.ActivityIndicator);
        Assert.Equal(AndroidSkUiColorScheme.Light.Accent, AndroidSkUiColorScheme.Light.ButtonBackground);

        // Windows: Fluent's light standard button with dark text and a faint border.
        var windows = WindowsSkUiColorScheme.WithAccent(Colors.Purple, false);
        Assert.Equal(Color.FromArgb("#B3FFFFFF"), windows.ButtonBackground);
        Assert.Equal(windows.DefaultForeground, windows.ButtonForeground);
        Assert.Equal(Color.FromArgb("#0F000000"), windows.ButtonBorder);
        Assert.Equal(Colors.Purple, windows.ActivityIndicator);
        Assert.Equal(Color.FromArgb("#0FFFFFFF"), WindowsSkUiColorScheme.Dark.ButtonBackground);

        var copy = new LightSkUiColorScheme();
        copy.CopyFrom(windows);
        Assert.Equal(windows.ButtonBackground, copy.ButtonBackground);
        Assert.Equal(Colors.Purple, copy.ActivityIndicator);
    }
}

[Collection(GlobalStateCollection.Name)]
public class ControlColorTokenTests
{
    [Fact]
    public void ButtonsAndSpinnersTakeTheSchemesColorsAndTheLooksBorder()
    {
        var scheme = SkUiColorScheme.Current;
        var look = SkUiLook.Current;
        try
        {
            SkUiColorScheme.Current = WindowsSkUiColorScheme.Light;
            SkUiLook.Current = WindowsSkUiLook.Instance;
            var button = new SkUiButton { Text = "OK" };
            Assert.Equal(WindowsSkUiColorScheme.Light.ButtonBackground, button.FillColor);
            Assert.Equal(WindowsSkUiColorScheme.Light.DefaultForeground, button.TextColor);
            Assert.Equal(WindowsSkUiColorScheme.Light.ButtonBorder, button.BorderColor);
            Assert.Equal(1, button.BorderWidth);
            var core = new MauiSkiaUi.Core.SkUiCoreButton();
            Assert.Equal(button.FillColor, core.FillColor);
            Assert.Equal(button.TextColor, core.TextColor);
            Assert.Equal(button.BorderColor, core.BorderColor);
            Assert.Equal(1, core.BorderWidth);
            Assert.Equal(WindowsSkUiColorScheme.Light.Accent, new SkUiActivityIndicator().Color);
            Assert.Equal(WindowsSkUiColorScheme.Light.Accent, new MauiSkiaUi.Core.SkUiCoreActivityIndicator().Color);
            // Explicit values win; a label keeps no border.
            Assert.Equal(Colors.Red, new SkUiButton { BorderColor = Colors.Red, BorderWidth = 3 }.BorderColor);
            Assert.Equal(0, new SkUiLabel().BorderWidth);
        }
        finally
        {
            SkUiColorScheme.Current = scheme;
            SkUiLook.Current = look;
        }
        var plain = new SkUiButton();
        Assert.Equal(SkUiColors.Accent, plain.FillColor);
        Assert.Equal(Colors.White, plain.TextColor);
        Assert.Equal(0, plain.BorderWidth);
    }
}
