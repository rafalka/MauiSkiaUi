# SkUiImageButton

Tappable image with command support and pressed/disabled tint.

**MAUI counterpart:** [`ImageButton`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/imagebutton)

## How it works

Extends [`SkUiImage`](SkUiImage.md). Intrinsic tap; `Command.CanExecute` controls eligibility. Optional `CornerRadius` clips the **tint overlay** only — the bitmap itself is not rounded-clipped in v1.


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** bindables call the direct setter. Direct setters **do not** write back to the bindable store (intentional FR-10 desync). Prefer one update path per property.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's own tap model (`Tapped` / `TappedCommand`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../../EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../../LayoutSystem.md).


## How to use

```xml
<sk:SkUiImageButton Source="earth.jpg" Command="{Binding OpenCommand}" CornerRadius="8" />
```

## Key properties

All Image APIs plus `Command`, `CommandParameter`, `Clicked`, `CornerRadius`.

## Differences from MAUI ImageButton

| Topic | SkiaUi |
| --- | --- |
| Image corner clip | Not applied to the bitmap (tint only) |
| Aspect / source limits | Same as [`SkUiImage`](SkUiImage.md) |
| Gestures | SkiaUi tap model only |

## Related

Gallery: `ImageButtonDemoPage`
