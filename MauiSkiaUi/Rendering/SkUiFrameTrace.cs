#if SKUI_DIAGNOSTICS
using System.Diagnostics;

namespace MauiSkiaUi.Rendering;

/// <summary>
/// One composited frame (diagnostics): when it started, what each phase cost, and what else happened while it ran.
/// Durations in milliseconds.
/// </summary>
/// <param name="StartMs">Start, in milliseconds since the trace was enabled.</param>
/// <param name="IntervalMs">Time since the previous frame started (a missed vsync shows here, even outside a frame).</param>
/// <param name="ApplyMs">Applying committed batches (props, pictures, children).</param>
/// <param name="AnimationsMs">Ticking render-thread animations.</param>
/// <param name="DrawMs">Replaying the retained tree onto the canvas.</param>
/// <param name="FlushMs">Skia's flush: encoding the frame's GPU commands (and compiling any new GPU pipeline).</param>
/// <param name="Batches">Committed batches applied in this frame.</param>
/// <param name="Updates">Node updates in those batches.</param>
/// <param name="Collections">Garbage collections (any generation, any thread) that ran during the frame.</param>
/// <param name="FullCollections">Of those, gen-2 (full) collections.</param>
/// <param name="ShadowRasterizations">Content shadows rasterized while drawing this frame (CPU work inside the draw phase).</param>
/// <param name="PresentMs">
/// After the flush: the surface's command buffer, present and commit (Metal). Surfaces that do not report the end of
/// their flush count all of it in <paramref name="FlushMs"/>.
/// </param>
internal readonly record struct SkUiFrameSample(
    double StartMs, double IntervalMs, double ApplyMs, double AnimationsMs, double DrawMs, double FlushMs,
    int Batches, int Updates, int Collections, int FullCollections, int ShadowRasterizations, double PresentMs)
{
    /// <summary>The whole frame, as <see cref="SkUiRenderStatistics"/> counts it.</summary>
    public double TotalMs => ApplyMs + AnimationsMs + DrawMs + FlushMs + PresentMs;
}

/// <summary>
/// Per-frame timings of a compositor while enabled (diagnostics builds; stress pages): a ring of the last
/// <see cref="Capacity"/> frames. Written on the render thread, read on any thread through <see cref="Snapshot"/>.
/// </summary>
internal sealed class SkUiFrameTrace
{
    public const int Capacity = 4096;

    private readonly object _gate = new();
    private readonly SkUiFrameSample[] _samples = new SkUiFrameSample[Capacity];
    private readonly long _origin = Stopwatch.GetTimestamp();
    private int _count;
    private int _next;
    private long _previousStart;

    // The frame being composited (render thread only).
    private long _start, _applied, _animated, _drawn, _flushed;
    private int _batches, _updates, _collectionsAtStart, _fullAtStart, _rasterizationsAtStart, _rasterizations;

    private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    private static int Collections => GC.CollectionCount(0);

    /// <summary>Render thread: a frame starts; <paramref name="rasterizations"/> is the compositor's running count.</summary>
    public void Begin(int rasterizations)
    {
        _rasterizationsAtStart = rasterizations;
        _start = Stopwatch.GetTimestamp();
        _applied = _animated = _drawn = _flushed = 0;
        _batches = _updates = 0;
        // Gen-0 counts include every collection (a gen-1 or gen-2 collection also collects gen 0).
        _collectionsAtStart = Collections;
        _fullAtStart = GC.CollectionCount(2);
    }

    /// <summary>Render thread: the frame applied <paramref name="batches"/> committed batches with <paramref name="updates"/> node updates.</summary>
    public void Applied(int batches, int updates)
    {
        _applied = Stopwatch.GetTimestamp();
        _batches = batches;
        _updates = updates;
    }

    public void Animated() => _animated = Stopwatch.GetTimestamp();

    public void Drawn(int rasterizations)
    {
        _drawn = Stopwatch.GetTimestamp();
        _rasterizations = rasterizations - _rasterizationsAtStart;
    }

    /// <summary>Render thread: the surface finished Skia's flush (optional; the rest of the frame is the present).</summary>
    public void Flushed() => _flushed = Stopwatch.GetTimestamp();

    /// <summary>Render thread: the frame was flushed and presented.</summary>
    public void End()
    {
        if (_start == 0 || _drawn == 0)
            return;
        var end = Stopwatch.GetTimestamp();
        var flushed = _flushed >= _drawn ? _flushed : end;
        var sample = new SkUiFrameSample(
            Ms(_start - _origin), _previousStart == 0 ? 0 : Ms(_start - _previousStart),
            Ms(_applied - _start), Ms(_animated - _applied), Ms(_drawn - _animated), Ms(flushed - _drawn),
            _batches, _updates, Collections - _collectionsAtStart, GC.CollectionCount(2) - _fullAtStart, _rasterizations,
            Ms(end - flushed));
        _previousStart = _start;
        _start = 0;
        lock (_gate)
        {
            _samples[_next] = sample;
            _next = (_next + 1) % Capacity;
            _count = Math.Min(_count + 1, Capacity);
        }
    }

    /// <summary>Any thread: forgets the recorded frames.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _count = 0;
            _next = 0;
        }
    }

    /// <summary>Any thread: the recorded frames, oldest first.</summary>
    public SkUiFrameSample[] Snapshot()
    {
        lock (_gate)
        {
            var result = new SkUiFrameSample[_count];
            var first = (_next - _count + Capacity) % Capacity;
            for (var index = 0; index < _count; index++)
                result[index] = _samples[(first + index) % Capacity];
            return result;
        }
    }
}
#endif
