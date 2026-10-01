# SkUiImage

Asynchronously decoded bitmap painted with aspect modes.

**MAUI counterpart:** [`Image`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/image)

## How it works

Changing `Source` starts `ReloadAsync`. Decode runs off the UI thread; completion is marshaled back to the starting dispatcher. Errors set `LoadError` and leave a blank image. Implements `IDisposable` for permanent teardown. Aspect destination rect is drawn via `SkUiLook.Current.DrawImage` (same path as `SkUiCoreImage`). `Aspect.Center` draws the image unscaled (one source pixel per DIP) in the middle, clipped to the bounds, also when the decoder reduced a large image.


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): both write the bindable store, and getters read it, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10).
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

```xml
<sk:SkUiImage Source="earth.jpg" Aspect="AspectFit" HeightRequest="200" />
```

```csharp
await image.LoadingTask; // wait for success or error publication
```

## Key properties

`Source`, `Aspect`, `IsLoading`, `LoadError`, `ImageSize`, `LoadingTask`, `ReloadAsync()`, `Dispose()`.

Supported sources: `FileImageSource` (absolute path or **Resources/Raw**), `StreamImageSource`, HTTP(S) `UriImageSource`. Plain `http://` needs the platform's cleartext permission (Android `usesCleartextTraffic` / network security config, iOS App Transport Security); without it the load fails with `LoadError`.

## Differences from MAUI Image

| Topic | SkiaUi |
| --- | --- |
| `MauiImage` / generated resources | Not supported — use Raw assets or streams |
| `FontImageSource` | Not supported |
| EXIF / animated images | Not supported |
| Download cache | None |
| Size limits | 32 MiB encoded, 16 megapixels decoded |
| Disposal | Call `Dispose()` when permanently removing |

## Related

Gallery: `ImageDemoPage`
