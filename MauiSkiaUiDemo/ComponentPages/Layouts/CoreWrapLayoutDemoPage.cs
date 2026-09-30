using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUiDemo;

/// <summary>Core wrap layout playground: chips that wrap onto new rows.</summary>
public sealed class CoreWrapLayoutDemoPage : ComponentDemoPage
{
    private static readonly string[] Words = ["Core", "chips", "wrap onto", "new rows", "when the next", "one does not fit", "#core"];
    private static readonly Color[] Fills = [Color.FromArgb("#BFDBFE"), Color.FromArgb("#BBF7D0"), Color.FromArgb("#FDE68A")];

    public CoreWrapLayoutDemoPage()
        : base(nameof(SkUiCoreWrapLayout), CreateHost(out var wrap), native: null, widthRange: (80, 360, 240), heightRange: (60, 320, 160))
    {
        Number(nameof(SkUiCoreWrapLayout.Spacing), 0, 16, 6, value => wrap.Spacing = value, () => wrap.Spacing);
        Number(nameof(SkUiCoreWrapLayout.RowSpacing), 0, 16, 6, value => wrap.RowSpacing = value, () => wrap.RowSpacing);
        Number(nameof(SkUiCoreWrapLayout.Padding), 0, 24, 4, value => wrap.Padding = new Thickness(value), () => wrap.Padding.Left);
        Toggle("FirstChipVisible", true, value => ((SkUiCoreNode)wrap.Children[0]).IsVisible = value, () => wrap.Children[0].IsVisible);
        // One child changing its size: its row re-wraps, and later chips move between rows.
        SkUiCoreLabel Chip3() => (SkUiCoreLabel)wrap.Children[2];
        Text("Chip3.Text", Words[2], value => Chip3().SetText(value), () => Chip3().Text);
        Number("Chip3.Width", 0, 240, 0, value => Chip3().Width = value <= 0 ? double.NaN : value,
            () => double.IsNaN(Chip3().Width) ? 0 : Chip3().Width, whole: true);
        Number("Chip3.Height", 0, 80, 0, value => Chip3().Height = value <= 0 ? double.NaN : value,
            () => double.IsNaN(Chip3().Height) ? 0 : Chip3().Height, whole: true);
        ActionButton("Add chip", () => wrap.Add(Chip("chip " + (wrap.Children.Count + 1), wrap.Children.Count)));
        ActionButton("Remove chip", () => { if (wrap.Children.Count > 3) wrap.Remove(wrap.Children[^1]); });
        OnReset(() =>
        {
            while (wrap.Children.Count > Words.Length) wrap.Remove(wrap.Children[^1]);
            while (wrap.Children.Count < Words.Length) wrap.Add(Chip(Words[wrap.Children.Count], wrap.Children.Count));
        });
    }

    private static SkUiCoreHost CreateHost(out SkUiCoreWrapLayout wrap)
    {
        wrap = new SkUiCoreWrapLayout();
        foreach (var word in Words)
            wrap.Add(Chip(word, wrap.Children.Count));
        var host = new SkUiCoreHost { BackgroundColor = Colors.LightGray };
        host.SetContent(wrap);
        return host;
    }

    private static SkUiCoreLabel Chip(string text, int index) =>
        new SkUiCoreLabel().SetText(text).SetTextColor(DemoColors.Ink).SetFontSize(14).SetPadding(new Thickness(10, 4))
            .SetFillColor(Fills[index % Fills.Length]).SetCornerRadius(12).SetVerticalTextAlignment(TextAlignment.Center);
}
