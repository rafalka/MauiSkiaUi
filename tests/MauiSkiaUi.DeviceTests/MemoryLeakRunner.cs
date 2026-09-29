using System.Diagnostics;
using System.Text.Json;

using MauiSkiaUi.LeakTests;

namespace MauiSkiaUi.DeviceTests;

public enum LeakStatus
{
    NotRun,
    Running,
    Pass,
    Fail
}

/// <summary>Outcome of one on-device scenario run.</summary>
public sealed record LeakResult(string Name, LeakStatus Status, string? Details = null, double Seconds = 0, int Tracked = 0);

/// <summary>
/// Runs <see cref="LeakScenarios"/> on a device, with real handlers and platform views: each scenario's page is
/// pushed on the current Shell navigation stack, exercised, checked for objects it removed while open, then popped
/// (Shell disconnects the page's handlers, as for any navigation). Everything tracked — the page, every MAUI view,
/// drawn view and Core node, each handler, platform view and what surface handlers own (renderer, compositor, GPU /
/// software surfaces, overlay container) — must then be collected. Used by <see cref="MemoryLeaksPage"/>; with
/// <c>--autorun</c> (<see cref="RunForAutomationAsync"/>) results go to the console for scripts/device_tests.sh:
/// <c>SKUILEAK_START</c>, <c>SKUILEAK_RENDER {json}</c> (<see cref="RenderCheck"/>), <c>SKUILEAK_DETECTOR {json}</c>, one
/// <c>SKUILEAK {json}</c> per scenario, <c>SKUILEAK_DONE</c>.
/// </summary>
public static class MemoryLeakRunner
{

    /// <summary>How long objects may take to be collected after the page closes (native peers are released asynchronously).</summary>
    public static TimeSpan CollectTimeout { get; set; } = TimeSpan.FromSeconds(10);

    private static readonly Dictionary<string, LeakResult> s_results = [];

    /// <summary>Raised on the UI thread whenever a scenario's result changes.</summary>
    public static event Action<LeakResult>? ResultChanged;

    public static bool IsRunning { get; private set; }

    /// <summary>A text field on the test page that takes focus after a scenario focused one (set by <see cref="MemoryLeaksPage"/>).</summary>
    public static Entry? FocusSink { get; set; }

    /// <summary>Latest result per scenario (not run yet when missing).</summary>
    public static LeakResult ResultOf(string name) => s_results.TryGetValue(name, out var result) ? result : new(name, LeakStatus.NotRun);

    public static IReadOnlyList<LeakResult> Results => LeakScenarios.All.Select(scenario => ResultOf(scenario.Name)).ToList();

    /// <summary>Runs <paramref name="scenarios"/> one after another (UI thread). Returns their results.</summary>
    public static async Task<IReadOnlyList<LeakResult>> RunAsync(IEnumerable<LeakScenario> scenarios)
    {
        if (IsRunning)
            throw new InvalidOperationException("A leak run is already in progress.");
        IsRunning = true;
        var results = new List<LeakResult>();
        try
        {
            foreach (var scenario in scenarios.ToList())
                results.Add(await RunOneAsync(scenario));
        }
        finally
        {
            IsRunning = false;
        }
        return results;
    }

    /// <summary>
    /// Automation entry point: the detector self-test, then <paramref name="options"/>' scenarios, all reported on the
    /// console; exits the app afterwards when requested.
    /// </summary>
    public static async Task RunForAutomationAsync(DeviceTestOptions options)
    {
        var failed = 0;
        try
        {
            var scenarios = options.SelectedScenarios();
            Console.WriteLine($"SKUILEAK_START scenarios={scenarios.Count} platform={DeviceInfo.Platform} {DeviceInfo.VersionString} model={DeviceInfo.Model}");
            var render = RenderCheck.Run();
            Console.WriteLine("SKUILEAK_RENDER " + JsonSerializer.Serialize(render, LeakJson.Default.LeakResult));
            if (render.Status != LeakStatus.Pass)
                failed++;
            var detector = await CheckDetectorAsync();
            Console.WriteLine("SKUILEAK_DETECTOR " + JsonSerializer.Serialize(detector, LeakJson.Default.LeakResult));
            if (detector.Status != LeakStatus.Pass)
                failed++;
            failed += (await RunAsync(scenarios)).Count(result => result.Status != LeakStatus.Pass);
        }
        catch (Exception exception)
        {
            failed++;
            Console.WriteLine("SKUILEAK_ERROR " + exception.ToString().ReplaceLineEndings(" | "));
        }
        Console.WriteLine($"SKUILEAK_DONE failed={failed}");
        if (options.ExitWhenDone)
        {
            await Task.Delay(500); // let the console flush
            Environment.Exit(failed == 0 ? 0 : 1);
        }
    }

