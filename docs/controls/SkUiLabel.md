# SkUiLabel

Drawn plain text with wrapping, truncation, fonts, alignment, and padding, with optional rounded chrome for badges, chips and tags.

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

**Rounded chrome:** `CornerRadii` (per corner, MAUI's `CornerRadius` type as on [`SkUiBorder`](SkUiBorder.md)), `BorderColor` and `BorderWidth` shape the `Background` / `BackgroundColor` fill, drawn through `SkUiLook.DrawRoundedBox` like [`SkUiButton`](SkUiButton.md) (which inherits them). `CornerRadius` (an `int`, as on MAUI's `Button`) sets all four corners at once and reads the top-left one, rounded; use `CornerRadii` for fractional radii; whichever of the two is set last wins. A badge is a single label, without a wrapping [`SkUiBorder`](SkUiBorder.md), so it costs no extra layout node or picture. Radii larger than the label allows are scaled down, so a large uniform radius gives a pill. The border is drawn inside the bounds and `Padding` is not adjusted. With a radius, text is clipped to the rounded shape; hit bounds stay rectangular. A label with square corners and no border draws its background as before.

**Direction:** effective `FlowDirection` RTL (explicit on the label or inherited from any ancestor) makes paragraphs RTL. `FlowDirection="LeftToRight"` forces LTR. The default (`MatchParent` under an LTR parent) lets the first strong character decide, like Android's `firstStrong`. `HorizontalTextAlignment` `Start` / `End` follow the resolved paragraph direction, so `Start` is the right edge for RTL text.


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): both write the bindable store, and getters read it, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10).
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

`Text`, `TextColor`, `FontSize`, `FontFamily`, `FontAttributes`, `LineBreakMode`, `HorizontalTextAlignment`, `VerticalTextAlignment`, `Padding`, `CornerRadii`, `CornerRadius`, `BorderColor`, `BorderWidth` (+ matching `Set*` setters).

## Differences from MAUI Label

| Topic | SkiaUi |
| --- | --- |
| Rich text / spans | Not supported |
| Bidi / complex scripts | Supported (HarfBuzz + UAX #9 implicit levels). Explicit embedding / isolate control characters (LRE…PDI) are treated as neutral; LRM / RLM / ALM work |
| Fallback fonts | Chosen by Skia's font manager per character; may differ from the native text stack's choice (e.g. a different Hebrew face on iOS) |
| Selection / copy | Not supported |
| Fonts | System names, `ConfigureFonts` aliases or `SkUiFonts.Register`; not the MAUI font scaling pipeline |
| Gestures | Opt-in `Tapped` / `TappedCommand` only |
| Rounded background / border | Built in (`CornerRadii` / `CornerRadius`, `BorderColor`, `BorderWidth`); MAUI needs a `Border` around the label |

## Related

Gallery: `LabelDemoPage`
