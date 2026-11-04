using System.ComponentModel;
using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// Edge-to-edge scrolling: translucent drawn top and bottom bars over a collection view or a scroll view, with the bars' sizes
/// passed to <c>ViewportInsets</c>. The content scrolls under the bars, and nothing stays covered at the ends of the range; the
/// scroll bar, snap points, the sticky header, sticky group headers, pull to refresh, scrolling to a row and the text field at
/// the end of the scroll view (keyboard reveal) work in the band the bars leave. The bars' padding follows the surface root's
/// <see cref="SkUiView.SafeAreaInsets"/>, as a full-page root's would. This preview sits below the page's toolbar, away from
/// the window's insets, so it reads 0 here (the status line shows it).
/// </summary>
public sealed class EdgeToEdgeScrollingDemoPage : ComponentDemoPage
{
    private const string ListContent = "Collection view";
    private const string ScrollContent = "Scroll view";
    private const string GroupedContent = "Grouped list";

    private static readonly Color BarColor = Color.FromRgba(15, 118, 110, 200);

    private readonly SkUiGrid _root;
    private readonly SkUiContentView _topBar;
    private readonly SkUiContentView _bottomBar;
    private readonly SkUiCollectionView _list;
    private readonly SkUiCollectionView _grouped;
    private readonly SkUiScrollView _scroll;
    private SkUiView _shown;
    private bool _covered = true;
    private SnapPointsType _snap = SnapPointsType.None;
    private SnapPointsAlignment _snapAlignment = SnapPointsAlignment.Start;
    private int _scrollRow;

    public EdgeToEdgeScrollingDemoPage() : base("Edge-to-edge scrolling", new SkUiGrid(), widthRange: (220, 640, 340), heightRange: (300, 800, 520))
    {
        _root = (SkUiGrid)SkiaControl;
        _list = List();
        _list.SetHeader(new SkUiBox { HeightRequest = 36, Color = Color.FromArgb("#CCFBF1") }, sticky: true);
        _list.IsPullToRefreshEnabled = true;
        _list.Refreshing += (_, _) => Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(1.5), () => _list.IsRefreshing = false);
        _grouped = Grouped();
        _scroll = new SkUiScrollView { Content = ScrollRows() };
        _shown = _list;

        _topBar = Bar("Top bar", LayoutOptions.Start);
        _bottomBar = Bar("Bottom bar", LayoutOptions.End);
        _topBar.SizeChanged += (_, _) => UpdateInsets();
        _bottomBar.SizeChanged += (_, _) => UpdateInsets();
        _root.PropertyChanged += OnRootPropertyChanged;
        _root.Children.Add(_list);
        _root.Children.Add(_topBar);
        _root.Children.Add(_bottomBar);
        UpdateBars();

        _list.Scrolled += (_, _) => Status();
        _grouped.Scrolled += (_, _) => Status();
        _scroll.Scrolled += (_, _) => Status();
        Status();

