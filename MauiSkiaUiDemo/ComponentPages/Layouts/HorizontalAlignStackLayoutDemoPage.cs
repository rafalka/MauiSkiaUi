using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// Property playground for <see cref="SkUiHorizontalAlignStackLayout"/>: a bar (white) with Cancel at the start side, a title
/// in the middle and a close button at the end side, over a marker of the layout's center. The sides differ, yet the title is
/// centered; edit the buttons' texts, show Clear or make the title longer to watch the title keep its place or move aside,
/// and compare the center strategies.
/// </summary>
public sealed class HorizontalAlignStackLayoutDemoPage : ComponentDemoPage
{
    private const string TitleText = "Select a monkey";
    private const string CancelText = "Cancel";
    private const string ClearText = "Clear";
    private const string CloseText = "×";

    public HorizontalAlignStackLayoutDemoPage()
        : base(nameof(SkUiHorizontalAlignStackLayout), new SkUiGrid(), widthRange: (160, 480, 360), heightRange: (72, 160, AlignStackDemo.AreaHeight))
    {
        DocumentTypes(typeof(SkUiLabel), typeof(SkUiButton));
        var area = (SkUiGrid)SkiaControl;
        area.BackgroundColor = Colors.LightGray;
        var title = new SkUiLabel
        {
            Text = TitleText, TextColor = Ink, FontSize = 16, FontAttributes = FontAttributes.Bold, MaxLines = 1,
            LineBreakMode = LineBreakMode.TailTruncation, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
        };
        var cancel = Button(CancelText, LayoutOptions.Start);
        var clear = Button(ClearText, LayoutOptions.Start);
        clear.IsVisible = false;
        var close = Button(CloseText, LayoutOptions.End);
        // Listed in any order: the alignment places them.
        var bar = new SkUiHorizontalAlignStackLayout
        {
            Spacing = 8, Padding = new Thickness(8, 0), BackgroundColor = Colors.White, HeightRequest = AlignStackDemo.BarHeight,
            VerticalOptions = LayoutOptions.Center, Children = { title, close, cancel, clear }
        };
        area.Children.Add(bar);
        area.Children.Add(new SkUiBox
        {
            Color = AlignStackDemo.MarkerColor, WidthRequest = 1, HeightRequest = AlignStackDemo.MarkerHeight,
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, InputTransparent = true
        });
        area.Children.Add(new SkUiLabel
        {
            Text = AlignStackDemo.MarkerText, TextColor = AlignStackDemo.MarkerColor, FontSize = AlignStackDemo.MarkerFontSize,
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.End, Margin = new Thickness(0, 0, 0, 4),
            InputTransparent = true
        });

        Choice(nameof(SkUiHorizontalAlignStackLayout.CenterStrategy), Enum.GetValues<SkUiCenterStrategy>(), SkUiCenterStrategy.Parent,
            value => bar.CenterStrategy = value, () => bar.CenterStrategy);
        Text("Title.Text", TitleText, value => title.Text = value, () => title.Text ?? string.Empty);
        // Longer or shorter side buttons: the title stays centered while it fits, then moves aside.
        Text("Cancel.Text", CancelText, value => cancel.Text = value, () => cancel.Text ?? string.Empty);
        Text("Clear.Text", ClearText, value => clear.Text = value, () => clear.Text ?? string.Empty);
        Text("Close.Text", CloseText, value => close.Text = value, () => close.Text ?? string.Empty);
        Toggle("Cancel.IsVisible", true, value => cancel.IsVisible = value, () => cancel.IsVisible);
        Toggle("Clear.IsVisible", false, value => clear.IsVisible = value, () => clear.IsVisible);
        Toggle("Close.IsVisible", true, value => close.IsVisible = value, () => close.IsVisible);
        Number(nameof(SkUiHorizontalAlignStackLayout.Spacing), 0, 24, 8, value => bar.Spacing = value, () => bar.Spacing);
    }

    private static SkUiButton Button(string text, LayoutOptions alignment) => new()
    {
        Text = text, FontSize = 14, Padding = new Thickness(8, 4), HorizontalOptions = alignment, VerticalOptions = LayoutOptions.Center
    };
}

/// <summary>What the align stack demo pages share: the bar's size and the marker of the layout's center under it.</summary>
internal static class AlignStackDemo
{
    public const string MarkerText = "Layout center";
    public const double AreaHeight = 96;
    public const double BarHeight = 44;
    /// <summary>A little taller than the bar, so the line reads as a marker over it rather than a stray pixel column.</summary>
    public const double MarkerHeight = BarHeight + 12;
    public const double MarkerFontSize = 11;
    public static readonly Color MarkerColor = Color.FromArgb("#DC2626");
}
