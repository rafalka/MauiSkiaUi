# SkiaUi 1.7 implementation plan: vector icons

**Goal:** an app's `.svg` icons draw at runtime on both layers, sharp at the size the view has and in the color the theme asks for, as cheap per frame as a bitmap. `SkiaUi.Maui` gets a small SVG reader of its own, with no new dependency and no new package.

**Scope:** this one feature, for icons. It reads the part of SVG that icons use and reports what it knows it cannot draw as a `LoadError`. A `MauiImage` keeps working exactly as today, and an app can use both. Full SVG (gradients, `<use>`, filters, downloaded illustrations) stays in the [backlog](Roadmap.md#new-controls-and-packages), with [its own plan](ImplementationPlan-FullSvg.md).

**Baseline:** .NET MAUI 10.0.120 (or the latest 10.0.x when 1.7 ships), SkiaSharp 4.154.0, branch `1.7-implementation` from `devel` after 1.6.0. What has shipped is in the [changelog](../../CHANGELOG.md); the rest of the 1.x backlog stays in the [roadmap](Roadmap.md#backlog-1x).

**Rules** (as in 1.4, from [Requirements.md](Requirements.md#principles)):

- 1.x keeps the 1.0 API: everything here is additive.
- Both layers: each member lands on the SkUi\* side and its Core twin, with one shared reader and the one image loader and cache.
- A feature a view does not use costs nothing per frame (`scripts/bench.sh --baseline v1.6.0`), and raster decoding gains no work.

## What a prototype and the reader found

Run on 4 November 2026 on macOS arm64 with CoreCLR and SkiaSharp 4.154.0, on a CPU raster surface. The coverage is from the reader itself (R1–R3, `SKUI_VECTOR_CORPUS`), the cost from a throwaway prototype of it. Each file was drawn at 3× its size and compared with Svg.Skia 5.2.3; a pixel differs when a channel is off by more than 48. These are Mac numbers: what the shipped reader costs on the phones is in [Performance.md](../Performance.md#vector-icons).

**Coverage**, with the features in [R1](#phase-r--the-reader):

| Icons | Files | Drawn | Largest difference | The rest |
| --- | --- | --- | --- | --- |
| A design tool's export for a mobile app, mostly 24 × 24, all `currentColor` | 69 | **69** | 0.04% | |
| An existing mobile app's image folder | 47 | 29 | 1.07% | 18 are 512 × 512 PNGs embedded in an SVG pattern: not vector art, so they stay `MauiImage` |
| Status badges with text as paths | 8 | 7 | 0% | 1 fills a rect with a pattern whose `<image>` has no data: re-export it |

Dashed strokes hide their errors when every stroke has one color, so those files were also compared with each stroke recolored: 0.00%. Three older exports with a fractional `pt` size differ by 14–20% of a few hundred pixels: Svg.Skia rounds their size to whole units (14.41 × 20.77 becomes 14 × 21) and the comparison stretches that, while the ink bounds agree within a pixel.

**Cost** of a 24 dp gear icon (46 path verbs) shown at 72 px:

| Approach | First icon in the process | Each further icon | Draw per frame |
| --- | --- | --- | --- |
| `MauiImage` (PNG decode) | 1.4 ms | 32 µs | 1.7 µs |
| **This plan** (reader, then a bitmap at the view's size) | 5.7 ms | 28 µs | about 1.7 µs (assumed: an image of the same size) |
| Generated C# recorded as a picture | 0.9 ms | 6 µs | 9 µs |
| Svg.Skia, recorded as a picture | 100 ms | 360 µs | 9 µs |

Memory per icon on screen is the same as a `MauiImage` of that size (20 KB at 72 px). In the package, one icon takes 0.8 KB against 7.4 KB of PNGs across five densities.

## Phase R — The reader

**Done.**

Internal to `MauiSkiaUi/Imaging`: bytes to a small tree of nodes, and that tree to an `SKImage` at a given size and color.

- **R1 Parsing** ([D3](#decisions)). Size: M.
  - `XmlReader`, forward only, into own nodes. A document is read whole before it draws, because `<defs>` often follows what refers to it.
  - Elements: `svg`, `g`, `a`, `path`, `rect`, `circle`, `ellipse`, `line`, `polyline`, `polygon`, `defs`, `clipPath`, `mask`, `style`, and one-line `text` (`x`, `y`, `font-size`, `font-family`, `font-weight`, `font-style`, `text-anchor`).
  - Properties, inherited as SVG says: `fill`, `stroke`, `stroke-width`, `stroke-linecap`, `stroke-linejoin`, `stroke-miterlimit`, `stroke-dasharray`, `stroke-dashoffset`, `fill-rule`, `clip-rule`, `opacity`, `fill-opacity`, `stroke-opacity`, `color`, `display`, `transform`, `clip-path`, `mask`. As attributes, in an inline `style`, and from `.class` rules in `<style>` (comma lists; a rule beats an attribute, an inline style beats a rule, `!important` beats both). A clip's and a mask's content inherits from what is around them, not from what uses them.
  - Colors: `#rgb`, `#rgba`, `#rrggbb`, `#rrggbbaa`, `rgb()`, `rgba()`, `none`, `currentColor`, the CSS named colors from a static table (no reflection).
  - Lengths: numbers, `px`, `pt`, `pc`, `in`, `cm`, `mm`. `viewBox`, root `width` / `height` (percentages and relative units such as `em` fall back to the `viewBox`), `preserveAspectRatio`.
  - Tests: each element and property with its inheritance; forward references; the cascade; BOM, prolog, comments, CDATA in `<style>`.
- **R2 Drawing.** Size: M.
  - Paths through `SKPath.ParseSvgPathData`; a clip through `ClipPath`; a mask through a layer with `DstIn` (luminance unless `mask-type: alpha`); dashes through `SKPathEffect.CreateDash`; group opacity through a layer; `text` through the fonts the text engine uses ([`SkUiFonts`](../../MauiSkiaUi/Helpers/SkUiFonts.cs)).
  - Tests: pixel checks against Svg.Skia, used by the test project only ([D7](#decisions)), on documents written for the tests. `SKUI_VECTOR_CORPUS=<folder>` adds every `.svg` in a folder, so an app's private icons can be checked without entering the repository. Text with the bundled font (Linux CI has no system fonts).
- **R3 Bad and hostile files** ([D4](#decisions)). Size: S.
  - A DTD is skipped (older exports start with one) and its entities are not expanded, with no XML resolver. No element can reach outside the document, so a file makes no requests and reads nothing. Limits: `MaxBytes` 1 MiB, `MaxElements` 20,000, `MaxDepth` 64 on nesting, `MaxSize` 1,000,000 DIPs a side, `MaxStyleRules` 5,000 and `MaxStyleWork` 1,000,000 in the cascade, `MaxDrawDepth` 128 with mask content, `MaxLayers` 12 open at once (each can take the whole bitmap's memory), `MaxWork` 4,000,000 in drawing (charged by element, by characters read, more for strokes, by layer pixels and by dashes, since masks can multiply work). Reading and drawing stop when the load is cancelled. Mask cycles are rejected. The bitmap is clamped to `MaxDecodeDimension`, aspect kept (L1).
  - An SVG feature the reader cannot draw (`image`, `use`, `symbol`, `pattern`, gradients, `filter`, `foreignObject`, `tspan`, `textPath`, `marker`, `switch`, a `url()` paint, a selector other than a class, a blend mode, a `paint-order` that strokes first, `vector-effect`, a `transform-origin` other than zero) fails with an `InvalidDataException` naming it, shown in `LoadError`. Other unknown elements are skipped with their content, as a design tool's template elements are, and unknown properties are ignored. `script` and the `animate` elements are skipped too: an animated file draws its first state.
  - Tests: nesting that overflowed the stack in Svg.Skia (512 levels), chained masks on a 256 KB stack, entity bombs, reference cycles, `stroke-dasharray=".001"` on a long path and huge stroke widths finish in bounded time; bad path data; `<html/>`, empty and truncated files; no document opens a socket or a file.

## Phase L — In the image loader

**Done.** Costly files on the phones stay in the [verification backlog](Roadmap.md#verification-backlog).

- **L1 Where bytes become an image** ([D1](#decisions)). Size: S.
  - `SkUiImageDecoder.Decode`: where `SKCodec.Create` returns `null` and the bytes start with `<` after a BOM and white space, the reader runs. Raster decoding gains nothing, and every source gets it: `Source="check.svg"`, a URI with its disk cache and progress, a file, a stream.
  - The bitmap is drawn at the decode bounds, larger ones too, or else at the intrinsic size × the display scale, which `SkUiImageLoadContext.DecodeAsync` passes on. `IsReduced` is `true`, because larger bounds give more pixels. Transformations apply to it as to any image.
  - `.svg` files ship as `MauiAsset`. A `MauiImage` of the same name wins, because the lookup finds its generated PNG first, and so does an Android drawable. That lookup needs a platform, so it has no headless test: checked with a temporary page on an iOS simulator, Mac Catalyst and an Android emulator (with the density picked by each), and on Windows at 100% display scale. The docs say so.
  - Tests: every source kind, keys differ by bounds, a corrupt cached file is downloaded again, raster paths unchanged.
- **L2 Following the view** ([D2](#decisions)). Size: M.
  - A vector loads first at its intrinsic size, so measure works and nothing waits for an arrange. `SkUiImage` and `SkUiCoreImage` then draw it again at the arranged size in exact pixels when that differs by more than a pixel, keeping the shown image meanwhile. `DownsampleToViewSize`'s 16 DIP steps do not apply: a 24 DIP icon drawn at 32 and scaled down is soft. Explicit decode bounds win; `Aspect.Center` draws unscaled. A fade finishes before the redraw, since a redraw would end it.
  - An icon whose view has its intrinsic size loads once. An image button draws it for the space inside its padding. Button icons and slider thumbs draw at the intrinsic size or L3's `Size`, as they do a `MauiImage`; avatars at their content size; placeholders at the image's decode bounds when it has any.
  - Each redraw is a load: it raises the load events, and reads the source again. That is the disk cache for a download by default; a stream or an uncached download is read once per size.
  - A new `IsVector` on `SkUiDecodedImage` and `SkUiCachedImage`. The shared logic sits next to `SkUiViewSizedDecode`.
  - Tests: sharp pixels at larger and smaller sizes on both layers; an unchanged size loads once; a size animation does not queue a load per frame (one redraw runs at a time, then the latest size); a list of one icon shares one entry per size and color.
- **L3 Source and color** ([D5](#decisions)). Size: S.
  - `SkUiVectorImageSource`, a MAUI `ImageSource` shaped like `FontImageSource`, so it works in every property that takes an image: `Source` (as `Image.Source`'s string), `Color` (the starting value of `currentColor`), `Size` (the longest edge in DIPs; 0: the file's). `FromMaui` maps it, and `AffectsImage` gains `Source`.
  - Core: `SkUiImageSource.FromVector(source, color, size)` and `FromVectorText(svg, color, size)`. Color and size join the memory cache's key, so a theme change is a new load per file (0.2–0.9 ms on the phones, off the UI thread); the disk cache and `SkUiImageCache`'s removal stay keyed by the file, so a download is shared by its colors.
  - The source says it is a vector before it loads, so a view that has a size draws it for that size at once, with no step through the file's size. If the bytes turn out to be a bitmap (a `MauiImage` of that name), the view loads it again without those bounds. A new color or size of the same file keeps the icon shown until it is drawn, on every slot user, so a theme change does not blank icons for a frame.
  - `Source="check.svg"` needs none of this and draws `currentColor` black, as SVG says. `SkUiTintTransformation` still recolors a file whose colors are written out.
  - Tests: pixels and keys; a changed `Color` reloads in each control that tracks a source (image, button, slider, avatar); `AppThemeBinding` and `DynamicResource` on `Color`; XAML loading (`RuntimeXamlCollection`); `ImageSourceParityTests`; leak scenario `VectorIconsSwitched` on both layers, in `tests/Shared/MemoryLeaks`.

## Phase M — Measurements, demo and docs

- **M1 Cost on devices.** Size: S, device time.
  - Bench scenarios: 1,000 vector icons against the same icons as PNGs, on both layers (first frame, per-frame composite, allocation). They set options through `TrySet`, so `--baseline v1.6.0` still builds.
  - On the Galaxy S9 and the iPhone 8: the first icon in the process, a scrolling list of 50 icons per screen, a theme change on a page of icons, against PNGs and font glyphs. Under Native AOT on Mac Catalyst: named colors and `XmlReader`. Results go in [Performance.md](../Performance.md).
  - If the first icon is a visible hitch on the S9, warm the reader up in the background at startup. **Result: no warm-up.** It runs off the UI thread and shows about 100 ms later than a PNG, once per process.
  - **Done** on the S9 and the iPhone 8. `VectorIconsSwitched` passes on the S9, an iOS simulator and under Native AOT on Mac Catalyst, with named colors, a class rule and no trim or AOT warnings. `--baseline v1.6.0` headless: 77 of 77 measures within noise, on selected scenarios (labels, buttons, avatars, images, entries, `virtual-fling`).
- **M2 Demo and docs.** Size: S.
  - A gallery page next to the image page: sizes, colors following dark mode, an icon in a button and a slider thumb, both layers side by side, a file with a gradient showing its `LoadError`.
  - **Done**, the page checked on an iOS simulator, the Galaxy S9, Mac Catalyst and Windows. The samples app also has an example, "Icons from SVG files": settings rows, buttons and sizes following the theme, in XAML.
  - [SkUiImage.md](../controls/SkUiImage.md): a section with the project-file line, what the reader supports, what fails, and when to use a `MauiImage` instead; its "SVG at runtime" row. [SkUiCore.md](../controls/SkUiCore.md). [Migration.md](../Migration.md) and the migration skills in `plugins/skiaui-migration`: `FontImageSource` icons and tint behaviors map to `SkUiVectorImageSource`.

## Not in 1.7

Gradients (the likely next addition: additive, and 13% of the sample SVGs in the reference repositories need only them); `<use>`, `<symbol>`, `<pattern>`, embedded images, filters, full CSS, multi-line or styled text; scale animations redrawing the vector (the bitmap stretches, as a `MauiImage` does); a warning at build time for an icon the reader would reject; `SkUiVectorImageSource` on MAUI's native `Image`.

## Docs, migration and demo

Done with each step: [CHANGELOG.md](../../CHANGELOG.md) (one entry under `## Unreleased`), [Roadmap.md](Roadmap.md); the rest as listed in [M2](#phase-m--measurements-demo-and-docs).

## Order

| # | Steps | Size | Why here |
| --- | --- | --- | --- |
| 1 | R1, R2 | M, M | Everything draws through it; testable alone, with the corpus check from the start |
| 2 | R3 | S | Limits and errors belong to the parser before any file comes from a real source |
| 3 | L1 | S | First icon on screen; `MauiAsset` lookup checked on each platform |
| 4 | L2 | M | Sharp at any size |
| 5 | L3 | S | Color and the XAML API |
| 6 | M1 | S | Device numbers, against the prototype's |
| 7 | M2 | S | Documents what M1 settled |

## Decisions

- **D1 The reader lives in the decoder, not behind a new source kind.** Every source already reads bytes, so URIs, the disk cache, progress, shared loads and events work with no new code; Android's XML drawables already render at the decode bounds through the same loader. **Implemented.**
- **D2 A bitmap at the view's size, not a picture.** A bitmap draws about five times faster per frame on a CPU raster surface, needs no change to `SkUiImageSlot`, the fade or the slot's users, and is what an icon needs: it is sized by layout and rarely animated in scale. **Implemented.**
- **D3 Our own subset, not Svg.Skia.** For icons it loads about 13 times faster per icon and about 18 times faster for the first one, adds no dependency, package or Trusted Publishing policy, avoids Svg.Custom's reflection over named colors when trimmed, and has no external references to close. It costs a reader to maintain and files it will not draw, which fail visibly. The design on Svg.Skia and its spike findings stay in the [full SVG plan](ImplementationPlan-FullSvg.md), if a real app needs it. **Implemented.**
- **D4 Fail, do not guess.** An icon with a part missing looks like a design bug that is hard to trace, while a `LoadError` names the element. Elements that are not SVG are skipped, which matched Svg.Skia's result on the one such file scanned. **Implemented.**
- **D5 Color on the source, as `FontImageSource`.** It reaches every control that takes an image, moving an icon from a font glyph is a one-element change, and the view gains no property. `currentColor` is what these icons use, so `Color` replaces only that and leaves colors written in the file alone. Names: `SkUiVectorImageSource`, `FromVector`, `FromVectorText`. **Implemented.**
- **D6 No source generator.** Against the reader it saves about 22 µs per icon, once, and draws no faster than a picture. Its first call is slower than `SKPath.ParseSvgPathData` on a string constant, because the method has to be compiled. A generator would also need its own SVG parser, since SkiaSharp's native library does not belong in the compiler, and a name lookup from XAML would keep every icon in a trimmed app. Build-time work, if ever wanted, fits an MSBuild task as Resizetizer does. **Implemented.**
- **D7 Svg.Skia as a test oracle only.** A `PackageReference` in `tests/MauiSkiaUi.Tests`, so pixels are compared with the renderer MAUI's Resizetizer uses and `SkiaUi.Maui` gets nothing. It restores and runs locally on `net10.0`; if it does not on CI, the comparison moves to expected PNGs generated once and committed. **Implemented.**

## Acceptance

- Headless suite and memory-leak scenarios pass (new: reader, hostile files, `VectorIconsSwitched`); 0 build warnings with the trim and AOT analyzers.
- `scripts/bench.sh --baseline v1.6.0` shows no regression in the existing scenarios, raster image scenarios included.
- `SkiaUi.Maui`'s dependencies are unchanged.
- The coverage table above holds with the shipped reader: the corpus check over those folders draws what the prototype drew, within the same differences.
- No hostile test file ends the process or makes a request, on the S9 and the iPhone 8 as well as headless.
- The gallery page checked on iOS, Android, Mac Catalyst and Windows; under Native AOT on Mac Catalyst (CI); M1's numbers in [Performance.md](../Performance.md).
