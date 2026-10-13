using System.Collections.ObjectModel;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// The multi-column picker: columns and their selection rules, the field's text, the wheel popup (request, choosing by
/// scrolling, tapping and keys, staying open, following the app), through a recording presenter.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class MultiColumnPickerTests : IDisposable
{
    private static readonly string[] Hours = ["00", "01", "02", "03", "04", "05", "06", "07", "08", "09", "10", "11"];
    private static readonly string[] Minutes = ["00", "15", "30", "45"];
    private static readonly string[] Parts = ["AM", "PM"];

    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly RecordingPopupPresenter _presenter = new();
    private readonly ISkUiPopupPresenter? _previous = SkUiPopups.Presenter;

    public MultiColumnPickerTests() => SkUiPopups.Presenter = _presenter;

    public void Dispose()
    {
        SkUiPopups.Presenter = _previous;
        _ui.Dispose();
    }

    private static SkUiMultiColumnPicker TimePicker(bool arrange = true)
    {
        var picker = new SkUiMultiColumnPicker { Title = "Time", Separator = ":" };
        picker.Columns.Add(new SkUiPickerColumn { ItemsSource = Hours, Loop = true });
        picker.Columns.Add(new SkUiPickerColumn { ItemsSource = Minutes, Loop = true });
        picker.Columns.Add(new SkUiPickerColumn { ItemsSource = Parts });
        if (arrange)
            Arrange(picker);
        return picker;
    }

    private static void Arrange(SkUiMultiColumnPicker picker)
    {
        var stack = new SkUiVerticalStackLayout { Padding = new Thickness(10, 20) };
        stack.Children.Add(picker);
        SkUiTestHelpers.Arrange(stack, 360, 700);
    }

    private static string? FieldText(SkUiMultiColumnPicker picker) =>
        SkUiSemanticsTree.Build((SkUiView)picker.Parent!).Flatten().Single(node => ReferenceEquals(node.Source, picker)).Value;

    private SkUiColumnsPopupRequest Shown() => Assert.IsType<SkUiColumnsPopupRequest>(_presenter.Current);

    private SkUiColumnsPopup Open(SkUiMultiColumnPicker picker, out SkUiColumnsPopupRequest request)
    {
        picker.IsOpen = true;
        request = Shown();
        var content = Assert.IsType<SkUiColumnsPopup>(request.Content);
        return content;
    }

    [Fact]
    public void TheFieldShowsTheChosenItemsJoined()
    {
        var picker = TimePicker();
        Assert.Null(FieldText(picker)); // nothing chosen: the title shows
        Assert.Equal([-1, -1, -1], picker.SelectedIndices);

        picker.Columns[0].SelectedIndex = 7;
        picker.Columns[1].SelectedItem = "30";
        Assert.Equal("07:30", FieldText(picker));

        picker.Columns[2].SelectedIndex = 1;
        Assert.Equal("07:30:PM", FieldText(picker));
        picker.Format = "{0}:{1} {2}";
        Assert.Equal("07:30 PM", FieldText(picker));
        Assert.Equal([7, 2, 1], picker.SelectedIndices);
        Assert.Equal(["07", "30", "PM"], picker.SelectedItems.Cast<string>());
    }

    [Fact]
    public void ChangesRaiseTheChangedColumn()
    {
        var picker = TimePicker();
        var changes = new List<(int Column, int Previous, int Current)>();
        picker.SelectionChanged += (_, args) => changes.Add((args.ColumnIndex, args.PreviousIndex, args.CurrentIndex));

        picker.Columns[1].SelectedIndex = 2;
        picker.Columns[0].SelectedIndex = 4;
        picker.Columns[1].SelectedIndex = 2; // unchanged

        Assert.Equal([(1, -1, 2), (0, -1, 4)], changes);
    }

    [Fact]
    public void ARangeUpToTheLargestNumberEnds()
    {
        var column = SkUiPickerColumn.Range(int.MaxValue - 2, int.MaxValue, 2);
        Assert.Equal([int.MaxValue - 2, int.MaxValue], column.ItemsSource!.Cast<int>());
        Assert.Equal([int.MaxValue], SkUiPickerColumn.Range(int.MaxValue, int.MaxValue).ItemsSource!.Cast<int>());
    }

    [Fact]
    public void APickerWithoutColumnsOpens()
    {
        var picker = new SkUiMultiColumnPicker { Title = "Nothing yet" };
        Arrange(picker);
        var content = Open(picker, out var request);
        Assert.Empty(request.Columns);
        Assert.True(content.PreferredSize(200, 400).Height >= 0);
    }

    [Fact]
    public void AColumnsSelectionFollowsTheRulesOfThePicker()
    {
        var column = new SkUiPickerColumn { SelectedIndex = 3 }; // before the items: kept
        Assert.Equal(3, column.SelectedIndex);
        column.ItemsSource = Minutes.ToList();
        Assert.Equal(3, column.SelectedIndex);
        Assert.Equal("45", column.SelectedItem);

        column.SelectedIndex = 9; // beyond the items: the last
        Assert.Equal(3, column.SelectedIndex);
        column.SelectedItem = "not there";
        Assert.Equal(-1, column.SelectedIndex);
        Assert.Null(column.SelectedItem);

        var late = new SkUiPickerColumn { SelectedItem = "30" }; // an item before the items: found when they come
        late.ItemsSource = Minutes;
        Assert.Equal(2, late.SelectedIndex);
    }

    [Fact]
    public void AChosenItemStaysChosenWhereItMoves()
    {
        var source = new ObservableCollection<string>(Minutes);
        var column = new SkUiPickerColumn { ItemsSource = source, SelectedItem = "30" };

        source.Insert(0, "-15");
        Assert.Equal(3, column.SelectedIndex);
        Assert.Equal("30", column.SelectedItem);
        source.Remove("30");
        Assert.Equal(3, column.SelectedIndex);
        Assert.Equal("45", column.SelectedItem);
    }

    [Fact]
    public void OpeningHandsThePresenterAColumnsRequest()
    {
        var picker = TimePicker();
        picker.Columns[0].SelectedIndex = 5;
        picker.Columns[2].SelectedIndex = 1;
        picker.PopupPlacement = SkUiPopupPlacement.Anchored;

        var content = Open(picker, out var request);

        Assert.Same(picker, request.Anchor);
        Assert.Equal([Hours, Minutes, Parts], request.Columns);
        Assert.Equal([true, true, false], request.Loops);
        Assert.Equal([5, -1, 1], request.SelectedIndices);
        Assert.Equal(SkUiPopupPlacement.Anchored, request.Placement);
        Assert.True(picker.IsOpen);
        Assert.Equal(request.Bounds.Width, content.WidthRequest);
        Assert.Equal(5, content.Wheel(0).Position);
        Assert.Equal(1, content.Wheel(2).Position);
        Assert.True(request.PreferredSize.Height >= 5 * SkUiLook.Current.PopupItemHeight);
    }

    [Fact]
    public void ScrollingAWheelChoosesAtOnceAndTheListStaysOpen()
    {
        var picker = TimePicker();
        var content = Open(picker, out var request);
        var events = new List<string>();
        picker.SelectionChanged += (_, args) => events.Add($"{args.ColumnIndex}->{args.CurrentIndex}");
        picker.Closed += (_, _) => events.Add("Closed");

        content.Wheel(1).Position = 3;
        content.Wheel(0).Position = 8;

        // The columns with nothing chosen take what their wheels show (the first items): never half a time.
        Assert.True(picker.IsOpen);
        Assert.Equal([8, 3, 0], picker.SelectedIndices);
        Assert.Equal([8, 3, 0], request.SelectedIndices);
        Assert.Equal(["0->0", "1->3", "2->0", "0->8"], events);
        Assert.Equal("08:45:AM", FieldText(picker));

        // A dismissal keeps what was chosen.
        _presenter.Dismiss();
        _ui.RunPending();
        Assert.False(picker.IsOpen);
        Assert.Equal([8, 3, 0], picker.SelectedIndices);
        Assert.Equal(["0->0", "1->3", "2->0", "0->8", "Closed"], events);
    }

    [Fact]
    public void ATapChoosesAnItemEvenWhenItsWheelAlreadyShowsIt()
    {
        var picker = TimePicker();
        var content = Open(picker, out var request);
        using var surface = new SkUiTestSurface(content, 360, 400);
        surface.Frame();
        // Column 2 has nothing chosen and shows "AM" in the middle row; tapping it chooses it (and what the other wheels show).
        var item = Assert.IsAssignableFrom<SkUiView>(content.Wheel(2).GetRealizedView(0));

        Assert.NotNull(SkUiDiagnostics.SimulateTap(item));
        _ui.RunPending();

        Assert.Equal(0, picker.Columns[2].SelectedIndex);
        Assert.Equal([0, 0, 0], request.SelectedIndices);
    }

    [Fact]
    public void TappingTheChosenItemFillsTheColumnsWithNothingChosen()
    {
        var picker = TimePicker();
        picker.Columns[0].SelectedIndex = 5; // half chosen: the minutes and the part of the day are not
        var content = Open(picker, out var request);
        using var surface = new SkUiTestSurface(content, 360, 400);
        surface.Frame();
        Assert.Equal([5, -1, -1], request.SelectedIndices);

        var item = Assert.IsAssignableFrom<SkUiView>(content.Wheel(0).GetRealizedView(5));
        Assert.NotNull(SkUiDiagnostics.SimulateTap(item));
        _ui.RunPending();

        Assert.Equal([5, 0, 0], request.SelectedIndices);
        Assert.Equal([5, 0, 0], picker.SelectedIndices);
    }

    [Fact]
    public void APresenterOfItsOwnChoosesThroughTheRequest()
    {
        var picker = TimePicker();
        Open(picker, out var request);

        Assert.True(request.SetSelectedIndex(0, 11));
        Assert.True(request.SetSelectedIndex(1, 1));
        Assert.Equal([11, 1, -1], picker.SelectedIndices);
        Assert.False(request.SetSelectedIndex(1, 9)); // not an item
        Assert.False(request.SetSelectedIndex(7, 0)); // not a column
        Assert.False(request.SetSelection([1, 2])); // not one per column
        Assert.True(request.Complete([3, 0, 1]));
        _ui.RunPending();

        Assert.Equal([3, 0, 1], picker.SelectedIndices);
        Assert.False(picker.IsOpen);
        Assert.False(request.SetSelectedIndex(0, 1)); // closed
    }

    [Fact]
    public void TheAppsChangesMoveTheOpenWheels()
    {
        var picker = TimePicker();
        var content = Open(picker, out var request);
        using var surface = new SkUiTestSurface(content, 360, 400);
        surface.Frame();

        picker.Columns[0].SelectedIndex = 9;
        picker.Columns[1].SelectedItem = "15";

        Assert.Equal(9, content.Wheel(0).Position);
        Assert.Equal(1, content.Wheel(1).Position);
        Assert.Equal([9, 1, -1], request.SelectedIndices);
        Assert.True(picker.IsOpen);
    }

    [Fact]
    public void TheKeysMoveTheActiveWheel()
    {
        var picker = TimePicker();
        var content = Open(picker, out var request);
        var handler = (ISkUiKeyHandler)content;
        bool Press(SkUiKey key, char character = '\0') => handler.OnKeyDown(new SkUiKeyEvent(key, SkUiKeyModifiers.None, false, character));

        Assert.True(Press(SkUiKey.Down)); // nothing chosen: starts from the middle row
        Assert.Equal(0, picker.Columns[0].SelectedIndex);
        Assert.True(Press(SkUiKey.Down));
        Assert.True(Press(SkUiKey.Down));
        Assert.Equal(2, picker.Columns[0].SelectedIndex);
        Assert.True(Press(SkUiKey.Up));
        Assert.True(Press(SkUiKey.Up));
        Assert.True(Press(SkUiKey.Up));
        Assert.Equal(11, picker.Columns[0].SelectedIndex); // the hours loop

        Assert.True(Press(SkUiKey.Right));
        Assert.Equal(1, content.ActiveColumn);
        Assert.True(Press(SkUiKey.End));
        Assert.Equal(3, picker.Columns[1].SelectedIndex);
        Assert.True(Press(SkUiKey.Right));
        Assert.True(Press(SkUiKey.Character, 'p'));
        Assert.Equal(1, picker.Columns[2].SelectedIndex);
        Assert.True(Press(SkUiKey.Down)); // the last item of a column that does not loop
        Assert.Equal(1, picker.Columns[2].SelectedIndex);
        Assert.True(Press(SkUiKey.Right)); // past the last column
        Assert.Equal(2, content.ActiveColumn);

        Assert.Equal([11, 3, 1], request.SelectedIndices);
        Assert.True(picker.IsOpen);
        Assert.True(Press(SkUiKey.Enter));
        _ui.RunPending();
        Assert.False(picker.IsOpen);
        Assert.Equal([11, 3, 1], picker.SelectedIndices);
    }

    [Fact]
    public void ChangingTheColumnsClosesTheOpenPopup()
    {
        var picker = TimePicker();
        Open(picker, out _);

        picker.Columns.RemoveAt(2);
        _ui.RunPending();

        Assert.False(picker.IsOpen);
        Assert.Equal(2, picker.SelectedIndices.Count);

        Open(picker, out _);
        picker.Columns[0].ItemsSource = Minutes;
        _ui.RunPending();
        Assert.False(picker.IsOpen);
    }

    [Fact]
    public void TheClosedFieldOpensWithAlt()
    {
        var picker = TimePicker();
        Assert.True(picker.Focus());
        var root = (SkUiView)picker.Parent!;

        Assert.False(root.FocusManager.KeyDown(SkUiKey.Down)); // no arrow selection without the wheels
        Assert.True(root.FocusManager.KeyDown(SkUiKey.Down, SkUiKeyModifiers.Alt));
        Assert.True(picker.IsOpen);
    }

    [Fact]
    public void ColumnsBindToThePickersBindingContext()
    {
        var picker = new SkUiMultiColumnPicker { BindingContext = new Model(Minutes) };
        var column = new SkUiPickerColumn();
        column.SetBinding(SkUiPickerColumn.ItemsSourceProperty, static (Model model) => model.Items, BindingMode.OneWay, source: null);
        picker.Columns.Add(column);

        Assert.Same(Minutes, column.ItemsSource);
    }

    internal sealed record Model(string[] Items);

    [Fact]
    public void AColumnsDisplayBindingShapesItsTexts()
    {
        var column = new SkUiPickerColumn
        {
            ItemsSource = new List<Model> { new(["a"]), new(["bb", "c"]) },
            ItemDisplayBinding = new Binding(nameof(Model.Items), converter: new CountConverter())
        };

        Assert.Equal(["1", "2"], column.Texts);
    }

    private sealed class CountConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => ((string[])value!).Length.ToString();

        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
    }
}

