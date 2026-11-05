# Full SVG implementation plan (reference)

**Status: not scheduled.** Written when 1.7 was to be full SVG on Svg.Skia. 1.7 became [vector icons](ImplementationPlan-1.7.md) instead, because icons were the need and a small reader serves them with no dependency. This stays as the design and spike findings to start from if a real app needs gradients, `<use>`, filters or CSS in downloaded files and rich illustrations ([backlog](Roadmap.md#new-controls-and-packages)). Version numbers and the branch name below are those of that time. If taken up: re-run the spike on the current Svg.Skia, and decide how the package's decoder and the 1.7 reader share the place where bytes become an image.

**Goal:** `.svg` files draw at runtime on both layers, from any image source (downloaded, from Raw assets, files, streams, text), sharp at any size and under any scale, recolored from code, in a new optional package, `SkiaUi.Maui.Svg`. A document from the network can neither reach anything else nor take the app down. `SkiaUi.Maui` gets no new dependency, and an app that does not use the package pays nothing.

**Scope:** this one feature. An SVG added as a `MauiImage` keeps working as the PNG generated at build time. Starting point: [the archived plan, phase E](../archive/ImplementationPlan-1.0.md#phase-e--svg-images-later); this plan replaces its design where the two differ.

**Baseline:** .NET MAUI 10.0.120 (or the latest 10.0.x when 1.7 ships), SkiaSharp 4.154.0, Svg.Skia 5.2.3 (what MAUI's Resizetizer uses), branch `1.7-implementation` from `devel` after 1.6.0. What has shipped is in the [changelog](../../CHANGELOG.md); the rest of the 1.x backlog stays in the [roadmap](Roadmap.md#backlog-1x).

**Rules** (as in 1.4, from [Requirements.md](Requirements.md#principles)):

- 1.x keeps the 1.0 API: changes to `SkiaUi.Maui` are additive.
- Both layers: every image view of both layers draws SVG through the one shared loader, cache and `SkUiImageSlot`.
- A feature a view does not use costs nothing per frame (`scripts/bench.sh --baseline v1.6.0`), and raster decoding gains no work.
- The engine stays generic: `SkiaUi.Maui` knows vector images and a decoder hook, not SVG. Like `SkiaUi.Maui.CommunityToolkit`, the package uses only public API, with no `InternalsVisibleTo` to it.

## What a spike found

Run on 4 November 2026 on macOS arm64 with CoreCLR, using Svg.Skia 5.2.3's `netstandard2.0` DLLs from the Resizetizer 10.0.120 package and SkiaSharp 4.154.0. It has not run on a device, with the package's `net10.0` assets, or under Native AOT, which is what [G1](#phase-g--gate-on-the-real-package) is for.

| Question | Result | What it means here |
| --- | --- | --- |
| Works with SkiaSharp 4.154.0? | Yes: paths, gradients, CSS, filters, clips, `use`, text | The package asks for SkiaSharp 4.148 and MAUI's copy references 4.150; both are below our pin, and nothing else was needed |
| Parse time | Warm: 0.1 ms for an icon, about 1 ms for a rich document. Cold, first in the process: 175–630 ms | Always off the UI thread; the cold parse is the number to measure on the S9 |
| Parsing in parallel | 64 parses on 8 threads, no errors | Fits the loader's decode queue |
| Picture lifetime | **Drawing `SKSvg.Picture` after disposing the `SKSvg` crashes the process (SIGSEGV).** A copy recorded into our own `SKPictureRecorder` survives | [S2](#phase-s--skiauimauisvg) records a copy |
| Replay cost | Rich document at 96 × 48, CPU raster: picture 27 µs, image 2 µs | Vectors cost more per frame than bitmaps; [D2](#decisions) and M1 |
| Deep nesting | **Uncatchable stack overflow.** 512 nested `<g>` on a 512 KB stack (a 4 KB file), 1,500 on 1.5 MB; 256 and 1,024 pass on those stacks | A downloaded file can kill the app: [S3](#phase-s--skiauimauisvg) |
| Remote references | **By default the parser fetches `<image>` and `<use>` URLs itself.** `SvgDocument.ResolveExternalImages` and `ResolveExternalElements` are `Local, Remote`; set to `None`, no requests | Tracking and requests to internal hosts: [D4](#decisions) |
| Entities | External entities are not resolved (default `None`); entity bombs end in an `XmlException` | Safe with the defaults, so DTDs can stay on |
| Bad input | `XmlException` for broken XML, **`NullReferenceException` for `<html/>`**, a 1 × 1 picture for an empty `<svg>` | Normalized to one error for `LoadError` |
| Intrinsic size | `viewBox` alone and percentages give the `viewBox`; no size at all gives the content's bounds; `width="100000000"` is taken as it is | Clamp for layout and rasters |
| Trimming | ILLink over the app and these DLLs: 3 warnings, all in Svg.Custom's `ColorConverter` (`TypeDescriptor`, reflection over named colors). IL 3.4 MB, 2.3 MB after linking | **Named colors may break when trimmed**: must be run, not only analyzed |
| Blocked images | A blocked `<image>` draws a broken-image placeholder (`EnableBrokenImagePlaceholders` is on) | Turned off |
| Scripts | `EnableJavaScript` is off by default | Stays off |

## Phase G — Gate on the real package

- **G1 Svg.Skia in real app builds** (first; decides whether the rest goes ahead as planned). Size: S, device time.
  - A scratch branch of the samples app with the real `Svg.Skia` 5.2.3 package on the pinned SkiaSharp, drawing one document with named colors, gradients, CSS and text.
  - Builds and draws on iOS, Android, Mac Catalyst and Windows, including that the package's `HarfBuzzSharp` and SkiaSharp dependencies resolve to our pinned versions on each TFM.
  - `scripts/device_tests.sh -t maccatalyst --aot` and `--trim`: **the named colors draw correctly**. If they do not, a `DynamicDependency` or a linker descriptor in the package keeps what `ColorConverter` reflects over; if that cannot work, the package is documented as not trimmable.
  - Numbers to record: app size with and without the package on each platform; the cold first parse and the stack depth that overflows on the Galaxy S9 and the iPhone 8, on the threads the decode queue uses.
  - Also checked: the license of each dependency in 5.2.3 (Svg.Custom was MS-PL in 4.9.0; Svg.Skia is MIT).
  - Stop and rethink if it cannot run under Native AOT at all, or a cold parse takes seconds on the S9.

## Phase C — Vector images in `SkiaUi.Maui`

- **C1 Vector entries.** Size: M.
  - `SkUiDecodedImage` and `SkUiCachedImage` carry either frames or an `SKPicture` (`Vector`). The entry owns the picture and disposes it with its last reference; pictures already recorded keep their own native references, as they do with frames (checked in the spike).
  - `SkUiImageSlot.Paint` draws a vector through `SkUiImageDrawing` and a new `SkUiLook.DrawVectorImage` (a protected virtual core on the look, as `DrawImageCore`), placed by the existing `ComputeImageDestination`. It **clips to the destination**, because a nested picture's cull rect does not clip what it draws. `ImagePainter` takes an `SKImage`, so it does not run for vectors, and its doc says so.
  - Everything that reads frames learns about vectors: `SkUiImageSlot.Image` and `IsShowingPlaceholder`, the slider thumb's `hasImage`, the avatar's `ShowsImage`, `SkUiImageFade.GetDrawable` (which takes the frame as its key and treats `null` as no fade), `Frames[_frame]`, and `SkUiImageLoader.Transform`. A `HasContent` on the slot replaces the null checks.
  - Memory cost for the LRU: the larger of the picture's `ApproximateBytesUsed` and the encoded size (the approximation is low: 248 bytes for an icon).
  - Tests, with a test decoder and no SVG: each `Aspect`, a clip at the destination, sharp pixels when drawn scaled, every slot user (image, image button, button, slider thumb, avatar, placeholders, both layers), fade-in, cache sharing and eviction; leak scenario.
- **C2 Decoder hook** ([D1](#decisions)). Size: S.
  - `SkUiImageDecoder.RegisterVectorDecoder(ISkUiVectorImageDecoder)`: `CanDecode(ReadOnlySpan<byte>)` and `Decode(byte[])`, returning the picture and its size in DIPs. Consulted only where `SKCodec.Create` returned `null`, so raster decoding gains nothing.
  - Decode bounds and transformations give a raster ([D2](#decisions)). Unlike a bitmap, a vector rasterizes **up** to the bounds (fit or cover), limited by `MaxDecodeDimension`; `Transform` rasterizes at the bounds, else at the size in DIPs × the display scale.
  - Without a registered decoder, bytes that look like XML fail with a message naming `SkiaUi.Maui.Svg` and `UseSkiaUiSvg()`, rather than "Unsupported or corrupt image".
  - Tests: file, package asset, URI (with progress and the disk cache), stream and shared in-flight loads with the test decoder; a corrupt cached file is downloaded again; keys differ by bounds.
- **C3 Source hooks.** Size: S.
  - `SkUiImageSource.WithContentFilter(string key, Func<byte[], byte[]> filter)`: bytes pass through the filter before decoding, on the decode queue, whatever the source. The key joins the cache key; the disk cache keeps the original bytes.
  - `SkUiImageSource.FromMaui` asks an `ImageSource` that implements a new `ISkUiImageSourceProvider` for its source, instead of failing with "not supported". `SkUiMauiImageSources.AffectsImage` gains one more property name that such a source raises when its image changes.
  - Tests: filtered bytes, keys, a MAUI source's change reloads each control that tracks one (image, button, slider, avatar).

## Phase S — `SkiaUi.Maui.Svg`

- **S1 Project and release wiring.** Size: S.
  - `MauiSkiaUi.Svg/` modeled on `MauiSkiaUi.CommunityToolkit` (same TFMs, trim analysis, package metadata and `ProjectReference`), `SvgSkiaVersion` in [Directory.Build.props](../../Directory.Build.props), [SkiaUi.slnx](../../SkiaUi.slnx).
  - CI: the Apple and Windows restore and build steps in [ci.yml](../../.github/workflows/ci.yml), the release loops in `nuget-pack.yml` and `nuget-publish.yml`, the note in `scripts/merge_nupkgs.py`.
  - **Before the first publish, a nuget.org Trusted Publishing policy must cover the new package, or its push fails** ([Releasing.md](../Releasing.md)). The repository owner sets this up.
  - `UseSkiaUiSvg()` on `MauiAppBuilder`, as `UseSkiaUiCommunityToolkit()`.
- **S2 Decoder.** Size: M.
  - `CanDecode`: after a BOM and white space, `<`, and an `<svg` root within the first kilobytes (past the prolog, comments and a doctype). `Decode` runs `SKSvg.FromSvg` from the bytes, **records a copy of the picture into our own recorder, then disposes the `SKSvg`**.
  - Size: `width` / `height`, else the `viewBox`, else the content's bounds; clamped to `MaxDecodeDimension` keeping its aspect ratio. A document with no size at all or an empty picture is an error, as is any parser exception: one `InvalidDataException`, shown in `LoadError`.
  - Settings: `EnableBrokenImagePlaceholders` off, `EnableJavaScript` off, `EnableTextSelectionRendering` off. `TypefaceProviders` gets one that resolves through `SkUiFonts`, so `<text>` finds fonts registered with `ConfigureFonts`.
  - Tests: the spike's documents as pixel checks; `viewBox`, percent, missing and huge sizes; text with the bundled font (Linux CI has no system fonts); first frame of an animated document.
- **S3 Untrusted documents** ([D4](#decisions)). Size: M.
  - `UseSkiaUiSvg()` sets `ResolveExternalImages` and `ResolveExternalElements` to `None`. They are process-wide statics, so this also applies to an app's own direct use of Svg.Skia, and the docs say so. `data:` images keep working (test).
  - Before parsing, a forward-only `XmlReader` pass rejects documents nested deeper than `SkUiSvg.MaxDepth` (proposed 128, below the 256 that passed on a 512 KB stack; G1's device numbers confirm it). External entities stay unresolved; DTDs stay on, because older design tools export entities in them.
  - Element depth is not the only recursion: test chains of `<use>`, `<symbol>`, `<pattern>`, `<mask>`, `<clipPath>` and gradient `href`s, and reference cycles (a direct `<use>` cycle passed). **If a chain can overflow the stack, parsing moves to one worker thread with a large stack,** which covers what a depth scan cannot.
  - Cost limit: documents over `SkUiSvg.MaxOperations` (from M1) are an error rather than a frame-time problem. The loader's 32 MiB limit on encoded bytes still applies.
  - Tests: every case above in the headless suite, except that a chain which overflows is run on the devices only after the fix; a loopback `HttpListener` asserts that no request is made (`<image>`, `<use>`, `fill="url(...)"`, CSS `@import`, entities, `file:` references).
- **S4 Sources and recoloring** ([D3](#decisions), [D5](#decisions)). Size: M.
  - A plain source needs nothing new: `Source="logo.svg"` (a `MauiAsset`), `Source="https://..."`, a file or a stream draws once the package is registered.
  - Core: `SkUiSvg.FromText(string, ...)` and `source.WithSvgOptions(tintColor, replacements)`. SkUi\*: `SkUiSvgImageSource`, a MAUI `ImageSource` usable in XAML (`Source`, `TintColor`, `Replacements`), through C3's provider.
  - `Replacements`: text replaced before parsing, through `WithContentFilter`, for `currentColor` and theme colors. `TintColor`: the whole image in one color (SrcIn), kept as a vector by wrapping the copy from S2 in a color-filtered layer. Both join the cache key, and a change reloads.
  - A MAUI `Image` does not draw `SkUiSvgImageSource`; the docs say so.
  - Tests: pixels and keys for both options; a changed tint reloads and shares entries by value; XAML loading (`RuntimeXamlCollection`); leak scenario `SvgImagesSwitched` on both layers, in `tests/Shared/MemoryLeaks`.

## Phase M — Measurements, demo and docs

- **M1 Cost of vectors.** Size: S–M, device time.
  - Bench scenarios: 1,000 SVG icons against the same icons as PNGs, on both layers (first frame, per-frame composite, recording allocation), a cold and a warm parse. They set options through `TrySet` and the project reference has an `Exists` condition, so `--baseline v1.6.0` still builds.
  - On the S9 and the iPhone 8: a scrolling list of 50 icons per screen, one complex document, and a scale animation. Compare with PNGs in frame time and memory; add gradients and blurs to [`SkUiGpuWarmUp`](../../MauiSkiaUi/Rendering/SkUiGpuWarmUp.cs) if the first draw hitches.
  - Sets `MaxOperations`, the guidance on when to rasterize, and whether [D2](#decisions) needs an automatic raster after all. Results go in [Performance.md](../Performance.md).
- **M2 Demo and docs.** Size: S.
  - A gallery page next to the image page: sources, aspects, a scale animation showing sharp edges, tint and replacements following dark mode, a bad document showing `LoadError`, on both layers.
  - New `docs/controls/SkUiSvg.md` with supported features, what degrades, and the security defaults; [SkUiImage.md](../controls/SkUiImage.md) (sources, "SVG at runtime" row), [SkUiCore.md](../controls/SkUiCore.md), the controls index; [Development.md](../../Development.md), [Releasing.md](../Releasing.md) and [README.md](../../README.md) (three packages); [Migration.md](../Migration.md) and the migration skills in `plugins/skiaui-migration` (runtime SVG views from image libraries map to `SkUiImage` with this package); a package `README.md`.

## Not in 1.7

SVG animation (animated documents draw their first frame; Svg.Skia has the pieces, which makes this a candidate for later); scripts; external resources of any kind, including relative `<image>` references to app assets; hit-testing parts of a document; screen readers reading `<title>` (an image is read from `SemanticProperties.Description`, as now); `.svgz`; a separate drawn SVG control (`SkUiImage` is the view); `SkUiSvgImageSource` on MAUI's native `Image`.

## Docs, migration and demo

Done with each step: [CHANGELOG.md](../../CHANGELOG.md) (an entry under `## Unreleased`; the core hooks and the package are one feature), [Roadmap.md](Roadmap.md); the rest as listed in [M2](#phase-m--measurements-demo-and-docs).

## Order

| # | Steps | Size | Why here |
| --- | --- | --- | --- |
| 1 | G1 | S | Decides whether the design below stands; everything else assumes it |
| 2 | C1 | M | The engine change every other step draws through; testable without SVG |
| 3 | C2, C3 | S, S | Hooks on C1 |
| 4 | S1, S2 | S, M | First real SVG on screen |
| 5 | S3 | M | **Before any network document is tried in the demo;** a release blocker |
| 6 | S4 | M | Options on S2 |
| 7 | M1 | S–M | Judges D2 and sets `MaxOperations` (S3's limit) |
| 8 | M2 | S | Documents what M1 settled |

C1 can start before G1 ends, since it has no SVG dependency.

## Decisions

- **D1 A decoder hook, not a new source type.** Every source already reads bytes, and SVG support belongs where bytes become an image, so `Source="https://.../logo.svg"` gets the disk cache, progress, shared loads and events without new code; XAML and existing properties work unchanged. The archived plan's source-type hook would have needed `SkUiImageSource` opened to other assemblies (its constructor is `private protected`, and `LoadAsync` returns an internal type). **Proposed.**
- **D2 Vector by default; a raster only when asked for.** Without decode bounds or transformations, an SVG replays as a vector: sharp at any size and under composite-time scale, one cheap op to record. `DownsampleToViewSize`, explicit bounds or a transformation give a bitmap, through options that already mean "decode to pixels", so an app can choose it for a heavy document or a long list. This narrows the archived plan's automatic raster for documents "too costly to replay": knowing the view's size needs a second load, so it comes only if M1 shows that apps cannot manage without it. **Proposed.**
- **D3 Names.** `SkiaUi.Maui.Svg`, namespace `MauiSkiaUi.Svg`, `UseSkiaUiSvg()`, `SkUiSvg`, `SkUiSvgImageSource`, `ISkUiVectorImageDecoder`, `ISkUiImageSourceProvider`, `WithContentFilter`, `WithSvgOptions`, `DrawVectorImage`. **Proposed.**
- **D4 Closed by default, with no option to open it in 1.7.** Runtime SVGs are often downloaded, and an image must not make requests or read files. An option can follow if a real app needs it. **Proposed.**
- **D5 Two ways to recolor, both on the source.** `TintColor` covers one-color icons; `Replacements` covers multi-color documents and `currentColor`. A transformation such as `SkUiTintTransformation` still works, but rasterizes (D2). **Proposed.**
- **D6 The package follows the one-version rule.** It is released with the others and depends on `SkiaUi.Maui` of the same version ([Releasing.md](../Releasing.md)). Whether 1.7 first goes out as a prerelease line (as the release process offers big features) is decided after M1. **Proposed.**

## Acceptance

- Headless suite and memory-leak scenarios pass (new: vector entries, untrusted documents, `SvgImagesSwitched`); 0 build warnings with the trim and AOT analyzers in both packages.
- `scripts/bench.sh --baseline v1.6.0` shows no regression in the existing scenarios, raster image scenarios included.
- `SkiaUi.Maui`'s dependencies are unchanged, and an app without the package restores and draws as before.
- No test document makes a network or file request, and none of the hostile documents ends the process, on the S9 and the iPhone 8 as well as headless.
- The gallery page checked on iOS, Android, Mac Catalyst and Windows; under Native AOT on Mac Catalyst (CI), named colors included; M1's numbers in [Performance.md](../Performance.md).
