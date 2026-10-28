using System.Collections.ObjectModel;
using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// Reordering on <see cref="SkUiCollectionView"/> next to MAUI's <see cref="CollectionView"/>: both lists show the same
/// tasks, so a move in either shows in both. A list, a grid or a horizontal list; grouped, with or without moves between
/// the groups.
/// </summary>
public sealed class ReorderDemoPage : ComponentDemoPage
{
    private const string List = "List";
    private const string GridLayout = "Grid, 2";
    private const string Horizontal = "Horizontal";
    private const double RowHeight = 56;
    private const double CardWidth = 120;
    private const double Spacing = 6;

    private static readonly string[] Names =
    [
        "Plan the week", "Answer email", "Review the draft", "Call the bank", "Book flights", "Water the plants", "Fix the bike",
        "Buy groceries", "Pay the rent", "Read a chapter", "Clean the desk", "Back up photos"
    ];

    private readonly SkUiCollectionView _skia;
    private readonly CollectionView _native;
    private readonly ObservableCollection<TaskItem> _tasks = [];
    private readonly ObservableCollection<TaskGroup> _groups = [];
    private string _layout = List;
    private bool _grouped;
    private int _moves;

    public ReorderDemoPage() : base("SkUiCollectionView (reordering)", new SkUiCollectionView(), new CollectionView(),
        widthRange: (160, 400, 300), heightRange: (200, 640, 420))
    {
        _skia = (SkUiCollectionView)SkiaControl;
        _native = (CollectionView)NativeControl!;
        Fill();
        _skia.ItemTemplate = new DataTemplate(() => new SkiaTask());
        _skia.GroupHeaderTemplate = new DataTemplate(() => new SkiaHeader());
        _skia.ReorderCompleted += (_, _) => { _moves++; Status(); };
        _native.ItemTemplate = new DataTemplate(() => new NativeTask());
        _native.GroupHeaderTemplate = new DataTemplate(() => new NativeHeader());
        _native.ReorderCompleted += (_, _) => Status();
        Apply();

        Toggle(nameof(SkUiCollectionView.CanReorderItems), true,
            value => { _skia.CanReorderItems = value; _native.CanReorderItems = value; },
            () => _skia.CanReorderItems, () => _native.CanReorderItems);
        Choice(nameof(SkUiCollectionView.ItemsLayout), [List, GridLayout, Horizontal], List, value => { _layout = value; Apply(); }, () => _layout);
        Toggle(nameof(SkUiCollectionView.IsGrouped), false, value => { _grouped = value; Apply(); }, () => _skia.IsGrouped, () => _native.IsGrouped);
        Toggle(nameof(SkUiCollectionView.CanMixGroups), false,
            value => { _skia.CanMixGroups = value; _native.CanMixGroups = value; },
            () => _skia.CanMixGroups, () => _native.CanMixGroups);
        ActionButton("Reset the order", () => { Fill(); Apply(); });
    }

    /// <summary>The tasks in their first order: one list, and the same tasks in two groups.</summary>
    private void Fill()
    {
        _tasks.Clear();
        for (var index = 0; index < Names.Length; index++)
            _tasks.Add(new TaskItem(Names[index], index % 3 == 0 ? Accent : index % 3 == 1 ? DemoColors.SampleA : DemoColors.SampleB));
        _groups.Clear();
        _groups.Add(new TaskGroup("Today", _tasks.Take(5)));
        _groups.Add(new TaskGroup("Later", _tasks.Skip(5)));
    }

    /// <summary>One layout object and one source for both lists.</summary>
    private void Apply()
    {
        ItemsLayout layout = _layout switch
        {
            GridLayout => new GridItemsLayout(2, ItemsLayoutOrientation.Vertical) { HorizontalItemSpacing = Spacing, VerticalItemSpacing = Spacing },
            Horizontal => new LinearItemsLayout(ItemsLayoutOrientation.Horizontal) { ItemSpacing = Spacing },
            _ => new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = Spacing }
        };
        _skia.ItemsLayout = layout;
        _native.ItemsLayout = layout;
        _skia.IsGrouped = _grouped;
        _native.IsGrouped = _grouped;
        System.Collections.IEnumerable source = _grouped ? _groups : _tasks;
        _skia.ItemsSource = source;
        _native.ItemsSource = source;
        Status();
    }

    private void Status()
    {
        var order = _grouped
            ? string.Join(" | ", _groups.Select(group => string.Join(", ", group.Select(Short))))
            : string.Join(", ", _tasks.Take(6).Select(Short)) + "…";
        Feedback($"{_moves} moves · {order}");

        static string Short(TaskItem task) => task.Title.Split(' ')[^1];
    }

    private sealed record TaskItem(string Title, Color Color);

    private sealed class TaskGroup(string name, IEnumerable<TaskItem> tasks) : ObservableCollection<TaskItem>(tasks)
    {
        public string Name { get; } = name;
    }

    /// <summary>A task: a colored bar and its title, a row in a list, a card in a grid or a horizontal list.</summary>
    private sealed class SkiaTask : SkUiGrid
    {
        private readonly SkUiBox _bar = new() { WidthRequest = 6, CornerRadius = 3 };
        private readonly SkUiLabel _title = new() { FontSize = 15, TextColor = DemoColors.Ink, VerticalTextAlignment = TextAlignment.Center, Margin = new Thickness(12, 0) };

        public SkiaTask()
        {
            HeightRequest = RowHeight;
            MinimumWidthRequest = CardWidth;
            Background = Colors.White;
            ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)];
            Children.Add(_bar);
            Microsoft.Maui.Controls.Grid.SetColumn((BindableObject)_title, 1);
            Children.Add(_title);
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is not TaskItem task)
                return;
            _bar.Color = task.Color;
            _title.Text = task.Title;
        }
    }

    private sealed class NativeTask : Microsoft.Maui.Controls.Grid
    {
        private readonly BoxView _bar = new() { WidthRequest = 6, CornerRadius = 3 };
        private readonly Label _title = new() { FontSize = 15, TextColor = DemoColors.Ink, VerticalTextAlignment = TextAlignment.Center, Margin = new Thickness(12, 0) };

        public NativeTask()
        {
            HeightRequest = RowHeight;
            MinimumWidthRequest = CardWidth;
            Background = Colors.White;
            ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)];
            Children.Add(_bar);
            SetColumn((BindableObject)_title, 1);
            Children.Add(_title);
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is not TaskItem task)
                return;
            _bar.Color = task.Color;
            _title.Text = task.Title;
        }
    }

    private sealed class SkiaHeader : SkUiLabel
    {
        public SkiaHeader()
        {
            FontSize = 13;
            FontAttributes = FontAttributes.Bold;
            TextColor = DemoColors.Caption;
            Padding = new Thickness(4, 12, 4, 4);
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            Text = (BindingContext as TaskGroup)?.Name ?? string.Empty;
        }
    }

    private sealed class NativeHeader : Label
    {
        public NativeHeader()
        {
            FontSize = 13;
            FontAttributes = FontAttributes.Bold;
            TextColor = DemoColors.Caption;
            Padding = new Thickness(4, 12, 4, 4);
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            Text = (BindingContext as TaskGroup)?.Name ?? string.Empty;
        }
    }
}
