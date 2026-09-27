namespace MauiSkiaUi;

/// <summary>
/// Render-thread compositing cost of a standalone SkiaUi surface (diagnostics / benchmarks): frames composited and
/// their average / maximum duration in milliseconds. Excludes GPU execution and presentation.
/// </summary>
public readonly record struct SkUiRenderStatistics(long Frames, double AverageMilliseconds, double MaxMilliseconds);
