using System.Runtime.CompilerServices;
using MauiSkiaUi.LeakTests;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Headless leak checks for the shared scenario catalog (<see cref="LeakScenarios"/>, tests/Shared/MemoryLeaks; the
/// same scenarios run on devices in tests/MauiSkiaUi.DeviceTests, see scripts/device_tests.sh). Each scenario is built, hosted on headless surfaces, exercised
/// (clicks, re-layout, scrolling, gestures, animations) and then its surfaces are disposed, like a handler
/// disconnect. Every drawn view, Core node, renderer and compositor must then be collectable; objects the scenario
/// removed while running must be collectable while the rest is still alive.
/// </summary>
// Shares the collection with OverlayScrollTests: its static capture hook would otherwise capture overlays created here.
[Collection(nameof(OverlayScrollTests))]
public class MemoryLeakTests
{
    public static TheoryData<string> Scenarios() => new(LeakScenarios.All.Select(scenario => scenario.Name));

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void ScenarioLeavesNothingAlive(string name)
    {
        var (tracked, detachedSurvivors) = Run(LeakScenarios.Find(name));
        Assert.True(detachedSurvivors.Count == 0, $"Removed during the run but still alive: {string.Join(", ", detachedSurvivors)}");
        var survivors = LeakTracker.CollectNow(tracked);
        Assert.True(survivors.Count == 0, $"Still alive after the surfaces were disposed: {string.Join(", ", survivors)}");
        Assert.True(tracked.Count > 5, $"Only {tracked.Count} objects tracked: the tree walk found nothing.");
    }

    [Fact]
    public void DetectorReportsADeliberateLeak()
    {
        try
        {
            var (tracked, _) = Run(LeakScenarios.DeliberateLeak);
            var survivors = LeakTracker.CollectNow(tracked);
            Assert.Contains($"{nameof(SkUiContentView)} '{LeakScenarios.DeliberateLeakMarker}'", survivors);
        }
        finally
        {
            LeakScenarios.ReleaseDeliberateLeak();
        }
    }

    [Fact]
    public void LongLivedCommandDropsListenersOfCollectedControls()
    {
        var command = LeakCommands.Shared;
        LeakTracker.CollectNow([]);
        command.RaiseCanExecuteChanged();
        var baseline = command.ListenerCount;

        Run(LeakScenarios.Find("ButtonsClicked"));
        Run(LeakScenarios.Find("CoreControls"));
        LeakTracker.CollectNow([]);
        // Weak listeners of collected controls unsubscribe themselves on the next raise.
        command.RaiseCanExecuteChanged();
        Assert.True(command.ListenerCount <= baseline, $"{command.ListenerCount} listeners left (baseline {baseline}).");
    }

    /// <summary>
    /// Builds, hosts and exercises <paramref name="scenario"/>, then disposes its surfaces. Not inlined, so no local of
    /// this frame keeps the UI alive for the caller's collection.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (List<TrackedObject> Tracked, IReadOnlyList<string> DetachedSurvivors) Run(LeakScenario scenario)
    {
        using var clock = new ManualGestureClock();
        var surfaces = new List<(SkUiTestSurface Surface, int Width, int Height)>();
        var time = TimeSpan.Zero;
        void Frames(TimeSpan delay)
        {
            do
            {
                var step = delay < TimeSpan.FromMilliseconds(16) ? delay : TimeSpan.FromMilliseconds(16);
                clock.Advance(step);
                time += step;
                delay -= step;
                foreach (var (surface, width, height) in surfaces)
                {
                    // The platform's per-frame work: tick UI-thread animations (ProgressTo, ...), then the layout
                    // pass (re-measure / re-arrange after invalidations; cached when clean).
                    surface.Root.AnimationClock.Tick(time);
                    SkUiTestHelpers.Arrange(surface.Root, width, height);
                    surface.Frame(time);
                }
            }
            while (delay > TimeSpan.Zero);
        }
        var context = new LeakScenarioContext(delay => { Frames(delay); return Task.CompletedTask; }, isDevice: false);
        var run = scenario.Create();
        var view = run.Build(context);
        foreach (var root in Roots(view))
        {
            var height = root.HeightRequest > 0 ? (int)root.HeightRequest : 640;
            surfaces.Add((new SkUiTestSurface(root, 360, height), 360, height));
        }
        Frames(TimeSpan.FromMilliseconds(16));

        var interaction = run.InteractAsync(context);
        // Headless waits complete synchronously; awaiting here would hop threads and lose the gesture clock.
        Assert.True(interaction.IsCompleted, "Headless interactions must not await control tasks directly (use WaitForAsync).");
        interaction.GetAwaiter().GetResult();
        Frames(TimeSpan.FromMilliseconds(32));
        if (run.CheckInteraction() is { } problem)
            Assert.Fail($"{scenario.Name}: the interaction did not take effect: {problem}");

        // Removed objects must go while the page is still alive.
        var detachedSurvivors = LeakTracker.CollectNow(context.Detached);
        var tracked = LeakTracker.TrackTree(view);
        tracked.AddRange(context.Tracked);
        foreach (var (surface, _, _) in surfaces)
        {
            tracked.Add(LeakTracker.Track(surface.Renderer, nameof(SkUiFrameRenderer)));
            tracked.Add(LeakTracker.Track(surface.Renderer.Compositor, "SkUiCompositor"));
        }
        GC.KeepAlive(view);
        GC.KeepAlive(run);
        foreach (var (surface, _, _) in surfaces)
            surface.Dispose();
        return (tracked, detachedSurvivors);
    }

    /// <summary>Surface roots of a scenario view: the view itself, or the drawn roots inside MAUI views.</summary>
    private static IEnumerable<SkUiView> Roots(View view) =>
        view is SkUiView root
            ? [root]
            : view.GetVisualTreeDescendants().OfType<SkUiView>().Where(candidate => candidate.SkiaParent is null).ToList();
}
