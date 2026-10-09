using System.Reflection;

namespace MauiSkiaUi.Benchmarks;

/// <summary>
/// One benchmark scenario, shared by the headless runner (<c>MauiSkiaUi.Benchmarks</c>) and the on-device bench app
/// (<c>MauiSkiaUiBench</c>). Scenarios use only long-stable public API so they also build against older library
/// commits (baseline comparisons); newer options are applied through <see cref="TrySet"/>.
/// </summary>
public abstract class BenchScenario
{
    /// <summary>Stable id used on the command line (e.g. <c>core-labels</c>).</summary>
    public abstract string Name { get; }

    /// <summary>One-line description.</summary>
    public abstract string Description { get; }

    /// <summary>Only meaningful on a device (needs a real surface, motion or a native control).</summary>
    public virtual bool DeviceOnly => false;

    /// <summary>Builds the tree to measure (timed as <c>generate</c>). Must return a new tree each call.</summary>
    public abstract View Build();

    /// <summary>
    /// Optional steady-state change applied after the first frame (timed as <c>update</c>, including the re-layout /
    /// re-record it causes), e.g. changing every label's text.
    /// </summary>
    public virtual Action<View>? Update => null;

    /// <summary>
    /// Optional continuous motion started after the first frame (device only). The runner samples render-thread
    /// statistics for <see cref="MotionDuration"/> and then disposes the handle.
    /// </summary>
    public virtual Func<View, IDisposable?>? Motion => null;

    /// <summary>How long <see cref="Motion"/> is sampled.</summary>
    public virtual TimeSpan MotionDuration => TimeSpan.FromSeconds(1.5);

    /// <summary>Sets a property if the library under test has it (newer APIs in baseline comparisons).</summary>
    protected static void TrySet(object target, string property, object value) => TrySetOption(target, property, value);

    /// <summary>Sets <paramref name="property"/> when the library has it (a no-op on older libraries), for scenarios and their helpers.</summary>
    public static void TrySetOption(object target, string property, object value)
    {
        var info = target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public);
        if (info is null || !info.CanWrite)
            return;
        var converted = value is string name && info.PropertyType.IsEnum ? Enum.Parse(info.PropertyType, name) : value;
        info.SetValue(target, converted);
    }
}

/// <summary>
/// Timings of one scenario iteration in milliseconds (null = not measured by this runner / scenario).
/// <c>Record</c> = UI-thread recording of the first frame; <c>Composite</c> = compositing it (headless, offscreen);
/// <c>FirstFrame</c> = device time from attach to the first composited frame (includes layout).
/// </summary>
public sealed record BenchSample(
    string Scenario,
    string Runner,
    int Iteration,
    double Generate,
    double? Add,
    double? Measure,
    double? Arrange,
    double? Record,
    double? Composite,
    double? FirstFrame,
    double? Update,
    double? MotionFps,
    double? MotionAvgRenderMs,
    double? MotionMaxRenderMs,
    long? AllocatedBytes,
    double? MotionUiFps = null,
    double? MotionAvgUiMs = null,
    double? MotionMaxUiMs = null);
