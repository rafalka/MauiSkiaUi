using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>A binding source with one property, <see cref="Value"/>, that raises its change only when it changes.</summary>
internal sealed class BindingSource<T>(T value) : INotifyPropertyChanged
{
    private static readonly PropertyChangedEventArgs ValueChanged = new(nameof(Value));

    private T _value = value;

    public T Value
    {
        get => _value;
        set
        {
            if (EqualityComparer<T>.Default.Equals(_value, value))
                return;
            _value = value;
            PropertyChanged?.Invoke(this, ValueChanged);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Binds <paramref name="property"/> of <paramref name="target"/> to a new source holding <paramref name="value"/>.</summary>
    public static BindingSource<T> Bind(BindableObject target, BindableProperty property, T value, BindingMode mode = BindingMode.OneWay)
    {
        var source = new BindingSource<T>(value);
        target.SetBinding(property, new Binding(nameof(Value), mode, source: source));
        return source;
    }
}

/// <summary>
/// Issue #92's bug class in the input controls: a user change (a drag, a tap, typing, a choice) or a value the control derived
/// (a range clamp, the other view of a selection) is written as a handler value, as MAUI's controls write it, so a one-way
/// binding keeps applying afterwards. A plain <c>SetValue</c> would outrank the binding for good. Each test binds one-way,
/// makes the user change, then changes the source and checks the control follows. In the global collection: text input and
/// the expander's motion use process-wide state.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class OneWayBindingInputTests : IDisposable
{
    private static long _pointer = 920_000;

    private readonly IDisposable _dispatcher = SkUiTestHelpers.UseTestDispatcher();

    public void Dispose() => _dispatcher.Dispose();

    private static void Tap(SkUiView root, Point at)
    {
        var id = ++_pointer;
        var start = TimeSpan.FromSeconds(id);
        root.Touch(new(id, SkUiTouchAction.Pressed, at, start));
        root.Touch(new(id, SkUiTouchAction.Released, at, start + TimeSpan.FromMilliseconds(30)));
    }

    private static bool Perform(SkUiView control, SkUiSemanticsActions action) => ((ISkUiAccessibleNode)control).PerformSemanticsAction(action);

    private static SkUiVerticalStackLayout Arranged(SkUiView control, double width = 300, double height = 200)
    {
        var root = new SkUiVerticalStackLayout { Children = { control } };
        SkUiTestHelpers.Arrange(root, width, height);
        return root;
    }

    [Fact]
    public void SliderFollowsTheBindingAfterATapAndARangeClamp()
    {
        var slider = new SkUiSlider();
        var root = new SkUiContentView { Content = slider };
        SkUiTestHelpers.Arrange(root, 220, 32); // the thumb travels 10…210
        var source = BindingSource<double>.Bind(slider, SkUiSlider.ValueProperty, 0.1);
        Assert.Equal(0.1, slider.Value, 3);

        Tap(root, new Point(60, 16));
        Assert.Equal(0.25, slider.Value, 2);
        Assert.Equal(0.1, source.Value); // one-way: the source keeps its value
        source.Value = 0.75;
        Assert.Equal(0.75, slider.Value, 3);

        slider.Maximum = 0.5; // the value is clamped (derived), and the binding still applies
        Assert.Equal(0.5, slider.Value, 3);
        source.Value = 0.3;
        Assert.Equal(0.3, slider.Value, 3);

        Assert.True(Perform(slider, SkUiSemanticsActions.Increment)); // a screen reader
        Assert.NotEqual(0.3, slider.Value, 3);
        source.Value = 0.2;
        Assert.Equal(0.2, slider.Value, 3);
    }

    [Fact]
    public void SliderKeepsTheUsersValueWhenTheRangeGrows()
    {
        var slider = new SkUiSlider { Maximum = 10 };
        var root = new SkUiContentView { Content = slider };
        SkUiTestHelpers.Arrange(root, 220, 32); // the thumb travels 10…210
        BindingSource<double>.Bind(slider, SkUiSlider.ValueProperty, 15); // out of range: shown at 10, 15 requested
        Assert.Equal(10, slider.Value, 3);

        Tap(root, new Point(215, 16)); // the user picks the end: 10, the value it already holds
        Assert.Equal(10, slider.Value, 3);

        slider.Maximum = 20; // the user's 10 is the requested value now, not the binding's earlier 15
        Assert.Equal(10, slider.Value, 3);
    }

    [Fact]
    public void LabelCornerRadiusFollowsTheBindingAfterCornerRadiiChange()
    {
        var label = new SkUiLabel { Text = "chip" };
        var source = BindingSource<int>.Bind(label, SkUiLabel.CornerRadiusProperty, 4);
        Assert.Equal(4, label.CornerRadius);

        label.CornerRadii = new Microsoft.Maui.CornerRadius(10); // the uniform radius follows the corners (derived)
        Assert.Equal(10, label.CornerRadius);

        source.Value = 6;
        Assert.Equal(6, label.CornerRadius);
        Assert.Equal(new Microsoft.Maui.CornerRadius(6), label.CornerRadii);
    }

    [Fact]
    public void SliderStillWritesTapsToATwoWayBinding()
    {
        var slider = new SkUiSlider();
        var root = new SkUiContentView { Content = slider };
        SkUiTestHelpers.Arrange(root, 220, 32);
        var source = BindingSource<double>.Bind(slider, SkUiSlider.ValueProperty, 0.1, BindingMode.TwoWay);
        Tap(root, new Point(60, 16));
        Assert.Equal(0.25, source.Value, 2);
        source.Value = 0.75;
        Assert.Equal(0.75, slider.Value, 3);
    }

    [Fact]
    public void StepperFollowsTheBindingAfterAStepAndARangeChange()
    {
        var stepper = new SkUiStepper { Maximum = 10 };
        Arranged(stepper);
        var source = BindingSource<double>.Bind(stepper, SkUiStepper.ValueProperty, 2d);

        Assert.True(Perform(stepper, SkUiSemanticsActions.Increment));
        Assert.Equal(3, stepper.Value);
        source.Value = 5;
        Assert.Equal(5, stepper.Value);

        // The held value is the requested one, and the new range still moves it (then the binding applies again).
        stepper.Maximum = 4;
        Assert.Equal(4, stepper.Value);
        source.Value = 1;
        Assert.Equal(1, stepper.Value);
    }

    [Fact]
    public void TypedStepperFollowsTheBindingAfterAStepAndARangeChange()
    {
        var stepper = new SkUiStepper<int> { Maximum = 10 };
        Arranged(stepper);
        var source = BindingSource<int>.Bind(stepper, SkUiStepper<int>.ValueProperty, 2);

        Assert.True(Perform(stepper, SkUiSemanticsActions.Decrement));
        Assert.Equal(1, stepper.Value);
        source.Value = 7;
        Assert.Equal(7, stepper.Value);

        stepper.Maximum = 6;
        Assert.Equal(6, stepper.Value);
        source.Value = 3;
        Assert.Equal(3, stepper.Value);
    }

    [Fact]
    public void EntryTextAndCursorFollowTheBindingAfterTyping()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var entry = new SkUiEntry { FontFamily = SkUiTestHelpers.BundledFontFamily };
        Arranged(entry);
        var text = BindingSource<string?>.Bind(entry, SkUiInputView.TextProperty, "ab");
        Assert.True(entry.Focus());
        var cursor = BindingSource<int>.Bind(entry, SkUiInputView.CursorPositionProperty, 0);

        entry.Model.Editor.SelectAll();
        entry.Model.Editor.InsertText("typed");
        Assert.Equal("typed", entry.Text);
        Assert.Equal(5, entry.CursorPosition);
        Assert.Equal("ab", text.Value);

        text.Value = "from the app";
        Assert.Equal("from the app", entry.Text);
        cursor.Value = 2;
        Assert.Equal(2, entry.CursorPosition);

        entry.MaxLength = 4; // cut (derived)
        Assert.Equal("from", entry.Text);
        entry.MaxLength = int.MaxValue;
        text.Value = "again";
        Assert.Equal("again", entry.Text);
    }

    [Fact]
    public void NumericEntryValueFollowsTheBindingAfterTyping()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var entry = new SkUiNumericEntry { FontFamily = SkUiTestHelpers.BundledFontFamily, Culture = CultureInfo.GetCultureInfo("en-US") };
        Arranged(entry);
        var source = BindingSource<double?>.Bind(entry, SkUiNumericEntry<double>.ValueProperty, 1d);
        Assert.Equal(1, entry.Value);

        Assert.True(entry.Focus());
        entry.Model.Editor.SelectAll();
        entry.Model.Editor.InsertText("7", SkUiTextEditKind.Other);
        entry.Unfocus();
        Assert.Equal(7, entry.Value);
        Assert.Equal(1, source.Value);

        source.Value = 3;
        Assert.Equal(3, entry.Value);
        Assert.Equal("3", entry.Text);
    }

