using System.Diagnostics;
using CommunityToolkit.Maui.Views;
using MauiSkiaUi;
using SkiaSharp;

namespace MauiSkiaUiDemo;

/// <summary>
/// Stress harness for a large hosted SkiaUi tree. Starts with configuration only; the heavy UI is built
/// after a delayed UI-thread post so click/paint work is excluded from timings.
/// </summary>
public sealed class StressPage : ContentPage
{
    private const int DefaultChildCount = 1000;
    private const int MaxChildCount = 50_000;
    private const int RunDelayMilliseconds = 200;
    private static readonly TimeSpan ScrollProbeDuration = TimeSpan.FromSeconds(12);

    private readonly Entry _countEntry;
    private readonly CheckBox _hwAcceleration;
    private readonly CheckBox _animate;
    private readonly Button _runButton;
    private readonly Label _metrics;
    private readonly Label _selected;
    private readonly ContentView _stressHost;
    private SkUiScrollView? _scroller;
    private IDisposable? _motion;
    private bool _scrollProbeRunning;

    /// <summary>Builds the configuration UI; the stress tree is created only when the user runs the test.</summary>
    public StressPage()
    {
        Title = "Stress test";
        Background = DemoColors.PageBackground;

        var description = new Label
        {
            Text =
                "Builds a two-column SkUiButton grid under one SkUiScrollView, then measures generate, attach, " +
                "first-layout/render, and overall time until the UI thread is idle. " +
                "With Animate checked, the second column uses running SkUiActivityIndicator views instead of buttons. " +
                "Record times CPU Paint; Scroll animates and reports average UI-thread RecordFrame ms (HW on vs off).",
            TextColor = DemoColors.Caption,
            FontFamily = DemoFonts.OpenSansRegular,
            FontSize = 12,
            LineBreakMode = LineBreakMode.WordWrap
        };

        _countEntry = new Entry
        {
            Text = DefaultChildCount.ToString(),
            Keyboard = Keyboard.Numeric,
            Placeholder = "Count",
            AutomationId = "StressChildCount",
            FontFamily = DemoFonts.OpenSansRegular,
            TextColor = DemoColors.Ink,
            WidthRequest = 88,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Center
        };

        _hwAcceleration = new CheckBox
        {
            IsChecked = true,
            Color = DemoColors.Accent,
            AutomationId = "StressHwAcceleration",
            VerticalOptions = LayoutOptions.Center
        };

        _animate = new CheckBox
        {
            IsChecked = false,
            Color = DemoColors.Accent,
            AutomationId = "StressAnimate",
            VerticalOptions = LayoutOptions.Center
        };

        _runButton = new Button
        {
            Text = "Run test",
            AutomationId = "StressRunTest",
            BackgroundColor = DemoColors.Accent,
            TextColor = Colors.White,
            FontFamily = DemoFonts.OpenSansSemibold,
            Padding = new Thickness(14, 6),
            HorizontalOptions = LayoutOptions.Start
        };
        _runButton.Clicked += OnRunClicked;

        var configurationContent = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new HorizontalStackLayout
                {
                    Spacing = 12,
                    VerticalOptions = LayoutOptions.Center,
                    Children =
                    {
                        new Label
                        {
                            Text = "Children",
                            TextColor = DemoColors.Ink,
                            FontFamily = DemoFonts.OpenSansSemibold,
                            FontSize = 13,
                            VerticalOptions = LayoutOptions.Center
                        },
                        _countEntry,
                        _hwAcceleration,
                        new Label
                        {
                            Text = "HW accel",
                            TextColor = DemoColors.Ink,
                            FontFamily = DemoFonts.OpenSansRegular,
                            FontSize = 13,
                            VerticalOptions = LayoutOptions.Center
                        }
                    }
                },
                new HorizontalStackLayout
                {
                    Spacing = 12,
                    VerticalOptions = LayoutOptions.Center,
                    Children =
                    {
                        _animate,
                        new Label
                        {
                            Text = "Animate (2nd column = spinners)",
                            TextColor = DemoColors.Ink,
                            FontFamily = DemoFonts.OpenSansRegular,
                            FontSize = 13,
                            VerticalOptions = LayoutOptions.Center
                        }
                    }
                }
            }
        };

        _metrics = new Label
        {
            Text = "Not run yet.",
            TextColor = DemoColors.Caption,
            FontFamily = DemoFonts.OpenSansRegular,
            FontSize = 12,
            LineBreakMode = LineBreakMode.WordWrap,
            AutomationId = "StressMetrics"
        };

        _selected = new Label
        {
            Text = "No selection",
            TextColor = DemoColors.Ink,
            FontFamily = DemoFonts.OpenSansRegular,
            FontSize = 12,
            AutomationId = "StressSelection"
        };

        _stressHost = new ContentView { AutomationId = "StressHost" };

        var descriptionExpander = CreateExpander("About this test", description, isExpanded: false);
        var configurationExpander = CreateExpander("Test configuration", configurationContent, isExpanded: false);
        var resultsExpander = CreateExpander("Test results", _metrics, isExpanded: true);

        var config = new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                descriptionExpander,
                configurationExpander,
                _runButton,
                resultsExpander
            }
        };

        var layout = new Grid
        {
            Padding = new Thickness(12, 8),
            RowSpacing = 6,
            RowDefinitions =
            [
                new(GridLength.Auto),
                new(GridLength.Star),
                new(GridLength.Auto)
            ]
        };
        layout.Add(config);
        layout.Add(_stressHost, 0, 1);
        layout.Add(_selected, 0, 2);
        Content = layout;

        ToolbarItems.Add(new ToolbarItem("Record", null, MeasureRecording));
        ToolbarItems.Add(new ToolbarItem("Scroll", null, () => _ = MeasureScrollAsync()));
        ToolbarItems.Add(new ToolbarItem("Top", null, () => _scroller?.ScrollTo(0, 0)));
    }

    /// <summary>
    /// Creates a Community Toolkit expander with a tinted header, title on the left, and a
    /// right-aligned expand/collapse chevron that tracks <see cref="Expander.IsExpanded"/>.
    /// </summary>
    private static Expander CreateExpander(string headerText, View content, bool isExpanded)
    {
        var indicator = new Label
        {
            Text = ExpandIndicator(isExpanded),
            TextColor = DemoColors.Caption,
            FontFamily = DemoFonts.OpenSansSemibold,
            FontSize = 14,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.End,
            Margin = new Thickness(8, 0, 0, 0)
        };

        var title = new Label
        {
            Text = headerText,
            TextColor = DemoColors.Ink,
            FontFamily = DemoFonts.OpenSansSemibold,
            FontSize = 13,
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        };

        var header = new Grid
        {
            BackgroundColor = DemoColors.SoftSurface,
            Padding = new Thickness(10, 8),
            ColumnSpacing = 8,
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ]
        };
        header.Add(title);
        header.Add(indicator, 1, 0);

        var expander = new Expander
        {
            IsExpanded = isExpanded,
            Header = header,
            Content = new ContentView
            {
                Padding = new Thickness(10, 8, 10, 4),
                Content = content
            }
        };
        expander.ExpandedChanged += (_, args) => indicator.Text = ExpandIndicator(args.IsExpanded);
        return expander;
    }

    /// <summary>Chevron shown in an expander header for the current expanded state.</summary>
    private static string ExpandIndicator(bool isExpanded) => isExpanded ? "▾" : "▸";

    private void OnRunClicked(object? sender, EventArgs e)
    {
        if (_scrollProbeRunning)
            return;

        _runButton.IsEnabled = false;
        _motion?.Dispose();
        _motion = null;
        _scroller = null;
        _stressHost.Content = null;
        _selected.Text = "No selection";
        _metrics.Text = $"GC… then starting in {RunDelayMilliseconds} ms…";

        // Let the previous tree detach before collecting, then delay so the click/paint settle.
        Dispatcher.Dispatch(async () =>
        {
            await FlushUiFrameAsync().ConfigureAwait(true);
            CollectGarbage();
            _metrics.Text = $"Starting in {RunDelayMilliseconds} ms…";
            await Task.Delay(RunDelayMilliseconds).ConfigureAwait(true);
            await RunTestAsync().ConfigureAwait(true);
        });
    }

    /// <summary>Forces a full GC so prior stress trees do not inflate the next run's Generate timings.</summary>
    private static void CollectGarbage()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private async Task RunTestAsync()
    {
        try
        {
            var childCount = ParseChildCount(_countEntry.Text);
            _countEntry.Text = childCount.ToString();
            var hwAccelerated = _hwAcceleration.IsChecked;
            var animate = _animate.IsChecked;

            var overall = Stopwatch.StartNew();

            var generate = Stopwatch.StartNew();
            var scroller = BuildStressTree(childCount, hwAccelerated, animate);
            generate.Stop();

            var add = Stopwatch.StartNew();
            _scroller = scroller;
            _stressHost.Content = scroller;
            add.Stop();

            var render = Stopwatch.StartNew();
            await WaitForLaidOutAsync(scroller).ConfigureAwait(true);
            await FlushUiFrameAsync().ConfigureAwait(true);
            render.Stop();

            await WhenUiThreadIdleAsync().ConfigureAwait(true);
            overall.Stop();

            var metrics =
                $"Children: {childCount:N0}  |  HW accel: {(hwAccelerated ? "on" : "off")}  |  Animate: {(animate ? "on" : "off")}\n" +
                $"Generate UI: {generate.Elapsed.TotalMilliseconds:F1} ms\n" +
                $"Add to page: {add.Elapsed.TotalMilliseconds:F1} ms\n" +
                $"UI render (layout + first frame): {render.Elapsed.TotalMilliseconds:F1} ms\n" +
                $"Overall (start → UI idle): {overall.Elapsed.TotalMilliseconds:F1} ms";
            _metrics.Text = metrics;
            Debug.WriteLine($"[Stress] {metrics.Replace("\n", " | ")}");
        }
        catch (Exception ex)
        {
            _metrics.Text = $"Test failed: {ex.Message}";
            Debug.WriteLine($"[Stress] {_metrics.Text}");
        }
        finally
        {
            _runButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// Creates the scrollable two-column grid without attaching it to the page.
    /// When <paramref name="animate"/> is true, column 1 hosts running activity indicators instead of buttons.
    /// Cell properties use fluent <c>Set*</c> setters (not bindable <c>SetValue</c>) and inserts run inside
    /// <see cref="SkUiView.StartUpdating"/> / <see cref="SkUiView.EndUpdating"/> so one invalidate covers the batch.
    /// Per-cell / scroller <c>AutomationId</c> is omitted to avoid bindable write cost in generate profiles.
    /// </summary>
    private SkUiScrollView BuildStressTree(int childCount, bool hwAccelerated, bool animate)
    {
        var grid = new SkUiGrid();
        grid.SetColumnSpacing(8).SetRowSpacing(4).SetPadding(new Thickness(8));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        grid.StartUpdating();
        try
        {
            for (var index = 0; index < childCount; index++)
            {
                if (index % 2 == 0)
                    grid.RowDefinitions.Add(new RowDefinition(new GridLength(48)));

                var itemNumber = index + 1;
                var column = index % 2;
                var row = index / 2;
                ISkUiView cell;
                if (animate && column == 1)
                {
                    var spinner = new SkUiActivityIndicator();
                    spinner.SetIsRunning(true).SetColor(DemoColors.Accent);
                    spinner.HorizontalOptions = LayoutOptions.Center;
                    spinner.VerticalOptions = LayoutOptions.Center;
                    cell = spinner;
                }
                else
                {
                    var button = new SkUiButton();
                    button.SetText($"Item {itemNumber:0000}")
                        .SetFontSize(14)
                        .SetPadding(new Thickness(6))
                        .SetTextColor(DemoColors.Ink);
                    button.SetFillColor(index % 4 < 2 ? Colors.White : DemoColors.StressAlt)
                        .SetBorderColor(DemoColors.Border)
                        .SetBorderWidth(1)
                        .SetCommand(new Command(() => _selected.Text = $"Selected item {itemNumber:0000}"));
                    cell = button;
                }

                Grid.SetRow((BindableObject)cell, row);
                Grid.SetColumn((BindableObject)cell, column);
                grid.Children.Add(cell);
            }
        }
        finally
        {
            grid.EndUpdating();
        }

        // HwAccelerated must be assigned before the view gets a handler (i.e. before it joins the MAUI tree).
        // Skip AutomationId on stress cells/scroller — bindable SetValue showed up in generate profiles.
        var scroller = new SkUiScrollView
        {
            Background = Colors.White,
            HwAccelerated = hwAccelerated
        };
        scroller.SetContent(grid);
        return scroller;
    }

    private static int ParseChildCount(string? text)
    {
        if (!int.TryParse(text, out var count) || count < 1)
            return DefaultChildCount;
        return Math.Min(count, MaxChildCount);
    }

    /// <summary>Waits until the stress surface has a positive arranged size (or times out).</summary>
    private Task WaitForLaidOutAsync(VisualElement view)
    {
        if (view.Width > 0 && view.Height > 0)
            return Task.CompletedTask;

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnSizeChanged(object? sender, EventArgs args)
        {
            if (view.Width <= 0 || view.Height <= 0)
                return;
            view.SizeChanged -= OnSizeChanged;
            tcs.TrySetResult();
        }

        view.SizeChanged += OnSizeChanged;
        Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(15), () =>
        {
            view.SizeChanged -= OnSizeChanged;
            tcs.TrySetResult();
        });
        return tcs.Task;
    }

    /// <summary>Lets the dispatcher process the layout/paint work queued by attaching the stress tree.</summary>
    private Task FlushUiFrameAsync()
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.Dispatch(() => tcs.TrySetResult());
        return tcs.Task;
    }

    /// <summary>
    /// Approximates UI-thread idle by draining several dispatcher turns after layout/paint have been queued.
    /// </summary>
    private Task WhenUiThreadIdleAsync()
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.Dispatch(() =>
            Dispatcher.Dispatch(() =>
                Dispatcher.Dispatch(() => tcs.TrySetResult())));
        return tcs.Task;
    }

    /// <summary>
    /// Animates a full scroll and reports average UI-thread <c>RecordFrame</c> cost.
    /// This is the metric that should be compared for HW on vs off during scrolling.
    /// </summary>
    private async Task MeasureScrollAsync()
    {
        if (_scroller is null || _scrollProbeRunning)
            return;

        var scroller = _scroller;
        _scrollProbeRunning = true;
        _runButton.IsEnabled = false;
        try
        {
            _motion?.Dispose();
            _motion = null;
            scroller.ScrollTo(0, 0);
            await FlushUiFrameAsync().ConfigureAwait(true);
            await WhenUiThreadIdleAsync().ConfigureAwait(true);

#if SKUI_DIAGNOSTICS
            scroller.ResetDiagnosticRecordStats();
#endif
            var targetY = Math.Max(0, scroller.ContentSize.Height - scroller.Height);
            var wall = Stopwatch.StartNew();
            // Bound the probe to the owned scroll duration — not AnimationClock.IsRunning, which stays
            // true while activity indicators (Animate) keep the shared clock alive.
            var motion = scroller.AnimateScrollTo(0, targetY, ScrollProbeDuration);
            _motion = motion;
            await Task.Delay(ScrollProbeDuration).ConfigureAwait(true);

            if (!ReferenceEquals(_scroller, scroller))
                return;

            motion.Dispose();
            _motion = null;
            wall.Stop();

#if SKUI_DIAGNOSTICS
            var frames = scroller.DiagnosticRecordFrameCount;
            var totalMs = scroller.DiagnosticRecordFrameTotalMs;
            var avgMs = frames > 0 ? totalMs / frames : 0;
            var hw = scroller.HwAccelerated ? "on" : "off";
            var scrollMetrics =
                $"Scroll probe ({ScrollProbeDuration.TotalSeconds:0}s): HW {hw}\n" +
                $"RecordFrame: {frames} frames, avg {avgMs:F2} ms, total {totalMs:F1} ms\n" +
                $"Wall clock: {wall.Elapsed.TotalMilliseconds:F0} ms  |  ~{(frames > 0 ? 1000.0 * frames / wall.Elapsed.TotalMilliseconds : 0):F1} record FPS";
#else
            var hw = scroller.HwAccelerated ? "on" : "off";
            var scrollMetrics =
                $"Scroll probe ({ScrollProbeDuration.TotalSeconds:0}s): HW {hw}\n" +
                $"Wall clock: {wall.Elapsed.TotalMilliseconds:F0} ms\n" +
                $"(RecordFrame stats require a SKUI_DIAGNOSTICS build)";
#endif
            _metrics.Text = $"{_metrics.Text}\n{scrollMetrics}";
            Debug.WriteLine($"[Stress/Scroll] {scrollMetrics.Replace("\n", " | ")}");
        }
        finally
        {
            _scrollProbeRunning = false;
            _runButton.IsEnabled = true;
        }
    }

    private void MeasureRecording()
    {
        if (_scroller is null || _scroller.Width <= 0 || _scroller.Height <= 0)
            return;

        using var recorder = new SKPictureRecorder();
        void Record()
        {
            var canvas = recorder.BeginRecording(new SKRect(0, 0, (float)_scroller.Width, (float)_scroller.Height));
            _scroller.Paint(canvas);
            using var picture = recorder.EndRecording();
        }

        Record();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var timer = Stopwatch.StartNew();
        for (var frame = 0; frame < 30; frame++)
            Record();
        timer.Stop();
        var bytes = (GC.GetAllocatedBytesForCurrentThread() - allocated) / 30;
        var recordMetrics =
            $"CPU Paint (direct): {timer.Elapsed.TotalMilliseconds / 30:F2} ms / {bytes:N0} B per frame";
        _metrics.Text = $"{_metrics.Text}\n{recordMetrics}";
        Debug.WriteLine($"[Stress/Record] {recordMetrics}");
    }

    /// <inheritdoc />
    protected override void OnDisappearing()
    {
        _motion?.Dispose();
        _scroller?.ScrollTo(_scroller.ScrollX, _scroller.ScrollY);
        base.OnDisappearing();
    }
}
