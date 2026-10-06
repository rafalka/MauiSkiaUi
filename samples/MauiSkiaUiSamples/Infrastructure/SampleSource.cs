namespace MauiSkiaUiSamples;

/// <summary>
/// An example's source file, embedded by the project (<c>SampleSource/&lt;section&gt;/&lt;file name&gt;</c>, see
/// <see cref="SampleInfo.SourceKey"/>), as text or highlighted HTML.
/// </summary>
public static class SampleSource
{
    public static string Load(string key)
    {
        using var stream = typeof(SampleSource).Assembly.GetManifestResourceStream("SampleSource/" + key);
        if (stream is null)
            return $"// {key} is not embedded (see MauiSkiaUiSamples.csproj).";
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>A page showing <paramref name="code"/> syntax-highlighted (ColorCode), unwrapped, in a monospace font.</summary>
    public static string ToHtml(string code, bool xml = false)
    {
        var language = xml ? ColorCode.Languages.Xml : ColorCode.Languages.CSharp;
        var highlighted = new ColorCode.HtmlFormatter().GetHtmlString(code.Replace("\r\n", "\n"), language);
        return $$"""
            <!DOCTYPE html>
            <html>
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <style>
              html, body { margin: 0; background: #FFFFFF; -webkit-text-size-adjust: 100%; }
              body { padding: 12px; }
              pre, div { font-family: Menlo, Consolas, "Roboto Mono", monospace; font-size: 12px; line-height: 1.5; }
              pre { margin: 0; white-space: pre; tab-size: 4; }
            </style>
            </head>
            <body>{{highlighted}}</body>
            </html>
            """;
    }
}

/// <summary>
/// An example's source, syntax-highlighted in a web view (scrolls both ways, text can be selected and copied). Titled
/// with the file name; a XAML page shows its markup, and a toolbar button switches to its code-behind and back. On Mac
/// Catalyst and Windows, when the file exists on this machine (the app runs where it was built), a toolbar button opens
/// it in the IDE that built the app (<see cref="SourceEditor"/>).
/// </summary>
public sealed class SourcePage : ContentPage
{
    private readonly IReadOnlyList<(string Key, string Path)> _files;
    private readonly WebView _view = new();
    private readonly ToolbarItem? _switch;
    private ToolbarItem? _open;
    private int _shown;

    public SourcePage(SampleInfo info)
    {
        _files = info.SourceFiles;
        BackgroundColor = SampleColors.Surface;
        SampleColors.ApplyNavigationBar(this);
        if (_files.Count > 1)
        {
            _switch = new ToolbarItem { Command = new Command(() => Show((_shown + 1) % _files.Count)) };
            ToolbarItems.Add(_switch);
        }
        Content = _view;
        Show(0);
    }

    private void Show(int index)
    {
        _shown = index;
        var (key, path) = _files[index];
        var xaml = key.EndsWith(".xaml", StringComparison.Ordinal);
        Title = System.IO.Path.GetFileName(key);
        _view.Source = new HtmlWebViewSource { Html = SampleSource.ToHtml(SampleSource.Load(key), xaml) };
        if (_switch is not null)
            _switch.Text = xaml ? "C#" : "XAML";
        if (_open is not null)
            ToolbarItems.Remove(_open);
        _open = SourceEditor.ActionTitle(path) is { } action
            ? new ToolbarItem(action, null, async () => await SourceEditor.OpenAsync(path))
            : null;
        if (_open is not null)
            ToolbarItems.Add(_open);
    }
}
