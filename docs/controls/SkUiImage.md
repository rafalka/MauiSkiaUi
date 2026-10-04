# SkUiImage

Asynchronously loaded, Skia-drawn image with MAUI's sources and aspect modes, a shared memory and disk cache, and FFImageLoading-style transformations.

**MAUI counterpart:** [`Image`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/image)

## How it works

Changing `Source` (or a property of the source object) starts a load through the image loader that both layers share:

1. **Memory cache.** If the same source with the same decode size and transformations was decoded before, the view shows it **synchronously**: no empty frame and no decode. Views of one source share one decoded image.
2. **Shared load.** Views that ask for the same image at the same time share one download and one decode. The work is cancelled only when every one of them has let go (source changed or cleared).
3. **Source.** The bytes come from the disk cache (downloads), the network, the app package, a file or a stream. Font glyphs are drawn through the text engine.
4. **Decode** happens off the UI thread, a few images at a time. EXIF orientation is applied. Large images are decoded at reduced size (`SkUiImageDecoder.MaxDecodeDimension`, `DownsampleWidth` / `DownsampleHeight`). Animated GIF / WebP decode to all their frames.
5. **Transformations** run off the UI thread. The result goes into the memory cache.

Completion is applied on the dispatcher that started the load. Errors set `LoadError` and show the `ErrorPlaceholder` (else nothing). While loading, the previous image is cleared and the `LoadingPlaceholder` shows. Drawing goes through `SkUiLook.Current.DrawImage`, the same path as `SkUiCoreImage`. `Aspect.Center` draws the image unscaled (one intrinsic DIP per DIP) in the middle, clipped to the bounds, also when the decoder reduced it.

**Intrinsic size** (`ImageSize`, used by measure) is in DIPs: source pixels divided by the source's density. A `MauiImage` lays out at its base size, as in MAUI. Other bitmaps use one pixel per DIP, and a font glyph uses its font size. Downsampling, the decode limit and EXIF do not change it (a quarter-turn EXIF orientation swaps width and height). Transformations that change the pixel size do: a circle crop of a 40 × 20 image is 20 × 20.

## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`), not MAUI `GestureRecognizers`. See [EventMechanism.md](../design/EventMechanism.md).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).

## How to use

MAUI markup works with only the prefix changed:

```xml
<sk:SkUiImage Source="dotnet_bot.png" Aspect="AspectFit" />                <!-- MauiImage -->
<sk:SkUiImage Source="https://aka.ms/campus.jpg" />                       <!-- download, disk-cached -->
<sk:SkUiImage Source="spinner.gif" IsAnimationPlaying="True" />
<sk:SkUiImage>
  <sk:SkUiImage.Source>
    <FontImageSource FontFamily="MaterialSymbols" Glyph="&#xe88a;" Size="24" Color="Black" />
  </sk:SkUiImage.Source>
</sk:SkUiImage>
```

Avatars in a list: a small decode, circle-cropped, shared by every row that shows the same URL:

```xml
<sk:SkUiImage Source="{Binding AvatarUrl}" WidthRequest="48" HeightRequest="48"
              DownsampleWidth="48">
  <sk:SkUiImage.Transformations>
    <sk:SkUiCircleTransformation BorderWidth="2" BorderColor="White" />
  </sk:SkUiImage.Transformations>
</sk:SkUiImage>
```

```csharp
await image.LoadingTask; // wait for success or error publication
```

## Sources

