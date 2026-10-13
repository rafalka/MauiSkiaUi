using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUiSamples.Samples.Core;

/// <summary>
/// HOWTO: pickers on the Core layer. <see cref="SkUiCorePicker"/> and <see cref="SkUiCoreMultiColumnPicker"/> are the Core twins of
/// the drawn pickers: the same field, popups, wheels and options, set with fluent <c>Set*</c> calls instead of bindable
/// properties, and functions where the SkiaUi pickers take bindings (<c>ItemDisplay</c>, <c>ItemEnabled</c>,
/// <c>SelectedItemDisplay</c>). They live in a Core tree inside an <see cref="SkUiCoreHost"/>, the MAUI view their popups anchor to.
/// <para>
/// The page builds a small task form in Core nodes: a project picked by its short code (the list shows full names, an
/// archived project cannot be chosen), labels chosen as tokens, and an estimate on two wheels (hours and quarter hours).
/// </para>
/// </summary>
public sealed class CorePickersSample : SamplePage, ISample
{
    public static SampleInfo Info { get; } = new(
        SampleSection.Core,
        Title: "Pickers in a Core tree",
        Summary: "SkUiCorePicker and SkUiCoreMultiColumnPicker in a Core form: a project shown by its code, labels as tokens, and an " +
                 "estimate on wheels, set up with fluent calls and functions instead of bindings.",
        HowTo:
        [
            "Build the pickers fluently: `new SkUiCorePicker().SetTitle(\"Project\").SetItemsSource(projects).SetItemDisplay(p => ((Project)p!).Name)`.",
            "Short field text and items that cannot be chosen: `SetSelectedItemDisplay(p => ((Project)p!).Code)`, " +
                "`SetItemEnabled(p => !((Project)p!).IsArchived)`.",
            "Several choices as tokens: `SetSelectionMode(SkUiSelectionMode.Multiple).SetSelectedItemsDisplay(SkUiSelectedItemsDisplay.Tokens)`; " +
                "choose from code with `SetSelectedItems([...])`.",
            "Wheels: `new SkUiCoreMultiColumnPicker().AddColumn(SkUiPickerColumn.Range(0, 12).SetHeader(\"Hours\"))`.",
            "Host the tree: `new SkUiCoreHost().SetContent(form)`; popups anchor to the host."
        ],
        ThingsToKnow:
        [
            "Core pickers raise the same events (`SelectedIndexChanged`, `SelectionChanged`) and `INotifyPropertyChanged` for every property.",
            "Columns (`SkUiPickerColumn`) are shared with the SkiaUi multi-column picker; they are bindable objects, Core or not.",
            "The app's popup views (`SetPopupHeaderTemplate`) work on Core pickers (they are drawn in the popup's own surface); a field " +
                "template (`SelectedItemTemplate`) is the SkiaUi picker's only.",
            "Core nodes have no enabled state: hide or remove a picker to stop it."
        ]);

    /// <summary>A project: its short code, full name, and whether it is archived (then it cannot be chosen).</summary>
    public sealed record Project(string Code, string Name, bool IsArchived = false);

    private static readonly Project[] Projects =
    [
        new("WEB", "Website relaunch"), new("APP", "Mobile app 3.0"), new("OPS", "Operations and support"),
        new("LEG", "Legacy billing", IsArchived: true), new("DOC", "Documentation portal")
    ];

    private static readonly string[] Labels = ["bug", "feature", "design", "backend", "frontend", "urgent", "research", "chore"];

    private readonly SkUiCorePicker _project;
    private readonly SkUiCorePicker _labels;
    private readonly SkUiCoreMultiColumnPicker _estimate;
    private readonly SkUiCoreLabel _summary = new SkUiCoreLabel().SetFontSize(14).SetTextColor(SampleColors.Caption);

    public CorePickersSample() : base(Info)
    {
        _project = new SkUiCorePicker()
            .SetTitle("Project")
            .SetItemsSource(Projects)
            .SetItemDisplay(project => ((Project)project!).Name)
            .SetSelectedItemDisplay(project => ((Project)project!).Code)
            .SetItemEnabled(project => !((Project)project!).IsArchived)
            .SetPopupPlacement(SkUiPopupPlacement.Anchored)
            .SetMaxPopupWidth(280)
            .SetSelectedIndex(0)
            .SetTextColor(SampleColors.Ink);
        _project.SetHorizontalAlignment(LayoutAlignment.Start);
        _labels = new SkUiCorePicker()
            .SetTitle("Labels")
            .SetPlaceholder("Add labels")
            .SetItemsSource(Labels)
            .SetSelectionMode(SkUiSelectionMode.Multiple)
            .SetSelectedItemsDisplay(SkUiSelectedItemsDisplay.Tokens)
            .SetIsSelectAllVisible(true)
            .SetIsClearButtonVisible(true)
            .SetPopupPlacement(SkUiPopupPlacement.Anchored)
            .SetSelectedItems(["feature", "frontend"]);
        _estimate = new SkUiCoreMultiColumnPicker()
            .SetTitle("Estimate")
            .SetFormat("{0} h {1} min")
            .SetUpdateMode(SkUiPickerUpdateMode.WhenFinished)
            .SetPopupPlacement(SkUiPopupPlacement.Window)
            .SetMinPopupWidth(220)
            .SetTextColor(SampleColors.Ink)
            .AddColumn(SkUiPickerColumn.Range(0, 12).SetHeader("Hours"))
            .AddColumn(SkUiPickerColumn.Range(0, 45, 15, "00").SetHeader("Minutes"));
        _estimate.Columns[0].SelectedItem = 2;
        _estimate.Columns[1].SelectedItem = 30;
        _project.SelectedIndexChanged += (_, _) => UpdateSummary();
        _labels.SelectionChanged += (_, _) => UpdateSummary();
        _estimate.SelectionChanged += (_, _) => UpdateSummary();

        var form = new SkUiCoreVerticalStackLayout()
            .SetSpacing(10)
            .Add(Heading("Project"))
            .Add(_project)
            .Add(Heading("Labels"))
            .Add(_labels)
            .Add(Heading("Estimate"))
            .Add(_estimate)
            .Add(_summary);
        form.SetPadding(new Thickness(16));
        form.SetBackground(SampleColors.Surface);
        SampleContent = new SkUiCoreHost().SetContent(form);
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var project = _project.SelectedItem as Project;
        var hours = _estimate.Columns[0].SelectedItem as int? ?? 0;
        var minutes = _estimate.Columns[1].SelectedItem as int? ?? 0;
        var labels = _labels.SelectedItems.Count == 0 ? "no labels" : string.Join(", ", _labels.SelectedItems);
        _summary.SetText($"{project?.Name ?? "No project"} · {labels} · {hours} h {minutes:00} min");
    }

    private static SkUiCoreLabel Heading(string text) =>
        new SkUiCoreLabel().SetText(text.ToUpperInvariant()).SetFontSize(13).SetFontAttributes(FontAttributes.Bold).SetTextColor(SampleColors.Caption);
}
