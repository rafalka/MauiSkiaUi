using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// The date picker: MAUI's rules (today by default, a day without its time, kept in the range, <c>DateSelected</c>, MAUI's opened
/// and closed events), the field's text, and its wheels through a recording presenter: choosing, a month without the chosen day,
/// the range, blackout days, OK / Cancel, a presenter's own date UI, clearing.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class DatePickerTests : IDisposable
{
    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly RecordingPopupPresenter _presenter = new();
    private readonly ISkUiPopupPresenter? _previous = SkUiPopups.Presenter;
    private readonly IDisposable _culture = DateTimeWheelsTests.UseCulture("en-US");

    public DatePickerTests() => SkUiPopups.Presenter = _presenter;

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

    internal static string? FieldText(SkUiView picker) =>
        SkUiSemanticsTree.Build((SkUiView)picker.Parent!).Flatten().Single(node => ReferenceEquals(node.Source, picker)).Value;

    private SkUiColumnsPopup Open(SkUiPickerBase picker, out SkUiDatePopupRequest request)
    {
        picker.IsOpen = true;
        request = Assert.IsType<SkUiDatePopupRequest>(_presenter.Current);
        return Assert.IsType<SkUiColumnsPopup>(request.Content);
    }

    /// <summary>Turns a wheel to <paramref name="index"/> at once (as a wheel the user stopped there).</summary>
    internal static void Turn(SkUiColumnsPopup content, int column, int index) => _ = content.Wheel(column).ScrollToIndex(index, animated: false);

    [Fact]
    public void MauisRulesHold()
    {
        var picker = new SkUiDatePicker();
        Assert.Equal(DateTime.Today, picker.Date);
        Assert.Equal(new DateTime(1900, 1, 1), picker.MinimumDate);
        Assert.Equal(new DateTime(2100, 12, 31), picker.MaximumDate);
        Assert.Equal("d", picker.Format);

        var changes = new List<(DateTime?, DateTime?)>();
        picker.DateSelected += (_, args) => changes.Add((args.OldDate, args.NewDate));
        picker.Date = new DateTime(2026, 5, 4, 15, 45, 0); // the day, without its time
        Assert.Equal(new DateTime(2026, 5, 4), picker.Date);

        picker.MinimumDate = new DateTime(2027, 1, 1); // the date moves into the range
        Assert.Equal(new DateTime(2027, 1, 1), picker.Date);
        picker.Date = new DateTime(2200, 1, 1);
        Assert.Equal(new DateTime(2100, 12, 31), picker.Date);
        picker.MaximumDate = new DateTime(2000, 1, 1); // before the minimum: ignored
        Assert.Equal(new DateTime(2100, 12, 31), picker.MaximumDate);
        Assert.Throws<ArgumentOutOfRangeException>(() => picker.SetMaximumDate(new DateTime(2000, 1, 1)));

        picker.Date = null;
        Assert.Equal(
        [
            (DateTime.Today, new DateTime(2026, 5, 4)), (new DateTime(2026, 5, 4), new DateTime(2027, 1, 1)),
            (new DateTime(2027, 1, 1), new DateTime(2100, 12, 31)), (new DateTime(2100, 12, 31), null)
        ], changes);
    }

    [Fact]
    public void TheFieldShowsTheDateInItsFormat()
    {
        var picker = Arranged(new SkUiDatePicker { Date = new DateTime(2026, 10, 13), Title = "Due" });
        Assert.Equal("10/13/2026", FieldText(picker));
        picker.Format = "D";
        Assert.Equal("Tuesday, October 13, 2026", FieldText(picker));
        picker.Format = "dd MMM yyyy";
        Assert.Equal("13 Oct 2026", FieldText(picker));

        picker.IsClearButtonVisible = true;
        picker.ClearSelection();
        Assert.Null(picker.Date);
        Assert.Null(FieldText(picker)); // the title shows
    }

    [Fact]
    public void OpeningShowsTheCulturesWheels()
    {
        var picker = Arranged(new SkUiDatePicker { Date = new DateTime(2026, 10, 13) });
        var openedEvents = new List<string>();
        picker.Opened += (_, args) => openedEvents.Add(args.GetType().Name);
        ((SkUiPickerBase)picker).Opened += (_, args) => openedEvents.Add(args.GetType().Name);

        var content = Open(picker, out var request);

        Assert.Equal([nameof(DatePickerOpenedEventArgs), nameof(PickerOpenedEventArgs)], openedEvents.Order());
        Assert.Equal(3, request.Columns.Count);
        Assert.Equal("October", request.Columns[0][9]);
        Assert.Equal([9, 12, 126], request.SelectedIndices);
        Assert.Equal(new DateTime(2026, 10, 13), request.Date);
        Assert.Equal(new DateTime(2026, 10, 13), request.CurrentDate);
        Assert.False(request.IncludesTime);
        Assert.Equal(12, content.Wheel(1).Position);
    }

    [Fact]
    public void TurningAWheelChoosesAtOnceAndADayTheMonthLacksMovesToItsLast()
    {
        var picker = Arranged(new SkUiDatePicker { Date = new DateTime(2026, 3, 31) });
        var content = Open(picker, out var request);
        using var surface = new SkUiTestSurface(content, 360, 400);
        surface.Frame();

        Turn(content, 0, 1); // February
        Assert.Equal(new DateTime(2026, 2, 28), picker.Date);
        Assert.Equal(27, content.Wheel(1).Position);
        Assert.False(request.ItemsEnabled[1]![29]); // the 30th is dimmed
        Assert.True(picker.IsOpen);

        Turn(content, 2, 2028 - 1900); // a leap year: the 29th can be chosen again
        Assert.True(request.ItemsEnabled[1]![28]);
        Assert.Equal(new DateTime(2028, 2, 28), picker.Date);
    }

    [Fact]
    public void TheRangeAndBlackoutDatesCannotBeChosen()
    {
        var picker = Arranged(new SkUiDatePicker
        {
            MinimumDate = new DateTime(2026, 10, 10),
            MaximumDate = new DateTime(2026, 12, 31),
            Date = new DateTime(2026, 10, 20),
            UpdateMode = SkUiPickerUpdateMode.WhenFinished
        });
        picker.BlackoutDates!.Add(new DateTime(2026, 10, 21));
        var content = Open(picker, out var request);
        using var surface = new SkUiTestSurface(content, 360, 400);
        surface.Frame();

        Assert.Equal(["2026"], request.Columns[2]);
        Assert.False(request.ItemsEnabled[0]![8]); // September
        Assert.False(request.ItemsEnabled[1]![8]); // the 9th of October
        Assert.False(request.ItemsEnabled[1]![20]); // a blackout date
        Assert.False(request.IsDateEnabled(new DateTime(2026, 10, 21)));
        Assert.False(request.SetDate(new DateTime(2026, 10, 21)));
        Assert.False(request.SetDate(new DateTime(2027, 1, 1)));

        // A wheel passing over a dimmed day does not choose it.
        Turn(content, 1, 20);
        Assert.Equal(new DateTime(2026, 10, 20), request.CurrentDate);
        Turn(content, 1, 21);
        Assert.Equal(new DateTime(2026, 10, 22), request.CurrentDate);
        // Waiting for OK: the picker keeps its date until then.
        Assert.Equal(new DateTime(2026, 10, 20), picker.Date);

        // November 2026 has every day; back to October from the 5th: before the minimum, so the 10th.
        Turn(content, 0, 10);
        Turn(content, 1, 4);
        Turn(content, 0, 9);
        Assert.Equal(new DateTime(2026, 10, 10), request.CurrentDate);
        Assert.Equal(9, content.Wheel(1).Position);

        Assert.True(request.Accept());
        _ui.RunPending();
        Assert.Equal(new DateTime(2026, 10, 10), picker.Date);
        Assert.False(picker.IsOpen);
    }

    [Fact]
    public void AWheelStillTurningFollowsTheRulesOnceItStops()
    {
        var picker = Arranged(new SkUiDatePicker
        {
            MinimumDate = new DateTime(2026, 10, 10),
            MaximumDate = new DateTime(2026, 12, 31),
            Date = new DateTime(2026, 11, 20),
            UpdateMode = SkUiPickerUpdateMode.WhenFinished
        });
        var content = Open(picker, out var request);
        using var surface = new SkUiTestSurface(content, 360, 400);
        surface.Frame(0);

        content.Wheel(1).Position = 4; // turning to the 5th
        Assert.True(content.Wheel(1).IsScrolling);
        Turn(content, 0, 9); // October while it turns: the 5th is before the minimum
        Assert.Equal(new DateTime(2026, 10, 10), request.CurrentDate);
        Assert.Equal(4, content.Wheel(1).Position); // a turning wheel is not yanked

        for (var time = 0; time <= 2000; time += 16)
            surface.Frame(time);
        Assert.False(content.Wheel(1).IsScrolling);
        Assert.Equal(9, content.Wheel(1).Position); // once stopped, it rolls to the 10th
        Assert.Equal(new DateTime(2026, 10, 10), request.CurrentDate);
    }

    [Fact]
    public void ADatePickerWithoutADateShowsItsHighlightedDate()
    {
        var picker = Arranged(new SkUiDatePicker { Date = null, DefaultHighlightedDate = new DateTime(1990, 6, 15) });
        var content = Open(picker, out var request);
        using var surface = new SkUiTestSurface(content, 360, 400);
        surface.Frame();

        Assert.Equal([-1, -1, -1], request.SelectedIndices);
        Assert.Equal(new DateTime(1990, 6, 15), request.DefaultHighlightedDate);
        Assert.Equal(90, content.Wheel(2).Position);
        Assert.Null(request.CurrentDate);

        // Turning one wheel chooses what the others show.
        Turn(content, 1, 0);
        Assert.Equal(new DateTime(1990, 6, 1), picker.Date);
    }

    [Fact]
    public void APresenterOfItsOwnChoosesADate()
    {
        var picker = Arranged(new SkUiDatePicker { Date = new DateTime(2026, 1, 31), DateEnabled = date => date.DayOfWeek != DayOfWeek.Sunday });
        Open(picker, out var request);

        Assert.False(request.CompleteDate(new DateTime(2026, 2, 1))); // a Sunday
        Assert.True(request.SetDate(new DateTime(2026, 2, 28, 18, 0, 0))); // the time is ignored; the 30th is no longer chosen
        Assert.Equal(new DateTime(2026, 2, 28), picker.Date);
        Assert.True(request.CompleteDate(new DateTime(2026, 4, 30)));
        _ui.RunPending();
        Assert.Equal(new DateTime(2026, 4, 30), picker.Date);
        Assert.False(picker.IsOpen);
        Assert.Null(request.CurrentDate); // completed: it lets go

        Open(picker, out var again);
        Assert.True(again.Clear());
        _ui.RunPending();
        Assert.Null(picker.Date);
    }

    [Fact]
    public void AMonthAndYearPicker()
    {
        var picker = Arranged(new SkUiDatePicker
        {
            WheelFormat = "MM/yy",
            Format = "MM/yy",
            MinimumDate = new DateTime(2026, 1, 1),
            MaximumDate = new DateTime(2036, 12, 31),
            Date = new DateTime(2028, 5, 17)
        });
        Assert.Equal("05/28", FieldText(picker));
        var content = Open(picker, out var request);
        Assert.Equal(["01", "02", "03"], request.Columns[0].Take(3));
        Turn(content, 0, 8);
        Assert.Equal(new DateTime(2028, 9, 1), picker.Date); // the 1st: the day has no wheel
    }

    [Fact]
    public void HeadersAndTheLoopingSetting()
    {
        var picker = Arranged(new SkUiDatePicker { AreColumnHeadersVisible = true, IsLooping = false });
        Open(picker, out var request);
        Assert.Equal(["Month", "Day", "Year"], request.Headers.Select(header => header ?? string.Empty));
        Assert.Equal([false, false, false], request.Loops);
    }

    [Fact]
    public void ChangingTheRangeWhileOpenClosesThePopup()
    {
        var picker = Arranged(new SkUiDatePicker());
        Open(picker, out _);
        picker.MaximumDate = new DateTime(2099, 1, 1);
        _ui.RunPending();
        Assert.False(picker.IsOpen);
    }
}
