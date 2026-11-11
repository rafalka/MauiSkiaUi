using System.Collections.ObjectModel;
using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// Animated item changes (<see cref="SkUiCollectionView.AnimatesItemChanges"/>) next to MAUI's <see cref="CollectionView"/>, whose
/// platform lists animate by default: both show the same notes, so an insert, removal or move shows in both.
/// </summary>
public sealed class CollectionViewAnimationsDemoPage : ComponentDemoPage
{
    private const string List = "List";
    private const string GridLayout = "Grid, 2";
    private const double RowHeight = 52;

    private static readonly string[] Words = ["Milk", "Bread", "Coffee", "Apples", "Rice", "Cheese", "Tea", "Eggs", "Honey", "Pasta", "Lemons", "Butter"];

    private readonly SkUiCollectionView _skia;
    private readonly CollectionView _native;
    private readonly ObservableCollection<Note> _notes = [];
    private readonly Random _random = new(7);
    private string _layout = List;
    private int _next;

    public CollectionViewAnimationsDemoPage() : base("SkUiCollectionView (animated changes)", new SkUiCollectionView(), new CollectionView(),
        widthRange: (160, 400, 300), heightRange: (200, 640, 420))
    {
        _skia = (SkUiCollectionView)SkiaControl;
        _native = (CollectionView)NativeControl!;
        Fill();
        _skia.ItemsSource = _notes;
        _native.ItemsSource = _notes;
        _skia.ItemTemplate = new DataTemplate(() => new SkiaNote());
        _native.ItemTemplate = new DataTemplate(() => new NativeNote());
        _skia.AnimatesItemChanges = true;
        ApplyLayout();

        Toggle(nameof(SkUiCollectionView.AnimatesItemChanges), true, value => _skia.AnimatesItemChanges = value, () => _skia.AnimatesItemChanges == true);
        Choice(nameof(SkUiCollectionView.ItemsLayout), [List, GridLayout], List, value => { _layout = value; ApplyLayout(); }, () => _layout);
        ActionButton("Insert", () => _notes.Insert(Math.Min(_notes.Count, 1 + _random.Next(Math.Min(4, _notes.Count + 1))), NewNote()));
        ActionButton("Remove", () =>
        {
            if (_notes.Count > 0)
                _notes.RemoveAt(_random.Next(Math.Min(5, _notes.Count)));
        });
        ActionButton("Move", () =>
        {
            if (_notes.Count > 1)
                _notes.Move(_random.Next(Math.Min(6, _notes.Count)), _random.Next(Math.Min(6, _notes.Count)));
        });
        ActionButton("Shuffle the first eight", () =>
        {
            for (var step = 0; step < 6 && _notes.Count > 1; step++)
                _notes.Move(_random.Next(Math.Min(8, _notes.Count)), _random.Next(Math.Min(8, _notes.Count)));
        });
        ActionButton("Reset", Fill);
    }

    private void Fill()
    {
        _notes.Clear();
        _next = 0;
        for (var index = 0; index < 16; index++)
            _notes.Add(NewNote());
    }

    private Note NewNote()
    {
        var index = _next++;
        return new Note($"{Words[index % Words.Length]} {index + 1}", index % 3 == 0 ? Accent : index % 3 == 1 ? DemoColors.SampleA : DemoColors.SampleB);
    }

    private void ApplyLayout()
    {
        ItemsLayout layout = _layout == GridLayout
            ? new GridItemsLayout(2, ItemsLayoutOrientation.Vertical) { HorizontalItemSpacing = 4, VerticalItemSpacing = 4 }
            : new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 4 };
        _skia.ItemsLayout = layout;
        _native.ItemsLayout = layout;
    }

    private sealed record Note(string Text, Color Color);

    private sealed class SkiaNote : SkUiGrid
    {
        private readonly SkUiBox _bar = new() { WidthRequest = 6, CornerRadius = 3 };
        private readonly SkUiLabel _text = new() { FontSize = 15, TextColor = DemoColors.Ink, VerticalTextAlignment = TextAlignment.Center, Margin = new Thickness(12, 0) };

        public SkiaNote()
        {
            HeightRequest = RowHeight;
            Background = Colors.White;
            ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)];
            Children.Add(_bar);
            Microsoft.Maui.Controls.Grid.SetColumn((BindableObject)_text, 1);
            Children.Add(_text);
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is not Note note)
                return;
            _bar.Color = note.Color;
            _text.Text = note.Text;
        }
    }

    private sealed class NativeNote : Microsoft.Maui.Controls.Grid
    {
        private readonly BoxView _bar = new() { WidthRequest = 6, CornerRadius = 3 };
        private readonly Label _text = new() { FontSize = 15, TextColor = DemoColors.Ink, VerticalTextAlignment = TextAlignment.Center, Margin = new Thickness(12, 0) };

        public NativeNote()
        {
            HeightRequest = RowHeight;
            Background = Colors.White;
            ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)];
            Children.Add(_bar);
            SetColumn((BindableObject)_text, 1);
            Children.Add(_text);
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is not Note note)
                return;
            _bar.Color = note.Color;
            _text.Text = note.Text;
        }
    }
}
