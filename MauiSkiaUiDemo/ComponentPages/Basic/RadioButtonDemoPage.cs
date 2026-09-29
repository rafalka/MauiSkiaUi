using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiRadioButton"/>.</summary>
public sealed class RadioButtonDemoPage : ComponentDemoPage
{
    public RadioButtonDemoPage() : base(nameof(SkUiRadioButton), new SkUiRadioButton(), new RadioButton())
    {
        var skia = (SkUiRadioButton)SkiaControl;
        var native = (RadioButton)NativeControl!;
        native.Content = "Option";
        Choice(nameof(SkUiRadioButton.CheckState), [SkUiCheckState.Unchecked, SkUiCheckState.Checked, SkUiCheckState.Indeterminate], SkUiCheckState.Unchecked,
            // IsChecked (MAUI) is its two-state view; the native control follows it.
            value => { skia.CheckState = value; native.IsChecked = value == SkUiCheckState.Checked; }, () => skia.CheckState);
        ColorEditor(nameof(SkUiRadioButton.Color), Accent, value => skia.Color = value, () => skia.Color);
    }
}