    [Fact]
    public void NumericEntryKeepsTheUsersValueWhenTheRangeGrows()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var entry = new SkUiNumericEntry { FontFamily = SkUiTestHelpers.BundledFontFamily, Culture = CultureInfo.GetCultureInfo("en-US"), Maximum = 10 };
        Arranged(entry);
        BindingSource<double?>.Bind(entry, SkUiNumericEntry<double>.ValueProperty, 15d); // out of range: shown at 10, 15 requested
        Assert.Equal(10, entry.Value);

        Assert.True(entry.Focus());
        entry.Model.Editor.SelectAll();
        entry.Model.Editor.InsertText("50", SkUiTextEditKind.Other); // clamped to 10, the value it already holds
        entry.Unfocus();
        Assert.Equal(10, entry.Value);

        entry.Maximum = 20; // the user's 10 is the requested value now, not the binding's earlier 15
        Assert.Equal(10, entry.Value);
    }

    [Fact]
    public void IndicatorPositionFollowsTheBindingAfterAStep()
    {
        var indicator = new SkUiIndicatorView { Count = 5 };
        Arranged(indicator, 300, 20);
        var source = BindingSource<int>.Bind(indicator, SkUiIndicatorView.PositionProperty, 1);

        Assert.True(Perform(indicator, SkUiSemanticsActions.Increment));
        Assert.Equal(2, indicator.Position);
        source.Value = 4;
        Assert.Equal(4, indicator.Position);

        indicator.Count = 3; // clamped to the last item (derived)
        Assert.Equal(2, indicator.Position);
        source.Value = 0;
        Assert.Equal(0, indicator.Position);
    }

    [Fact]
    public void ExpanderFollowsTheBindingAfterAHeaderTap()
    {
        var expander = new SkUiExpander { Header = new SkUiBox { HeightRequest = 20 }, Content = new SkUiBox { HeightRequest = 30 } };
        var root = new SkUiContentView { Content = new SkUiVerticalStackLayout { Children = { expander } } };
        SkUiTestHelpers.Arrange(root, 100, 200);
        var source = BindingSource<bool>.Bind(expander, SkUiExpander.IsExpandedProperty, false);

        Tap(root, new Point(10, 10));
        Assert.True(expander.IsExpanded);
        Assert.False(source.Value);
        source.Value = true;
        source.Value = false;
        Assert.False(expander.IsExpanded);
    }
}

