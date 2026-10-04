# SkUiLabel

Drawn text with wrapping, truncation, fonts, alignment, padding and MAUI Label's text properties, spans with their own styles and tap recognizers (`FormattedText`), HTML text (`TextType="Html"`) with tappable links, custom line breaking (shorter forms instead of an ellipsis) and optional rounded chrome for badges, chips and tags.

**MAUI counterpart:** [`Label`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/label)

## How it works

Text is shaped with **HarfBuzz**: ligatures, kerning, Arabic joining, Indic reordering and mark placement. **Bidirectional** paragraphs (RTL and mixed LTR/RTL) are resolved with the Unicode bidi algorithm. **Font fallback** is per character, so a font that lacks a script or emoji falls back to a system font that has it. The primary font is always used where it has the glyph, including spaces and punctuation. Measure accounts for padding and the line-break mode.

Line breaking works on the shaped widths and never splits a grapheme. It breaks after spaces and hyphens, and between CJK ideographs and kana. Shaped lines are cached as text blobs per wrap width, so color and alignment changes don't re-shape. Passive by default: does not consume taps unless `Tapped` / `TappedCommand` is set.

**Rendering modes (`TextRendering`):**

| Mode | Behavior | Use for |
| --- | --- | --- |
| `Default` | Uses the global `SkUiTextOptions.DefaultRendering` (initially `Auto`) | Most labels |
| `Auto` | **Fast path** for text made only of Latin / digits / common punctuation that the font fully covers, in an LTR paragraph. It uses Skia `MeasureText` / `DrawText`, the same cost as the pre-HarfBuzz renderer. Anything else is shaped with HarfBuzz | Mixed content |
| `Shaped` | Always HarfBuzz: kerning and ligatures even for Latin | Typography-sensitive text |
| `Simple` | Never shapes: no bidi, no font fallback. Complex scripts render incorrectly and missing glyphs show as .notdef | Dense grids of plain text or numbers |

A label measured at one width and drawn at a wider one reuses its lines when nothing had to wrap, so measure and draw cost one layout.

**Text properties (MAUI Label's):**

| Property | Behavior |
| --- | --- |
| `LineBreakMode` | `WordWrap` (default; buttons: `NoWrap`, as MAUI), `CharacterWrap`, `NoWrap`, head / middle / tail truncation. A `NoWrap` line wider than the label starts at the start side whatever the alignment, and is cut at the end, as native labels show it |
| `MaxLines` | The most lines drawn; `-1` (default) or `0`: no limit. Wrapped lines past it are dropped. With `TailTruncation` the text wraps and the last line ends with the ellipsis, as on MAUI; without `MaxLines`, tail truncation keeps one line per paragraph. Explicit newlines count as lines |
| `LineHeight` | Multiplier of the font's line spacing (`1.8`: 80 % more); `-1` (default) or `0`: the font's. The extra space is split above and below each line (CSS half-leading), so a single line stays centered in its taller box |
| `CharacterSpacing` | DIPs added after each character (grapheme, or ligature), negative for tighter; measure, wrapping and truncation count it. Plain Latin text keeps the `Auto` fast path |
| `TextDecorations` | `Underline`, `Strikethrough` or both, in the text color, at the font's positions, across each line's glyphs (all of its bidi runs). Paint-only: no re-layout |
| `TextTransform` | `Uppercase` / `Lowercase` (invariant culture, as MAUI) change the displayed text; `Text` keeps its value. `Default` / `None`: as written |

**Rounded chrome:** `CornerRadii` (per corner, MAUI's `CornerRadius` type as on [`SkUiBorder`](SkUiBorder.md)), `BorderColor` and `BorderWidth` shape the `Background` (solid or gradient) / `BackgroundColor` fill, drawn through `SkUiLook.DrawRoundedBox` like [`SkUiButton`](SkUiButton.md) (which inherits them). `CornerRadius` (an `int`, as on MAUI's `Button`) sets all four corners at once and reads the top-left one, rounded; use `CornerRadii` for fractional radii; whichever of the two is set last wins. A badge is a single label, without a wrapping [`SkUiBorder`](SkUiBorder.md), so it costs no extra layout node or picture. Radii larger than the label allows are scaled down, so a large uniform radius gives a pill. The border is drawn inside the bounds and `Padding` is not adjusted. With a radius, text is clipped to the rounded shape; hit bounds stay rectangular. A label with square corners and no border draws its background as before.

**Bold and italic:** `FontAttributes` picks the family's bold / italic face (system families, and fonts MAUI registered with CoreText on iOS / Mac Catalyst). A face that has none, such as a `ConfigureFonts` font with only a regular file, is drawn with synthetic bold (Skia's emboldened outlines) and a synthetic slant, as Android does. Font fallback keeps the style.

