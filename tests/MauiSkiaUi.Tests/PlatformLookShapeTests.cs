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
        // MAUI's radio template: 41 around its text (7 on each side, the 21 circle, 6 to the text); Mac Catalyst inherits it.
        Assert.Equal(new Thickness(7), IosSkUiLook.Instance.RadioButtonPadding);
        Assert.Equal(new Thickness(7), MacSkUiLook.Instance.RadioButtonPadding);
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

    [Fact]
    public void Material2ProgressBarsHaveSquareEndsAndMaterial3RoundOnes()
    {
        // The 4 DIP bar centered in 16: its first column is fully drawn with square ends, half covered with round ones.
        SKColor FirstColumn(DefaultSkUiLook look)
        {
            using var bitmap = new SKBitmap(100, 16);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Black);
            look.DrawProgressBar(canvas, new SkUiProgressBarPaint(new SKRect(0, 0, 100, 16), 0.5f, false, SKColors.Gray, SKColors.Blue, 0, true));
            return bitmap.GetPixel(0, 6);
        }

        Assert.Equal(0, AndroidSkUiLook.Instance.GetProgressBarCornerRadius(4));
        Assert.Equal(SKColors.Blue, FirstColumn(AndroidSkUiLook.Instance));
        Assert.Equal(2, AndroidMaterial3SkUiLook.Instance.GetProgressBarCornerRadius(4));
        Assert.Equal(2, IosSkUiLook.Instance.GetProgressBarCornerRadius(4));
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

    [Fact]
    public void SpokesTooLargeForAnImageDrawAsShapes()
    {
        // Past the largest image kept, the spokes are drawn one by one instead of rasterized.
        const float size = DefaultSkUiLook.SpokesImage.MaxPixels + 10;
        using var paint = new SKPaint { Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, IsAntialias = true, Color = SKColors.Black };
        using var recorder = new SKPictureRecorder();
        IosSkUiLook.Instance.DrawActivityIndicator(recorder.BeginRecording(new SKRect(0, 0, size, size)), size, size, 0, paint);
        using var picture = recorder.EndRecording();
        Assert.True(picture.ApproximateOperationCount >= IosSkUiLook.Instance.ActivityIndicatorSpokeCount, $"{picture.ApproximateOperationCount} operations");
        Assert.Equal(SKColors.Black, paint.Color);

        using var drawn = new SKBitmap((int)size, (int)size);
        using (var canvas = new SKCanvas(drawn))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawPicture(picture);
        }
        // The first spoke, at the top and the brightest, halfway between 55 % of the radius and the circle.
        Assert.Equal(255, drawn.GetPixel((int)(size / 2), (int)(size / 2 - size / 2 * 0.775f)).Alpha);
    }

    [Fact]
    public void SpokesEvictedFromTheCacheStayInRecordedPictures()
    {
        using var paint = new SKPaint { Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, IsAntialias = true, Color = SKColors.Black };
        static SKPicture Record(SKPaint paint, float size)
        {
            using var recorder = new SKPictureRecorder();
            IosSkUiLook.Instance.DrawActivityIndicator(recorder.BeginRecording(new SKRect(0, 0, size, size)), size, size, 0, paint);
            return recorder.EndRecording();
        }

        // A size no other test records, then more sizes than the cache keeps: the first image is evicted (and disposed).
        using var first = Record(paint, 41.25f);
        for (var index = 0; index <= DefaultSkUiLook.SpokesImage.Capacity; index++)
            Record(paint, 50.25f + index).Dispose();
        Assert.True(DefaultSkUiLook.SpokesImage.Count <= DefaultSkUiLook.SpokesImage.Capacity);

        using var drawn = new SKBitmap(42, 42);
        using (var canvas = new SKCanvas(drawn))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawPicture(first);
        }
        Assert.True(drawn.GetPixel(21, 21 - (int)(41.25f / 2 * 0.775f)).Alpha > 200);
    }

    #region Segmented control and tab strip (1.17 S3)

    [Fact]
    public void LooksHaveTheirPlatformsSegmentedAndTabStripSizesAndStyles()
    {
        Assert.Equal((SkUiSegmentedStyle.Pill, 36d), (DefaultSkUiLook.Instance.SegmentedStyle, DefaultSkUiLook.Instance.SegmentedMetrics.Height));
        Assert.Equal((SkUiSegmentedStyle.Thumb, 32d), (IosSkUiLook.Instance.SegmentedStyle, IosSkUiLook.Instance.SegmentedMetrics.Height));
        Assert.Equal((SkUiSegmentedStyle.Thumb, 28d), (MacSkUiLook.Instance.SegmentedStyle, MacSkUiLook.Instance.SegmentedMetrics.Height));
        Assert.Equal((SkUiSegmentedStyle.Tinted, 36d), (AndroidSkUiLook.Instance.SegmentedStyle, AndroidSkUiLook.Instance.SegmentedMetrics.Height));
        Assert.Equal((SkUiSegmentedStyle.Outlined, 40d), (AndroidMaterial3SkUiLook.Instance.SegmentedStyle, AndroidMaterial3SkUiLook.Instance.SegmentedMetrics.Height));
        Assert.Equal((SkUiSegmentedStyle.Bar, 32d), (WindowsSkUiLook.Instance.SegmentedStyle, WindowsSkUiLook.Instance.SegmentedMetrics.Height));
        Assert.Equal(16, IosSkUiLook.Instance.SegmentedMetrics.CornerRadius); // a capsule
        Assert.Equal(18, AndroidMaterial3SkUiLook.Instance.SegmentedMetrics.CheckSize);
        Assert.True(AndroidSkUiLook.Instance.SegmentedSelectionFades);
        Assert.False(IosSkUiLook.Instance.SegmentedSelectionFades);

        Assert.Equal((SkUiTabStripStyle.HeaderUnderline, 44d, 56d), TabStrip(DefaultSkUiLook.Instance));
        Assert.Equal((SkUiTabStripStyle.HeaderUnderline, 44d, 56d), TabStrip(IosSkUiLook.Instance));
        Assert.Equal((SkUiTabStripStyle.HeaderUnderline, 32d, 48d), TabStrip(MacSkUiLook.Instance));
        Assert.Equal((SkUiTabStripStyle.Filled, 48d, 72d), TabStrip(AndroidSkUiLook.Instance));
        Assert.Equal((SkUiTabStripStyle.LabelUnderline, 48d, 64d), TabStrip(AndroidMaterial3SkUiLook.Instance));
        Assert.Equal((SkUiTabStripStyle.ShortBar, 40d, 52d), TabStrip(WindowsSkUiLook.Instance));
        Assert.True(AndroidMaterial3SkUiLook.Instance.TabStripMetrics.IconAbove);
        Assert.False(WindowsSkUiLook.Instance.TabStripMetrics.IconAbove);

        static (SkUiTabStripStyle, double, double) TabStrip(DefaultSkUiLook look) => (look.TabStripStyle, look.TabStripMetrics.Height, look.TabStripMetrics.IconHeight);
    }

    private static readonly SKRect[] ThreeSegments = [new(2, 2, 52, 34), new(52, 2, 102, 34), new(102, 2, 152, 34)];

    private static SkUiSegmentedPaint Segmented(DefaultSkUiLook look, int selected = 1, int previous = -1, float progress = 1, bool enabled = true)
    {
        var colors = look.GetSegmentedColors(LightSkUiColorScheme.Instance);
        static SKColor Sk(Color color) => new((byte)(color.Red * 255), (byte)(color.Green * 255), (byte)(color.Blue * 255), (byte)(color.Alpha * 255));
        var selection = selected < 0 ? SKRect.Empty : ThreeSegments[selected];
        if (previous >= 0 && selected >= 0 && !look.SegmentedSelectionFades)
        {
            var from = ThreeSegments[previous];
            selection = new SKRect(from.Left + (selection.Left - from.Left) * progress, from.Top, from.Right + (selection.Right - from.Right) * progress, from.Bottom);
        }
        return new SkUiSegmentedPaint
        {
            Bounds = new SKRect(0, 0, 154, 36), Segments = ThreeSegments, ContentWidths = [30, 30, 30], Selection = selection,
            SelectedIndex = selected, PreviousIndex = previous, Progress = progress, Track = Sk(colors.Track), SelectionColor = Sk(colors.Selection),
            Text = Sk(colors.Text), SelectedText = Sk(colors.SelectedText), Border = Sk(colors.Border), Accent = SKColors.Blue,
            CornerRadius = (float)look.SegmentedMetrics.CornerRadius, PressedIndex = -1, HoveredIndex = -1, IsEnabled = enabled
        };
    }

    [Theory]
    [MemberData(nameof(Looks))]
    public void EveryLookMarksTheSelectedSegmentAndDrawsItsTabIndicator(DefaultSkUiLook look)
    {
        using var bitmap = new SKBitmap(154, 36);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        look.DrawSegmented(canvas, Segmented(look));

        // The selected (middle) segment looks different from the unselected first one.
        var differs = false;
        for (var y = 3; y < 33 && !differs; y++)
            for (var x = 4; x < 50 && !differs; x++)
                differs = bitmap.GetPixel(x, y) != bitmap.GetPixel(x + 50, y);
        Assert.True(differs, $"{look.GetType().Name} marks the selected segment");

        bitmap.Erase(SKColors.White);
        look.DrawTabIndicator(canvas, new SkUiTabIndicatorPaint { Bounds = new SKRect(0, 0, 60, 3), Color = SKColors.Blue, Style = SkUiTabIndicatorStyle.Underline });
        Assert.True(HasInk(bitmap), $"{look.GetType().Name} tab indicator");
        bitmap.Erase(SKColors.White);
        look.DrawTabIndicator(canvas, new SkUiTabIndicatorPaint { Bounds = new SKRect(0, 0, 60, 3), Color = SKColors.Blue, Style = SkUiTabIndicatorStyle.None });
        Assert.False(HasInk(bitmap));
        Assert.True(look.GetTransition(SkUiTransitionKind.SegmentSelection).Duration > TimeSpan.Zero, $"{look.GetType().Name} animates the selection");
    }

    [Fact]
    public void SlidingSelectionsColorTheTextByOverlapAndFadingOnesByProgress()
    {
        var sliding = Segmented(DefaultSkUiLook.Instance, selected: 1, previous: 0, progress: 0.25f);
        Assert.Equal(0.75f, DefaultSkUiLook.Instance.GetSegmentSelection(sliding, 0), 3);
        Assert.Equal(0.25f, DefaultSkUiLook.Instance.GetSegmentSelection(sliding, 1), 3);
        Assert.Equal(0, DefaultSkUiLook.Instance.GetSegmentSelection(sliding, 2));

        var material = AndroidMaterial3SkUiLook.Instance;
        var fading = Segmented(material, selected: 2, previous: 0, progress: 0.4f);
        Assert.Equal(0.6f, material.GetSegmentSelection(fading, 0), 3);
        Assert.Equal(0, material.GetSegmentSelection(fading, 1));
        Assert.Equal(0.4f, material.GetSegmentSelection(fading, 2), 3);

        var settled = Segmented(material, selected: 2);
        Assert.Equal(settled.SelectedText, material.GetSegmentTextColor(settled, 2));
        Assert.Equal(settled.Text, material.GetSegmentTextColor(settled, 0));
        Assert.Equal(26, material.GetSegmentLeadingWidth(settled, 2)); // the check and the gap after it
        Assert.Equal(0, material.GetSegmentLeadingWidth(settled, 0));
        Assert.Equal(0, IosSkUiLook.Instance.GetSegmentLeadingWidth(Segmented(IosSkUiLook.Instance), 1));

        var disabled = Segmented(DefaultSkUiLook.Instance, enabled: false) with { };
        Assert.True(DefaultSkUiLook.Instance.GetSegmentTextColor(disabled, 0).Alpha < disabled.Text.Alpha);
    }

    [Fact]
    public void TabIndicatorsAreSizedByTheStripStyleAndFaceThePanes()
    {
        var header = new Rect(100, 0, 120, 48);

        Assert.Equal(new Rect(100, 46, 120, 2), DefaultSkUiLook.Instance.GetTabIndicatorBounds(header, 50, SkUiTabIndicatorStyle.Underline, stripAtBottom: false));
        Assert.Equal(new Rect(100, 0, 120, 2), DefaultSkUiLook.Instance.GetTabIndicatorBounds(header, 50, SkUiTabIndicatorStyle.Underline, stripAtBottom: true));
        Assert.Equal(new Rect(135, 45, 50, 3), AndroidMaterial3SkUiLook.Instance.GetTabIndicatorBounds(header, 50, SkUiTabIndicatorStyle.Underline, false));
        Assert.Equal(new Rect(152, 41, 16, 3), WindowsSkUiLook.Instance.GetTabIndicatorBounds(header, 50, SkUiTabIndicatorStyle.Underline, false));
        Assert.Equal(new Rect(104, 4, 112, 40), WindowsSkUiLook.Instance.GetTabIndicatorBounds(header, 50, SkUiTabIndicatorStyle.Pill, false));
        Assert.Equal(Rect.Zero, IosSkUiLook.Instance.GetTabIndicatorBounds(header, 50, SkUiTabIndicatorStyle.None, false));
        Assert.Equal(Colors.White, AndroidSkUiLook.Instance.GetTabStripColors(LightSkUiColorScheme.Instance).SelectedText);
    }

    #endregion

    private static bool HasInk(SKBitmap bitmap)
    {
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y) != SKColors.White)
                    return true;
        return false;
    }
}
