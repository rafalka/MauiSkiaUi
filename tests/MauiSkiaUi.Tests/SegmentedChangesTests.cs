using System.Collections.ObjectModel;
using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// 1.17: both segmented controls while things change under them: the selected option removed, an index beyond the options, equal
/// items, an item selected before the options come, the selection set again inside its notifications, Space and Enter, and the
/// outlined style's check taking its room.
/// </summary>
public class SegmentedChangesTests
{
    private sealed record Segmented(SkUiView Root, ISkUiAccessibleNode Node, Func<int> Selected, Action<int> Select, Func<object?> Item, Action<object?> SelectItem,
        Action<System.Collections.IEnumerable> SetItems, Action<Action<int, int>> OnChanged, SkUiSegmentedControl? Drawn, SkUiCoreSegmentedControl? Core);

    private static Segmented Make(bool core)
    {
        if (core)
        {
            var control = new SkUiCoreSegmentedControl().SetFontFamily(SkUiTestHelpers.BundledFontFamily);
            return new Segmented(new SkUiCoreHost().SetContent(control), control, () => control.SelectedIndex, value => control.SetSelectedIndex(value),
                () => control.SelectedItem, value => control.SetSelectedItem(value), value => control.SetItemsSource(value),
                handler => control.SelectionChanged += (_, args) => handler(args.PreviousIndex, args.CurrentIndex), null, control);
        }
        var drawn = new SkUiSegmentedControl { FontFamily = SkUiTestHelpers.BundledFontFamily };
        return new Segmented(new SkUiContentView { Content = drawn }, drawn, () => drawn.SelectedIndex, value => drawn.SelectedIndex = value, () => drawn.SelectedItem,
            value => drawn.SelectedItem = value, value => drawn.ItemsSource = value,
            handler => drawn.SelectionChanged += (_, args) => handler(args.PreviousIndex, args.CurrentIndex), drawn, null);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AfterTheSelectedLastOptionIsRemovedAnAddedOneIsNotSelected(bool core)
    {
        var items = new ObservableCollection<string> { "A", "B", "C" };
        var control = Make(core);
        control.SetItems(items);
        control.Select(2);
        items.RemoveAt(2);
        Assert.Equal((1, "B"), (control.Selected(), control.Item()));
        items.Add("D");
        Assert.Equal((1, "B"), (control.Selected(), control.Item()));
        items.Insert(0, "First"); // still followed
        Assert.Equal((2, "B"), (control.Selected(), control.Item()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnIndexBeyondTheOptionsBecomesTheLastForGoodAndOneAskedForEarlyWaits(bool core)
    {
        var items = new ObservableCollection<string> { "A", "B", "C" };
        var control = Make(core);
        control.SetItems(items);
        control.Select(10);
        Assert.Equal(2, control.Selected());
        items.Add("D");
        Assert.Equal(2, control.Selected());

        var early = Make(core);
        early.Select(2);
        var later = new ObservableCollection<string>();
        early.SetItems(later);
        foreach (var item in new[] { "A", "B", "C", "D" })
            later.Add(item);
        Assert.Equal((2, "C"), (early.Selected(), early.Item()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EqualItemsKeepTheirPlace(bool core)
    {
        var items = new ObservableCollection<string> { "A", "A", "B" };
        var control = Make(core);
        control.SetItems(items);
        control.Select(1);
        items.Add("C");
        Assert.Equal(1, control.Selected());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnItemSelectedBeforeTheOptionsWaitsForThem(bool core)
    {
        var items = new ObservableCollection<string>();
        var control = Make(core);
        control.SelectItem("B");
        control.SetItems(items);
        items.Add("A");
        Assert.Equal(-1, control.Selected());
        items.Add("B");
        Assert.Equal((1, "B"), (control.Selected(), control.Item()));
        items.Add("C");
        Assert.Equal(1, control.Selected());

        // Choosing an index first gives the item up.
        var other = Make(core);
        other.SelectItem("B");
        other.Select(0);
        other.SetItems(new[] { "A", "B" });
        Assert.Equal((0, "A"), (other.Selected(), other.Item()));
    }

    private sealed class ViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public object? Current { get; set; } = "B";

        public string[]? Options
        {
            get;
            set
            {
                field = value;
                PropertyChanged?.Invoke(this, new(nameof(Options)));
            }
        }
    }

    [Fact]
    public void ATwoWayItemBoundBeforeTheItemsIsNotOverwritten()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var model = new ViewModel();
        var control = new SkUiSegmentedControl { BindingContext = model };
        control.SetBinding(SkUiSegmentedControl.SelectedItemProperty, nameof(ViewModel.Current), BindingMode.TwoWay);
        control.SetBinding(SkUiSegmentedControl.ItemsSourceProperty, nameof(ViewModel.Options));
        model.Options = ["A", "B", "C"];
        Assert.Equal("B", model.Current);
        Assert.Equal((1, "B"), (control.SelectedIndex, control.SelectedItem));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ASelectionMadeInTheEventOfARemovalIsWhatWasAskedForLast(bool core)
    {
        var items = new ObservableCollection<string> { "A", "B", "C" };
        var control = Make(core);
        control.SetItems(items);
        control.Select(2);
        control.OnChanged((_, current) =>
        {
            if (current == 1)
                control.Select(0);
        });
        items.RemoveAt(2);
        Assert.Equal((0, "A"), (control.Selected(), control.Item()));
        items.Add("D");
        Assert.Equal(0, control.Selected());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ASelectionMadeInAPropertysNotificationIsTheOneEverythingShows(bool core)
    {
        var control = Make(core);
        control.SetItems(new[] { "A", "B", "C" });
        control.Select(0);
        var events = new List<int>();
        control.OnChanged((_, current) => events.Add(current));
        System.ComponentModel.INotifyPropertyChanged notifier = core ? control.Core! : control.Drawn!;
        notifier.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == "SelectedIndex" && control.Selected() == 1)
                control.Select(2);
        };
        control.Select(1);
        Assert.Equal((2, "C"), (control.Selected(), control.Item()));
        Assert.Equal(2, events[^1]);
    }

    [Theory]
    [InlineData(false, "Space")]
    [InlineData(false, "Enter")]
    [InlineData(true, "Space")]
    [InlineData(true, "Enter")]
    public void SpaceAndEnterSelectNothingElse(bool core, string key)
    {
        var runs = 0;
        var control = Make(core);
        control.SetItems(new[] { "A", "B", "C" });
        control.Select(0);
        if (core)
            control.Core!.SetCommand(new Command(() => runs++));
        else
            control.Drawn!.Command = new Command(() => runs++);
        using var surface = new SkUiTestSurface(control.Root, 300, 40);
        surface.Frame(0);
        Assert.True(control.Root.FocusManager.Focus(control.Node));
        Assert.True(control.Root.FocusManager.KeyDown(Enum.Parse<SkUiKey>(key)));
        control.Node.PerformSemanticsAction(SkUiSemanticsActions.Activate);
        Assert.Equal((0, 0), (control.Selected(), runs));
        var info = new SkUiSemanticsInfo();
        control.Node.GetSemantics(info);
        Assert.Equal(SkUiSemanticsActions.None, info.Actions & SkUiSemanticsActions.Activate);
    }

    /// <summary>Material's check: the control asks for its room, so the selected text shows whole at the size it asks for, and the check is inside its segment.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheOutlinedCheckHasRoomAtTheSizeTheControlAsksFor(bool core)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var control = Make(core);
        control.SetItems(new[] { "Wednesday", "Thu" });
        control.Select(0);
        control.Root.Look = new AndroidMaterial3SkUiLook();
        var asked = core ? control.Core!.Measure(double.PositiveInfinity, 60) : ((IView)control.Drawn!).Measure(double.PositiveInfinity, 60);
        var width = (int)Math.Ceiling(asked.Width);
        using var narrow = new SkUiTestSurface(new SkUiContentView { Background = Colors.White, Content = control.Root }, width, 40);
        narrow.Frame(0);
        narrow.Frame(1000);
        var same = Make(core);
        same.SetItems(new[] { "Wednesday", "Thu" });
        same.Select(0);
        same.Root.Look = new AndroidMaterial3SkUiLook();
        using var wide = new SkUiTestSurface(new SkUiContentView { Background = Colors.White, Content = same.Root }, width + 200, 40);
        wide.Frame(0);
        wide.Frame(1000);

        // The dark ink (texts and the check) of the selected segment is the same where there is room to spare.
        int Ink(SkUiTestSurface surface, int from, int until)
        {
            var ink = 0;
            for (var x = from; x < until; x++)
                for (var y = 4; y < 36; y++)
                    if (surface.Bitmap.GetPixel(x, y) is { Red: < 60, Green: < 60, Blue: < 60 })
                        ink++;
            return ink;
        }
        var first = core ? control.Core!.SegmentBounds(0) : control.Drawn!.SegmentBounds(0);
        var firstWide = core ? same.Core!.SegmentBounds(0) : same.Drawn!.SegmentBounds(0);
        var expected = Ink(wide, (int)firstWide.Left + 2, (int)firstWide.Right - 2);
        Assert.True(expected > 100, $"only {expected} dark pixels");
        Assert.InRange(Ink(narrow, (int)first.Left + 2, (int)first.Right - 2), expected * 0.95, expected * 1.05);
    }
}
