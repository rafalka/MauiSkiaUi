namespace MauiSkiaUi;

public partial class SkUiCollectionView
{
    // The current group's header over the list (IsStickyGroupHeader), created the first time it shows.
    private StickyHost? _stickyGroupHost;
    private GroupHeaderHost? _stickyGroupHeader;
    private readonly Dictionary<DataTemplate, ISkUiView> _stickyGroupContents = [];
    private int _stickyGroup = -1;
    private bool _updatingSticky;

    /// <summary>Bindable property for <see cref="IsGrouped"/>.</summary>
    public static readonly BindableProperty IsGroupedProperty = BindableProperty.Create(nameof(IsGrouped), typeof(bool), typeof(SkUiCollectionView), false,
        propertyChanged: (view, _, _) => ((SkUiCollectionView)view).ApplyStructure());

    /// <summary>Bindable property for <see cref="GroupHeaderTemplate"/>.</summary>
    public static readonly BindableProperty GroupHeaderTemplateProperty = BindableProperty.Create(nameof(GroupHeaderTemplate), typeof(DataTemplate), typeof(SkUiCollectionView), null,
        propertyChanged: (view, _, _) => ((SkUiCollectionView)view).OnGroupTemplateChanged());

    /// <summary>Bindable property for <see cref="GroupFooterTemplate"/>.</summary>
    public static readonly BindableProperty GroupFooterTemplateProperty = BindableProperty.Create(nameof(GroupFooterTemplate), typeof(DataTemplate), typeof(SkUiCollectionView), null,
        propertyChanged: (view, _, _) => ((SkUiCollectionView)view).OnGroupTemplateChanged());

    /// <summary>Bindable property for <see cref="IsStickyGroupHeader"/>.</summary>
    public static readonly BindableProperty IsStickyGroupHeaderProperty = BindableProperty.Create(nameof(IsStickyGroupHeader), typeof(bool), typeof(SkUiCollectionView), false,
        propertyChanged: (view, _, _) => ((SkUiCollectionView)view).UpdateStickyGroupHeader());

    /// <summary>Bindable property for <see cref="AllowGroupExpandCollapse"/>.</summary>
    public static readonly BindableProperty AllowGroupExpandCollapseProperty = BindableProperty.Create(nameof(AllowGroupExpandCollapse), typeof(bool), typeof(SkUiCollectionView), false);

    /// <summary>Bindable property for <see cref="AutoExpandGroups"/>.</summary>
    public static readonly BindableProperty AutoExpandGroupsProperty = BindableProperty.Create(nameof(AutoExpandGroups), typeof(bool), typeof(SkUiCollectionView), true);

    /// <summary>
    /// Whether <see cref="ItemsSource"/> is a list of groups, each the list of its items (MAUI's grouped source: a
    /// <c>List&lt;T&gt;</c> subclass with the group's own properties, such as its name). Group and item changes are both followed.
    /// </summary>
    public bool IsGrouped { get => (bool)GetValue(IsGroupedProperty); set => SetValue(IsGroupedProperty, value); }

    /// <summary>
    /// Creates each group's header row (drawn views; the group is its binding context; a <see cref="DataTemplateSelector"/>
    /// chooses per group). Without it groups have no header. The header's root goes to the <c>Expanded</c> or
    /// <c>Collapsed</c> visual state.
    /// </summary>
    public DataTemplate? GroupHeaderTemplate { get => (DataTemplate?)GetValue(GroupHeaderTemplateProperty); set => SetValue(GroupHeaderTemplateProperty, value); }

    /// <summary>Creates each expanded group's footer row (after its items; the group is its binding context).</summary>
    public DataTemplate? GroupFooterTemplate { get => (DataTemplate?)GetValue(GroupFooterTemplateProperty); set => SetValue(GroupFooterTemplateProperty, value); }

    /// <summary>
    /// Whether the header of the group at the start of the list stays there while its items scroll behind it, pushed away by
    /// the next group's header (default <c>false</c>). It is drawn over the list (below a sticky list header), rebound only
    /// when the group changes, and taps on it expand or collapse its group (<see cref="AllowGroupExpandCollapse"/>).
    /// Scrolling to an item places it after it.
    /// </summary>
    public bool IsStickyGroupHeader { get => (bool)GetValue(IsStickyGroupHeaderProperty); set => SetValue(IsStickyGroupHeaderProperty, value); }

    /// <summary>
    /// Whether a tap on a group's header expands or collapses the group (default <c>false</c>). Collapsed groups omit their
    /// items (and footer) from the list: they are not realized, and what shows stays in place.
    /// </summary>
    public bool AllowGroupExpandCollapse { get => (bool)GetValue(AllowGroupExpandCollapseProperty); set => SetValue(AllowGroupExpandCollapseProperty, value); }

