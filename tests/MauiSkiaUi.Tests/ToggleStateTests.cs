using System.ComponentModel;
using System.Runtime.CompilerServices;
using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>Three-state toggles (<see cref="SkUiCheckState"/>) on both layers, and user changes reaching bindings.</summary>
public class ToggleStateTests
{
    private static void Tap(SkUiView control)
    {
        var root = SkUiDiagnostics.GetSurfaceRoot(control)!;
        SkUiTestHelpers.Arrange(root, 100, 40);
        Assert.NotNull(SkUiDiagnostics.SimulateTap(control));
    }

    private static T Hosted<T>(T control) where T : SkUiView
    {
        _ = new SkUiContentView { Content = control };
        return control;
    }

    [Fact]
    public void TwoStateTapsToggleAndIndeterminateGoesToChecked()
    {
        var box = Hosted(new SkUiCheckBox());
        Tap(box);
        Assert.Equal(SkUiCheckState.Checked, box.CheckState);
        Assert.True(box.IsChecked);
        Tap(box);
        Assert.Equal(SkUiCheckState.Unchecked, box.CheckState);

        box.CheckState = SkUiCheckState.Indeterminate; // e.g. "some items in the group checked"
        Assert.False(box.IsChecked);
        Tap(box);
        Assert.Equal(SkUiCheckState.Checked, box.CheckState);
    }

    [Fact]
    public void ThreeStateTapsCycleThroughIndeterminate()
    {
        var box = Hosted(new SkUiCheckBox { IsThreeState = true });
        var seen = new List<SkUiCheckState>();
        box.CheckStateChanged += (_, state) => seen.Add(state);
        for (var tap = 0; tap < 3; tap++)
            Tap(box);
        Assert.Equal([SkUiCheckState.Checked, SkUiCheckState.Indeterminate, SkUiCheckState.Unchecked], seen);
    }

    [Fact]
    public void IsCheckedWrapsTheStateAndRaisesOnlyItsOwnChanges()
    {
        var box = new SkUiCheckBox();
        var checkedChanges = new List<bool>();
        box.CheckedChanged += (_, value) => checkedChanges.Add(value);
        box.IsChecked = true;
        Assert.Equal(SkUiCheckState.Checked, box.CheckState);
        box.CheckState = SkUiCheckState.Indeterminate;
        Assert.False(box.IsChecked);
        Assert.False((bool)box.GetValue(SkUiToggleControl.IsCheckedProperty));
        box.CheckState = SkUiCheckState.Unchecked; // IsChecked stays false: no CheckedChanged
        Assert.Equal([true, false], checkedChanges);
        box.IsChecked = true;
        box.IsChecked = false;
        Assert.Equal(SkUiCheckState.Unchecked, box.CheckState);
    }

    [Fact]
    public void TapsWriteBackToTwoWayBindings()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var model = new ToggleModel { State = SkUiCheckState.Indeterminate };
        var box = Hosted(new SkUiCheckBox { BindingContext = model });
        box.SetBinding(SkUiToggleControl.CheckStateProperty, nameof(ToggleModel.State));
        box.SetBinding(SkUiToggleControl.IsCheckedProperty, nameof(ToggleModel.IsOn));
        Assert.Equal(SkUiCheckState.Indeterminate, box.CheckState);

        Tap(box);
        Assert.Equal(SkUiCheckState.Checked, model.State);
        Assert.True(model.IsOn);
        Tap(box);
        Assert.Equal(SkUiCheckState.Unchecked, model.State);
        Assert.False(model.IsOn);

