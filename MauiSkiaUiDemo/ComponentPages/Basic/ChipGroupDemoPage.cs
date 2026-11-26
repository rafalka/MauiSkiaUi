using System.Collections.ObjectModel;
using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiChipGroup"/> (1.18 K, #130): a filter group over request states, recipients as input chips removed with their ×,
/// and a single-choice row that scrolls with edge padding.
/// </summary>
public sealed class ChipGroupDemoPage : ComponentDemoPage
{
    private const string AddRecipient = "Add recipient";

    public ChipGroupDemoPage() : base(nameof(SkUiChipGroup), CreateSample(out var filters, out var recipients, out var period, out var names))
    {
        filters.SelectionChanged += (_, args) => Feedback($"Filters: {string.Join(", ", args.CurrentSelection)}");
        period.SelectionChanged += (_, args) => Feedback($"Period: {args.CurrentItem ?? "none"}");
        recipients.ChipRemoved += (_, args) =>
        {
            names.Remove((string)args.Item!);
            Feedback($"Removed {args.Item}");
        };
        Toggle(nameof(SkUiChipGroup.Wraps), true, value => filters.Wraps = value, () => filters.Wraps);
        Choice(nameof(SkUiChipGroup.SelectionMode), Enum.GetValues<SkUiChipSelectionMode>(), SkUiChipSelectionMode.Multiple, value => filters.SelectionMode = value,
            () => filters.SelectionMode);
        Choice(nameof(SkUiChipGroup.ChipKind), Enum.GetValues<SkUiChipKind>(), SkUiChipKind.Filter, value => filters.ChipKind = value, () => filters.ChipKind);
        Number(nameof(SkUiChipGroup.Spacing), 0, 24, 8, value => filters.Spacing = value, () => filters.Spacing);
        AddEditor("Recipients (input chips, ×)", recipients, property: false);
        ActionButton(AddRecipient, () => names.Add($"Person {names.Count + 1}"));
        AddEditor("Period (one, scrolling row)", period, property: false);
    }

    internal static SkUiChipGroup CreateSample(out SkUiChipGroup filters, out SkUiChipGroup recipients, out SkUiChipGroup period, out ObservableCollection<string> names)
    {
        filters = new SkUiChipGroup { ItemsSource = new[] { "Approved", "Pending", "Rejected", "Cancelled", "This week", "Mine" } };
        names = ["Anna", "Ben", "Chiara"];
        recipients = new SkUiChipGroup { ItemsSource = names, ChipKind = SkUiChipKind.Input, SelectionMode = SkUiChipSelectionMode.None };
        period = new SkUiChipGroup
        {
            ItemsSource = new[] { "Today", "Yesterday", "This week", "Last week", "This month", "Last month", "This year" },
            SelectionMode = SkUiChipSelectionMode.Single, Wraps = false, Padding = new Thickness(16, 0), SelectedItem = "This week"
        };
        return filters;
    }
}
