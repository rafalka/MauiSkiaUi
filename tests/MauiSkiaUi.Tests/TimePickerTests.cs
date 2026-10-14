using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// The time picker: MAUI's rules (midnight by default, a time of a day, <c>TimeSelected</c>, MAUI's opened and closed events), the
/// range and the steps, the culture's clock, and a presenter's own time UI.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class TimePickerTests : IDisposable
{
    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly RecordingPopupPresenter _presenter = new();
    private readonly ISkUiPopupPresenter? _previous = SkUiPopups.Presenter;
    private readonly IDisposable _culture = DateTimeWheelsTests.UseCulture("en-US");

    public TimePickerTests() => SkUiPopups.Presenter = _presenter;

    public void Dispose()
    {
        SkUiPopups.Presenter = _previous;
        _culture.Dispose();
        _ui.Dispose();
    }

    private static SkUiTimePicker Arranged(SkUiTimePicker picker)
    {
        var stack = new SkUiVerticalStackLayout { Padding = new Thickness(10, 20) };
        stack.Children.Add(picker);
        SkUiTestHelpers.Arrange(stack, 360, 700);
        return picker;
    }

    private SkUiColumnsPopup Open(SkUiTimePicker picker, out SkUiTimePopupRequest request)
    {
        picker.IsOpen = true;
        request = Assert.IsType<SkUiTimePopupRequest>(_presenter.Current);
        return Assert.IsType<SkUiColumnsPopup>(request.Content);
    }

    /// <summary>The field's text, with .NET's narrow space before AM / PM as a space.</summary>
    internal static string? Field(SkUiView picker) => DatePickerTests.FieldText(picker)?.Replace('\u202F', ' ');

    [Fact]
    public void MauisRulesHold()
    {
        var picker = Arranged(new SkUiTimePicker());
        Assert.Equal(TimeSpan.Zero, picker.Time);
        Assert.Equal("t", picker.Format);
        Assert.Equal("12:00 AM", Field(picker));

        var changes = new List<(TimeSpan?, TimeSpan?)>();
        picker.TimeSelected += (_, args) => changes.Add((args.OldTime, args.NewTime));
        picker.Time = new TimeSpan(14, 5, 0);
        Assert.Equal("2:05 PM", Field(picker));
        picker.Time = TimeSpan.FromHours(25); // not a time of a day: ignored
        Assert.Equal(new TimeSpan(14, 5, 0), picker.Time);
        Assert.Throws<ArgumentOutOfRangeException>(() => picker.SetTime(TimeSpan.FromHours(-1)));

        // The range keeps it in, as the date picker's.
        picker.MaximumTime = new TimeSpan(12, 0, 0);
        Assert.Equal(new TimeSpan(12, 0, 0), picker.Time);
        picker.Format = "HH:mm";
        Assert.Equal("12:00", Field(picker));
        picker.Time = null;
        Assert.Null(Field(picker));
        Assert.Equal([(TimeSpan.Zero, new TimeSpan(14, 5, 0)), (new TimeSpan(14, 5, 0), new TimeSpan(12, 0, 0)), (new TimeSpan(12, 0, 0), null)],
            changes);
    }

    [Fact]
    public void TheWheelsChooseOnTheCulturesClock()
    {
        var picker = Arranged(new SkUiTimePicker { Time = new TimeSpan(9, 30, 0) });
        var opened = new List<string>();
        picker.Opened += (_, args) => opened.Add(args.GetType().Name);
        var content = Open(picker, out var request);
        using var surface = new SkUiTestSurface(content, 360, 400);
        surface.Frame();

        Assert.Equal([nameof(TimePickerOpenedEventArgs)], opened);
        Assert.False(request.Is24Hour);
        Assert.False(request.IncludesSeconds);
        Assert.Equal([9, 30, 0], request.SelectedIndices);
        Assert.Equal(new TimeSpan(9, 30, 0), request.Time);

        DatePickerTests.Turn(content, 2, 1); // PM
        Assert.Equal(new TimeSpan(21, 30, 0), picker.Time);
        DatePickerTests.Turn(content, 0, 0); // 12
        Assert.Equal(new TimeSpan(12, 30, 0), picker.Time);
    }

    [Fact]
    public void StepsAndARangeLimitTheWheels()
    {
        var picker = Arranged(new SkUiTimePicker
        {
            WheelFormat = "HH:mm",
            MinuteInterval = 15,
            MinimumTime = new TimeSpan(9, 0, 0),
            MaximumTime = new TimeSpan(17, 30, 0),
            Time = null,
            DefaultHighlightedTime = new TimeSpan(12, 10, 0)
        });
        var content = Open(picker, out var request);
        using var surface = new SkUiTestSurface(content, 360, 400);
        surface.Frame();

        Assert.Equal(["00", "15", "30", "45"], request.Columns[1]);
        Assert.Equal(new TimeSpan(12, 0, 0), request.DefaultHighlightedTime);
        Assert.Equal(12, content.Wheel(0).Position);
        Assert.False(request.ItemsEnabled[0]![8]);
        Assert.True(request.ItemsEnabled[0]![9]);

        // 12:45, then 17: 17:45 is after the maximum, so 17:30, and its minute is dimmed.
        DatePickerTests.Turn(content, 1, 3);
        Assert.Equal(new TimeSpan(12, 45, 0), picker.Time);
        DatePickerTests.Turn(content, 0, 17);
        Assert.Equal(new TimeSpan(17, 30, 0), picker.Time);
        Assert.Equal(2, content.Wheel(1).Position);
        Assert.False(request.ItemsEnabled[1]![3]);

        // A presenter's own time: rounded down to the steps, refused outside the range.
        Assert.False(request.SetTime(new TimeSpan(8, 0, 0)));
        Assert.True(request.CompleteTime(new TimeSpan(10, 7, 0)));
        _ui.RunPending();
        Assert.Equal(new TimeSpan(10, 0, 0), picker.Time);
        Assert.False(picker.IsOpen);
    }

    [Fact]
    public void SecondsHaveAWheelWhenTheFormatHasThem()
    {
        var picker = Arranged(new SkUiTimePicker { WheelFormat = "T", SecondInterval = 10, Time = new TimeSpan(1, 2, 37) });
        Open(picker, out var request);
        Assert.True(request.IncludesSeconds);
        Assert.Equal(4, request.Columns.Count);
        Assert.Equal(["00", "10", "20", "30", "40", "50"], request.Columns[2]);
        Assert.Equal([1, 2, 3, 0], request.SelectedIndices);
    }
}