/// <summary>Pickers through a recording presenter: a choice in the popup does not cut a one-way binding (issue #92's bug class).</summary>
[Collection(GlobalStateCollection.Name)]
public sealed class OneWayBindingPickerTests : IDisposable
{
    private static readonly string[] Monkeys = ["Baboon", "Capuchin Monkey", "Blue Monkey", "Squirrel Monkey", "Golden Lion Tamarin", "Howler Monkey"];

    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly IDisposable _dispatcher = SkUiTestHelpers.UseTestDispatcher();
    private readonly IDisposable _culture = DateTimeWheelsTests.UseCulture("en-US");
    private readonly RecordingPopupPresenter _presenter = new();
    private readonly ISkUiPopupPresenter? _previous = SkUiPopups.Presenter;

    public OneWayBindingPickerTests() => SkUiPopups.Presenter = _presenter;

    public void Dispose()
    {
        SkUiPopups.Presenter = _previous;
        _culture.Dispose();
        _dispatcher.Dispose();
        _ui.Dispose();
    }

    private static T Arranged<T>(T picker) where T : SkUiView
    {
        var stack = new SkUiVerticalStackLayout { Padding = new Thickness(10, 20), Children = { picker } };
        SkUiTestHelpers.Arrange(stack, 360, 700);
        return picker;
    }

    private static SkUiPicker Picker()
    {
        var picker = new SkUiPicker { Title = "Select a monkey" };
        foreach (var monkey in Monkeys)
            picker.Items.Add(monkey);
        return Arranged(picker);
    }

    private T Open<T>(SkUiPickerBase picker) where T : SkUiPopupRequest
    {
        picker.IsOpen = true;
        return Assert.IsType<T>(_presenter.Current);
    }

