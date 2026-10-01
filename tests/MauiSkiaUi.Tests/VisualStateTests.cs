using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// MAUI visual states on drawn controls (Phase P2 in ImplementationPlan.md): each control goes through the same states as
/// its MAUI counterpart for the same steps, plus <c>PointerOver</c> from hover input and state triggers.
/// </summary>
public class VisualStateTests
{
    private static readonly Point Inside = new(10, 10);

    /// <summary>Groups with every state MAUI's controls raise, so each step shows where every group is.</summary>
    private static VisualStateGroupList Groups(bool isCheckedState = true)
    {
        var common = new VisualStateGroup { Name = "CommonStates" };
        foreach (var name in new[] { "Normal", "Disabled", "Pressed", "PointerOver", "On", "Off" })
            common.States.Add(new VisualState { Name = name });
        if (isCheckedState)
            common.States.Add(new VisualState { Name = "IsChecked" });
        var focus = new VisualStateGroup { Name = "FocusStates", States = { new VisualState { Name = "Focused" }, new VisualState { Name = "Unfocused" } } };
        var check = new VisualStateGroup { Name = "CheckedStates", States = { new VisualState { Name = "Checked" }, new VisualState { Name = "Unchecked" } } };
        return [common, focus, check];
    }

    private static string States(VisualElement element) =>
        string.Join(" ", VisualStateManager.GetVisualStateGroups(element).Select(group => group.CurrentState?.Name ?? "-"));

    /// <summary>Runs the same steps on both controls and returns the states after each, MAUI first.</summary>
    private static (List<string> Maui, List<string> SkUi) Run<TMaui, TSkUi>(TMaui maui, TSkUi skui, bool isCheckedState,
        params (string Step, Action<TMaui> OnMaui, Action<TSkUi> OnSkUi)[] steps)
        where TMaui : VisualElement where TSkUi : SkUiView
    {
        SkUiTestHelpers.Arrange(skui, 100, 50);
        VisualStateManager.SetVisualStateGroups(maui, Groups(isCheckedState));
        VisualStateManager.SetVisualStateGroups(skui, Groups(isCheckedState));
        List<string> mauiStates = [$"start: {States(maui)}"], skuiStates = [$"start: {States(skui)}"];
        foreach (var (step, onMaui, onSkUi) in steps)
        {
            onMaui(maui);
            onSkUi(skui);
            mauiStates.Add($"{step}: {States(maui)}");
            skuiStates.Add($"{step}: {States(skui)}");
        }
        return (mauiStates, skuiStates);
    }

    private static void Press(SkUiView view) => view.Touch(new(1, SkUiTouchAction.Pressed, Inside));
    private static void Release(SkUiView view) => view.Touch(new(1, SkUiTouchAction.Released, Inside));

    [Fact]
    public void ButtonsGoThroughMauisStates()
    {
        var (maui, skui) = Run(new Button(), new SkUiButton { Text = "" }, isCheckedState: true,
            ("press", b => ((IButtonController)b).SendPressed(), Press),
            ("release", b => ((IButtonController)b).SendReleased(), Release),
            ("disable", b => b.IsEnabled = false, b => b.IsEnabled = false),
            ("enable", b => b.IsEnabled = true, b => b.IsEnabled = true));
        Assert.Equal(maui, skui);
        Assert.Equal("press: Pressed Unfocused -", skui[1]);

        using var image = new SkUiImageButton();
        var (mauiImage, skuiImage) = Run(new ImageButton(), image, isCheckedState: true,
            ("press", b => ((IButtonController)b).SendPressed(), Press),
            ("release", b => ((IButtonController)b).SendReleased(), Release),
            ("disable", b => b.IsEnabled = false, b => b.IsEnabled = false));
        Assert.Equal(mauiImage, skuiImage);
    }