        model.IsOn = true; // source → control
        Assert.Equal(SkUiCheckState.Checked, box.CheckState);
        Assert.Equal(SkUiCheckState.Checked, model.State);
    }

    [Fact]
    public void ChangeEventsSeeBindingsAlreadyUpdated()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var model = new ToggleModel();
        var box = Hosted(new SkUiCheckBox { BindingContext = model });
        box.SetBinding(SkUiToggleControl.CheckStateProperty, nameof(ToggleModel.State));
        box.SetBinding(SkUiToggleControl.IsCheckedProperty, nameof(ToggleModel.IsOn));
        var seen = new List<string>();
        box.CheckStateChanged += (_, state) => seen.Add($"state {state}: model {model.State}, {model.IsOn}, store {box.GetValue(SkUiToggleControl.IsCheckedProperty)}");
        box.CheckedChanged += (_, isChecked) => seen.Add($"checked {isChecked}: model {model.State}, {model.IsOn}");

        Tap(box);
        Assert.Equal(["state Checked: model Checked, True, store True", "checked True: model Checked, True"], seen);
        seen.Clear();
        model.IsOn = false; // source → control: the other binding is updated before the events too
        Assert.Equal(["state Unchecked: model Unchecked, False, store False", "checked False: model Unchecked, False"], seen);
    }

    [Fact]
    public void SettingIsCheckedFalseClearsIndeterminate()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var box = new SkUiCheckBox { CheckState = SkUiCheckState.Indeterminate };
        box.IsChecked = false; // IsChecked is already false: still clears the state, like the fluent and Core setters
        Assert.Equal(SkUiCheckState.Unchecked, box.CheckState);

        var model = new ToggleModel { IsOn = true };
        var bound = new SkUiRadioButton { BindingContext = model };
        bound.SetBinding(SkUiToggleControl.IsCheckedProperty, nameof(ToggleModel.IsOn));
        bound.CheckState = SkUiCheckState.Indeterminate;
        Assert.False(model.IsOn);
        bound.IsChecked = false; // e.g. an app clearing radio siblings
        Assert.Equal(SkUiCheckState.Unchecked, bound.CheckState);
        Assert.False(model.IsOn);
    }

    [Fact]
    public void RadioTapsOnlySelectEvenFromIndeterminate()
    {
        var radio = Hosted(new SkUiRadioButton { IsThreeState = true, CheckState = SkUiCheckState.Indeterminate });
        Tap(radio);
        Assert.Equal(SkUiCheckState.Checked, radio.CheckState);
        Tap(radio);
        Assert.Equal(SkUiCheckState.Checked, radio.CheckState);
    }

    [Fact]
    public void SwitchAcceptsThreeStatesForCommonality()
    {
        var toggle = Hosted(new SkUiSwitch { IsThreeState = true });
        Tap(toggle);
        Tap(toggle);
        Assert.Equal(SkUiCheckState.Indeterminate, toggle.CheckState);
    }

    [Fact]
    public void CoreTogglesMatchTheSkUiRules()
    {
        var box = new SkUiCoreCheckBox();
        var names = new List<string?>();
        box.PropertyChanged += (_, args) => names.Add(args.PropertyName);
        var host = new SkUiCoreHost().SetContent(box);
        SkUiTestHelpers.Arrange(host, 40, 40);

        SkUiDiagnostics.SimulateTap(box);
        Assert.Equal(SkUiCheckState.Checked, box.CheckState);
        box.SetCheckState(SkUiCheckState.Indeterminate);
        Assert.False(box.IsChecked);
        Assert.Contains(nameof(SkUiCoreCheckBox.IsChecked), names);
        SkUiDiagnostics.SimulateTap(box);
        Assert.Equal(SkUiCheckState.Checked, box.CheckState);

        box.SetIsThreeState(true);
        SkUiDiagnostics.SimulateTap(box);
        Assert.Equal(SkUiCheckState.Indeterminate, box.CheckState);

        var radio = new SkUiCoreRadioButton();
        radio.SetCheckState(SkUiCheckState.Indeterminate);
        var radioHost = new SkUiCoreHost().SetContent(radio);
        SkUiTestHelpers.Arrange(radioHost, 40, 40);
        SkUiDiagnostics.SimulateTap(radio);
        Assert.Equal(SkUiCheckState.Checked, radio.CheckState);
    }

    [Fact]
    public void IndeterminateCheckBoxDrawsADash()
    {
        static SKColor Center(SkUiCheckState state)
        {
            using var bitmap = new SKBitmap(24, 24);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            SkUiLook.Current.DrawCheckBox(canvas, 24, state, SKColors.Teal, SKColors.Teal);
            return bitmap.GetPixel(12, 12);
        }
        Assert.Equal(SKColors.White, Center(SkUiCheckState.Indeterminate)); // the dash crosses the center
        Assert.NotEqual(SKColors.White, Center(SkUiCheckState.Checked)); // the check mark does not
    }

    private sealed class ToggleModel : INotifyPropertyChanged
    {
        private SkUiCheckState _state;
        private bool _isOn;

        public event PropertyChangedEventHandler? PropertyChanged;

        public SkUiCheckState State { get => _state; set => Set(ref _state, value); }

        public bool IsOn { get => _isOn; set => Set(ref _isOn, value); }

        private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
