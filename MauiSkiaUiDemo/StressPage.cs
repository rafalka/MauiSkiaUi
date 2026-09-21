using System.Diagnostics;
using CommunityToolkit.Maui.Views;
using MauiSkiaUi;
using MauiSkiaUi.Core;
using SkiaSharp;

namespace MauiSkiaUiDemo;

/// <summary>
/// Stress harness for a large hosted tree. Starts with configuration only; the heavy UI is built
/// after a delayed UI-thread post so click/paint work is excluded from timings.
/// </summary>
public sealed class StressPage : ContentPage
{
    private enum StressLayer
    {
        /// <summary>MAUI-compatible SkiaUi controls (<see cref="SkUiButton"/>, etc.).</summary>
        SkUi,
        /// <summary>Lightweight Core nodes under <see cref="SkUiCoreHost"/>.</summary>
        Core,
        /// <summary>Stock MAUI controls (<see cref="Button"/>, <see cref="Grid"/>, <see cref="ScrollView"/>).</summary>
        NativeMaui
    }

    private const int DefaultChildCount = 1000;
    private const int MaxChildCount = 50_000;
    private const int RunDelayMilliseconds = 200;
    private static readonly TimeSpan ScrollProbeDuration = TimeSpan.FromSeconds(12);

    private readonly Entry _countEntry;
    private readonly CheckBox _hwAcceleration;
    private readonly CheckBox _animate;
    private readonly Picker _layerPicker;
    private readonly Button _runButton;
    private readonly Label _metrics;
    private readonly Label _selected;
    private readonly ContentView _stressHost;
    private SkUiScrollView? _skUiScroller;
    private ScrollView? _nativeScroller;
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
                "Builds a two-column grid of buttons under one scroll view, then measures generate, attach, " +
                "first-layout/render, and overall time until the UI thread is idle. " +
                "Layer picks SkUi* (MAUI-compatible SkiaUi), Core (SkUiCoreHost + Core grid + Core buttons/spinners), " +
                "or Native MAUI (stock Grid / Button / ActivityIndicator / ScrollView). " +
                "With Animate checked, the second column uses running activity indicators. " +
                "HW accel applies to SkUi/Core surfaces only. Record times CPU Paint (SkUi/Core); Scroll reports scroll cost.",
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

