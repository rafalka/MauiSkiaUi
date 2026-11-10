using System.Collections.ObjectModel;
using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// Keyboard navigation and extended selection on <see cref="SkUiCollectionView"/> next to MAUI's <see cref="CollectionView"/>
/// over the same files: Tab into the list (one tab stop), arrows / Home / End / Page keys move the current item with the focus
/// ring, Space / Enter select it; with extended selection, clicks select as desktop lists (Ctrl / Command and Shift).
/// </summary>
public sealed class CollectionViewKeyboardDemoPage : ComponentDemoPage
{
    private const string None = "None";
    private const string Single = "Single";
    private const string Multiple = "Multiple";
    private const string List = "List";
    private const string GridLayout = "Grid, 3";
    private const double RowHeight = 48;

    private static readonly string[] Kinds = ["Report", "Invoice", "Photo", "Notes", "Slides", "Budget", "Contract", "Draft"];
    // The accent at 18 % over white: opaque, as the rows' white background it replaces.
    private static readonly Color SelectedFill = Color.FromRgb(1 - 0.18f * (1 - DemoColors.Accent.Red), 1 - 0.18f * (1 - DemoColors.Accent.Green),
        1 - 0.18f * (1 - DemoColors.Accent.Blue));

    private readonly SkUiCollectionView _skia;
    private readonly CollectionView _native;
    private readonly ObservableCollection<FileItem> _files = [];
    private readonly ObservableCollection<FileGroup> _groups = [];
    private string _mode = Multiple;
    private string _layout = List;
    private bool _grouped;

    public CollectionViewKeyboardDemoPage() : base("SkUiCollectionView (keyboard and extended selection)", new SkUiCollectionView(), new CollectionView(),
        widthRange: (160, 400, 300), heightRange: (200, 640, 420))
    {
        _skia = (SkUiCollectionView)SkiaControl;
        _native = (CollectionView)NativeControl!;
        for (var index = 0; index < 60; index++)
            _files.Add(new FileItem($"{Kinds[index % Kinds.Length]} {index + 1}", index % 3 == 0 ? Accent : index % 3 == 1 ? DemoColors.SampleA : DemoColors.SampleB));
        _groups.Add(new FileGroup("This week", _files.Take(12)));
        _groups.Add(new FileGroup("Earlier", _files.Skip(12)));
        _skia.ItemTemplate = new DataTemplate(() => new SkiaFile());
        _skia.GroupHeaderTemplate = new DataTemplate(() => new SkiaHeader());
        _skia.IsExtendedSelectionEnabled = true;
        _skia.AllowGroupExpandCollapse = true;
        _skia.SelectionChanged += (_, _) => Status();
        _skia.ItemTapped += (_, args) => Feedback($"Tapped {((FileItem)args.Item!).Name} · {Selected(_skia.SelectedItems, _skia.SelectedItem)}");
        _native.ItemTemplate = new DataTemplate(() => new NativeFile());
        _native.GroupHeaderTemplate = new DataTemplate(() => new NativeHeader());
        _native.SelectionChanged += (_, _) => Status();
        Apply();

        AddEditor("Keys", Caption("Tab into the list: it is one tab stop. Arrows, Home / End and Page Up / Down move the current item "
            + "(the focus ring); Space / Enter select it. Group headers: Left / Right collapse and expand. Extended selection: click, "
            + "Ctrl / Command+click, Shift+click; arrows keep the selection, Space toggles, Insert toggles and moves on, Shift+arrows "
            + "toggle a range, Enter opens (taps) without selecting, Ctrl / Command+A selects all."), property: false);
        Choice(nameof(SkUiCollectionView.SelectionMode), [None, Single, Multiple], Multiple, value => { _mode = value; Apply(); }, () => _mode);
        Toggle(nameof(SkUiCollectionView.IsExtendedSelectionEnabled), true, value => _skia.IsExtendedSelectionEnabled = value,
            () => _skia.IsExtendedSelectionEnabled);
        Choice(nameof(SkUiCollectionView.ItemsLayout), [List, GridLayout], List, value => { _layout = value; Apply(); }, () => _layout);
        Toggle(nameof(SkUiCollectionView.IsGrouped), false, value => { _grouped = value; Apply(); }, () => _skia.IsGrouped, () => _native.IsGrouped);
        ActionButton("Clear the selection", () => { _skia.ClearSelection(); _native.SelectedItems?.Clear(); _native.SelectedItem = null; });
    }

