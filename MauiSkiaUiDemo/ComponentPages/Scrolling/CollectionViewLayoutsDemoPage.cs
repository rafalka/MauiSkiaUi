using System.Collections.ObjectModel;
using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiCollectionView"/> (B3) next to MAUI's <see cref="CollectionView"/> on the same contacts: groups by letter
/// (sticky, collapsible headers on the drawn list only), a grid, a horizontal list, multiple selection, and loading more.
/// </summary>
public sealed class CollectionViewLayoutsDemoPage : ComponentDemoPage
{
    private static readonly string[] FirstNames = ["Ada", "Ben", "Cleo", "Dan", "Eva", "Finn", "Gus", "Hana", "Ian", "Jo", "Kai", "Lea"];
    private static readonly Color[] Colors3 = [Accent, DemoColors.SampleA, DemoColors.SampleB];

    private readonly SkUiCollectionView _skia;
    private readonly CollectionView _native;
    private readonly ObservableCollection<ContactGroup> _groups = [];
    private readonly ObservableCollection<Contact> _flat = [];
    private string _layout = "List";
    private bool _grouped = true;
    private int _next;

    public CollectionViewLayoutsDemoPage() : base("SkUiCollectionView (groups, grid, horizontal)", new SkUiCollectionView(), new CollectionView(),
        widthRange: (160, 400, 300), heightRange: (160, 600, 380))
    {
        _skia = (SkUiCollectionView)SkiaControl;
        _native = (CollectionView)NativeControl!;
        for (var letter = 'A'; letter <= 'P'; letter++)
            AddGroup(letter);

        _skia.ItemTemplate = new DataTemplate(() => new SkiaContact());
        _skia.GroupHeaderTemplate = new DataTemplate(() => new SkiaGroupHeader());
        _skia.IsStickyGroupHeader = true;
        _skia.AllowGroupExpandCollapse = true;
        _skia.SelectionChanged += (_, _) => Status();
        _skia.LoadMoreCommand = new Command(LoadMore);
        _native.ItemTemplate = new DataTemplate(() => new NativeContact());
        _native.GroupHeaderTemplate = new DataTemplate(() => new NativeGroupHeader());
        _native.SelectionChanged += (_, _) => Status();
        Apply();

        Choice("Layout", ["List", "Grid (2)", "Grid (3)", "Horizontal"], "List", value => { _layout = value; Apply(); }, () => _layout);
        Toggle(nameof(SkUiCollectionView.IsGrouped), true, value => { _grouped = value; Apply(); }, () => _skia.IsGrouped, () => _native.IsGrouped);
        // SkiaUi only.
        Toggle(nameof(SkUiCollectionView.IsStickyGroupHeader), true, value => _skia.IsStickyGroupHeader = value, () => _skia.IsStickyGroupHeader);
        Toggle(nameof(SkUiCollectionView.AllowGroupExpandCollapse), true, value => _skia.AllowGroupExpandCollapse = value, () => _skia.AllowGroupExpandCollapse);
        Choice(nameof(SkUiCollectionView.SelectionMode), Enum.GetValues<SkUiSelectionMode>(), SkUiSelectionMode.Multiple,
            value =>
            {
                _skia.SelectionMode = value;
                _native.SelectionMode = value switch { SkUiSelectionMode.None => SelectionMode.None, SkUiSelectionMode.Multiple => SelectionMode.Multiple, _ => SelectionMode.Single };
            },
            () => _skia.SelectionMode);
        Toggle(nameof(SkUiCollectionView.KeepSelectionVisible), false, value => _skia.KeepSelectionVisible = value, () => _skia.KeepSelectionVisible);
        Choice(nameof(SkUiCollectionView.LoadMoreMode), Enum.GetValues<SkUiLoadMoreMode>(), SkUiLoadMoreMode.None, value => _skia.LoadMoreMode = value, () => _skia.LoadMoreMode);
        Choice(nameof(SkUiCollectionView.LoadMorePosition), Enum.GetValues<SkUiLoadMorePosition>(), SkUiLoadMorePosition.End, value => _skia.LoadMorePosition = value, () => _skia.LoadMorePosition);
        ActionButton("Select all", () => _skia.SelectAll());
        ActionButton("Clear selection", () => { _skia.ClearSelection(); _native.SelectedItems?.Clear(); });
        ActionButton("Collapse all", () => _skia.CollapseAll());
        ActionButton("Expand all", () => _skia.ExpandAll());
        ActionButton("Scroll to group M", () =>
        {
            if (_groups.FirstOrDefault(group => group.Letter == 'M') is { } group && _grouped)
            {
                _ = _skia.ScrollToGroup(group);
                _native.ScrollTo(group.First(), group, ScrollToPosition.Start);
            }
        });
        _skia.SelectionMode = SkUiSelectionMode.Multiple;
        _native.SelectionMode = SelectionMode.Multiple;
        OnReset(() => _skia.ClearSelection());
    }

