using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// Interactive host demo for <see cref="SkUiMauiContentView"/> wrapping Editor and WebView.
/// Uses a 2-column grid that rearranges for phone vs tablet page widths.
/// </summary>
public sealed class MauiContentViewDemoPage : ComponentDemoPage
{
    private const double WideBreakpoint = 720;

    private readonly Editor _editor = new()
    {
        AutoSize = EditorAutoSizeOption.TextChanges,
        BackgroundColor = Colors.White,
        TextColor = Ink,
        FontFamily = DemoFonts.RobotoMono
    };
    private readonly WebView _webView = new();
    private readonly SkUiGrid _grid;
    private readonly SkUiLabel _sourceLabel;
    private readonly SkUiMauiContentView _editorHost;
    private readonly SkUiLabel _previewLabel;
    private readonly SkUiMauiContentView _webHost;
    private readonly SkUiButton _refresh;
    private bool _isWideLayout;

    public MauiContentViewDemoPage() : base(nameof(SkUiMauiContentView), new SkUiGrid { RowSpacing = 8, ColumnSpacing = 12, Padding = 12 },
        widthRange: (220, 640, 320), heightRange: (300, 560, 440))
    {
        _editor.Text = "<h3>Live HTML</h3>\n<p>Edit this HTML \u2014 the WebView below updates as you type.</p>";
        _grid = (SkUiGrid)SkiaControl;
        _grid.ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)];
        _grid.RowDefinitions = [new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)];

        _sourceLabel = new SkUiLabel { Text = "HTML source", FontAttributes = FontAttributes.Bold, TextColor = Ink, VerticalTextAlignment = TextAlignment.Center };
        _editorHost = new SkUiMauiContentView { Content = _editor };
        _previewLabel = new SkUiLabel { Text = "Live preview", FontAttributes = FontAttributes.Bold, TextColor = Ink, VerticalTextAlignment = TextAlignment.Center };
        _webHost = new SkUiMauiContentView { Content = _webView };
        _refresh = new SkUiButton { Text = "Refresh preview", FillColor = Accent, FontSize = 14 };

        _grid.Children.Add(_sourceLabel);
        _grid.Children.Add(_editorHost);
        _grid.Children.Add(_previewLabel);
        _grid.Children.Add(_webHost);
        _grid.Children.Add(_refresh);
        Grid.SetColumnSpan(_refresh, 2);

        ApplyResponsiveLayout(Width);
        SizeChanged += (_, _) => ApplyResponsiveLayout(Width);

        void Apply() => _webView.Source = new HtmlWebViewSource { Html = _editor.Text };
        Apply();
        _editor.TextChanged += (_, _) => Apply();
        _refresh.Clicked += (_, _) => Apply();
    }

    /// <summary>
    /// Tablet (wide): labels on row 0, Editor|WebView on row 1, Refresh spanning row 2.
    /// Phone (narrow): HTML source|Editor on row 0, Live preview|WebView on row 1, Refresh spanning row 2.
    /// </summary>
    private void ApplyResponsiveLayout(double pageWidth)
    {
        var wide = pageWidth >= WideBreakpoint;
        if (wide == _isWideLayout && pageWidth > 0) return;
        _isWideLayout = wide;

        if (wide)
        {
            // | HTML source | Live preview |
            // |   Editor    |   WebView    |
            // |      Refresh preview       |
            _grid.RowDefinitions = [new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)];
            Place(_sourceLabel, row: 0, column: 0);
            Place(_previewLabel, row: 0, column: 1);
            Place(_editorHost, row: 1, column: 0);
            Place(_webHost, row: 1, column: 1);
        }
        else
        {
            // | HTML source | Editor  |
            // | Live preview | WebView |
            // |      Refresh preview   |
            _grid.RowDefinitions = [new(GridLength.Star), new(GridLength.Star), new(GridLength.Auto)];
            Place(_sourceLabel, row: 0, column: 0);
            Place(_editorHost, row: 0, column: 1);
            Place(_previewLabel, row: 1, column: 0);
            Place(_webHost, row: 1, column: 1);
        }

        Place(_refresh, row: 2, column: 0);
        Grid.SetColumnSpan(_refresh, 2);
    }

    private static void Place(BindableObject view, int row, int column)
    {
        Grid.SetRow(view, row);
        Grid.SetColumn(view, column);
        Grid.SetColumnSpan(view, 1);
    }
}