    private void Apply()
    {
        var (skiaMode, nativeMode) = _mode switch
        {
            None => (SkUiSelectionMode.None, SelectionMode.None),
            Single => (SkUiSelectionMode.Single, SelectionMode.Single),
            _ => (SkUiSelectionMode.Multiple, SelectionMode.Multiple)
        };
        _skia.SelectionMode = skiaMode;
        _native.SelectionMode = nativeMode;
        ItemsLayout layout = _layout == GridLayout
            ? new GridItemsLayout(3, ItemsLayoutOrientation.Vertical) { HorizontalItemSpacing = 4, VerticalItemSpacing = 4 }
            : new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 2 };
        _skia.ItemsLayout = layout;
        _native.ItemsLayout = layout;
        _skia.IsGrouped = _grouped;
        _native.IsGrouped = _grouped;
        System.Collections.IEnumerable source = _grouped ? _groups : _files;
        _skia.ItemsSource = source;
        _native.ItemsSource = source;
        Status();
    }

    private void Status() => Feedback(Selected(_skia.SelectedItems, _skia.SelectedItem), Selected(_native.SelectedItems, _native.SelectedItem));

    private static string Selected(IList<object>? items, object? item) =>
        items is { Count: > 0 } ? $"{items.Count} selected" : item is FileItem file ? $"Selected {file.Name}" : "Nothing selected";

    /// <summary>
    /// The row's root is opaque, so it covers the selection background the list draws behind it (on both lists, as in MAUI):
    /// its <c>Selected</c> visual state shows the selection instead. <c>PointerOver</c> looks as <c>Normal</c>: without it,
    /// MAUI leaves a row deselected under the pointer in <c>Selected</c>.
    /// </summary>
    private static void ShowSelection(VisualElement root) =>
        VisualStateManager.SetVisualStateGroups(root, [new VisualStateGroup
        {
            Name = nameof(VisualStateManager.CommonStates),
            States =
            {
                new VisualState { Name = VisualStateManager.CommonStates.Normal },
                new VisualState { Name = VisualStateManager.CommonStates.PointerOver },
                new VisualState
                {
                    Name = VisualStateManager.CommonStates.Selected,
                    Setters = { new Setter { Property = VisualElement.BackgroundProperty, Value = new SolidColorBrush(SelectedFill) } }
                }
            }
        }]);

    private sealed record FileItem(string Name, Color Color);

    private sealed class FileGroup(string name, IEnumerable<FileItem> files) : ObservableCollection<FileItem>(files)
    {
        public string Name { get; } = name;
    }

    private sealed class SkiaFile : SkUiGrid
    {
        private readonly SkUiBox _icon = new() { WidthRequest = 24, HeightRequest = 24, CornerRadius = 6, Margin = new Thickness(10, 0) };
        private readonly SkUiLabel _name = new() { FontSize = 15, TextColor = DemoColors.Ink, VerticalTextAlignment = TextAlignment.Center };

        public SkiaFile()
        {
            HeightRequest = RowHeight;
            Background = Colors.White;
            ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)];
            Children.Add(_icon);
            Microsoft.Maui.Controls.Grid.SetColumn((BindableObject)_name, 1);
            Children.Add(_name);
            ShowSelection(this);
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is not FileItem file)
                return;
            _icon.Color = file.Color;
            _name.Text = file.Name;
        }
    }

    private sealed class NativeFile : Microsoft.Maui.Controls.Grid
    {
        private readonly BoxView _icon = new() { WidthRequest = 24, HeightRequest = 24, CornerRadius = 6, Margin = new Thickness(10, 0) };
        private readonly Label _name = new() { FontSize = 15, TextColor = DemoColors.Ink, VerticalTextAlignment = TextAlignment.Center };

        public NativeFile()
        {
            HeightRequest = RowHeight;
            Background = Colors.White;
            ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)];
            Children.Add(_icon);
            SetColumn((BindableObject)_name, 1);
            Children.Add(_name);
            ShowSelection(this);
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is not FileItem file)
                return;
            _icon.Color = file.Color;
            _name.Text = file.Name;
        }
    }

    private sealed class SkiaHeader : SkUiLabel
    {
        public SkiaHeader()
        {
            FontSize = 13;
            FontAttributes = FontAttributes.Bold;
            TextColor = DemoColors.Caption;
            Padding = new Thickness(8, 12, 8, 4);
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            Text = (BindingContext as FileGroup)?.Name ?? string.Empty;
        }
    }

    private sealed class NativeHeader : Label
    {
        public NativeHeader()
        {
            FontSize = 13;
            FontAttributes = FontAttributes.Bold;
            TextColor = DemoColors.Caption;
            Padding = new Thickness(8, 12, 8, 4);
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            Text = (BindingContext as FileGroup)?.Name ?? string.Empty;
        }
    }
}
