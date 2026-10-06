using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// Native controls (Entry, Editor, WebView) hosted inside a drawn scroller, between a drawn header and footer:
/// <list type="bullet">
/// <item>Clipping — scrolled overlays never cover the header / footer and cannot be touched outside the viewport.</item>
/// <item>Snapshot while scrolling (<see cref="SkUiMauiContentView.ScrollMode"/>) — overlays are frozen as bitmaps that
/// move exactly with the drawn content (try a fling, then the "Stall 2s" toolbar item); Live repositions the native
/// views instead. "HighlightSnapshots" outlines frozen overlays in red.</item>
/// <item>A focused Entry stays live; an Entry inside a nested horizontal scroller follows both scrollers.</item>
/// <item>An Entry in a collapsed expander is hidden; the WebView scrolls its own content first (scroll nesting).</item>
/// <item>"Check native placement" compares every native view with where the drawn tree places it (also with the
/// soft keyboard up); the hosted-controls checklist in docs/design/Testing.md walks through this page.</item>
/// </list>
/// </summary>
public sealed class OverlayScrollingDemoPage : ComponentDemoPage
{
    private readonly List<SkUiMauiContentView> _overlays = [];
    private readonly SkUiScrollView _scroll;
    private readonly SkUiScrollView _nested;

    public OverlayScrollingDemoPage()
        : base("Native overlays in ScrollView", new SkUiGrid { RowDefinitions = [new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)] },
            widthRange: (220, 640, 340), heightRange: (300, 640, 480))
    {
        SinglePanelHeight = 520;
        var grid = (SkUiGrid)SkiaControl;
        grid.Children.Add(Bar("Drawn header — scrolled native controls must never cover it", "#0F766E"));
        var footer = Bar("Drawn footer", "#0F766E");
        Grid.SetRow(footer, 2);
        grid.Children.Add(footer);

        var content = new SkUiVerticalStackLayout { Spacing = 8, Padding = new Thickness(12) };
        content.Children.Add(Text("Scroll with a finger (drag and fling). Native controls are hidden while the list moves and drawn from a snapshot, then restored when it settles."));
        for (var i = 1; i <= 3; i++)
        {
            content.Children.Add(Text($"Entry {i}", bold: true));
            content.Children.Add(Host(new Entry { Placeholder = $"Type here ({i})", BackgroundColor = Colors.White, TextColor = Ink }, 44));
        }
        var clicks = 0;
        var button = new SkUiButton { Text = "Drawn button (between overlays)", FillColor = Accent, FontSize = 14 };
        button.Clicked += (_, _) => { clicks++; button.Text = $"Drawn button — {clicks} taps"; };
        content.Children.Add(button);
        content.Children.Add(Text("Editor", bold: true));
        content.Children.Add(Host(new Editor { Text = "Multi-line native editor.\nFocus it and scroll: a focused control stays live.", BackgroundColor = Colors.White, TextColor = Ink }, 90));

        content.Children.Add(Text("Nested horizontal scroller with an Entry (clipped by both viewports)", bold: true));
        var row = new SkUiHorizontalStackLayout { Spacing = 8 };
        row.Children.Add(Card("Card A"));
        row.Children.Add(Host(new Entry { Placeholder = "Entry inside the carousel", BackgroundColor = Colors.White, TextColor = Ink }, 44, width: 220));
        row.Children.Add(Card("Card B"));
        row.Children.Add(Card("Card C"));
        _nested = new SkUiScrollView { Orientation = ScrollOrientation.Horizontal, Content = row, HeightRequest = 56 };
        content.Children.Add(_nested);

        content.Children.Add(new SkUiExpander
        {
            Header = Text("Expander with an Entry (tap to expand)", bold: true),
            Content = Host(new Entry { Placeholder = "Entry inside the expander", BackgroundColor = Colors.White, TextColor = Ink }, 44)
        });

        content.Children.Add(Text("WebView", bold: true));
        content.Children.Add(Host(new WebView
        {
            Source = new HtmlWebViewSource { Html = "<html><head><meta name='viewport' content='width=device-width, initial-scale=1'></head><body style='font-family:sans-serif;margin:8px;background:#EEF2FF'><h3>Native WebView</h3><p>Scroll the page: this view is frozen while moving.</p>" +
                "<p>Drag inside this box: the web page scrolls first; at its end the drawn list takes over.</p>" +
                string.Concat(Enumerable.Range(1, 12).Select(i => $"<p>Web paragraph {i}</p>")) + "</body></html>" }
        }, 140));
        for (var i = 1; i <= 8; i++)
            content.Children.Add(Text($"Drawn row {i}: filler content so the list scrolls well past the overlays."));
        content.Children.Add(Text("Last Entry", bold: true));
        content.Children.Add(Host(new Entry { Placeholder = "At the end of the list", BackgroundColor = Colors.White, TextColor = Ink }, 44));

        _scroll = new SkUiScrollView { Content = content };
        Grid.SetRow(_scroll, 1);
        grid.Children.Add(_scroll);

        _scroll.PropertyChanged += (_, args) => { if (args.PropertyName is nameof(SkUiScrollView.IsScrolling) or nameof(SkUiScrollView.ScrollY)) Status(); };
        _nested.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(SkUiScrollView.IsScrolling)) Status(); };
        foreach (var overlay in _overlays)
            overlay.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(SkUiMauiContentView.IsShowingSnapshot)) Status(); };

        Choice(nameof(SkUiMauiContentView.ScrollMode), Enum.GetValues<SkUiOverlayScrollMode>(), SkUiOverlayScrollMode.Auto,
            value => { foreach (var overlay in _overlays) overlay.ScrollMode = value; Status(); }, () => _overlays[0].ScrollMode);
        Toggle(nameof(SkUiMauiContentView.HighlightSnapshots), true, value => SkUiMauiContentView.HighlightSnapshots = value, () => SkUiMauiContentView.HighlightSnapshots);
        SkUiMauiContentView.HighlightSnapshots = true;
        Number("SnapshotRestoreDelay (ms)", 0, 1000, 150,
            value => SkUiMauiContentView.SnapshotRestoreDelay = TimeSpan.FromMilliseconds(value),
            () => SkUiMauiContentView.SnapshotRestoreDelay.TotalMilliseconds);
        ActionButton("Scroll to top", () => _scroll.ScrollToAsync(0, 0));
        ActionButton("Scroll to WebView", () => _scroll.ScrollToAsync(0, Math.Max(0, _overlays[^2].Y - 20)));
        ActionButton("Check native placement", CheckPlacement);
        OnReset(() =>
        {
            SkUiMauiContentView.HighlightSnapshots = true;
            SkUiMauiContentView.SnapshotRestoreDelay = TimeSpan.FromMilliseconds(150);
            _scroll.ScrollTo(0, 0);
            _nested.ScrollTo(0, 0);
        });
        Status();
    }

    protected override void OnDisappearing()
    {
        SkUiMauiContentView.HighlightSnapshots = false;
        base.OnDisappearing();
    }

    private void Status()
    {
        var frozen = _overlays.Count(overlay => overlay.IsShowingSnapshot);
        var mode = _overlays.Count > 0 && _overlays[0].UsesSnapshotWhileScrolling ? "snapshot" : "live";
        Feedback($"{(_scroll.IsScrolling || _nested.IsScrolling ? "Scrolling" : "Idle")} · mode {mode} · frozen {frozen}/{_overlays.Count} · offset {_scroll.ScrollY:F0}");
    }

    /// <summary>
    /// Reads every native view back from the platform and reports any that is not where the drawn tree places it (the
    /// device tests' hosted check uses the same comparison: shown / hidden, frame and visible rectangle).
    /// </summary>
    private void CheckPlacement()
    {
        if (_overlays.All(overlay => overlay.GetNativePlacement() is null))
        {
            Feedback("No native views attached (run on a device).");
            return;
        }
        var problems = _overlays
            .Select(overlay => overlay.FindNativePlacementMismatch() is { } mismatch ? $"{overlay.Content?.GetType().Name ?? "overlay"} {mismatch}" : null)
            .OfType<string>()
            .ToList();
        Feedback(problems.Count == 0 ? $"All {_overlays.Count} native views are where the drawn tree places them." : string.Join(" · ", problems));
    }

    private SkUiMauiContentView Host(View control, double height, double? width = null)
    {
        var host = new SkUiMauiContentView { Content = control, HeightRequest = height };
        if (width is { } w)
            host.WidthRequest = w;
        _overlays.Add(host);
        return host;
    }

    private static SkUiLabel Text(string text, bool bold = false) => new()
    {
        Text = text, TextColor = Ink, FontSize = 14, LineBreakMode = LineBreakMode.WordWrap, FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None
    };

    private static SkUiLabel Bar(string text, string color) => new()
    {
        Text = text, TextColor = Colors.White, FontSize = 13, Background = Color.FromArgb(color), Padding = new Thickness(10, 8),
        LineBreakMode = LineBreakMode.TailTruncation
    };

    private static SkUiBox Card(string _) => new() { Color = Color.FromArgb("#C7D2FE"), WidthRequest = 140, HeightRequest = 50, VerticalOptions = LayoutOptions.Center };
}
