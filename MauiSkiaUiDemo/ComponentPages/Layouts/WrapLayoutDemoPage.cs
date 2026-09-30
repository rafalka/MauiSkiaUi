using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Property playground for <see cref="SkUiWrapLayout"/>: chips that wrap onto new rows.</summary>
public sealed class WrapLayoutDemoPage : ComponentDemoPage
{
    private static readonly string[] Words = ["Chips", "wrap", "onto new rows", "when", "the next one", "does not fit", "tags", "filters", "#skia"];

    public WrapLayoutDemoPage()
        : base(nameof(SkUiWrapLayout), new SkUiWrapLayout(), widthRange: (80, 360, 240), heightRange: (60, 320, 160))
    {
        var wrap = (SkUiWrapLayout)SkiaControl;
        wrap.BackgroundColor = Colors.LightGray;
        foreach (var word in Words)
            wrap.Children.Add(Chip(word, wrap.Children.Count));

        Number(nameof(SkUiWrapLayout.Spacing), 0, 16, 6, value => wrap.Spacing = value, () => wrap.Spacing);
        Number(nameof(SkUiWrapLayout.RowSpacing), 0, 16, 6, value => wrap.RowSpacing = value, () => wrap.RowSpacing);
        Number(nameof(SkUiWrapLayout.Padding), 0, 24, 4, value => wrap.Padding = value, () => wrap.Padding.Left);
        Toggle("TallFirstChip", false, value => ((SkUiView)wrap.Children[0]).HeightRequest = value ? 44 : -1,
            () => ((SkUiView)wrap.Children[0]).HeightRequest > 0);
        Choice("ChipsVerticalOptions", ["Fill", "Start", "Center", "End"], "Fill",
            value => { foreach (var chip in wrap.Children.Skip(1)) ((SkUiView)chip).VerticalOptions = ToOptions(value); },
            () => FromOptions(((SkUiView)wrap.Children[1]).VerticalOptions));
        // One child changing its size: its row re-wraps, and later chips move between rows.
        SkUiLabel Chip3() => (SkUiLabel)wrap.Children[2];
        Text("Chip3.Text", Words[2], value => Chip3().Text = value, () => Chip3().Text);
        Number("Chip3.Width", 0, 240, 0, value => Chip3().WidthRequest = value <= 0 ? -1 : value,
            () => Chip3().WidthRequest < 0 ? 0 : Chip3().WidthRequest, whole: true);
        ActionButton("Add chip", () => wrap.Children.Add(Chip("chip " + (wrap.Children.Count + 1), wrap.Children.Count)));
        ActionButton("Remove chip", () => { if (wrap.Children.Count > 3) wrap.Children.RemoveAt(wrap.Children.Count - 1); });
        OnReset(() =>
        {
            while (wrap.Children.Count > Words.Length) wrap.Children.RemoveAt(wrap.Children.Count - 1);
            while (wrap.Children.Count < Words.Length) wrap.Children.Add(Chip(Words[wrap.Children.Count], wrap.Children.Count));
        });
    }

    private static SkUiLabel Chip(string text, int index) => new()
    {
        Text = text, TextColor = Colors.White, FontSize = 14, Padding = new Thickness(10, 4),
        Background = (index % 3) switch { 0 => Accent, 1 => DemoColors.SampleA, _ => DemoColors.SampleB },
        VerticalTextAlignment = TextAlignment.Center
    };

    private static LayoutOptions ToOptions(string value) => value switch
    {
        "Start" => LayoutOptions.Start,
        "Center" => LayoutOptions.Center,
        "End" => LayoutOptions.End,
        _ => LayoutOptions.Fill
    };

    private static string FromOptions(LayoutOptions options) => options.Alignment switch
    {
        LayoutAlignment.Start => "Start",
        LayoutAlignment.Center => "Center",
        LayoutAlignment.End => "End",
        _ => "Fill"
    };
}
