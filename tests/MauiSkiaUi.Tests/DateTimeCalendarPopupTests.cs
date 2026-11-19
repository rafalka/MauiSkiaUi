using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// A date and time picker's calendar popup (1.15 B2, <see cref="SkUiDateTimePicker.PopupStyle"/>): the calendar over the time wheels,
/// following <c>UpdateMode</c> as the wheels do, the range's times on its first and last days, keys, and the Core twin. November 2026
/// in en-US (Sunday weeks; "h mm tt" time wheels).
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class DateTimeCalendarPopupTests : IDisposable
{
    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly RecordingPopupPresenter _presenter = new();
    private readonly ISkUiPopupPresenter? _previous = SkUiPopups.Presenter;
    private readonly IDisposable _culture = DateTimeWheelsTests.UseCulture("en-US");

    public DateTimeCalendarPopupTests() => SkUiPopups.Presenter = _presenter;

    public void Dispose()
    {
        SkUiPopups.Presenter = _previous;
        _culture.Dispose();
        _ui.Dispose();
    }

    private static T Arranged<T>(T picker) where T : SkUiView
    {
        var stack = new SkUiVerticalStackLayout { Padding = new Thickness(10, 20) };
        stack.Children.Add(picker);
        SkUiTestHelpers.Arrange(stack, 360, 700);
        return picker;
    }

    private SkUiDateTimeCalendarPopup Open(Func<bool> open, out SkUiDatePopupRequest request)
    {
        Assert.True(open());
        request = Assert.IsType<SkUiDatePopupRequest>(_presenter.Current);
        return Assert.IsType<SkUiDateTimeCalendarPopup>(request.Content);
    }

    private SkUiDateTimeCalendarPopup Open(SkUiDateTimePicker picker, out SkUiDatePopupRequest request) =>
        Open(() => picker.IsOpen = true, out request);

    /// <summary>A day of the November 2026 page chosen as a screen reader would.</summary>
    private static void ChooseDay(SkUiDateTimeCalendarPopup content, int day)
    {
        Assert.Equal(new DateTime(2026, 11, 1), content.Calendar.Engine.PageStart);
        Assert.True(((ISkUiAccessibleNode)content.Calendar).PerformSemanticsPartAction(3 + day - 1, SkUiSemanticsActions.Activate));
    }

    /// <summary>Turns a time wheel to the item that reads <paramref name="text"/>, as a wheel the user stopped there.</summary>
    private void Turn(SkUiDateTimeCalendarPopup content, int column, string text)
    {
        var index = Array.IndexOf(content.TimeTexts[column], text);
        Assert.True(index >= 0, $"{text} in column {column}");
        _ = content.TimeWheels!.Wheel(column).ScrollToIndex(index, animated: false);
        _ui.RunPending();
    }

    private static bool Key(SkUiDateTimeCalendarPopup content, SkUiKey key) =>
        ((ISkUiKeyHandler)content).OnKeyDown(new SkUiKeyEvent(key, SkUiKeyModifiers.None, false));

    [Fact]
    public void EachChangeAppliesAtOnceAndThePopupStaysOpen()
    {
        var picker = Arranged(new SkUiDateTimePicker { PopupStyle = SkUiDatePickerPopupStyle.Calendar, Date = new DateTime(2026, 11, 10, 14, 30, 0) });
        var content = Open(picker, out var request);
        Assert.True(request.IncludesTime);
        Assert.Equal(new DateTime(2026, 11, 10), content.Calendar.SelectedDate);
        Assert.Equal(new DateTime(2026, 11, 10, 14, 30, 0), content.Draft);
        // The picker's time columns only: 12-hour hours, minutes, AM / PM.
        Assert.Equal(3, content.TimeTexts.Length);
        Assert.Equal(12, content.TimeTexts[0].Length);
        Assert.Equal(60, content.TimeTexts[1].Length);
        Assert.Equal(["AM", "PM"], content.TimeTexts[2]);
        Assert.Equal("h mm tt", SkUiDateTimeCalendarPopup.TimeFormat(SkUiDateTimeWheels.Create(
            new SkUiDateTimeSettings(SkUiDateTimeKind.DateAndTime, null, DateTime.MinValue, DateTime.MaxValue, null, null),
            System.Globalization.CultureInfo.CurrentCulture, SkUiStrings.Current)));

        ChooseDay(content, 12); // the day changes, the time stays; the popup stays open
        _ui.RunPending();
        Assert.Equal(new DateTime(2026, 11, 12, 14, 30, 0), picker.Date);
        Assert.True(picker.IsOpen);
        Turn(content, 1, "45");
        Assert.Equal(new DateTime(2026, 11, 12, 14, 45, 0), picker.Date);
        Turn(content, 2, "AM");
        Assert.Equal(new DateTime(2026, 11, 12, 2, 45, 0), picker.Date);

        // The app's change while open shows; Escape closes.
        picker.Date = new DateTime(2026, 11, 20, 9, 5, 0);
        Assert.Equal(new DateTime(2026, 11, 20), content.Calendar.SelectedDate);
        Assert.Equal(new DateTime(2026, 11, 20, 9, 5, 0), content.Draft);
        Assert.True(Key(content, SkUiKey.Escape));
        _ui.RunPending();
        Assert.False(picker.IsOpen);
        Assert.Equal(new DateTime(2026, 11, 20, 9, 5, 0), picker.Date);
    }

    [Fact]
    public void WaitingForOkTheDayAndTimeAreADraftAndTabMovesToTheWheels()
    {
        var picker = Arranged(new SkUiDateTimePicker
        {
            PopupStyle = SkUiDatePickerPopupStyle.Calendar, UpdateMode = SkUiPickerUpdateMode.WhenFinished, Date = new DateTime(2026, 11, 10, 8, 0, 0)
        });
        var content = Open(picker, out var request);
        ChooseDay(content, 18);
        Turn(content, 1, "15");
        Assert.Equal(new DateTime(2026, 11, 18, 8, 15, 0), request.CurrentDate);
        Assert.Equal(new DateTime(2026, 11, 10, 8, 0, 0), picker.Date);
        Assert.True(picker.IsOpen);

        // Tab: the keys turn the time wheels (Down: the next hour); Enter accepts.
        Assert.True(Key(content, SkUiKey.Tab));
        Assert.True(Key(content, SkUiKey.Down));
        _ui.RunPending();
        Assert.Equal(new DateTime(2026, 11, 18, 9, 15, 0), request.CurrentDate);
        Assert.True(Key(content, SkUiKey.Enter));
        _ui.RunPending();
        Assert.False(picker.IsOpen);
        Assert.Equal(new DateTime(2026, 11, 18, 9, 15, 0), picker.Date);
    }

    [Fact]
    public void TheRangesFirstDayOffersOnlyItsTimes()
    {
        var picker = Arranged(new SkUiDateTimePicker
        {
            PopupStyle = SkUiDatePickerPopupStyle.Calendar,
            MinimumDate = new DateTime(2026, 11, 12, 9, 30, 0),
            MaximumDate = new DateTime(2026, 11, 20, 17, 0, 0),
            Date = new DateTime(2026, 11, 15, 8, 0, 0),
        });
        var content = Open(picker, out _);
        Assert.False(content.Calendar.Engine.IsDayEnabled(new DateTime(2026, 11, 11)));
        Assert.True(content.Calendar.Engine.IsDayEnabled(new DateTime(2026, 11, 12)));
        // The 15th: every hour.
        Assert.All(content.TimeTexts[0], (_, index) => Assert.True(SkUiItemStates.IsEnabled(content.TimeWheels!.StatesOf(0), index)));

        // The first day: 8:00 is before the range, so the time moves to its start; the morning's earlier hours cannot be chosen.
        ChooseDay(content, 12);
        _ui.RunPending();
        Assert.Equal(new DateTime(2026, 11, 12, 9, 30, 0), picker.Date);
        Assert.False(SkUiItemStates.IsEnabled(content.TimeWheels!.StatesOf(0), Array.IndexOf(content.TimeTexts[0], "8")));
        Assert.True(SkUiItemStates.IsEnabled(content.TimeWheels!.StatesOf(0), Array.IndexOf(content.TimeTexts[0], "9")));

        // The last day: after 5 PM is past the range.
        Turn(content, 2, "PM");
        Turn(content, 0, "4");
        ChooseDay(content, 20);
        _ui.RunPending();
        Assert.Equal(new DateTime(2026, 11, 20, 16, 30, 0), picker.Date);
        Turn(content, 0, "6");
        Assert.True(picker.Date <= new DateTime(2026, 11, 20, 17, 0, 0));
    }

    [Fact]
    public void ThePopupStyleFollowsTheLookByDefaultAndTheCoreTwinHasIt()
    {
        var picker = Arranged(new SkUiDateTimePicker { Date = new DateTime(2026, 11, 10, 14, 30, 0) });
        Assert.Equal(SkUiDatePickerPopupStyle.Default, picker.PopupStyle);
        picker.IsOpen = true;
        Assert.IsType<SkUiColumnsPopup>(_presenter.Current!.Content); // the built-in looks show wheels
        picker.IsOpen = false;
        _ui.RunPending();
        Assert.Throws<ArgumentOutOfRangeException>(() => picker.SetPopupStyle((SkUiDatePickerPopupStyle)7));

        var style = new Style(typeof(SkUiCalendar)) { Setters = { new Setter { Property = SkUiCalendar.FirstDayOfWeekProperty, Value = DayOfWeek.Monday } } };
        var core = new Core.SkUiCoreDateTimePicker().SetPopupStyle(SkUiDatePickerPopupStyle.Calendar).SetCalendarStyle(style)
            .SetDate(new DateTime(2026, 11, 10, 14, 30, 0));
        Assert.Equal(SkUiDatePickerPopupStyle.Calendar, core.PopupStyle);
        Assert.Same(style, core.CalendarStyle);
        Arranged(new Core.SkUiCoreHost().SetContent(core));
        var content = Open(() => core.IsOpen = true, out _);
        Assert.Equal(DayOfWeek.Monday, content.Calendar.FirstDayOfWeek);
        Assert.Equal(new DateTime(2026, 11, 10), content.Calendar.SelectedDate);
        // As wide as the calendar, and as tall as it and three rows of wheels.
        var size = content.PreferredSize(0, 1000, 1000);
        Assert.Equal(7 * 44 + 16, size.Width);
        Assert.True(size.Height > content.Calendar.Measure(size.Width - 16, double.PositiveInfinity).Height + 3 * SkUiLook.Current.PickerWheelRowHeight);
    }
}