    /// <summary>Whether new groups start expanded (default <c>true</c>); a group that is an <see cref="ISkUiExpandableGroup"/> starts as it says.</summary>
    public bool AutoExpandGroups { get => (bool)GetValue(AutoExpandGroupsProperty); set => SetValue(AutoExpandGroupsProperty, value); }

    /// <summary>A group is about to expand (a header tap or <see cref="ExpandGroup"/>): set <see cref="SkUiGroupChangingEventArgs.Cancel"/> to keep it collapsed.</summary>
    public event EventHandler<SkUiGroupChangingEventArgs>? GroupExpanding;

    /// <summary>A group expanded (also when its <see cref="ISkUiExpandableGroup.IsExpanded"/> changed).</summary>
    public event EventHandler<SkUiGroupEventArgs>? GroupExpanded;

    /// <summary>A group is about to collapse: set <see cref="SkUiGroupChangingEventArgs.Cancel"/> to keep it expanded.</summary>
    public event EventHandler<SkUiGroupChangingEventArgs>? GroupCollapsing;

    /// <summary>A group collapsed.</summary>
    public event EventHandler<SkUiGroupEventArgs>? GroupCollapsed;

    /// <summary>Whether <paramref name="group"/> (a group of <see cref="ItemsSource"/>) is expanded.</summary>
    /// <exception cref="ArgumentException"><paramref name="group"/> is not a group of the list.</exception>
    public bool IsGroupExpanded(object group) => _model.Groups[GroupIndex(group)].Expanded;

    /// <summary>Expands <paramref name="group"/>; returns <c>false</c> when <see cref="GroupExpanding"/> canceled it.</summary>
    /// <exception cref="ArgumentException"><paramref name="group"/> is not a group of the list.</exception>
    public bool ExpandGroup(object group) => ChangeGroupExpanded(GroupIndex(group), true, fromGroup: false);

    /// <summary>Collapses <paramref name="group"/>; returns <c>false</c> when <see cref="GroupCollapsing"/> canceled it.</summary>
    /// <exception cref="ArgumentException"><paramref name="group"/> is not a group of the list.</exception>
    public bool CollapseGroup(object group) => ChangeGroupExpanded(GroupIndex(group), false, fromGroup: false);

    /// <summary>Expands every group (each may cancel through <see cref="GroupExpanding"/>).</summary>
    public void ExpandAll()
    {
        for (var index = 0; index < _model.Groups.Count; index++)
            ChangeGroupExpanded(index, true, fromGroup: false);
    }

    /// <summary>Collapses every group (each may cancel through <see cref="GroupCollapsing"/>).</summary>
    public void CollapseAll()
    {
        for (var index = 0; index < _model.Groups.Count; index++)
            ChangeGroupExpanded(index, false, fromGroup: false);
    }

    /// <summary>Scrolls to <paramref name="group"/>'s header (its first item without headers); see <see cref="ScrollToIndex"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="group"/> is not a group of the list.</exception>
    public Task ScrollToGroup(object group, ScrollToPosition position = ScrollToPosition.Start, bool animated = true)
    {
        var index = GroupIndex(group);
        var row = _model.HasGroupHeaders ? _model.HeaderRowOf(index) : _model.RowOfItem(index, 0);
        return row < 0 || row >= _model.RowCount ? Task.CompletedTask : _items.ScrollToIndex(row, position, animated);
    }

    private int GroupIndex(object group)
    {
        var index = _model.IndexOfGroup(group);
        return index >= 0 ? index : throw new ArgumentException("The object is not a group of the list.", nameof(group));
    }

    /// <summary>The state a new group starts with.</summary>
    private bool InitialExpanded(object? group) => group is ISkUiExpandableGroup expandable ? expandable.IsExpanded : AutoExpandGroups;

    private void OnGroupTemplateChanged()
    {
        _stickyGroupContents.Clear();
        if (_stickyGroupHeader is not null)
            _stickyGroupHeader.Content = null;
        _stickyGroup = -1;
        ApplyStructure(templatesChanged: IsGrouped);
    }

    /// <summary>
    /// Expands or collapses the group at <paramref name="index"/>: the app may cancel (<see cref="GroupExpanding"/>,
    /// <see cref="GroupCollapsing"/>) unless the group itself changed (<paramref name="fromGroup"/>). Returns whether the group
    /// is now as asked.
    /// </summary>
    private bool ChangeGroupExpanded(int index, bool expanded, bool fromGroup)
    {
        var group = _model.Groups[index];
        if (group.Expanded == expanded)
            return true;
        if (!fromGroup)
        {
            var args = new SkUiGroupChangingEventArgs(group.Value, index);
            (expanded ? GroupExpanding : GroupCollapsing)?.Invoke(this, args);
            if (args.Cancel)
                return false;
        }
        // Collapsing the group shown by the sticky header: its header comes to the start of the list, where it was.
        var wasSticky = !expanded && _stickyGroup == index && _stickyGroupHost is { IsVisible: true };
        _model.SetExpanded(index, expanded);
        if (group.Value is ISkUiExpandableGroup expandable && expandable.IsExpanded != expanded)
            expandable.IsExpanded = expanded;
        RefreshGroupHeader(index);
        if (wasSticky && _model.HeaderRowOf(index) is >= 0 and var header)
            _items.ScrollToIndex(header, ScrollToPosition.Start, animated: false);
        (expanded ? GroupExpanded : GroupCollapsed)?.Invoke(this, new SkUiGroupEventArgs(group.Value, index));
        UpdateStickyGroupHeader();
        return true;
    }

