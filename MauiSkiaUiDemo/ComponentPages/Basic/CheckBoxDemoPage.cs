using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiCheckBox"/>.</summary>
public sealed class CheckBoxDemoPage : ComponentDemoPage
{
    public CheckBoxDemoPage() : base(nameof(SkUiCheckBox), new SkUiCheckBox(), new CheckBox())
    {
        var skia = (SkUiCheckBox)SkiaControl;
        var native = (CheckBox)NativeControl!;
        Toggle(nameof(SkUiCheckBox.IsChecked), false, value => { skia.IsChecked = value; native.IsChecked = value; }, () => skia.IsChecked, () => native.IsChecked);
        ColorEditor(nameof(SkUiCheckBox.Color), Accent, value => { skia.Color = value; native.Color = value; }, () => skia.Color, () => native.Color);
    }
}
