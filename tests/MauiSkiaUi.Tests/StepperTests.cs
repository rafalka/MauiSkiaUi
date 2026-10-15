using SkiaSharp;
using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// The drawn stepper (D7): MAUI's <c>Stepper</c> coercion, the look's − and + buttons (taps, held repeats, the range's ends), keys,
/// screen readers, the vertical orientation, texts in place of − and + and the Core twin.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public class StepperTests
{
    private static (SkUiVerticalStackLayout Root, SkUiStepper Stepper) Field(SkUiStepper? stepper = null)
    {
        stepper ??= new SkUiStepper();
        stepper.HorizontalOptions = LayoutOptions.Start;
        var root = new SkUiVerticalStackLayout { Children = { stepper } };
        SkUiTestHelpers.Arrange(root, 400, 200);
        return (root, stepper);
    }

    private static void Tap(SkUiView root, Point point, double atMs = 0)
    {
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Pressed, point, TimeSpan.FromMilliseconds(atMs)));
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Released, point, TimeSpan.FromMilliseconds(atMs + 30)));
    }

    [Fact]
    public void ValueIsCoercedAsMauisStepper()
    {
        var stepper = new SkUiStepper { Minimum = 0, Maximum = 10 };
        stepper.Value = 15;
        Assert.Equal(10, stepper.Value);
        // The asked value comes back when the range allows it again.
        stepper.Maximum = 20;
        Assert.Equal(15, stepper.Value);
        stepper.Increment = 0.1;
        stepper.Value = 1.234567;
        Assert.Equal(1.23457, stepper.Value, 10);
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiStepper(5, 5, 5, 1));
        var native = new Stepper(1, 9, 20, 2);
        var drawn = new SkUiStepper(1, 9, 20, 2);
        Assert.Equal((native.Minimum, native.Maximum, native.Value, native.Increment), (drawn.Minimum, drawn.Maximum, drawn.Value, drawn.Increment));
    }

    [Fact]
    public void ButtonsStepAndAreDisabledAtTheirEnds()
    {
        var (root, stepper) = Field(new SkUiStepper { Minimum = 0, Maximum = 2, Value = 1 });
        var changes = new List<double>();
        stepper.ValueChanged += (_, args) => changes.Add(args.NewValue);
        var button = SkUiLook.Current.StepperButtonSize;
        var minus = new Point(button.Width / 2, button.Height / 2);
        var plus = new Point(stepper.Width - button.Width / 2, button.Height / 2);
        Tap(root, plus);
        Assert.Equal(2, stepper.Value);
        Tap(root, plus, 1000);
        Assert.Equal(2, stepper.Value);
        Tap(root, minus, 2000);
        Tap(root, minus, 3000);
        Tap(root, minus, 4000);
        Assert.Equal(0, stepper.Value);
        Assert.Equal([2d, 1d, 0d], changes);
    }

    [Fact]
    public void RightToLeftPutsPlusOnTheLeft()
    {
        var (root, stepper) = Field(new SkUiStepper { FlowDirection = FlowDirection.RightToLeft });
        var button = SkUiLook.Current.StepperButtonSize;
        Tap(root, new Point(button.Width / 2, button.Height / 2));
        Assert.Equal(1, stepper.Value);
    }

    [Fact]
    public void AHeldButtonRepeats()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var (root, stepper) = Field();
        var button = SkUiLook.Current.StepperButtonSize;
        var plus = new Point(stepper.Width - button.Width / 2, button.Height / 2);
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Pressed, plus, TimeSpan.Zero, Device: SkUiPointerDevice.Mouse));
        Assert.Equal(1, stepper.Value);
        TestDispatcherProvider.RunDelayed();
        TestDispatcherProvider.RunDelayed();
        TestDispatcherProvider.RunDelayed();
        Assert.Equal(4, stepper.Value);
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Released, plus, TimeSpan.FromSeconds(1), Device: SkUiPointerDevice.Mouse));
        TestDispatcherProvider.RunDelayed();
        Assert.Equal(4, stepper.Value);
    }

    [Fact]
    public void KeysAndScreenReadersStep()
    {
        var (root, stepper) = Field();
        Assert.True(stepper.Focus());
        root.FocusManager.KeyDown(SkUiKey.Up);
        root.FocusManager.KeyDown(SkUiKey.Right);
        Assert.Equal(2, stepper.Value);
        root.FocusManager.KeyDown(SkUiKey.Down);
        Assert.Equal(1, stepper.Value);
        root.FocusManager.KeyDown(SkUiKey.End);
        Assert.Equal(100, stepper.Value);
        var info = new SkUiSemanticsInfo();
        ((ISkUiAccessibleNode)stepper).GetSemantics(info);
        Assert.Equal(SkUiSemanticsRole.Slider, info.Role);
        Assert.Equal(100, info.Range!.Value.Value);
        Assert.True(((ISkUiAccessibleNode)stepper).PerformSemanticsAction(SkUiSemanticsActions.Decrement));
        Assert.Equal(99, stepper.Value);
    }

    [Fact]
    public void FocusDoesNotAttachTheTextInput()
    {
        var proxy = new RecordingProxy();
        var (root, stepper) = Field();
        root.FocusManager.TextInput = proxy;
        Assert.True(stepper.Focus());
        Assert.Empty(proxy.Calls);
    }

    [Fact]
    public void VerticalStacksPlusOnTop()
    {
        var button = SkUiLook.Current.StepperButtonSize;
        var stepper = new SkUiStepper { Value = 5, Orientation = StackOrientation.Vertical, FlowDirection = FlowDirection.RightToLeft };
        var size = ((IView)stepper).Measure(double.PositiveInfinity, double.PositiveInfinity);
        Assert.Equal(new Size(button.Width, button.Height * 2), size);
        var (root, _) = Field(stepper);
        Tap(root, new Point(button.Width / 2, button.Height / 2));
        Assert.Equal(6, stepper.Value);
        Tap(root, new Point(button.Width / 2, button.Height * 1.5), 1000);
        Tap(root, new Point(button.Width / 2, button.Height * 1.5), 2000);
        Assert.Equal(4, stepper.Value);
        var info = new SkUiSemanticsInfo();
        ((ISkUiAccessibleNode)stepper).GetSemantics(info);
        Assert.False(info.IsHorizontal);
        // Back to side by side; an invalid orientation is ignored by the property and refused by the setter.
        stepper.Orientation = StackOrientation.Horizontal;
        Assert.Equal(new Size(button.Width * 2, button.Height), ((IView)stepper).Measure(double.PositiveInfinity, double.PositiveInfinity));
        stepper.Orientation = (StackOrientation)7;
        Assert.Equal(StackOrientation.Horizontal, stepper.Orientation);
        Assert.Throws<ArgumentOutOfRangeException>(() => stepper.SetOrientation((StackOrientation)7));
    }

    [Fact]
    public void AWiderStepperSplitsItsWidthBetweenTheButtons()
    {
        var stepper = new SkUiStepper { Value = 5, WidthRequest = 300 };
        var (root, _) = Field(stepper);
        Assert.Equal(300, stepper.Width);
        // Just right of the middle is +, just left of it is −.
        Tap(root, new Point(160, 10));
        Assert.Equal(6, stepper.Value);
        Tap(root, new Point(140, 10), 1000);
        Assert.Equal(5, stepper.Value);
    }

    [Fact]
    public void TextsReplaceTheGlyphsAndWidenTheButtons()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var button = SkUiLook.Current.StepperButtonSize;
        var stepper = new SkUiStepper
        {
            Value = 5, DecrementText = "Less", IncrementText = "More", FontFamily = SkUiTestHelpers.BundledFontFamily, FontSize = 24,
            TextColor = Colors.Red
        };
        var size = ((IView)stepper).Measure(double.PositiveInfinity, double.PositiveInfinity);
        Assert.True(size.Width > button.Width * 2 + 20, $"{size}");
        Assert.True(size.Height >= button.Height);
        // A bigger font widens it further; without texts it is the look's size again.
        stepper.FontSize = 32;
        Assert.True(((IView)stepper).Measure(double.PositiveInfinity, double.PositiveInfinity).Width > size.Width);
        stepper.FontSize = 24;

        var look = DefaultSkUiLook.Instance;
        try
        {
            SkUiStepperPaint? drawn = null;
            // No chrome: only the control's texts reach the bitmap.
            look.StepperPainter = (_, paint) => drawn = paint;
            using var surface = new SkUiTestSurface(stepper, 300, 60);
            var bitmap = surface.Frame(0);
            Assert.True(drawn is { HasDecrementText: true, HasIncrementText: true });
            Assert.True(HasRed(bitmap, 0, 150) && HasRed(bitmap, 150, 300));
            stepper.DecrementText = null;
            bitmap = surface.Frame(16);
            Assert.True(drawn is { HasDecrementText: false, HasIncrementText: true });
            Assert.False(HasRed(bitmap, 0, 150));
            Assert.True(HasRed(bitmap, 150, 300));
        }
        finally
        {
            look.StepperPainter = null;
        }
        stepper.IncrementText = string.Empty;
        Assert.Equal(new Size(button.Width * 2, button.Height), ((IView)stepper).Measure(double.PositiveInfinity, double.PositiveInfinity));

        static bool HasRed(SKBitmap bitmap, int from, int to)
        {
            for (var x = from; x < to; x++)
            for (var y = 0; y < bitmap.Height; y++)
                if (bitmap.GetPixel(x, y) is { Red: > 200, Green: < 80, Alpha: > 200 })
                    return true;
            return false;
        }
    }

    [Fact]
    public void CornerRadiusAndOutlineAreDrawn()
    {
        var stepper = new SkUiStepper();
        Assert.Equal((8, 0d, (Color?)null), (stepper.CornerRadius, stepper.BorderWidth, stepper.BorderColor));
        stepper.CornerRadius = -1;
        stepper.BorderWidth = -2;
        stepper.BorderWidth = double.NaN;
        Assert.Equal((8, 0d), (stepper.CornerRadius, stepper.BorderWidth));
        Assert.Throws<ArgumentOutOfRangeException>(() => stepper.SetCornerRadius(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => stepper.SetBorderWidth(double.PositiveInfinity));
        Assert.Same(stepper, stepper.SetCornerRadius(0).SetBorderWidth(4).SetBorderColor(Colors.Blue));

        var look = DefaultSkUiLook.Instance;
        using var surface = new SkUiTestSurface(stepper, 300, 60);
        try
        {
            SkUiStepperPaint? drawn = null;
            look.StepperPainter = (_, paint) => drawn = paint;
            surface.Frame(0);
            Assert.True(drawn is { CornerRadius: 0, BorderWidth: 4 }, $"{drawn}");
            Assert.Equal(new SKColor(0, 0, 255), drawn!.Value.BorderColor);
            // No color: the color scheme's muted color, as the segments' tint.
            stepper.BorderColor = null;
            surface.Frame(16);
            Assert.Equal(drawn!.Value.Border, drawn.Value.BorderColor);
            stepper.BorderColor = Colors.Blue;
        }
        finally
        {
            look.StepperPainter = null;
        }
        // Square corners: the outline fills them; the divider crosses the whole height in the outline's color.
        var bitmap = surface.Frame(32);
        Assert.True(IsBlue(bitmap, 1, 1) && IsBlue(bitmap, 1, 30) && IsBlue(bitmap, 150, 2) && IsBlue(bitmap, 150, 30));
        Assert.False(IsBlue(bitmap, 75, 30));
        // A pill: the corners are cut.
        stepper.CornerRadius = 100;
        bitmap = surface.Frame(48);
        Assert.False(IsBlue(bitmap, 1, 1));
        Assert.True(IsBlue(bitmap, 1, 30) && IsBlue(bitmap, 150, 30));

        // Texts keep clear of the outline: a wider outline widens a stepper with texts.
        using var _ = SkUiTestHelpers.UseBundledFont();
        stepper.SetDecrementText("Less").SetIncrementText("More").SetFontFamily(SkUiTestHelpers.BundledFontFamily);
        var thin = ((IView)stepper).Measure(double.PositiveInfinity, double.PositiveInfinity);
        stepper.BorderWidth = 12;
        Assert.True(((IView)stepper).Measure(double.PositiveInfinity, double.PositiveInfinity).Width > thin.Width);

        static bool IsBlue(SKBitmap bitmap, int x, int y) => bitmap.GetPixel(x, y) is { Blue: > 200, Red: < 60, Green: < 60, Alpha: > 200 };
    }

    [Fact]
    public void CoreStepperHasTheCornerRadiusAndOutlineToo()
    {
        var core = new SkUiCoreStepper();
        Assert.Equal((8d, 0d, (Color?)null), (core.CornerRadius, core.BorderWidth, core.BorderColor));
        var changed = new List<string?>();
        core.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        SkUiCoreStepper chained = core.SetCornerRadius(4.5).SetBorderWidth(2).SetBorderColor(Colors.Red);
        Assert.Same(core, chained);
        Assert.Equal([nameof(core.CornerRadius), nameof(core.BorderWidth), nameof(core.BorderColor)], changed);
        Assert.Throws<ArgumentOutOfRangeException>(() => core.SetCornerRadius(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => core.SetBorderWidth(double.NaN));
        var look = DefaultSkUiLook.Instance;
        try
        {
            SkUiStepperPaint? drawn = null;
            look.StepperPainter = (_, paint) => drawn = paint;
            using var surface = new SkUiTestSurface(new SkUiCoreHost().SetContent(core), 300, 60);
            surface.Frame(0);
            Assert.True(drawn is { CornerRadius: 4.5f, BorderWidth: 2 }, $"{drawn}");
            Assert.Equal(new SKColor(255, 0, 0), drawn!.Value.BorderColor);
        }
        finally
        {
            look.StepperPainter = null;
        }
    }

    [Fact]
    public void CoreStepperStepsLikeTheSkUiStepper()
    {
        var core = new SkUiCoreStepper().SetMaximum(10).SetValue(15);
        Assert.Equal(10, core.Value);
        core.SetMaximum(20);
        Assert.Equal(15, core.Value);
        var root = new SkUiVerticalStackLayout { Children = { new SkUiCoreHost().SetContent(core) } };
        SkUiTestHelpers.Arrange(root, 400, 200);
        Assert.True(core.Focus());
        root.FocusManager.KeyDown(SkUiKey.Up);
        Assert.Equal(16, core.Value);
        var plus = new Point(core.Frame.Width - SkUiLook.Current.StepperButtonSize.Width / 2, core.Frame.Height / 2);
        Tap(root, plus);
        Assert.Equal(17, core.Value);
        core.SetOrientation(StackOrientation.Vertical);
        SkUiTestHelpers.Arrange(root, 400, 200);
        var button = SkUiLook.Current.StepperButtonSize;
        // It fills the host's width (as Core nodes do); the buttons stack.
        Assert.Equal(button.Height * 2, core.Frame.Height, 3);
        Tap(root, new Point(button.Width / 2, button.Height * 1.5), 1000);
        Assert.Equal(16, core.Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => core.SetOrientation((StackOrientation)7));
        // Texts in place of − and + widen the stepper as on the SkUi layer.
        using var _ = SkUiTestHelpers.UseBundledFont();
        var plain = core.Measure(double.PositiveInfinity, double.PositiveInfinity);
        core.SetOrientation(StackOrientation.Horizontal).SetIncrementText("More").SetDecrementText("Less")
            .SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetFontSize(24);
        Assert.True(core.Measure(double.PositiveInfinity, double.PositiveInfinity).Width > plain.Width * 2 + 20);
        Assert.Throws<ArgumentOutOfRangeException>(() => core.SetFontSize(0));
    }

    private sealed class RecordingProxy : ISkUiTextInputProxy
    {
        public readonly List<string> Calls = [];
        public bool ReceivesCharacters => false;
        public void Attach(ISkUiTextInputClient client) { client.InputAttached(this); Calls.Add("attach"); }
        public void Detach(ISkUiTextInputClient client) { client.InputAttached(null); Calls.Add("detach"); }
        public void ShowKeyboard(ISkUiTextInputClient client) { }
        public void HideKeyboard(ISkUiTextInputClient client) { }
        public void StateChanged(ISkUiTextInputClient client, bool textChanged) { }
        public void OptionsChanged(ISkUiTextInputClient client) { }
        public void GeometryChanged(ISkUiTextInputClient client) { }
        public void ShowEditMenu(ISkUiTextInputClient client, Rect anchor) { }
        public void HideEditMenu(ISkUiTextInputClient client) { }
    }
}
