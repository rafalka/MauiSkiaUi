# SkiaUi 1.10 implementation plan: Community Toolkit layouts and collection view parity

**Goal:** pages that use the Community Toolkit's `DockLayout` and `UniformItemsLayout` port by changing the prefix, and the drawn collection view behaves like the platforms' lists where it does not yet: keyboard item navigation, extended (Shift / Ctrl) selection, a template for selected items, and animated inserts and removes.

**Scope:** two items moved here from the roadmap's backlog: Community Toolkit layouts (from [New controls and packages](Roadmap.md#new-controls-and-packages)) and CollectionView (from [MAUI parity](Roadmap.md#maui-parity)). Started on `1.10-implementation` (10 November 2026), from `devel` after 1.9.0: L1–L4 done.

**Baseline:** .NET MAUI 10.0.120 (or the latest 10.0.x when 1.10 ships), SkiaSharp 4.154.0, CommunityToolkit.Maui 15.0.1. .NET 11 / MAUI 11 waits until it is released ([roadmap](Roadmap.md#net-11--maui-11-support)). What has shipped is in the [changelog](../../CHANGELOG.md); the rest of the 1.x backlog stays in the [roadmap](Roadmap.md#backlog-1x).

**Rules** (as in 1.9, from [Requirements.md](Requirements.md#principles)):

- 1.x keeps the 1.0 API: everything here is additive, unless a [decision](#decisions) says otherwise and the change is agreed first.
- Both layers where a control has both. The collection view has no Core twin, so phase C is SkUi\* only; the toolkit layouts are SkUi\* only too ([D1](#decisions)).
- Integration packages are built on `SkiaUi.Maui`'s public API only ([Popups.md](Popups.md#integration-packages)).
- A feature a view does not use costs nothing per frame (`scripts/bench.sh --baseline v1.9.0`).

## Phase L — Community Toolkit layouts

In `SkiaUi.Maui.CommunityToolkit`, on the toolkit's public layout managers (`DockLayoutManager`, `UniformItemsLayoutManager` in `CommunityToolkit.Maui.Core.Layouts`), as `SkUiFlexLayout`, `SkUiGrid` and the stacks run MAUI's managers through `ILayout` / `IPadding` (padding includes the root's safe area).

- **L1 Layout extension points made public** (in `SkiaUi.Maui`). Size: S. **Done** (`ToolkitLayoutTests`; documented in [SkUiLayout.md](../controls/SkUiLayout.md#layouts-of-your-own)).
  - `SkUiLayout.AffectsChildLayout(string?)` and `SkUiLayout.OnChildrenChanged()` go from `private protected virtual` to `protected virtual` (the built-in overrides in the grid, flex, absolute and shrink layouts follow). Additive: no caller can see them today. Their XML docs already describe the contract; add that a layout running a third-party manager lists the attached properties it reads.
  - Tests: a test subclass whose attached property re-measures the layout; children added, replaced, removed and cleared each reach `OnChildrenChanged`; the API parity test unchanged.
- **L2 `SkUiDockLayout`.** Size: M. **Done** (`ToolkitLayoutTests`; in the MAUI API parity test against the toolkit's `DockLayout`). The frames are checked against the manager's rules by hand: the toolkit's own layout cannot be measured headless (its MAUI children have no handlers).
  - Derives from `SkUiLayout`, implements the toolkit's `IDockLayout`, measures and arranges with `DockLayoutManager`.
  - Bindable `ShouldExpandLastChild` (default `true`), `HorizontalSpacing`, `VerticalSpacing` (≥ 0, `SkUiValidate`), with fluent `Set*`; `GetDockPosition(IView)` and `Add(IView, DockPosition)` as on the toolkit's layout.
  - The attached property stays the toolkit's: `SkUiDockLayout.DockPositionProperty = DockLayout.DockPositionProperty`, so `toolkit:DockLayout.DockPosition="Top"` on children ports unchanged (as `SkUiFlexLayout` reuses `FlexLayout.GrowProperty`); `AffectsChildLayout` lists it. The toolkit's own change callback only invalidates its `DockLayout` parent, so this is what re-measures the drawn one.
  - Tests: each dock position, spacing, padding, `ShouldExpandLastChild` on and off, hidden children skipped, a child's `DockPosition` changed at runtime, right to left (frames mirrored as in the drawn grid and stacks).
- **L3 `SkUiUniformItemsLayout`.** Size: S–M. **Done** (`ToolkitLayoutTests`; in the parity test against the toolkit's `UniformItemsLayout`).
  - Derives from `SkUiLayout`, implements `IUniformItemsLayout`, runs `UniformItemsLayoutManager`. Bindable `MaxRows`, `MaxColumns` (≥ 1, default `int.MaxValue` as the toolkit's) with `Set*`.
  - The manager measures the first visible child once and keeps that size for its lifetime ([D2](#decisions)): the layout creates a new manager each time it measures (only when its measure is invalid: children, a child's measure, an option or the constraint changed; children cache their measure), so a re-templated first item or a font change re-sizes the cells.
  - Children past `MaxRows` × `MaxColumns` are not arranged by the manager: the layout arranges them empty (zero size) after it, so they neither draw nor take input, instead of keeping an old frame.
  - Tests: columns across widths, `MaxRows` / `MaxColumns`, padding, hidden children, the first child's size changing, children past the limit, right to left.
- **L4 Docs, migration and demo.** Size: S. **Done** (`DockLayoutDemoPage`, `UniformItemsLayoutDemoPage` beside the toolkit's layouts; the checker's `TOOLKIT_LAYOUTS` with `CheckXamlTests`; the editor help embeds the package's XML documentation).
  - Control pages `docs/controls/SkUiDockLayout.md` and `SkUiUniformItemsLayout.md`, the controls index; the package's README, `Description` and `PackageTags`; [Popups.md](Popups.md#integration-packages) (no longer "planned").
  - [Migration.md](../Migration.md): rows for the two toolkit layouts in the toolkit table and the mapping table (prefix swap; the package and `UseSkiaUiCommunityToolkit()`); the migration skills: `TOOLKIT_DRAWN` in `audit_maui_app.py` and the mapping in `skiaui-migrate`.
  - XAML namespace for the package's types ([D3](#decisions)): `xmlns:skct="clr-namespace:MauiSkiaUi.CommunityToolkit;assembly=MauiSkiaUi.CommunityToolkit"`.
  - Gallery pages next to the toolkit's own layouts (the demo already references the toolkit), with a resizable preview area.

## Phase C — Collection view parity

MAUI's `CollectionView` API is already matched ([MauiApiParityTests](../../tests/MauiSkiaUi.Tests/MauiApiParityTests.cs); the three breaking changes stay [held for 2.0](Roadmap.md#held-for-20)). What is left is behavior the platforms' lists have: keyboards (Windows, Mac Catalyst, iPad and Android with a keyboard), desktop selection, and item animations (on by default in UIKit and RecyclerView, which MAUI's handlers use). The design goes into [ScrollingAndCollectionViews.md](ScrollingAndCollectionViews.md#collection-view-implemented) with each step.

- **C1 Keyboard item navigation.** Size: M–L. **Done** (`CollectionViewKeyboardTests`; [Keyboard](../controls/SkUiCollectionView.md#keyboard)). Settled while building it: only an interactive list is a tab stop (item taps, a selection mode or expandable groups), as other drawn controls; a move reveals its item at once, not animated, as native lists follow their keyboard focus; a UI Automation client focusing an item focuses the list with that item current (`SkUiFocusManager.FocusOrOwner`). Device checks open.
  - The list is one tab stop, not one per item: the collection view takes keyboard focus and keeps a *current item* (by item, not by index or container, so it survives collection changes, recycling and scrolling it out of view). `ISkUiKeyHandler` on the collection view, ahead of the scroller's arrow scrolling ([Accessibility.md](Accessibility.md#keyboard-focus)).
  - Keys: arrows move the current item along the axis (a grid also across, by `Span`; Left / Right mirrored right to left); Home / End; Page Up / Page Down by the uncovered band; each move realizes and reveals the item through the engine's `ScrollToIndex` (sticky insets and the sticky group header counted). Space / Enter select it as a tap would (`SelectionChanging` first) and raise `ItemTapped` / `ItemTappedCommand`. Group headers are stops when `AllowGroupExpandCollapse` is on; Left / Right (or Enter) collapse and expand them.
  - Focus ring: the look's `DrawFocusRing` on the current item's container while focus came from the keyboard (as for other nodes); a pointer press on Windows makes the pressed item current without a ring (`PointerPressFocuses`).
  - When focus arrives and there is no current item: the selected item (single), the focus item of `KeepSelectionVisible` (multiple), else the first visible item. A removed current item passes to its neighbor.
  - Screen readers keep their own navigation; the current item is not a second accessibility focus.
  - Tests: every key in lists and grids of both orientations, right to left, grouped and collapsed, sticky parts, the current item across inserts, removes, moves, resets and recycling (10k items), Tab in and out, focus ring only from the keyboard. Device checks: Windows, Mac Catalyst, iPad with a keyboard, Android with a keyboard ([verification backlog](Roadmap.md#verification-backlog)).
- **C2 Extended selection** (after C1). Size: M. **Done** (`CollectionViewExtendedSelectionTests`; [Keyboard](../controls/SkUiCollectionView.md#keyboard)). Settled while building it: Enter taps without changing the selection (Space toggles); Ctrl / Command+letter now reach drawn key handlers on Apple and Windows (Android had them); taps carry their device, so touch keeps toggling; Android's software surfaces report no modifiers (SkiaSharp's touch events carry none). Device checks open.
  - `IsExtendedSelectionEnabled` (bindable `bool`, default `false`; [D4](#decisions)) with `SelectionMode="Multiple"`: a click selects only its item, Ctrl+click (Command on Apple) toggles one, Shift+click selects the range from the anchor; with the keyboard, the moving keys move only the current item (the selection stays), Space toggles it, Insert toggles it and moves on, Shift with the moving keys toggles the range from where Shift was first pressed, Enter taps without selecting, Ctrl / Command+A selects all (changed after review: arrows that selected the item they reached, as Windows and Finder do, dropped a selection made with clicks). Touch keeps `Multiple`'s toggling tap.
  - Ranges count items across groups in source order; items of collapsed groups are left out. Each gesture changes `SelectedItems` in one step: one cancelable `SelectionChanging`, one `SelectionChanged` and one command run with the whole change.
  - Needs the pointer's modifier keys: `SkUiPointer.Modifiers` (`SkUiKeyModifiers`; additive) from Windows' `KeyModifiers`, UIKit's `UIEvent.ModifierFlags` (iOS 13.4+, Mac Catalyst) and Android's `MotionEvent.MetaState`, and on the tap a recognizer reports. Headless tests set them on the synthetic pointer.
  - Tests: every gesture above, anchors after resets, ranges over groups, cancel, one event per gesture, `KeepSelectionVisible` with select-all (built so: removes keep the anchor by item, as the current item, covered by C1's tests).
- **C3 Selected-item template.** Size: M. **Done** (`CollectionViewSelectedTemplateTests`, leak scenario `CollectionViewSelectedTemplate`). Settled while building it: a realized row is made again by the engine's new `RenewItem` (release to the old key's pool, realize in place, anchored as a change of the items, so with `KeepScrollOffset` the rows after it move); a selector that chooses no template falls back to `ItemTemplate`; setting the template rebuilds every row, as a new item template does.
  - `SelectedItemTemplate` (a `DataTemplate` or selector; SkiaUi, as the picker's `SelectedItemTemplate` is the field's view of its selected item): a selected item's row uses it instead of `ItemTemplate`. For a different look the `Selected` visual state stays the cheaper way; this is for selected rows that show more (details, actions) or differ in size.
  - The template is part of the recycle key, so a selection change replaces the row's container (from the pool of that template) and the row is measured again; anchoring keeps what shows in place, and with C4 the size change animates. Grid cells swap the same way. The reorder proxy and the current item (C1) use the row's template.
  - Nothing changes while it is unset (the recycle key stays the item template).
  - Tests: swapping on select and deselect in single and multiple modes, grids, groups, recycling, the size change with anchoring, `SelectAll`, a selector; leak scenario `CollectionViewSelectedTemplate`.
- **C4 Animated insert, remove and move.** Size: L. **Done** (`CollectionViewItemAnimationTests`, leak scenario `CollectionViewItemsAnimated`, bench `collection-changes` / `collection-changes-animated`: off, no change; on, the update costs what it did, with one more recording per removed row and about 240 KB per burst of 21 changes). Settled while building it: the changes of a frame are animated together after the next layout; views keep their final values and are drawn from their old places (`AnimateFromAsync`, a render tween with explicit start values), so recycling a view mid-glide leaves nothing behind; parts are matched by what they show, not by view; grids fade nothing out (cells rebind; the others glide over).
  - Opt-in for 1.x ([D5](#decisions)): `AnimatesItemChanges` (`bool?`, `null`: the look's `CollectionViewAnimatesItemChanges`, `false` in `DefaultSkUiLook`).
  - On a collection change while the list is laid out: the realized rows' offsets are kept before the change; after the next layout, rows that moved get a translation from their old place, animated to zero on the render thread (composite-time props; nothing re-records). Inserted rows fade in, removed rows fade out at their old place: the removed container stays in a host over the items (as the reorder proxy's drag host) until its animation ends, then goes back to its pool. Grids move cells to their new cells, as reordering moves them aside.
  - Only realized rows animate; changes outside the window just anchor. Resets, changes during a fling, a reorder drop (animated already) and Reduce Motion skip the animation. A new change while one runs starts from where the rows are drawn. Expanding and collapsing a group use the same path.
  - Look: `SkUiTransitionKind.ItemInsert`, `ItemRemove`, `ItemMove` with per-platform durations and easings (UIKit's and RecyclerView's `DefaultItemAnimator` as the references: [platform look targets](ControlLook.md)).
  - Taps during an animation hit the final layout (hit-testing against render-thread transforms is in the [backlog](Roadmap.md#architecture-and-performance)).
  - Tests: offsets and translations for inserts, removes, moves, replaces in lists and grids, groups, horizontal and right to left, overlapping changes, skipped cases, the ghost container released; leak scenario `CollectionViewItemsAnimated`; bench: a realized list inserting and removing with the animation on and off, and no change in the existing collection view scenarios when it is off.
- **C5 Docs and demo.** Size: S. **Done** (docs with each step; gallery pages `CollectionViewKeyboardDemoPage` and `CollectionViewAnimationsDemoPage` next to MAUI's `CollectionView`, `CollectionViewSelectedTemplateDemoPage`; device checks in the [verification backlog](Roadmap.md#verification-backlog)). Also: rows after a row that takes its selected template glide to make room when item animations are on (C3 with C4).
  - [SkUiCollectionView.md](../controls/SkUiCollectionView.md) (drop "Not available: keyboard item navigation"), [ScrollingAndCollectionViews.md](ScrollingAndCollectionViews.md) (drop the "Not yet" line), [Accessibility.md](Accessibility.md#keyboard-focus) (lists as one stop), [Migration.md](../Migration.md#lists-collectionview) (native lists' animations and keyboard support now match; WinUI's extended selection maps to `IsExtendedSelectionEnabled`), the migration skills where a mapping changes.
  - Gallery pages: keyboard navigation and extended selection, the selected-item template, animated changes (add, remove, move buttons; the animation switch).

## Docs, migration and demo

Done with each step: the pages named in the steps; [Migration.md](../Migration.md) and the migration skills (`plugins/skiaui-migration`) where a pattern now maps; the demo's component gallery; [Roadmap.md](Roadmap.md) (items leave the backlog as they ship); [Requirements.md](Requirements.md) (FR-22's status; the toolkit layouts); [CHANGELOG.md](../../CHANGELOG.md) (an entry under `## Unreleased` per feature).

## Order

| # | Steps | Size | Why here |
| --- | --- | --- | --- |
| 1 | L1 | S | Unblocks L2 and L3; the only change to `SkiaUi.Maui` in phase L |
| 2 | L2, L3, L4 | M, S–M, S | Independent of phase C |
| 3 | C1 | M–L | C2 builds on its current item and anchor |
| 4 | C2 | M | Needs C1 and the pointer's modifiers |
| 5 | C3 | M | Independent of C1 / C2; before C4, whose size-change animation it uses |
| 6 | C4 | L | Largest and riskiest (engine row changes, render-thread props); last |
| 7 | C5 | S | With each step; the gallery pages at the end |

Phase L and phase C are independent of each other.

## Decisions

- **D1 The toolkit layouts are SkUi\* only.** They run the toolkit's managers, which take `IView`s, and exist so MAUI pages port by prefix. Core trees already have the grid, stacks, wrap and tile layouts; a Core dock waits for an app that needs one. **Proposed; built so in L2 / L3.**
- **D2 The uniform items layout keeps the toolkit's manager.** It measures the first child once per manager instance; a new manager per measure keeps the toolkit's rules (the same frames as its layout) without its stale size, at one small allocation per measure (measures run only when something changed, never per frame). The alternative, an engine of our own like `SkUiTileEngine`, would drift from the toolkit. **Proposed; built so in L3.**
- **D3 The package's XAML namespace.** SkiaUi has no XAML URI (`xmlns:sk` is a `clr-namespace`), so one prefix for both assemblies would need an `XmlnsDefinition` URI for both, a change for every existing page. The types stay in the package's namespace `MauiSkiaUi.CommunityToolkit` with their own prefix (`skct:`), as the Markdown package's `MauiSkiaUi.Markdown`. **Proposed; built so in L4.**
- **D4 Extended selection is a switch, not a selection mode.** `SkUiSelectionMode` moves to MAUI's `SelectionMode` in 2.0 ([held for 2.0](Roadmap.md#held-for-20)), which has no `Extended`; a new enum member would have to move again. `IsExtendedSelectionEnabled` works with `Multiple` and keeps working after 2.0. **Proposed; built so in C2.**
- **D5 Item animations are opt-in in 1.x.** UIKit and RecyclerView animate by default, so MAUI parity says on; but turning them on for every 1.x app changes what existing lists show after an update (and headless tests that read frames right after a change). A nullable property with a look default lets an app turn them on everywhere with one look override; 2.0 can flip the default. **Proposed; built so in C4.**
- **D6 Names.** `SkUiDockLayout`, `SkUiUniformItemsLayout` (the toolkit's names with the prefix); `IsExtendedSelectionEnabled`; `SelectedItemTemplate`; `AnimatesItemChanges`, `CollectionViewAnimatesItemChanges`; `SkUiTransitionKind.ItemInsert` / `ItemRemove` / `ItemMove`; `SkUiPointer.Modifiers`. **Proposed; built so in L2–C4.**

## Acceptance

- Headless suite and memory-leak scenarios pass (new: `CollectionViewSelectedTemplate`, `CollectionViewItemsAnimated`); 0 build warnings with the trim and AOT analyzers in `SkiaUi.Maui` and `SkiaUi.Maui.CommunityToolkit`.
- `scripts/bench.sh --baseline v1.9.0` shows no regression in the existing scenarios (the collection view's with every new feature off); the new animation scenario costs nothing when off.
- The two toolkit layouts give the toolkit's frames for the same children; a toolkit page ports by changing the prefix (and adding the package).
- Device checks of C1 and C2 (keyboards and modifier clicks on Windows, Mac Catalyst, iPad and Android) and the feel of C4 on the Galaxy S9 and the iPhone 8 recorded in [Testing.md](Testing.md) or left in the [verification backlog](Roadmap.md#verification-backlog).
