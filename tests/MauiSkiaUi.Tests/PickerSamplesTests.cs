using MauiSkiaUi.Core;
using MauiSkiaUiSamples.Samples.Controls.Pickers;
using MauiSkiaUiSamples.Samples.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// The picker examples of the samples app work as they describe: the currency converter's short fields and wide lists, the pizza
/// order's XAML pickers and total, the alarm clock's wheels and Clear, the date of birth's days, the Core form.
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public sealed class PickerSamplesTests : IDisposable
{
    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly RecordingPopupPresenter _presenter = new();
    private readonly ISkUiPopupPresenter? _previous = SkUiPopups.Presenter;
    private readonly IDisposable _dispatcher = SkUiTestHelpers.UseTestDispatcher();

    public PickerSamplesTests() => SkUiPopups.Presenter = _presenter;

    public void Dispose()
    {
        SkUiPopups.Presenter = _previous;
        _dispatcher.Dispose();
        _ui.Dispose();
    }

    /// <summary>The example's drawn content, laid out at a phone's width.</summary>
    private static SkUiView Content(MauiSkiaUiSamples.SamplePage page)
    {
        var content = Assert.IsAssignableFrom<SkUiView>(SkUiTestHelpers.DetachSampleContent(page));
        SkUiTestHelpers.Arrange(content, 380, 900);
        return content;
    }

    private static IEnumerable<ISkUiView> Descendants(ISkUiView view)
    {
        yield return view;
        if (view is SkUiView node)
            foreach (var child in node.SkiaChildren)
                foreach (var descendant in Descendants(child))
                    yield return descendant;
    }

    private static string? Value(SkUiView root, object node) =>
        SkUiSemanticsTree.Build(root).Flatten().Single(element => ReferenceEquals(element.Source, node)).Value;

    [Fact]
    public void TheCurrencyConverterShowsCodesOverWholeNames()
    {
        var root = Content(new CurrencyConverterSample());
        var pickers = Descendants(root).OfType<SkUiPicker>().ToList();
        Assert.Equal(2, pickers.Count);
        var from = pickers[0];
        Assert.Equal("Polish Zloty (PLN)", Value(root, from)); // screen readers: the whole name
        Assert.Contains(Descendants(root).OfType<SkUiLabel>(), label => label.Text?.EndsWith(" EUR") == true && label.FontSize > 20);

        // The list's width comes from its measured texts: the bundled font (Linux CI has no system fonts).
        using var font = SkUiTestHelpers.UseBundledFont();
        from.FontFamily = SkUiTestHelpers.BundledFontFamily;
        from.IsOpen = true;
        var request = _presenter.Current!;
        Assert.True(request.Bounds.Width > from.Width); // the list is wider than the narrow field
        Assert.True(request.Bounds.Width <= 260 + 0.5);

        // The swap button swaps the currencies.
        var swap = Descendants(root).OfType<SkUiButton>().Single();
        from.IsOpen = false;
        _ui.RunPending();
        Assert.NotNull(SkUiDiagnostics.SimulateTap(swap));
        Assert.Equal("Euro (EUR)", Value(root, from));
    }

    [Fact]
    public void ThePizzaOrderTotalsWhatIsChosen()
    {
        var page = new PizzaOrderSample();
        var root = Content(page);
        var model = Assert.IsType<PizzaOrderModel>(page.BindingContext);
        var pickers = Descendants(root).OfType<SkUiPicker>().ToList();
        var (size, crust, toppings) = (pickers[0], pickers[1], pickers[2]);

        // The size field is drawn by its template; the toppings are tokens.
        Assert.NotNull(size.FieldView);
        Assert.Equal(["Mozzarella", "Mushrooms"], toppings.TokenBoxes.Select(token => token.Text));
        var total = model.Total;

        // Out of stock: Salami and Pineapple cannot be chosen; the item texts come from the binding with the item's type.
        toppings.IsOpen = true;
        var request = Assert.IsType<SkUiMultiListPopupRequest>(_presenter.Current);
        Assert.Equal("Salami", request.Items[5]);
        Assert.False(request.ItemsEnabled![5]);
        Assert.False(request.SetSelected(5, true));
        Assert.True(request.SetSelected(4, true)); // Ham, a draft until Add
        Assert.Equal(total, model.Total);
        Assert.Equal("Add", request.AcceptText);
        Assert.True(request.Accept());
        _ui.RunPending();
        Assert.Contains(model.ChosenToppings, topping => ((Topping)topping).Name == "Ham");
        Assert.NotEqual(total, model.Total); // the command recomputed it

        size.SelectedIndex = 3;
        Assert.Equal(model.Sizes[3], model.Size);
        // The total follows the size however it changes (here from code, without the picker's command).
        var withSize = model.Total;
        model.Size = model.Sizes[0];
        Assert.NotEqual(withSize, model.Total);
        Assert.Equal("Choose a crust", crust.Placeholder);
        crust.SelectedIndex = 0;
        Assert.Equal("Thin", model.Crust);
    }

    [Fact]
    public void TheAlarmClockTurnsOffWithClear()
    {
        var root = Content(new AlarmClockSample());
        var time = Descendants(root).OfType<SkUiMultiColumnPicker>().Single();
        Assert.Equal("07:30", Value(root, time));
        var summary = Descendants(root).OfType<SkUiLabel>().Last();
        Assert.StartsWith("Rings at 07:30", summary.Text);

        time.IsOpen = true;
        var request = Assert.IsType<SkUiColumnsPopupRequest>(_presenter.Current);
        Assert.Equal(["Hour", "Minute"], request.Headers);
        Assert.Equal("No alarm", Descendants((SkUiView)request.Content!).OfType<SkUiButton>().First().Text);
        Assert.True(request.Clear()); // closes: the wheels cannot show "no alarm"
        Assert.True(request.IsCompleted);
        _ui.RunPending();

        Assert.Equal("The alarm is off.", summary.Text);
        Assert.Null(Value(root, time)); // the placeholder shows
    }

    [Fact]
    public void TheDateOfBirthDropsDaysAMonthDoesNotHave()
    {
        var root = Content(new DateOfBirthSample());
        var date = Descendants(root).OfType<SkUiMultiColumnPicker>().Single();
        var (days, months) = (date.Columns[0], date.Columns[1]);
        months.SelectedIndex = 2; // March
        days.SelectedIndex = 30; // the 31st
        Assert.Equal(30, days.SelectedIndex);

        months.SelectedIndex = 1; // February 1990: 28 days
        Assert.Equal(27, days.SelectedIndex);

        // Open: the wheel follows a change of the year (a leap year has the 29th).
        date.Columns[2].SelectedItem = 1992;
        date.IsOpen = true;
        var request = Assert.IsType<SkUiColumnsPopupRequest>(_presenter.Current);
        Assert.True(request.ItemsEnabled[0]![28]);
        Assert.False(request.ItemsEnabled[0]![29]);
        Assert.True(request.SetSelectedIndex(0, 28));
        date.Columns[2].SelectedItem = 1993;
        Assert.Equal(27, days.SelectedIndex);
        Assert.False(request.ItemsEnabled[0]![28]);
    }

    [Fact]
    public void TheBookingKeepsToWeekdaysOpeningHoursAndWholeMonths()
    {
        using var culture = DateTimeWheelsTests.UseCulture("en-US");
        var page = new BookingSample();
        var root = Content(page);
        var model = Assert.IsType<BookingModel>(page.BindingContext);
        var dates = Descendants(root).OfType<SkUiDatePicker>().ToList();
        var (day, expiry) = (dates[0], dates[1]);
        var time = Descendants(root).OfType<SkUiTimePicker>().Single();
        Assert.Equal(model.Day, day.Date);
        Assert.NotEqual(DayOfWeek.Saturday, day.Date!.Value.DayOfWeek);
        Assert.Null(time.Time);

        // Weekends and holidays are dimmed in the day's wheels.
        day.IsOpen = true;
        var request = Assert.IsType<SkUiDatePopupRequest>(_presenter.Current);
        var saturday = DateTime.Today.AddDays((DayOfWeek.Saturday - DateTime.Today.DayOfWeek + 7) % 7 + 7);
        Assert.False(request.IsDateEnabled(saturday));
        Assert.False(request.SetDate(saturday));
        Assert.Equal(["Month", "Day", "Year"], request.Headers.Select(header => header ?? string.Empty));
        day.IsOpen = false;
        _ui.RunPending();

        // Opening hours in quarter hours, chosen with OK; the wheels start on 9:00.
        time.IsOpen = true;
        var times = Assert.IsType<SkUiTimePopupRequest>(_presenter.Current);
        Assert.Equal(new TimeSpan(9, 0, 0), times.DefaultHighlightedTime);
        Assert.False(times.SetTime(new TimeSpan(18, 0, 0)));
        Assert.True(times.SetTime(new TimeSpan(10, 50, 0)));
        Assert.Null(model.Time); // a draft until OK
        Assert.True(times.Accept());
        _ui.RunPending();
        Assert.Equal(new TimeSpan(10, 45, 0), model.Time);

        // A card's expiry: month and year, the 1st of the month.
        expiry.IsOpen = true;
        var card = Assert.IsType<SkUiDatePopupRequest>(_presenter.Current);
        Assert.Equal(2, card.Columns.Count);
        Assert.True(card.CompleteDate(new DateTime(DateTime.Today.Year + 3, 7, 19)));
        _ui.RunPending();
        Assert.Equal(new DateTime(DateTime.Today.Year + 3, 7, 1), model.Expiry);
        Assert.Contains("card valid to 07/", model.Summary);
    }

    [Fact]
    public void TheCoreFormPicksWithFunctions()
    {
        var root = Content(new CorePickersSample());
        var host = Descendants(root).OfType<SkUiCoreHost>().Single();
        Assert.Same(root, host);
        var stack = Assert.IsType<SkUiCoreVerticalStackLayout>(host.Content);
        var pickers = stack.Children.OfType<SkUiCorePicker>().ToList();
        var (project, labels) = (pickers[0], pickers[1]);

        Assert.Equal("Website relaunch", Value(root, project));
        Assert.Equal(["feature", "frontend"], labels.TokenBoxes.Select(token => token.Text));
        project.IsOpen = true;
        var request = Assert.IsType<SkUiListPopupRequest>(_presenter.Current);
        Assert.False(request.ItemsEnabled![3]); // archived
        Assert.True(request.Bounds.Width <= 280 + 0.5);
    }
}
