# SkUiMarkdownView

A drawn Markdown document on both layers (`SkUiMarkdownView`, `SkUiCoreMarkdown`), the same on every platform, with no platform view per paragraph. It lives in its own package, **`SkiaUi.Maui.Markdown`** (namespace `MauiSkiaUi.Markdown`), on [Markdig](https://github.com/xoofx/markdig), so `SkiaUi.Maui` has no parser dependency and an app without Markdown pays nothing. Formulas are in a second package, **`SkiaUi.Maui.Markdown.Math`**.

**MAUI counterpart:** none.

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

It is as tall as its document, so put it in a scroll view. `SkUiMarkdownView` is a `SkUiCoreHost` around a `SkUiCoreMarkdown` (its `Markdown` property), with the same members as bindable properties.

## What it draws

Headings, paragraphs, bold, italic, strikethrough, highlight (`==text==`), inserted text (`++text++`), superscript and subscript (`x^2^`, `H~2~O`), inline code, quotes, alerts (`> [!NOTE]`), containers (`:::`), fenced and indented code with a copy button, rules, nested and task lists, pipe and grid tables (spans, and cells that hold any blocks), links, email links and autolinks, emoji shortcodes, footnotes, definition lists, images and formulas.

| Member | Meaning |
| --- | --- |
| `Text` | The Markdown |
| `MarkdownStyle` | A `SkUiMarkdownStyle`: an immutable record whose `null` values come from `SkUiColorScheme` and the look. Copy `SkUiMarkdownStyle.Default` with `with`. A new one builds the document again |
| `LinkTapped`, `LinkTappedCommand` | A link's target, `mailto:` included. Nothing opens by itself |
| `ImageTapped`, `ImageTappedCommand` | An image's URL and alt text, also when the alt text shows instead of it |
| `AllowRemoteImages` | Whether `http` and `https` images load. **Off by default** |
| `ImageResolver` | `Func<string, SkUiImageSource?>`: replaces the rule of what a URL may load; `null` shows the alt text |
| `AllowImageZoom` | A tap on a picture opens it, zoomable ([`SkUiZoomView`](SkUiZoomView.md)), through the app's [popup presenter](../design/Popups.md); without one, `ImageTapped` remains |
| `CodeHighlighter` | Code and language in, `SkUiCoreSpan`s out. No tokenizer ships. Returning `null` or throwing shows the code plain |
| `MathRenderer` | Draws formulas; set by the math package. Without one, a formula shows its source in the code style |
| `IsPlainText`, `Warning` | The document could not be built or passed a limit, and shows as plain text; the event says why |
| `ContentUpdated`, `WhenUpdated` | The text is on screen ([Threads](#threads)) |

The style groups body text, heading sizes (multiples of the body size), spacing, links, highlight, code, quotes, alerts, containers, tables, rules and the limits below. An alert's titles are in the style too (`AlertNoteTitle`, …), so an app localizes them there.

## What a document cannot do

A document may come from the network or from a model, so nothing it says happens by itself.

- **Links** raise an event. A document can link to anything, `javascript:` included: check the target's scheme before you open it.
- **Remote images do not load** until you set `AllowRemoteImages`. An image URL reaches its server as soon as it draws, which can track a reader, or carry data out of a conversation when a model writes the URL. With it off, the alt text shows, and a tap still raises `ImageTapped`.
- **Local files stay out of reach.** The default rule (`SkUiMarkdownImages.Resolve`): `data:` image URLs decode in place, up to 8 MB (a document has to fit `MaximumInputLength` too, 1,000,000 characters by default); a bare relative name (no scheme, no leading `/`, no `..`, no `\`, no `%`) is one of the app's own images or assets; `http` and `https` with `AllowRemoteImages`; anything else, absolute paths and `file:` included, shows the alt text.
- **Raw HTML shows as text.** It is never interpreted.

An SVG goes through the [vector icon reader](SkUiImage.md#vector-icons), with `currentColor` as the text color. What the reader cannot draw (gradients, `use`, filters) shows the alt text.

### Images

An image alone in a paragraph is a block fitted to the width. A paragraph of only images (a row of badges) flows and wraps. Images among text cut the paragraph into text and image blocks. Size attributes reserve the space, so a load does not move the page; a load changes the document's height otherwise:

```markdown
![A chart](chart.png){width=200 height=100 aspect=AspectFit horizontal=Center}
```

`aspect` is an `Aspect` name; `horizontal` and `vertical` are `LayoutAlignment` names. Values that are not valid are ignored.

## Limits

Bad input never throws into the app. A document past one of these shows as plain text, with a `Warning`, unless the row says otherwise. All are in `SkUiMarkdownStyle`.

| Limit | Default | Past it |
| --- | --- | --- |
| `MaximumInputLength` | 1,000,000 characters | The text is cut there and shows as plain text |
| `MaximumBlocks` | 20,000 | Plain text |
| `MaximumBlockDepth` | 32 | Plain text |
| `MaximumInlineDepth` | 64 | Plain text |
| `MaximumTableCells` | 2,000 per table | That table shows its source |
| `MaximumImages` | 100 per document | Later images show their alt text |
| `MaximumInPlaceParseLength` | 0 | See [Threads](#threads) |

A paragraph, heading or table with more than 4,000 delimiters (`*`, `[`, and so on) also shows as plain text: parse time grows faster than such a block's size, and a parse cannot be stopped. Code, formulas and the data of a `data:` image do not count. A document with more than 4,000 lines holding `]:` or only `=` or `-` shows as plain text too, as definitions and setext underlines slow the parser down: those lines count even in code, so a very long pasted log can do this.

## Threads

Parsing and creating the nodes never run on the UI thread: `Text` starts a build on a background thread, and the previous content stays until it is done (`ContentUpdated`, `WhenUpdated`). Only putting the nodes in the tree, measuring and recording run on the UI thread. So a document's first frame comes a moment after `Text` is set. An app that wants the content at once, for text it trusts, sets `MaximumInPlaceParseLength` to a length up to which it builds on the setting thread, or `int.MaxValue` for always; text with more than 1,000 delimiters still goes to the background then, unless it is `int.MaxValue`.

Set `Text` and the other properties on the UI thread. `ImageResolver`, `CodeHighlighter` and a `MathRenderer` run on the background thread, so they must not touch views. Events and commands are raised on the UI thread.

## Updates

Setting `Text` again parses it whole, and top-level blocks whose source and look did not change keep their nodes, so a growing text (a streamed answer) only builds its tail, and an edit only what it touched. One build runs at a time, with the latest text winning, so a stream of appends cannot pile up parses. A new `MarkdownStyle`, a new color scheme, a new look or a new text size builds everything again. Sizes come from the app's look (`SkUiLook.Current`), because a build runs off the UI thread: a look set on part of the tree does not reach them.

## Formulas

```csharp
builder.UseSkiaUi().UseSkiaUiMarkdownMath(); // every Markdown view of the app (SkiaUi.Maui.Markdown.Math)
markdownView.UseMath();                      // or one view
```

`$$ … $$` is a block, centered, scrolling sideways when too wide. `$ … $` is part of its line, through an [inline object](SkUiLabel.md#inline-objects). Screen readers read the source.

A deep formula could end the process with a stack overflow, which cannot be caught, so every formula is checked before the engine sees it: at most 4,096 characters, and at most 48 of what can nest, added up: open `{`, commands that can hold something (`\frac`, `\sqrt`, `\left`, and anything unknown), and `^` or `_` that hold more than one symbol. Symbols such as `\alpha` and `\sum` do not count, and neither do `x^2` or `x_{10}` once closed. At most 8 `\left` can be open at once, because typesetting time grows exponentially with them. A formula holding a LaTeX comment (a `%` without its backslash) is refused too, because the engine skips what follows it, closing braces included; `\%` is fine. A formula past these limits shows its source in the code style, as does one the engine cannot read, or one that comes out too large to place: 20 lines high in a line of text, or 3,000 DIPs high or 20,000 wide on its own.

A formula is typeset as the document builds, off the UI thread, and again on the UI thread only if the text size changed since, until the new build is done.

## Not included

Text selection (labels have none); a syntax highlighter (the hook only); raw HTML rendering; editing; scrolling inside the view, and scrolling to `#anchors` (a footnote's mark does not jump); images inside a line of text.
