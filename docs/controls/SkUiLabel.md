# SkUiLabel

Drawn plain text with wrapping, truncation, fonts, alignment, and padding.

**MAUI counterpart:** [`Label`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/label)

## How it works

Skia text layout paints into the arranged bounds. Measure accounts for padding and line-break mode. Passive by default (does not consume taps unless `Tapped` / `TappedCommand` is set).


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** bindables call the direct setter. Direct setters **do not** write back to the bindable store (intentional FR-10 desync). Prefer one update path per property.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's own tap model (`Tapped` / `TappedCommand`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../../EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../../LayoutSystem.md).


## How to use

```xml
<sk:SkUiLabel Text="Oceans cover about 71 percent of Earth."
              FontSize="17" TextColor="#202A2C"
              LineBreakMode="WordWrap" Padding="0,8" />
```

```csharp
label.SetText("Hello").SetFontFamily("OpenSansRegular").SetFontSize(20);
```

Register app-embedded fonts with `SkUiFonts.Register` (MAUI font aliases alone are not enough for Skia).

## Key properties

`Text`, `TextColor`, `FontSize`, `FontFamily`, `FontAttributes`, `LineBreakMode`, `HorizontalTextAlignment`, `VerticalTextAlignment`, `Padding` (+ matching `Set*` setters).

## Differences from MAUI Label

| Topic | SkiaUi |
| --- | --- |
| Rich text / spans | Not supported |
| Bidi / complex scripts | Not supported |
| Selection / copy | Not supported |
| Fonts | System names or `SkUiFonts.Register`; not full MAUI font scaling pipeline |
| Gestures | Opt-in `Tapped` / `TappedCommand` only |
| Rounded border | Compose with [`SkUiBorder`](SkUiBorder.md) (Phase 3 backlog also mentions Label+border) |

## Related

Gallery: `LabelDemoPage`
