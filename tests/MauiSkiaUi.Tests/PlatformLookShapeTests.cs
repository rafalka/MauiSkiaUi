using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

public class PlatformLookShapeTests
{
    public static TheoryData<DefaultSkUiLook> Looks => new()
    {
        DefaultSkUiLook.Instance, AndroidSkUiLook.Instance, AndroidMaterial3SkUiLook.Instance, IosSkUiLook.Instance, MacSkUiLook.Instance,
        WindowsSkUiLook.Instance
    };

    [Fact]
    public void LooksHaveTheirPlatformsSizes()
    {
        // The sizes MAUI's native controls measure at on each platform (iOS 26; Mac Catalyst in the iPad idiom draws iOS's).
        Assert.Equal(new Size(51, 31), DefaultSkUiLook.Instance.DefaultSwitchSize);
        Assert.Equal(new Size(63, 28), IosSkUiLook.Instance.DefaultSwitchSize);
        Assert.Equal(new Size(63, 28), MacSkUiLook.Instance.DefaultSwitchSize);
        Assert.Equal(new Size(48, 48), AndroidSkUiLook.Instance.DefaultSwitchSize);
        Assert.Equal(new Size(52, 48), AndroidMaterial3SkUiLook.Instance.DefaultSwitchSize);
        Assert.Equal(new Size(40, 40), WindowsSkUiLook.Instance.DefaultSwitchSize);
        Assert.Equal(20, WindowsSkUiLook.Instance.SwitchTrackHeight);
        Assert.Equal(new Size(32, 32), WindowsSkUiLook.Instance.DefaultCheckBoxSize);
        Assert.Equal(120, WindowsSkUiLook.Instance.DefaultCheckBoxMinimumWidth);
        Assert.Equal(120, WindowsSkUiLook.Instance.DefaultRadioButtonMinimumWidth);
        Assert.Equal(0, DefaultSkUiLook.Instance.DefaultCheckBoxMinimumWidth);
        Assert.Equal(3, WindowsSkUiLook.Instance.DefaultProgressBarHeight);
        Assert.Equal(new Size(32, 32), WindowsSkUiLook.Instance.StepperButtonSize);
        Assert.Equal(new Size(32, 32), AndroidSkUiLook.Instance.DefaultCheckBoxSize);
        Assert.Equal(new Size(48, 32), AndroidMaterial3SkUiLook.Instance.DefaultCheckBoxSize);
        Assert.Equal(new Size(18, 18), IosSkUiLook.Instance.DefaultCheckBoxSize);
        Assert.Equal(2, AndroidSkUiLook.Instance.GetCheckBoxCornerRadius(18));
        Assert.Equal(4, WindowsSkUiLook.Instance.GetCheckBoxCornerRadius(20));
        Assert.Equal(9, IosSkUiLook.Instance.GetCheckBoxCornerRadius(18));
        Assert.Equal(36, AndroidSkUiLook.Instance.DefaultButtonMinimumHeight);
        Assert.Equal(36, IosSkUiLook.Instance.DefaultButtonMinimumHeight);
        Assert.Equal(35, MacSkUiLook.Instance.DefaultButtonMinimumHeight);
        Assert.Equal(32, WindowsSkUiLook.Instance.DefaultButtonMinimumHeight);
        Assert.Equal(new Size(20, 20), IosSkUiLook.Instance.DefaultActivityIndicatorSize);
        Assert.Equal(new Size(48, 48), AndroidSkUiLook.Instance.DefaultActivityIndicatorSize);
        Assert.Equal(new Size(40, 40), AndroidMaterial3SkUiLook.Instance.DefaultActivityIndicatorSize);
        Assert.Equal(SkUiSwitchStyle.Outlined, WindowsSkUiLook.Instance.SwitchStyle);
        Assert.Equal(SkUiSwitchStyle.Outlined, AndroidMaterial3SkUiLook.Instance.SwitchStyle);
        Assert.Equal(SkUiSwitchStyle.ThumbOverTrack, AndroidSkUiLook.Instance.SwitchStyle);
        Assert.Equal(SkUiSwitchStyle.Pill, IosSkUiLook.Instance.SwitchStyle);
        Assert.Equal(37, IosSkUiLook.Instance.SwitchThumbWidth);
        Assert.Equal(SkUiSliderStyle.Handle, AndroidMaterial3SkUiLook.Instance.SliderStyle);
        Assert.Equal(SkUiSliderStyle.Thumb, AndroidSkUiLook.Instance.SliderStyle);
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
        // The 40 × 20 track centered in the 40 × 40 control.
        var bounds = new SKRect(2, -8, 42, 32);
        using var bitmap = new SKBitmap(50, 30);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        look.DrawSwitch(canvas, new SkUiSwitchPaint(bounds, SkUiToggleVisual.Settled(SkUiCheckState.Unchecked), SKColors.Blue, SKColors.Black, SKColors.White, true));
        Assert.Equal(SKColors.White, bitmap.GetPixel(30, 12)); // inside the track, away from the thumb
        canvas.Clear(SKColors.White);
        look.DrawSwitch(canvas, new SkUiSwitchPaint(bounds, SkUiToggleVisual.Settled(SkUiCheckState.Checked), SKColors.Blue, SKColors.Black, SKColors.White, true));
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(10, 12));
    }

    [Fact]
    public void FluentRadioButtonsFillAroundAWhiteDot()
    {
        var look = WindowsSkUiLook.Instance;
        using var bitmap = new SKBitmap(20, 20);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Black);
        look.DrawRadioButton(canvas, new SkUiRadioButtonPaint(20, SkUiToggleVisual.Settled(SkUiCheckState.Checked), SKColors.Blue, SKColors.Gray, true));
        Assert.Equal(SKColors.White, bitmap.GetPixel(10, 10)); // the dot
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(10, 2)); // the filled ring
        canvas.Clear(SKColors.Black);
        look.DrawRadioButton(canvas, new SkUiRadioButtonPaint(20, SkUiToggleVisual.Settled(SkUiCheckState.Unchecked), SKColors.Blue, SKColors.Gray, true));
        Assert.Equal(SKColors.Black, bitmap.GetPixel(10, 10));
        Assert.Equal(SKColors.Black, bitmap.GetPixel(10, 3)); // a thin ring only
    }

    [Fact]
    public void FluentSliderThumbsAreWhiteAroundAnAccentDot()
    {
        using var bitmap = new SKBitmap(100, 32);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Black);
        WindowsSkUiLook.Instance.DrawSlider(canvas, new SkUiSliderPaint(new SKRect(0, 0, 100, 32), 0.5f, StackOrientation.Horizontal,
            SKColors.Blue, SKColors.Gray, SKColors.Red, 0, true));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(50, 16));
        Assert.Equal(SKColors.White, bitmap.GetPixel(50, 8)); // 8 from the center: the white disc, outside the dot
        // The track runs to the slider's ends.
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(3, 16));
        Assert.Equal(SKColors.Gray, bitmap.GetPixel(96, 16));
    }

    [Fact]
    public void FluentProgressBarsFillOverAThinTrackLine()
    {
        using var bitmap = new SKBitmap(100, 3);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Black);
        WindowsSkUiLook.Instance.DrawProgressBar(canvas, new SkUiProgressBarPaint(new SKRect(0, 0, 100, 3), 0.5f, false, SKColors.Gray, SKColors.Blue, 0, true));
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(25, 0));
        Assert.Equal(SKColors.Gray, bitmap.GetPixel(75, 1));
        Assert.Equal(SKColors.Black, bitmap.GetPixel(75, 0)); // above the 1 DIP line
    }

    [Theory]
    [MemberData(nameof(Looks))]
    public void EveryLookDrawsIndicatorsFieldsAndFocusRings(DefaultSkUiLook look)
    {
        using var bitmap = new SKBitmap(80, 60);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint { Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, IsAntialias = true, Color = SKColors.Blue };
        look.DrawActivityIndicator(canvas, 40, 40, 0, paint);
        Assert.True(HasInk(bitmap), $"{look.GetType().Name} activity indicator");

        bitmap.Erase(SKColors.White);
        look.DrawPickerField(canvas, new SkUiPickerFieldPaint(new SKRect(2, 2, 78, 40), new SKRect(50, 2, 78, 40), SKColors.Blue, SKColors.Gray,
            SKColors.Black, 1, 0, SkUiPressVisual.None, false, true, false));
        Assert.True(HasInk(bitmap), $"{look.GetType().Name} picker field");

        bitmap.Erase(SKColors.White);
        look.DrawFocusRing(canvas, new SkUiFocusRingPaint(new SKRect(4, 4, 70, 40), new CornerRadius(6), SKColors.Blue, SKColors.White));
        Assert.True(HasInk(bitmap), $"{look.GetType().Name} focus ring");
    }

    [Fact]
    public void PlatformLooksChooseTheirChrome()
    {
        Assert.Equal(SkUiActivityIndicatorStyle.Arc, DefaultSkUiLook.Instance.ActivityIndicatorStyle);
        Assert.Equal(SkUiActivityIndicatorStyle.Spokes, IosSkUiLook.Instance.ActivityIndicatorStyle);
        Assert.Equal(SkUiActivityIndicatorStyle.Ring, WindowsSkUiLook.Instance.ActivityIndicatorStyle);
        Assert.Equal(SkUiFocusRingStyle.Halo, MacSkUiLook.Instance.FocusRingStyle);
        Assert.Equal(SkUiFocusRingStyle.Double, WindowsSkUiLook.Instance.FocusRingStyle);
        Assert.Equal(48, AndroidSkUiLook.Instance.PopupItemHeight);
        Assert.Equal(32, WindowsSkUiLook.Instance.PopupItemHeight);
        Assert.Equal(11, MacSkUiLook.Instance.ScrollBarExpandedThickness);
        Assert.Equal(2, WindowsSkUiLook.Instance.ScrollBarThickness);
    }

    [Fact]
    public void SpokesRecordOneImageThatMatchesTheirShapes()
    {
        const float size = 32;
        using var paint = new SKPaint { Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, IsAntialias = true, Color = SKColors.Black };
        using var recorder = new SKPictureRecorder();
        IosSkUiLook.Instance.DrawActivityIndicator(recorder.BeginRecording(new SKRect(0, 0, size, size)), size, size, 0, paint);
        using var picture = recorder.EndRecording();
        // The render thread turns the picture every frame: one draw, not one per spoke.
        Assert.Equal(1, picture.ApproximateOperationCount);
        Assert.Equal(SKPaintStyle.Stroke, paint.Style);
        Assert.Equal(SKColors.Black, paint.Color);

        using var drawn = new SKBitmap((int)size, (int)size);
        using (var canvas = new SKCanvas(drawn))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawPicture(picture);
        }
        using var expected = new SKBitmap((int)size, (int)size);
        using (var canvas = new SKCanvas(expected))
        {
            // The spokes as round-capped lines from 55 % of the radius to the circle, the first at the top and the brightest.
            canvas.Clear(SKColors.Transparent);
            using var line = new SKPaint { Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, IsAntialias = true, StrokeWidth = Math.Max(1.5f, size * 0.09f) };
            const int spokes = 8;
            for (var spoke = 0; spoke < spokes; spoke++)
            {
                line.Color = SKColors.Black.WithAlpha((byte)(255 * (1 - 0.8f * spoke / spokes)));
                var angle = -Math.PI / 2 + spoke * 2 * Math.PI / spokes;
                var (cos, sin) = ((float)Math.Cos(angle), (float)Math.Sin(angle));
                canvas.DrawLine(size / 2 + cos * size / 2 * 0.55f, size / 2 + sin * size / 2 * 0.55f, size / 2 + cos * size / 2, size / 2 + sin * size / 2, line);
            }
        }
        // Not turned, the image lands on whole pixels: it draws what the spokes drew as shapes.
        double total = 0;
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                total += Math.Abs(drawn.GetPixel(x, y).Alpha - expected.GetPixel(x, y).Alpha);
        var mean = total / (size * size);
        Assert.True(mean < 1, $"mean alpha difference {mean:F2}");
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