    [Fact]
    public void PickerSelectedIndexFollowsTheBindingAfterAChoice()
    {
        var picker = Picker();
        var source = BindingSource<int>.Bind(picker, SkUiPicker.SelectedIndexProperty, 0);

        Assert.True(Open<SkUiListPopupRequest>(picker).Complete(3));
        _ui.RunPending();
        Assert.Equal(3, picker.SelectedIndex);
        Assert.Equal(Monkeys[3], picker.SelectedItem);
        Assert.Equal(0, source.Value);

        source.Value = 1;
        Assert.Equal(1, picker.SelectedIndex);
        Assert.Equal(Monkeys[1], picker.SelectedItem);
    }

    [Fact]
    public void PickerSelectedItemFollowsTheBindingAfterAChoice()
    {
        var picker = Picker();
        var source = BindingSource<object?>.Bind(picker, SkUiPicker.SelectedItemProperty, Monkeys[0]);

        Assert.True(Open<SkUiListPopupRequest>(picker).Complete(3));
        _ui.RunPending();
        Assert.Equal(Monkeys[3], picker.SelectedItem);

        source.Value = Monkeys[5];
        Assert.Equal(Monkeys[5], picker.SelectedItem);
        Assert.Equal(5, picker.SelectedIndex);
    }

    [Fact]
    public void PickerSelectedIndexFollowsTheBindingAfterClearing()
    {
        var picker = Picker();
        var source = BindingSource<int>.Bind(picker, SkUiPicker.SelectedIndexProperty, 2);
        Assert.Equal(2, picker.SelectedIndex);

        picker.ClearSelection(); // the user's clear button
        Assert.Equal(-1, picker.SelectedIndex);
        Assert.Equal(2, source.Value);

        source.Value = 4;
        Assert.Equal(4, picker.SelectedIndex);
    }

    [Fact]
    public void PickerSelectedIndexStillReachesATwoWayBinding()
    {
        var picker = Picker();
        var source = BindingSource<int>.Bind(picker, SkUiPicker.SelectedIndexProperty, 0, BindingMode.TwoWay);
        Assert.True(Open<SkUiListPopupRequest>(picker).Complete(3));
        _ui.RunPending();
        Assert.Equal(3, source.Value);
        source.Value = 1;
        Assert.Equal(1, picker.SelectedIndex);
    }

    [Fact]
    public void IsOpenFollowsTheBindingAfterATap()
    {
        var picker = Picker();
        var source = BindingSource<bool>.Bind(picker, SkUiPickerBase.IsOpenProperty, false);

        Assert.NotNull(SkUiDiagnostics.SimulateTap(picker));
        Assert.True(picker.IsOpen);
        Assert.False(source.Value);

        source.Value = true;
        source.Value = false;
        Assert.False(picker.IsOpen);
        _ui.RunPending();
        Assert.False(picker.IsPopupShown);
    }

    [Fact]
    public void DatePickerFollowsTheBindingAfterAChoice()
    {
        var picker = Arranged(new SkUiDatePicker());
        var source = BindingSource<DateTime?>.Bind(picker, SkUiDatePicker.DateProperty, new DateTime(2026, 1, 10));

        Assert.True(Open<SkUiDatePopupRequest>(picker).CompleteDate(new DateTime(2026, 4, 30)));
        _ui.RunPending();
        Assert.Equal(new DateTime(2026, 4, 30), picker.Date);

        source.Value = new DateTime(2026, 2, 2);
        Assert.Equal(new DateTime(2026, 2, 2), picker.Date);

        picker.MaximumDate = new DateTime(2026, 1, 31); // moved into the range (derived)
        Assert.Equal(new DateTime(2026, 1, 31), picker.Date);
        source.Value = new DateTime(2026, 1, 5);
        Assert.Equal(new DateTime(2026, 1, 5), picker.Date);
    }

    [Fact]
    public void DateTimePickerFollowsTheBindingAfterAChoice()
    {
        var picker = Arranged(new SkUiDateTimePicker());
        var source = BindingSource<DateTime?>.Bind(picker, SkUiDateTimePicker.DateProperty, new DateTime(2026, 1, 10, 8, 0, 0));

        Assert.True(Open<SkUiDatePopupRequest>(picker).CompleteDate(new DateTime(2026, 4, 30, 9, 30, 0)));
        _ui.RunPending();
        Assert.Equal(new DateTime(2026, 4, 30, 9, 30, 0), picker.Date);

        source.Value = new DateTime(2026, 2, 2, 7, 0, 0);
        Assert.Equal(new DateTime(2026, 2, 2, 7, 0, 0), picker.Date);
    }