**Justify:** `HorizontalTextAlignment="Justify"` widens the spaces of each wrapped line so it fills the width; a paragraph's last line, and a line without spaces, stay at `Start`. Justified labels measure as wide as the width they are given. `VerticalTextAlignment="Justify"` spreads the lines over the label's height (a single line stays at the top); MAUI has no vertical justify.

**Direction:** effective `FlowDirection` RTL (explicit on the label or inherited from any ancestor) makes paragraphs RTL. `FlowDirection="LeftToRight"` forces LTR. The default (`MatchParent` under an LTR parent) lets the first strong character decide, like Android's `firstStrong`. `HorizontalTextAlignment` `Start` / `End` follow the resolved paragraph direction, so `Start` is the right edge for RTL text.


## Custom line breaking

`LineBreaker` (an `SkUiTextLineBreaker`) replaces `LineBreakMode` with your own code: it gets the text and the available width and returns the lines to draw. Use it when "..." is the wrong way to shorten text:

- **Shorter forms**, longest first: a number with fewer decimals, an abbreviation. `SkUiTextLineBreakers.FirstFit(context => forms)` draws the text as it is when it fits, else the first form that fits, else the last form broken by `LineBreakMode`.
- **Another ellipsis:** `SkUiTextLineBreakers.WithEllipsis("…")` or `" (more)"`, with the label's own `LineBreakMode` and `MaxLines`.
- **Anything else**, e.g. a context-aware ellipsis such as "Alice, Bob +2": a lambda over `SkUiTextLineBreakContext`.

The context (valid during the call):

| Member | |
| --- | --- |
| `Text` | The displayed text (after `TextTransform`) |
| `AvailableWidth` | Content width in DIPs (label width minus `Padding`); infinite when measured unconstrained |
| `MaxLines`, `LineBreakMode` | The label's settings |
| `Font`, `Owner` | The primary `SKFont`; the label (`SkUiLabel` or `SkUiCoreLabel`), e.g. to read its `BindingContext` |
| `Measure(text)`, `Fits(text)` | One-line width as the label draws it: shaping, fallback fonts, character spacing |
| `Break()`, `Break(text, mode, ellipsis)` | The stock breaking, to fall back to or reuse |

Each returned string is one line, shaped as its own paragraph; lines past `MaxLines` are dropped. The breaker runs when the text, a text property or the width changes (stock modes reuse cached lines across widths; a custom breaker runs again at each new width). If it reads other state, call `InvalidateTextLayout()` when that changes. The stock instances (`SkUiTextLineBreakers.WordWrap`, …, `For(mode)`) are their `LineBreakMode`. Core labels take the same breakers (`SetLineBreaker`).

```csharp
// A distance that drops decimals before it is truncated: "384,400.12" → "384,400.1" → "384,400".
distance.LineBreaker = SkUiTextLineBreakers.FirstFit(_ =>
    Enumerable.Range(0, 3).Reverse().Select(decimals => km.ToString($"N{decimals}")));

// As many names as fit, then how many more: "Alice, Bob +2".
people.LineBreaker = context =>
{
    var names = context.Text.Split(", ");
    for (var shown = names.Length; shown > 0; shown--)
    {
        var candidate = string.Join(", ", names[..shown]) + (shown < names.Length ? $" +{names.Length - shown}" : "");
        if (context.Fits(candidate))
            return [candidate];
    }
    return context.Break(); // not even one name: the label's LineBreakMode
};
```

