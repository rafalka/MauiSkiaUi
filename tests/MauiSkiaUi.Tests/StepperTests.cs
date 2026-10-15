using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// The drawn stepper (D7): MAUI's <c>Stepper</c> coercion, the look's − and + buttons (taps, held repeats, the range's ends), keys,
/// screen readers, the optional editable value (committed on Return or unfocus) and the Core twin.
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
    public void TheShownValueIsEditedAndCommitted()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var stepper = new SkUiStepper { Value = 5, Maximum = 50 };
        var buttonsOnly = ((IView)stepper).Measure(double.PositiveInfinity, double.PositiveInfinity).Width;
        stepper.IsValueVisible = true;
        Assert.True(((IView)stepper).Measure(double.PositiveInfinity, double.PositiveInfinity).Width > buttonsOnly + 40);
        var (root, _) = Field(stepper);
        Assert.Equal("5", stepper.Model.Field.Editor.Text);
        stepper.Focus();
        stepper.Model.Field.Editor.SelectAll();
        foreach (var character in "42")
            root.FocusManager.KeyDown(SkUiKey.Character, character: character);
        Assert.Equal(5, stepper.Value);
        root.FocusManager.KeyDown(SkUiKey.Enter);
        Assert.Equal(42, stepper.Value);
        // Not a number: the field goes back to the value; out of range: coerced.
        stepper.Model.Field.Editor.SelectAll();
        root.FocusManager.KeyDown(SkUiKey.Character, character: 'x');
        stepper.Unfocus();
        Assert.Equal("42", stepper.Model.Field.Editor.Text);
        stepper.Focus();
        stepper.Model.Field.Editor.SelectAll();
        foreach (var character in "500")
            root.FocusManager.KeyDown(SkUiKey.Character, character: character);
        stepper.Unfocus();
        Assert.Equal(50, stepper.Value);
        Assert.Equal("50", stepper.Model.Field.Editor.Text);
    }

    [Fact]
    public void OnlyAShownValueAttachesTheTextInput()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var proxy = new RecordingProxy();
        var (root, stepper) = Field();
        root.FocusManager.TextInput = proxy;
        stepper.Focus();
        Assert.Empty(proxy.Calls);
        stepper.IsValueVisible = true;
        Assert.Equal(["attach"], proxy.Calls);
        stepper.IsValueVisible = false;
        Assert.Equal(["attach", "detach"], proxy.Calls);
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
