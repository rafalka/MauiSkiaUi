using System.Globalization;
using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// Property playground for <see cref="SkUiNumericEntry{T}"/> on <see cref="decimal"/> values (a price): exact steps, the culture's
/// currency format while not edited, two decimal digits typed at most, and − / + split around the value (Telerik's layout).
/// </summary>
public sealed class TypedNumericEntryDemoPage : ComponentDemoPage
{
    private static readonly decimal[] Increments = [0.01m, 0.05m, 0.25m, 1m];
    private static readonly string[] Cultures = ["en-US", "de-DE", "ja-JP", "fr-CH"];

    public TypedNumericEntryDemoPage() : base("SkUiNumericEntry<decimal>", new SkUiNumericEntry<decimal>
    {
        Minimum = 0, Maximum = 1000, Value = 9.99m, Increment = 0.25m, Format = "C2", MaximumDecimalDigits = 2, AllowNull = false,
        ButtonPlacement = SkUiStepButtonPlacement.Split, HorizontalTextAlignment = TextAlignment.Center, Culture = CultureInfo.GetCultureInfo("en-US")
    })
    {
        var skia = (SkUiNumericEntry<decimal>)SkiaControl;
        skia.ValueChanged += (_, args) => Feedback($"Value {args.NewValue}m, text \"{skia.Text}\"");
        Number(nameof(SkUiNumericEntry<decimal>.Value), 0, 100, 10, value => skia.Value = (decimal)value, () => (double)(skia.Value ?? 0), whole: true);
        Choice(nameof(SkUiNumericEntry<decimal>.Increment), Increments, 0.25m, value => skia.Increment = value, () => skia.Increment);
        Choice(nameof(SkUiNumericEntry<decimal>.Culture), Cultures, "en-US", value => skia.Culture = CultureInfo.GetCultureInfo(value),
            () => skia.Culture?.Name ?? string.Empty);
        Choice(nameof(SkUiNumericEntry<decimal>.ButtonOrientation), [StackOrientation.Horizontal, StackOrientation.Vertical], StackOrientation.Horizontal,
            value => skia.ButtonOrientation = value, () => skia.ButtonOrientation);
        Toggle(nameof(SkUiNumericEntry<decimal>.AutoReverse), false, value => skia.AutoReverse = value, () => skia.AutoReverse);
    }
}