The samples app shows these side by side (**Controls › Text that fits**).

## Formatted text (spans)

`FormattedText` takes MAUI's own `FormattedString` and `Span`s, so MAUI XAML ports by changing the label's prefix only:

```xml
<sk:SkUiLabel LineBreakMode="WordWrap">
    <sk:SkUiLabel.FormattedText>
        <FormattedString>
            <Span Text="Red Bold, " TextColor="Red" FontAttributes="Bold" />
            <Span Text="here" TextColor="Blue" TextDecorations="Underline">
                <Span.GestureRecognizers>
                    <TapGestureRecognizer Command="{Binding TapCommand}" CommandParameter="https://learn.microsoft.com/dotnet/maui/" />
                </Span.GestureRecognizers>
            </Span>
            <Span Text=" italic small." FontAttributes="Italic" FontSize="12" />
        </FormattedString>
    </sk:SkUiLabel.FormattedText>
</sk:SkUiLabel>
```

- **One paragraph:** the spans' text is shaped, wrapped, truncated and aligned as one text. Bidi levels are resolved across span boundaries, so RTL and mixed text reorder as in a plain label; each span falls back from its own font for characters it lacks. Newlines inside spans start paragraphs.
- **Per span:** `FontFamily`, `FontSize`, `FontAttributes`, `TextColor`, `BackgroundColor` (behind the span's glyphs, from the font's ascent to its descent), `TextDecorations` (in the span's color, at its font's positions), `CharacterSpacing`, `LineHeight` and `TextTransform`. As on MAUI, what a span does not set is the label's: its font, size, attributes, color, decorations, spacing, line height (a span's `LineHeight` of -1) and transform (a span's `Default`). Span `Style`s and bindings work: the label parents the formatted string and hands it its binding context.
- **Line heights:** a line is as tall as its tallest span; each span's line height adds its extra space above and below that span's glyphs, as with a plain label.
- **Label settings:** `LineBreakMode`, `MaxLines`, alignment (also `Justify`), padding, direction and the rounded chrome apply. Truncation keeps the spans' styles; the ellipsis takes the style of the text it replaces. `LineBreaker` and `TextRendering` do not apply: spans are always shaped with HarfBuzz.
- **Taps:** a span's `TapGestureRecognizer`s run when that span is tapped: the command (when it can execute), then `Tapped` with the label as the sender and `GetPosition(label)` in the label's coordinates. `NumberOfTapsRequired` 1 and 2 work (primary button). A press on a tappable span takes the tap from the label's own `Tapped` and from its ancestors; a press beside it (other spans, empty space) leaves them as they are. A tap counts when it is released on the span it was pressed on. `SpanAt(point)` returns the span drawn at a point. MAUI raises a recognizer's `Tapped` only from an internal method, which SkiaUi calls through an `UnsafeAccessor` (trimming- and Native-AOT-safe): if a future MAUI renames it, `Command` still runs but `Tapped` does not fire, so prefer commands; the headless tests check it against the pinned MAUI version. A span is hit on its glyph advances on its line (as MAUI), not on a wider target.
- **Text and FormattedText:** setting `Text` clears `FormattedText`, and setting `FormattedText` clears `Text`, as on MAUI's Label.
- **Updates:** adding, removing or changing spans updates the label. A change that only repaints (colors, backgrounds, decorations) keeps the shaped lines; the rest lays the text out again.

Core labels take `SkUiCoreSpan`s with the same rules (`SetSpans`, `AddSpan`, a span's `Tapped` event); see [SkUiCore.md](SkUiCore.md#text-and-spans).

## HTML text

`TextType="Html"` (MAUI's) draws `Text` as HTML. SkiaUi parses the markup itself (`SkUiHtml`, no dependency) into styled runs drawn like spans, so the result is the same on every platform and does not need the platform's HTML importer (MAUI uses Android's `Html.fromHtml`, WebKit on iOS / Mac Catalyst and its own XHTML reader on Windows, which differ).

```xml
<sk:SkUiLabel TextType="Html" LinkTappedCommand="{Binding OpenCommand}">
    <![CDATA[
    <h3>Release notes</h3>
    <p>Now with <b>bold</b>, <span style="color:red">color</span> and <a href="https://learn.microsoft.com/dotnet/maui/">a link</a>.</p>
    <ul><li>One</li><li>Two</li></ul>
    ]]>
</sk:SkUiLabel>
```

| Markup | Drawn as |
| --- | --- |
| `b`, `strong` / `i`, `em`, `cite`, `dfn` | Bold / italic (added to the label's `FontAttributes`) |
| `u`, `ins` / `s`, `strike`, `del` | Underline / strikethrough |
| `h1`–`h6` | Bold paragraph, 1.5× … 1× the label's size |
| `big`, `small`, `sub`, `sup` | 1.25×, 0.8×, 0.7×, 0.7× (no baseline shift for `sub` / `sup`) |
| `tt`, `code`, `kbd`, `pre` | The platform's monospace font; `pre` keeps whitespace and line breaks |
| `font color face size` | Color, family, HTML size 1–7 (or `+1` / `-1`) |
| `style="…"` on any element | `color`, `background-color`, `font-size` (px, pt, em, %, keywords), `font-weight`, `font-style`, `font-family`, `text-decoration` |
| `p`, `div`, `blockquote`, `br`, `hr` | Paragraphs (one line break between blocks, none at the edges) and line breaks |
| `ul`, `ol`, `li` | "• " bullets, "1. " numbers, nested lists indented |
| `a href` | Link: accent color, underline, tappable |
| `img`, `script`, `style`, comments | Not shown; other tags show their content |

- Whitespace collapses as in HTML and entities are decoded (`&amp;`, `&nbsp;`, `&#x1F600;`). Broken markup (unclosed, stray or misnested tags) is read leniently and never throws.
- The label's font, size, attributes, color, decorations, character spacing, line height, alignment, `LineBreakMode`, `MaxLines`, padding and chrome are the defaults the markup overrides. `TextTransform`, `LineBreaker` and `TextRendering` do not apply. `FormattedText` wins over `TextType`, as on MAUI.
- **Links:** a tap on an `<a href>` raises `LinkTapped` (`Href`, `Position`) and runs `LinkTappedCommand` with the href; nothing opens by itself (MAUI's HTML labels do not make links tappable at all). `LinkAt(point)` returns the href at a point. A press on a link takes the tap from the label and its ancestors.
- To change the result, convert it to spans yourself: `SkUiHtml.ToFormattedString(html, fontSize, href => …)` (MAUI spans, links with tap recognizers) or `SkUiHtml.ToCoreSpans(...)`.
- Core labels: `SetTextType(TextType.Html)` and the `LinkTapped` event.

## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

```xml
<sk:SkUiLabel Text="Oceans cover about 71 percent of Earth."
              FontSize="17" TextColor="#202A2C"
              LineBreakMode="WordWrap" Padding="0,8" />
```

```csharp
label.SetText("Hello").SetFontFamily("OpenSansRegular").SetFontSize(20);
```

Two lines with an ellipsis, spaced and underlined (MAUI's markup, prefix changed):

```xml
<sk:SkUiLabel Text="{Binding Description}" LineBreakMode="TailTruncation" MaxLines="2"
              LineHeight="1.2" CharacterSpacing="0.5" TextDecorations="Underline" TextTransform="Uppercase" />
```

A badge:

```xml
<sk:SkUiLabel Text="12" FontSize="12" TextColor="White" BackgroundColor="#C62828"
              CornerRadius="100" Padding="8,2" HorizontalOptions="Start" />
```

A tab with rounded top corners (`CornerRadii` order: top-left, top-right, bottom-left, bottom-right):

```xml
<sk:SkUiLabel Text="Details" BackgroundColor="#E0F2F1" CornerRadii="12,12,0,0" Padding="16,8" />
```

Fonts registered with MAUI's `ConfigureFonts` (`fonts.AddFont("file.ttf", "Alias")`, as `MauiFont` items or embedded resources) work by their alias, on every platform. `SkUiFonts.Register(alias, openStream)` registers a font for drawn text only, or overrides one.

## Key properties

`Text`, `FormattedText`, `TextType`, `LinkTappedCommand`, `TextColor`, `FontSize`, `FontAutoScalingEnabled`, `FontFamily`, `FontAttributes`, `LineBreakMode`, `MaxLines`, `LineHeight`, `CharacterSpacing`, `TextDecorations`, `TextTransform`, `LineBreaker`, `HorizontalTextAlignment`, `VerticalTextAlignment`, `TextRendering`, `Padding`, `CornerRadii`, `CornerRadius`, `BorderColor`, `BorderWidth` (+ matching `Set*` setters); `InvalidateTextLayout()`, `SpanAt(point)`, `LinkAt(point)`; `LinkTapped`.

## Differences from MAUI Label

| Topic | SkiaUi |
| --- | --- |
| Spans (`FormattedText`) | Supported, with span tap recognizers. Spans ignore `LineBreaker` and `TextRendering` (always shaped). Only `TapGestureRecognizer` on spans (as MAUI); `TappedEventArgs.GetPosition` answers for the label only |
| HTML (`TextType="Html"`) | Parsed by SkiaUi (the Android tag subset, plus inline `style`), the same on every platform; links are tappable (`LinkTapped`); no images, `sub` / `sup` baseline shift or block indents |
| `<sk:SkUiLabel>text</sk:SkUiLabel>` | `Text` is the content property, as on MAUI |
| Span decorations | A span without its own `TextDecorations` takes the label's (as MAUI on Android; iOS draws none) |
| `HorizontalTextAlignment="Justify"` | Justified on every platform (MAUI's Android Label ignores it) |
| `FontAttributes` without a bold / italic face | Synthetic bold and slant on every platform (MAUI synthesizes on Android only) |
| `CharacterSpacing` | DIPs at every font size (as iOS); Android and Windows scale MAUI's value with the font size (1/16 em), so they match at 16 DIPs |
| `LineHeight` | Extra space split above and below each line (iOS adds it above, Android below) |
| `MaxLines` with `HeadTruncation` / `MiddleTruncation` / `NoWrap` | One line per paragraph, then at most `MaxLines` lines (MAUI shows a single line) |
| Custom line breaking | `LineBreaker` (MAUI has none) |
| Bidi / complex scripts | Supported (HarfBuzz + UAX #9 implicit levels). Explicit embedding / isolate control characters (LRE…PDI) are treated as neutral; LRM / RLM / ALM work |
| Fallback fonts | Chosen by Skia's font manager per character; may differ from the native text stack's choice (e.g. a different Hebrew face on iOS) |
| Selection / copy | Not supported |
| Fonts | System names, `ConfigureFonts` aliases or `SkUiFonts.Register`, through SkiaUi's text engine (not MAUI's font manager) |
| System text size | Followed as MAUI's (`FontAutoScalingEnabled`, default `true`; spans and HTML sizes too): Android's font scale (non-linear on Android 14+), Dynamic Type, Windows text scaling (applied linearly); `SkUiLook.FontScale` prescales all text app-wide, also with auto scaling off |
| Accessibility | Read as text (its `Text`, the spans' or the HTML's text); a heading with `SemanticProperties.HeadingLevel`; links inside are read as part of the text, not as their own elements |
| Gestures | Opt-in `Tapped` / `TappedCommand` only |
| Rounded background / border | Built in (`CornerRadii` / `CornerRadius`, `BorderColor`, `BorderWidth`); MAUI needs a `Border` around the label |

## Related

Gallery: `LabelDemoPage`
