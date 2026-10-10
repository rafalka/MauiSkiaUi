# SkUiIndicatorView

A drawn row of indicators, one per item, with the current one selected: the dots under a carousel or a paged onboarding flow.

**MAUI counterpart:** `IndicatorView`. Its XAML ports by changing the prefix, except `IndicatorTemplate` (see below). Beyond MAUI: `IndicatorSpacing`, `Orientation` (vertical rows for vertical carousels), a selection that slides with a linked carousel's scrolling, and drawing by the look. Core twin: `SkUiCoreIndicatorView` (fluent setters, the same drawing and taps).

## How it works

- **Items and selection.** `Count` indicators (or as many as `ItemsSource` has, following its changes; listened to weakly; 0 once it is cleared), `Position` (two-way) selected. `Position` stays within the items: beyond the last it becomes the last, also when `Count` drops (MAUI's keeps it); set before there are items, it is kept for them. With more items than `MaximumVisible`, a window of them around the selected one shows; with a looping carousel the window wraps too (the last items and the first show together across the wrap). `HideSingle` (default `true`) draws nothing for a single item; the view keeps its size.
- **Linked to a carousel.** Set the carousel's `IndicatorView` ([SkUiCarouselView.md](SkUiCarouselView.md)): the carousel sets `Count` and `Position` (an `ItemsSource` of the indicator's own counts again once it is unlinked), the drawn selection follows its scrolling (halfway between two dots halfway through a swipe, and across the wrap of a looping carousel from the last dot to the first), and a tap on a dot scrolls the carousel there. A vertical carousel makes the row vertical.
- **On its own.** A tap selects the nearest indicator (anywhere in the view: give it a height or padding for a larger touch target), and the selection moves there with the look's `IndicatorPosition` transition (250 ms in the default look; none with reduced motion).
- **Layout.** The row is `IndicatorSize` thick (default 6, as MAUI's) and as long as its indicators and the gaps between them (`IndicatorSpacing`; negative, the default: the look's, 8), plus what the look adds to the selected one (a pill). It is centered in the view; right-to-left layouts mirror it.
- **Look.** `IndicatorColor` (default: the color scheme's `TrackOff`) and `SelectedIndicatorColor` (default: the accent), `IndicatorsShape` (`Circle` or `Square`). Drawing is the look's (below).
- **Keyboard and screen readers.** An adjustable element (a slider role with the position among the items): arrow keys, Home and End, and screen readers' increment and decrement move the selection.

Not available: MAUI's `IndicatorTemplate` and `IndicatorLayout` (one template view per indicator). Draw other indicators through the look instead: one painter draws the whole row, cheaply, also while the selection slides.

## Custom indicators

The current look ([ControlLook.md](../design/ControlLook.md)) draws every indicator view:

- `DefaultSkUiLook.IndicatorStyle`: `Dots` (default; the selected dot takes the selected color, blended while the selection moves) or `Pill` (the selected indicator stretches into a pill two sizes longer that slides between the dots, as Material's). Call `SkUiLook.NotifyChanged()` after changing it.
- `SkUiLook.DrawIndicators(canvas, SkUiIndicatorPaint)`, or the `IndicatorPainter` delegate, draws the row. The paint struct has the row's `Bounds`, the visible `Count` and the `First` item shown, the fractional `Position`, the sizes, shape, colors and orientation, and helpers that lay the row out exactly as the control hit-tests it: `GetIndicatorBounds(slot)`, `GetSelection(slot)` (0–1, shared by two neighbors while the selection moves) and `GetColor(slot)`. It is drawn again while the selection moves, so keep it cheap.
- `GetSelectedIndicatorExtraLength(size, shape)`: how much longer the selected indicator is (the control lays the row out with it); `DefaultIndicatorSpacing`; `GetTransition(SkUiTransitionKind.IndicatorPosition)`.

```csharp
public sealed class BarsLook : DefaultSkUiLook
{
    public override double GetSelectedIndicatorExtraLength(double indicatorSize, IndicatorShape shape) => 3 * indicatorSize;

    protected override void DrawIndicatorsCore(SKCanvas canvas, SkUiIndicatorPaint paint)
    {
        using var fill = new SKPaint { IsAntialias = true };
        for (var slot = 0; slot < paint.Count; slot++)
        {
            fill.Color = paint.GetColor(slot);
            var bar = paint.GetIndicatorBounds(slot);
            bar.Inset(0, bar.Height / 3); // thin bars, the selected one long
            canvas.DrawRoundRect(bar, bar.Height / 2, bar.Height / 2, fill);
        }
    }
}
```

## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`dots.SetCount(5).SetIndicatorSize(8)`). Getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10).
- **Gestures** use SkiaUi's gesture arena. See [EventMechanism.md](../design/EventMechanism.md).

## How to use

```xml
<sk:SkUiCarouselView ItemsSource="{Binding Pages}" IndicatorView="{x:Reference Dots}" … />
<sk:SkUiIndicatorView x:Name="Dots" HorizontalOptions="Center" Margin="0,8"
                      IndicatorColor="LightGray" SelectedIndicatorColor="DarkSlateGray" IndicatorSize="8" />
```

On its own, bound to a pager of your own:

```xml
<sk:SkUiIndicatorView Count="{Binding PageCount}" Position="{Binding PageIndex}" MaximumVisible="7" />
```

## Key properties

| Property | Default | Notes |
| --- | --- | --- |
| `Position` | `0` | Two-way; a tap sets it |
| `Count` / `ItemsSource` | `0` / `null` | `ItemsSource` sets `Count`; a linked carousel sets both |
| `IndicatorColor` / `SelectedIndicatorColor` | scheme `TrackOff` / accent | Snapshot of the color scheme when created |
| `IndicatorSize` | `6` | Thickness (and length) of an indicator |
| `IndicatorsShape` | `Circle` | Or `Square` |
| `MaximumVisible` | all | A window around the selected one |
| `HideSingle` | `true` | Nothing drawn for one item |
| `IndicatorSpacing` | `-1` | Gap; negative: the look's (8) (SkiaUi) |
| `Orientation` | `Horizontal` | `StackOrientation` (SkiaUi) |
