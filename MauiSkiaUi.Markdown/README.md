# SkiaUi.Maui.Markdown

A drawn Markdown view for [SkiaUi.Maui](https://www.nuget.org/packages/SkiaUi.Maui), SkiaSharp-drawn .NET MAUI controls. It shows a document the same way on every platform, without a platform view per paragraph, on [Markdig](https://github.com/xoofx/markdig).

```xml
<sk:SkUiScrollView>
    <md:SkUiMarkdownView Text="{Binding Body}"
                         LinkTappedCommand="{Binding OpenLinkCommand}" />
</sk:SkUiScrollView>
```

```csharp
var view = new SkUiCoreMarkdown()
    .SetText(body)
    .SetMarkdownStyle(SkUiMarkdownStyle.Default with { CodeFontSize = 13 });
view.LinkTapped += OnLink;
```

Nothing a document says happens by itself: links and images raise events, a document cannot read a local file, and remote images load only with `AllowRemoteImages`. A document that cannot be built shows as plain text; bad input never throws into the app.

Formulas are in [SkiaUi.Maui.Markdown.Math](https://www.nuget.org/packages/SkiaUi.Maui.Markdown.Math).

Released with SkiaUi.Maui, with the same version. Documentation: [SkUiMarkdownView](https://github.com/rafalka/MauiSkiaUi/blob/master/docs/controls/SkUiMarkdownView.md).
