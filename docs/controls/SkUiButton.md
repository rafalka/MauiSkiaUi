# SkUiButton

Drawn text button with intrinsic tap, command, rounded chrome, and visual states.

**MAUI counterpart:** [`Button`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/button)

## How it works

Extends [`SkUiLabel`](SkUiLabel.md). Background (fill, border and press feedback) is drawn by `SkUiLook.Current.DrawButton` with a `SkUiButtonPaint`; content is clipped to the same rounded path. Press feedback animates with the look's [transitions](../design/ControlLook.md#state-change-transitions-fr-26): the default look dims the fill (`PressEffect = Dim`) or spreads a ripple from the press point (`Ripple`); a quick tap still shows its full press. `HandlesTap` is true. `Command.CanExecute` gates eligibility and Disabled visual state.


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

```xml
<sk:SkUiButton Text="Add observation" Command="{Binding AddCommand}"
               FillColor="#087F83" TextColor="White" CornerRadius="6" />
```

## Image and content layout

As MAUI's Button, a button can show an image beside its text: `ImageSource` (MAUI's `ImageSource`: files, `MauiImage` resources, `FontImageSource` glyphs, URIs, streams, through the [shared image loader and cache](SkUiImage.md)) placed by `ContentLayout`, MAUI's own `Button.ButtonContentLayout` (`Position`: `Left`, `Top`, `Right`, `Bottom`; `Spacing`, default 10 DIPs). XAML takes MAUI's markup: `ContentLayout="Top"`, `"Right, 20"`, `"20"` (left, 20 DIPs).

```xml
<sk:SkUiButton Text="Settings" ImageSource="settings.png" ContentLayout="Top, 8" />
<sk:SkUiButton Text="Add" ContentLayout="Left, 6">
  <sk:SkUiButton.ImageSource>
    <FontImageSource Glyph="+" FontFamily="OpenSansRegular" Size="20" Color="White" />
  </sk:SkUiButton.ImageSource>
</sk:SkUiButton>
```

What MAUI's buttons do on every platform:
- **Size:** the image keeps its intrinsic size (DIPs) and is scaled down uniformly (never up) to fit inside the `Padding`, leaving the text the rest; the button measures image + spacing + text along the layout axis, the larger of the two across it.
- **Placement:** image and text are one group, centered in the button (SkiaUi places the group by `HorizontalTextAlignment` / `VerticalTextAlignment`, centered by default); across the axis each is centered against the other. Without text the image is centered and there is no spacing.
- **Direction:** `Left` is the start side, so in right-to-left layouts the image sits on the right (as on Android and iOS).
- **Not tinted** by `TextColor` (a `FontImageSource` has its own `Color`); drawn under the press feedback and clipped to the corner radii with the text.

`SkUiCoreButton` has `ImageSource` (an `SkUiImageSource`) and `ContentLayout` (`SetImageSource`, `SetContentLayout`), drawn by the same layout engine.

## Key properties

Inherits Label text APIs (MAUI Button's `CharacterSpacing`, `TextTransform`, `LineBreakMode`, which defaults to `NoWrap` as in MAUI: text that does not fit is cut at the padding, from its start, plus the label's `MaxLines`, `LineHeight`, `TextDecorations` and a custom `LineBreaker`) and its rounded chrome: per-corner `CornerRadii`, and `CornerRadius` as in MAUI (an `int` that sets all four corners; use `CornerRadii` for fractional radii), both defaulting to the look's `DefaultButtonCornerRadius` (`SkUiCoreButton` has only `CornerRadii`, plus `SetCornerRadius(double)` for all four); `BorderColor`, `BorderWidth`. Adds `Command`, `CommandParameter`, `Clicked`, `Pressed`, `Released`, `FillColor`, `ImageSource`, `ContentLayout`. As in MAUI, a tap raises `Pressed`, `Released`, then `Clicked`; a cancelled press (a scroll took over, the pointer left) raises `Released` without `Clicked`.

## Differences from MAUI Button

| Topic | SkiaUi |
| --- | --- |
| Image + text content | `ImageSource` + `ContentLayout` as MAUI (above); the group follows the text alignments, which MAUI's Button does not have |
| Hit region | Rectangular arranged bounds (corners outside the round fill still hit) |
| `TappedCommand` vs `Command` | On tap, only `Command` runs (plus `Clicked` / `Tapped` event). Do not rely on both commands. |
| Chrome | `FillColor`; a `Background` (solid or gradient) overrides it. Gradients reach the look as `SkUiButtonPaint.FillPaint` (`Fill` then holds the stops averaged, for looks that draw colors only); the default look dims them when pressed. An opaque fill casts the `Shadow` from the rounded chrome |
| Visual states | MAUI's: `Normal`, `PointerOver` (hover), `Pressed`, `Disabled` (also when the command cannot execute) |

## Related

Gallery: `ButtonDemoPage`