    [Fact]
    public void TimePickerFollowsTheBindingAfterAChoice()
    {
        var picker = Arranged(new SkUiTimePicker());
        var source = BindingSource<TimeSpan?>.Bind(picker, SkUiTimePicker.TimeProperty, new TimeSpan(8, 0, 0));

        Assert.True(Open<SkUiTimePopupRequest>(picker).CompleteTime(new TimeSpan(9, 30, 0)));
        _ui.RunPending();
        Assert.Equal(new TimeSpan(9, 30, 0), picker.Time);

        source.Value = new TimeSpan(7, 15, 0);
        Assert.Equal(new TimeSpan(7, 15, 0), picker.Time);
    }

    [Fact]
    public void PickerColumnFollowsTheBindingAfterItsItemMoves()
    {
        var hours = new ObservableCollection<string> { "00", "01", "02", "03" };
        var hour = new SkUiPickerColumn { ItemsSource = hours };
        var source = BindingSource<int>.Bind(hour, SkUiPickerColumn.SelectedIndexProperty, 1);
        Assert.Equal("01", hour.SelectedItem);

        hours.Insert(0, "23"); // the chosen item moves to index 2 (derived)
        Assert.Equal(2, hour.SelectedIndex);
        Assert.Equal("01", hour.SelectedItem);

        source.Value = 3;
        Assert.Equal(3, hour.SelectedIndex);
        Assert.Equal("02", hour.SelectedItem);
    }

    [Fact]
    public void PickerColumnFollowsTheBindingAfterAChoice()
    {
        string[] hours = ["00", "01", "02", "03", "04", "05"], minutes = ["00", "15", "30", "45"];
        var picker = new SkUiMultiColumnPicker { Title = "Time" };
        var hour = new SkUiPickerColumn { ItemsSource = hours };
        picker.Columns.Add(hour);
        picker.Columns.Add(new SkUiPickerColumn { ItemsSource = minutes });
        Arranged(picker);
        var source = BindingSource<int>.Bind(hour, SkUiPickerColumn.SelectedIndexProperty, 1);

        Assert.True(Open<SkUiColumnsPopupRequest>(picker).Complete([3, 0]));
        _ui.RunPending();
        Assert.Equal(3, hour.SelectedIndex);
        Assert.Equal(hours[3], hour.SelectedItem);

        source.Value = 5;
        Assert.Equal(5, hour.SelectedIndex);
        Assert.Equal(hours[5], hour.SelectedItem);
    }
}

/// <summary>Items views, refresh and radio groups: a user change does not cut a one-way binding (issue #92's bug class).</summary>
[Collection(RuntimeXamlCollection.Name)]
public sealed class OneWayBindingItemsTests : IDisposable
{
    private static long _pointer = 930_000;

    private readonly IDisposable _dispatcher = SkUiTestHelpers.UseTestDispatcher();

    public void Dispose() => _dispatcher.Dispose();

    private static ObservableCollection<string> Items(int count) => [.. Enumerable.Range(0, count).Select(index => $"Item {index}")];

    private static DataTemplate Rows(double height) => new(() => new SkUiBox { HeightRequest = height, Color = Colors.Blue });

    private static void Tap(SkUiView root, Point at)
    {
        var id = ++_pointer;
        var start = TimeSpan.FromSeconds(id);
        root.Touch(new(id, SkUiTouchAction.Pressed, at, start));
        root.Touch(new(id, SkUiTouchAction.Released, at, start + TimeSpan.FromMilliseconds(20)));
    }

    [Fact]
    public void CollectionViewSelectionFollowsTheBindingAfterATap()
    {
        var items = Items(20);
        var list = new SkUiCollectionView
        {
            ItemsSource = items, ItemTemplate = Rows(50), SelectionMode = SkUiSelectionMode.Single,
            VerticalScrollBarVisibility = ScrollBarVisibility.Never, Overscroll = SkUiOverscrollMode.None
        };
        SkUiTestHelpers.Arrange(list, 300, 500);
        var source = BindingSource<object?>.Bind(list, SkUiCollectionView.SelectedItemProperty, items[0]);

        Tap(list, new Point(100, 125)); // item 2
        Assert.Same(items[2], list.SelectedItem);
        Assert.Same(items[0], source.Value);
        source.Value = items[4];
        Assert.Same(items[4], list.SelectedItem);

        list.SelectionMode = SkUiSelectionMode.None; // clears the selection (derived)
        Assert.Null(list.SelectedItem);
        list.SelectionMode = SkUiSelectionMode.Single;
        source.Value = items[1];
        Assert.Same(items[1], list.SelectedItem);
    }

