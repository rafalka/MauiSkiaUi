# SkiaUi.Maui.Markdown.Math

Formulas for [SkiaUi.Maui.Markdown](https://www.nuget.org/packages/SkiaUi.Maui.Markdown): `$$ … $$` and `$ … $` in a document, drawn with [CSharpMath](https://github.com/verybadcat/CSharpMath).

```csharp
builder.UseSkiaUi().UseSkiaUiMarkdownMath(); // every Markdown view of the app
markdownView.UseMath();                      // or one view
```

A deep formula could end the process with a stack overflow, which cannot be caught, so every formula is checked before the engine sees it. One past the limits, or one the engine cannot read, shows its source in the code style.

CSharpMath bundles its math fonts (Latin Modern Math and AMS Capital Blackboard Bold, among others): its assembly is about 1.6 MB.

Released with SkiaUi.Maui, with the same version. Documentation: [SkUiMarkdownView](https://github.com/rafalka/MauiSkiaUi/blob/master/docs/controls/SkUiMarkdownView.md).
