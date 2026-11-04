# SkiaUi 1.6 implementation plan: viewport insets, tiles, Core containers and performance

**Goal:** edge-to-edge pages scroll their content under drawn top and bottom bars and still show every row clear of them at the ends of the range, with scrolling, scroll bars, snapping, refresh and reveal working in the uncovered band; galleries of equal tiles lay out by themselves; the Core twins of the state container and the expander close the Core layer's container gaps; the Core layer stops re-recording ancestors on a measure change, shaped borders stop costing seconds on the first frame, and drawn password fields can show their text.

**Baseline:** .NET MAUI 10.0.120 (or the latest 10.0.x when 1.6 ships), SkiaSharp 4.154.0, branch `1.6-implementation` from `devel` after 1.5.1. .NET 11 / MAUI 11 waits until it is released ([roadmap](Roadmap.md#net-11--maui-11-support)). What has shipped is in the [changelog](../../CHANGELOG.md); the rest of the 1.x backlog stays in the [roadmap](Roadmap.md#backlog-1x).

**Rules** (as in 1.4, from [Requirements.md](Requirements.md#principles)):

- 1.x keeps the 1.0 API: everything here is additive, unless a [decision](#decisions) says otherwise and the change is agreed first.
- Both layers: each member lands on the SkUi\* control and its Core twin (`Set*`), with one shared engine.
- A feature a view does not use costs nothing per frame (`scripts/bench.sh --baseline v1.5.1`).

## Phase V — Viewport insets (first)

**Today.** `SkUiScrollView.Padding` (and `SkUiCoreScrollView`'s) sits inside the scrolled extent: content starts below a top padding and ends above a bottom one, and scrolls under the bars in between. So the simple case works now. `SkUiCollectionView` has no padding (MAUI's has none either); `SkUiVirtualVerticalStackLayout.Padding` works as the scroll view's. What padding cannot do is tell the scroller which part of its viewport is covered: the scroll bars run under the bars, `ScrollTo` / `ScrollToAsync(element, Start)` and `KeepSelectionVisible` put a row under the top bar, `SnapPointsAlignment` lines up with the covered edge, the refresh indicator appears under the top bar, the collection view's sticky header and sticky group headers pin under it, and keyboard and screen-reader reveal stop under the bottom bar.

The scroller already has the mechanism, internal: `SkUiScrollController.Insets` (start / end of the scroll axis covered by views drawn over the scroller), used by the collection view's sticky header and footer and, with `KeyboardInset`, by keyboard avoidance. The scroll bars' track, keep-selection, sticky group headers, reorder auto-scroll and the focus manager's reveal already work in the band it leaves uncovered. Phase V makes it public and brings the rest along.

- **V1 `ViewportInsets` on the scrollers** (`Thickness`, default zero; [D1](#decisions)). Size: M.
  - On `SkUiScrollView` and `SkUiCoreScrollView` (`SetViewportInsets`): the parts of the viewport covered by views drawn over the scroller. The content is arranged after the start inset and the extent grows by both, so at the ends of the range nothing is covered (as padding does today); unlike padding, every behavior below uses the uncovered band.
  - The scroll axis's insets add to the sticky header / footer insets the owner sets; the keyboard combines with the end inset ([D2](#decisions)). Cross-axis insets pad the content and keep the scroll bar out of them (a landscape notch).
  - Scroll bars: the track in the uncovered band (done for `Insets` on `SkUiScrollView`; the Core scroll view gets it). `SnapPointsAlignment` Start / Center / End against the band. `ScrollToAsync(x, y)` stays raw offsets; `ScrollToAsync(element / node, position)` places it in the band.
  - Tests: content arranged after the start inset; extent; each behavior above with and without insets; insets changing while scrolled keep the visible content where it is (the offset moves by the change).
- **V2 Lists and refresh.** Size: M.
  - `ViewportInsets` on `SkUiCollectionView`, and on `SkUiVirtualScrollView` by inheritance; a bare `SkUiVirtualVerticalStackLayout` has no scroller of its own to cover and follows the insets of the `SkUiScrollView` around it: `ScrollTo(index / item, position)`, `KeepSelectionVisible`, the sticky header and footer (drawn inside the insets, pinned under the bars), sticky group headers, snap points, load-more triggers (the band's end, not the viewport's), reorder auto-scroll zones.
  - `SkUiRefreshView`: the indicator travels from the start inset, not from the viewport's edge; a pull starts as before.
  - Not in V: the carousel (its peek insets already define its band) and the Core collection-like containers (no Core virtual list yet).
  - Tests: as V1, for each list behavior; a grouped list with sticky group headers under a top inset.
- **V3 Safe area** ([D3](#decisions)). Size: S.
  - A scroller that is the surface root already takes the window's safe area into its padding. For a scroller below an app's own bars, the app needs the safe area to size the bars and the insets: a read-only, bindable `SafeAreaInsets` on the surface root (changes with rotation and window resize), so XAML binds the bars' padding and `ViewportInsets` to it.
  - Tests: values follow the surface's safe area; bindings update on change.
- **V4 Demo and docs.** Size: S.
  - A gallery page "Edge-to-edge scrolling": translucent drawn top and bottom bars over a collection view and a scroll view, the bars' sizes bound into `ViewportInsets`, with snap points, a sticky header, pull to refresh and a text field at the bottom.
  - [SkUiScrollView.md](../controls/SkUiScrollView.md), [SkUiCollectionView.md](../controls/SkUiCollectionView.md), [SkUiRefreshView.md](../controls/SkUiRefreshView.md), [ScrollingAndCollectionViews.md](ScrollingAndCollectionViews.md); [Migration.md](../Migration.md): iOS `ContentInset` / Android `clipToPadding="false"` patterns and MAUI pages that fake them with header and footer spacers map to `ViewportInsets`.

## Phase R — Performance backed by measurements

- **R1 Device results measured again** (needs the Galaxy S9 and the iPhone 8). Size: S, device time.
  - The stress (1,000 buttons) and effects (400 cards) pages in Release on both devices, on the current toolchain; [Performance.md](../Performance.md) updated with the commit and toolchain. `scripts/bench.sh` `core-buttons` and `skui-buttons` on the S9 for the first-frame question below.
  - Done first: R2 and R3 are judged against these numbers. If Core's first frame is still no faster than SkUi\*'s although Core builds three times faster, find where it goes (frame trace) before R2.
- **R2 Core measure invalidation without re-recording ancestors** (done for SkUi\* views). Size: M.
  - Today `SkUiCoreNode.InvalidateMeasure` calls the parent's `InvalidateMeasure`, which invalidates its paint, so one label's new text re-records every container up to the host. A container's own picture (background, border, chrome) depends on its frame, not on its children's layout: a child's measure change marks the ancestors' layout dirty only, and a container re-records when its arranged size changes or it opts in (containers that paint from their children's layout, such as the Core table's track backgrounds and grid splitters).
  - Bench: a new scenario changing one label's text in a deep Core tree (counting recorded pictures), next to `skel-tax-on` / `skel-tax-off` and `core-labels-update`; set through `TrySetOption` so `--baseline v1.5.1` still builds. Tests: recorded pictures per change; containers that paint from children still update.
  - Needed by S2: the Core expander relays out every frame of its animation.
- **R3 Shaped borders' first frame.** Size: S–M.
  - 400 ticket-shaped cards take 1–2 s to their first frame (iPhone 8 and S9) because each card computes its outline and its content clip (a path operation) when it is first laid out. Borders of one shape and size share them (a small cache keyed by the shape's geometry, size and stroke), and a content clip that equals the inset outline skips the path operation.
  - Ownership: shared outlines are disposed by the cache, not by the compositor ([RenderingPipeline.md](RenderingPipeline.md)); leak scenario for borders that change shape.
  - Measured with R1's effects page; headless: a bench scenario of 400 shaped cards (first frame).
- Not in 1.6: text shadows cast on the GPU, the raster cache, recording Core subtrees off the UI thread ([roadmap](Roadmap.md#architecture-and-performance)).

## Phase T — Tile layout

- **T1 `SkUiTileLayout` + `SkUiCoreTileLayout`** ([D4](#decisions)). Size: M.
  - Equal tiles for galleries and attachment strips: `TileSize` (the largest tile), `MinimumTileSize`, `TileAspectRatio` (1 by default), `ColumnSpacing`, `RowSpacing`, `Padding`. The column count is the most tiles of at most `TileSize` that fit the width; tiles stretch to fill the row, never past `TileSize` (the width left over stays at the row's end), and drop a column when they would be smaller than `MinimumTileSize`.
  - One engine for both layers (as `SkUiWrapEngine`); children arranged in the tiles in order; the column count is measured per width and cached with the measure.
  - Tests: column counts and sizes across widths, spacing, padding, RTL, aspect ratio, hidden children; Core / SkUi\* frames equal.
- **T2 Overflow tile.** Size: S–M.
  - `MaximumRows` (default unlimited): when the children need more rows, the last visible tile shows `OverflowTemplate` (bound to an `SkUiTileOverflow` with the hidden count, so a "+5" over the last image), and the rest are not measured or drawn. `OverflowTapped` / `OverflowCommand`.
  - Tests: counts, the template's binding, children added and removed.
- **T3 Docs and demo.** Size: S. A new `docs/controls/SkUiTileLayout.md` page, the controls index, a gallery page (photos with an overflow tile, a resizable preview area); [Migration.md](../Migration.md): grids rebuilt in `SizeChanged` to fit equal tiles become a tile layout. Virtualized galleries stay on the collection view's `GridItemsLayout`.

## Phase S — Core containers

- **S1 Core state container.** Size: M.
  - Core nodes are not bindable, so a node with keyed states instead of attached properties: `SkUiCoreStateContainer` with `States` (key → node or factory), `CurrentState` (`string?`, `null` shows the children), `CanStateChange`, and the SkUi\* container's before / after animations on the render thread (the shared `SkUiStateTransition`). Shimmer and skeleton states work as on the SkUi\* layer.
  - Tests and a leak scenario as `StatesSwitched`, on Core.
- **S2 Core expander** (after R2). Size: M.
  - First `ScaleX` / `ScaleY` and `AnchorX` / `AnchorY` on Core nodes ([D5](#decisions)): composite-time render props, as SkUi\* views have them.
  - `SkUiCoreExpander`: `Header`, `Content`, `IsExpanded`, `Direction`, `Command`, `ExpandedChanged`, `LazyContentExpansion`, `AnimationLength` / `AnimationEasing`, on the SkUi\* expander's engine (content scaled from the header's side while the height follows on the UI clock). The header is a button for screen readers and the keyboard.
  - Tests: as `ExpanderTests` on Core; recorded pictures per animation frame (R2's win); leak scenario.
- **S3 Docs and demo.** Size: S. [SkUiStateContainer.md](../controls/SkUiStateContainer.md), [SkUiExpander.md](../controls/SkUiExpander.md), [SkUiCore.md](../controls/SkUiCore.md); gallery rows on the Core layer; [Migration.md](../Migration.md): Core code that animated one scale axis.

## Phase E — Password reveal

- **E1 Show / hide in drawn password fields** ([D6](#decisions)). Size: M.
  - On `SkUiEntry` and `SkUiCoreEntry`: `IsPasswordRevealed` (two-way) and `ShowsPasswordRevealButton` (`bool`, default `false`). The button is a trailing icon drawn by the look (new virtual members for its glyphs and size), a toggle for screen readers ("Show password" / "Hide password" in `SkUiStrings`).
  - The field stays a password field for the platform while revealed: Android keeps the password input type (only the drawing stops masking), iOS keeps `secureTextEntry`, Windows' proxy box keeps its mask; so the keyboard, autofill and password managers behave as before, and copy stays disabled. Screen readers read the text only while revealed.
  - Hidden again when the field loses focus (`HidesPasswordOnUnfocus`, default `true`).
  - Tests: drawing masked and revealed, the caret and selection staying put, the button's hit and semantics; device checks (autofill and password managers on a revealed field) in the [verification backlog](Roadmap.md#verification-backlog).
- **E2 Docs.** [SkUiEntry.md](../controls/SkUiEntry.md), [TextInput.md](TextInput.md); the demo's sign-in form.

## Docs, migration and demo

Done with each step: the control pages named in the steps; [Migration.md](../Migration.md) and the migration skills (`plugins/skiaui-migration`) where a MAUI pattern now maps; the demo's component gallery; [Roadmap.md](Roadmap.md) (items leave the backlog as they ship); [Requirements.md](Requirements.md) (status); [CHANGELOG.md](../../CHANGELOG.md) (an entry under `## Unreleased` per feature).

## Order

| # | Steps | Size | Why here |
| --- | --- | --- | --- |
| 1 | V1 | M | Asked for first; the scroller change every V step builds on |
| 2 | V2, V3 | M, S | Lists and refresh on V1; the safe area the demo binds |
| 3 | V4 | S | The edge-to-edge page checks V1–V3 together |
| 4 | R1 | S | Device runs; can run alongside V |
| 5 | R2 | M | Before S2; judged against R1 |
| 6 | R3 | S–M | Independent; judged against R1 |
| 7 | T1, T2, T3 | M, S–M, S | Independent track |
| 8 | S1 | M | Independent; the SkUi\* container's transitions |
| 9 | S2 | M | Needs R2 and the new Core transforms |
| 10 | E1, E2 | M | Independent; needs device time for the proxies |

V comes first; T, S1 and E are independent of it and of each other.

## Decisions

- **D1 A new `ViewportInsets`, not `Padding`.** Padding keeps its meaning (space inside the scrolled content, as in MAUI), so 1.x pages draw as before; `ViewportInsets` says which part of the viewport is covered. **Implemented.**
- **D2 The keyboard and the end inset.** The keyboard covers the bottom bar, so the end inset is the larger of the two, not their sum. A collection view's sticky footer still adds to that, as it did to the keyboard before. **Implemented.**
- **D3 The safe area stays the app's.** Drawn views inside a surface still do not apply `SafeAreaEdges` ([not planned](Roadmap.md#not-planned)); the surface root exposes the insets so the app sizes its bars and passes them on. **Implemented.**
- **D4 The tile layout is a layout, not a virtualized list.** Galleries with many items use the collection view's grid; the tile layout is for a page's strip or block of tiles. **Proposed.**
- **D5 Core `ScaleX` / `ScaleY` become independent.** Today Core nodes scale uniformly, and animating `ScaleX` or `ScaleY` on a Core node scales both. With separate scales an animation of `ScaleX` scales X only, as on SkUi\* views and in MAUI; `Scale` still sets both. A behavior change for Core code that animates one axis, so it goes under `### Breaking changes` in the changelog with the fix (animate `Scale`). **Decided:** independent.
- **D6 Reveal names.** `IsPasswordRevealed` / `ShowsPasswordRevealButton` / `HidesPasswordOnUnfocus`: MAUI has no API to follow. **Proposed.**

## Acceptance

- Headless suite and memory-leak scenarios pass (new: Core states switched, Core expander toggled, shaped borders reshaped); 0 build warnings with the trim and AOT analyzers.
- `scripts/bench.sh --baseline v1.5.1` shows no regression in the existing scenarios; the new R2 and R3 scenarios show the expected drop.
- The edge-to-edge gallery page checked on an iPhone with a notch, an Android phone in edge-to-edge, Mac Catalyst and Windows; [Performance.md](../Performance.md) measured again on both devices.
- Device checks of E1 (keyboards, autofill, password managers) recorded in [Testing.md](Testing.md) or left in the verification backlog.
