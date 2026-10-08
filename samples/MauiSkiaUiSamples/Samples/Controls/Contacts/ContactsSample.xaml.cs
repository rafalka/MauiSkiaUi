using System.ComponentModel;
using MauiSkiaUi;

namespace MauiSkiaUiSamples.Samples.Controls.Contacts;

/// <summary>
/// HOWTO: a contacts screen on <see cref="SkUiCollectionView"/>. The page's XAML (ContactsSample.xaml) lays out the
/// screen; this file wires the list to the view model:
/// <list type="number">
/// <item>What shows: <see cref="ContactsViewModel.View"/>, a <see cref="LiveGroupedList{TItem, TKey}"/> over the contacts
/// (filtered, sorted, grouped; a contact may be in several groups). Its collections change item by item as contacts are
/// added, removed, renamed or starred, so the list changes only those rows; a new grouping, order or search rebuilds it.</item>
/// <item>Templates per mode: rows or tiles (<see cref="SkUiCollectionView.Span"/> from the width), with checkboxes in the
/// selection mode; changing the template rebuilds the shown rows only.</item>
/// <item>Preview: next to the list on wide screens (tablets, desktops, phones held sideways), a popup on narrow phones; the
/// list keeps the previewed contact selected and in view (<see cref="SkUiCollectionView.KeepSelectionVisible"/>), also
/// after a rotation.</item>
/// <item>Selection mode: a long press on a contact or a group header; the list switches to multiple selection over
/// <see cref="ContactsViewModel.Selected"/>; group headers show three-state checkboxes.</item>
/// </list>
/// </summary>
public sealed partial class ContactsSample : SamplePage, ISample
{
    public static SampleInfo Info { get; } = new(
        SampleSection.Controls,
        Title: "Contacts",
        Summary: "A contacts screen on SkUiCollectionView: search, sort and group (by letter, by group, or none), rows or " +
                 "tiles, a preview beside the list or in a popup, a selection mode with actions, and a form to add contacts.",
        HowTo:
        [
            "Keep what the list shows in a live grouped view over the source (`LiveGroupedList.cs`): it filters, sorts and " +
                "groups, and follows changes item by item, so the list changes only the rows they touch.",
            "Bind `ItemsSource` to its groups with `IsGrouped=\"True\"`, or to its items when not grouped; a contact may be in " +
                "several groups (it shows, and is selected, in each).",
            "Rows or tiles: swap `ItemTemplate`; for tiles set `Span` from the list's width.",
            "Long press: switch to `SelectionMode=\"Multiple\"` over your own `SelectedItems` list, with templates that show " +
                "checkboxes (the item root's `Selected` state checks them).",
            "`KeepSelectionVisible=\"True\"` keeps the previewed contact in view, also after a rotation.",
            "Native controls (the search box, the form's entries) are hosted in `SkUiMauiContentView`."
        ],
        ThingsToKnow:
        [
            "Moving an item within the shown list (a rename) is a move, not a remove and an insert, so the list keeps it " +
                "selected; a contact that changes groups is added to the new ones before it leaves the old ones.",
            "Group headers are templates too: their checkbox state (all, some, none) is a property of the group, kept by the " +
                "view model.",
            "The photos are MIT-licensed portraits from the Syncfusion Toolkit for .NET MAUI gallery (Resources/Images/README.md).",
            "The page fills the screen below this description, as an app's page would."
        ])
    {
        MoreSources = ["ContactsModel.cs", "LiveGroupedList.cs", "ContactViews.cs", "ContactEditorPage.cs"]
    };

    // The choices of the action sheets: shown, and compared with what was picked.
    private const string Cancel = "Cancel";
    private const string GroupByName = "By name";
    private const string GroupByGroup = "By group";
    private const string NotGrouped = "Not grouped";
    private const string SortByFirstName = "First name";
    private const string SortByLastName = "Last name";
    private const string SortByRecent = "Recently added";
    private const string AddToFamily = "Add to Family";
    private const string AddToBusiness = "Add to Business";
    private const string RemoveFromGroups = "Remove from groups";
    private const string Delete = "Delete";