    private void AddGroup(char letter)
    {
        var group = new ContactGroup(letter, Enumerable.Range(0, 3 + letter % 5).Select(_ => NewContact(letter)));
        _groups.Add(group);
        foreach (var contact in group)
            _flat.Add(contact);
    }

    private Contact NewContact(char letter)
    {
        var index = _next++;
        return new Contact($"{letter}{FirstNames[index % FirstNames.Length].ToLowerInvariant()} {index}", Colors3[index % 3]);
    }

    private void Apply()
    {
        var horizontal = _layout == "Horizontal";
        var span = _layout switch { "Grid (2)" => 2, "Grid (3)" => 3, _ => 1 };
        _skia.Orientation = horizontal ? ItemsLayoutOrientation.Horizontal : ItemsLayoutOrientation.Vertical;
        _skia.Span = span;
        _skia.SpanSpacing = span > 1 ? 4 : 0;
        _skia.ItemSpacing = span > 1 || horizontal ? 4 : 0;
        _skia.IsGrouped = _grouped;
        _skia.ItemsSource = _grouped ? _groups : _flat;
        _native.ItemsLayout = span > 1
            ? new GridItemsLayout(span, ItemsLayoutOrientation.Vertical) { HorizontalItemSpacing = 4, VerticalItemSpacing = 4 }
            : new LinearItemsLayout(horizontal ? ItemsLayoutOrientation.Horizontal : ItemsLayoutOrientation.Vertical) { ItemSpacing = horizontal ? 4 : 0 };
        _native.IsGrouped = _grouped;
        _native.ItemsSource = _grouped ? _groups : _flat;
        Status();
    }