    [Fact]
    public void LoadMoreFollowsTheBindingAfterTheListLoads()
    {
        var rows = Items(2);
        var list = new SkUiCollectionView
        {
            ItemsSource = rows, ItemTemplate = Rows(50), LoadMoreMode = SkUiLoadMoreMode.Auto, LoadMoreCommand = new Command(_ => { })
        };
        var source = BindingSource<bool>.Bind(list, SkUiCollectionView.IsLoadMoreActiveProperty, false);
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.True(list.IsLoadMoreActive); // the end shows: the list loads more
        Assert.False(source.Value);

        source.Value = true;
        source.Value = false;
        Assert.False(list.IsLoadMoreActive);

        rows.Add("Item 2");
        SkUiTestHelpers.Arrange(list, 300, 500);
        Assert.True(list.IsLoadMoreActive); // and again
        source.Value = true;
        source.Value = false;
        Assert.False(list.IsLoadMoreActive);
    }

    [Fact]
    public void CollectionViewRefreshFollowsTheBindingAfterAnAutomaticRefresh()
    {
        var runs = 0;
        var list = new SkUiCollectionView
        {
            ItemsSource = Items(5), ItemTemplate = Rows(50), IsPullToRefreshEnabled = true,
            RefreshCompletion = SkUiRefreshCompletion.Automatic, RefreshCommand = new Command(_ => runs++)
        };
        SkUiTestHelpers.Arrange(list, 300, 500);
        var source = BindingSource<bool>.Bind(list, SkUiCollectionView.IsRefreshingProperty, false);

        source.Value = true; // the command ran at once: the refresh ends (written by the list)
        Assert.Equal(1, runs);
        Assert.False(list.IsRefreshing);
        source.Value = false;
        source.Value = true;
        Assert.Equal(2, runs);
    }

    [Fact]
    public void CarouselPositionFollowsTheBindingAfterASwipe()
    {
        var carousel = new SkUiCarouselView { ItemsSource = Items(5), Loop = false, ItemTemplate = Rows(200) };
        using var surface = new SkUiTestSurface(carousel, 300, 200);
        surface.Frame(0);
        var source = BindingSource<int>.Bind(carousel, SkUiCarouselView.PositionProperty, 0);

        var id = ++_pointer;
        carousel.Touch(new(id, SkUiTouchAction.Pressed, new Point(250, 100), TimeSpan.Zero));
        carousel.Touch(new(id, SkUiTouchAction.Moved, new Point(183, 100), TimeSpan.FromMilliseconds(10)));
        carousel.Touch(new(id, SkUiTouchAction.Moved, new Point(50, 100), TimeSpan.FromMilliseconds(20)));
        carousel.Touch(new(id, SkUiTouchAction.Released, new Point(50, 100), TimeSpan.FromMilliseconds(25)));
        for (var ms = 30d; ms <= 2000; ms += 16)
            surface.Frame(ms);
        Assert.Equal(1, carousel.Position);
        Assert.Equal(0, source.Value);

        source.Value = 3;
        Assert.Equal(3, carousel.Position);
        Assert.Equal("Item 3", carousel.CurrentItem);
    }

    [Fact]
    public void CarouselCurrentItemFollowsTheBindingAfterAScroll()
    {
        var items = Items(5);
        var carousel = new SkUiCarouselView { ItemsSource = items, Loop = false, ItemTemplate = Rows(200) };
        using var surface = new SkUiTestSurface(carousel, 300, 200);
        surface.Frame(0);
        var source = BindingSource<object?>.Bind(carousel, SkUiCarouselView.CurrentItemProperty, items[0]);

        _ = carousel.ScrollToIndex(2, animated: false);
        Assert.Equal(items[2], carousel.CurrentItem);
        source.Value = items[4];
        Assert.Equal(4, carousel.Position);
        Assert.Equal(items[4], carousel.CurrentItem);
    }

