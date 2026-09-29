using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// State-change transitions (FR-26): toggles, press feedback, slider and progress follow the look's
/// <see cref="SkUiLook.GetTransition"/> on the UI animation clock, re-recording only the animating control.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public class TransitionTests
{
    private static long s_pointer = 70_000;

    /// <summary>Runs <paramref name="body"/> with <paramref name="look"/> as the current look and motion on.</summary>
    private static void WithLook(SkUiLook look, Action body)
    {
        var previous = SkUiLook.Current;
        var reduce = SkUiMotion.ReduceMotion;
        try
        {
            SkUiLook.Current = look;
            SkUiMotion.ReduceMotion = false;
            body();
        }
        finally
        {
            SkUiLook.Current = previous;
            SkUiMotion.ReduceMotion = reduce;
        }
    }

    /// <summary>Ticks the root's UI clock to <paramref name="milliseconds"/>, then renders a frame.</summary>
    private static void At(SkUiTestSurface surface, double milliseconds)
    {
        surface.Root.AnimationClock.Tick(TimeSpan.FromMilliseconds(milliseconds));
        surface.Frame(milliseconds);
    }

    private static void Tap(SkUiView root, Point at)
    {
        var id = ++s_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, at, TimeSpan.FromSeconds(1)));
        root.Touch(new(id, SkUiTouchAction.Released, at, TimeSpan.FromSeconds(1.02)));
    }

    [Fact]
    public void SwitchSlidesAndReversesFromWhereItIs()
    {
        SkUiToggleVisual seen = default;
        WithLook(new DefaultSkUiLook { SwitchPainter = (_, paint) => seen = paint.Visual }, () =>
        {
            var toggle = new SkUiSwitch();
            var root = new SkUiContentView { Content = toggle };
            using var surface = new SkUiTestSurface(root, 51, 31);
            surface.Frame();
            var recorded = surface.RecordedPictures;

            Tap(root, new Point(20, 15));
            Assert.True(toggle.IsChecked);
            surface.Frame();
            Assert.Equal(new SkUiToggleVisual(SkUiCheckState.Checked, SkUiCheckState.Unchecked, 0), seen);
            At(surface, 100); // 200 ms, CubicInOut: halfway
            Assert.Equal(0.5f, seen.Progress, 2);
            Assert.Equal(0.5f, seen.Weight(SkUiCheckState.Checked), 2);
            Assert.Equal(recorded + 2, surface.RecordedPictures); // only the switch re-records

            toggle.SetCheckState(SkUiCheckState.Unchecked); // back mid-way: continues from the same point
            surface.Frame(100);
            Assert.Equal(SkUiCheckState.Unchecked, seen.State);
            Assert.Equal(SkUiCheckState.Checked, seen.From);
            Assert.Equal(0.5f, seen.Weight(SkUiCheckState.Checked), 2);
            At(surface, 150); // half of the remaining 100 ms
            Assert.Equal(0.25f, seen.Weight(SkUiCheckState.Checked), 2);
            At(surface, 200);
            Assert.True(seen.IsSettled);
            Assert.Equal(1, seen.Weight(SkUiCheckState.Unchecked));
            At(surface, 400); // the tap's press (80 ms in, 220 ms out) has faded too: no more frames
            Assert.Equal(0, seen.Pressed);
            Assert.False(root.AnimationClock.IsRunning);
        });
    }

    [Fact]
    public void StatesSetBeforeTheFirstFrameOrWithReducedMotionShowAtOnce()
    {
        SkUiToggleVisual seen = default;
        WithLook(new DefaultSkUiLook { CheckBoxPainter = (_, paint) => seen = paint.Visual }, () =>
        {
            var box = new SkUiCheckBox { IsChecked = true }; // e.g. a page constructor or a binding
            var root = new SkUiContentView { Content = box };
            using var surface = new SkUiTestSurface(root, 24, 24);
            surface.Frame();
            Assert.True(seen.IsSettled);
            Assert.False(root.AnimationClock.IsRunning);

            SkUiMotion.ReduceMotion = true;
            box.IsChecked = false;
            surface.Frame();
            Assert.Equal(SkUiToggleVisual.Settled(SkUiCheckState.Unchecked), seen);
            Assert.False(root.AnimationClock.IsRunning);
        });
    }

    [Fact]
    public void StoppingTheClockJumpsToTheEnd()
    {
        SkUiToggleVisual seen = default;
        WithLook(new DefaultSkUiLook { RadioButtonPainter = (_, paint) => seen = paint.Visual }, () =>
        {
            var radio = new SkUiRadioButton();
            var root = new SkUiContentView { Content = radio };
            using var surface = new SkUiTestSurface(root, 24, 24);
            surface.Frame();
            radio.IsChecked = true;
            At(surface, 40);
            Assert.False(seen.IsSettled);
            root.AnimationClock.StopAll(); // what closing the page does
            surface.Frame(50);
            Assert.Equal(SkUiToggleVisual.Settled(SkUiCheckState.Checked), seen);
        });
    }

    [Fact]
    public void QuickTapsShowTheirWholePressAndRippleFromThePressPoint()
    {
        var presses = new List<SkUiPressVisual>();
        WithLook(new DefaultSkUiLook { ButtonPainter = (_, paint) => presses.Add(paint.Press) }, () =>
        {
            var button = new SkUiButton { Text = "OK" };
            var root = new SkUiContentView { Content = button };
            using var surface = new SkUiTestSurface(root, 120, 44);
            surface.Frame();
            presses.Clear();

            Tap(root, new Point(30, 20)); // pressed and released within one frame
            for (var time = 16; time <= 400; time += 16)
                At(surface, time);
            Assert.Equal(new SKPoint(30, 20), presses[^1].Origin);
            Assert.Equal(1, presses.Max(press => press.Pressed), 2); // the press showed fully before releasing
            Assert.Equal(0, presses[^1].Pressed);
            Assert.Equal(1, presses[^1].RippleFade);
            Assert.False(presses[^1].HasRipple);
            Assert.False(root.AnimationClock.IsRunning);
        });
    }

    [Fact]
    public void RippleSpreadsFromThePressPointClippedToTheButton()
    {
        var look = new DefaultSkUiLook { PressEffect = SkUiPressEffect.Ripple };
        WithLook(look, () =>
        {
            var button = new SkUiButton { Text = "", FillColor = Colors.Black, CornerRadius = 0 };
            var root = new SkUiContentView { Content = button, Background = Colors.White };
            using var surface = new SkUiTestSurface(root, 200, 44);
            surface.Frame();
            var id = ++s_pointer;
            root.Touch(new(id, SkUiTouchAction.Pressed, new Point(20, 22), TimeSpan.FromSeconds(1)));
            At(surface, 100);
            var bitmap = surface.Bitmap;
            Assert.NotEqual(SKColors.Black, bitmap.GetPixel(20, 22)); // the (light) ripple over the dark fill
            var far = bitmap.GetPixel(195, 5);
            var near = bitmap.GetPixel(20, 22);
            Assert.True(near.Red > far.Red, $"ripple near {near} vs far {far}");
            root.Touch(new(id, SkUiTouchAction.Released, new Point(20, 22), TimeSpan.FromSeconds(1.2)));
            At(surface, 600);
            Assert.Equal(SKColors.Black, surface.Bitmap.GetPixel(20, 22));
        });
    }

    [Fact]
    public void SliderThumbGlidesToATappedValueButFollowsDrags()
    {
        var fractions = new List<float>();
        WithLook(new DefaultSkUiLook { SliderPainter = (_, paint) => fractions.Add(paint.Fraction) }, () =>
        {
            var slider = new SkUiSlider();
            var root = new SkUiContentView { Content = slider };
            using var surface = new SkUiTestSurface(root, 220, 32);
            surface.Frame();
            Tap(root, new Point(160, 16)); // 0.75
            Assert.Equal(0.75, slider.Value, 2); // the value changes at once
            surface.Frame();
            Assert.Equal(0, fractions[^1], 2);
            At(surface, 75);
            Assert.InRange(fractions[^1], 0.3f, 0.74f);
            At(surface, 200);
            Assert.Equal(0.75f, fractions[^1], 2);

            slider.Value = 0.25; // code: at once
            surface.Frame(200);
            Assert.Equal(0.25f, fractions[^1], 2);
        });
    }

    [Fact]
    public void ProgressFillFollowsTheLooksTransitionExceptDuringProgressTo()
    {
        var progress = new List<float>();
        var look = new DefaultSkUiLook
        {
            ProgressBarPainter = (_, paint) => progress.Add(paint.Progress),
            TransitionProvider = kind => kind == SkUiTransitionKind.Progress ? SkUiTransition.FromMilliseconds(100) : SkUiTransition.None
        };
        WithLook(look, () =>
        {
            var bar = new SkUiProgressBar();
            var root = new SkUiContentView { Content = bar };
            using var surface = new SkUiTestSurface(root, 200, 4);
            surface.Frame();
            bar.Progress = 1;
            At(surface, 50);
            Assert.Equal(0.5f, progress[^1], 2);
            At(surface, 100);
            Assert.Equal(1f, progress[^1], 2);

            _ = bar.ProgressTo(0, 100); // its own tween, not smoothed again
            At(surface, 150);
            Assert.Equal(0.5f, progress[^1], 2);
        });
    }

    [Fact]
    public void CoreTogglesAndButtonsAnimateToo()
    {
        SkUiToggleVisual toggleSeen = default;
        var presses = new List<float>();
        var look = new DefaultSkUiLook
        {
            SwitchPainter = (_, paint) => toggleSeen = paint.Visual,
            ButtonPainter = (_, paint) => presses.Add(paint.Press.Pressed)
        };
        WithLook(look, () =>
        {
            var toggle = new SkUiCoreSwitch();
            var button = new SkUiCoreButton().SetText("OK");
            var stack = new SkUiCoreVerticalStackLayout();
            stack.Add(toggle);
            stack.Add(button);
            var host = new SkUiCoreHost().SetContent(stack);
            using var surface = new SkUiTestSurface(host, 120, 120);
            surface.Frame();
            toggle.SetCheckState(SkUiCheckState.Checked);
            At(surface, 100);
            Assert.Equal(0.5f, toggleSeen.Weight(SkUiCheckState.Checked), 2);
            At(surface, 250);
            Assert.True(toggleSeen.IsSettled);

            var at = new Point(20, toggle.Frame.Height + button.Frame.Height / 2);
            Tap(host, at);
            for (var time = 266; time <= 700; time += 16)
                At(surface, time);
            Assert.Equal(1, presses.Max(), 2);
            Assert.Equal(0, presses[^1]);
        });
    }

    [Fact]
    public void ContainersWithShowsPressEffectGetThePressFeedbackAsOneControl()
    {
        var overlays = new List<SkUiPressOverlayPaint>();
        WithLook(new DefaultSkUiLook { PressOverlayPainter = (_, paint) => overlays.Add(paint) }, () =>
        {
            // SkUi: a border card with a label inside; the press lands on the label and presses the card.
            var card = new SkUiBorder { CornerRadius = 12, ShowsPressEffect = true, Content = new SkUiLabel { Text = "Card" } };
            var taps = 0;
            card.Tapped += (_, _) => taps++;
            var root = new SkUiContentView { Content = card };
            using var surface = new SkUiTestSurface(root, 200, 60);
            surface.Frame();
            Assert.Equal(new CornerRadius(12), overlays[^1].CornerRadii);
            Assert.Equal(0, overlays[^1].Press.Pressed);
            var id = ++s_pointer;
            root.Touch(new(id, SkUiTouchAction.Pressed, new Point(40, 30), TimeSpan.FromSeconds(1)));
            At(surface, 100);
            Assert.Equal(1, overlays[^1].Press.Pressed, 2);
            Assert.Equal(new SKPoint(40, 30), overlays[^1].Press.Origin);
            root.Touch(new(id, SkUiTouchAction.Released, new Point(40, 30), TimeSpan.FromSeconds(1.1)));
            At(surface, 400);
            Assert.Equal(0, overlays[^1].Press.Pressed);
            Assert.Equal(1, taps);
        });

        overlays.Clear();
        WithLook(new DefaultSkUiLook { PressOverlayPainter = (_, paint) => overlays.Add(paint) }, () =>
        {
            // Core: a composite button (border + grid of labels and a button); the inner button takes its own presses.
            var inner = new SkUiCoreButton();
            inner.SetText("Go").SetWidth(60).SetHeight(40);
            var grid = new SkUiCoreGrid()
                .SetColumnDefinitions([new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star), new SkUiCoreColumnDefinition(SkUiCoreGridLength.Auto)])
                .SetRowDefinitions([new SkUiCoreRowDefinition(new SkUiCoreGridLength(40))]);
            grid.Add(new SkUiCoreLabel().SetText("Title"), 0, 0);
            grid.Add(inner, 0, 1);
            var card = new SkUiCoreBorder().SetCornerRadius(8).SetPadding(new Thickness(0)).SetContent(grid);
            card.SetShowsPressEffect(true);
            var taps = 0;
            card.Tapped += (_, _) => taps++;
            var host = new SkUiCoreHost().SetContent(card);
            using var surface = new SkUiTestSurface(host, 200, 40);
            surface.Frame();

            var id = ++s_pointer;
            host.Touch(new(id, SkUiTouchAction.Pressed, new Point(180, 20), TimeSpan.FromSeconds(1))); // on the inner button
            At(surface, 100);
            Assert.Equal(0, overlays[^1].Press.Pressed);
            host.Touch(new(id, SkUiTouchAction.Released, new Point(180, 20), TimeSpan.FromSeconds(1.1)));
            At(surface, 400);
            Assert.Equal(0, taps);

            id = ++s_pointer;
            host.Touch(new(id, SkUiTouchAction.Pressed, new Point(30, 20), TimeSpan.FromSeconds(2))); // on the title label
            At(surface, 500);
            Assert.Equal(new CornerRadius(8), overlays[^1].CornerRadii);
            Assert.Equal(1, overlays[^1].Press.Pressed, 2);
            host.Touch(new(id, SkUiTouchAction.Released, new Point(30, 20), TimeSpan.FromSeconds(2.1)));
            At(surface, 900);
            Assert.Equal(1, taps);
            Assert.Equal(0, overlays[^1].Press.Pressed);
        });
    }

    [Fact]
    public void DefaultPressOverlayDimsTheWholeContainer()
    {
        WithLook(new DefaultSkUiLook(), () =>
        {
            var card = new SkUiBorder { CornerRadius = 0, StrokeThickness = 0, BackgroundColor = Colors.White, ShowsPressEffect = true };
            card.Tapped += (_, _) => { };
            var root = new SkUiContentView { Content = card };
            using var surface = new SkUiTestSurface(root, 100, 40);
            Assert.Equal(SKColors.White, surface.Frame().GetPixel(50, 20));
            root.Touch(new(++s_pointer, SkUiTouchAction.Pressed, new Point(50, 20), TimeSpan.FromSeconds(1)));
            At(surface, 100);
            var pressed = surface.Bitmap.GetPixel(50, 20);
            Assert.True(pressed.Red < 240, $"pressed card pixel {pressed}");
        });
    }

    [Fact]
    public void DetachingMidTransitionStopsItsFrames()
    {
        WithLook(new DefaultSkUiLook(), () =>
        {
            var toggle = new SkUiSwitch();
            var stack = new SkUiVerticalStackLayout { Children = { toggle } };
            var root = new SkUiContentView { Content = stack };
            using var surface = new SkUiTestSurface(root, 60, 40);
            surface.Frame();
            toggle.IsChecked = true;
            At(surface, 50);
            Assert.True(root.AnimationClock.IsRunning);
            stack.Children.Remove(toggle); // recycled / removed mid-way
            At(surface, 66);
            Assert.False(root.AnimationClock.IsRunning); // no more frames for a detached control
        });
    }

    [Fact]
    public void ProgressToAndDragsTakeOverFromSmoothingAndGlides()
    {
        var progress = new List<float>();
        var fractions = new List<float>();
        var look = new DefaultSkUiLook
        {
            ProgressBarPainter = (_, paint) => progress.Add(paint.Progress),
            SliderPainter = (_, paint) => fractions.Add(paint.Fraction),
            TransitionProvider = kind => kind is SkUiTransitionKind.Progress or SkUiTransitionKind.SliderThumb
                ? SkUiTransition.FromMilliseconds(100) : SkUiTransition.None
        };
        WithLook(look, () =>
        {
            var bar = new SkUiProgressBar();
            var slider = new SkUiSlider();
            var stack = new SkUiVerticalStackLayout { Children = { bar, slider } };
            var root = new SkUiContentView { Content = stack };
            using var surface = new SkUiTestSurface(root, 220, 60);
            surface.Frame();

            bar.Progress = 1;
            At(surface, 50);
            Assert.Equal(0.5f, progress[^1], 2);
            _ = bar.ProgressTo(1, 100); // already at 1: ProgressTo owns Progress, the smoothing stops
            surface.Frame(50);
            Assert.Equal(1f, progress[^1], 2);

            var y = bar.Height + slider.Height / 2;
            Tap(root, new Point(160, y)); // glide towards 0.75
            surface.Frame(50);
            Assert.True(fractions[^1] < 0.7f);
            var id = ++s_pointer;
            root.Touch(new(id, SkUiTouchAction.Pressed, new Point(160, y), TimeSpan.FromSeconds(2)));
            root.Touch(new(id, SkUiTouchAction.Moved, new Point(172, y), TimeSpan.FromSeconds(2.05))); // a drag from the same value
            surface.Frame(50);
            Assert.Equal((float)slider.Value, fractions[^1], 2); // the thumb is under the finger at once
            root.Touch(new(id, SkUiTouchAction.Released, new Point(172, y), TimeSpan.FromSeconds(2.1)));
        });
    }

    [Fact]
    public void DisabledContainersWithShowsPressEffectAreNotVeiled()
    {
        WithLook(new DefaultSkUiLook(), () =>
        {
            var card = new SkUiBorder { CornerRadius = 0, StrokeThickness = 0, BackgroundColor = Colors.White, ShowsPressEffect = true, IsEnabled = false };
            var root = new SkUiContentView { Content = card };
            using var surface = new SkUiTestSurface(root, 100, 40);
            Assert.Equal(SKColors.White, surface.Frame().GetPixel(50, 20));
        });
    }

    [Fact]
    public void LookChangesReMeasureAndRedrawLiveTrees()
    {
        var toggle = new SkUiSwitch { HorizontalOptions = LayoutOptions.Start };
        var core = new SkUiCoreSwitch();
        var host = new SkUiCoreHost { HorizontalOptions = LayoutOptions.Start }.SetContent(core);
        var stack = new SkUiVerticalStackLayout { Children = { toggle, host } };
        var root = new SkUiContentView { Content = stack };
        using var surface = new SkUiTestSurface(root, 200, 200);
        surface.Frame();
        Assert.Equal(51, toggle.Width);
        var recorded = surface.RecordedPictures;
        var previous = SkUiLook.Current;
        try
        {
            SkUiLook.Current = new BigSwitchLook();
            MauiSkiaUi.Rendering.SkUiRenderInvalidation.MarkLookChanged(root); // what a live surface does on CurrentChanged
            SkUiTestHelpers.Arrange(root, 200, 200);
            surface.Frame();
            Assert.Equal(80, toggle.Width);
            Assert.Equal(80, core.Frame.Width);
            Assert.True(surface.RecordedPictures - recorded >= 5, "every drawn node re-records");
        }
        finally
        {
            SkUiLook.Current = previous;
        }
    }

    private sealed class BigSwitchLook : DefaultSkUiLook
    {
        public override Size DefaultSwitchSize => new(80, 40);
    }

    [Fact]
    public void ToggleVisualBlendsBetweenStates()
    {
        var visual = new SkUiToggleVisual(SkUiCheckState.Checked, SkUiCheckState.Unchecked, 0.25f);
        Assert.Equal(0.75f, visual.Weight(SkUiCheckState.Unchecked));
        Assert.Equal(0.25f, visual.Weight(SkUiCheckState.Checked));
        Assert.Equal(0, visual.Weight(SkUiCheckState.Indeterminate));
        Assert.Equal(25f, visual.Blend(0f, 100f, 50f));
        Assert.Equal(new SKColor(64, 0, 191, 255), visual.Blend(SKColors.Blue, SKColors.Red, SKColors.Green));
        Assert.Equal(1, SkUiToggleVisual.Settled(SkUiCheckState.Indeterminate).Weight(SkUiCheckState.Indeterminate));
    }
}
