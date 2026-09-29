using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiSwitch"/>.</summary>
public sealed class SwitchDemoPage : ComponentDemoPage
{
    public SwitchDemoPage() : base(nameof(SkUiSwitch), new SkUiSwitch(), new Switch())
    {
        var skia = (SkUiSwitch)SkiaControl;
        var native = (Switch)NativeControl!;
        Choice(nameof(SkUiSwitch.CheckState), [SkUiCheckState.Unchecked, SkUiCheckState.Checked, SkUiCheckState.Indeterminate], SkUiCheckState.Unchecked,
            // IsChecked (MAUI) is its two-state view; the native control follows it.
            value => { skia.CheckState = value; native.IsToggled = value == SkUiCheckState.Checked; }, () => skia.CheckState);
        Toggle(nameof(SkUiSwitch.IsThreeState), false, value => skia.IsThreeState = value, () => skia.IsThreeState);
        ColorEditor(nameof(SkUiSwitch.OnColor), Accent, value => { skia.OnColor = value; native.OnColor = value; }, () => skia.OnColor, () => native.OnColor);
    }
}
