using System.Diagnostics;

using MauiSkiaUi.LeakTests;

namespace MauiSkiaUi.DeviceTests;

/// <summary>
/// On-device memory leak checks (<see cref="MemoryLeakRunner"/>): run one scenario, all of them, or only the failed
/// ones; with <c>--autorun</c> it runs everything on its own (scripts/device_tests.sh). The page uses plain MAUI
/// controls, so the harness is not part of what it measures. Automation ids:
/// <c>Leak_RunAll</c>, <c>Leak_RerunFailed</c>, <c>Leak_CheckDetector</c>, <c>Leak_Summary</c>, and per scenario
/// <c>Leak_Run_&lt;name&gt;</c>, <c>Leak_Status_&lt;name&gt;</c>, <c>Leak_Details_&lt;name&gt;</c>.
/// </summary>
public sealed class MemoryLeaksPage : ContentPage
{
    private readonly Dictionary<string, (Label Status, Label Details)> _rows = [];
    private readonly Label _summary = new() { FontSize = 13, TextColor = LeakColors.Ink, AutomationId = "Leak_Summary" };
    private readonly Label _detector = new() { FontSize = 12, TextColor = LeakColors.Caption, AutomationId = "Leak_DetectorResult", IsVisible = false };
    private readonly List<Button> _buttons = [];
    // Takes text focus after a scenario focused a field (MemoryLeakRunner.FocusSink).
    private readonly Entry _focusSink = new() { Placeholder = "Focus sink", FontSize = 12, AutomationId = "Leak_FocusSink" };

    public MemoryLeaksPage()
    {
        Title = "Memory leaks";
        MemoryLeakRunner.FocusSink = _focusSink;
        BackgroundColor = LeakColors.PageBackground;
        var header = new VerticalStackLayout
        {
            Padding = new Thickness(12, 8), Spacing = 6,
            Children =
            {
                new Label
                {
                    Text = "Each scenario opens a page, exercises it (clicks, layout changes, scrolling, gestures, animations), closes it, " +
                           "and checks that the page, every view, handler, platform view and surface was garbage collected.",
                    FontSize = 13, TextColor = LeakColors.Caption
                },
                new HorizontalStackLayout { Spacing = 8, Children = { Command("Run all", "Leak_RunAll", RunAll), Command("Rerun failed", "Leak_RerunFailed", RerunFailed), Command("Self-checks", "Leak_CheckDetector", CheckDetector) } },
                _summary,
                _detector,
                _focusSink
            }
        };
        if (Debugger.IsAttached)
            header.Children.Insert(0, new Label
            {
                Text = "Debugger attached: Hot Reload / the debugger may keep instances alive.",
                BackgroundColor = Colors.LightPink, Padding = new Thickness(6), FontSize = 13, AutomationId = "Leak_DebuggerWarning"
            });

        var list = new VerticalStackLayout { Padding = new Thickness(12, 0, 12, 24), Spacing = 6 };
        foreach (var group in LeakScenarios.All.GroupBy(scenario => scenario.Group))
        {
            list.Children.Add(new Label { Text = group.Key, FontAttributes = FontAttributes.Bold, FontSize = 15, TextColor = LeakColors.Ink, Margin = new Thickness(0, 8, 0, 0) });
            foreach (var scenario in group)
                list.Children.Add(Row(scenario));
        }
        Content = new Grid
        {
            RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
            Children = { header, WithRow(new ScrollView { Content = list }, 1) }
        };
        Loaded += OnLoaded;
        Unloaded += (_, _) => MemoryLeakRunner.ResultChanged -= OnResultChanged;
        foreach (var result in MemoryLeakRunner.Results)
            Show(result);
        UpdateSummary();
    }

    private static bool s_autorunStarted;

    private async void OnLoaded(object? sender, EventArgs e)
    {
        MemoryLeakRunner.ResultChanged += OnResultChanged;
        if (!DeviceTestOptions.Current.Autorun || s_autorunStarted)
            return;
        s_autorunStarted = true;
        SetBusy(true);
        await Task.Delay(500); // first frames of the page before the first scenario
        await MemoryLeakRunner.RunForAutomationAsync(DeviceTestOptions.Current);
        SetBusy(false);
        UpdateSummary();
    }

