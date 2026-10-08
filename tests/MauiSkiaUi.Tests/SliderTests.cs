using System.ComponentModel;
using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary><see cref="SkUiSlider"/> / <see cref="SkUiCoreSlider"/>: range, input along either axis, scroll competition, drawing.</summary>
public class SliderTests
{
    private static long _pointer = 9_000;

    /// <summary>A drag from <paramref name="from"/> to <paramref name="to"/> (root coordinates) in 8 steps over 160 ms.</summary>
    private static void Drag(SkUiView root, Point from, Point to)
    {
        var id = ++_pointer;
        var start = TimeSpan.FromSeconds(10);
        root.Touch(new(id, SkUiTouchAction.Pressed, from, start));
        for (var step = 1; step <= 8; step++)
        {
            var t = step / 8.0;
            root.Touch(new(id, SkUiTouchAction.Moved, new Point(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t), start + TimeSpan.FromMilliseconds(160 * t)));
        }
        root.Touch(new(id, SkUiTouchAction.Released, to, start + TimeSpan.FromMilliseconds(170)));
    }

    private static void Tap(SkUiView root, Point at)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, at, TimeSpan.FromSeconds(20)));
        root.Touch(new(id, SkUiTouchAction.Released, at, TimeSpan.FromSeconds(20.05)));
    }

    /// <summary>A 220×32 horizontal (or 32×220 vertical) slider as the whole surface: the thumb travels 10…210.</summary>
    private static (SkUiContentView Root, SkUiSlider Slider) Surface(StackOrientation orientation = StackOrientation.Horizontal, FlowDirection flow = FlowDirection.LeftToRight)
    {
        var slider = new SkUiSlider { Orientation = orientation };
        var root = new SkUiContentView { Content = slider, FlowDirection = flow };
        if (orientation == StackOrientation.Vertical)
            SkUiTestHelpers.Arrange(root, 32, 220);
        else
            SkUiTestHelpers.Arrange(root, 220, 32);
        return (root, slider);
    }

    [Fact]
    public void ValueIsClampedAndFollowsRangeChanges()
    {
        var slider = new SkUiSlider { Maximum = 10, Value = 20 };
        Assert.Equal(10, slider.Value);
        var changes = new List<(double Old, double New)>();
        slider.ValueChanged += (_, args) => changes.Add((args.OldValue, args.NewValue));
        slider.Maximum = 4;
        Assert.Equal(4, slider.Value);
        Assert.Equal(4d, (double)slider.GetValue(SkUiSlider.ValueProperty));
        slider.Minimum = 5;
        Assert.Equal(5, slider.Value);
        Assert.Equal([(10d, 4d), (4d, 5d)], changes);
    }

    [Fact]
    public void RequestedValueComesBackWhenTheRangeWidens()
    {
        // XAML order Value="7" Minimum="5" Maximum="10": the value ends at 7, as with MAUI's Slider.
        var slider = new SkUiSlider { Value = 7, Minimum = 5, Maximum = 10 };
        Assert.Equal(7, slider.Value);
        slider.Maximum = 6;
        Assert.Equal(6, slider.Value);
        slider.Maximum = 10;
        Assert.Equal(7, slider.Value);
        slider.Maximum = 5; // empty range: the value is the minimum
        Assert.Equal(5, slider.Value);

        var core = new SkUiCoreSlider().SetValue(7).SetMinimum(5).SetMaximum(10);
        Assert.Equal(7, core.Value);
    }

    [Fact]
    public void ValueChangedSeesTheBindingAlreadyUpdated()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var model = new SliderModel();
        var (root, slider) = Surface();
        slider.BindingContext = model;
        slider.SetBinding(SkUiSlider.ValueProperty, nameof(SliderModel.Level));
        var stale = 0;
        slider.ValueChanged += (_, args) =>
        {
            if (model.Level != args.NewValue || (double)slider.GetValue(SkUiSlider.ValueProperty) != args.NewValue)
                stale++;
        };
        Drag(root, new Point(10, 16), new Point(160, 16));
        slider.Maximum = 0.5;
        Assert.Equal(0.5, model.Level);
        Assert.Equal(0, stale);
    }

    [Fact]
    public void DraggingAlongTheSliderMovesTheValueAndWritesItBack()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var model = new SliderModel();
        var (root, slider) = Surface();
        slider.BindingContext = model;
        slider.SetBinding(SkUiSlider.ValueProperty, nameof(SliderModel.Level));
        var events = new List<string>();
        slider.DragStarted += (_, _) => events.Add("started");
        slider.DragCompleted += (_, _) => events.Add("completed");

        Drag(root, new Point(10, 16), new Point(160, 16)); // thumb center 10 → 160 of 10…210
        Assert.Equal(0.75, slider.Value, 2);
        Assert.Equal(0.75, model.Level, 2);
        Assert.Equal(["started", "completed"], events);
        Assert.False(slider.IsDragging);
    }

    [Fact]
    public void TapMovesTheValueToTheTappedPosition()
    {
        var (root, slider) = Surface();
        Tap(root, new Point(60, 16));
        Assert.Equal(0.25, slider.Value, 2);
    }

    [Fact]
    public void VerticalSlidersRunFromTheBottomUp()
    {
        var (root, slider) = Surface(StackOrientation.Vertical);
        Assert.Equal(new Size(32, 220), new Size(slider.Width, slider.Height));
        Drag(root, new Point(16, 210), new Point(16, 60)); // bottom (min) → 3/4 up
        Assert.Equal(0.75, slider.Value, 2);
        Tap(root, new Point(16, 215));
        Assert.Equal(0, slider.Value, 2);

        using var surface = new SkUiTestSurface(root, 32, 220);
        var bitmap = surface.Frame();
        // Value 0: the thumb (accent) sits at the bottom, the top of the track is the plain max track.
        Assert.Equal(SkUiColors.Accent.ToSKColorOpaque(), bitmap.GetPixel(16, 210));
        Assert.NotEqual(SkUiColors.Accent.ToSKColorOpaque(), bitmap.GetPixel(16, 20));
    }

    [Fact]
    public void RightToLeftSlidersStartAtTheRight()
    {
        var (root, slider) = Surface(flow: FlowDirection.RightToLeft);
        Tap(root, new Point(160, 16));
        Assert.Equal(0.25, slider.Value, 2);
    }

    [Fact]
    public void DragsAcrossASliderScrollItsScrollViewAndDragsAlongItSlide()
    {
        var slider = new SkUiSlider { HeightRequest = 40 };
        var stack = new SkUiVerticalStackLayout { Children = { slider, new SkUiBox { HeightRequest = 2000 } } };
        var scroll = new SkUiScrollView { Content = stack };
        var root = new SkUiContentView { Content = scroll };
        SkUiTestHelpers.Arrange(root, 220, 300);

        Drag(root, new Point(100, 20), new Point(100, 200)); // down: can't scroll up from 0, try up instead
        Drag(root, new Point(100, 250), new Point(100, 20));
        Assert.True(scroll.ScrollY > 0, "a vertical drag over the slider scrolls the page");
        Assert.Equal(0, slider.Value);

        scroll.ScrollTo(0, 0);
        SkUiTestHelpers.Arrange(root, 220, 300);
        Drag(root, new Point(10, 20), new Point(210, 20));
        Assert.Equal(1, slider.Value, 2);
        Assert.Equal(0, scroll.ScrollY);
    }

    [Fact]
    public void CoreSliderMatches()
    {
        var slider = new SkUiCoreSlider();
        slider.SetMaximum(100);
        var host = new SkUiCoreHost().SetContent(slider);
        SkUiTestHelpers.Arrange(host, 220, 32);
        var events = new List<string>();
        slider.DragStarted += (_, _) => events.Add("started");
        slider.DragCompleted += (_, _) => events.Add("completed");
        var names = new List<string?>();
        ((INotifyPropertyChanged)slider).PropertyChanged += (_, args) => names.Add(args.PropertyName);

        Drag(host, new Point(10, 16), new Point(110, 16));
        Assert.Equal(50, slider.Value, 1);
        Assert.Equal(["started", "completed"], events);
        Assert.Contains(nameof(SkUiCoreSlider.Value), names);

        slider.SetMaximum(20);
        Assert.Equal(20, slider.Value);
    }

    private sealed class SliderModel : INotifyPropertyChanged
    {
        private static readonly PropertyChangedEventArgs LevelChanged = new(nameof(Level));

        private double _level;

        public event PropertyChangedEventHandler? PropertyChanged;

        public double Level
        {
            get => _level;
            set
            {
                if (_level == value) return;
                _level = value;
                PropertyChanged?.Invoke(this, LevelChanged);
            }
        }
    }
}

internal static class SliderTestColors
{
    public static SKColor ToSKColorOpaque(this Color color) =>
        new((byte)(color.Red * 255), (byte)(color.Green * 255), (byte)(color.Blue * 255), 255);
}
