using System.Diagnostics;
using System.Text;
using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// Effects stress test: a long list of cards (<see cref="EffectsStressScene"/>) with gradient backgrounds, rounded or
/// shaped borders, card and text shadows, clips and translucency switched on one by one, measuring what each costs to
/// build, to show first and to scroll. "Run matrix" measures the baseline, every effect alone and all of them together.
/// </summary>
public sealed class EffectsStressPage : ContentPage
{
    private const int DefaultCardCount = 400;
    private const int MaxCardCount = 20_000;
    private static readonly TimeSpan ScrollDuration = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan MatrixScrollDuration = TimeSpan.FromSeconds(4);
    private static readonly StressEffects[] Features =
        [StressEffects.Gradient, StressEffects.RoundedCorners, StressEffects.ShapedBorder, StressEffects.CardShadow,
         StressEffects.TextShadow, StressEffects.Clip, StressEffects.Translucent, StressEffects.Spinner];
    private static readonly TimeSpan IdleDuration = TimeSpan.FromSeconds(1);

    private readonly Entry _countEntry;
    private readonly CheckBox _hwAcceleration;
    private readonly Picker _layerPicker;
    private readonly Picker _scrollPicker;
    private readonly Dictionary<StressEffects, CheckBox> _featureBoxes = [];
    private readonly ToolbarItem[] _actions;
    private readonly StressPageChrome _chrome;
    private readonly ContentView _host;
    private SkUiScrollView? _skUiScroller;
    private ScrollView? _nativeScroller;
    private BuildResult? _lastBuild;
    private bool _busy;

    /// <summary>What a build cost.</summary>
    private sealed record BuildResult(EffectsStressLayer Layer, StressEffects Effects, int Count, bool Hw, double GenerateMs, double FirstFrameMs, int RecordedPictures);

    /// <summary>
    /// What a scroll cost, after an idle second with nothing scrolling (animated content such as spinners keeps the
    /// surface drawing then too: <see cref="IdleFps"/> is 0 without it).
    /// </summary>
    private sealed record ScrollResult(double WallMs, long RenderFrames, double RenderAverageMs, double RenderMaxMs,
        long UiFrames, double UiAverageMs, double UiMaxMs, int RecordFrames, double RecordAverageMs,
        int RecordedPictures, int ShadowRasterizations, int LiveShadows)
    {
        public double Fps => WallMs > 0 ? RenderFrames * 1000 / WallMs : 0;

        /// <summary>Frames composited per second while idle.</summary>
        public double IdleFps { get; init; }

        /// <summary>Compositor time per idle frame (ms).</summary>
        public double IdleRenderAverageMs { get; init; }

        /// <summary>Per-frame timings while scrolling (diagnostics builds; empty otherwise).</summary>
        public FrameSample[] Frames { get; init; } = [];
    }

    /// <summary>One scroll frame (copied from the library's diagnostics trace): start and phases in ms.</summary>
    private readonly record struct FrameSample(double StartMs, double IntervalMs, double ApplyMs, double AnimationsMs, double DrawMs,
        double FlushMs, int Batches, int Updates, int Collections, int FullCollections, int ShadowRasterizations, double PresentMs)
    {
        public double TotalMs => ApplyMs + AnimationsMs + DrawMs + FlushMs + PresentMs;
    }