| Source | Resolves to |
| --- | --- |
| `FileImageSource`, relative (`"dotnet_bot.png"`) | The **`MauiImage`** Resizetizer made for the display density: an Android drawable (density bucket), iOS / Mac Catalyst `name@3x.png` / `@2x` / `name.png`, Windows `name.scale-NNN.png`. An SVG item is referenced by its generated `.png` name, as in MAUI. If there is no MauiImage of that name, a **`MauiAsset`** in Resources/Raw. |
| `FileImageSource`, absolute path | The file. |
| `UriImageSource` (HTTP(S)) | A download. With `CachingEnabled` (default) the bytes go into the disk cache for `CacheValidity` (default 1 day). Plain `http://` needs the platform's cleartext permission (Android `usesCleartextTraffic` / network security config, iOS App Transport Security). |
| `StreamImageSource` (`ImageSource.FromStream`, `FromResource`) | The stream's bytes. Not cached: the factory may return different content each time. |
| `FontImageSource` | The glyph, drawn through the text engine: fonts from `ConfigureFonts` / `SkUiFonts`, ligatures (icon names), per-character fallback. The image is the text's advance wide and its line high, rasterized at the display density, in `Color` (white when unset, as MAUI). Synchronous. |

## Placeholders and load events

```xml
<sk:SkUiImage Source="{Binding PhotoUrl}" LoadingPlaceholder="photo_loading.png" ErrorPlaceholder="photo_broken.png" />
```

- **`LoadingPlaceholder`** shows while `Source` loads; **`ErrorPlaceholder`** after it failed. Both take any source
  kind and load through the same cache, so a `MauiImage` or a `FontImageSource` glyph shows at once. A source that
  is already in the memory cache shows without its loading placeholder appearing.
- While a placeholder shows, the view measures as the placeholder (an image without a size request does not
  collapse); `ImageSize` stays the image's own size (zero until it loads), and `IsShowingPlaceholder` is true.
- **`TransformPlaceholders`** (default `true`): placeholders get the same `Transformations` and downsampling, so a
  circle avatar's placeholder is a circle too.
- Animated placeholders (a spinner GIF) always play, whatever `IsAnimationPlaying` says.

**`LoadingStarted`** and **`LoadingFinished`** report every load of `Source`, also memory-cache hits (both raised
at once):

```csharp
image.LoadingFinished += (_, e) =>
{
    if (e.IsSuccess) Log($"{e.Source} from {e.Origin} in {e.Elapsed.TotalMilliseconds} ms");
    else if (e.Status == SkUiImageLoadStatus.Failed) Log($"{e.Source} failed: {e.Error}");
};
```

| `SkUiImageLoadFinishedEventArgs` | Meaning |
| --- | --- |
| `Status` / `IsSuccess` | `Succeeded`, `Failed`, or `Cancelled` (the source changed or was cleared before the load finished) |
| `Origin` | Where a successful image came from: `MemoryCache`, `DiskCache`, `Network`, `MauiImage`, `PackageAsset`, `File`, `Stream`, `Font`. A view that joined another view's load of the same image reports that load's origin |
| `Error` | Why it failed |
| `ImageSize`, `Elapsed`, `Source` | The image's intrinsic size, the load time, and the `SkUiImageSource` that was loaded (SkUi* views convert MAUI's `ImageSource`; read `image.Source` for the MAUI object) |

- **Pairing:** every `LoadingStarted` gets exactly one `LoadingFinished`.
- **Timing:** both are raised on the UI thread. `LoadingFinished` comes after `IsLoading`, `LoadError` and
  `ImageSize` are updated.
- **Scope:** placeholder loads raise no events.

## Caching

`SkUiImageCache` configures the caches shared by every image, image button and slider thumb on both layers:

