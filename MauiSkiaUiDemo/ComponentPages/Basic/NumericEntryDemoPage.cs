using System.Globalization;
using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// Property playground for <see cref="SkUiNumericEntry"/> (D16: numbers only, − / + buttons in the field, formatted when not edited)
/// next to what MAUI apps use instead: an <see cref="Entry"/> with <c>Keyboard.Numeric</c>, which takes any text the keyboard gives.
/// </summary>
public sealed class NumericEntryDemoPage : ComponentDemoPage
{
    private const string Placeholder = "Amount";
    private const string NoFormat = "None";
    private const string CurrentCulture = "Current";
    private const string AnyDigits = "Any";
    private const string NoText = "None";
    private const string Words = "Less / More";
    private const string Chevrons = "‹ / ›";
    private static readonly string[] Formats = [NoFormat, "N2", "C2", "0.## kg", "P0"];
    private static readonly string[] Cultures = [CurrentCulture, "en-US", "de-DE", "fr-FR", "ar-SA"];
    private static readonly string[] Digits = [AnyDigits, "0", "1", "2", "4"];
    private static readonly double[] Increments = [0.1, 0.5, 1, 5, 10];

    public NumericEntryDemoPage() : base(nameof(SkUiNumericEntry), new SkUiNumericEntry { Placeholder = Placeholder, ButtonPlacement = SkUiStepButtonPlacement.End },
        new Entry { Placeholder = Placeholder, Keyboard = Keyboard.Numeric })
    {
        var skia = (SkUiNumericEntry)SkiaControl;
        var native = (Entry)NativeControl!;
        skia.ValueChanged += (_, args) => Feedback($"Value {Show(args.NewValue)} (was {Show(args.OldValue)}), text \"{skia.Text}\"", $"Text \"{native.Text}\"");
        skia.Completed += (_, _) => Feedback($"Completed: {Show(skia.Value)}");
        native.TextChanged += (_, args) => Feedback($"Value {Show(skia.Value)}", $"Text \"{args.NewTextValue}\"");
        Number(nameof(SkUiNumericEntry.Minimum), -100, 0, -100, value => skia.Minimum = value, () => skia.Minimum, whole: true);
        Number(nameof(SkUiNumericEntry.Maximum), 0, 100, 100, value => skia.Maximum = value, () => skia.Maximum, whole: true);
        Number(nameof(SkUiNumericEntry.Value), -100, 100, 25, value => skia.Value = value, () => skia.Value ?? 0, whole: true);
        Choice(nameof(SkUiNumericEntry.Increment), Increments, 1, value => skia.Increment = value, () => skia.Increment);
        Choice(nameof(SkUiNumericEntry.ButtonPlacement), Enum.GetValues<SkUiStepButtonPlacement>(), SkUiStepButtonPlacement.End,
            value => skia.ButtonPlacement = value, () => skia.ButtonPlacement);
        Choice(nameof(SkUiNumericEntry.ButtonOrientation), [StackOrientation.Horizontal, StackOrientation.Vertical], StackOrientation.Horizontal,
            value => skia.ButtonOrientation = value, () => skia.ButtonOrientation);
        Choice(nameof(SkUiNumericEntry.Format), Formats, NoFormat, value => skia.Format = value == NoFormat ? null : value, () => skia.Format ?? NoFormat);
        Choice(nameof(SkUiNumericEntry.Culture), Cultures, CurrentCulture,
            value => skia.Culture = value == CurrentCulture ? null : CultureInfo.GetCultureInfo(value), () => skia.Culture?.Name ?? CurrentCulture);
        Choice(nameof(SkUiNumericEntry.MaximumDecimalDigits), Digits, AnyDigits,
            value => skia.MaximumDecimalDigits = value == AnyDigits ? null : int.Parse(value, CultureInfo.InvariantCulture),
            () => skia.MaximumDecimalDigits?.ToString(CultureInfo.InvariantCulture) ?? AnyDigits);
        Choice(nameof(SkUiNumericEntry.ValueChangeMode), Enum.GetValues<SkUiValueChangeMode>(), SkUiValueChangeMode.OnCompleted,
            value => skia.ValueChangeMode = value, () => skia.ValueChangeMode);
        Toggle(nameof(SkUiNumericEntry.AllowNull), true, value => skia.AllowNull = value, () => skia.AllowNull);
        Toggle(nameof(SkUiNumericEntry.IsEditable), true, value => skia.IsEditable = value, () => skia.IsEditable);
        Toggle(nameof(SkUiNumericEntry.AutoReverse), false, value => skia.AutoReverse = value, () => skia.AutoReverse);
        Toggle(nameof(SkUiNumericEntry.IsReadOnly), false, value => { skia.IsReadOnly = value; native.IsReadOnly = value; }, () => skia.IsReadOnly, () => native.IsReadOnly);
        var texts = NoText;
        Choice("Button texts", [NoText, Words, Chevrons], NoText, value =>
        {
            texts = value;
            (skia.DecrementText, skia.IncrementText) = value switch
            {
                Words => ("Less", "More"),
                Chevrons => ("‹", "›"),
                _ => ((string?)null, (string?)null)
            };
        }, () => texts);
        Choice(nameof(SkUiNumericEntry.HorizontalTextAlignment), [TextAlignment.Start, TextAlignment.Center, TextAlignment.End], TextAlignment.Start,
            value => { skia.HorizontalTextAlignment = value; native.HorizontalTextAlignment = value; }, () => skia.HorizontalTextAlignment, () => native.HorizontalTextAlignment);
        Number(nameof(SkUiNumericEntry.FontSize), 10, 32, 16, value => { skia.FontSize = value; native.FontSize = value; }, () => skia.FontSize, () => native.FontSize);
        ColorEditor(nameof(SkUiNumericEntry.ButtonColor), Ink, value => skia.ButtonColor = value, () => skia.ButtonColor ?? Ink);
        ActionButton("Clear value", () => skia.Value = null);
        ActionButton("Focus", () => skia.Focus());
    }

    private static string Show(double? value) => value?.ToString(CultureInfo.CurrentCulture) ?? "null";
}
