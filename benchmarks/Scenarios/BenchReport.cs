using System.Globalization;
using System.Text.Json;

namespace MauiSkiaUi.Benchmarks;

/// <summary>Command-line / launch-argument options shared by both runners.</summary>
public sealed class BenchArgs
{
    public int Runs { get; private set; } = 6;
    public int Warmup { get; private set; } = 1;
    public IReadOnlyList<string> ScenarioNames { get; private set; } = [];
    public string? JsonPath { get; private set; }
    public string Label { get; private set; } = "current";
    public bool List { get; private set; }
    /// <summary>Device app: run immediately on launch (otherwise wait for the Run button).</summary>
    public bool AutoRun { get; private set; }
    /// <summary>Device app: terminate the process after an automatic run (used by scripts on iOS).</summary>
    public bool ExitWhenDone { get; private set; }

    /// <summary>Parses <c>--runs 6 --warmup 1 --scenarios a,b --json path --label x --list --autorun --exit</c>.</summary>
    public static BenchArgs Parse(IReadOnlyList<string> args)
    {
        var options = new BenchArgs();
        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            string Next() => index + 1 < args.Count ? args[++index] : throw new ArgumentException($"{arg} needs a value");
            switch (arg)
            {
                case "--runs": options.Runs = Math.Max(1, int.Parse(Next(), CultureInfo.InvariantCulture)); break;
                case "--warmup": options.Warmup = Math.Max(0, int.Parse(Next(), CultureInfo.InvariantCulture)); break;
                case "--scenarios": options.ScenarioNames = SplitNames(Next()); break;
                case "--json": options.JsonPath = Next(); break;
                case "--label": options.Label = Next(); break;
                case "--list": options.List = true; break;
                case "--autorun": options.AutoRun = true; break;
                case "--exit": options.ExitWhenDone = true; break;
            }
        }
        return options;
    }

    /// <summary>Builds options from key/value launch extras (Android intent extras).</summary>
    public static BenchArgs FromValues(string? scenarios, int? runs, int? warmup, bool autoRun, string? label)
    {
        var options = new BenchArgs { AutoRun = autoRun };
        if (!string.IsNullOrWhiteSpace(scenarios)) options.ScenarioNames = SplitNames(scenarios);
        if (runs is > 0) options.Runs = runs.Value;
        if (warmup is >= 0) options.Warmup = warmup.Value;
        if (!string.IsNullOrWhiteSpace(label)) options.Label = label;
        return options;
    }

    private static string[] SplitNames(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Selected scenarios (all when none named; unknown names are reported and skipped).</summary>
    public IEnumerable<BenchScenario> Resolve()
    {
        if (ScenarioNames.Count == 0 || ScenarioNames.Contains("all"))
            return Scenarios.All;
        var found = new List<BenchScenario>();
        foreach (var name in ScenarioNames)
        {
            if (Scenarios.Find(name) is { } scenario) found.Add(scenario);
            else Console.WriteLine($"SKUIBENCH_WARN unknown scenario '{name}'");
        }
        return found;
    }
}

/// <summary>Median tables and JSON result files.</summary>
public static class BenchReport
{
    private static readonly (string Name, Func<BenchSample, double?> Get)[] Metrics =
    [
        ("generate", s => s.Generate),
        ("add", s => s.Add),
        ("measure", s => s.Measure),
        ("arrange", s => s.Arrange),
        ("record", s => s.Record),
        ("composite", s => s.Composite),
        ("firstFrame", s => s.FirstFrame),
        ("update", s => s.Update),
        ("motionFps", s => s.MotionFps),
        ("motionAvgRenderMs", s => s.MotionAvgRenderMs),
        ("motionMaxRenderMs", s => s.MotionMaxRenderMs),
        ("allocKB", s => s.AllocatedBytes / 1024.0),
    ];

    public static double? Median(IEnumerable<double?> values)
    {
        var sorted = values.Where(v => v.HasValue).Select(v => v!.Value).OrderBy(v => v).ToArray();
        if (sorted.Length == 0) return null;
        return sorted.Length % 2 == 1 ? sorted[sorted.Length / 2] : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2;
    }

    /// <summary>Scenario → metric → median, for every metric measured by at least one sample.</summary>
    public static Dictionary<string, Dictionary<string, double>> Medians(IEnumerable<BenchSample> samples)
    {
        var result = new Dictionary<string, Dictionary<string, double>>();
        foreach (var group in samples.GroupBy(s => s.Scenario))
        {
            var row = new Dictionary<string, double>();
            foreach (var (name, get) in Metrics)
                if (Median(group.Select(get)) is { } median)
                    row[name] = Math.Round(median, 2);
            result[group.Key] = row;
        }
        return result;
    }

    public static string FormatMedians(IEnumerable<BenchSample> samples)
    {
        var writer = new StringWriter();
        PrintMedians(samples, writer);
        return writer.ToString();
    }

    public static void PrintMedians(IEnumerable<BenchSample> samples, TextWriter output)
    {
        var list = samples.ToList();
        var medians = Medians(list);
        var columns = Metrics.Select(m => m.Name).Where(name => medians.Values.Any(row => row.ContainsKey(name))).ToArray();
        output.WriteLine($"{"scenario",-22} {"runs",4} " + string.Join(" ", columns.Select(c => $"{c,11}")));
        foreach (var (scenario, row) in medians)
        {
            var runs = list.Count(s => s.Scenario == scenario);
            output.WriteLine($"{scenario,-22} {runs,4} " + string.Join(" ", columns.Select(c =>
                row.TryGetValue(c, out var v) ? v.ToString("F1", CultureInfo.InvariantCulture).PadLeft(11) : "-".PadLeft(11))));
        }
    }

    /// <summary>Writes <c>{ runner, label, created, environment, samples }</c> for scripts/bench_compare.py.</summary>
    public static void Write(string path, string runner, string label, IEnumerable<BenchSample> samples)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var document = new
        {
            runner,
            label,
            created = DateTimeOffset.Now,
            environment = $"{System.Runtime.InteropServices.RuntimeInformation.OSDescription} / {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}",
            samples
        };
        File.WriteAllText(path, JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }));
    }
}
