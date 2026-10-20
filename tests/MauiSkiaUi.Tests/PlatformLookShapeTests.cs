using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

public class PlatformLookShapeTests
{
    public static TheoryData<DefaultSkUiLook> Looks => new()
    {
        DefaultSkUiLook.Instance, AndroidSkUiLook.Instance, IosSkUiLook.Instance, MacSkUiLook.Instance, WindowsSkUiLook.Instance
    };

    [Fact]
    public void LooksHaveTheirPlatformsSizes()
    {
        Assert.Equal(new Size(51, 31), DefaultSkUiLook.Instance.DefaultSwitchSize);
        Assert.Equal(new Size(51, 31), IosSkUiLook.Instance.DefaultSwitchSize);
        Assert.Equal(new Size(52, 32), AndroidSkUiLook.Instance.DefaultSwitchSize);
        Assert.Equal(new Size(40, 20), WindowsSkUiLook.Instance.DefaultSwitchSize);
        Assert.Equal(new Size(38, 22), MacSkUiLook.Instance.DefaultSwitchSize);
        Assert.Equal(new Size(18, 18), AndroidSkUiLook.Instance.DefaultCheckBoxSize);
        Assert.Equal(2, AndroidSkUiLook.Instance.GetCheckBoxCornerRadius(18));
        Assert.Equal(4, WindowsSkUiLook.Instance.GetCheckBoxCornerRadius(20));
        Assert.Equal(11, IosSkUiLook.Instance.GetCheckBoxCornerRadius(22));
        Assert.Equal(40, AndroidSkUiLook.Instance.DefaultButtonMinimumHeight);
        Assert.Equal(44, IosSkUiLook.Instance.DefaultButtonMinimumHeight);
        Assert.Equal(32, WindowsSkUiLook.Instance.DefaultButtonMinimumHeight);
        Assert.Equal(SkUiSwitchStyle.Outlined, WindowsSkUiLook.Instance.SwitchStyle);
        Assert.Equal(SkUiSwitchStyle.Pill, IosSkUiLook.Instance.SwitchStyle);
    }

    [Theory]
    [MemberData(nameof(Looks))]
    public void EveryLookDrawsItsControls(DefaultSkUiLook look)
    {
        var size = look.DefaultSwitchSize;
        foreach (var state in new[] { SkUiCheckState.Unchecked, SkUiCheckState.Checked, SkUiCheckState.Indeterminate })
        {
            var visual = SkUiToggleVisual.Settled(state);
            using var bitmap = new SKBitmap(60, 40);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.White);
            look.DrawSwitch(canvas, new SkUiSwitchPaint(new SKRect(2, 2, 2 + (float)size.Width, 2 + (float)size.Height), visual,
                SKColors.Blue, SKColors.Gray, SKColors.White, true));
            Assert.True(HasInk(bitmap), $"{look.GetType().Name} switch {state}");

            bitmap.Erase(SKColors.White);
            var box = (float)look.DefaultCheckBoxSize.Width;
            look.DrawCheckBox(canvas, new SkUiCheckBoxPaint(box, visual, SKColors.Blue, SKColors.White, SKColors.Gray, true));
            Assert.True(HasInk(bitmap), $"{look.GetType().Name} check box {state}");

            bitmap.Erase(SKColors.White);
            var radio = (float)look.DefaultRadioButtonSize.Width;
            look.DrawRadioButton(canvas, new SkUiRadioButtonPaint(radio, visual, SKColors.Blue, SKColors.Gray, true));
            Assert.True(HasInk(bitmap), $"{look.GetType().Name} radio {state}");
        }
    }

    [Fact]
    public void AnOutlinedSwitchIsHollowWhenOffAndFilledWhenOn()
    {
        var look = WindowsSkUiLook.Instance;
        var bounds = new SKRect(2, 2, 42, 22);
        using var bitmap = new SKBitmap(50, 30);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        look.DrawSwitch(canvas, new SkUiSwitchPaint(bounds, SkUiToggleVisual.Settled(SkUiCheckState.Unchecked), SKColors.Blue, SKColors.Black, SKColors.White, true));
        Assert.Equal(SKColors.White, bitmap.GetPixel(30, 12)); // inside the track, away from the thumb
        canvas.Clear(SKColors.White);
        look.DrawSwitch(canvas, new SkUiSwitchPaint(bounds, SkUiToggleVisual.Settled(SkUiCheckState.Checked), SKColors.Blue, SKColors.Black, SKColors.White, true));
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(10, 12));
    }

    private static bool HasInk(SKBitmap bitmap)
    {
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y) != SKColors.White)
                    return true;
        return false;
    }
}
