using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiSwitch"/>.</summary>
public sealed class SwitchDemoPage : ComponentDemoPage
{
    public SwitchDemoPage() : base(nameof(SkUiSwitch), new SkUiSwitch(), new Switch())
    {
        var skia = (SkUiSwitch)SkiaControl;
        var native = (Switch)NativeControl!;
        Toggle(nameof(SkUiSwitch.IsChecked), false, value => { skia.IsChecked = value; native.IsToggled = value; }, () => skia.IsChecked, () => native.IsToggled);
        ColorEditor(nameof(SkUiSwitch.OnColor), Accent, value => { skia.OnColor = value; native.OnColor = value; }, () => skia.OnColor, () => native.OnColor);
    }
}