    /// <summary>Builds the configuration UI; the scene is created only when a test runs.</summary>
    public EffectsStressPage()
    {
        Title = "Effects stress";
        Background = DemoColors.PageBackground;

        _countEntry = new Entry
        {
            Text = DefaultCardCount.ToString(), Keyboard = Keyboard.Numeric, WidthRequest = 88, AutomationId = "EffectsStressCount",
            FontFamily = DemoFonts.OpenSansRegular, TextColor = DemoColors.Ink, VerticalOptions = LayoutOptions.Center
        };
        _hwAcceleration = new CheckBox { IsChecked = true, Color = DemoColors.Accent, AutomationId = "EffectsStressHw", VerticalOptions = LayoutOptions.Center };
        _layerPicker = new Picker
        {
            Title = "Layer", ItemsSource = new[] { "SkUi* (MAUI-compatible)", "Core", "Native MAUI (reference)" }, SelectedIndex = 0,
            AutomationId = "EffectsStressLayer", FontFamily = DemoFonts.OpenSansRegular, TextColor = DemoColors.Ink
        };
        _scrollPicker = new Picker
        {
            Title = "Scroll", ItemsSource = new[] { "Render thread (animated scroll)", "UI thread (ScrollTo every frame)" }, SelectedIndex = 0,
            AutomationId = "EffectsStressScrollMode", FontFamily = DemoFonts.OpenSansRegular, TextColor = DemoColors.Ink
        };

        var features = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)], RowSpacing = 0, ColumnSpacing = 8 };
        for (var index = 0; index < Features.Length; index++)
        {
            var feature = Features[index];
            var box = new CheckBox { Color = DemoColors.Accent, AutomationId = "EffectsStress" + feature, VerticalOptions = LayoutOptions.Center };
            _featureBoxes[feature] = box;
            features.AddRowDefinition(new RowDefinition(GridLength.Auto));
            features.Add(new HorizontalStackLayout { Spacing = 4, Children = { box, Caption(FeatureName(feature), 13, DemoColors.Ink) } }, index % 2, index / 2);
        }

        var about = Caption("A scrolling list of cards (avatar, title, subtitle). Tick effects in Config, Run (build and first " +
            "frame), then Scroll from the toolbar; or Matrix for the baseline, each effect alone and all together (after an " +
            "unreported warm-up). Render: compositor time per frame on the render thread (UI thread for software surfaces). " +
            "FPS: frames composited per second while scrolling (capped by the display). Record: UI-thread pictures recorded " +
            "while scrolling (0 = composite-time). Raster / live: content shadows rasterized once / drawn live. Idle: frames " +
            "per second during one second with nothing scrolling, measured before each scroll; 0 unless something animates " +
            "(the spinner effect puts a running activity indicator on every card).",
            13, DemoColors.Ink);
        var configuration = new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new HorizontalStackLayout
                {
                    Spacing = 10,
                    Children = { Caption("Cards", 13, DemoColors.Ink), _countEntry, _hwAcceleration, Caption("HW accel", 13, DemoColors.Ink) }
                },
                _layerPicker,
                _scrollPicker,
                features
            }
        };

        _host = new ContentView { AutomationId = "EffectsStressHost" };
        _chrome = new StressPageChrome(this, "Effects stress", about, configuration, StatusChips, () => _ = RunExclusiveAsync(BuildSelectedAsync), "EffectsStress");
        _countEntry.TextChanged += (_, _) => _chrome.RefreshChips();
        _hwAcceleration.CheckedChanged += (_, _) => _chrome.RefreshChips();
        _layerPicker.SelectedIndexChanged += (_, _) => _chrome.RefreshChips();
        _scrollPicker.SelectedIndexChanged += (_, _) => _chrome.RefreshChips();
        foreach (var box in _featureBoxes.Values)
            box.CheckedChanged += (_, _) => _chrome.RefreshChips();

        _actions =
        [
            new ToolbarItem("Scroll", null, () => _ = RunExclusiveAsync(ScrollSelectedAsync)) { AutomationId = "EffectsStressScroll" },
            new ToolbarItem("Matrix", null, () => _ = RunExclusiveAsync(RunMatrixAsync)) { AutomationId = "EffectsStressMatrix" }
        ];
        foreach (var action in _actions)
            ToolbarItems.Add(action);
        ToolbarItems.Add(new ToolbarItem("Top", null, () =>
        {
            _skUiScroller?.ScrollTo(0, 0);
            _ = _nativeScroller?.ScrollToAsync(0, 0, false);
        }) { AutomationId = "EffectsStressTop" });

        var layout = new Grid
        {
            Padding = new Thickness(12, 8),
            RowSpacing = 8,
            RowDefinitions = [new(GridLength.Auto), new(GridLength.Star)]
        };
        layout.Add(_chrome.Header);
        layout.Add(_host, 0, 1);
        Content = layout;
    }

    /// <summary>Status chips: layer, HW / SW surface, scroll mode, card count and the ticked effects.</summary>
    private IEnumerable<string> StatusChips()
    {
        yield return SelectedLayer switch
        {
            EffectsStressLayer.Core => "CORE",
            EffectsStressLayer.NativeMaui => "NATIVE",
            _ => "SKUI"
        };
        if (SelectedLayer != EffectsStressLayer.NativeMaui)
        {
            yield return _hwAcceleration.IsChecked ? "HW" : "SW";
            yield return UiThreadScroll ? "UI SCROLL" : "RT SCROLL";
        }
        yield return $"{ParseCount(_countEntry.Text):N0} cards"; // no normalizing while the user types
        var effects = SelectedEffects;
        if (effects == StressEffects.None)
            yield return "NO EFFECTS";
        foreach (var feature in Features)
            if (effects.HasFlag(feature))
                yield return feature switch
                {
                    StressEffects.Gradient => "GRAD",
                    StressEffects.RoundedCorners => "ROUND",
                    StressEffects.ShapedBorder => "SHAPE",
                    StressEffects.CardShadow => "SHADOW",
                    StressEffects.TextShadow => "TEXT SHADOW",
                    StressEffects.Clip => "CLIP",
                    StressEffects.Translucent => "ALPHA",
                    _ => "SPIN"
                };
    }

    private EffectsStressLayer SelectedLayer => _layerPicker.SelectedIndex switch
    {
        1 => EffectsStressLayer.Core,
        2 => EffectsStressLayer.NativeMaui,
        _ => EffectsStressLayer.SkUi
    };

    private bool UiThreadScroll => _scrollPicker.SelectedIndex == 1;

    private StressEffects SelectedEffects =>
        _featureBoxes.Where(pair => pair.Value.IsChecked).Aggregate(StressEffects.None, (effects, pair) => effects | pair.Key);

    /// <summary>The card count to run with; normalizes the entry's text.</summary>
    private int CardCount
    {
        get
        {
            var count = ParseCount(_countEntry.Text);
            _countEntry.Text = count.ToString();
            return count;
        }
    }

    private static int ParseCount(string? text) =>
        int.TryParse(text, out var parsed) && parsed > 0 ? Math.Min(parsed, MaxCardCount) : DefaultCardCount;

    // ---- Runs -------------------------------------------------------------------------------------------------

    private async Task RunExclusiveAsync(Func<Task> run)
    {
        if (_busy) return;
        _busy = true;
        SetActionsEnabled(false);
        try
        {
            await run().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _chrome.Report("Test failed", $"Test failed: {ex.Message}");
        }
        finally
        {
            _busy = false;
            SetActionsEnabled(true);
        }
    }

    private void SetActionsEnabled(bool enabled)
    {
        _chrome.RunEnabled = enabled;
        foreach (var action in _actions)
            action.IsEnabled = enabled;
    }

    private async Task BuildSelectedAsync()
    {
        var result = await BuildAsync(SelectedLayer, CardCount, SelectedEffects, _hwAcceleration.IsChecked).ConfigureAwait(true);
        _chrome.Report(BuildSummary(result), FormatBuild(result));
    }

    private async Task ScrollSelectedAsync()
    {
        if (_lastBuild is null)
            await BuildSelectedAsync().ConfigureAwait(true);
        var scroll = await ScrollAsync(ScrollDuration).ConfigureAwait(true);
        _chrome.Report(ScrollSummary(scroll), $"{FormatBuild(_lastBuild!)}\n{FormatScroll(scroll)}");
    }

    /// <summary>Baseline, each feature alone, then all of them: build and scroll each, then a table with the costs over the baseline.</summary>
    private async Task RunMatrixAsync()
    {
        var layer = SelectedLayer;
        var count = CardCount;
        var hw = _hwAcceleration.IsChecked;
        StressEffects[] configurations = [StressEffects.None, .. Features, StressEffects.All];
        var rows = new List<(StressEffects Effects, BuildResult Build, ScrollResult? Scroll)>();
        // Warm-up, not reported: JIT, font and shader caches would otherwise be charged to the baseline.
        _chrome.SetStatus("Matrix: warming up…");
        await BuildAsync(layer, count, StressEffects.All, hw).ConfigureAwait(true);
        await ScrollAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(true);
        for (var index = 0; index < configurations.Length; index++)
        {
            var effects = configurations[index];
            _chrome.SetStatus($"Matrix {index + 1}/{configurations.Length}: {EffectsStressScene.Describe(effects)}…");
            var build = await BuildAsync(layer, count, effects, hw).ConfigureAwait(true);
            await Task.Delay(300).ConfigureAwait(true); // let the first frames settle (and shadows rasterize) before scrolling
            var scroll = await ScrollAsync(MatrixScrollDuration).ConfigureAwait(true);
            rows.Add((effects, build, scroll));
        }
        _chrome.Report(MatrixSummary(rows), FormatMatrix(layer, count, hw, rows));
    }

    private async Task<BuildResult> BuildAsync(EffectsStressLayer layer, int count, StressEffects effects, bool hw)
    {
        await ReleaseSceneAsync().ConfigureAwait(true);
        var generate = Stopwatch.StartNew();
        var scene = EffectsStressScene.Build(layer, count, effects, hw);
        generate.Stop();

        var firstFrame = Stopwatch.StartNew();
        _skUiScroller = scene as SkUiScrollView;
        _nativeScroller = scene as ScrollView;
        _host.Content = scene;
        await WaitForLaidOutAsync(scene).ConfigureAwait(true);
        await FlushUiAsync().ConfigureAwait(true);
        await WhenIdleAsync().ConfigureAwait(true);
        firstFrame.Stop();

        var recorded = _skUiScroller?.GetRenderCounters().RecordedPictures ?? 0;
        return _lastBuild = new BuildResult(layer, effects, count, hw, generate.Elapsed.TotalMilliseconds, firstFrame.Elapsed.TotalMilliseconds, recorded);
    }

    private async Task<ScrollResult?> ScrollAsync(TimeSpan duration)
    {
        if (_skUiScroller is { } scroller)
            return await ScrollDrawnAsync(scroller, duration).ConfigureAwait(true);
        if (_nativeScroller is { } native)
        {
            await native.ScrollToAsync(0, 0, false).ConfigureAwait(true);
            await WhenIdleAsync().ConfigureAwait(true);
            var target = Math.Max(0, (native.Content?.Height ?? 0) - native.Height);
            var wall = Stopwatch.StartNew();
            await native.ScrollToAsync(0, target, true).ConfigureAwait(true);
            wall.Stop();
            return new ScrollResult(wall.Elapsed.TotalMilliseconds, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }
        return null;
    }

    private async Task<ScrollResult> ScrollDrawnAsync(SkUiScrollView scroller, TimeSpan duration)
    {
        scroller.ScrollTo(0, 0);
        await FlushUiAsync().ConfigureAwait(true);
        await WhenIdleAsync().ConfigureAwait(true);
        var target = Math.Max(0, scroller.ContentSize.Height - scroller.Height);

        // Idle: what animated content (spinners) costs with nothing scrolling; static content draws no frames.
        scroller.ResetRenderStatistics();
        var idleWall = Stopwatch.StartNew();
        await Task.Delay(IdleDuration).ConfigureAwait(true);
        idleWall.Stop();
        var idle = scroller.GetRenderStatistics();

        var before = scroller.GetRenderCounters();
        scroller.ResetRenderStatistics();
#if SKUI_DIAGNOSTICS
        scroller.ResetDiagnosticRecordStats();
#endif
#if SKUI_DIAGNOSTICS
        var trace = scroller.StartFrameTrace();
#endif
        var wall = Stopwatch.StartNew();
        // Render thread: the compositor moves the content, nothing is recorded. UI thread: every frame sets the
        // offset, as a drag does, so each frame also commits from the UI thread.
        using (UiThreadScroll
            ? scroller.AnimationClock.Start(progress => scroller.ScrollTo(0, target * progress), duration)
            : scroller.AnimateScrollTo(0, target, duration))
        {
            await Task.Delay(duration).ConfigureAwait(true);
        }
        wall.Stop();
        await WhenIdleAsync().ConfigureAwait(true);
        FrameSample[] frames = [];
#if SKUI_DIAGNOSTICS
        scroller.StopFrameTrace();
        if (trace is not null)
            frames = [.. trace.Snapshot().Select(f => new FrameSample(f.StartMs, f.IntervalMs, f.ApplyMs, f.AnimationsMs, f.DrawMs,
                f.FlushMs, f.Batches, f.Updates, f.Collections, f.FullCollections, f.ShadowRasterizations, f.PresentMs))];
#endif
        var statistics = scroller.GetRenderStatistics();
        var after = scroller.GetRenderCounters();
        var recordFrames = 0;
        var recordAverage = 0.0;
#if SKUI_DIAGNOSTICS
        recordFrames = scroller.DiagnosticRecordFrameCount;
        recordAverage = recordFrames == 0 ? 0 : scroller.DiagnosticRecordFrameTotalMs / recordFrames;
#endif
        return new ScrollResult(wall.Elapsed.TotalMilliseconds, statistics.Frames, statistics.AverageMilliseconds, statistics.MaxMilliseconds,
            statistics.UiFrames, statistics.UiAverageMilliseconds, statistics.UiMaxMilliseconds, recordFrames, recordAverage,
            after.RecordedPictures - before.RecordedPictures, after.ShadowRasterizations - before.ShadowRasterizations,
            after.LiveShadows - before.LiveShadows)
        {
            IdleFps = idle.Frames * 1000 / idleWall.Elapsed.TotalMilliseconds,
            IdleRenderAverageMs = idle.AverageMilliseconds,
            Frames = frames
        };
    }

    /// <summary>Disconnects and drops the previous scene, then collects, so the next build starts from a clean heap.</summary>
    private async Task ReleaseSceneAsync()
    {
        var previous = _host.Content;
        _skUiScroller = null;
        _nativeScroller = null;
        _lastBuild = null;
        if (previous is null)
            return;
        // Disconnect while the subtree is intact so every platform view is released on the UI thread.
        previous.DisconnectHandlers();
        _host.Content = null;
        previous = null;
        await FlushUiAsync().ConfigureAwait(true);
        await WhenIdleAsync().ConfigureAwait(true);
        if (OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst())
            await Task.Delay(100).ConfigureAwait(true); // CALayer teardown; no WaitForPendingFinalizers on Apple (see StressPage)
        GC.Collect();
        if (!OperatingSystem.IsIOS() && !OperatingSystem.IsMacCatalyst())
        {
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        await FlushUiAsync().ConfigureAwait(true);
    }

    // ---- Formatting -------------------------------------------------------------------------------------------

    private static string FormatBuild(BuildResult build) =>
        $"{LayerName(build.Layer)} · {build.Count:N0} cards · HW {(build.Layer == EffectsStressLayer.NativeMaui ? "n/a" : build.Hw ? "on" : "off")} · {EffectsStressScene.Describe(build.Effects)}\n" +
        $"Generate {build.GenerateMs:F1} ms · first frame {build.FirstFrameMs:F1} ms" +
        (build.Layer == EffectsStressLayer.NativeMaui ? "" : $" · {build.RecordedPictures:N0} pictures recorded");

    private string FormatScroll(ScrollResult? scroll)
    {
        if (scroll is null) return "Nothing to scroll.";
        if (_lastBuild?.Layer == EffectsStressLayer.NativeMaui)
            return $"Scroll (MAUI animated ScrollToAsync): {scroll.WallMs:F0} ms wall; frame statistics n/a";
        var mode = UiThreadScroll ? "UI thread" : "render thread";
        return $"Idle ({IdleDuration.TotalSeconds:0} s, nothing scrolling): {scroll.IdleFps:F1} fps, {scroll.IdleRenderAverageMs:F2} ms avg render\n" +
            $"Scroll ({mode}, {scroll.WallMs / 1000:F1} s): {scroll.RenderFrames} frames, {scroll.Fps:F1} fps\n" +
            $"Render {scroll.RenderAverageMs:F2} ms avg / {scroll.RenderMaxMs:F2} ms max per frame\n" +
            (UiThreadScroll ? $"UI frames {scroll.UiFrames}: {scroll.UiAverageMs:F2} ms avg / {scroll.UiMaxMs:F2} ms max\n" : "") +
            $"Record during scroll: {scroll.RecordedPictures} pictures ({scroll.RecordFrames} frames, {scroll.RecordAverageMs:F2} ms avg)\n" +
            $"Content shadows: {scroll.ShadowRasterizations} rasterized, {scroll.LiveShadows} drawn live" +
            FormatSlowFrames(scroll.Frames, 5);
    }

    /// <summary>
    /// The slowest scroll frames with their phases (diagnostics builds): where a hitch comes from (compositing, flush /
    /// present, a garbage collection) and when in the scroll it happened; and gaps between frames (missed vsyncs).
    /// </summary>
    private static string FormatSlowFrames(FrameSample[] frames, int count)
    {
        if (frames.Length < 3) return "";
        var intervals = frames.Skip(1).Select(frame => frame.IntervalMs).Order().ToArray();
        var typical = intervals[intervals.Length / 2];
        var gaps = intervals.Count(interval => interval > typical * 1.5);
        var text = new StringBuilder();
        text.Append($"\nFrames: {frames.Length}, typical interval {typical:F1} ms, {gaps} gaps over {typical * 1.5:F0} ms, longest gap {intervals[^1]:F1} ms");
        text.Append($"\nGCs during frames: {frames.Sum(frame => frame.Collections)} ({frames.Sum(frame => frame.FullCollections)} full)");
        text.Append("\nSlowest frames (at ms into the scroll: total = apply + animations + draw + flush + present):");
        foreach (var frame in frames.OrderByDescending(frame => frame.TotalMs).Take(count))
            text.Append($"\n  @{frame.StartMs,6:F0}: {frame.TotalMs,6:F2} = {frame.ApplyMs:F2} + {frame.AnimationsMs:F2} + {frame.DrawMs:F2} + {frame.FlushMs:F2} + {frame.PresentMs:F2}" +
                $"  batches {frame.Batches} ({frame.Updates} updates)" + (frame.ShadowRasterizations > 0 ? $"  {frame.ShadowRasterizations} shadows rasterized" : "") + (frame.Collections > 0 ? $"  GC {frame.Collections}{(frame.FullCollections > 0 ? " full" : "")}" : ""));
        return text.ToString();
    }

    private string FormatMatrix(EffectsStressLayer layer, int count, bool hw, List<(StressEffects Effects, BuildResult Build, ScrollResult? Scroll)> rows)
    {
        var native = layer == EffectsStressLayer.NativeMaui;
        var text = new StringBuilder();
        text.AppendLine($"Matrix: {LayerName(layer)} · {count:N0} cards · HW {(native ? "n/a" : hw ? "on" : "off")} · scroll {(UiThreadScroll ? "UI thread" : "render thread")} {MatrixScrollDuration.TotalSeconds:0} s");
        if (native)
        {
            text.AppendLine("effect          gen ms  1st ms  scroll ms");
            foreach (var (effects, build, scroll) in rows)
                text.AppendLine($"{Short(effects),-15} {build.GenerateMs,6:F0} {build.FirstFrameMs,7:F0} {scroll?.WallMs ?? 0,10:F0}");
            return text.ToString();
        }
        var baseline = rows[0].Scroll;
        text.AppendLine("effect          gen  1st  idle   fps  render avg/max   Δavg  rec rast live");
        foreach (var (effects, build, scroll) in rows)
        {
            var delta = baseline is { RenderAverageMs: > 0 } && scroll is not null
                ? $"{(scroll.RenderAverageMs / baseline.RenderAverageMs - 1) * 100,5:+0;-0}%" : "    –";
            text.AppendLine($"{Short(effects),-15} {build.GenerateMs,4:F0} {build.FirstFrameMs,4:F0} {scroll?.IdleFps ?? 0,5:F1} {scroll?.Fps ?? 0,5:F1} " +
                $"{scroll?.RenderAverageMs ?? 0,6:F2}/{scroll?.RenderMaxMs ?? 0,6:F2} {delta} {scroll?.RecordedPictures ?? 0,4} {scroll?.ShadowRasterizations ?? 0,4} {scroll?.LiveShadows ?? 0,4}");
        }
        text.Append("gen / 1st: build and first frame (ms) · idle: frames per second with nothing scrolling (animated content) · " +
            "render: compositor ms per scroll frame · rec: pictures recorded while scrolling");
        if (rows.Any(row => row.Scroll?.Frames.Length > 0))
        {
            text.Append("\nSlowest frame per run (@ms into the scroll: total = apply + animations + draw + flush + present):");
            foreach (var (effects, _, scroll) in rows)
            {
                if (scroll?.Frames is not { Length: > 0 } frames) continue;
                var slowest = frames.MaxBy(frame => frame.TotalMs);
                var gaps = frames.Count(frame => frame.IntervalMs > 25);
                text.Append($"\n{Short(effects),-15} @{slowest.StartMs,5:F0}: {slowest.TotalMs,6:F2} = {slowest.ApplyMs:F2} + {slowest.AnimationsMs:F2} + " +
                    $"{slowest.DrawMs:F2} + {slowest.FlushMs:F2} + {slowest.PresentMs:F2}, batches {slowest.Batches}/{slowest.Updates}" +
                    (slowest.ShadowRasterizations > 0 ? $", {slowest.ShadowRasterizations} rasterized" : "") +
                    (slowest.Collections > 0 ? $", GC {slowest.Collections}{(slowest.FullCollections > 0 ? " full" : "")}" : "") +
                    $"; {gaps} gaps > 25 ms; GCs {frames.Sum(frame => frame.Collections)}" +
                    $"; max rasterized/frame {frames.Max(frame => frame.ShadowRasterizations)}");
            }
        }
        return text.ToString();
    }

    private static string Short(StressEffects effects) => effects switch
    {
        StressEffects.None => "baseline",
        StressEffects.All => "all",
        _ => FeatureName(effects)
    };

    private static string FeatureName(StressEffects feature) => feature switch
    {
        StressEffects.Gradient => "gradient bg",
        StressEffects.RoundedCorners => "rounded",
        StressEffects.ShapedBorder => "shaped border",
        StressEffects.CardShadow => "card shadow",
        StressEffects.TextShadow => "text shadow",
        StressEffects.Clip => "clip",
        StressEffects.Translucent => "translucent",
        StressEffects.Spinner => "spinner",
        _ => feature.ToString()
    };

    private static string LayerName(EffectsStressLayer layer) => layer switch
    {
        EffectsStressLayer.Core => "Core",
        EffectsStressLayer.NativeMaui => "Native MAUI",
        _ => "SkUi*"
    };

    private static string BuildSummary(BuildResult build) =>
        $"{LayerName(build.Layer)} · {build.Count:N0} · {EffectsStressScene.Describe(build.Effects)} · generate {build.GenerateMs:F0} ms · first frame {build.FirstFrameMs:F0} ms";

    private string ScrollSummary(ScrollResult? scroll) => scroll switch
    {
        null => "Nothing to scroll.",
        _ when _lastBuild?.Layer == EffectsStressLayer.NativeMaui => $"Native MAUI · scroll {scroll.WallMs:F0} ms wall",
        _ => $"{EffectsStressScene.Describe(_lastBuild?.Effects ?? StressEffects.None)} · {scroll.Fps:F0} fps · render {scroll.RenderAverageMs:F2} / {scroll.RenderMaxMs:F2} ms · {scroll.RecordedPictures} recorded" +
            (scroll.IdleFps > 0 ? $" · idle {scroll.IdleFps:F0} fps" : "")
    };

    private static string MatrixSummary(List<(StressEffects Effects, BuildResult Build, ScrollResult? Scroll)> rows)
    {
        if (rows.Count == 0 || rows[0].Scroll is not { RenderAverageMs: > 0 } baseline)
            return $"Matrix · {rows.Count} runs (see Results)";
        var costliest = rows.Skip(1).Where(row => row.Effects != StressEffects.All && row.Scroll is not null)
            .MaxBy(row => row.Scroll!.RenderAverageMs);
        var all = rows[^1].Scroll;
        return $"Matrix · baseline {baseline.RenderAverageMs:F2} ms · all {all?.RenderAverageMs ?? 0:F2} ms" +
            (costliest.Scroll is { } worst ? $" · costliest {Short(costliest.Effects)} {worst.RenderAverageMs:F2} ms" : "");
    }

    // ---- UI helpers -------------------------------------------------------------------------------------------

    private static Label Caption(string text, double size, Color color) => new()
    {
        Text = text, FontSize = size, TextColor = color, FontFamily = DemoFonts.OpenSansRegular,
        LineBreakMode = LineBreakMode.WordWrap, VerticalOptions = LayoutOptions.Center
    };

    private Task WaitForLaidOutAsync(VisualElement view)
    {
        if (view.Width > 0 && view.Height > 0)
            return Task.CompletedTask;
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnSizeChanged(object? sender, EventArgs args)
        {
            if (view.Width <= 0 || view.Height <= 0) return;
            view.SizeChanged -= OnSizeChanged;
            done.TrySetResult();
        }
        view.SizeChanged += OnSizeChanged;
        Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(15), () =>
        {
            view.SizeChanged -= OnSizeChanged;
            done.TrySetResult();
        });
        return done.Task;
    }

    private Task FlushUiAsync()
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.Dispatch(() => done.TrySetResult());
        return done.Task;
    }

    /// <summary>Approximates UI-thread idle by draining a few dispatcher turns after layout / recording were queued.</summary>
    private Task WhenIdleAsync()
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.Dispatch(() => Dispatcher.Dispatch(() => Dispatcher.Dispatch(() => done.TrySetResult())));
        return done.Task;
    }

    private bool _autoRunStarted;

    /// <summary>
    /// Scripted runs: <c>SKUI_EFFECTS_STRESS=matrix</c> runs the matrix once the page shows; <c>SKUI_EFFECTS_STRESS=repeat</c>
    /// builds the effects in <c>SKUI_EFFECTS_STRESS_EFFECTS</c> (comma-separated <see cref="StressEffects"/> names) once and
    /// scrolls <c>SKUI_EFFECTS_STRESS_REPEAT</c> times (default 8), reporting each scroll's slowest frames (with
    /// <c>SKUI_EFFECTS_STRESS_LAYER</c> = <c>skui</c> | <c>core</c> | <c>native</c>, <c>SKUI_EFFECTS_STRESS_HW=0</c> for software,
    /// <c>SKUI_EFFECTS_STRESS_SCROLL=ui</c>, <c>SKUI_EFFECTS_STRESS_COUNT</c>); results go to the console (<c>[EffectsStress]</c>).
    /// </summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        var mode = Environment.GetEnvironmentVariable("SKUI_EFFECTS_STRESS");
        if (_autoRunStarted || mode is not ("matrix" or "repeat"))
            return;
        _autoRunStarted = true;
        _layerPicker.SelectedIndex = Environment.GetEnvironmentVariable("SKUI_EFFECTS_STRESS_LAYER")?.ToLowerInvariant() switch
        {
            "core" => 1,
            "native" => 2,
            _ => 0
        };
        _hwAcceleration.IsChecked = Environment.GetEnvironmentVariable("SKUI_EFFECTS_STRESS_HW") != "0";
        _scrollPicker.SelectedIndex = Environment.GetEnvironmentVariable("SKUI_EFFECTS_STRESS_SCROLL") == "ui" ? 1 : 0;
        if (Environment.GetEnvironmentVariable("SKUI_EFFECTS_STRESS_COUNT") is { Length: > 0 } count)
            _countEntry.Text = count;
        _chrome.RefreshChips();
        if (Environment.GetEnvironmentVariable("SKUI_EFFECTS_STRESS_EFFECTS") is { Length: > 0 } names)
            foreach (var name in names.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                if (Enum.TryParse<StressEffects>(name, ignoreCase: true, out var effect) && _featureBoxes.TryGetValue(effect, out var box))
                    box.IsChecked = true;
        var repeat = int.TryParse(Environment.GetEnvironmentVariable("SKUI_EFFECTS_STRESS_REPEAT"), out var times) && times > 0 ? times : 8;
        _chrome.RefreshChips();
        Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(1), () => _ = RunExclusiveAsync(async () =>
        {
            if (mode == "matrix")
            {
                await RunMatrixAsync().ConfigureAwait(true);
            }
            else
            {
                await BuildSelectedAsync().ConfigureAwait(true);
                for (var run = 1; run <= repeat; run++)
                {
                    var scroll = await ScrollAsync(MatrixScrollDuration).ConfigureAwait(true);
                    _chrome.Report($"Scroll {run}/{repeat}", $"Scroll {run}/{repeat}: {ScrollSummary(scroll)}" + FormatSlowFrames(scroll?.Frames ?? [], 3));
                }
            }
            Console.WriteLine("[EffectsStress] done");
        }));
    }

    /// <inheritdoc />
    protected override void OnDisappearing()
    {
        _skUiScroller?.ScrollTo(_skUiScroller.ScrollX, _skUiScroller.ScrollY); // stops a running scroll
        base.OnDisappearing();
    }
}
