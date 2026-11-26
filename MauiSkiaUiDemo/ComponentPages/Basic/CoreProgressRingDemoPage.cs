using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiCoreProgressRing"/> and <see cref="SkUiCoreStackedBar"/> (1.18 P3, P4): the progress ring with a label node
/// inside and the stacked bar on Core nodes, drawn by the same engine and look as the SkUi controls.
/// </summary>
public sealed class CoreProgressRingDemoPage : ComponentDemoPage
{
    public CoreProgressRingDemoPage() : base(nameof(SkUiCoreProgressRing), CreateSample(out var ring, out var label, out var bar))
    {
        void Show() => label.SetText($"{ring.Progress:P0}");
        Show();
        Number(nameof(SkUiCoreProgressRing.Progress), 0, 1, 0.4, value => { ring.SetProgress(value); Show(); }, () => ring.Progress);
        Toggle(nameof(SkUiCoreProgressRing.IsIndeterminate), false, value => ring.SetIsIndeterminate(value), () => ring.IsIndeterminate);
        Number("Bar: worked", 0, 40, 30, value => bar.SetSegments(new SkUiBarValue(value, Colors.SteelBlue, "Worked"), bar.Segments[1]),
            () => bar.Segments[0].Value, whole: true);
        Number("Bar: overtime", 0, 20, 4, value => bar.SetSegments(bar.Segments[0], new SkUiBarValue(value, Colors.Orange, "Overtime")),
            () => bar.Segments[1].Value, whole: true);
        ActionButton("ProgressTo 1 (1 s)", () => _ = ring.ProgressTo(1, 1000, Easing.CubicInOut).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(Show)));
    }

    internal static SkUiCoreHost CreateSample(out SkUiCoreProgressRing ring, out SkUiCoreLabel label, out SkUiCoreStackedBar bar)
    {
        label = new SkUiCoreLabel().SetFontSize(18).SetHorizontalTextAlignment(TextAlignment.Center).SetVerticalTextAlignment(TextAlignment.Center);
        ring = new SkUiCoreProgressRing().SetProgress(0.4).SetThickness(8);
        ring.SetContent(label);
        ring.SetWidth(120).SetHeight(120);
        bar = new SkUiCoreStackedBar().SetMaximum(40).SetSegments(new SkUiBarValue(30, Colors.SteelBlue, "Worked"), new SkUiBarValue(4, Colors.Orange, "Overtime"));
        bar.SetHeight(10);
        var stack = new SkUiCoreVerticalStackLayout().SetSpacing(16);
        stack.Add(ring);
        stack.Add(bar);
        return new SkUiCoreHost().SetContent(stack);
    }
}
