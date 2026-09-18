using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

public class LookAndColorSchemeTests
{
    [Fact]
    public void LookAndSchemeCurrentNeverReturnNull()
    {
        Assert.NotNull(SkUiLook.Current);
        Assert.NotNull(SkUiColorScheme.Current);
        Assert.Throws<ArgumentNullException>(() => SkUiLook.Current = null!);
        Assert.Throws<ArgumentNullException>(() => SkUiColorScheme.Current = null!);
    }

    [Fact]
    public void ColorSchemeAccentMutationRaisesChanged()
    {
        var scheme = new LightSkUiColorScheme();
        var raised = 0;
        scheme.Changed += (_, _) => raised++;
        scheme.Accent = Color.FromArgb("#FF0000");
        Assert.Equal(1, raised);
        Assert.Equal(Color.FromArgb("#FF0000"), scheme.Accent);
        scheme.Accent = Color.FromArgb("#FF0000");
        Assert.Equal(1, raised);
    }

    [Fact]
    public void LookSwitchMeasureDelegateChangesIntrinsicSize()
    {
        var previous = SkUiLook.Current;
        try
        {
            var look = new DefaultSkUiLook
            {
                SwitchMeasure = (_, _) => new Size(60, 40)
            };
            SkUiLook.Current = look;
            var sw = new SkUiSwitch();
            Arrange(sw, 200, 100);
            Assert.Equal(60, sw.DesiredSize.Width);
            Assert.Equal(40, sw.DesiredSize.Height);
        }
        finally
        {
            SkUiLook.Current = previous;
        }
    }

    [Fact]
    public void LookRadioButtonPainterDelegateIsInvoked()
    {
        var previous = SkUiLook.Current;
        var painted = false;
        try
        {
            var look = new DefaultSkUiLook
            {
                RadioButtonPainter = (_, _, _, _, _) => painted = true
            };
            SkUiLook.Current = look;
            var radio = new SkUiRadioButton { IsChecked = true };
            Arrange(radio, 24, 24);
            using var bitmap = new SKBitmap(24, 24);
            using var canvas = new SKCanvas(bitmap);
            radio.Paint(canvas);
            Assert.True(painted);
        }
        finally
        {
            SkUiLook.Current = previous;
        }
    }

    [Fact]
    public void CoreAndMauiSwitchShareLookMeasure()
    {
        var previous = SkUiLook.Current;
        try
        {
            var look = new DefaultSkUiLook
            {
                SwitchMeasure = (_, _) => new Size(55, 33)
            };
            SkUiLook.Current = look;
            Assert.Equal(new Size(55, 33), SkUiLook.Current.MeasureSwitch(100, 100));
            var core = new MauiSkiaUi.Core.SkUiCoreSwitch();
            core.Measure(100, 100);
            Assert.Equal(55, core.DesiredSize.Width);
            Assert.Equal(33, core.DesiredSize.Height);
        }
        finally
        {
            SkUiLook.Current = previous;
        }
    }

    [Fact]
    public void SubclassLookCanOverrideDrawSwitchCore()
    {
        var previous = SkUiLook.Current;
        try
        {
            SkUiLook.Current = new TinySwitchLook();
            Assert.Equal(new Size(10, 10), SkUiLook.Current.MeasureSwitch(0, 0));
            using var bitmap = new SKBitmap(10, 10);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            SkUiLook.Current.DrawSwitch(canvas, new SKRect(0, 0, 10, 10), true, SKColors.Red, SKColors.White);
            Assert.Contains(bitmap.Pixels, p => p.Alpha > 0);
        }
        finally
        {
            SkUiLook.Current = previous;
        }
    }

    private static void Arrange(IView view, double width, double height)
    {
        view.Measure(width, height);
        view.Arrange(new Rect(0, 0, width, height));
    }

    [Fact]
    public void DefaultSizePropertiesDriveMeasureWhenDelegatesUnset()
    {
        var look = new SizedLook();
        Assert.Equal(new Size(70, 40), look.MeasureSwitch(0, 0));
        Assert.Equal(new Size(30, 30), look.MeasureCheckBox(0, 0));
        Assert.Equal(new Size(28, 28), look.MeasureRadioButton(0, 0));
        Assert.Equal(new Size(50, 50), look.MeasureActivityIndicator(0, 0));
    }

    private sealed class SizedLook : DefaultSkUiLook
    {
        public override Size DefaultSwitchSize => new(70, 40);
        public override Size DefaultCheckBoxSize => new(30, 30);
        public override Size DefaultRadioButtonSize => new(28, 28);
        public override Size DefaultActivityIndicatorSize => new(50, 50);
    }

    private sealed class TinySwitchLook : DefaultSkUiLook
    {
        public override Size DefaultSwitchSize => new(10, 10);

        protected override void DrawSwitchCore(SKCanvas canvas, SKRect bounds, bool isChecked, SKColor track, SKColor thumb)
        {
            using var paint = new SKPaint { Color = track, IsAntialias = true };
            canvas.DrawRect(bounds, paint);
        }
    }
}
