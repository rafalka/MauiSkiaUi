using MauiSkiaUi.LeakTests;

namespace MauiSkiaUi.DeviceTests;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().UseSkiaUi()
            .ConfigureFonts(fonts => fonts.AddFont("RobotoMono-Regular.ttf", RenderCheck.MauiFontAlias));
        return builder.Build();
    }
}

public sealed class App : Application
{
    protected override Window CreateWindow(IActivationState? activationState) => new(new DeviceTestsShell()) { Title = "SkiaUi Device Tests" };
}

/// <summary>
/// Scenario pages are pushed on this Shell's navigation stack and popped again; Shell disconnects the handlers of a
/// popped page, as it does in apps.
/// </summary>
public sealed class DeviceTestsShell : Shell
{
    public DeviceTestsShell()
    {
        FlyoutBehavior = FlyoutBehavior.Disabled;
        Items.Add(new ShellContent { Title = "Memory leaks", Route = "leaks", ContentTemplate = new DataTemplate(typeof(MemoryLeaksPage)) });
    }
}

/// <summary>
/// Launch options, set by the platform entry points: <c>--autorun</c> runs the detector self-test and the scenarios
/// once the page is shown, <c>--exit</c> quits afterwards, <c>--scenarios a,b</c> limits the run,
/// <c>--placement-tolerance d</c> overrides the hosted check's tolerance (DIPs; a negative one must make it fail).
/// </summary>
public sealed record DeviceTestOptions(bool Autorun, bool ExitWhenDone, IReadOnlyList<string> Scenarios, double? PlacementTolerance = null)
{
    public static DeviceTestOptions Current { get; set; } = new(false, false, []);

    public static DeviceTestOptions Parse(IReadOnlyList<string> args)
    {
        var scenarios = new List<string>();
        string? tolerance = null;
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (args[index] == "--scenarios")
                scenarios.AddRange(Split(args[index + 1]));
            else if (args[index] == "--placement-tolerance")
                tolerance = args[index + 1];
        }
        return new(args.Contains("--autorun"), args.Contains("--exit"), scenarios, Number(tolerance));
    }

    public static DeviceTestOptions FromValues(bool autorun, bool exit, string? scenarios, string? placementTolerance = null) =>
        new(autorun, exit, Split(scenarios), Number(placementTolerance));

    private static double? Number(string? value) =>
        double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number) ? number : null;

    /// <summary>The scenarios to run: all, or the requested ones (unknown names fail fast).</summary>
    public IReadOnlyList<LeakScenario> SelectedScenarios() =>
        Scenarios.Count == 0 ? LeakScenarios.All : Scenarios.Select(LeakScenarios.Find).ToList();

    private static string[] Split(string? value) =>
        value?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];
}
