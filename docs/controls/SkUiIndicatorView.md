# SkUiIndicatorView

A drawn row of indicators, one per item, with the current one selected: the dots under a carousel or a paged onboarding flow.

**MAUI counterpart:** `IndicatorView`. Its XAML ports by changing the prefix, `IndicatorTemplate` included when the template creates drawn views ([below](#indicator-templates)). Beyond MAUI: `IndicatorSpacing`, `Orientation` (vertical rows for vertical carousels), a selection that slides with a linked carousel's scrolling, and drawing by the look. Core twin: `SkUiCoreIndicatorView` (fluent setters, the same drawing and taps).

## How it works

- **Items and selection.** `Count` indicators (or as many as `ItemsSource` has, following its changes; listened to weakly; 0 once it is cleared), `Position` (two-way) selected. `Position` stays within the items: beyond the last it becomes the last, also when `Count` drops (MAUI's keeps it); set before there are items, it is kept for them. With more items than `MaximumVisible`, a window of them around the selected one shows; with a looping carousel the window wraps too (the last items and the first show together across the wrap). `HideSingle` (default `true`) draws nothing for a single item; the view keeps its size.
- **Linked to a carousel.** Set the carousel's `IndicatorView` ([SkUiCarouselView.md](SkUiCarouselView.md)): the carousel sets `Count` and `Position` (an `ItemsSource` of the indicator's own counts again once it is unlinked), the drawn selection follows its scrolling (halfway between two dots halfway through a swipe, and across the wrap of a looping carousel from the last dot to the first), and a tap on a dot scrolls the carousel there. A vertical carousel makes the row vertical.
- **On its own.** A tap selects the nearest indicator (anywhere in the view: give it a height or padding for a larger touch target), and the selection moves there with the look's `IndicatorPosition` transition (250 ms in the default look; none with reduced motion).
- **Layout.** The row is `IndicatorSize` thick (default 6, as MAUI's) and as long as its indicators and the gaps between them (`IndicatorSpacing`; negative, the default: the look's, 8), plus what the look adds to the selected one (a pill). It is centered in the view; right-to-left layouts mirror it.
- **Look.** `IndicatorColor` (default: the color scheme's `TrackOff`) and `SelectedIndicatorColor` (default: the accent), `IndicatorsShape` (`Circle` or `Square`). Drawing is the look's (below).
- **Keyboard and screen readers.** An adjustable element (a slider role with the position among the items): arrow keys, Home and End, and screen readers' increment and decrement move the selection.


## Custom indicators

The current look ([ControlLook.md](../design/ControlLook.md)) draws every indicator view:

- `DefaultSkUiLook.IndicatorStyle`: `Dots` (the default look's; the selected dot takes the selected color, blended while the selection moves) or `Pill` (the selected indicator stretches into a pill two sizes longer that slides between the dots, as Material's). The option's own default, `Default`, is the look's `BaseIndicatorStyle` (`Pill` in `AndroidSkUiLook`). Call `SkUiLook.NotifyChanged()` after changing it.
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

## Indicator templates

MAUI's `IndicatorTemplate` makes a view per indicator, as in MAUI: set, the indicator view shows the template's views in a stack of its own (`IndicatorLayout`) instead of drawing the row.

- **Views.** One per visible indicator (`Count`, or the items of `ItemsSource`, up to `MaximumVisible`), created from the template (a `DataTemplateSelector` chooses by item). The template must create drawn (SkUi\*) views. Each view's binding context is its item, or its index when there is no `ItemsSource` (a carousel link sets only `Count`).
- **Selection.** As MAUI marks it: each view's `BackgroundColor` is `IndicatorColor`, the current one's `SelectedIndicatorColor` (set both to `Transparent` when the template colors itself), and the current one is in the `Selected` visual state. A tap on a view selects it.
- **Layout.** A horizontal stack (vertical with `Orientation="Vertical"`), 4 DIPs apart as MAUI's, centered in the view inside `Padding`; hidden with a single indicator while `HideSingle`. `IndicatorSize`, `IndicatorsShape` and `IndicatorSpacing` are the drawn row's and do not apply.
- **`IndicatorLayout`.** The stack, set with the template; a drawn layout of the app's set instead is shown as it is (MAUI's content form), and clearing the template draws the row again.
- **Cost.** A view per indicator, and the selection jumps from one to the next (it does not slide with a carousel's scrolling). A look painter (above) draws the whole row with no views: prefer it for dots, bars and pills.

```xml
<sk:SkUiIndicatorView ItemsSource="{Binding Monkeys}" IndicatorColor="Transparent" SelectedIndicatorColor="Transparent">
    <sk:SkUiIndicatorView.IndicatorTemplate>
        <DataTemplate>
            <sk:SkUiLabel Text="★" FontSize="12">
                <VisualStateManager.VisualStateGroups>
                    <VisualStateGroup x:Name="CommonStates">
                        <VisualState x:Name="Normal">
                            <VisualState.Setters><Setter Property="TextColor" Value="LightGray" /></VisualState.Setters>
                        </VisualState>
                        <VisualState x:Name="Selected">
                            <VisualState.Setters><Setter Property="TextColor" Value="Black" /></VisualState.Setters>
                        </VisualState>
                    </VisualStateGroup>
                </VisualStateManager.VisualStateGroups>
            </sk:SkUiLabel>
        </DataTemplate>
    </sk:SkUiIndicatorView.IndicatorTemplate>
</sk:SkUiIndicatorView>
```

The Core twin has no templates (Core nodes are not templated).

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
| `Padding` | `0` | As MAUI's: space around the row, added to the measured size; the row is centered inside it |
| `IndicatorTemplate` | `null` | A drawn view per indicator ([Indicator templates](#indicator-templates)); SkUi* only |
| `IndicatorLayout` | `null` | The template's stack, or a drawn layout of the app's shown instead of the row |