/// <summary>
/// The date and time picker (no MAUI counterpart): no value by default, the time kept, the range's
/// times on its first and last days; and the Core date and time pickers.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class DateTimePickerTests : IDisposable
{
    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly RecordingPopupPresenter _presenter = new();
    private readonly ISkUiPopupPresenter? _previous = SkUiPopups.Presenter;
    private readonly IDisposable _culture = DateTimeWheelsTests.UseCulture("en-US");

    public DateTimePickerTests() => SkUiPopups.Presenter = _presenter;

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

    [Fact]
    public void ADayAndATimeOnOnePopup()
    {
        var start = new DateTime(2026, 10, 13, 9, 0, 0);
        var picker = Arranged(new SkUiDateTimePicker { MinimumDate = start, MaximumDate = start.AddDays(30), MinuteInterval = 30, Title = "Appointment" });
        Assert.Null(picker.Date);
        Assert.Equal("g", picker.Format);
        Assert.Null(TimePickerTests.Field(picker));

        picker.IsOpen = true;
        var request = Assert.IsType<SkUiDatePopupRequest>(_presenter.Current);
        var content = Assert.IsType<SkUiColumnsPopup>(request.Content);
        using var surface = new SkUiTestSurface(content, 360, 400);
        surface.Frame();
        Assert.True(request.IncludesTime);
        Assert.Equal(6, request.Columns.Count); // month, day, year, hour, minute, AM / PM
        Assert.Equal("Oct", request.Columns[0][9]);
        Assert.Equal(["00", "30"], request.Columns[4]);

        Assert.True(request.SetDate(new DateTime(2026, 10, 13, 15, 30, 0)));
        Assert.Equal(new DateTime(2026, 10, 13, 15, 30, 0), picker.Date);
        Assert.Equal("10/13/2026 3:30 PM", TimePickerTests.Field(picker));
        Assert.Null(request.ItemsEnabled[3]); // every afternoon hour

        // AM on the first day: 3:30 AM is before the minimum, so 9:00, and the earlier hours are dimmed.
        DatePickerTests.Turn(content, 5, 0);
        Assert.Equal(start, picker.Date);
        Assert.False(request.ItemsEnabled[3]![8]);
        Assert.True(request.ItemsEnabled[3]![9]);
        // The next day keeps the time.
        DatePickerTests.Turn(content, 1, 13);
        Assert.Equal(new DateTime(2026, 10, 14, 9, 0, 0), picker.Date);
    }

    [Fact]
    public void TheCorePickersWorkAsTheirTwins()
    {
        using var _ = DateTimeWheelsTests.UseCulture("pl-PL");
        var date = new Core.SkUiCoreDatePicker().SetDate(new DateTime(2026, 3, 31, 10, 0, 0)).SetMinimumDate(new DateTime(2026, 3, 15));
        var time = new Core.SkUiCoreTimePicker().SetMinuteInterval(5).SetTime(new TimeSpan(7, 40, 0)).SetAreColumnHeadersVisible(true);
        var both = new Core.SkUiCoreDateTimePicker().SetWheelFormat("dd MMM HH:mm").SetDefaultHighlightedDate(new DateTime(2027, 1, 1));
        var stack = new Core.SkUiCoreVerticalStackLayout().Add(date).Add(time).Add(both);
        var host = new Core.SkUiCoreHost().SetContent(stack);
        SkUiTestHelpers.Arrange(host, 360, 700);

        Assert.Equal(new DateTime(2026, 3, 31), date.Date);
        var changes = new List<DateTime?>();
        date.DateSelected += (_, args) => changes.Add(args.NewDate);
        date.SetMinimumDate(new DateTime(2026, 4, 1));
        Assert.Equal([new DateTime(2026, 4, 1)], changes);

        date.SetIsOpen(true);
        var dateRequest = Assert.IsType<SkUiDatePopupRequest>(_presenter.Current);
        Assert.Equal([0, 3, 0], dateRequest.SelectedIndices); // dd.MM.yyyy: the 1st, April, the minimum's year
        Assert.True(dateRequest.CompleteDate(new DateTime(2026, 4, 30)));
        _ui.RunPending();
        Assert.Equal(new DateTime(2026, 4, 30), date.Date);

        time.SetIsOpen(true);
        var timeRequest = Assert.IsType<SkUiTimePopupRequest>(_presenter.Current);
        Assert.True(timeRequest.Is24Hour);
        Assert.Equal("Hour Minute", string.Join(' ', timeRequest.Headers)); // SkUiStrings' texts, whatever the culture
        Assert.Equal([7, 8], timeRequest.SelectedIndices);
        Assert.True(timeRequest.Clear());
        _ui.RunPending();
        Assert.Null(time.Time);

        Assert.Null(both.Date);
        both.SetIsOpen(true);
        var bothRequest = Assert.IsType<SkUiDatePopupRequest>(_presenter.Current);
        Assert.Equal(4, bothRequest.Columns.Count);
        // Without a year wheel the year is the highlighted date's: another one cannot be chosen.
        Assert.False(bothRequest.CompleteDate(new DateTime(2028, 2, 3, 4, 5, 0)));
        Assert.True(bothRequest.CompleteDate(new DateTime(2027, 2, 3, 4, 5, 0)));
        _ui.RunPending();
        Assert.Equal(new DateTime(2027, 2, 3, 4, 5, 0), both.Date);
    }
}