    /// <summary>
    /// Runs <see cref="LeakScenarios.DeliberateLeak"/> and reports whether the checker caught it (the detector works on
    /// this device and build). The retained root is released afterwards.
    /// </summary>
    public static async Task<LeakResult> CheckDetectorAsync()
    {
        try
        {
            var result = (await RunAsync([LeakScenarios.DeliberateLeak]))[0];
            return result.Status == LeakStatus.Fail && result.Details?.Contains("SkUiContentView", StringComparison.Ordinal) == true
                ? result with { Status = LeakStatus.Pass, Details = $"Detector caught the deliberate leak: {result.Details}" }
                : result with { Status = LeakStatus.Fail, Details = $"Detector missed the deliberate leak ({result.Status}: {result.Details})" };
        }
        finally
        {
            LeakScenarios.ReleaseDeliberateLeak();
        }
    }

    private static async Task<LeakResult> RunOneAsync(LeakScenario scenario)
    {
        Report(new LeakResult(scenario.Name, LeakStatus.Running));
        var watch = Stopwatch.StartNew();
        LeakResult result;
        try
        {
            var outcome = await OpenExerciseCloseAsync(scenario);
            var survivors = await LeakTracker.WaitForCollectionAsync(outcome.Tracked, CollectTimeout);
            var problems = new List<string>();
            if (outcome.InteractionProblem is { } interaction)
                problems.Add($"Interaction had no effect: {interaction}");
            if (outcome.DetachedSurvivors.Count > 0)
                problems.Add($"Removed while open but still alive: {string.Join(", ", outcome.DetachedSurvivors)}");
            if (survivors.Count > 0)
                problems.Add($"Still alive after close: {string.Join(", ", survivors)}");
            var summary = problems.Count == 0 ? $"{outcome.Tracked.Count} objects collected" : string.Join(" · ", problems);
            result = new LeakResult(scenario.Name, problems.Count == 0 ? LeakStatus.Pass : LeakStatus.Fail, summary,
                watch.Elapsed.TotalSeconds, outcome.Tracked.Count);
        }
        catch (Exception exception)
        {
            result = new LeakResult(scenario.Name, LeakStatus.Fail, $"{exception.GetType().Name}: {exception.Message}", watch.Elapsed.TotalSeconds);
        }
        Report(result);
        return result;
    }

    private sealed record Outcome(List<TrackedObject> Tracked, IReadOnlyList<string> DetachedSurvivors, string? InteractionProblem);

    /// <summary>
    /// Everything that references the scenario's UI lives in this method's state machine, which is gone once it
    /// returns: only weak references leave it.
    /// </summary>
    private static async Task<Outcome> OpenExerciseCloseAsync(LeakScenario scenario)
    {
        var navigation = Shell.Current?.Navigation
            ?? Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation
            ?? throw new InvalidOperationException("No navigation to host the scenario page.");
        var context = new LeakScenarioContext(delay => Task.Delay(delay), isDevice: true);
        var run = scenario.Create();
        var page = new ContentPage { Title = scenario.Name, BackgroundColor = Colors.White, Content = run.Build(context) };
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnLoaded(object? sender, EventArgs args) => loaded.TrySetResult();
        page.Loaded += OnLoaded;
        await navigation.PushAsync(page, animated: false);
        await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
        page.Loaded -= OnLoaded;
        await Task.Delay(400); // first layout and frames

        await run.InteractAsync(context);
        await Task.Delay(300);
        var problem = run.CheckInteraction();
        var detachedSurvivors = await LeakTracker.WaitForCollectionAsync(context.Detached, TimeSpan.FromSeconds(5));

        var tracked = LeakTracker.TrackTree(page);
        tracked.AddRange(context.Tracked);
        await navigation.PopAsync(animated: false);
        if (context.FocusedTextInput && FocusSink is { } sink)
        {
            // Hand text focus to the test page's own field (see LeakScenarioContext.FocusAsync).
            sink.Focus();
            await Task.Delay(300);
            sink.Unfocus();
        }
        return new Outcome(tracked, detachedSurvivors, problem);
    }

    private static void Report(LeakResult result)
    {
        s_results[result.Name] = result;
        if (result.Status is LeakStatus.Pass or LeakStatus.Fail && result.Name != LeakScenarios.DeliberateLeak.Name)
            Console.WriteLine("SKUILEAK " + JsonSerializer.Serialize(result, LeakJson.Default.LeakResult));
        ResultChanged?.Invoke(result);
    }
}

/// <summary>Source-generated JSON for the console results (trimming / Native AOT safe).</summary>
[System.Text.Json.Serialization.JsonSourceGenerationOptions(UseStringEnumConverter = true)]
[System.Text.Json.Serialization.JsonSerializable(typeof(LeakResult))]
internal sealed partial class LeakJson : System.Text.Json.Serialization.JsonSerializerContext;
