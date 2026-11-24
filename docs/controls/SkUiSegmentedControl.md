# SkUiSegmentedControl

> **Status: in progress for 1.17.** `SkUiSegmentedControl` (step S4) and `SkUiCoreSegmentedControl` (S5) are built: segments and items, selection, widths, the looks, taps, the animated marker; the keys and screen readers (S6) follow. The page is the contract for steps S2 to S6 of [ImplementationPlan-1.17.md](../design/ImplementationPlan-1.17.md#phase-s-segmented-control-and-tab-view-f1); the [review's decisions](#review-decisions) are at the end.

A row of two to about five mutually exclusive options drawn as one control, with the selected one marked: a view switch ("Day · Week · Month"), a filter ("All · Unread"), a mode ("Map · Satellite"). One selection at a time, always visible, no popup.

**MAUI counterpart:** none. MAUI has no segmented control; apps build one from a row of `RadioButton`s with a `ControlTemplate`, a `CollectionView` with single selection, or a third-party control (Syncfusion `SfSegmentedControl`, Telerik `RadSegmentedControl`). Each platform has a native one with its own look: iOS `UISegmentedControl`, Material 3 segmented buttons, WinUI's `SelectorBar` and the Windows Community Toolkit's `Segmented`. SkiaUi draws it by the look ([below](#looks)), the same on every platform, on both layers (`SkUiCoreSegmentedControl`).

## How it works

- **Segments.** `Segments` (the XAML content property) holds `SkUiSegment`s: `Text`, `Icon` (an `ImageSource`, drawn at the start of the text, or alone), `IsEnabled` and `AutomationId`. A segment is a `BindableObject` that takes the control's binding context, so its text can bind. Without segments, `ItemsSource` gives one segment per item, its text through `ItemDisplayBinding` (as the picker's, `SkUiDisplayBinding`: a path or a binding, the item's `ToString()` without one), following `INotifyCollectionChanged` sources (listened to weakly). `Segments` wins when both are set.
- **Selection.** `SelectedIndex` (two-way, default -1: none) and `SelectedItem` (two-way: the segment, or the item of `ItemsSource`) stay in step. Values outside the segments are coerced to -1 to count − 1 before they are written back, so a one-way binding is not cut (as every SkiaUi control's own writes: `WriteBack`). A tap on a segment selects it; a tap on the selected one does nothing (there is no "none" from a tap). When the segments change, the selected segment stays selected where it moved (found by reference, or by equality for items); when it was removed, the index stays within the segments.
- **Events and command.** `SelectionChanged` (`SkUiSelectedIndexChangedEventArgs`: `PreviousIndex`, `CurrentIndex`) is raised for every change, from the user or the app. `Command` (with `CommandParameter`) runs only after the user selects (a tap, a key, a screen reader's action), when it can execute: code that sets `SelectedIndex` does not run it.
- **Widths.** `SegmentWidthMode="Equal"` (default) gives every segment the width of the widest one's content; arranged wider, the control splits its width equally. `Content` gives each segment its content's width plus the look's padding; arranged wider, the extra room is shared out in proportion. Arranged narrower than its content in either mode, segments shrink (in proportion to their content in `Content` mode) and their text is truncated with an ellipsis; text does not wrap. Measured with an unbounded width it takes its content's width; `HorizontalOptions="Fill"` stretches it.
- **Selection marker.** The look draws the marker: a pill under the selected segment, a raised thumb, a check, or a bar ([looks](#looks)). When the selection changes it slides (or fades) from the old segment to the new one with the look's `SegmentSelection` transition, also across segments of different widths; under reduce motion it moves at once. The marker belongs to the control, not to a segment: segments are not views, and changing the selection re-records nothing but the control.
- **Disabled segments.** A segment with `IsEnabled="False"` is drawn dimmed, does not take taps and is skipped by the keys; the app can still select it by code. The whole control disables with `IsEnabled`.
- **Colors and text.** `TextColor`, `SelectedTextColor`, `SelectionColor` (the marker) and `TrackColor` (the track behind the segments), each `null` for the look's (from the color scheme). `CornerRadius` (-1: the look's; 0 squares the track; half the height or more makes a capsule). The font members (`FontFamily`, `FontSize`, `FontAttributes`, `FontAutoScalingEnabled`) are `ISkUiFontElement`'s, on the [shared text engine](SkUiLabel.md).
- **Keyboard.** The control is one tab stop. Left / Right select the previous / next enabled segment (mirrored right to left), Home / End the first / last; the selection does not wrap. A selection by key runs `Command`, as a tap.
- **Screen readers.** The control is a group named by its `SemanticProperties.Description` (or `AutomationProperties.Name`); each segment is a selectable element of its own with its text (or its icon's description) and its state: "Week, selected, 2 of 3". Activating a segment selects it. Disabled segments read as dimmed.
- **Right to left.** Segments run from the right; the first is at the right end.
- **Performance.** One drawn control, no view per segment: segments are measured and placed by an internal engine (`SkUiSegmentedEngine`, shared with the tab view's strip) and drawn by the look into the control's picture. Texts are laid out once per text and width (the shared text cache). Icons load through the shared image loader.

## Looks

Drawn by every look from the platform's guidelines (MAUI has no native control to measure against); sizes and transitions come from `SkUiLook`.

| Look | Track | Selected segment | Selection change | Height |
| --- | --- | --- | --- | --- |
| Default | Filled, rounded | A pill in the accent color, white text | Slides | 36 |
| iOS 26 | Capsule, light fill | A raised white thumb with a shadow, label text | Slides | 32 |
| Material 3 | None: outlined segmented buttons, dividers between them | A secondary-container fill and a check before the text | Fades | 40 |
| Material 2 | Outlined | An accent-tinted fill | Fades | 36 |
| Mac Catalyst | Rounded, light fill | A raised thumb | Slides | 28 |
| Windows | Subtle fill | An accent bar under the selected text (Fluent `SelectorBar`) | Slides | 32 |

Pressed segments take the look's press feedback; a mouse over a segment shows the look's hover state.

## How to use

```xml
<sk:SkUiSegmentedControl SelectedIndex="{Binding Range}">
    <sk:SkUiSegment Text="Day" />
    <sk:SkUiSegment Text="Week" />
    <sk:SkUiSegment Text="Month" />
</sk:SkUiSegmentedControl>

<!-- From items, sized to their texts, running a command when the user picks one. -->
<sk:SkUiSegmentedControl ItemsSource="{Binding Filters}" ItemDisplayBinding="{Binding Name}"
                         SelectedItem="{Binding Filter}" SegmentWidthMode="Content"
                         Command="{Binding ApplyFilterCommand}" />

<!-- Icons only, with descriptions for screen readers. -->
<sk:SkUiSegmentedControl SelectedIndex="0">
    <sk:SkUiSegment Icon="list.png" SemanticProperties.Description="List" />
    <sk:SkUiSegment Icon="grid.png" SemanticProperties.Description="Grid" />
</sk:SkUiSegmentedControl>
```

```csharp
var range = new SkUiSegmentedControl().SetSegments("Day", "Week", "Month").SetSelectedIndex(1);
range.SelectionChanged += (_, args) => Load(args.CurrentIndex);

var core = new SkUiCoreSegmentedControl().SetSegments("All", "Unread").SetSelectedIndex(0);
core.SelectionChanged += (_, args) => Filter(args.CurrentIndex == 1);
```

## Core

`SkUiCoreSegmentedControl` is the same control on Core nodes, on the same engine and look: fluent `Set*` with `INotifyPropertyChanged`. Its segments are `SkUiCoreSegment`s (plain objects: `Text`, `Icon`, `IsEnabled`, `AutomationId`, with change notification; Core has no `BindableObject`), or `SetSegments(params string[])`; `ItemsSource` takes its texts from `ItemDisplay` (`Func<object?, string>`, as the Core picker's). `SelectionChanged`, `Command` and the keys and semantics are the same.

## Key properties

| Property | Default | Notes |
| --- | --- | --- |
| `Segments` | empty | Content property; `SkUiSegment`: `Text`, `Icon`, `IsEnabled`, `AutomationId` |
| `ItemsSource` / `ItemDisplayBinding` | `null` | One segment per item while `Segments` is empty |
| `SelectedIndex` / `SelectedItem` | `-1` / `null` | Two-way, in step; coerced before written back |
| `SegmentWidthMode` | `Equal` | `Equal` or `Content` |
| `TextColor`, `SelectedTextColor`, `SelectionColor`, `TrackColor` | `null` | The look's |
| `CornerRadius` | `-1` | The look's |
| `FontFamily`, `FontSize`, `FontAttributes`, `FontAutoScalingEnabled` | | `ISkUiFontElement` |
| `Command` / `CommandParameter` | `null` | After a user selection only |
| `SelectionChanged` | | `SkUiSelectedIndexChangedEventArgs` (`PreviousIndex`, `CurrentIndex`), every change |

## Related

[`SkUiTabView`](SkUiTabView.md) (its strip is drawn on the same engine) · [`SkUiRadioButton`](SkUiRadioButton.md) (one of many, as a list) · [`SkUiPicker`](SkUiPicker.md) (one of many, in a popup)

## Review decisions

Reviewed and accepted on 24 November 2026, as proposed:

1. **Core segments** are `SkUiCoreSegment`, a plain class with change notification; `SkUiSegment` (a `BindableObject`) is the SkUi\* layer's.
2. **Event arguments:** one new `SkUiSelectedIndexChangedEventArgs` (`PreviousIndex`, `CurrentIndex`), shared with the tab view.
3. **Equal widths:** every segment as wide as the widest content; arranged wider, the width is split equally.
4. **No deselecting** from a tap; no empty-selection option.
5. **Keys** select at once (arrows, Home / End); no separate focus ring per segment.
