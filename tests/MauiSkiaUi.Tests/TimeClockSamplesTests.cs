using MauiSkiaUiSamples.Samples.Controls.TimeClock;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// The 1.18 time-clock examples of the samples app work as they describe: the ring and the bars follow the clock, the time is in
/// tabular figures, the status is a live region, a break is picked in a sheet; the shift filters, claims and leave requests.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class TimeClockSamplesTests : IDisposable
{
    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly IDisposable _dispatcher = SkUiTestHelpers.UseTestDispatcher();
    private readonly IDisposable _font = SkUiTestHelpers.UseBundledFont();
    private double _time;

    public void Dispose()
    {
        _font.Dispose();
        _dispatcher.Dispose();
        _ui.Dispose();
    }

    private static IEnumerable<ISkUiView> Descendants(ISkUiView view)
    {
        yield return view;
        if (view is SkUiView node)
            foreach (var child in node.SkiaChildren)
                foreach (var descendant in Descendants(child))
                    yield return descendant;
    }

    private void Settle(SkUiTestSurface surface)
    {
        for (var frame = 0; frame < 60; frame++)
            surface.Frame(_time += 16);
        _ui.RunPending();
    }

    private static void Activate(ISkUiView view) =>
        ((ISkUiAccessibleNode)view).PerformSemanticsAction(SkUiSemanticsActions.Activate);

    [Fact]
    public async Task TheTimeClockFollowsTheClockAndTakesABreakFromASheet()
    {
        var page = new TimeClockSample();
        var root = Assert.IsAssignableFrom<SkUiView>(page.Content);
        using var surface = new SkUiTestSurface(root, 400, 900);
        Settle(surface);

        var ring = Descendants(root).OfType<SkUiProgressRing>().Single();
        var time = Descendants(ring).OfType<SkUiLabel>().First();
        var status = Descendants(root).OfType<SkUiLabel>().Single(label => label.Text?.StartsWith("Clocked in at") == true);
        Assert.Equal("5:14:05", time.Text);
        Assert.Contains(SkUiFontFeature.TabularFigures, time.FontFeatures!);
        Assert.Equal(SkUiLiveRegion.Polite, SkUiAccessibility.GetLiveRegion(status));
        var clockedIn = status.Text;
        Assert.True(SkUiAccessibility.GetUpdatesFrequently(ring));

        // Half an hour later: the time, the ring and the day's bar follow.
        var progress = ring.Progress;
        page.Tick(TimeSpan.FromMinutes(30));
        Assert.Equal("5:44:05", time.Text);
        Assert.True(ring.Progress > progress);
        var worked = Descendants(root).OfType<SkUiStackedBar>().First().Segments[0];
        Assert.Equal(new TimeSpan(5, 44, 5).TotalMinutes, worked.Value, 3);

        // "Take a break": a sheet with lengths and types; a type closes it with both, and the status says so.
        var picking = page.StartOrEndBreakAsync();
        Settle(surface);
        // ShowAsync opens the sheet on the surface root's overlay layer.
        var sheet = Descendants(root.OverlayIfAny!).OfType<SkUiSheet>().Single(candidate => candidate.IsOpen);
        var lengths = Descendants(sheet).OfType<SkUiChipGroup>().Single();
        lengths.Chips[0].IsSelected = true; // 10 min
        Activate(Descendants(sheet).OfType<SkUiButton>().Single(button => button.Text?.StartsWith("Coffee") == true));
        Settle(surface);
        await picking.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.StartsWith("On a coffee break since", status.Text);
        Assert.Equal("End break", Descendants(root).OfType<SkUiButton>().Single(button => button.Text == "End break").Text);

        // The ring fills with the break; past its length the caption says by how much.
        page.Tick(TimeSpan.FromMinutes(5));
        Assert.Equal(0.5, ring.Progress, 3);
        page.Tick(TimeSpan.FromMinutes(7));
        Assert.Equal(1, ring.Progress, 3);
        Assert.Contains(Descendants(ring).OfType<SkUiLabel>(), label => label.Text == "break · over by 2 min");

        // Ending the break goes back to work; the worked time did not grow during it.
        await page.StartOrEndBreakAsync();
        Assert.Equal(clockedIn, status.Text); // still the morning's clock-in, not the break's end
        Assert.Equal("5:44:05", time.Text);
    }

    [Fact]
    public void TheShiftsFilterClaimAndRequestLeave()
    {
        var page = new ShiftsAndLeaveSample();
        var root = Assert.IsAssignableFrom<SkUiView>(page.Content);
        using var surface = new SkUiTestSurface(root, 400, 800);
        Settle(surface);

        var tabs = Descendants(root).OfType<SkUiTabView>().Single();
        Assert.NotNull(tabs.HeaderTemplate);
        Assert.Equal(["9", "3", null], tabs.Tabs.Select(tab => tab.BadgeText));

        // A filter chip narrows the list to its shifts and says so.
        var list = Descendants(root).OfType<SkUiCollectionView>().Single();
        var filters = Descendants(root).OfType<SkUiChipGroup>().First();
        filters.Chips.Single(chip => chip.Text == "Night").IsSelected = true;
        var shown = Assert.IsAssignableFrom<IEnumerable<ShiftsAndLeaveSample.ShiftWeek>>(list.ItemsSource).SelectMany(week => week).ToList();
        Assert.NotEmpty(shown);
        Assert.All(shown, shift => Assert.Equal("Night", shift.Kind));
        // The summary sums the shifts' own lengths (not all shifts are 8 h).
        var hours = (int)shown.Aggregate(TimeSpan.Zero, (sum, shift) => sum + shift.Length).TotalHours;
        var summary = $"{shown.Count} {(shown.Count == 1 ? "shift" : "shifts")} · {hours} h (filtered)";
        Assert.Contains(Descendants(root).OfType<SkUiLabel>(), label => label.Text == summary);

        // Weekend narrows the kinds: weekend nights only (which days those are depends on today).
        filters.Chips.Single(chip => chip.Text == "Weekend").IsSelected = true;
        var weekendNights = list.ItemsSource is IEnumerable<ShiftsAndLeaveSample.ShiftWeek> weeks ? weeks.SelectMany(week => week).ToList() : [];
        Assert.All(weekendNights, shift => Assert.True(shift.Kind == "Night" && shift.IsWeekend));
        Assert.Equal(shown.Count(shift => shift.IsWeekend), weekendNights.Count);
        filters.Chips.Single(chip => chip.Text == "Weekend").IsSelected = false;

        // Claiming an open shift moves it to My shifts: both counts change.
        tabs.SelectedIndex = 1;
        Settle(surface);
        Activate(Descendants(root).OfType<SkUiChip>().First(chip => chip.Text == "Claim"));
        Assert.Equal(["10", "2", null], tabs.Tabs.Select(tab => tab.BadgeText));

        // A vacation request from the sheet: the balance ring and the bar take the requested days, the Leave tab counts it.
        tabs.SelectedIndex = 2;
        Settle(surface);
        var vacationRing = Descendants(root).OfType<SkUiProgressRing>().First();
        var before = vacationRing.Progress;
        Activate(Descendants(root).OfType<SkUiButton>().Single(button => button.Text == "Request leave"));
        Settle(surface);
        var sheet = Descendants(root).OfType<SkUiSheet>().Single();
        Assert.True(sheet.IsOpen);
        var monday = DateTime.Today.AddDays(7 - ((int)DateTime.Today.DayOfWeek + 6) % 7);
        Descendants(sheet).OfType<SkUiDateRangePicker>().Single().Value = new SkUiDateRange(monday, monday.AddDays(2));
        Activate(Descendants(sheet).OfType<SkUiButton>().Single(button => button.Text == "Send request"));
        Settle(surface);
        Assert.False(sheet.IsOpen);
        Assert.Equal(before - 3d / 26, vacationRing.Progress, 3);
        Assert.Equal("1", tabs.Tabs[2].BadgeText);
        var bar = Descendants(root).OfType<SkUiStackedBar>().Single();
        Assert.Equal(3, bar.Segments[1].Value);
    }
}
