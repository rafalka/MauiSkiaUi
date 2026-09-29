using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiCheckBox"/>.</summary>
public sealed class CheckBoxDemoPage : ComponentDemoPage
{
    public CheckBoxDemoPage() : base(nameof(SkUiCheckBox), new SkUiCheckBox(), new CheckBox())
    {
        var skia = (SkUiCheckBox)SkiaControl;
        var native = (CheckBox)NativeControl!;
        Choice(nameof(SkUiCheckBox.CheckState), [SkUiCheckState.Unchecked, SkUiCheckState.Checked, SkUiCheckState.Indeterminate], SkUiCheckState.Unchecked,
            // IsChecked (MAUI) is its two-state view; the native control follows it.
            value => { skia.CheckState = value; native.IsChecked = value == SkUiCheckState.Checked; }, () => skia.CheckState);
        Toggle(nameof(SkUiCheckBox.IsThreeState), false, value => skia.IsThreeState = value, () => skia.IsThreeState);
        ColorEditor(nameof(SkUiCheckBox.Color), Accent, value => { skia.Color = value; native.Color = value; }, () => skia.Color, () => native.Color);
    }
}
