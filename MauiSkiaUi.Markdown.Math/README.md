# SkiaUi.Maui.Markdown.Math

Formulas for [SkiaUi.Maui.Markdown](https://www.nuget.org/packages/SkiaUi.Maui.Markdown): `$$ … $$` and `$ … $` in a document, drawn with [CSharpMath](https://github.com/verybadcat/CSharpMath).

```csharp
builder.UseSkiaUi().UseSkiaUiMarkdownMath(); // every Markdown view of the app
markdownView.UseMath();                      // or one view
```

A deep formula could end the process with a stack overflow, which cannot be caught, so every formula is checked before the engine sees it. One past the limits, or one the engine cannot read, shows its source in the code style.

CSharpMath embeds its math fonts: Latin Modern Math (GUST Font License), Cyrillic Modern and AMS Capital Blackboard Bold (SIL Open Font License 1.1). They reach your app, so carry their notices with it: `THIRD-PARTY-NOTICES.txt` in this package lists them. Its assembly is about 1.6 MB.

Released with SkiaUi.Maui, with the same version. Documentation: [SkUiMarkdownView](https://github.com/rafalka/MauiSkiaUi/blob/master/docs/controls/SkUiMarkdownView.md).
