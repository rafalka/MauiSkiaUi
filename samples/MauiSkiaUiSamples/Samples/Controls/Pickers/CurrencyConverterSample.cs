using System.Globalization;
using MauiSkiaUi;

namespace MauiSkiaUiSamples.Samples.Controls.Pickers;

/// <summary>
/// HOWTO: narrow pickers whose lists are wider than their fields. Each currency picker shows the selected currency's code in
/// its field ("PLN": <see cref="SkUiPicker.SelectedItemDisplayBinding"/>) and its full name in the list ("Polish Zloty (PLN)":
/// <see cref="SkUiPicker.ItemDisplayBinding"/>). An anchored list is as wide as its widest item, not limited to its field, and
/// <see cref="SkUiPickerBase.MaxPopupWidth"/> caps it (longer names are cut with an ellipsis).
/// <para>
/// The page converts an amount (a slider) between two currencies at fixed example rates; the arrow button swaps them, and
/// type-ahead finds a currency by its name ("s" for the Swiss franc) on the focused field or in the open list.
/// </para>
/// </summary>
public sealed class CurrencyConverterSample : SamplePage, ISample
{
    public static SampleInfo Info { get; } = new(
        SampleSection.Controls,
        Title: "Currency converter (narrow pickers)",
        Summary: "Narrow pickers that show a currency's code while their anchored lists show the full names, as wide as the names need, within a maximum.",
        HowTo:
        [
            "Name the list's text and the field's text apart: `ItemDisplayBinding=\"{Binding Name}\"` and " +
                "`SelectedItemDisplayBinding=\"{Binding Code}\"` (in code, `Binding.Create(static (Currency c) => c.Code)`).",
            "Let the field be as narrow as its text: `HorizontalOptions=\"Start\"`; open the list at it with `PopupPlacement=\"Anchored\"`.",
            "Cap the list's width with `MaxPopupWidth` (and its height with `MaxPopupHeight`); a list is as wide as its widest item " +
                "within `MinPopupWidth` and `MaxPopupWidth`, or exactly `PopupWidth`.",
            "Swap two pickers by swapping their `SelectedItem`s."
        ],
        ThingsToKnow:
        [
            "Without `MinPopupWidth` an anchored list is at least as wide as its field; with it, the minimum replaces the field's width.",
            "The list's width comes from the texts of its first 200 items (or the first templated row), so measuring a long list stays cheap.",
            "Type-ahead and screen readers use the item's full text, not the field's short one.",
            "The rates are examples, not real ones."
        ]);

    /// <summary>A currency: its code, full name and how many of it make one euro (an example rate).</summary>
    public sealed record Currency(string Code, string Name, decimal PerEuro);

    private static readonly Currency[] Currencies =
    [
        new("EUR", "Euro (EUR)", 1m), new("PLN", "Polish Zloty (PLN)", 4.30m), new("USD", "United States Dollar (USD)", 1.08m),
        new("GBP", "Pound Sterling (GBP)", 0.85m), new("CHF", "Swiss Franc (CHF)", 0.96m), new("CZK", "Czech Koruna (CZK)", 25.2m),
        new("SEK", "Swedish Krona (SEK)", 11.4m), new("JPY", "Japanese Yen (JPY)", 162m), new("NOK", "Norwegian Krone (NOK)", 11.6m),
        new("HUF", "Hungarian Forint (HUF)", 390m), new("CAD", "Canadian Dollar (CAD)", 1.47m), new("AUD", "Australian Dollar (AUD)", 1.64m)
    ];

    private readonly SkUiPicker _from = CurrencyPicker(1);
    private readonly SkUiPicker _to = CurrencyPicker(0);
    private readonly SkUiSlider _amount = new() { Minimum = 0, Maximum = 1000, Value = 250 };
    private readonly SkUiLabel _amountText = new() { FontSize = 15, TextColor = SampleColors.Ink };
    private readonly SkUiLabel _result = new() { FontSize = 28, FontAttributes = FontAttributes.Bold, TextColor = SampleColors.Ink };
    private readonly SkUiLabel _rate = new() { FontSize = 13, TextColor = SampleColors.Caption };

    public CurrencyConverterSample() : base(Info)
    {
        _from.SelectedIndexChanged += (_, _) => Convert();
        _to.SelectedIndexChanged += (_, _) => Convert();
        _amount.ValueChanged += (_, _) => Convert();
        SampleContent = Build();
        Convert();
    }

    /// <summary>A narrow picker: the code in the field, the full names in an anchored list at most 260 wide.</summary>
    private static SkUiPicker CurrencyPicker(int selected) => new()
    {
        ItemsSource = Currencies,
        SelectedIndex = selected,
        ItemDisplayBinding = Binding.Create(static (Currency currency) => currency.Name),
        SelectedItemDisplayBinding = Binding.Create(static (Currency currency) => currency.Code),
        HorizontalOptions = LayoutOptions.Start,
        VerticalOptions = LayoutOptions.Center,
        PopupPlacement = SkUiPopupPlacement.Anchored,
        MaxPopupWidth = 260,
        MaxPopupHeight = 320,
        FontAttributes = FontAttributes.Bold,
        TextColor = SampleColors.Ink
    };

    private View Build()
    {
        var swap = new SkUiButton { Text = "⇄", FontSize = 18, Padding = new Thickness(14, 6), VerticalOptions = LayoutOptions.Center };
        swap.Clicked += (_, _) => (_from.SelectedItem, _to.SelectedItem) = (_to.SelectedItem, _from.SelectedItem);
        var currencies = new SkUiHorizontalStackLayout { Spacing = 12, Children = { _from, swap, _to } };
        return new SkUiVerticalStackLayout
        {
            Spacing = 12,
            Padding = new Thickness(16),
            BackgroundColor = SampleColors.Surface,
            Children = { Heading("Amount"), _amountText, _amount, Heading("From, to"), currencies, _result, _rate }
        };
    }

    private void Convert()
    {
        var amount = Math.Round((decimal)_amount.Value);
        var from = _from.SelectedItem as Currency ?? Currencies[1];
        var to = _to.SelectedItem as Currency ?? Currencies[0];
        var rate = to.PerEuro / from.PerEuro;
        _amountText.Text = $"{amount.ToString("N0", CultureInfo.CurrentCulture)} {from.Code}";
        _result.Text = $"{(amount * rate).ToString("N2", CultureInfo.CurrentCulture)} {to.Code}";
        _rate.Text = $"1 {from.Code} = {rate.ToString("N4", CultureInfo.CurrentCulture)} {to.Code} (an example rate)";
    }

    private static SkUiLabel Heading(string text) =>
        new() { Text = text, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = SampleColors.Caption, TextTransform = TextTransform.Uppercase };
}
