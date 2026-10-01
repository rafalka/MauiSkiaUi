# SkUiLabel

Drawn plain text with wrapping, truncation, fonts, alignment, padding and MAUI Label's text properties, with custom line breaking (shorter forms instead of an ellipsis) and optional rounded chrome for badges, chips and tags.

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
| `MaxLines` | The most lines drawn; `-1` (default) or `0`: no limit. Wrapped lines past it are dropped. With `TailTruncation` the text wraps and the last line ends with the ellipsis, as on MAUI; without `MaxLines`, tail truncation keeps one line per paragraph. Explicit newlines count as lines |
| `LineHeight` | Multiplier of the font's line spacing (`1.8`: 80 % more); `-1` (default) or `0`: the font's. The extra space is split above and below each line (CSS half-leading), so a single line stays centered in its taller box |
| `CharacterSpacing` | DIPs added after each character (grapheme, or ligature), negative for tighter; measure, wrapping and truncation count it. Plain Latin text keeps the `Auto` fast path |
| `TextDecorations` | `Underline`, `Strikethrough` or both, in the text color, at the font's positions, across each line's glyphs (all of its bidi runs). Paint-only: no re-layout |
| `TextTransform` | `Uppercase` / `Lowercase` (invariant culture, as MAUI) change the displayed text; `Text` keeps its value. `Default` / `None`: as written |

**Rounded chrome:** `CornerRadii` (per corner, MAUI's `CornerRadius` type as on [`SkUiBorder`](SkUiBorder.md)), `BorderColor` and `BorderWidth` shape the `Background` / `BackgroundColor` fill, drawn through `SkUiLook.DrawRoundedBox` like [`SkUiButton`](SkUiButton.md) (which inherits them). `CornerRadius` (an `int`, as on MAUI's `Button`) sets all four corners at once and reads the top-left one, rounded; use `CornerRadii` for fractional radii; whichever of the two is set last wins. A badge is a single label, without a wrapping [`SkUiBorder`](SkUiBorder.md), so it costs no extra layout node or picture. Radii larger than the label allows are scaled down, so a large uniform radius gives a pill. The border is drawn inside the bounds and `Padding` is not adjusted. With a radius, text is clipped to the rounded shape; hit bounds stay rectangular. A label with square corners and no border draws its background as before.

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

`Text`, `TextColor`, `FontSize`, `FontFamily`, `FontAttributes`, `LineBreakMode`, `MaxLines`, `LineHeight`, `CharacterSpacing`, `TextDecorations`, `TextTransform`, `LineBreaker`, `HorizontalTextAlignment`, `VerticalTextAlignment`, `TextRendering`, `Padding`, `CornerRadii`, `CornerRadius`, `BorderColor`, `BorderWidth` (+ matching `Set*` setters); `InvalidateTextLayout()`.

## Differences from MAUI Label

| Topic | SkiaUi |
| --- | --- |
| Rich text / spans | Not supported yet (`FormattedText`, P5); `TextType="Html"` is not planned |
| `HorizontalTextAlignment="Justify"` | Justified on every platform (MAUI's Android Label ignores it) |
| `FontAttributes` without a bold / italic face | Synthetic bold and slant on every platform (MAUI synthesizes on Android only) |
| `CharacterSpacing` | DIPs at every font size (as iOS); Android and Windows scale MAUI's value with the font size (1/16 em), so they match at 16 DIPs |
| `LineHeight` | Extra space split above and below each line (iOS adds it above, Android below) |
| `MaxLines` with `HeadTruncation` / `MiddleTruncation` / `NoWrap` | One line per paragraph, then at most `MaxLines` lines (MAUI shows a single line) |
| Custom line breaking | `LineBreaker` (MAUI has none) |
| Bidi / complex scripts | Supported (HarfBuzz + UAX #9 implicit levels). Explicit embedding / isolate control characters (LRE…PDI) are treated as neutral; LRM / RLM / ALM work |
| Fallback fonts | Chosen by Skia's font manager per character; may differ from the native text stack's choice (e.g. a different Hebrew face on iOS) |
| Selection / copy | Not supported |
| Fonts | System names, `ConfigureFonts` aliases or `SkUiFonts.Register`; not the MAUI font scaling pipeline |
| Gestures | Opt-in `Tapped` / `TappedCommand` only |
| Rounded background / border | Built in (`CornerRadii` / `CornerRadius`, `BorderColor`, `BorderWidth`); MAUI needs a `Border` around the label |

## Related

Gallery: `LabelDemoPage`