/// <summary>The multi-column picker and the picker with several choices in XAML: columns as content, bindings both ways.</summary>
[Collection(RuntimeXamlCollection.Name)]
public class MultiColumnPickerXamlTests
{
    [Fact]
    public void ColumnsAreTheContentAndBindToTheViewModel()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        const string xaml = """
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <sk:SkUiMultiColumnPicker Title="Time" Format="{}{0}:{1}" VisibleRows="7">
                <sk:SkUiPickerColumn ItemsSource="{Binding Hours}" SelectedIndex="{Binding Hour}" Loop="True" />
                <sk:SkUiPickerColumn ItemsSource="{Binding Minutes}" SelectedItem="{Binding Minute}" Width="80" />
              </sk:SkUiMultiColumnPicker>
            </ContentView>
            """;
        var model = new TimeModel();
        var root = new ContentView { BindingContext = model };
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var picker = Assert.IsType<SkUiMultiColumnPicker>(root.Content);

        Assert.Equal(2, picker.Columns.Count);
        Assert.Equal((7, "{0}:{1}"), (picker.VisibleRows, picker.Format));
        Assert.True(picker.Columns[0].Loop);
        Assert.Equal(80, picker.Columns[1].Width);
        Assert.Equal([3, 2], picker.SelectedIndices);

