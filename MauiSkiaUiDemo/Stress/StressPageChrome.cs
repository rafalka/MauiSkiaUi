using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;

namespace MauiSkiaUiDemo;

/// <summary>
/// The fixed header of a stress page: status chips of the current configuration (layer, HW, animation, …), the last
/// test's one- or two-line summary, and buttons that open the description, the configuration and the full results in
/// popups and run the test. The header never changes height, so the test area below always covers the same screen area
/// and runs stay comparable.
/// </summary>
internal sealed class StressPageChrome
{
    private const double ChipRowHeight = 26;
    private const double SummaryHeight = 34; // two lines at 12 pt

    private readonly ContentPage _page;
    private readonly string _title;
    private readonly string _logTag;
    private readonly View _about;
    private readonly View _configuration;
    private readonly Func<IEnumerable<string>> _chips;
    private readonly HorizontalStackLayout _chipRow = new() { Spacing = 6, VerticalOptions = LayoutOptions.Center };
    private readonly Label _summary;
    private readonly Label _results;
    private readonly Button _run;
    private readonly Action _runAction;
    private bool _popupOpen;

    /// <param name="page">The stress page (popups open over it).</param>
    /// <param name="title">The test's name, in popup titles.</param>
    /// <param name="about">What the test does (shown in the About popup).</param>
    /// <param name="configuration">The configuration editors (shown in the Config popup; the page keeps reading them).</param>
    /// <param name="chips">The status chips of the current configuration, e.g. <c>CORE</c>, <c>HW</c>, <c>ANIM</c>.</param>
    /// <param name="run">Runs the test.</param>
    /// <param name="logTag">Prefix of the console lines (<c>[logTag] …</c>) that scripted runs read.</param>
    public StressPageChrome(ContentPage page, string title, View about, View configuration, Func<IEnumerable<string>> chips, Action run, string logTag)
    {
        _page = page;
        _title = title;
        _logTag = logTag;
        _about = about;
        _configuration = configuration;
        _chips = chips;
        _summary = new Label
        {
            Text = "Not run yet.", FontFamily = DemoFonts.OpenSansRegular, FontSize = 12, TextColor = DemoColors.Ink,
            MaxLines = 2, LineBreakMode = LineBreakMode.TailTruncation, HeightRequest = SummaryHeight,
            AutomationId = "StressSummary"
        };
        var openResults = new TapGestureRecognizer();
        openResults.Tapped += (_, _) => _ = ShowResultsAsync();
        _summary.GestureRecognizers.Add(openResults);
        _results = new Label
        {
            Text = "Not run yet.", FontFamily = DemoFonts.RobotoMono, FontSize = 11, TextColor = DemoColors.Ink,
            LineBreakMode = LineBreakMode.WordWrap, AutomationId = "StressResults"
        };
        _runAction = run;
        _run = HeaderButton("Run", "StressRun", run, primary: true);

        Header = new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new ScrollView
                {
                    Orientation = ScrollOrientation.Horizontal, HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
                    HeightRequest = ChipRowHeight, Content = _chipRow
                },
                _summary,
                new HorizontalStackLayout
                {
                    Spacing = 6,
                    Children =
                    {
                        HeaderButton("About", "StressAbout", () => _ = ShowAsync("About", _about)),
                        HeaderButton("Config", "StressConfig", () => _ = ShowConfigurationAsync()),
                        HeaderButton("Results", "StressShowResults", () => _ = ShowResultsAsync()),
                        _run
                    }
                }
            }
        };
        RefreshChips();
    }

    /// <summary>The fixed-height header to place above the test area.</summary>
    public View Header { get; }

    /// <summary>The full results text (shown in the Results popup).</summary>
    public string Results => _results.Text;

    /// <summary>Whether Run can be pressed (off while a test runs).</summary>
    public bool RunEnabled
    {
        get => _run.IsEnabled;
        set => _run.IsEnabled = value;
    }

    /// <summary>Rebuilds the status chips from the current configuration.</summary>
    public void RefreshChips()
    {
        _chipRow.Children.Clear();
        foreach (var chip in _chips())
            _chipRow.Children.Add(new Border
            {
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                BackgroundColor = DemoColors.SoftSurface,
                Padding = new Thickness(8, 2),
                VerticalOptions = LayoutOptions.Center,
                Content = new Label { Text = chip, FontFamily = DemoFonts.OpenSansSemibold, FontSize = 11, TextColor = DemoColors.Ink }
            });
    }

    /// <summary>Shows a progress or status line in the summary (e.g. "Releasing previous tree…").</summary>
    public void SetStatus(string status) => _summary.Text = status;

    /// <summary>Replaces the results with a new test's <paramref name="details"/> and shows its <paramref name="summary"/>.</summary>
    public void Report(string summary, string details)
    {
        _summary.Text = summary;
        _results.Text = details;
        Log(details);
    }

    /// <summary>Adds a follow-up measurement (scroll, record) to the results and shows its <paramref name="summary"/>.</summary>
    public void Append(string summary, string details)
    {
        _summary.Text = summary;
        _results.Text = $"{_results.Text}\n{details}";
        Log(details);
    }

    private void Log(string details)
    {
        foreach (var line in details.Split('\n'))
            Console.WriteLine($"[{_logTag}] {line}");
    }

    /// <summary>The configuration; its Run button closes the popup and runs the test with the new settings.</summary>
    private async Task ShowConfigurationAsync()
    {
        var run = await ShowAsync("Configuration", _configuration, runnable: true).ConfigureAwait(true);
        RefreshChips();
        if (run && _run.IsEnabled)
            _runAction();
    }

    private Task ShowResultsAsync() => ShowAsync("Results", _results);

    /// <summary>
    /// Shows <paramref name="body"/> in a popup with a title and a Close button (and with <paramref name="runnable"/>, a Run
    /// button; it is disabled while a test runs). Returns whether it was closed with Run. The body is detached again when
    /// the popup closes, so the same editors can be shown again in a later popup.
    /// </summary>
    private async Task<bool> ShowAsync(string title, View body, bool runnable = false)
    {
        if (_popupOpen)
            return false;
        _popupOpen = true;
        var runRequested = false;
        var scroll = new ScrollView { Content = body, MaximumHeightRequest = Math.Max(240, _page.Height * 0.7) };
        var popup = new Popup
        {
            Padding = new Thickness(16, 12),
            WidthRequest = Math.Min(Math.Max(280, _page.Width - 32), 560),
            BackgroundColor = Colors.White,
        };
        var close = new Button
        {
            Text = "Close", AutomationId = "StressPopupClose", BackgroundColor = DemoColors.Accent, TextColor = Colors.White,
            FontFamily = DemoFonts.OpenSansSemibold, FontSize = 13, Padding = new Thickness(14, 4), HorizontalOptions = LayoutOptions.End
        };
        close.Clicked += async (_, _) => await popup.CloseAsync();
        var actions = new HorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.End };
        if (runnable)
        {
            var run = new Button
            {
                Text = "Run", AutomationId = "StressPopupRun", BackgroundColor = DemoColors.Accent, TextColor = Colors.White,
                FontFamily = DemoFonts.OpenSansSemibold, FontSize = 13, Padding = new Thickness(14, 4), IsEnabled = _run.IsEnabled
            };
            run.Clicked += async (_, _) =>
            {
                runRequested = true;
                await popup.CloseAsync();
            };
            close.BackgroundColor = DemoColors.SoftSurface;
            close.TextColor = DemoColors.Ink;
            actions.Add(close);
            actions.Add(run);
        }
        else
        {
            actions.Add(close);
        }
        popup.Content = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label { Text = $"{_title} · {title}", FontFamily = DemoFonts.OpenSansSemibold, FontSize = 15, TextColor = DemoColors.Ink },
                scroll,
                actions
            }
        };
        try
        {
            await _page.ShowPopupAsync(popup, new PopupOptions { CanBeDismissedByTappingOutsideOfPopup = true }).ConfigureAwait(true);
        }
        finally
        {
            scroll.Content = null; // the page owns the body: free it for the next popup
            _popupOpen = false;
        }
        return runRequested;
    }

    private static Button HeaderButton(string text, string automationId, Action action, bool primary = false)
    {
        var button = new Button
        {
            Text = text, AutomationId = automationId, FontFamily = DemoFonts.OpenSansSemibold, FontSize = 13, Padding = new Thickness(12, 4),
            BackgroundColor = primary ? DemoColors.Accent : DemoColors.SoftSurface, TextColor = primary ? Colors.White : DemoColors.Ink
        };
        button.Clicked += (_, _) => action();
        return button;
    }
}