    [Fact]
    public void RefreshViewFollowsTheBindingAfterAPull()
    {
        var refresh = new SkUiRefreshView
        {
            Content = new SkUiScrollView { Content = new SkUiBox { HeightRequest = 1000 }, Overscroll = SkUiOverscrollMode.None }
        };
        SkUiTestHelpers.Arrange(refresh, 300, 400);
        var source = BindingSource<bool>.Bind(refresh, SkUiRefreshView.IsRefreshingProperty, false);

        var id = ++_pointer;
        refresh.Touch(new(id, SkUiTouchAction.Pressed, new Point(150, 50), TimeSpan.Zero));
        for (var step = 1; step <= 6; step++)
            refresh.Touch(new(id, SkUiTouchAction.Moved, new Point(150, 50 + 50 * step), TimeSpan.FromMilliseconds(step * 100)));
        refresh.Touch(new(id, SkUiTouchAction.Released, new Point(150, 350), TimeSpan.FromMilliseconds(900)));
        Assert.True(refresh.IsRefreshing);
        Assert.False(source.Value);

        source.Value = true;
        source.Value = false;
        Assert.False(refresh.IsRefreshing);

        source.Value = true;
        refresh.IsRefreshEnabled = false; // ends the refresh (derived)
        Assert.False(refresh.IsRefreshing);
        refresh.IsRefreshEnabled = true;
        source.Value = false;
        source.Value = true;
        Assert.True(refresh.IsRefreshing);
    }

    [Fact]
    public void RadioIsCheckedFollowsTheBindingAfterTheGroupUnchecksIt()
    {
        SkUiRadioButton first = new() { Value = "A", GroupName = "letters" }, second = new() { Value = "B", GroupName = "letters" };
        var root = new SkUiContentView { Content = new SkUiVerticalStackLayout { Children = { first, second } } };
        SkUiTestHelpers.Arrange(root, 200, 200);
        var source = BindingSource<bool>.Bind(first, SkUiToggleControl.IsCheckedProperty, true);
        Assert.True(first.IsChecked);

        Assert.True(SkUiDiagnostics.SimulateTap(second, releaseOnlyIfTap: true).IsTap); // checking B unchecks A (derived)
        Assert.False(first.IsChecked);
        Assert.True(source.Value);

        source.Value = false;
        source.Value = true;
        Assert.True(first.IsChecked);
        Assert.False(second.IsChecked);
    }

    [Fact]
    public void IndicatorPositionFollowsTheBindingAfterTheCarouselMoves()
    {
        var indicator = new SkUiIndicatorView();
        var carousel = new SkUiCarouselView { ItemsSource = Items(5), Loop = false, ItemTemplate = Rows(200), IndicatorView = indicator };
        using var surface = new SkUiTestSurface(new SkUiVerticalStackLayout { Children = { carousel, indicator } }, 300, 260);
        surface.Frame(0);
        var source = BindingSource<int>.Bind(indicator, SkUiIndicatorView.PositionProperty, 0);

        carousel.Position = 2; // the linked indicator follows (derived)
        Assert.Equal(2, indicator.Position);
        Assert.Equal(0, source.Value);

        source.Value = 4;
        Assert.Equal(4, indicator.Position);
    }

    [Fact]
    public void RadioGroupSelectedValueFollowsTheBindingAfterATap()
    {
        SkUiRadioButton red = new() { Value = "Red" }, green = new() { Value = "Green" }, blue = new() { Value = "Blue" };
        var layout = new SkUiVerticalStackLayout { Children = { red, green, blue } };
        RadioButtonGroup.SetGroupName(layout, "colors");
        var root = new SkUiContentView { Content = layout };
        SkUiTestHelpers.Arrange(root, 200, 200);
        var source = BindingSource<object?>.Bind(layout, RadioButtonGroup.SelectedValueProperty, "Green");
        Assert.True(green.IsChecked);

        Assert.NotNull(SkUiDiagnostics.SimulateTap(red));
        Assert.True(red.IsChecked);
        Assert.Equal("Red", RadioButtonGroup.GetSelectedValue(layout));
        Assert.Equal("Green", source.Value);

        source.Value = "Blue";
        Assert.True(blue.IsChecked);
        Assert.False(red.IsChecked);
        Assert.Equal("Blue", RadioButtonGroup.GetSelectedValue(layout));
    }
}
