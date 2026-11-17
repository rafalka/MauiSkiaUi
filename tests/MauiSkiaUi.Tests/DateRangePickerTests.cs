using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// <see cref="SkUiDateRangePicker"/> and <see cref="SkUiCoreDateRangePicker"/>: the field's text, the calendar popup choosing a range in
/// two taps (or a draft until OK), the rules handed to the calendar, the keys, and the typed request. November 2026 in en-US
/// (Sunday weeks: the grid starts on Sunday 1 November).
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class DateRangePickerTests : IDisposable
{
    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly RecordingPopupPresenter _presenter = new();
    private readonly ISkUiPopupPresenter? _previous = SkUiPopups.Presenter;
    private readonly IDisposable _culture = DateTimeWheelsTests.UseCulture("en-US");

    public DateRangePickerTests() => SkUiPopups.Presenter = _presenter;

    public void Dispose()
    {
        SkUiPopups.Presenter = _previous;
        _culture.Dispose();
        _ui.Dispose();
    }

    private static SkUiDateRange Range(int first, int last) => new(new DateTime(2026, 11, first), new DateTime(2026, 11, last));

    private static SkUiVerticalStackLayout Root(ISkUiView child)
    {
        var stack = new SkUiVerticalStackLayout { Padding = new Thickness(10, 20) };
        stack.Children.Add(child);
        SkUiTestHelpers.Arrange(stack, 360, 700);
        return stack;
    }

    private static string? FieldValue(SkUiView root, ISkUiAccessibleNode picker) =>
        SkUiSemanticsTree.Build(root).Flatten().First(node => ReferenceEquals(node.Source, picker)).Value;

    private SkUiDateRangePopupContent Open(SkUiPickerBase picker, out SkUiValuePopupRequest<SkUiDateRange?> request)
    {
        picker.IsOpen = true;
        return Shown(out request);
    }

    private SkUiDateRangePopupContent Open(SkUiCorePickerBase picker, out SkUiValuePopupRequest<SkUiDateRange?> request)
    {
        picker.IsOpen = true;
        return Shown(out request);
    }

    private SkUiDateRangePopupContent Shown(out SkUiValuePopupRequest<SkUiDateRange?> request)
    {
        request = Assert.IsType<SkUiValuePopupRequest<SkUiDateRange?>>(_presenter.Current);
        var popup = Assert.IsType<SkUiCustomPopup>(request.Content);
        return Assert.IsType<SkUiDateRangePopupContent>(popup.AppContent);
    }

    /// <summary>A day of the November 2026 page chosen as a screen reader would (its cell's element activated).</summary>
    private static void Choose(SkUiDateRangePopupContent content, int day)
    {
        var calendar = content.Calendar!;
        Assert.Equal(new DateTime(2026, 11, 1), calendar.Engine.PageStart);
        Assert.True(((ISkUiAccessibleNode)calendar).PerformSemanticsPartAction(3 + day - 1, SkUiSemanticsActions.Activate));
    }

    [Fact]
    public void TwoTapsChooseTheRangeAndTheFieldShowsIt()
    {
        var picker = new SkUiDateRangePicker { Value = Range(10, 12) };
        var root = Root(picker);
        Assert.Equal("11/10/2026 – 11/12/2026", FieldValue(root, picker));
        var values = new List<SkUiDateRange?>();
        picker.ValueChanged += (_, args) => values.Add(args.NewValue);

        var content = Open(picker, out var request);
        Assert.Equal(Range(10, 12), content.Calendar!.SelectedRange);
        Assert.Equal(SkUiCalendarSelectionMode.Range, content.Calendar.SelectionMode);
        Choose(content, 3);
        Assert.True(picker.IsOpen); // the start alone changes nothing yet
        Assert.Equal(Range(10, 12), picker.Value);
        Choose(content, 5);
        _ui.RunPending();
        Assert.Equal(Range(3, 5), picker.Value);
        Assert.False(picker.IsOpen);
        Assert.Equal(Range(3, 5), request.Result);
        Assert.Equal([Range(3, 5)], values);
        Assert.Equal("11/3/2026 – 11/5/2026", FieldValue(root, picker));

        picker.DisplayFormat = "{0:MMM d} to {1:MMM d} ({2} days)";
        Assert.Equal("Nov 3 to Nov 5 (3 days)", FieldValue(root, picker));
        // An open range has no last day or count; a format that cannot be used falls back to the default.
        picker.DisplayFormat = "{0:MMM d} – {1:MMM d}";
        picker.Value = new SkUiDateRange(new DateTime(2026, 11, 3));
        Assert.Equal("Nov 3 –", FieldValue(root, picker));
        picker.DisplayFormat = "{0:d} – {3}";
        picker.Value = Range(3, 5);
        Assert.Equal("11/3/2026 – 11/5/2026", FieldValue(root, picker));
        Assert.Throws<ArgumentNullException>(() => picker.SetDisplayFormat(null!));
    }

    [Fact]
    public void WaitingForOkTheRangeIsADraft()
    {
        var picker = new SkUiDateRangePicker { Value = Range(10, 12), UpdateMode = SkUiPickerUpdateMode.WhenFinished };
        Root(picker);
        var content = Open(picker, out var request);
        Choose(content, 20);
        Assert.Equal(Range(20, 20), request.Value); // a start alone: one day
        Assert.Equal(new SkUiDateRange(new DateTime(2026, 11, 20)), content.Calendar!.SelectedRange); // still open in the calendar
        Choose(content, 24);
        Assert.Equal(Range(20, 24), request.Value);
        Assert.Equal(Range(10, 12), picker.Value);
        Assert.True(picker.IsOpen);

        // Enter on the draft accepts it.
        Assert.True(((ISkUiKeyHandler)request.Content!).OnKeyDown(new SkUiKeyEvent(SkUiKey.Enter, SkUiKeyModifiers.None, false)));
        _ui.RunPending();
        Assert.Equal(Range(20, 24), picker.Value);
        Assert.False(picker.IsOpen);

        // Cancel drops the draft; the arrow keys move the calendar's focus.
        content = Open(picker, out request);
        var popup = (ISkUiKeyHandler)request.Content!;
        Assert.True(popup.OnKeyDown(new SkUiKeyEvent(SkUiKey.Right, SkUiKeyModifiers.None, false)));
        Assert.NotNull(content.Calendar!.FocusDate);
        Choose(content, 2);
        Assert.True(popup.OnKeyDown(new SkUiKeyEvent(SkUiKey.Escape, SkUiKeyModifiers.None, false)));
        _ui.RunPending();
        Assert.Equal(Range(20, 24), picker.Value);

        // Waiting for OK, Clear empties the draft and the popup stays open; OK then applies none.
        content = Open(picker, out request);
        Assert.True(request.Clear());
        Assert.True(picker.IsOpen);
        Assert.Null(request.Value);
        Assert.Null(content.Calendar!.SelectedRange);
        Assert.Equal(Range(20, 24), picker.Value);
        Choose(content, 3);
        Choose(content, 4);
        Assert.Equal(Range(3, 4), request.Value);
        Assert.True(request.Clear());
        Assert.True(request.Accept());
        _ui.RunPending();
        Assert.Null(picker.Value);
        Assert.False(picker.IsOpen);

        // Without OK, Clear clears the picker and closes.
        picker.UpdateMode = SkUiPickerUpdateMode.Immediately;
        picker.Value = Range(20, 24);
        Open(picker, out request);
        Assert.True(request.Clear());
        _ui.RunPending();
        Assert.Null(picker.Value);
        Assert.False(picker.IsOpen);
    }

    [Fact]
    public void TheRulesAreHandedToTheCalendar()
    {
        var picker = new SkUiDateRangePicker
        {
            Value = Range(10, 12),
            MinimumDate = new DateTime(2026, 11, 2),
            MaximumRangeDays = 3,
            AllowsDisabledDaysInRange = false,
            DateEnabled = date => date.Day != 25
        };
        picker.BlackoutDates!.Add(new DateTime(2026, 11, 14));
        Root(picker);
        var calendar = Open(picker, out _).Calendar!;
        Assert.Equal(new DateTime(2026, 11, 2), calendar.MinimumDate);
        Assert.Equal(3, calendar.MaximumRangeDays);
        Assert.False(calendar.AllowsDisabledDaysInRange);
        Assert.False(calendar.Engine.IsDayEnabled(new DateTime(2026, 11, 14)));
        Assert.False(calendar.Engine.IsDayEnabled(new DateTime(2026, 11, 25)));
        Assert.False(calendar.Engine.IsDayEnabled(new DateTime(2026, 11, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => picker.SetMinimumRangeDays(-1));
    }

    [Fact]
    public void TheCoreTwinChoosesARange()
    {
        var picker = new SkUiCoreDateRangePicker().SetDisplayFormat("{0:yyyy-MM-dd} / {1:yyyy-MM-dd}");
        picker.SetValue(Range(10, 12));
        var host = new SkUiCoreHost().SetContent(picker);
        var root = Root(host);
        Assert.Equal("2026-11-10 / 2026-11-12", FieldValue(root, picker));
        var changed = new List<string?>();
        picker.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        var content = Open(picker, out _);
        Choose(content, 16);
        Choose(content, 18);
        _ui.RunPending();
        Assert.Equal(Range(16, 18), picker.Value);
        Assert.Contains(nameof(picker.Value), changed);
        Assert.Equal("2026-11-16 / 2026-11-18", FieldValue(root, picker));
        Assert.Same(picker, picker.SetMaximumRangeDays(5).SetMinimumDate(new DateTime(2026, 1, 1)).SetAllowsDisabledDaysInRange(false));
        Assert.Throws<ArgumentOutOfRangeException>(() => picker.SetMaximumRangeDays(-1));
    }
}