        // The user's choice reaches the view model (two-way by default).
        picker.Columns[0].SelectedIndex = 5;
        picker.Columns[1].SelectedIndex = 1;
        Assert.Equal(5, model.Hour);
        Assert.Equal("15", model.Minute);
    }

    [Fact]
    public void SelectedItemsBindToAViewModelList()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        const string xaml = """
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <sk:SkUiPicker Title="Minutes" SelectionMode="Multiple" SelectedCountFormat="{}{0} chosen"
                             ItemsSource="{Binding Minutes}" SelectedItems="{Binding Chosen}" />
            </ContentView>
            """;
        var model = new TimeModel();
        model.Chosen.Add("30");
        var root = new ContentView { BindingContext = model };
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var picker = Assert.IsType<SkUiPicker>(root.Content);

        Assert.Same(model.Chosen, picker.SelectedItems);
        Assert.Equal([2], picker.SelectedIndices);
        model.Chosen.Add("00");
        Assert.Equal([0, 2], picker.SelectedIndices);
    }

    public sealed class TimeModel : System.ComponentModel.INotifyPropertyChanged
    {
        public string[] Hours { get; } = [.. Enumerable.Range(0, 12).Select(hour => hour.ToString("00"))];

        public string[] Minutes { get; } = ["00", "15", "30", "45"];

        public ObservableCollection<object> Chosen { get; } = [];

        public int Hour { get; set => Set(ref field, value); } = 3;

        public string? Minute { get; set => Set(ref field, value); } = "30";

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private void Set<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;
            field = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
        }
    }
}