| Member | Meaning |
| --- | --- |
| `MemoryCacheMaxBytes` | Budget for decoded images (4 bytes per pixel). Default: an eighth of the memory available to the process, between 32 and 256 MiB. Least recently used images go first. An image still shown stays alive until its views let go. |
| `MemoryCacheBytes`, `MemoryCacheCount`, `ClearMemoryCache()` | Current use; empty the cache. It is also cleared when the OS reports memory pressure (Android `onTrimMemory`, iOS / Mac Catalyst memory warnings). |
| `ClearAsync(caches)` | Empty the memory cache, the disk cache or both (default), e.g. for a "Clear cache" setting or at sign-out. Views keep their images; the next load decodes and downloads again. |
| `GetDiskCacheBytesAsync()` | Size of the downloaded files (to show the cache size). |
| `DiskCacheDirectory`, `DiskCacheMaxBytes` (100 MiB), `ClearDiskCacheAsync()` | Download cache in `FileSystem.CacheDirectory/SkiaUi.Images`. One file per URI, kept only when it decodes (an error page or a corrupt response is not cached; a cached file that no longer decodes is dropped). The oldest files are removed past the budget. Clearing deletes only the cache's own files (`*.img`), so the folder may be shared. |
| `RemoveAsync(Uri)` / `RemoveAsync(SkUiImageSource)` | Forget one source (all its decode sizes and transformations, and its download), so the next load fetches it again. Call `ReloadAsync()` on views that should refresh. |
| `CacheKeyFactory` | App-wide cache identity: a source → key delegate, `null` for the default key ([below](#custom-cache-keys)). |
| `HttpClient` | The client that downloads images (set one with your headers, handler or timeout). |

Per view: `CacheType` (`All`, `Memory`, `Disk`, `None`, as FFImageLoading's). For a URI, `UriImageSource.CachingEnabled = false` also skips the disk.

### Custom cache keys

By default a source is cached by its URI, file path, stream key or glyph settings. When one image is reachable
through URLs that differ in an access token, a signature or an expiry, set an app-wide key factory at startup:

```csharp
// MauiProgram: the same image whatever its token or signature
SkUiImageCache.CacheKeyFactory = SkUiImageCacheKeys.IgnoreQueryParameters("token", "sig", "expires");

// or your own rule; return null to keep the default key
SkUiImageCache.CacheKeyFactory = source => source switch
{
    SkUiUriImageSource { Uri.Host: "cdn.example.com" } uri => SkUiImageCacheKeys.WithoutQueryParameters(uri.Uri),
    SkUiStreamImageSource => null, // e.g. look the user's id up to cache avatars from streams
    _ => null
};
```

- Sources with the same key share the decoded image in memory (per decode size and transformations) and the
  downloaded file on disk. Images are still downloaded from the source's own URL.
- **Make sure the URLs are one image.** A shared key means one file for all of them: whichever URL downloaded it
  first is what the others show, and `CacheValidity` counts from when that file was written, not per URL. Give CDN
  variants that really differ (sizes, formats, crops) their own keys, e.g. keep the query parameters that select them.
- A key for a stream source makes it cacheable, which otherwise it is not.
- The factory sees SkiaUi's source records (`SkUiUriImageSource`, `SkUiFileImageSource`, `SkUiStreamImageSource`,
  `SkUiFontImageSource`), on both layers: MAUI's `ImageSource` is converted first.
- It runs for every load on the thread that starts it (usually the UI thread): keep it fast and thread-safe. An
  exception fails the load with a `LoadError`.
- Images cached before the factory changed keep their old keys: set it before images load.
- `RemoveAsync` removes by key, so it forgets every URL that shares it.

## Transformations

`Transformations` is a list per view, applied in order to the decoded image before it is cached. Adding or removing items reloads. Stock transformations (sizes in the image's DIPs):

| Transformation | Effect |
| --- | --- |
| `SkUiCircleTransformation` | Centered square, clipped to a circle; optional `BorderWidth` / `BorderColor` inside it |
| `SkUiRoundedTransformation` | Per-corner `CornerRadius`; optional `AspectRatio` crop and border |
| `SkUiCropTransformation` | `AspectRatio`, `ZoomFactor`, `OffsetX` / `OffsetY` (-1 … 1) |
| `SkUiFlipTransformation` | `Horizontal` / `Vertical` mirror |
| `SkUiRotateTransformation` | `Degrees` clockwise; the image grows to hold the rotated pixels |
| `SkUiBlurTransformation` | Gaussian `Radius`, edges clamped |
| `SkUiTintTransformation` | `Color` with a `BlendMode` (default `SrcIn`: recolors monochrome icons) |
| `SkUiColorMatrixTransformation`, `SkUiGrayscaleTransformation`, `SkUiSepiaTransformation` | 4 × 5 color matrix |

Write your own by implementing `ISkUiImageTransformation` (`Key` + `Transform(SKImage, pixelsPerDip)`), or deriving from `SkUiImageTransformation` for the raster-canvas helper. The `Key` must identify every setting: it is part of the cache key. Settings are read when a load starts, so changing a property of a transformation in use applies on the next load: the stock ones are copied for each load. A custom transformation is used as it is: keep its settings fixed while it is in use, or replace it; if its `Key` changes during a load, that result is shown but not cached. A load that is cancelled stops between frames and transformations.

Transformations change the decoded pixels once. For clipping that follows the layout (rounded corners at the view's size), use `SkUiImageButton`'s `CornerRadii` or a `SkUiBorder` instead.

## Key properties

`Source`, `Aspect`, `IsAnimationPlaying`, `Transformations`, `CacheType`, `DownsampleWidth`, `DownsampleHeight`, `LoadingPlaceholder`, `ErrorPlaceholder`, `TransformPlaceholders`, `IsLoading`, `LoadError`, `ImageSize`, `IsShowingPlaceholder`, `LoadingTask`, `ReloadAsync()`. Events: `LoadingStarted`, `LoadingFinished`. Fluent: `SetSource`, `SetAspect`, `SetIsAnimationPlaying`, `SetTransformations(...)`, `SetCacheType`, `SetDownsample(width, height)`, `SetPlaceholders(loading, error)`, `SetTransformPlaceholders`.

`DownsampleWidth` / `DownsampleHeight` are DIPs at the display density (FFImageLoading's with `DownsampleUseDipUnits`). The image is decoded no larger than that, aspect kept, never upscaled. Use them for thumbnails of large photos.

Animated GIF / WebP play on the view's UI clock while `IsAnimationPlaying` is true, re-recording only this image when its frame changes. Paused, the current frame stays. An animation whose frames would take more than `SkUiImageDecoder.MaxAnimationBytes` (64 MiB) decodes to its first frame and draws still.

## Differences from MAUI Image

| Topic | SkiaUi |
| --- | --- |
| Caching | Memory cache of decoded images shared by all views; download cache with MAUI's `CachingEnabled` / `CacheValidity`; plus `CacheType` per view |
| Transformations, downsampling, placeholders, load events | SkiaUi extensions (from FFImageLoading); MAUI has only `IsLoading` |
| `MauiImage` lookup | The file Resizetizer generated for the display density; Android vector (XML) drawables are not decoded |
| `FontImageSource.FontAutoScalingEnabled` | As MAUI's: the glyph follows the system text size (default `true`). The size is fixed when the source is set: after a change of the system text size or `SkUiLook.FontScale`, images already shown keep their size (labels re-measure); set the source again to draw the glyph at the new size |
| Screen readers | An image is read only with `SemanticProperties.Description` (or `AutomationProperties.IsInAccessibleTree="True"`); an image button is a button named by its description |
| `IsOpaque` | Not available (no effect on drawn images) |
| SVG at runtime | Not yet: an SVG `MauiImage` works (as the PNG generated at build time); drawing `.svg` files at runtime is planned ([ImplementationPlan.md](../design/ImplementationPlan.md)) |
| Size limits | 32 MiB encoded; decoded edge ≤ `MaxDecodeDimension` (2048 px) |
| Disposal | Not needed (images are not `IDisposable`): a collected image releases its reference on the shared cache by itself; set `Source` to `null` (Core: `Clear()`) to release it at once |

## Related

[`SkUiImageButton`](SkUiImageButton.md), [`SkUiSlider`](SkUiSlider.md) (`ThumbImageSource`), Core: [`SkUiCoreImage`](SkUiCore.md). Gallery: `ImageDemoPage` (every source kind, animation, transformations, cache).
