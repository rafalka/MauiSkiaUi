using MauiSkiaUi;
using Microsoft.Maui.Controls.Shapes;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiProgressRing"/> (1.18 P2, #128): a circular progress indicator with the remaining hours in the middle, as a
/// time-clock card shows them; editors for every property and <c>ProgressTo</c>.
/// </summary>
public sealed class ProgressRingDemoPage : ComponentDemoPage
{
    public ProgressRingDemoPage() : base(nameof(SkUiProgressRing), CreateSample(out var ring, out var value))
    {
        void Show() => value.Text = $"{ring.Progress * 8:0.#} h";
        ring.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SkUiProgressRing.Progress))
                Show();
        };
        Show();
        Number(nameof(SkUiProgressRing.Progress), 0, 1, 0.65, value => ring.Progress = value, () => ring.Progress);
        Toggle(nameof(SkUiProgressRing.IsIndeterminate), false, value => ring.IsIndeterminate = value, () => ring.IsIndeterminate);
        Number(nameof(SkUiProgressRing.Thickness), -1, 24, 10, value => ring.Thickness = value < 0 ? -1 : value, () => ring.Thickness);
        Number(nameof(SkUiProgressRing.StartAngle), -180, 180, -90, value => ring.StartAngle = value, () => ring.StartAngle, whole: true);
        Toggle(nameof(SkUiProgressRing.IsClockwise), true, value => ring.IsClockwise = value, () => ring.IsClockwise);
        Choice(nameof(SkUiProgressRing.LineCap), Enum.GetValues<PenLineCap>(), PenLineCap.Round, value => ring.LineCap = value, () => ring.LineCap);
        ColorEditor(nameof(SkUiProgressRing.ProgressColor), Accent, value => ring.ProgressColor = value, () => ring.ProgressColor);
        ColorEditor(nameof(SkUiProgressRing.TrackColor), SkUiColors.TrackOff, value => ring.TrackColor = value, () => ring.TrackColor);
        ActionButton("ProgressTo 1 (1 s)", () => _ = ring.ProgressTo(1, 1000, Easing.CubicInOut));
        ActionButton("ProgressTo 0 (1 s)", () => _ = ring.ProgressTo(0, 1000, Easing.CubicInOut));
    }

    internal static SkUiProgressRing CreateSample(out SkUiProgressRing ring, out SkUiLabel value)
    {
        value = new SkUiLabel { FontSize = 20, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center };
        var caption = new SkUiLabel { Text = "left today", FontSize = 11, HorizontalTextAlignment = TextAlignment.Center };
        ring = new SkUiProgressRing
        {
            Progress = 0.65, Thickness = 10, WidthRequest = 140, HeightRequest = 140,
            Content = new SkUiVerticalStackLayout { VerticalOptions = LayoutOptions.Center, Children = { value, caption } }
        };
        SemanticProperties.SetDescription(ring, "Hours left today");
        return ring;
    }
}
