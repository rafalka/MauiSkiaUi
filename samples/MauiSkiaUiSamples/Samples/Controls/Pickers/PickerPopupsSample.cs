using MauiSkiaUi;
using MauiSkiaUi.CommunityToolkit;

namespace MauiSkiaUiSamples.Samples.Controls.Pickers;

/// <summary>
/// HOWTO: drawn pickers whose lists open on your app's popup system. <see cref="SkUiPicker"/> draws its field and its list;
/// the list is shown by an <see cref="ISkUiPopupPresenter"/>: here CommunityToolkit.Maui's popup
/// (<see cref="SkUiToolkitPopupPresenter"/>, from the SkiaUi.Maui.CommunityToolkit package), or MAUI's action sheet
/// (<see cref="SkUiActionSheetPopupPresenter"/>, the fallback when nothing is registered).
/// <para>
/// The page shows a picker anchored to its field as a combo box, one whose list opens in the window as a dialog with a title,
/// one docked to the window's bottom edge as iOS's picker, one with drawn rows (an item template over objects), one on MAUI's
/// action sheet, and what was picked. The samples app
/// registers the toolkit's presenter once (<c>UseSkiaUiCommunityToolkit()</c> in MauiProgram); the pickers here also set it
/// per picker, to show that option, and the T-shirt size picker uses MAUI's action sheet instead.
/// </para>
/// </summary>
public sealed class PickerPopupsSample : SamplePage, ISample
{
    public static SampleInfo Info { get; } = new(
        SampleSection.Controls,
        Title: "Pickers on your popup system",
        Summary: "Drawn pickers (MAUI's Picker API) whose lists open in CommunityToolkit's popup: anchored as a combo box, as a dialog, docked at the bottom as iOS's picker, with drawn rows, or on MAUI's action sheet.",
        HowTo:
        [
            "With CommunityToolkit.Maui: add the `SkiaUi.Maui.CommunityToolkit` package and call " +
                "`builder.UseMauiCommunityToolkit().UseSkiaUi().UseSkiaUiCommunityToolkit()`; or per picker, `PopupPresenter = SkUiToolkitPopupPresenter.Instance`.",
            "With another popup system, write a presenter: `ShowAsync(request, token)` shows `request.Content` at `request.Bounds` " +
                "(or aligned by `request.HorizontalOptions` / `VerticalOptions`), closes it when the token is cancelled, and returns once it is gone. " +
                "Register it with `builder.UseSkiaUi(options => options.PopupPresenter = …)`.",
            "Use the picker as MAUI's: `<sk:SkUiPicker Title=\"Fruit\" ItemsSource=\"{Binding Fruits}\" SelectedItem=\"{Binding Fruit}\" />`.",
            "Choose where the list opens: `PopupPlacement=\"Anchored\"` (a combo box), `\"Window\"` (a centered dialog), " +
                "`\"BottomPane\"` (docked to the window's bottom edge with a toolbar and a close button, as iOS's picker), or leave `Default` for the look's choice per platform.",
            "Draw rows of your own: an `ItemTemplate` of `SkUi*` views bound to your items; `ItemDisplayBinding` still names the field's text."
        ],
        ThingsToKnow:
        [
            "SkiaUi has no popup windows of its own: without a presenter, pickers open MAUI's action sheet (plain texts, MAUI's UI).",
            "A presenter that shows its own UI instead (a vendor picker) reads `SkUiListPopupRequest` (`Items`, `SelectedIndex`) " +
                "and calls `request.Complete(index)`.",
            "An anchored list closes when the page scrolls or the window resizes, as a combo box does; Escape and a tap outside close it too.",
            "The bottom pane is a wheel with the iOS look (it chooses as it turns; the × takes the item it shows) and a list with the others.",
            "Keyboard: arrow keys change the selection of the closed field, Alt+Down opens it; in the list, arrows, Home / End, " +
                "typing and Enter choose.",
            "The request lets go of the picker once the popup has gone: a presenter that keeps requests (a popup stack) leaks nothing."
        ]);

    /// <summary>A product with a color swatch, for the templated picker.</summary>
    public sealed record Paint(string Name, Color Color);

    private static readonly string[] Fruits = ["Apple", "Apricot", "Banana", "Blueberry", "Cherry", "Grape", "Kiwi", "Lemon", "Mango", "Orange", "Peach", "Pear", "Plum"];

    private static readonly string[] Seasons = ["Spring", "Summer", "Autumn", "Winter"];

    private static readonly Paint[] Paints =
    [
        new("Teal", Color.FromArgb("#087F83")), new("Coral", Color.FromArgb("#F2665C")), new("Sun", Color.FromArgb("#F2B33D")),
        new("Leaf", Color.FromArgb("#4C9A2A")), new("Night", Color.FromArgb("#2D3A4A")), new("Plum", Color.FromArgb("#7B4B94"))
    ];