    private View Row(LeakScenario scenario)
    {
        var status = new Label { FontSize = 13, FontAttributes = FontAttributes.Bold, AutomationId = "Leak_Status_" + scenario.Name, VerticalOptions = LayoutOptions.Center };
        var details = new Label { FontSize = 12, TextColor = LeakColors.Caption, AutomationId = "Leak_Details_" + scenario.Name };
        _rows[scenario.Name] = (status, details);
        var run = Command("Run", "Leak_Run_" + scenario.Name, () => Run([scenario]));
        var text = new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                new Label { Text = scenario.Name, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = LeakColors.Ink },
                new Label { Text = scenario.Description, FontSize = 12, TextColor = LeakColors.Caption },
                details
            }
        };
        var grid = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto)],
            ColumnSpacing = 8,
            Children = { text, WithColumn(status, 1), WithColumn(run, 2) }
        };
        return new Border { Stroke = LeakColors.Border, BackgroundColor = Colors.White, Padding = new Thickness(10, 8), Content = grid };
    }

    private Button Command(string text, string automationId, Func<Task> action)
    {
        var button = new Button { Text = text, AutomationId = automationId, FontSize = 13, Padding = new Thickness(12, 4), BackgroundColor = LeakColors.Accent, TextColor = Colors.White };
        button.Clicked += async (_, _) =>
        {
            if (MemoryLeakRunner.IsRunning)
                return;
            SetBusy(true);
            try
            {
                await action();
            }
            finally
            {
                SetBusy(false);
                UpdateSummary();
            }
        };
        _buttons.Add(button);
        return button;
    }

    private Task RunAll() => Run(LeakScenarios.All);

    private Task RerunFailed() =>
        Run(LeakScenarios.All.Where(scenario => MemoryLeakRunner.ResultOf(scenario.Name).Status == LeakStatus.Fail));

    private async Task CheckDetector()
    {
        var render = await RenderCheck.RunAsync();
        var result = await MemoryLeakRunner.CheckDetectorAsync();
        _detector.IsVisible = true;
        _detector.TextColor = result.Status == LeakStatus.Pass && render.Status == LeakStatus.Pass ? LeakColors.Pass : LeakColors.Fail;
        _detector.Text = $"Detector: {result.Details}\nRendering: {render.Details}";
    }

    private static Task Run(IEnumerable<LeakScenario> scenarios) => MemoryLeakRunner.RunAsync(scenarios);

    private void SetBusy(bool busy)
    {
        foreach (var button in _buttons)
            button.IsEnabled = !busy;
    }

    private void OnResultChanged(LeakResult result)
    {
        Show(result);
        UpdateSummary();
    }

    private void Show(LeakResult result)
    {
        if (!_rows.TryGetValue(result.Name, out var row))
            return;
        row.Status.Text = result.Status switch
        {
            LeakStatus.Pass => "PASS",
            LeakStatus.Fail => "FAIL",
            LeakStatus.Running => "…",
            _ => ""
        };
        row.Status.TextColor = result.Status == LeakStatus.Fail ? LeakColors.Fail : result.Status == LeakStatus.Pass ? LeakColors.Pass : LeakColors.Caption;
        row.Details.Text = result.Details is null ? "" : result.Seconds > 0 ? $"{result.Details} ({result.Seconds:F1} s)" : result.Details;
    }

    private void UpdateSummary()
    {
        var results = MemoryLeakRunner.Results;
        var passed = results.Count(result => result.Status == LeakStatus.Pass);
        var failed = results.Count(result => result.Status == LeakStatus.Fail);
        var running = results.FirstOrDefault(result => result.Status == LeakStatus.Running);
        _summary.Text = $"{passed} passed · {failed} failed · {results.Count - passed - failed} not run" + (running is null ? "" : $" · running {running.Name}");
    }

    private static View WithRow(View view, int row)
    {
        Grid.SetRow(view, row);
        return view;
    }

    private static View WithColumn(View view, int column)
    {
        Grid.SetColumn(view, column);
        return view;
    }
}
