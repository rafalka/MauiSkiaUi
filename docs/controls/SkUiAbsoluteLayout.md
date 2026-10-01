# SkUiAbsoluteLayout

Absolute / proportional positioning via MAUI's `AbsoluteLayoutManager`.

**MAUI counterpart:** [`AbsoluteLayout`](https://learn.microsoft.com/dotnet/maui/user-interface/layouts/absolutelayout)

## How it works

Delegates to MAUI `AbsoluteLayout.Get/SetLayoutBounds` and `Get/SetLayoutFlags` attached properties (same pattern as Grid attached props).


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): both write the bindable store, and getters read it, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10).
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

```xml
<sk:SkUiAbsoluteLayout>
  <sk:SkUiBox Color="Teal"
              AbsoluteLayout.LayoutBounds="0.5,0.5,100,40"
              AbsoluteLayout.LayoutFlags="PositionProportional" />
</sk:SkUiAbsoluteLayout>
```

```csharp
SkUiAbsoluteLayout.SetLayoutBounds(child, new Rect(0.1, 0.2, 80, 40));
SkUiAbsoluteLayout.SetLayoutFlags(child, AbsoluteLayoutFlags.PositionProportional);
```

## Key APIs

Static `Get/SetLayoutBounds`, `Get/SetLayoutFlags`; instance `Children`, `Padding`, and batch `Add(IEnumerable<IView>)` (wraps `StartUpdating` / `EndUpdating` so many inserts invalidate once).

While `StartUpdating()` is active, adding or mutating children only marks dirty flags; measure / layout / paint notifications flush on the matching `EndUpdating()`.

## Differences from MAUI AbsoluteLayout

Children must be `ISkUiView`. Attached property APIs are the MAUI ones (via wrappers on the SkUi type). Use `Add(IEnumerable<IView>)` or an explicit update batch when inserting large child sets.

## Related

Gallery: `AbsoluteLayoutDemoPage`
