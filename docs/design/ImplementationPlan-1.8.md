# SkiaUi 1.8 implementation plan: Markdown

**Goal:** a drawn Markdown view on both layers, in a new optional package, `SkiaUi.Maui.Markdown`, that shows a document the same way on every platform without a platform view per paragraph. A document from the network can neither reach anything it should not nor take the app down. `SkiaUi.Maui` gets no new dependency, and an app that does not use the package pays nothing.

**Scope:** headings, paragraphs, bold, italic, strikethrough, highlight, inserted text, superscript and subscript, inline code, quotes, alerts, containers, fenced and indented code with a copy button, rules, nested and task lists, pipe and grid tables, links, email links and autolinks, emoji shortcodes, footnotes, definition lists, images (local, remote, base64, SVG, with size attributes), block and inline math, a tap-to-zoom image popup, and light and dark colors. What stays out is under [Not included](#not-included).

**Baseline:** .NET MAUI 10.0.120 (or the latest 10.0.x when 1.8 ships), SkiaSharp 4.154.0, Markdig 1.6.0, CSharpMath.Rendering 0.5.1, branch `1.8-implementation` from `devel` after 1.7.0. What was planned for 1.8 before (performance, tiles, Core containers, password reveal) moved to [1.9](ImplementationPlan-1.9.md). What has shipped is in the [changelog](../../CHANGELOG.md).

**Rules** (from [Requirements.md](Requirements.md#principles)):

- 1.x keeps the 1.0 API: changes to `SkiaUi.Maui` are additive.
- Both layers: every member lands on the SkUi\* control and its Core twin (`Set*`), with one shared engine.
- A feature a view does not use costs nothing per frame (`scripts/bench.sh --baseline v1.7.0`).
- Like `SkiaUi.Maui.CommunityToolkit`, the package uses only public API, with no `InternalsVisibleTo` to it. What it needs from the engine becomes public API in [Phase X](#phase-x--engine-additions-in-skiauimaui).
- Content is never dropped: a block the builder does not know shows its source text. Bad input never throws into the app.

## Status

Built on `1.8-implementation`: phases P, B, I, U, M, X1, X2 and Z. Not built: X3 (step 12), which nothing has asked for. V2 is not planned for now and is in the [backlog](Roadmap.md#lists-and-scrolling). Images among text still cut the paragraph; X1 could place them in the line later.

G1 and V1 so far: the Markdown scenario with formulas passes under Native AOT on Mac Catalyst, and fully trimmed on the Galaxy S9, with no trim or AOT warnings from SkiaUi. Numbers: [Performance.md](../Performance.md#markdown). Assemblies before trimming: Markdig 0.5 MB, CSharpMath 1.8 MB with its fonts.

Still open before release: the iPhone 8 and Windows; app size with and without each package on each platform; the LaTeX depth that overflows on those devices' UI thread (the guard's limits come from the 512 KB stack test, not from them; formulas are typeset on a background thread and drawn on the UI thread); a screen reader pass.

CSharpMath's embedded fonts: Latin Modern Math under the GUST Font License, Cyrillic Modern and AMS Capital Blackboard Bold under the SIL Open Font License 1.1. Both allow redistribution; `THIRD-PARTY-NOTICES.txt` in the math package lists them, with the copyright lines from the fonts themselves.

## What a spike found

Run on 5 November 2026 on macOS arm64, with Markdig 1.6.0 (its `net10.0` assets) and CSharpMath.Rendering 0.5.1 (`netstandard2.0`). Timings are desktop numbers. **Nothing here has run on a device or drawn a pixel:** math used a canvas that only counts calls, which is what [G1](#phase-g--gate-on-the-real-packages) and [M1](#phase-m--math) are for.

| Question | Result | What it means here |
| --- | --- | --- |
| Native AOT, both libraries | A console app published for `osx-arm64` with `TrimmerSingleWarn=false`: **no trim or AOT warnings**, and both ran correctly. Markdig declares `IsAotCompatible` on `net8.0`+ and has its own AOT tests; CSharpMath.Rendering's project file does not declare it | Both can sit behind `TrimAnalysis.props`. The only warning was NU1903, a security advisory on the `Microsoft.DotNet.ILCompiler` 10.0.13 package that the publish pulled in, not on either library |
| Licenses | Markdig BSD-2-Clause, CSharpMath MIT | Fine for an MIT package; dependencies of 0.5.1 are checked in G1 |
| Parse time | 38 KB of mixed Markdown: 2.4–4.7 ms. 1 MB of plain text: 53 ms | Ordinary documents can parse on the UI thread |
| Slow input | 13 KB of unbalanced emphasis: 165 ms. 19 KB of `[a](`: 170 ms. **156 KB of `[a](`: 8.7 s** | Time grows faster than size, so large input parses off the UI thread: [U2](#phase-u--updates-and-hostile-input) |
| Deep Markdown | `ArgumentException` at 128 nested quotes, 64 nested lists, or 140 KB of emphasis runs. No crash on a 512 KB stack | Catchable: [U3](#phase-u--updates-and-hostile-input) falls back to plain text. The limits are in Markdig's `ThrowHelper`: 128 for blocks, **10,240 for inlines**. So a parsed document can still hold inlines nested far deeper than our recursion should go, and the builder needs its own caps, or an iterative walk for inlines |
| Deep LaTeX | **Uncatchable stack overflow** on a 512 KB stack at 1,000 `{`, 1,000 `x^{`, 500 `\frac`, 500 `\sqrt`, 500 `\left`; on 1 MB at 2,000 `\frac` and 5,000 `{`. 100 `\frac`, 100 `x^{` and 500 `{` pass | A short formula in a downloaded document can kill the app: [M2](#phase-m--math) checks every formula first. **A release blocker for math** |
| Invalid LaTeX | No exception: `ErrorMessage` is set and `Measure` returns the size of the error text | The view shows the formula's source instead |
| Typesetting | First formula 17 ms, then about 0.1 ms each. A 40 KB one-line formula: 408 ms and 718,234 wide | Fast enough to run while building; a length cap goes in M2 |
| Math drawing | `ICanvas` is 13 members and `Path` 7: lines, rectangles, `MoveTo` / `LineTo` / quadratic and cubic curves | Maps directly onto `SKCanvas` and `SKPath`: a small adapter |
| Syntax tree | Headings, paragraphs, quotes, lists, tasks, fenced code, tables (grid tables arrive as `Table` too), containers, footnotes, definition lists, emoji, links, autolinks, inline math | Everything seen arrives as a typed node. All emphasis kinds share `EmphasisInline`, told apart by delimiter. Alerts, block math and indented code did not appear, because the sample did not write them in a form that parses as such: B3, B5 and M3 test them |

Found in this repository:

| Fact | What it means here |
| --- | --- |
| `SkUiCoreSpan` has font, colors, background, decorations, line height and `Tapped`; `SkUiCoreLabel` has `SetSpans`; `SkUiCoreTable`, `SkUiCoreBorder`, `SkUiCoreScrollView`, `SkUiCoreWrapLayout`, `SkUiCoreCheckBox`, `SkUiCoreImage` exist | Most of the view is a mapping from the syntax tree to nodes that exist |
| Own nodes override the public `MeasureContent` and `OnPaintContent` | A math node needs nothing new |
| `SkUiImageSource.FromString` opens absolute paths and `file:` URIs as files | **Never pass a URL from a document to it:** [I1](#phase-i--images) resolves URLs itself |
| The icon reader fails on gradients, `use`, `image`, filters and more ([SkUiImage.md](../controls/SkUiImage.md)) | Such an SVG shows its alt text until [Full SVG](ImplementationPlan-FullSvg.md) exists |
| The compositor culls when drawing, but dirty nodes are recorded whether they show or not | A long document records everything on its first frame: measured in [V1](#phase-v--long-documents) |
| No Core virtual stack exists ([SkUiVirtualVerticalStackLayout.md](../controls/SkUiVirtualVerticalStackLayout.md)) | Virtualizing would need the twin first or be SkUi-only: V2 |
| Core controls copy scheme colors when created ([roadmap](Roadmap.md#architecture-and-performance)) | A theme change rebuilds the tree: U4 |
| Rich text has no inline objects; `sub` / `sup` have no baseline shift; links are not accessibility elements | [Phase X](#phase-x--engine-additions-in-skiauimaui) |
| No default monospace family exists | B5 picks one per platform |

## API

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

| Member | Meaning |
| --- | --- |
| `Text` | The Markdown |
| `MarkdownStyle` | A `SkUiMarkdownStyle`: an immutable record whose `null` values come from `SkUiColorScheme` and the look. Not `Style`, which is MAUI's on the SkUi layer |
| `LinkTapped`, `LinkTappedCommand` | A link's target, `mailto:` included, as on HTML labels. Nothing opens by itself |
| `ImageTapped`, `ImageTappedCommand` | An image's source and alt text; the app decides what happens until Phase Z |
| `AllowRemoteImages` | Whether `http` and `https` images load; off by default ([D8](#decisions)) |
| `ImageResolver` | `Func<string, SkUiImageSource?>`: replaces the default rule of I1; `null` shows the alt text |
| `CodeHighlighter` | Optional: code and language in, `SkUiCoreSpan`s out. No tokenizer ships |
| `MathRenderer` | Set by the math package's `UseMath()`; without it a formula shows its source in the code style |

`SkUiMarkdownStyle` groups body text, heading sizes (multipliers of the body size, levels 1–6), spacing, links, highlight, code, quotes, alerts, tables and rules. Named styles are static values an app can copy with `with`.

## Phase G — Gate on the real packages

- **G1 The packages in real app builds** (first; decides whether math goes ahead as planned). Size: S, device time.
  - A scratch branch of the samples app with Markdig and CSharpMath.Rendering, parsing a mixed document and typesetting formulas.
  - Builds and runs on iOS, Android, Mac Catalyst and Windows. `scripts/device_tests.sh -t maccatalyst --aot` and `--trim`: the same output as without them, and the math fonts load.
  - Numbers to record: app size with and without each package on each platform; cold first parse and first formula on the Galaxy S9 and the iPhone 8; the LaTeX depth that overflows on those devices' UI thread.
  - Also checked: the licenses of CSharpMath's dependencies and of its bundled fonts.
  - If CSharpMath fails under AOT or trimming, the math package is documented as not trimmable or held back. The rest of the plan does not depend on it.

## Phase P — Package and view

- **P1 The package.** Size: S.
  - `MauiSkiaUi.Markdown` (package `SkiaUi.Maui.Markdown`, namespace `MauiSkiaUi.Markdown`), modeled on `MauiSkiaUi.CommunityToolkit`: same TFMs, `TrimAnalysis.props`, `IsAotCompatible`, packed against `SkiaUi.Maui` of the same version. Markdig as `[1.6.0, 2.0.0)`.
  - Added to `SkiaUi.slnx`, the tests, the demo and the samples; to the project lists in `ci.yml`, `nuget-pack.yml` and `nuget-publish.yml`; to [Releasing.md](../Releasing.md) and [Development.md](../../Development.md).
- **P2 `SkUiCoreMarkdown` and `SkUiMarkdownView`.** Size: M.
  - A builder walks the syntax tree and returns Core nodes in a vertical stack, recursing with a depth cap. `SkUiCoreMarkdown` owns it; `SkUiMarkdownView` is a `SkUiCoreHost` around one, with bindable properties in the [property pattern](../../CLAUDE.md#conventions).
  - The pipeline turns on only what the scope lists, not `UseAdvancedExtensions` (which also brings diagrams, media links, figures, citations and more that the builder would have to handle), plus `DisableHtml` ([D5](#decisions)). Emoji with `enableSmileys: false`, so `:)` stays text; `UseGenericAttributes` last, as Markdig requires. One shared pipeline, built once.
  - Tests: an empty and a `null` text, changing the text, measure at several widths, `ImplicitStyleTests`.

## Phase B — Blocks and text

- **B1 Inlines to spans.** Bold, italic and both; strikethrough; highlight (span background); inserted text (underline); superscript and subscript (smaller, until X2); inline code (code font, background); links, autolinks and email links (a span's `Tapped`); hard and soft breaks; emoji. Size: S.
- **B2 Paragraphs, headings, rules.** Heading sizes and `SetSemanticHeadingLevel`; `SkUiCoreLine` or `SkUiCoreBox` for rules; paragraph spacing from the style. Size: S.
- **B3 Quotes, alerts, containers.** `SkUiCoreBorder` with nested blocks. Alert titles and the five colors come from the style, so an app localizes the titles there: `SkUiStrings` lives in `SkiaUi.Maui`, which stays free of Markdown's texts. The bar is on the start side in right-to-left text. Size: S.
- **B4 Lists.** Marker and content in a grid, so wrapped lines stay aligned; numbers from the list's start value; nesting; task lists with a read-only `SkUiCoreCheckBox` whose state is read by screen readers. Size: S.
- **B5 Code blocks.** Default family per platform (Menlo, `monospace`, Consolas), checked on each with the look comparison in mind; no wrapping, a horizontal `SkUiCoreScrollView` inside, with its drags tested against the page's scroll view; a copy button through MAUI's `Clipboard`; `CodeHighlighter`. Size: S–M.
- **B6 Tables.** `SkUiCoreTable` with auto columns; column alignment; header background; separators from the style; column and row spans of grid tables; **cells take any blocks**, not only a first paragraph. Too wide: scrolls horizontally. Size: M.
- **B7 Footnotes and definition lists; everything else.** Footnotes as a list at the end, with their marks as plain small text (no jump, since scrolling to anchors is not included); definition lists as indented blocks; unknown nodes as source text. Size: S.

## Phase I — Images

- **I1 What a URL may load** (before any remote document is tried in the demo). Size: S.
  - Default rule: `http` and `https` load through `FromUri` only with `AllowRemoteImages` ([D8](#decisions)). `data:` image URLs decode through `FromStream`, up to a size limit. A bare relative name (no scheme, no leading `/`, no `..`) goes through `FromFile`, so an app's `MauiImage` or `MauiAsset` works. Everything else, absolute paths and `file:` included, shows the alt text.
  - `ImageResolver` replaces the rule. Tests for each scheme and for traversal.
- **I2 Images in the flow.** Size: M.
  - Alone in a paragraph: a block image, fitted to the width, honoring the `width`, `height`, `aspect`, `horizontal` and `vertical` attributes (`{width=200 aspect=AspectFit}`). Only images in a paragraph (a row of badges): `SkUiCoreWrapLayout`. Images among text: the paragraph splits into text and image blocks, until X1.
  - Reserves space from the attributes when given, so a load does not move the page; alt text as the semantic description and while a load fails. `ImageTapped`.
  - Loaded images change the height of the document: checked inside a scroll view, with the scroll offset kept.
- **I3 SVG.** `.svg` URLs and `data:` go through the existing reader; `currentColor` is the text color. What the reader cannot draw shows the alt text. Size: S.

## Phase U — Updates and hostile input

- **U1 Reusing blocks.** A new text is parsed whole, and top-level blocks with the same source and style keep their nodes, so a streamed answer (a growing text, as from an LLM) only rebuilds its tail. Tests count recorded pictures per append. Size: M.
- **U2 Nothing is parsed or built on the UI thread.** Parsing and creating Core nodes run on a background thread with the latest text winning; only putting them in the tree, measuring and recording run on the UI thread. An app can opt in to building in place for text it trusts. Size: S.
- **U3 Hostile documents** (a release blocker). Size: S.
  - Any parse exception or a nesting past the builder's cap: the text shows as a plain label, with a `SkUiDiagnostics` warning. Limits on input length, cells per table and images per document, all in the style or as constants documented on the control page.
  - Tests with the spike's documents, on a 512 KB stack as well, including inlines nested close to Markdig's limit of 10,240: they parse, so they reach the builder.
- **U4 Colors.** A weak subscription (`SkUiWeakListener`) to `SkUiColorScheme.CurrentChanged` rebuilds what the scheme colors. A leak scenario in `tests/Shared/MemoryLeaks`, with span tap handlers in the document. Size: S.

## Phase M — Math

A second package, `SkiaUi.Maui.Markdown.Math` ([D6](#decisions)).

- **M1 Adapter and node.** Size: S–M.
  - `ICanvas` and `Path` on `SKCanvas` and `SKPath`; a node that typesets in `MeasureContent` and draws in `OnPaintContent`. The text color, and font scaling as on labels (`FontAutoScalingEnabled`, `SkUiLook.FontScale`). Render tests against reference formulas.
  - The adapter disposes its paths when each one is drawn, and draws only inside the node's recording, so nothing native reaches the render thread.
- **M2 Guard** (before any network document with math; a release blocker). Size: S.
  - **Before the parser sees a formula,** a scan bounds its length and counts everything that nests, including what nests without braces (`\sqrt\sqrt`, `\frac\frac`, `^`, `_`, `{`, `\left`). The limit comes from G1's device numbers, well inside what passed (under 100 levels). Past it, or on `ErrorMessage`: the source in the code style.
  - Drawing recurses too, so the tests draw the deepest formula that passes on a 512 KB stack, not only parse it.
- **M3 Block math.** `$$ … $$`, centered, scrolling horizontally when too wide. Semantic description: the source. Size: S.
- **M4 Inline math.** Needs X1 ([D7](#decisions)); until then the source in the code style. Size: S once X1 exists.

## Phase X — Engine additions in `SkiaUi.Maui`

Additive, useful beyond Markdown, and each can be dropped without affecting the phases above.

- **X1 Inline objects in text.** A span with a fixed width, height and baseline that wraps with the line, on both layers, with a public way to place a node there. Touches `SkUiShaping` and `SkUiRichTextLayout`: bidi, truncation and hit testing must all handle it. `scripts/bench.sh`: text without one costs nothing. Unlocks inline math and images among text. Size: M–L.
- **X2 Baseline shift on spans.** A real superscript and subscript; also fixes `sub` / `sup` in `SkUiHtml`. Size: S–M.
- **X3 Links as accessibility elements.** Already in the [backlog](Roadmap.md#accessibility). Matters here because a document can be mostly links. Size: M.

## Phase V — Long documents

- **V1 Measure.** Bench scenarios for a 100 KB document: first frame, scrolling, and an append (`scripts/bench.sh`, set through `TrySetOption` so baselines still build), plus the same document on the S9 and the iPhone 8. Results in [Performance.md](../Performance.md). Size: S, device time.
- **V2 Not planned for now; in the [backlog](Roadmap.md#lists-and-scrolling).** To take up when a real app shows long documents or later measurements ask for it. Top-level blocks as rows of a virtual stack, recorded when near the viewport. Needs the Core twin of `SkUiVirtualVerticalStackLayout`, or is SkUi-only at first. The 1.9 plan's [R2](ImplementationPlan-1.9.md#phase-r--performance-backed-by-measurements) (no re-recording of ancestors on a measure change) would help appends and image loads, but comes after this release, so V1 measures without it and is run again after it. Size: M–L.

## Phase Z — Image zoom

- **Z1 `SkUiZoomView` and `SkUiCoreZoomView`** in `SkiaUi.Maui`: pinch, pan, double tap and wheel on the existing recognizers, with composite-time scale and offset so nothing re-records. Useful for any image, so not tied to Markdown. Size: M.
- **Z2 The popup.** `AllowImageZoom` opens one through the app's [`ISkUiPopupPresenter`](Popups.md); SkiaUi ships no popup windows. Without a presenter, `ImageTapped` remains. Size: S.

## Not included

Text selection (labels have none; a separate engine feature); a bundled syntax highlighter (the hook only); raw HTML rendering; editing; scrolling inside the view (put it in a scroll view) and scrolling to `#anchors`; a large set of ready-made themes.

## Docs, tests and demo

- [docs/controls/SkUiMarkdownView.md](../controls/README.md) and a row in the controls index; what the limits are and how to check a link's scheme before opening it. The package's `README.md`.
- A gallery page in `MauiSkiaUiDemo/ComponentPages` with every feature, and in-app links; an example in the samples app.
- Headless tests in `tests/MauiSkiaUi.Tests`: `UseBundledFont` for text, PNG bytes for images, no network.
- [CHANGELOG.md](../../CHANGELOG.md) per release; [Roadmap.md](Roadmap.md) status; [Requirements.md](Requirements.md) gets a new requirement number.

## Order

| # | Steps | Size | Why here |
| --- | --- | --- | --- |
| 1 | G1 | S | Sets limits and thresholds; decides math's packaging |
| 2 | P1, P2 | S, M | Everything draws through them |
| 3 | B1, B2 | S, S | First real document on screen |
| 4 | U3 | S | **Before any network document is tried;** a release blocker |
| 5 | B3, B4, B5, B6, B7 | S–M each | The rest of the blocks; independent of each other |
| 6 | I1, I2, I3 | S, M, S | I1 first, also a security step |
| 7 | U1, U2, U4 | M, S, S | Depend on the builder being complete |
| 8 | V1 | S | Measures long documents; **if 1.8 has to ship early, it can stop here** |
| 9 | M1, M2, M3 | S–M, S, S | Second package; M2 before any network formula |
| 10 | X2, X1, M4 | S–M, M–L, S | Inline objects, then inline math |
| 11 | Z1, Z2 | M, S | Independent of everything above |
| 12 | X3 | M | When a real app asks for it |

1.8 is steps 1–11. If it has to ship early, steps 1–8 stand alone (the whole scope except math, inline objects and zoom) and the rest moves to a later release. Step 12 is in 1.8 only if a real app asks for it. V2 is not a step of 1.8: it is in the [backlog](Roadmap.md#lists-and-scrolling).

## Decisions

Proposed unless marked **agreed**.

- **D1 A separate package.** `SkiaUi.Maui` has no parser dependency and an app without Markdown should not get one. It follows the one-version rule ([Releasing.md](../Releasing.md)).
- **D2 Built on Core nodes.** A document creates hundreds of nodes and Core trees build about three times faster, and the SkUi view is a host around the same engine, so both layers stay at parity by construction. Core's first frame is not faster on devices today ([roadmap](Roadmap.md#architecture-and-performance)), so V1 measures rather than assumes the gain.
- **D3 One style record, not dozens of bindable properties.** A document's look is set once and reused, a record is simple to share across views, and a rebuild has one trigger. A property for each heading level's color, size and font would be most of the API and little of the value.
- **D4 One link event.** `mailto:` is a link with a target; no second event for email. Nothing opens by itself, as on HTML labels.
- **D5 Raw HTML shows as text.** `DisableHtml` in the pipeline. Rendering it through `SkUiHtml` does not work for tags split across inlines, and untrusted documents should not gain markup. Can be revisited if a real app needs it.
- **D6 Math in its own package.** CSharpMath brings fonts and code most apps never use, and its AOT status is the only one not declared. Markdown exposes the `MathRenderer` hook, so the two stay separate.
- **D7 Inline math before X1.** Agreed: the source in the code style until X1 exists, since a paragraph split around a formula breaks wrapping, bidi and justification. X1 is in 1.8, so with the math package a formula is drawn in its line; without it, it shows its source.
- **D8 Remote images by default.** Agreed: off, with `AllowRemoteImages` to turn them on and `ImageResolver` to block or rewrite them. An image URL in a chat message or an LLM's answer reaches its server as soon as it draws, which can track a reader or carry data out of a conversation. It surprises everyone once, so the control page says it first and the alt text shows meanwhile.
- **D9 Zoom last.** `ImageTapped` covers apps until then, and Z1 is worth designing as a general control rather than as part of this view.
- **D10 Markdown is 1.8.** Agreed. It takes the place of what was planned for 1.8, which moved to [1.9](ImplementationPlan-1.9.md).

## Acceptance

- Headless suite and memory-leak scenarios pass; 0 build warnings with the trim and AOT analyzers in every package.
- `SkiaUi.Maui`'s dependencies are unchanged, and an app without the package restores and draws as before. `scripts/bench.sh --baseline v1.7.0` shows no regression in the existing scenarios.
- None of the hostile documents, Markdown or LaTeX, ends the process or throws into the app, on the S9 and the iPhone 8 as well as headless, on small stacks as well as the UI thread.
- No document can open a link or read a local file by itself, and unless an app sets `AllowRemoteImages` or an `ImageResolver`, no document makes a network request.
- The gallery page checked on iOS, Android, Mac Catalyst and Windows, in light and dark and right to left, with a screen reader on one platform per family; under Native AOT on Mac Catalyst (CI). V1's numbers in [Performance.md](../Performance.md).
