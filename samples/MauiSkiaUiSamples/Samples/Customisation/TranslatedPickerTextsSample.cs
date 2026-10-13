using MauiSkiaUi;

namespace MauiSkiaUiSamples.Samples.Customisation;

/// <summary>
/// HOWTO: translate SkiaUi's own texts. Everything SkiaUi shows or reads out by itself (a popup's OK, Cancel and Clear, the
/// "Select all" row, the "+N" token, a collection view's "Load more", the selected / expanded states screen readers read) is in
/// one object, <see cref="SkUiStrings.Current"/>; replacing it switches them all at once, as <see cref="SkUiLook.Current"/>
/// switches the look. Texts that are the app's (titles, items) stay the app's to translate.
/// <para>
/// The page switches between English, Polish and German: its own texts from a small table, SkiaUi's from three
/// <see cref="SkUiStrings"/> objects. Open the pickers after a switch: their buttons, "Select all" and the token count follow.
/// The app's texts are restored when the page goes.
/// </para>
/// </summary>
public sealed class TranslatedPickerTextsSample : SamplePage, ISample
{
    public static SampleInfo Info { get; } = new(
        SampleSection.Customisation,
        Title: "Translating SkiaUi's texts",
        Summary: "SkiaUi's own texts (OK, Cancel, Clear, Select all, \"+N\", Load more, the states screen readers read) are in one " +
                 "object, SkUiStrings: replace it to switch them all at once.",
        HowTo:
        [
            "Set the texts once at startup: `builder.UseSkiaUi(options => options.Strings = new SkUiStrings { AcceptText = \"Zatwierdź\", … })`.",
            "Switch language at runtime: `SkUiStrings.Current = polish;` (the next popup, live surfaces' screen-reader texts and " +
                "the load-more button follow).",
            "Or read your resources: derive, `public override string AcceptText => AppResources.Ok;`, and call " +
                "`SkUiStrings.NotifyChanged()` after switching `CultureInfo.CurrentUICulture`.",
            "One control differs: set its own `AcceptText`, `CancelText`, `ClearText` or `SelectAllText`."
        ],
        ThingsToKnow:
        [
            "A popup reads the texts when it opens; an open popup keeps them.",
            "Change the texts on the UI thread.",
            "SkiaUi has a handful of texts only; everything the user reads besides them is the app's.",
            "Setting a text on the current `SkUiStrings` applies like replacing it (it raises `SkUiStrings.CurrentChanged`)."
        ]);

    private const string English = "English";
    private const string Polish = "Polski";
    private const string German = "Deutsch";

    /// <summary>SkiaUi's texts per language (English: the defaults).</summary>
    private static readonly Dictionary<string, SkUiStrings> Strings = new()
    {
        [English] = new SkUiStrings(),
        [Polish] = new SkUiStrings
        {
            AcceptText = "Zatwierdź", CancelText = "Anuluj", ClearText = "Wyczyść", SelectAllText = "Zaznacz wszystko",
            MoreTokensFormat = "i {0} więcej", LoadMoreText = "Wczytaj więcej", SelectedStateText = "Zaznaczone",
            ExpandedStateText = "Rozwinięte", CollapsedStateText = "Zwinięte"
        },
        [German] = new SkUiStrings
        {
            AcceptText = "Übernehmen", CancelText = "Abbrechen", ClearText = "Leeren", SelectAllText = "Alle auswählen",
            MoreTokensFormat = "+{0} weitere", LoadMoreText = "Mehr laden", SelectedStateText = "Ausgewählt",
            ExpandedStateText = "Erweitert", CollapsedStateText = "Reduziert"
        }
    };

    /// <summary>The page's own texts per language: the pickers' titles and items.</summary>
    private static readonly Dictionary<string, (string Fruits, string Colors, string[] FruitNames, string[] ColorNames)> AppTexts = new()
    {
        [English] = ("Fruits", "Colour", ["Apple", "Pear", "Plum", "Cherry", "Peach", "Apricot"], ["Red", "Green", "Blue", "Yellow"]),
        [Polish] = ("Owoce", "Kolor", ["Jabłko", "Gruszka", "Śliwka", "Wiśnia", "Brzoskwinia", "Morela"], ["Czerwony", "Zielony", "Niebieski", "Żółty"]),
        [German] = ("Obst", "Farbe", ["Apfel", "Birne", "Pflaume", "Kirsche", "Pfirsich", "Aprikose"], ["Rot", "Grün", "Blau", "Gelb"])
    };

    private readonly SkUiStrings _appStrings = SkUiStrings.Current;
    private readonly SkUiPicker _fruits = new()
    {
        SelectionMode = SkUiSelectionMode.Multiple,
        SelectedItemsDisplay = SkUiSelectedItemsDisplay.Tokens,
        MaxTokenLines = 1,
        IsSelectAllVisible = true,
        IsPopupClearButtonVisible = true,
        UpdateMode = SkUiPickerUpdateMode.WhenFinished,
        PopupPlacement = SkUiPopupPlacement.Window,
        TextColor = SampleColors.Ink
    };
    private readonly SkUiPicker _colour = new()
    {
        UpdateMode = SkUiPickerUpdateMode.WhenFinished,
        IsPopupClearButtonVisible = true,
        PopupPlacement = SkUiPopupPlacement.Window,
        TextColor = SampleColors.Ink
    };
    private string _language = English;
    private bool _shown;

    public TranslatedPickerTextsSample() : base(Info)
    {
        var languages = new SkUiHorizontalStackLayout { Spacing = 16 };
        foreach (var language in new[] { English, Polish, German })
        {
            var option = new SkUiRadioButton { Content = language, GroupName = "language", IsChecked = language == English, TextColor = SampleColors.Ink };
            option.CheckedChanged += (_, args) =>
            {
                if (args.Value)
                    Switch(language);
            };
            languages.Children.Add(option);
        }
        SampleContent = new SkUiVerticalStackLayout
        {
            Spacing = 12,
            Padding = new Thickness(16),
            BackgroundColor = SampleColors.Surface,
            Children = { languages, _fruits, _colour }
        };
        Switch(English);
    }

    /// <summary>The page's texts and SkiaUi's, in <paramref name="language"/>.</summary>
    private void Switch(string language)
    {
        _language = language;
        var texts = AppTexts[language];
        _fruits.Title = texts.Fruits;
        _fruits.ItemsSource = texts.FruitNames;
        foreach (var name in texts.FruitNames.Take(4))
            _fruits.SelectedItems.Add(name);
        _colour.Title = texts.Colors;
        _colour.ItemsSource = texts.ColorNames;
        _colour.SelectedIndex = 2;
        // SkiaUi's texts, all at once (while the page shows: they are app-wide).
        if (_shown)
            SkUiStrings.Current = Strings[language];
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _shown = true;
        SkUiStrings.Current = Strings[_language];
    }

    /// <summary>An example's language is the page's only: the app's texts come back when it goes.</summary>
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _shown = false;
        SkUiStrings.Current = _appStrings;
    }
}
