namespace MauiSkiaUiSamples;

/// <summary>An example's source file, embedded by the project (<c>SampleSource/&lt;file name&gt;</c>), as text or highlighted HTML.</summary>
public static class SampleSource
{
    public static string Load(string fileName)
    {
        using var stream = typeof(SampleSource).Assembly.GetManifestResourceStream("SampleSource/" + fileName);
        if (stream is null)
            return $"// {fileName} is not embedded (see MauiSkiaUiSamples.csproj).";
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>A page showing <paramref name="code"/> syntax-highlighted (ColorCode), unwrapped, in a monospace font.</summary>
    public static string ToHtml(string code)
    {
        var highlighted = new ColorCode.HtmlFormatter().GetHtmlString(code.Replace("\r\n", "\n"), ColorCode.Languages.CSharp);
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
/// with the file name. On Mac Catalyst and Windows, when the file exists on this machine (the app runs where it was
/// built), a toolbar button opens it in the IDE that built the app (<see cref="SourceEditor"/>).
/// </summary>
public sealed class SourcePage : ContentPage
{
    public SourcePage(SampleInfo info)
    {
        Title = info.SourceFileName;
        BackgroundColor = SampleColors.Surface;
        SampleColors.ApplyNavigationBar(this);
        if (SourceEditor.ActionTitle(info.SourcePath) is { } action)
            ToolbarItems.Add(new ToolbarItem(action, null, async () => await SourceEditor.OpenAsync(info.SourcePath)));
        Content = new WebView { Source = new HtmlWebViewSource { Html = SampleSource.ToHtml(SampleSource.Load(info.SourceFileName)) } };
    }
}