        Choice("Content", [ListContent, GroupedContent, ScrollContent], ListContent, Show, () => Name(_shown));
        Toggle("ViewportInsets", true, value => { _covered = value; UpdateInsets(); }, () => _covered);
        Number("Top bar height", 0, 160, 56, value => _topBar.HeightRequest = value, () => _topBar.HeightRequest, whole: true);
        Number("Bottom bar height", 0, 160, 64, value => _bottomBar.HeightRequest = value, () => _bottomBar.HeightRequest, whole: true);
        Toggle(nameof(SkUiCollectionView.IsStickyHeader), true, value => _list.IsStickyHeader = value, () => _list.IsStickyHeader);
        Choice(nameof(SkUiScrollView.SnapPointsType), [SnapPointsType.None, SnapPointsType.Mandatory], SnapPointsType.None,
            value => { _snap = value; UpdateSnapPoints(); }, () => _snap);
        Choice(nameof(SkUiScrollView.SnapPointsAlignment), Enum.GetValues<SnapPointsAlignment>(), SnapPointsAlignment.Start,
            value => { _snapAlignment = value; UpdateSnapPoints(); }, () => _snapAlignment);
        Number("Scroll to row (Start)", 0, 49, 0, value =>
        {
            _scrollRow = (int)value;
            if (ReferenceEquals(_shown, _scroll))
                _ = _scroll.ScrollToAsync((Element)((SkUiVerticalStackLayout)_scroll.Content!).Children[_scrollRow], ScrollToPosition.Start, animated: true);
            else
                _ = ((SkUiCollectionView)_shown).ScrollToIndex(_scrollRow, ScrollToPosition.Start);
        }, () => _scrollRow, whole: true);
    }

    private static string Name(SkUiView view) => view switch
    {
        SkUiScrollView => ScrollContent,
        SkUiCollectionView { IsGrouped: true } => GroupedContent,
        _ => ListContent
    };

    private void Show(string content)
    {
        SkUiView next = content switch
        {
            ScrollContent => _scroll,
            GroupedContent => _grouped,
            _ => _list
        };
        if (ReferenceEquals(next, _shown))
            return;
        _root.Children.Remove(_shown);
        _root.Children.Insert(0, next);
        _shown = next;
        Status();
    }

    private void UpdateInsets()
    {
        var insets = _covered ? new Thickness(0, Math.Max(0, _topBar.Height), 0, Math.Max(0, _bottomBar.Height)) : default;
        _list.ViewportInsets = insets;
        _grouped.ViewportInsets = insets;
        _scroll.ViewportInsets = insets;
        Status();
    }

    private void UpdateSnapPoints()
    {
        _scroll.SnapPointsType = _snap;
        _scroll.SnapPointsAlignment = _snapAlignment;
        _list.ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { SnapPointsType = _snap, SnapPointsAlignment = _snapAlignment };
        _grouped.ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { SnapPointsType = _snap, SnapPointsAlignment = _snapAlignment };
    }

    private void OnRootPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(SkUiView.SafeAreaInsets))
            UpdateBars();
    }

    /// <summary>The bars reach the window's edges: their padding keeps their text clear of the notch and the home indicator.</summary>
    private void UpdateBars()
    {
        var safe = _root.SafeAreaInsets;
        _topBar.Padding = new Thickness(12 + safe.Left, 8 + safe.Top, 12 + safe.Right, 8);
        _bottomBar.Padding = new Thickness(12 + safe.Left, 8, 12 + safe.Right, 8 + safe.Bottom);
        Status();
    }

    private void Status()
    {
        // Called while the page is built, before the scrollers and bars all exist.
        if (_scroll is null || _topBar is null || _bottomBar is null)
            return;
        var safe = _root.SafeAreaInsets;
        var offset = _shown is SkUiScrollView scroll ? scroll.ScrollY : ((SkUiCollectionView)_shown).ScrollY;
        Feedback($"Offset {offset:F0}; insets {_topBar.Height:F0} / {_bottomBar.Height:F0}; safe area {safe.Top:F0} / {safe.Bottom:F0}");
    }

    private static SkUiContentView Bar(string text, LayoutOptions position) => new()
    {
        Background = BarColor,
        VerticalOptions = position,
        HeightRequest = position.Alignment == LayoutAlignment.Start ? 56 : 64,
        Content = new SkUiLabel { Text = text, TextColor = Colors.White, FontSize = 16, VerticalOptions = LayoutOptions.Center }
    };

    private static SkUiLabel RowLabel() => new() { Padding = new Thickness(16, 14), FontSize = 15, TextColor = Ink };

    private static SkUiCollectionView List()
    {
        var rows = Enumerable.Range(1, 50).Select(index => $"Row {index}").ToList();
        return new SkUiCollectionView
        {
            ItemsSource = rows,
            ItemTemplate = new DataTemplate(() =>
            {
                var label = RowLabel();
                label.SetBinding(SkUiLabel.TextProperty, Binding.SelfPath);
                return label;
            })
        };
    }

    private sealed class Section(string name, IEnumerable<string> rows) : List<string>(rows)
    {
        public string Name { get; } = name;
    }

    private static SkUiCollectionView Grouped()
    {
        var sections = Enumerable.Range(1, 5)
            .Select(group => new Section($"Group {group}", Enumerable.Range(1, 10).Select(index => $"Row {group}.{index}")))
            .ToList();
        return new SkUiCollectionView
        {
            IsGrouped = true,
            IsStickyGroupHeader = true,
            ItemsSource = sections,
            ItemTemplate = new DataTemplate(() =>
            {
                var label = RowLabel();
                label.SetBinding(SkUiLabel.TextProperty, Binding.SelfPath);
                return label;
            }),
            GroupHeaderTemplate = new DataTemplate(() =>
            {
                var label = new SkUiLabel { Padding = new Thickness(16, 6), FontSize = 13, TextColor = Colors.White, Background = Accent };
                label.SetBinding(SkUiLabel.TextProperty, nameof(Section.Name));
                return label;
            })
        };
    }

    private static SkUiVerticalStackLayout ScrollRows()
    {
        var rows = new SkUiVerticalStackLayout();
        for (var index = 1; index <= 50; index++)
        {
            var label = RowLabel();
            label.Text = $"Row {index}";
            rows.Children.Add(label);
        }
        // A native view shows above the drawn surface: it is cut off at the bars instead of scrolling visibly under them.
        rows.Children.Insert(3, new SkUiMauiContentView
        {
            Content = new Entry { Placeholder = "A native field, cut off at the bars" },
            HeightRequest = 44,
            Margin = new Thickness(16, 8)
        });
        // Focus it on a phone: it scrolls above the keyboard, or above the bottom bar when that is taller.
        rows.Children.Add(new SkUiEntry { Placeholder = "A text field at the end", Margin = new Thickness(16, 8) });
        return rows;
    }
}