    /// <summary>Loads two more groups after a moment.</summary>
    private void LoadMore() => Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(1), () =>
    {
        for (var count = 0; count < 2; count++)
        {
            var letter = (char)('A' + (_groups.Count % 26));
            var group = new ContactGroup(letter, Enumerable.Range(0, 4).Select(_ => NewContact(letter)));
            if (_skia.LoadMorePosition == SkUiLoadMorePosition.Start)
                _groups.Insert(0, group);
            else
                _groups.Add(group);
            foreach (var contact in group)
                _flat.Add(contact);
        }
        _skia.IsLoadMoreActive = false;
        Status();
    });

    private void Status() =>
        Feedback($"{_skia.ItemCount} contacts in {_groups.Count} groups · {_skia.SelectedItems.Count} selected",
            $"{_native.SelectedItems?.Count ?? 0} selected");

    internal sealed record Contact(string Name, Color Color);

    /// <summary>A group of contacts by letter; it keeps its expanded state (the list writes it, the header shows it).</summary>
    internal sealed class ContactGroup(char letter, IEnumerable<Contact> contacts) : ObservableCollection<Contact>(contacts), ISkUiExpandableGroup
    {
        private bool _expanded = true;

        public char Letter { get; } = letter;

        public string Title => $"{Letter} · {Count}";

        public bool IsExpanded
        {
            get => _expanded;
            set
            {
                if (_expanded == value)
                    return;
                _expanded = value;
                OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(IsExpanded)));
            }
        }
    }

    /// <summary>A contact: a colored initial and the name; bold while selected.</summary>
    private sealed class SkiaContact : SkUiHorizontalStackLayout
    {
        private readonly SkUiBox _badge = new() { WidthRequest = 28, HeightRequest = 28, CornerRadius = 14, VerticalOptions = LayoutOptions.Center };
        private readonly SkUiLabel _name = new() { FontSize = 14, TextColor = Ink, VerticalTextAlignment = TextAlignment.Center };

        public SkiaContact()
        {
            Spacing = 8;
            Padding = new Thickness(10, 6);
            MinimumWidthRequest = 120;
            Children.Add(_badge);
            Children.Add(_name);
            VisualStateManager.SetVisualStateGroups(this, [new VisualStateGroup
            {
                Name = "CommonStates",
                States = { new VisualState { Name = "Normal" }, new VisualState { Name = "PointerOver" }, new VisualState { Name = "Selected" } }
            }]);
        }

        protected override void ChangeVisualState()
        {
            base.ChangeVisualState();
            _name.FontAttributes = VisualStateManager.GetVisualStateGroups(this)[0].CurrentState?.Name == "Selected" ? FontAttributes.Bold : FontAttributes.None;
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is not Contact contact)
                return;
            _badge.Color = contact.Color;
            _name.Text = contact.Name;
        }
    }

    /// <summary>A group header: the letter and count, with a chevron that turns while the group is collapsed.</summary>
    private sealed class SkiaGroupHeader : SkUiGrid
    {
        private readonly SkUiLabel _title = new() { FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = DemoColors.Caption, VerticalTextAlignment = TextAlignment.Center };
        private readonly SkUiLabel _chevron = new() { Text = "⌄", FontSize = 16, TextColor = DemoColors.Caption, HorizontalOptions = LayoutOptions.End, VerticalTextAlignment = TextAlignment.Center };
        private ContactGroup? _group;

        public SkiaGroupHeader()
        {
            Padding = new Thickness(10, 6);
            Background = DemoColors.PageBackground.WithAlpha(0.92f);
            // The chevron in a column of its own: in a horizontal list the header is as wide as its content, so sharing a
            // cell would put it over the title.
            ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            ColumnSpacing = 8;
            Children.Add(_title);
            Children.Add(_chevron);
            Grid.SetColumn(_chevron, 1);
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (_group is not null)
                ((System.ComponentModel.INotifyPropertyChanged)_group).PropertyChanged -= OnGroupChanged;
            _group = BindingContext as ContactGroup;
            if (_group is null)
                return;
            ((System.ComponentModel.INotifyPropertyChanged)_group).PropertyChanged += OnGroupChanged;
            Show();
        }

        private void OnGroupChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args) => Show();

        private void Show()
        {
            _title.Text = _group!.Title;
            _chevron.Rotation = _group.IsExpanded ? 0 : -90; // composite-time: no re-record
        }
    }

    private sealed class NativeContact : ContentView
    {
        private readonly BoxView _badge = new() { WidthRequest = 28, HeightRequest = 28, CornerRadius = 14, VerticalOptions = LayoutOptions.Center };
        private readonly Label _name = new() { FontSize = 14, TextColor = Ink, VerticalTextAlignment = TextAlignment.Center };

        public NativeContact() => Content = new HorizontalStackLayout { Spacing = 8, Padding = new Thickness(10, 6), Children = { _badge, _name } };

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is not Contact contact)
                return;
            _badge.Color = contact.Color;
            _name.Text = contact.Name;
        }
    }

    private sealed class NativeGroupHeader : ContentView
    {
        private readonly Label _title = new() { FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = DemoColors.Caption, Padding = new Thickness(10, 6) };

        public NativeGroupHeader() => Content = _title;

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is ContactGroup group)
                _title.Text = group.Title;
        }
    }
}