    private readonly ContactsViewModel _model;
    private bool _wide;

    public ContactsSample() : this(ContactsViewModel.SampleContacts()) { }

    /// <summary>A page over <paramref name="contacts"/> (tests).</summary>
    internal ContactsSample(IEnumerable<Contact> contacts) : base(Info, fillsPage: true)
    {
        InitializeComponent();
        _model = new ContactsViewModel(contacts);
        BindingContext = _model;
        List.SelectedItems = _model.Selected;
        List.SelectionChanged += (_, _) => OnListSelectionChanged();
        _model.PropertyChanged += OnModelChanged;
        Root.SizeChanged += (_, _) => UpdateLayout();
        ApplySource();
        ApplyTemplates();
        ApplyMode();
        UpdateOptions();
    }

    internal ContactsViewModel Model => _model;

    /// <summary>Whether the preview shows beside the list (else in a popup).</summary>
    internal bool IsWide => _wide;

    private void OnModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        switch (args.PropertyName)
        {
            case nameof(ContactsViewModel.Shown):
                ApplySource();
                break;
            case nameof(ContactsViewModel.Display):
                ApplyTemplates();
                UpdateLayout();
                break;
            case nameof(ContactsViewModel.IsSelecting):
                ApplyMode();
                break;
            case nameof(ContactsViewModel.Current):
                Preview.BindingContext = _model.Current;
                if (!_model.IsSelecting && !ReferenceEquals(List.SelectedItem, _model.Current))
                    List.SelectedItem = _model.Current;
                break;
        }
        UpdateOptions();
    }

    private void ApplySource()
    {
        List.IsGrouped = _model.IsGrouped;
        List.ItemsSource = _model.Shown;
    }

    /// <summary>Rows or tiles, with checkboxes while selecting (the modes are read now: a change sets new templates).</summary>
    private void ApplyTemplates()
    {
        var model = _model;
        var selecting = model.IsSelecting;
        var tiles = model.Display == ContactDisplay.Tiles;
        List.ItemTemplate = new DataTemplate(() => tiles ? new ContactTile(model, selecting) : new ContactRow(model, selecting));
        List.GroupHeaderTemplate = new DataTemplate(() => new ContactGroupHeader(model, selecting));
        List.ItemSpacing = tiles ? 6 : 0;
        List.SpanSpacing = tiles ? 6 : 0;
    }

    /// <summary>The selection mode: multiple selection over the view model's list, checkboxes, the action bar.</summary>
    private void ApplyMode()
    {
        var selecting = _model.IsSelecting;
        Options.IsVisible = !selecting;
        // Group headers check their group instead of collapsing it.
        List.AllowGroupExpandCollapse = !selecting;
        List.SelectionMode = selecting ? SkUiSelectionMode.Multiple : SkUiSelectionMode.Single;
        ApplyTemplates();
        if (!selecting)
            List.SelectedItem = _model.Current;
    }

    private void OnListSelectionChanged()
    {
        if (!_model.IsSelecting && List.SelectedItem is Contact contact)
            _model.Current = contact;
    }

    private void OnItemTapped(object? sender, SkUiItemTappedEventArgs args)
    {
        if (_model.IsSelecting || args.Item is not Contact contact)
            return;
        _model.Current = contact;
        if (!_wide)
            ShowPopup(contact);
    }

    /// <summary>
    /// The preview beside the list on wide screens (tablets, desktops, phones held sideways), else in a popup; tiles as many
    /// per row as fit.
    /// </summary>
    private void UpdateLayout()
    {
        var width = Root.Width;
        if (width <= 0)
            return;
        var phone = DeviceInfo.Idiom == DeviceIdiom.Phone;
        _wide = phone ? Root.Width > Root.Height : width >= 640;
        var pane = _wide ? Math.Clamp(width * 0.4, 260, 380) : 0;
        Body.ColumnDefinitions[1].Width = new GridLength(pane);
        PreviewPane.IsVisible = _wide;
        if (_wide && Popup.IsVisible)
            Popup.IsVisible = false;
        List.Span = _model.Display == ContactDisplay.Tiles ? Math.Max(2, (int)((width - pane) / 130)) : 1;
    }

    private void ShowPopup(Contact contact)
    {
        PopupPreview.BindingContext = contact;
        Popup.Opacity = 0;
        Popup.IsVisible = true;
        _ = Popup.AnimateAsync(SkUiAnimatableProperty.Opacity, 1, 150);
    }

    private void OnClosePopup(object? sender, EventArgs args) => Popup.IsVisible = false;

    /// <summary>The option buttons say what is chosen.</summary>
    private void UpdateOptions()
    {
        var grouping = _model.Grouping switch
        {
            ContactGrouping.Groups => GroupByGroup,
            ContactGrouping.None => NotGrouped,
            _ => GroupByName
        };
        var sort = _model.Sort switch
        {
            ContactSort.LastName => SortByLastName,
            ContactSort.RecentlyAdded => SortByRecent,
            _ => SortByFirstName
        };
        GroupingButton.Text = $"{grouping} ▾";
        SortButton.Text = $"{sort} ▾";
        DisplayButton.Text = _model.Display == ContactDisplay.Tiles ? "Tiles" : "List";
    }

    private async void OnGrouping(object? sender, EventArgs args)
    {
        _model.Grouping = await DisplayActionSheetAsync("Group contacts", Cancel, null, GroupByName, GroupByGroup, NotGrouped) switch
        {
            GroupByName => ContactGrouping.Name,
            GroupByGroup => ContactGrouping.Groups,
            NotGrouped => ContactGrouping.None,
            _ => _model.Grouping
        };
    }

    private async void OnSort(object? sender, EventArgs args)
    {
        _model.Sort = await DisplayActionSheetAsync("Sort contacts", Cancel, null, SortByFirstName, SortByLastName, SortByRecent) switch
        {
            SortByFirstName => ContactSort.FirstName,
            SortByLastName => ContactSort.LastName,
            SortByRecent => ContactSort.RecentlyAdded,
            _ => _model.Sort
        };
    }

    private void OnDisplay(object? sender, EventArgs args) =>
        _model.Display = _model.Display == ContactDisplay.List ? ContactDisplay.Tiles : ContactDisplay.List;

    private void OnDone(object? sender, EventArgs args) => _model.EndSelection();

    private void OnSelectAll(object? sender, EventArgs args) => List.SelectAll();

    private void OnStar(object? sender, EventArgs args) => _model.ToggleStarOfSelected();

    private async void OnGroup(object? sender, EventArgs args)
    {
        if (_model.Selected.Count == 0)
            return;
        ContactGroups? group = await DisplayActionSheetAsync("Groups of the checked contacts", Cancel, null, AddToFamily, AddToBusiness, RemoveFromGroups) switch
        {
            AddToFamily => ContactGroups.Family,
            AddToBusiness => ContactGroups.Business,
            RemoveFromGroups => ContactGroups.None,
            _ => null
        };
        if (group is { } chosen)
            _model.SetGroupOfSelected(chosen);
    }

    private async void OnDelete(object? sender, EventArgs args)
    {
        var count = _model.Selected.Count;
        if (count == 0)
            return;
        if (await DisplayAlertAsync("Delete contacts", count == 1 ? "Delete the checked contact?" : $"Delete {count} checked contacts?", Delete, Cancel))
            _model.DeleteSelected();
    }

    private async void OnAdd(object? sender, EventArgs args)
    {
        var editor = new ContactEditorPage();
        await Navigation.PushModalAsync(new NavigationPage(editor));
        if (await editor.Result is not { } contact)
            return;
        if (_model.IsSelecting)
            _model.EndSelection();
        _model.Add(contact);
    }
}
