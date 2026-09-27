using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using MauiSkiaUi;
using MauiSkiaUi.Benchmarks;

namespace MauiSkiaUiBench;

/// <summary>
/// Runs scenarios on a real surface. Per iteration: <c>generate</c> (build the tree), <c>add</c> (attach it),
/// <c>firstFrame</c> (attach → first composited frame, includes layout and recording), optional <c>update</c>
/// (change → next composited frame) and motion statistics (render-thread frames / cost while animating).
/// </summary>
public sealed class BenchPage : ContentPage
{
    private readonly Grid _host = new() { BackgroundColor = Colors.White };
    private readonly Label _status = new() { FontFamily = "monospace", FontSize = 11, TextColor = Colors.Black };
    private readonly Entry _scenarios = new() { Placeholder = "scenarios (comma separated, empty = all)" };
    private readonly Entry _runs = new() { Text = "6", Keyboard = Keyboard.Numeric, WidthRequest = 60 };
    private readonly Button _run = new() { Text = "Run" };
    private bool _started;
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(100);

    public BenchPage()
    {
        BackgroundColor = Colors.White;
        _run.Clicked += (_, _) => _ = RunAsync(MauiSkiaUi.Benchmarks.BenchArgs.Parse(
            ["--scenarios", string.IsNullOrWhiteSpace(_scenarios.Text) ? "all" : _scenarios.Text, "--runs", _runs.Text ?? "6"]));
        var controls = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto)], ColumnSpacing = 6 };
        controls.Add(_scenarios, 0);
        controls.Add(_runs, 1);
        controls.Add(_run, 2);
        var layout = new Grid { Padding = new Thickness(8), RowSpacing = 6, RowDefinitions = [new(GridLength.Auto), new(new GridLength(560)), new(GridLength.Star)] };
        layout.Add(controls, 0, 0);
        layout.Add(_host, 0, 1);
        layout.Add(new ScrollView { Content = _status }, 0, 2);
        Content = layout;
        Loaded += (_, _) =>
        {
            if (_started || !BenchLaunch.Options.AutoRun) return;
            _started = true;
            _ = RunAsync(BenchLaunch.Options);
        };
    }

    private async Task RunAsync(MauiSkiaUi.Benchmarks.BenchArgs options)
    {
        _run.IsEnabled = false;
        var samples = new List<BenchSample>();
        try
        {
            Console.WriteLine($"SKUIBENCH_START label={options.Label} runs={options.Runs} warmup={options.Warmup}");
            if (GetStats is null)
                Console.WriteLine("SKUIBENCH_WARN library has no render statistics: firstFrame/update are approximated (layout + dispatcher turns), motion is not measured");
            foreach (var scenario in options.Resolve())
            {
                for (var iteration = 0; iteration < options.Warmup + options.Runs; iteration++)
                {
                    _status.Text = $"{scenario.Name} {iteration + 1}/{options.Warmup + options.Runs}…";
                    var sample = await RunOnceAsync(scenario, iteration - options.Warmup);
                    if (iteration < options.Warmup) continue;
                    samples.Add(sample);
                    Console.WriteLine("SKUIBENCH " + JsonSerializer.Serialize(sample));
                }
            }
            var table = BenchReport.FormatMedians(samples);
            foreach (var line in table.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                Console.WriteLine("SKUIBENCH_TABLE " + line);
            _status.Text = table;
        }
        catch (Exception exception)
        {
            Console.WriteLine("SKUIBENCH_ERROR " + exception);
            _status.Text = exception.ToString();
        }
        finally
        {
            _host.Clear();
            Console.WriteLine("SKUIBENCH_DONE");
            _run.IsEnabled = true;
            if (options.ExitWhenDone)
                Environment.Exit(0);
        }
    }

    private async Task<BenchSample> RunOnceAsync(BenchScenario scenario, int iteration)
    {
        _host.Clear();
        await Turns(3);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await Turns(3);

        var watch = Stopwatch.StartNew();
        var root = scenario.Build();
        var generate = watch.Elapsed.TotalMilliseconds;

        watch.Restart();
        _host.Add(root);
        var add = watch.Elapsed.TotalMilliseconds;

        watch.Restart();
        ForceLayout();
        await WaitForFramesAsync(root, minimumFrames: 1);
        var firstFrame = watch.Elapsed.TotalMilliseconds;

        double? update = null;
        if (scenario.Update is { } apply)
        {
            // Idle first: a change right after the previous frame waits for the next vsync (correct pacing), which
            // would measure frame phase instead of the update.
            await Task.Delay(Settle);
            var before = Stats(root)?.Frames ?? 0;
            watch.Restart();
            apply(root);
            ForceLayout();
            await WaitForFramesAsync(root, before + 1);
            update = watch.Elapsed.TotalMilliseconds;
        }

        double? fps = null, avg = null, max = null;
        if (scenario.Motion is { } motion && root is SkUiView)
        {
            await Task.Delay(Settle);
            Reset(root);
            watch.Restart();
            var handle = motion(root);
            await Task.Delay(scenario.MotionDuration);
            var stats = Stats(root);
            var seconds = watch.Elapsed.TotalSeconds;
            handle?.Dispose();
            if (stats is { } s)
            {
                fps = s.Frames / seconds;
                avg = s.AverageMilliseconds;
                max = s.MaxMilliseconds;
            }
        }

        return new BenchSample(scenario.Name, DeviceInfo.Current.Platform.ToString(), iteration, generate, add, null, null, null, null,
            firstFrame, update, fps, avg, max, null);
    }

    /// <summary>
    /// Waits until the surface has composited <paramref name="minimumFrames"/> frames with content. Native MAUI trees
    /// (and libraries without render statistics) fall back to "laid out + a few dispatcher turns".
    /// </summary>
    private async Task WaitForFramesAsync(View root, long minimumFrames)
    {
        var deadline = Stopwatch.StartNew();
        if (Stats(root) is null)
        {
            while (root.Width <= 0 && deadline.Elapsed < TimeSpan.FromSeconds(10))
                await Turns(1);
            await Turns(3);
            return;
        }
        while (deadline.Elapsed < TimeSpan.FromSeconds(10))
        {
            if (root.Width > 0 && Stats(root)?.Frames >= minimumFrames)
                return;
            await Task.Delay(1);
        }
        Console.WriteLine("SKUIBENCH_WARN frame wait timed out");
    }

    /// <summary>
    /// UIKit runs layout in its display-aligned update cycle, so attach / change → measure otherwise waits 0–16 ms
    /// depending on where the benchmark's continuation falls in that cycle: noise unrelated to the code under test.
    /// </summary>
    private void ForceLayout()
    {
#if IOS || MACCATALYST
        (_host.Handler?.PlatformView as UIKit.UIView)?.Window?.LayoutIfNeeded();
#endif
    }

    private static readonly MethodInfo? GetStats = typeof(SkUiView).GetMethod("GetRenderStatistics", BindingFlags.Instance | BindingFlags.Public);
    private static readonly MethodInfo? ResetStats = typeof(SkUiView).GetMethod("ResetRenderStatistics", BindingFlags.Instance | BindingFlags.Public);

    /// <summary>Render statistics through reflection, so the app also builds against libraries that predate the API.</summary>
    private static (long Frames, double AverageMilliseconds, double MaxMilliseconds)? Stats(View root)
    {
        if (root is not SkUiView view || GetStats?.Invoke(view, null) is not { } value)
            return null;
        var type = value.GetType();
        return ((long)type.GetProperty("Frames")!.GetValue(value)!,
            (double)type.GetProperty("AverageMilliseconds")!.GetValue(value)!,
            (double)type.GetProperty("MaxMilliseconds")!.GetValue(value)!);
    }

    private static void Reset(View root)
    {
        if (root is SkUiView view)
            ResetStats?.Invoke(view, null);
    }

    private Task Turns(int count)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Step(int remaining)
        {
            if (remaining <= 0) completion.TrySetResult();
            else Dispatcher.Dispatch(() => Step(remaining - 1));
        }
        Step(count);
        return completion.Task;
    }
}
