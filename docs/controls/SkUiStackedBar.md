# SkUiStackedBar

A horizontal bar of several coloured parts side by side, each as long as its share of the whole: a leave balance (taken, scheduled, available), storage use, a budget. Core twin: `SkUiCoreStackedBar`.

**MAUI counterpart:** none (MAUI's `ProgressBar` has one value).

## How it works

- **Parts:** `Segments` is a list of `SkUiBarSegment` (`Value`, `Color`, `Label`; bindable, they take the bar's binding context). On Core, `SetSegments(...)` takes `SkUiBarValue(Value, Color, Label)` records. Negative values and NaN count as 0; a segment without a colour takes the colour scheme's accent.
- **Scale:** `Maximum` is the value the whole bar stands for; `-1` (default) is the sum of the values, so the parts fill the bar. The rest up to `Maximum` shows `TrackColor`; parts past `Maximum` are cut at the bar's end.
- **Gaps and ends:** `SegmentSpacing` is cut from the end of each part that something follows (`-1`: the look's `StackedBarSegmentSpacing`, 2, Material 3's 4); `CornerRadius` rounds the bar's two outer ends (`-1`: the look's `GetStackedBarCornerRadius`, the progress bar's).
- **Animation:** when values, parts or `Maximum` change, every part tweens from what is drawn to its new value with the look's `StackedBar` transition (250 ms in the default look); under reduce motion, or before the bar is first drawn, it jumps.
- **Drawing:** `SkUiLook.Current.DrawStackedBar` with a `SkUiStackedBarPaint` (the parts as `SkUiStackedBarPart` start / end fractions); `StackedBarPainter` replaces it. Right-to-left layouts fill from the right.
- **Semantics:** one element, read as a progress bar whose value lists the parts: "Taken 0, Scheduled 24, Available 78". Set `SemanticProperties.Description` for what it shows.

## How to use

```xml
<sk:SkUiStackedBar HeightRequest="12" SemanticProperties.Description="Leave hours">
    <sk:SkUiBarSegment Value="{Binding Taken}" Color="IndianRed" Label="Taken" />
    <sk:SkUiBarSegment Value="{Binding Scheduled}" Color="Orange" Label="Scheduled" />
    <sk:SkUiBarSegment Value="{Binding Available}" Color="SeaGreen" Label="Available" />
</sk:SkUiStackedBar>
```

```csharp
var bar = new SkUiCoreStackedBar().SetMaximum(40)
    .SetSegments(new SkUiBarValue(30, Colors.SteelBlue, "Worked"), new SkUiBarValue(4, Colors.Orange, "Overtime"));
```

A legend stays app layout: a wrap layout of dots and labels bound to the same values.

## Key properties

`Segments`, `Maximum` (`-1`: the sum), `SegmentSpacing` (`-1`: the look's), `CornerRadius` (`-1`: the look's), `TrackColor`.

## Not included

`ItemsSource` with value and colour paths (reflection paths are not trim-safe; bind each segment instead); a ring version of the same data (a donut, the charts package).

## Related

[`SkUiProgressBar`](SkUiProgressBar.md) · [`SkUiProgressRing`](SkUiProgressRing.md) · Gallery: `StackedBarDemoPage`, `CoreProgressRingDemoPage`
