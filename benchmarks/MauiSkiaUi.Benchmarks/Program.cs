using System.Diagnostics;
using System.Text.Json;
using MauiSkiaUi.Benchmarks;
using SkiaSharp;

// Tier-1 headless benchmark runner. Output: a median table on stdout, one "SKUIBENCH {json}" line per sample,
// and (with --json) a results file consumed by scripts/bench_compare.py.
var options = BenchArgs.Parse(args);
if (options.List)
{
    foreach (var scenario in Scenarios.All)
        Console.WriteLine($"{scenario.Name,-22} {(scenario.DeviceOnly ? "[device] " : "")}{scenario.Description}");
    return 0;
}

var selected = options.Resolve().Where(scenario => !scenario.DeviceOnly).ToList();
var samples = new List<BenchSample>();
foreach (var scenario in selected)
{
    for (var iteration = 0; iteration < options.Warmup + options.Runs; iteration++)
    {
        var sample = Run(scenario, iteration - options.Warmup);
        if (iteration >= options.Warmup)
        {
            samples.Add(sample);
            Console.WriteLine("SKUIBENCH " + JsonSerializer.Serialize(sample));
        }
    }
}

BenchReport.PrintMedians(samples, Console.Out);
if (options.JsonPath is { } path)
    BenchReport.Write(path, "headless", options.Label, samples);
return 0;

static BenchSample Run(BenchScenario scenario, int iteration)
{
    const double width = 400, height = 800;
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    var allocated = GC.GetAllocatedBytesForCurrentThread();
    var watch = Stopwatch.StartNew();
    var root = scenario.Build();
    var generate = watch.Elapsed.TotalMilliseconds;

    IView view = root;
    watch.Restart();
    view.Measure(width, height);
    var measure = watch.Elapsed.TotalMilliseconds;
    watch.Restart();
    view.Arrange(new Rect(0, 0, width, height));
    var arrange = watch.Elapsed.TotalMilliseconds;

    // Real frame pipeline when the library has it (record every dirty node on the "UI" thread, then composite into
    // an offscreen surface); older baselines fall back to an immediate paint (record/composite then report null).
    using var pipeline = FramePipeline.TryCreate(root, width, height);
    double paint;
    double? composite = null;
    watch.Restart();
    if (pipeline is not null)
    {
        pipeline.Record();
        paint = watch.Elapsed.TotalMilliseconds;
        watch.Restart();
        pipeline.Composite();
        composite = watch.Elapsed.TotalMilliseconds;
    }
    else
    {
        Paint(root, width, height);
        paint = watch.Elapsed.TotalMilliseconds;
    }

    double? update = null;
    if (scenario.Update is { } apply)
    {
        watch.Restart();
        apply(root);
        view.Measure(width, height);
        view.Arrange(new Rect(0, 0, width, height));
        if (pipeline is not null)
        {
            pipeline.Record();
            pipeline.Composite();
        }
        else
        {
            Paint(root, width, height);
        }
        update = watch.Elapsed.TotalMilliseconds;
    }

    return new BenchSample(scenario.Name, "headless", iteration, generate, null, measure, arrange, paint, composite, null, update, null, null, null,
        GC.GetAllocatedBytesForCurrentThread() - allocated);
}

static void Paint(View root, double width, double height)
{
    if (root is not MauiSkiaUi.ISkUiView drawn)
        return;
    using var recorder = new SKPictureRecorder();
    var canvas = recorder.BeginRecording(new SKRect(0, 0, (float)width, (float)height));
    drawn.Paint(canvas);
    using var picture = recorder.EndRecording();
}

/// <summary>
/// Drives the library's internal UI-thread recorder and compositor through reflection, so the benchmark measures the
/// real retained pipeline and still runs against library commits that predate (or rename) it.
/// </summary>
sealed class FramePipeline : IDisposable
{
    private readonly object _renderer;
    private readonly System.Reflection.MethodInfo _present;
    private readonly System.Reflection.MethodInfo _render;
    private readonly SKBitmap _bitmap;
    private readonly SKCanvas _canvas;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private FramePipeline(object renderer, System.Reflection.MethodInfo present, System.Reflection.MethodInfo render, int width, int height)
    {
        _renderer = renderer;
        _present = present;
        _render = render;
        _bitmap = new SKBitmap(width, height);
        _canvas = new SKCanvas(_bitmap);
    }

    public static FramePipeline? TryCreate(View root, double width, double height)
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
        var type = typeof(MauiSkiaUi.SkUiView).Assembly.GetType("MauiSkiaUi.SkUiFrameRenderer");
        if (type is null || root is not MauiSkiaUi.SkUiView)
            return null;
        var ctor = type.GetConstructor(flags, [typeof(MauiSkiaUi.SkUiView), typeof(Action<Action>), typeof(Action), typeof(Action)]);
        var present = type.GetMethod("PresentFrame", flags, Type.EmptyTypes);
        var render = type.GetMethod("Render", flags, [typeof(SKCanvas), typeof(SKImageInfo), typeof(TimeSpan?)]);
        if (ctor is null || present is null || render is null)
            return null;
        var renderer = ctor.Invoke([root, new Action<Action>(_ => { }), new Action(() => { }), new Action(() => { })]);
        return new FramePipeline(renderer, present, render, (int)width, (int)height);
    }

    /// <summary>UI-thread half: record dirty nodes and commit.</summary>
    public void Record() => _present.Invoke(_renderer, null);

    /// <summary>Render-thread half: apply the commit and composite the retained tree.</summary>
    public void Composite() => _render.Invoke(_renderer, [_canvas, _bitmap.Info, (TimeSpan?)_clock.Elapsed]);

    public void Dispose()
    {
        (_renderer as IDisposable)?.Dispose();
        _canvas.Dispose();
        _bitmap.Dispose();
    }
}
