using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiSearchBar"/> and MAUI's <see cref="SearchBar"/>.</summary>
public sealed class SearchBarDemoPage : ComponentDemoPage
{
    private const string Placeholder = "Search";

    public SearchBarDemoPage() : base(nameof(SkUiSearchBar), new SkUiSearchBar { Placeholder = Placeholder, WidthRequest = 240 },
        new SearchBar { Placeholder = Placeholder, WidthRequest = 240 })
    {
        var skia = (SkUiSearchBar)SkiaControl;
        var native = (SearchBar)NativeControl!;
        skia.SearchButtonPressed += (_, _) => Feedback($"Searched \"{skia.Text}\"");
        native.SearchButtonPressed += (_, _) => Feedback($"Text \"{skia.Text}\"", $"Searched \"{native.Text}\"");
        skia.TextChanged += (_, args) => Feedback($"Text \"{args.NewTextValue}\"", $"Text \"{native.Text}\"");
        Text(nameof(SkUiSearchBar.Text), string.Empty, value => { skia.Text = value; native.Text = value; }, () => skia.Text ?? string.Empty,
            () => native.Text ?? string.Empty);
        Text(nameof(SkUiSearchBar.Placeholder), Placeholder, value => { skia.Placeholder = value; native.Placeholder = value; }, () => skia.Placeholder ?? string.Empty,
            () => native.Placeholder ?? string.Empty);
        Toggle(nameof(SkUiSearchBar.IsReadOnly), false, value => { skia.IsReadOnly = value; native.IsReadOnly = value; }, () => skia.IsReadOnly, () => native.IsReadOnly);
        ColorEditor(nameof(SkUiSearchBar.CancelButtonColor), SkUiColors.Muted, value => { skia.CancelButtonColor = value; native.CancelButtonColor = value; },
            () => skia.CancelButtonColor ?? SkUiColors.Muted, () => native.CancelButtonColor ?? SkUiColors.Muted);
        ColorEditor(nameof(SkUiSearchBar.TextColor), Ink, value => { skia.TextColor = value; native.TextColor = value; }, () => skia.TextColor ?? Ink, () => native.TextColor ?? Ink);
    }
}