    /// <summary>Shows a group's expanded state on its realized header (and the sticky one).</summary>
    private void RefreshGroupHeader(int index)
    {
        var expanded = _model.Groups[index].Expanded;
        if (_model.HeaderRowOf(index) is >= 0 and var row && _items.GetRealizedView(row) is GroupHeaderHost header)
            header.SetExpanded(expanded);
        if (_stickyGroup == index)
            _stickyGroupHeader?.SetExpanded(expanded);
    }

    /// <summary>A tap on a group header (in the list or the sticky one): its group expands or collapses.</summary>
    private void OnGroupHeaderTapped(GroupHeaderHost host)
    {
        var index = host.StickyGroup;
        if (index < 0 && _items.IndexOfRealizedView(host) is >= 0 and var row)
            index = _model.RowAt(row).Group;
        if (index >= 0 && index < _model.Groups.Count)
            ChangeGroupExpanded(index, !_model.Groups[index].Expanded, fromGroup: false);
    }

    /// <summary>Room a sticky group header takes before an item scrolled to (none for group headers themselves).</summary>
    private double StickyGroupInset(int row)
    {
        if (!IsStickyGroupHeader || !_model.HasGroupHeaders || row >= _model.RowCount || _model.RowAt(row) is not { Kind: not RowKind.GroupHeader } target)
            return 0;
        return _items.RowSize(_model.HeaderRowOf(target.Group));
    }

    /// <summary>
    /// Shows the header of the group at the start of the uncovered part of the list over it, pushed back by the next group's
    /// header; hides it while the group's own header shows there (or before the first group). Runs on scrolling (also each
    /// frame of a render-thread fling, as its offsets are reported), layout and changes of the items.
    /// </summary>
    private void UpdateStickyGroupHeader()
    {
        if (_updatingSticky)
            return;
        _updatingSticky = true;
        try
        {
            var show = -1;
            var shift = 0d;
            if (IsStickyGroupHeader && _model.HasGroupHeaders && _model.RowCount > 0 && _items.HasWindow)
            {
                var start = _items.VisibleStart + _scroller.Insets.Start;
                if (start > 0 && start < _items.TotalLength)
                {
                    var group = _model.RowAt(_items.RowAtOffset(start)).Group;
                    if (group >= 0 && start > _items.RowOffset(_model.HeaderRowOf(group)) + 0.5)
                    {
                        show = group;
                        var length = AlongOf(_stickyGroupHost);
                        if (group + 1 < _model.Groups.Count)
                            shift = Math.Min(0, _items.RowOffset(_model.HeaderRowOf(group + 1)) - start - length);
                    }
                }
            }
            ShowStickyGroup(show, shift);
        }
        finally
        {
            _updatingSticky = false;
        }
    }

    private void ShowStickyGroup(int index, double shift)
    {
        if (index < 0)
        {
            if (_stickyGroupHost is { IsVisible: true } hidden)
                hidden.IsVisible = false;
            return;
        }
        var host = _stickyGroupHost ??= Adopt(new StickyHost(Controller));
        var header = _stickyGroupHeader ??= new GroupHeaderHost(this);
        if (!ReferenceEquals(host.Content, header))
            host.Content = header;
        var group = _model.Groups[index];
        if (_stickyGroup != index || header.Content is null)
        {
            _stickyGroup = index;
            header.StickyGroup = index;
            // One view per template (a selector may choose another one per group), rebound to the group.
            if (SelectTemplate(GroupHeaderTemplate, group.Value) is { } template)
            {
                if (!_stickyGroupContents.TryGetValue(template, out var content))
                    _stickyGroupContents[template] = content = CreateFromTemplate(template, nameof(GroupHeaderTemplate));
                if (!ReferenceEquals(header.Content, content))
                    header.Content = content;
            }
            header.BindingContext = group.Value;
            header.SetExpanded(group.Expanded);
        }
        if (!host.IsVisible)
            host.IsVisible = true;
        if (_horizontal)
            host.TranslationX = IsRightToLeft ? -shift : shift;
        else
            host.TranslationY = shift;
    }

    /// <summary>The group shown by the sticky group header, or -1 (tests).</summary>
    internal int StickyGroupShown => _stickyGroupHost is { IsVisible: true } ? _stickyGroup : -1;

    /// <summary>The sticky group header's host (tests).</summary>
    internal SkUiView? StickyGroupHost => _stickyGroupHost;
}
