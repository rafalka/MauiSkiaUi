namespace MauiSkiaUi;

/// <summary>
/// Render-thread compositing cost of a standalone SkiaUi surface (diagnostics / benchmarks): frames composited and
/// their average / maximum duration in milliseconds, from the start of compositing until the frame was flushed and
/// submitted / presented. Includes GPU command submission (and first-use shader compilation); excludes GPU execution
/// time and display latency.
/// </summary>
public readonly record struct SkUiRenderStatistics(long Frames, double AverageMilliseconds, double MaxMilliseconds)
{
    /// <summary>UI-thread animation frames (UI-clock animations such as state-change transitions): ticks run.</summary>
    public long UiFrames { get; init; }

    /// <summary>Average UI-thread cost of such a frame in milliseconds: clock tick, recording and commit.</summary>
    public double UiAverageMilliseconds { get; init; }

    /// <summary>Longest UI-thread animation frame in milliseconds.</summary>
    public double UiMaxMilliseconds { get; init; }
}