/// <summary>A multi-column picker's <c>Format</c> before its columns, or malformed: the texts joined instead of an exception.</summary>
public class MultiColumnPickerFormatTests
{
    [Fact]
    public void AFormatThatDoesNotFitJoinsTheTexts()
    {
        var picker = new SkUiMultiColumnPicker { Format = "{0} h {1} min", Separator = "/" };

        picker.AddColumn(SkUiPickerColumn.Range(0, 12));
        picker.Columns[0].SelectedItem = 2;
        Assert.Equal("2", Text(picker)); // one column for two placeholders: joined

        picker.AddColumn(SkUiPickerColumn.Range(0, 45, 15, "00"));
        picker.Columns[1].SelectedItem = 30;
        Assert.Equal("2 h 30 min", Text(picker));

        picker.Format = "{0 broken";
        Assert.Equal("2/30", Text(picker));
    }

    private static string? Text(SkUiMultiColumnPicker picker)
    {
        var stack = new SkUiVerticalStackLayout();
        stack.Children.Add(picker);
        SkUiTestHelpers.Arrange(stack, 300, 200);
        var value = SkUiSemanticsTree.Build(stack).Flatten().Single(node => ReferenceEquals(node.Source, picker)).Value;
        stack.Children.Clear();
        return value;
    }
}

/// <summary>When a multi-column picker reports a change, the column's index and item agree.</summary>
public class MultiColumnPickerOrderTests
{
    [Fact]
    public void AChangeReportsTheNewItem()
    {
        var column = new SkUiPickerColumn { ItemsSource = new[] { "a", "b", "c" }, SelectedIndex = 0 };
        var picker = new SkUiMultiColumnPicker().AddColumn(column);
        var seen = new List<object?>();
        picker.SelectionChanged += (_, _) => seen.Add(column.SelectedItem);

        column.SelectedIndex = 2;
        column.SelectedItem = "b";

        Assert.Equal(["c", "b"], seen);
    }
}
