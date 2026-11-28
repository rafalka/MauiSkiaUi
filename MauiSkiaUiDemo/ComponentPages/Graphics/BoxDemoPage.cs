using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiBox"/> against MAUI's BoxView.</summary>
public sealed class BoxDemoPage : ComponentDemoPage
{
    public BoxDemoPage() : base(nameof(SkUiBox), new SkUiBox(), new BoxView())
    {
        var skia = (SkUiBox)SkiaControl;
        var native = (BoxView)NativeControl!;
        // The color is the box's fill; the brush editor below clears it for a gradient Background, as MAUI's BoxView.
        var color = Accent;
        ColorEditor(nameof(SkUiBox.Color), Accent, value =>
        {
            color = value;
            if (skia.Color is null) return;
            skia.Color = value;
            native.Color = value;
        }, () => skia.Color ?? color, () => native.Color ?? color);
        // Fill (MAUI 11's BoxView) wins over Color and the Background; the drawn box has it on both lines.
        Func<string>? nativeFill = null;
#if NET11_0_OR_GREATER
        nativeFill = () => FillName(native.Fill);
#endif
        Choice(nameof(SkUiBox.Fill), Fills, NoFill, value =>
        {
            skia.Fill = FillBrush(value);
#if NET11_0_OR_GREATER
            native.Fill = FillBrush(value);
#endif
        }, () => FillName(skia.Fill), nativeFill);
        Number(nameof(SkUiBox.CornerRadius), 0, 60, 0, value => { skia.CornerRadius = value; native.CornerRadius = value; },
            () => skia.CornerRadius.TopLeft, () => native.CornerRadius.TopLeft);
        Number(nameof(VisualElement.Rotation), -45, 45, 0, value => { skia.Rotation = value; native.Rotation = value; }, () => skia.Rotation, () => native.Rotation);
        // A gradient fills the box through its Background once Color is cleared, as MAUI's BoxView.
        EffectEditors(skia, native, (view, brush) =>
        {
            if (view is SkUiBox box) box.Color = brush is null ? color : null;
            if (view is BoxView boxView) boxView.Color = brush is null ? color : null;
            view.Background = brush;
        });
    }

    private const string NoFill = "None";
    private const string GradientFill = "Gradient";
    private const string SolidFill = "Solid";
    private static readonly string[] Fills = [NoFill, GradientFill, SolidFill];
    private static readonly Color SolidFillColor = Color.FromArgb("#2E7D32");

    private static Brush? FillBrush(string name) => name switch
    {
        GradientFill => new LinearGradientBrush([new GradientStop(Color.FromArgb("#FF8A00"), 0), new GradientStop(Color.FromArgb("#E52E71"), 1)],
            new Point(0, 0), new Point(1, 1)),
        SolidFill => new SolidColorBrush(SolidFillColor),
        _ => null
    };

    private static string FillName(Brush? brush) => brush switch
    {
        LinearGradientBrush => GradientFill,
        SolidColorBrush => SolidFill,
        _ => NoFill
    };
}