    [Fact]
    public void CommandThatCannotExecuteIsDisabled()
    {
        var canExecute = true;
        var command = new Command(() => { }, () => canExecute);
        var (maui, skui) = Run(new Button { Command = command }, new SkUiButton { Text = "", Command = command }, isCheckedState: true,
            ("cannot execute", _ => { canExecute = false; command.ChangeCanExecute(); }, _ => { }),
            ("can execute", _ => { canExecute = true; command.ChangeCanExecute(); }, _ => { }));
        Assert.Equal(maui, skui);
        Assert.Equal("cannot execute: Disabled Unfocused -", skui[1]); // disabled leaves the focus group alone, as in MAUI
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CheckBoxGoesToIsCheckedOnlyWhenTheStateExists(bool isCheckedState)
    {
        var (maui, skui) = Run(new CheckBox(), new SkUiCheckBox(), isCheckedState,
            ("check", c => c.IsChecked = true, c => c.IsChecked = true),
            ("disable", c => c.IsEnabled = false, c => c.IsEnabled = false),
            ("enable", c => c.IsEnabled = true, c => c.IsEnabled = true),
            ("uncheck", c => c.IsChecked = false, c => c.IsChecked = false));
        Assert.Equal(maui, skui);
        Assert.Equal(isCheckedState ? "check: IsChecked Unfocused -" : "check: Normal Unfocused -", skui[1]);
    }

    [Fact]
    public void SwitchAndRadioButtonGoThroughMauisStates()
    {
        var (mauiSwitch, skuiSwitch) = Run(new Switch(), new SkUiSwitch(), isCheckedState: true,
            ("on", s => s.IsToggled = true, s => s.IsToggled = true),
            ("disable", s => s.IsEnabled = false, s => s.IsEnabled = false),
            ("off", s => s.IsToggled = false, s => s.IsToggled = false),
            ("enable", s => s.IsEnabled = true, s => s.IsEnabled = true));
        Assert.Equal(mauiSwitch, skuiSwitch);

        var (mauiRadio, skuiRadio) = Run(new RadioButton(), new SkUiRadioButton(), isCheckedState: true,
            ("check", r => r.IsChecked = true, r => r.IsChecked = true),
            ("disable", r => r.IsEnabled = false, r => r.IsEnabled = false),
            ("uncheck", r => r.IsChecked = false, r => r.IsChecked = false));
        Assert.Equal(mauiRadio, skuiRadio);
        Assert.Equal("check: Normal Unfocused Checked", skuiRadio[1]);
    }

    [Fact]
    public void HoverSetsPointerOverOnTheViewUnderThePointerAndItsAncestors()
    {
        var first = new SkUiButton { Text = "", WidthRequest = 100, HeightRequest = 40 };
        var second = new SkUiButton { Text = "", WidthRequest = 100, HeightRequest = 40 };
        var stack = new SkUiVerticalStackLayout { Children = { first, second } };
        var root = new SkUiContentView { Content = stack };
        foreach (var view in new SkUiView[] { first, second, stack })
            VisualStateManager.SetVisualStateGroups(view, Groups());
        SkUiTestHelpers.Arrange(root, 100, 80);

        root.Touch(new(0, SkUiTouchAction.HoverMoved, new Point(10, 10)));
        Assert.Equal((true, false, true), (first.IsPointerOver, second.IsPointerOver, stack.IsPointerOver));
        Assert.Equal("PointerOver", VisualStateManager.GetVisualStateGroups(first)[0].CurrentState?.Name);
        Assert.Equal("PointerOver", VisualStateManager.GetVisualStateGroups(stack)[0].CurrentState?.Name);

        root.Touch(new(0, SkUiTouchAction.HoverMoved, new Point(10, 50)));
        Assert.Equal((false, true, true), (first.IsPointerOver, second.IsPointerOver, stack.IsPointerOver));
        Assert.Equal("Normal", VisualStateManager.GetVisualStateGroups(first)[0].CurrentState?.Name);

        // A press while hovered shows Pressed; on release the pointer is still over the button.
        root.Touch(new(1, SkUiTouchAction.Pressed, new Point(10, 50)));
        Assert.Equal("Pressed", VisualStateManager.GetVisualStateGroups(second)[0].CurrentState?.Name);
        root.Touch(new(1, SkUiTouchAction.Released, new Point(10, 50)));
        Assert.Equal("PointerOver", VisualStateManager.GetVisualStateGroups(second)[0].CurrentState?.Name);

        root.Touch(new(0, SkUiTouchAction.HoverExited, Point.Zero));
        Assert.Equal((false, false, false), (first.IsPointerOver, second.IsPointerOver, stack.IsPointerOver));
        Assert.Equal("Normal", VisualStateManager.GetVisualStateGroups(second)[0].CurrentState?.Name);
    }

    [Fact]
    public void PassiveViewsAreHoveredToo()
    {
        var label = new SkUiLabel { Text = "", WidthRequest = 100, HeightRequest = 40 };
        var root = new SkUiContentView { Content = label };
        VisualStateManager.SetVisualStateGroups(label, Groups());
        SkUiTestHelpers.Arrange(root, 100, 40);
        root.Touch(new(0, SkUiTouchAction.HoverMoved, new Point(10, 10)));
        Assert.True(label.IsPointerOver);
        Assert.Equal("PointerOver", VisualStateManager.GetVisualStateGroups(label)[0].CurrentState?.Name);
        root.Router.CancelAll(); // surface detached
        Assert.False(label.IsPointerOver);
    }

    [Fact]
    public void StateTriggersApplyToDrawnControls()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var model = new TriggerModel();
        var label = new SkUiLabel { Text = "" };
        var trigger = new StateTrigger { IsActive = false };
        var compare = new CompareStateTrigger { Value = "wide" };
        compare.SetBinding(CompareStateTrigger.PropertyProperty, nameof(TriggerModel.Mode));
        var adaptive = new AdaptiveTrigger { MinWindowWidth = 600 };
        VisualStateManager.SetVisualStateGroups(label, [
            Group("Triggered", "Default", "Active", trigger, SkUiLabel.TextColorProperty, Colors.Red),
            Group("Compared", "Narrow", "Wide", compare, SkUiLabel.FontSizeProperty, 30d),
            Group("Adaptive", "Small", "Large", adaptive, SkUiLabel.TextProperty, "large window"),
        ]);
        // As in MAUI, the small state needs its own trigger to be switched back to.
        VisualStateManager.GetVisualStateGroups(label)[2].States[0].StateTriggers.Add(new AdaptiveTrigger { MinWindowWidth = 0 });
        // MAUI hands the binding context to state triggers when it changes, so set it after the groups.
        label.BindingContext = model;
        // Triggers attach when the element joins a window.
        var window = new Window(new ContentPage { Content = new SkUiContentView { Content = label } });
        Assert.Same(window, label.Window);

        trigger.IsActive = true;
        Assert.Equal(Colors.Red, label.TextColor);
        model.Mode = "wide";
        Assert.Equal(30d, label.FontSize);
        ((IWindow)window).FrameChanged(new Rect(0, 0, 800, 600));
        Assert.Equal("large window", label.Text);
        ((IWindow)window).FrameChanged(new Rect(0, 0, 400, 600));
        Assert.Equal("", label.Text);
    }

    private static VisualStateGroup Group(string name, string idle, string triggered, StateTriggerBase trigger, BindableProperty property, object value)
    {
        var state = new VisualState { Name = triggered, StateTriggers = { trigger } };
        state.Setters.Add(new Setter { Property = property, Value = value });
        return new VisualStateGroup { Name = name, States = { new VisualState { Name = idle }, state } };
    }

    private sealed class TriggerModel : System.ComponentModel.INotifyPropertyChanged
    {
        private string _mode = "narrow";

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public string Mode
        {
            get => _mode;
            set
            {
                _mode = value;
                PropertyChanged?.Invoke(this, new(nameof(Mode)));
            }
        }
    }
}
