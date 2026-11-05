# SkiaUi 1.6 implementation plan: viewport insets

**Goal:** edge-to-edge pages scroll their content under drawn top and bottom bars and still show every row clear of them at the ends of the range, with scrolling, scroll bars, snapping, refresh and reveal working in the uncovered band.

**Status:** shipped in 1.6.0.

**Scope:** phase V and the fix for hosted native views that keep an old clip when what clips them is resized ([#81](https://github.com/rafalka/MauiSkiaUi/issues/81), fixed in [#82](https://github.com/rafalka/MauiSkiaUi/pull/82)). The other work first planned for 1.6 (performance, the tile layout, Core containers, password reveal) moved to [1.9](ImplementationPlan-1.9.md); 1.7 brings [vector icons](Roadmap.md#170-vector-icons-planned) and 1.8 [Markdown](ImplementationPlan-1.8.md).

**Baseline:** .NET MAUI 10.0.120, SkiaSharp 4.154.0, from `devel` after 1.5.1. What has shipped is in the [changelog](../../CHANGELOG.md); the rest of the 1.x backlog stays in the [roadmap](Roadmap.md#backlog-1x).

**Rules** (as in 1.4, from [Requirements.md](Requirements.md#principles)):

- 1.x keeps the 1.0 API: everything here is additive.
- Both layers: each member lands on the SkUi\* control and its Core twin (`Set*`), with one shared engine.
- A feature a view does not use costs nothing per frame (`scripts/bench.sh --baseline v1.5.1`).

## Phase V — Viewport insets

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

## Docs, migration and demo

Done with each step: the control pages named in the steps; [Migration.md](../Migration.md) and the migration skills (`plugins/skiaui-migration`) where a MAUI pattern now maps; the demo's component gallery; [Roadmap.md](Roadmap.md); [CHANGELOG.md](../../CHANGELOG.md) (an entry under `## Unreleased` per feature).

## Decisions

- **D1 A new `ViewportInsets`, not `Padding`.** Padding keeps its meaning (space inside the scrolled content, as in MAUI), so 1.x pages draw as before; `ViewportInsets` says which part of the viewport is covered. **Implemented.**
- **D2 The keyboard and the end inset.** The keyboard covers the bottom bar, so the end inset is the larger of the two, not their sum. A collection view's sticky footer still adds to that, as it did to the keyboard before. **Implemented.**
- **D3 The safe area stays the app's.** Drawn views inside a surface still do not apply `SafeAreaEdges` ([not planned](Roadmap.md#not-planned)); the surface root exposes the insets so the app sizes its bars and passes them on. **Implemented.**

## Acceptance

- Headless suite and memory-leak scenarios pass; 0 build warnings with the trim and AOT analyzers.
- `scripts/bench.sh --baseline v1.5.1` shows no regression in the existing scenarios.
- The edge-to-edge gallery page checked on an iPhone with a notch, an Android phone in edge-to-edge, Mac Catalyst and Windows (what is still open: [verification backlog](Roadmap.md#verification-backlog)).
