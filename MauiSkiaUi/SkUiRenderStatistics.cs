namespace MauiSkiaUi;

/// <summary>
/// Render-thread compositing cost of a standalone SkiaUi surface (diagnostics / benchmarks): frames composited and
/// their average / maximum duration in milliseconds, from the start of compositing until the frame was flushed and
/// submitted / presented. Includes GPU command submission (and first-use shader compilation); excludes GPU execution
/// time and display latency.
/// </summary>
public readonly record struct SkUiRenderStatistics(long Frames, double AverageMilliseconds, double MaxMilliseconds);
