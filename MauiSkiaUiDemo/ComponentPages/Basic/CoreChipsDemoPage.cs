using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiCoreChip"/> and <see cref="SkUiCoreChipGroup"/> (1.18 K): the chips on Core nodes, drawn by the same chrome and look
/// as the SkUi chips: a leave-type group (one), a filter group (several) and a badged chip.
/// </summary>
public sealed class CoreChipsDemoPage : ComponentDemoPage
{
    public CoreChipsDemoPage() : base(nameof(SkUiCoreChipGroup), CreateSample(out var types, out var filters))
    {
        types.SelectionChanged += (_, args) => Feedback($"Type: {args.CurrentItem ?? "none"}");
        filters.SelectionChanged += (_, args) => Feedback($"Filters: {string.Join(", ", args.CurrentSelection)}");
        Toggle("Filters wrap", true, value => filters.SetWraps(value), () => filters.Wraps);
        Choice("Filter kind", Enum.GetValues<SkUiChipKind>(), SkUiChipKind.Filter, value => filters.SetChipKind(value), () => filters.ChipKind);
    }

    internal static SkUiCoreHost CreateSample(out SkUiCoreChipGroup types, out SkUiCoreChipGroup filters)
    {
        types = new SkUiCoreChipGroup().SetItemsSource(new[] { "Vacation", "Sick", "Unpaid", "Training" }).SetSelectionMode(SkUiChipSelectionMode.Single)
            .SetSelectedItem("Vacation");
        filters = new SkUiCoreChipGroup().SetItemsSource(new[] { "Approved", "Pending", "Rejected", "Cancelled", "Mine" });
        var inbox = new SkUiCoreChip().SetBadgeText("12");
        inbox.SetText("Requests");
        inbox.SetHorizontalAlignment(LayoutAlignment.Start);
        var stack = new SkUiCoreVerticalStackLayout().SetSpacing(16);
        stack.Add(types);
        stack.Add(filters);
        stack.Add(inbox);
        return new SkUiCoreHost().SetContent(stack);
    }
}
