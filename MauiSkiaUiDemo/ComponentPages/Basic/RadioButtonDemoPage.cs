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
        Toggle(nameof(SkUiRadioButton.IsChecked), false, value => { skia.IsChecked = value; native.IsChecked = value; }, () => skia.IsChecked, () => native.IsChecked);
        ColorEditor(nameof(SkUiRadioButton.Color), Accent, value => skia.Color = value, () => skia.Color);
    }
}