        _layerPicker = new Picker
        {
            Title = "Layer",
            ItemsSource = new[]
            {
                "SkUi* (MAUI-compatible)",
                "Core (no MAUI View per cell)",
                "Native MAUI controls"
            },
            SelectedIndex = 0,
            AutomationId = "StressLayerPicker",
            FontFamily = DemoFonts.OpenSansRegular,
            TextColor = DemoColors.Ink,
            HorizontalOptions = LayoutOptions.Fill
        };
        _layerPicker.SelectedIndexChanged += (_, _) =>
        {
            var native = SelectedLayer == StressLayer.NativeMaui;
            _hwAcceleration.IsEnabled = !native;
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
                },
                new Label
                {
                    Text = "Layer",
                    TextColor = DemoColors.Ink,
                    FontFamily = DemoFonts.OpenSansSemibold,
                    FontSize = 13
                },
                _layerPicker
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
        ToolbarItems.Add(new ToolbarItem("Top", null, ScrollToTop));
    }

    private StressLayer SelectedLayer => _layerPicker.SelectedIndex switch
    {
        1 => StressLayer.Core,
        2 => StressLayer.NativeMaui,
        _ => StressLayer.SkUi
    };

    private void ScrollToTop()
    {
        _skUiScroller?.ScrollTo(0, 0);
        _ = _nativeScroller?.ScrollToAsync(0, 0, false);
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
        _skUiScroller = null;
        _nativeScroller = null;

        // Keep the old tree alive until native handlers are disconnected. Forcing
        // WaitForPendingFinalizers while UIViews still tear down races CALayer KVO
        // (__NSObject_Disposer → UIView dealloc → removeFromSuperview).
        var previous = _stressHost.Content;
        if (previous is not null)
        {
            // Disconnect while the subtree is still intact so every platform view is released
            // on the UI thread (Clear would orphan children from a DisconnectHandlers walk).
            previous.DisconnectHandlers();
            ClearStressTreeContents(previous);
        }
        _stressHost.Content = null;

        _selected.Text = "No selection";
        _metrics.Text = "Releasing previous tree…";

        Dispatcher.Dispatch(async () =>
        {
            try
            {
                await SettleAfterDetachAsync().ConfigureAwait(true);
                // Drop the last strong ref on the UI thread before collecting.
                previous = null;
                await CollectGarbageAsync().ConfigureAwait(true);
                _metrics.Text = $"Starting in {RunDelayMilliseconds} ms…";
                await Task.Delay(RunDelayMilliseconds).ConfigureAwait(true);
                await RunTestAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _metrics.Text = $"Test failed: {ex.Message}";
                Console.WriteLine($"[Stress] {_metrics.Text}");
                _runButton.IsEnabled = true;
            }
        });
    }

    /// <summary>
    /// Lets the visual tree finish removeFromSuperview after handlers were disconnected.
    /// </summary>
    private async Task SettleAfterDetachAsync()
    {
        await FlushUiFrameAsync().ConfigureAwait(true);
        await WhenUiThreadIdleAsync().ConfigureAwait(true);
        if (OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst())
        {
            // Several run-loop turns so CALayer transactions from DisconnectHandlers complete.
            await Task.Delay(100).ConfigureAwait(true);
            await FlushUiFrameAsync().ConfigureAwait(true);
            await WhenUiThreadIdleAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Clears children / nested content while the tree is still strongly referenced so teardown
    /// happens on the UI thread instead of during NSObject disposer finalization.
    /// </summary>
    private static void ClearStressTreeContents(View tree)
    {
        switch (tree)
        {
            case SkUiScrollView skUi:
                switch (skUi.Content)
                {
                    case SkUiLayout layout:
                        layout.StartUpdating();
                        try { layout.Children.Clear(); }
                        finally { layout.EndUpdating(); }
                        break;
                    case SkUiCoreHost host:
                        if (host.Content is SkUiCorePanel panel)
                        {
                            panel.StartUpdating();
                            try { panel.Clear(); }
                            finally { panel.EndUpdating(); }
                        }
                        host.SetContent(null);
                        break;
                }
                skUi.SetContent(null);
                break;
            case ScrollView native:
                if (native.Content is Layout mauiLayout)
                    mauiLayout.Children.Clear();
                native.Content = null;
                break;
        }
    }

    /// <summary>
    /// Collects managed memory so prior stress trees do not inflate the next Generate timing.
    /// On Apple platforms this deliberately skips <see cref="GC.WaitForPendingFinalizers"/> —
    /// that call drains <c>__NSObject_Disposer</c> and intermittently SIGSEGVs inside
    /// UIView/CALayer KVO when a large stress tree was just torn down.
    /// </summary>
    private async Task CollectGarbageAsync()
    {
        GC.Collect();
        await FlushUiFrameAsync().ConfigureAwait(true);

        if (OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst())
        {
            await Task.Delay(100).ConfigureAwait(true);
            await FlushUiFrameAsync().ConfigureAwait(true);
            await WhenUiThreadIdleAsync().ConfigureAwait(true);
            GC.Collect();
            return;
        }

        GC.WaitForPendingFinalizers();
        await FlushUiFrameAsync().ConfigureAwait(true);
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
            var layer = SelectedLayer;

            var overall = Stopwatch.StartNew();

            var generate = Stopwatch.StartNew();
            View tree = layer switch
            {
                StressLayer.Core => BuildCoreStressTree(childCount, hwAccelerated, animate),
                StressLayer.NativeMaui => BuildNativeMauiStressTree(childCount, animate),
                _ => BuildSkUiStressTree(childCount, hwAccelerated, animate)
            };
            generate.Stop();

            var add = Stopwatch.StartNew();
            switch (tree)
            {
                case SkUiScrollView skUi:
                    _skUiScroller = skUi;
                    _nativeScroller = null;
                    break;
                case ScrollView native:
                    _nativeScroller = native;
                    _skUiScroller = null;
                    break;
            }
            _stressHost.Content = tree;
            add.Stop();

            var render = Stopwatch.StartNew();
            await WaitForLaidOutAsync(tree).ConfigureAwait(true);
            await FlushUiFrameAsync().ConfigureAwait(true);
            render.Stop();

            await WhenUiThreadIdleAsync().ConfigureAwait(true);
            overall.Stop();

            var layerLabel = layer switch
            {
                StressLayer.Core => "Core",
                StressLayer.NativeMaui => "Native MAUI",
                _ => "SkUi*"
            };
            var hwLabel = layer == StressLayer.NativeMaui ? "n/a" : (hwAccelerated ? "on" : "off");
            var metrics =
                $"Layer: {layerLabel}  |  Children: {childCount:N0}  |  HW accel: {hwLabel}  |  Animate: {(animate ? "on" : "off")}\n" +
                $"Generate UI: {generate.Elapsed.TotalMilliseconds:F1} ms\n" +
                $"Add to page: {add.Elapsed.TotalMilliseconds:F1} ms\n" +
                $"UI render (layout + first frame): {render.Elapsed.TotalMilliseconds:F1} ms\n" +
                $"Overall (start → UI idle): {overall.Elapsed.TotalMilliseconds:F1} ms";
            _metrics.Text = metrics;
            Console.WriteLine($"[Stress] {metrics.Replace("\n", " | ")}");
        }
        catch (Exception ex)
        {
            _metrics.Text = $"Test failed: {ex.Message}";
            Console.WriteLine($"[Stress] {_metrics.Text}");
        }
        finally
        {
            _runButton.IsEnabled = true;
        }
    }

    private const double CellHeight = 48;
    private const double CellSpacing = 4;

    /// <summary>
    /// SkiaUi MAUI-compatible path: <see cref="SkUiGrid"/> + <see cref="SkUiButton"/> cells.
    /// Two star columns and one absolute-height row per pair of children.
    /// </summary>
    private SkUiScrollView BuildSkUiStressTree(int childCount, bool hwAccelerated, bool animate)
    {
        var layout = new SkUiGrid();
        layout.SetPadding(new Thickness(8));
        layout.SetRowSpacing(CellSpacing).SetColumnSpacing(CellSpacing);
        layout.SetColumnDefinitions(StarColumns());
        layout.SetRowDefinitions(AbsoluteRows(childCount));

        layout.StartUpdating();
        try
        {
            for (var index = 0; index < childCount; index++)
            {
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

                Grid.SetColumn((BindableObject)cell, column);
                Grid.SetRow((BindableObject)cell, row);
                layout.Children.Add(cell);
            }
        }
        finally
        {
            layout.EndUpdating();
        }

        var scroller = new SkUiScrollView
        {
            Background = Colors.White,
            HwAccelerated = hwAccelerated
        };
        scroller.SetContent(layout);
        return scroller;
    }

    /// <summary>
    /// Core path: one <see cref="SkUiCoreHost"/> wrapping <see cref="SkUiCoreGrid"/> of
    /// <see cref="SkUiCoreButton"/> / <see cref="SkUiCoreActivityIndicator"/> cells.
    /// Same two-column grid as the MAUI path for a fair generate/render comparison.
    /// </summary>
    private SkUiScrollView BuildCoreStressTree(int childCount, bool hwAccelerated, bool animate)
    {
        var rowCount = RowCount(childCount);
        var rows = new SkUiCoreRowDefinition[rowCount];
        for (var r = 0; r < rowCount; r++)
            rows[r] = new SkUiCoreRowDefinition(new SkUiCoreGridLength(CellHeight));

        var grid = new SkUiCoreGrid()
            .SetPadding(new Thickness(8))
            .SetRowSpacing(CellSpacing)
            .SetColumnSpacing(CellSpacing)
            .SetColumnDefinitions([
                new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star),
                new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star)
            ])
            .SetRowDefinitions(rows);

        grid.StartUpdating();
        try
        {
            for (var index = 0; index < childCount; index++)
            {
                var itemNumber = index + 1;
                var column = index % 2;
                var row = index / 2;
                ISkUiCoreNode cell;
                if (animate && column == 1)
                {
                    cell = new SkUiCoreActivityIndicator()
                        .SetIsRunning(true)
                        .SetColor(DemoColors.Accent)
                        .SetHorizontalAlignment(LayoutAlignment.Center)
                        .SetVerticalAlignment(LayoutAlignment.Center);
                }
                else
                {
                    var button = new SkUiCoreButton();
                    button.SetText($"Item {itemNumber:0000}");
                    button.SetFontSize(14);
                    button.SetPadding(new Thickness(6));
                    button.SetTextColor(DemoColors.Ink);
                    button.SetFillColor(index % 4 < 2 ? Colors.White : DemoColors.StressAlt);
                    button.SetBorderColor(DemoColors.Border);
                    button.SetBorderWidth(1);
                    button.SetClicked(() => _selected.Text = $"Selected item {itemNumber:0000}");
                    cell = button;
                }

                grid.Add(cell, row, column);
            }
        }
        finally
        {
            grid.EndUpdating();
        }

        var host = new SkUiCoreHost();
        host.SetContent(grid);

        var scroller = new SkUiScrollView
        {
            Background = Colors.White,
            HwAccelerated = hwAccelerated
        };
        scroller.SetContent(host);
        return scroller;
    }

    /// <summary>
    /// Native MAUI path: stock <see cref="Grid"/> + <see cref="Button"/> /
    /// <see cref="ActivityIndicator"/> under a MAUI <see cref="ScrollView"/>.
    /// Same two-column grid as the SkUi/Core paths for a fair generate/layout comparison.
    /// </summary>
    private ScrollView BuildNativeMauiStressTree(int childCount, bool animate)
    {
        var layout = new Grid
        {
            Padding = new Thickness(8),
            RowSpacing = CellSpacing,
            ColumnSpacing = CellSpacing,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            }
        };

        var rowCount = RowCount(childCount);
        for (var r = 0; r < rowCount; r++)
            layout.RowDefinitions.Add(new RowDefinition(new GridLength(CellHeight)));

        for (var index = 0; index < childCount; index++)
        {
            var itemNumber = index + 1;
            var column = index % 2;
            var row = index / 2;
            View cell;
            if (animate && column == 1)
            {
                cell = new ActivityIndicator
                {
                    IsRunning = true,
                    Color = DemoColors.Accent,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                };
            }
            else
            {
                var button = new Button
                {
                    Text = $"Item {itemNumber:0000}",
                    FontSize = 14,
                    Padding = new Thickness(6),
                    TextColor = DemoColors.Ink,
                    BackgroundColor = index % 4 < 2 ? Colors.White : DemoColors.StressAlt,
                    BorderColor = DemoColors.Border,
                    BorderWidth = 1,
                    CornerRadius = 6
                };
                var captured = itemNumber;
                button.Clicked += (_, _) => _selected.Text = $"Selected item {captured:0000}";
                cell = button;
            }

            layout.Add(cell, column, row);
        }

        return new ScrollView
        {
            BackgroundColor = Colors.White,
            Content = layout,
            AutomationId = "StressNativeScroll"
        };
    }

    private static int RowCount(int childCount) => (childCount + 1) / 2;

    private static ColumnDefinitionCollection StarColumns() =>
    [
        new ColumnDefinition(GridLength.Star),
        new ColumnDefinition(GridLength.Star)
    ];

    private static RowDefinitionCollection AbsoluteRows(int childCount)
    {
        var rows = new RowDefinitionCollection();
        var rowCount = RowCount(childCount);
        for (var r = 0; r < rowCount; r++)
            rows.Add(new RowDefinition(new GridLength(CellHeight)));
        return rows;
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
    /// Animates a full scroll and reports average UI-thread cost.
    /// SkUi/Core use <c>RecordFrame</c> diagnostics when available; native MAUI reports wall-clock scroll only.
    /// </summary>
    private async Task MeasureScrollAsync()
    {
        if (_scrollProbeRunning)
            return;

        if (_skUiScroller is not null)
        {
            await MeasureSkUiScrollAsync(_skUiScroller).ConfigureAwait(true);
            return;
        }

        if (_nativeScroller is not null)
        {
            await MeasureNativeScrollAsync(_nativeScroller).ConfigureAwait(true);
            return;
        }
    }

    private async Task MeasureSkUiScrollAsync(SkUiScrollView scroller)
    {
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

            if (!ReferenceEquals(_skUiScroller, scroller))
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
            Console.WriteLine($"[Stress/Scroll] {scrollMetrics.Replace("\n", " | ")}");
        }
        finally
        {
            _scrollProbeRunning = false;
            _runButton.IsEnabled = true;
        }
    }

    private async Task MeasureNativeScrollAsync(ScrollView scroller)
    {
        _scrollProbeRunning = true;
        _runButton.IsEnabled = false;
        try
        {
            await scroller.ScrollToAsync(0, 0, false).ConfigureAwait(true);
            await FlushUiFrameAsync().ConfigureAwait(true);
            await WhenUiThreadIdleAsync().ConfigureAwait(true);

            var contentHeight = scroller.Content is VisualElement content
                ? content.Height
                : 0;
            var targetY = Math.Max(0, contentHeight - scroller.Height);
            var wall = Stopwatch.StartNew();
            await scroller.ScrollToAsync(0, targetY, true).ConfigureAwait(true);
            wall.Stop();

            if (!ReferenceEquals(_nativeScroller, scroller))
                return;

            var scrollMetrics =
                $"Scroll probe (native MAUI animate): wall {wall.Elapsed.TotalMilliseconds:F0} ms\n" +
                $"(RecordFrame N/A — not a SkUi surface)";
            _metrics.Text = $"{_metrics.Text}\n{scrollMetrics}";
            Console.WriteLine($"[Stress/Scroll] {scrollMetrics.Replace("\n", " | ")}");
        }
        finally
        {
            _scrollProbeRunning = false;
            _runButton.IsEnabled = true;
        }
    }

    private void MeasureRecording()
    {
        if (_skUiScroller is null || _skUiScroller.Width <= 0 || _skUiScroller.Height <= 0)
        {
            if (_nativeScroller is not null)
            {
                var note = "CPU Paint N/A for native MAUI (no SkUi Paint path).";
                _metrics.Text = $"{_metrics.Text}\n{note}";
                Console.WriteLine($"[Stress/Record] {note}");
            }
            return;
        }

        var scroller = _skUiScroller;
        using var recorder = new SKPictureRecorder();
        void Record()
        {
            var canvas = recorder.BeginRecording(new SKRect(0, 0, (float)scroller.Width, (float)scroller.Height));
            scroller.Paint(canvas);
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
        Console.WriteLine($"[Stress/Record] {recordMetrics}");
    }

    /// <inheritdoc />
    protected override void OnDisappearing()
    {
        _motion?.Dispose();
        _skUiScroller?.ScrollTo(_skUiScroller.ScrollX, _skUiScroller.ScrollY);
        base.OnDisappearing();
    }
}
