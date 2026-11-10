namespace MauiSkiaUi;

// The selected item's own template: a selected item's row is made by SelectedItemTemplate instead of ItemTemplate. The
// template is part of the item's recycle key, so a selection change gives the rows whose state changed a container of the
// other template (from its pool) and measures them again; nothing of this runs while the template is unset.
public partial class SkUiCollectionView
{
    private DataTemplate? _selectedItemTemplate;
    // The items shown selected when the rows' templates were last brought up to date (only while SelectedItemTemplate is set).
    private readonly HashSet<object> _templatedSelection = [];

    /// <summary>Bindable property for <see cref="SelectedItemTemplate"/>.</summary>
    public static readonly BindableProperty SelectedItemTemplateProperty = BindableProperty.Create(nameof(SelectedItemTemplate),
        typeof(DataTemplate), typeof(SkUiCollectionView), null,
        propertyChanged: (view, _, value) => ((SkUiCollectionView)view).OnSelectedItemTemplateChanged((DataTemplate?)value));

    /// <summary>
    /// The template of a selected item's row (a <see cref="DataTemplate"/> or a <see cref="DataTemplateSelector"/>; one that
    /// chooses none falls back to <see cref="ItemTemplate"/>), instead of <see cref="ItemTemplate"/>: for selected rows that
    /// show more (details, actions) or differ in size. For another look only, the <c>Selected</c> visual state is cheaper.
    /// A selection change replaces the row's view with one of the other template (recycled per template) and measures the
    /// row again; what shows stays in place. Grid cells swap the same way. Rows of another size need
    /// <see cref="ItemSizingStrategy.MeasureAllItems"/> (the default).
    /// </summary>
    public DataTemplate? SelectedItemTemplate
    {
        get => (DataTemplate?)GetValue(SelectedItemTemplateProperty);
        set => SetValue(SelectedItemTemplateProperty, value);
    }

    /// <summary>Sets <see cref="SelectedItemTemplate"/> (same as the property setter).</summary>
    public SkUiCollectionView SetSelectedItemTemplate(DataTemplate? value)
    {
        SelectedItemTemplate = value;
        return this;
    }

    private void OnSelectedItemTemplateChanged(DataTemplate? value)
    {
        _selectedItemTemplate = value;
        _templatedSelection.Clear();
        if (value is not null)
            AddShownSelection(_templatedSelection);
        // As for a new item template: every row is made and measured again.
        Model.Rebuild();
        DropViews();
    }

    /// <summary>The template a selected item's row uses, or <c>null</c> (not selected, no selected template, or the selector chose none).</summary>
    private DataTemplate? SelectedTemplateOf(object? item) =>
        _selectedItemTemplate is not null && ShowsSelection(item) ? SelectTemplate(_selectedItemTemplate, item) : null;

    /// <summary>Adds the items shown selected now to <paramref name="items"/>.</summary>
    private void AddShownSelection(HashSet<object> items)
    {
        switch (_selectionMode)
        {
            case SkUiSelectionMode.Multiple:
                items.UnionWith(_selectedSet);
                break;
            case SkUiSelectionMode.Single or SkUiSelectionMode.SingleDeselect when _selectedItem is { } selected:
                items.Add(selected);
                break;
        }
    }

    /// <summary>
    /// The selection changed (<see cref="RefreshSelection"/>): the rows of the items whose selected state changed get a view
    /// of their template now. Realized rows are made again (a grid row swaps the cells); the others forget their size.
    /// </summary>
    private void RenewSelectedTemplates()
    {
        if (_selectedItemTemplate is null)
            return;
        var shown = new HashSet<object>();
        AddShownSelection(shown);
        var changed = new HashSet<object>(shown);
        changed.SymmetricExceptWith(_templatedSelection);
        _templatedSelection.Clear();
        _templatedSelection.UnionWith(shown);
        if (changed.Count == 0 || Model.RowCount == 0)
            return;
        // The rows showing a changed item (an item may show in several groups), each once: one pass over the items.
        var rows = new SortedSet<int>();
        if (Model.Grouped)
        {
            for (var group = 0; group < Model.Groups.Count; group++)
                for (var index = 0; index < Model.Groups[group].Count; index++)
                    if (Model.ItemAt(group, index) is { } item && changed.Contains(item) && Model.RowOfItem(group, index) is >= 0 and var row)
                        rows.Add(row);
        }
        else
        {
            for (var index = 0; index < Model.ItemCount; index++)
                if (Model.ItemAt(-1, index) is { } item && changed.Contains(item))
                    rows.Add(Model.RowOfItem(-1, index));
        }
        foreach (var row in rows)
        {
            if (ItemsHost.GetRealizedView(row) is GridRowPart rowPart)
            {
                rowPart.Bind(Model.RowAt(row)); // cells of another template swap with the pool
                rowPart.InvalidateMeasure();
            }
            else
            {
                ItemsHost.RenewItem(row);
            }
        }
        RefreshRing();
    }
}