    private readonly SkUiLabel _summary = new() { FontSize = 14, TextColor = SampleColors.Caption };
    private readonly SkUiPicker _fruit;
    private readonly SkUiPicker _country;
    private readonly SkUiPicker _season;
    private readonly SkUiPicker _paint;
    private readonly SkUiPicker _size;

    public PickerPopupsSample() : base(Info)
    {
        _fruit = new SkUiPicker { Title = "Pick a fruit", ItemsSource = Fruits, PopupPlacement = SkUiPopupPlacement.Anchored, MaxPopupHeight = 280 };
        _country = new SkUiPicker { Title = "Choose a country", ItemsSource = Countries(), PopupPlacement = SkUiPopupPlacement.Window, MaxPopupHeight = 480 };
        _season = new SkUiPicker { Title = "Season", ItemsSource = Seasons, PopupPlacement = SkUiPopupPlacement.BottomPane };
        _paint = new SkUiPicker
        {
            Title = "Pick a paint",
            ItemsSource = Paints,
            ItemDisplayBinding = Binding.Create(static (Paint paint) => paint.Name),
            ItemTemplate = new DataTemplate(PaintRow),
            PopupPlacement = SkUiPopupPlacement.Anchored
        };
        _size = new SkUiPicker { Title = "T-shirt size", ItemsSource = new[] { "S", "M", "L", "XL" }, PopupPresenter = SkUiActionSheetPopupPresenter.Instance };
        foreach (var picker in new[] { _fruit, _country, _season, _paint })
            picker.PopupPresenter = SkUiToolkitPopupPresenter.Instance;
        foreach (var picker in new[] { _fruit, _country, _season, _paint, _size })
        {
            picker.TextColor = SampleColors.Ink;
            picker.SelectedIndexChanged += (_, _) => UpdateSummary();
        }
        SampleContent = Build();
        UpdateSummary();
    }

    private View Build()
    {
        var column = new SkUiVerticalStackLayout { Spacing = 10, Padding = new Thickness(16), BackgroundColor = SampleColors.Surface };
        column.Children.Add(Heading("Anchored (a combo box)"));
        column.Children.Add(_fruit);
        column.Children.Add(Heading("In the window (a dialog)"));
        column.Children.Add(_country);
        column.Children.Add(Heading("Docked at the bottom (iOS's picker)"));
        column.Children.Add(_season);
        column.Children.Add(Heading("Drawn rows"));
        column.Children.Add(_paint);
        column.Children.Add(Heading("MAUI's action sheet"));
        column.Children.Add(_size);
        column.Children.Add(_summary);
        return column;
    }

    /// <summary>A row of the paint list: a swatch and the name, bound to a <see cref="Paint"/>.</summary>
    private static SkUiHorizontalStackLayout PaintRow()
    {
        var swatch = new SkUiBox { WidthRequest = 20, HeightRequest = 20, CornerRadius = new CornerRadius(10), VerticalOptions = LayoutOptions.Center };
        swatch.SetBinding(SkUiBox.ColorProperty, static (Paint paint) => paint.Color);
        var name = new SkUiLabel { FontSize = 16, TextColor = SampleColors.Ink, VerticalOptions = LayoutOptions.Center };
        name.SetBinding(SkUiLabel.TextProperty, static (Paint paint) => paint.Name);
        return new SkUiHorizontalStackLayout { Spacing = 12, Padding = new Thickness(16, 10), Children = { swatch, name } };
    }

    /// <summary>A long list: the countries of the cultures this device knows, once each, by their English names.</summary>
    private static List<string> Countries()
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var culture in System.Globalization.CultureInfo.GetCultures(System.Globalization.CultureTypes.SpecificCultures))
        {
            try
            {
                // Countries only: not continents ("003", North America).
                var region = new System.Globalization.RegionInfo(culture.Name);
                if (region.TwoLetterISORegionName is { Length: 2 } code && code.All(char.IsLetter))
                    names.TryAdd(code, region.EnglishName);
            }
            catch (ArgumentException)
            {
                // A culture without a region.
            }
        }
        return names.Count > 0 ? [.. names.Values.Order(StringComparer.CurrentCulture)] : [.. Fruits];
    }

    private void UpdateSummary() =>
        _summary.Text = $"{_fruit.SelectedItem ?? "No fruit"} · {_country.SelectedItem ?? "no country"} · {_season.SelectedItem ?? "no season"} · {(_paint.SelectedItem as Paint)?.Name ?? "no paint"} · size {_size.SelectedItem ?? "–"}";

    private static SkUiLabel Heading(string text) =>
        new() { Text = text, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = SampleColors.Caption, TextTransform = TextTransform.Uppercase };
}