/// <summary>MAUI's <c>DatePicker</c> and <c>TimePicker</c> XAML with the prefix changed: the same attributes and bindings work.</summary>
[Collection(RuntimeXamlCollection.Name)]
public class DateTimePickerXamlTests
{
    [Fact]
    public void MauisXamlWorksWithThePrefixChanged()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        const string xaml = """
            <VerticalStackLayout xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                                 xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                                 xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <sk:SkUiDatePicker Date="{Binding Due}" MinimumDate="2026-01-01" MaximumDate="2026-12-31" Format="dd MMM yyyy"
                                 WheelFormat="MMMM yyyy" AreColumnHeadersVisible="True" />
              <sk:SkUiTimePicker Time="{Binding Alarm}" Format="HH:mm" MinuteInterval="5" MinimumTime="06:00" />
              <sk:SkUiDateTimePicker Date="{Binding Meeting}" MinuteInterval="15" Placeholder="When?" />
            </VerticalStackLayout>
            """;
        var model = new Plan();
        var root = new VerticalStackLayout { BindingContext = model };
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var date = Assert.IsType<SkUiDatePicker>(root.Children[0]);
        var time = Assert.IsType<SkUiTimePicker>(root.Children[1]);
        var meeting = Assert.IsType<SkUiDateTimePicker>(root.Children[2]);

        Assert.Equal(new DateTime(2026, 7, 1), date.Date);
        Assert.Equal((new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), "MMMM yyyy"), (date.MinimumDate, date.MaximumDate, date.WheelFormat));
        Assert.True(date.AreColumnHeadersVisible);
        Assert.Equal(new TimeSpan(6, 30, 0), time.Time);
        Assert.Equal((5, new TimeSpan(6, 0, 0)), (time.MinuteInterval, time.MinimumTime));
        Assert.Null(meeting.Date);
        Assert.Equal("When?", meeting.Placeholder);

        // Two-way by default.
        date.Date = new DateTime(2026, 8, 1);
        time.Time = new TimeSpan(7, 0, 0);
        meeting.Date = new DateTime(2026, 9, 1, 10, 0, 0);
        Assert.Equal((new DateTime(2026, 8, 1), new TimeSpan(7, 0, 0), new DateTime(2026, 9, 1, 10, 0, 0)), (model.Due, model.Alarm, model.Meeting));
    }

    public sealed class Plan
    {
        public DateTime? Due { get; set; } = new DateTime(2026, 7, 1);

        public TimeSpan? Alarm { get; set; } = new TimeSpan(6, 30, 0);

        public DateTime? Meeting { get; set; }
    }
}
