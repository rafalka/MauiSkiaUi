# SkUiProgressRing

Circular progress indicator: an arc of the completed fraction over a track around a circle, with optional content in the middle (the remaining hours, a percentage, an icon). Core twin: `SkUiCoreProgressRing`.

**MAUI counterpart:** none (MAUI has `ProgressBar` and `ActivityIndicator`).

## How it works

- **Determinate:** the arc covers `Progress` (0–1) of the circle from `StartAngle` (degrees clockwise from 3 o'clock; `-90`, the top, by default), clockwise unless `IsClockwise` is `false`. Right-to-left layouts do not change the direction (a clock face reads the same either way).
- **Indeterminate:** `IsIndeterminate="True"` draws the track and one arc of the look's `IndeterminateProgressRingSweep`, and the compositor turns the recorded picture about the centre on the render thread (`IndeterminateProgressRingPeriod`), as [`SkUiActivityIndicator`](SkUiActivityIndicator.md) spins: no UI-thread work and no re-recording per frame. The content does not turn, but the ring's own `Background` does: put a background on a border around the ring instead. While indeterminate, `Progress` changes record nothing.
- **Content:** the ring is a content view (`Content`, `ContentTemplate`, `Padding`): the content is laid out in the square inscribed in the inner circle, less `Padding`. The ring measures as the look's `DefaultProgressRingSize`, or larger when the content's square needs a larger inner circle; with a fixed size, the content gets the square that fits.
- **Drawing:** `SkUiLook.Current.DrawProgressRing` with a `SkUiProgressRingPaint` (`ProgressRingPainter` replaces it). `Thickness` of `-1` takes the look's `ProgressRingThickness`; `LineCap` (MAUI's `PenLineCap`: `Round` by default, `Flat`, `Square`) shapes the arc's ends. Material 3's look leaves a gap between the arc and the track (`ProgressRingGap`); Fluent's ring is 32 epx. A look can smooth `Progress` changes with the `Progress` transition, as the bar's.
- **`ProgressTo`:** as [`SkUiProgressBar.ProgressTo`](SkUiProgressBar.md): animates `Progress` on the UI animation clock and returns `false` when replaced or stopped.
- **Semantics:** read as a progress bar with its value; set `SemanticProperties.Description` for what it measures. The content is read as its own elements.

## How to use

```xml
<sk:SkUiProgressRing Progress="{Binding DayFraction}" Thickness="10" WidthRequest="140" HeightRequest="140"
                     SemanticProperties.Description="Hours left today">
    <sk:SkUiVerticalStackLayout VerticalOptions="Center">
        <sk:SkUiLabel Text="{Binding HoursLeft, StringFormat='{0:0.#} h'}" FontSize="20" HorizontalTextAlignment="Center" />
        <sk:SkUiLabel Text="left today" FontSize="11" HorizontalTextAlignment="Center" />
    </sk:SkUiVerticalStackLayout>
</sk:SkUiProgressRing>
```

```csharp
var ring = new SkUiCoreProgressRing().SetProgress(0.4).SetThickness(8);
ring.SetContent(new SkUiCoreLabel().SetText("40 %").SetHorizontalTextAlignment(TextAlignment.Center).SetVerticalTextAlignment(TextAlignment.Center));
```

## Key properties

`Progress` (clamped 0–1; NaN becomes 0), `IsIndeterminate`, `ProgressColor`, `TrackColor` (transparent: no track), `Thickness` (`-1`: the look's), `StartAngle` (`-90`), `IsClockwise` (`true`), `LineCap` (`Round`), `Content` / `Padding`, `ProgressTo(value, length, easing)`.

## Not included

A ring of several coloured parts, a donut chart and gauges belong to the charts package (F4); a bar of several parts is [`SkUiStackedBar`](SkUiStackedBar.md).

## Related

[`SkUiProgressBar`](SkUiProgressBar.md) · [`SkUiActivityIndicator`](SkUiActivityIndicator.md) · Gallery: `ProgressRingDemoPage`, `CoreProgressRingDemoPage`
