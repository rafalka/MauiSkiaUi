using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Interactive host demo for <see cref="SkUiMauiContentView"/> wrapping Editor and WebView.</summary>
public sealed class MauiContentViewDemoPage : ComponentDemoPage
{
    private readonly Editor _editor = new() { AutoSize = EditorAutoSizeOption.TextChanges, BackgroundColor = Colors.White, TextColor = Ink, FontFamily = DemoFonts.RobotoMono };
    private readonly WebView _webView = new();

    public MauiContentViewDemoPage() : base(nameof(SkUiMauiContentView), new SkUiGrid { RowSpacing = 8, Padding = 12 },
        widthRange: (220, 420, 320), heightRange: (300, 560, 440))
    {
        _editor.Text = "<h3>Live HTML</h3>\n<p>Edit this HTML \u2014 the WebView below updates as you type.</p>";
        var grid = (SkUiGrid)SkiaControl;
        grid.RowDefinitions = [new(GridLength.Auto), new(new GridLength(140)), new(GridLength.Auto), new(GridLength.Auto), new(new GridLength(220))];
        var sourceLabel = new SkUiLabel { Text = "HTML source (edit me)", FontAttributes = FontAttributes.Bold, TextColor = Ink };
        var editorHost = new SkUiMauiContentView { Content = _editor };
        var refresh = new SkUiButton { Text = "Refresh preview", FillColor = Accent, FontSize = 14 };
        var previewLabel = new SkUiLabel { Text = "Live preview", FontAttributes = FontAttributes.Bold, TextColor = Ink };
        var webHost = new SkUiMauiContentView { Content = _webView };
        Grid.SetRow(editorHost, 1);
        Grid.SetRow(refresh, 2);
        Grid.SetRow(previewLabel, 3);
        Grid.SetRow(webHost, 4);
        grid.Children.Add(sourceLabel);
        grid.Children.Add(editorHost);
        grid.Children.Add(refresh);
        grid.Children.Add(previewLabel);
        grid.Children.Add(webHost);

        void Apply() => _webView.Source = new HtmlWebViewSource { Html = _editor.Text };
        Apply();
        _editor.TextChanged += (_, _) => Apply();
        refresh.Clicked += (_, _) => Apply();
    }
}
