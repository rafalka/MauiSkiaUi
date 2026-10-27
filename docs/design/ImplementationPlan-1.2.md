# SkiaUi 1.2 implementation plan: MAUI parity

**Goal:** more MAUI pages port by changing the XAML prefix. The MAUI XAML that has to be converted member by member today (gesture recognizers, the collection view's items layout and scroll API, MAUI 10's new members) loads and behaves as on MAUI. Where MAUI's own mechanism is internal (`TemplateBinding`, platform-specifics), the migration checker converts the XAML instead; `ImageBrush` is not public in MAUI at all ([scope review](#scope-review)).

**Baseline:** .NET MAUI 10.0.120, or the latest 10.0.x service release when 1.2 ships; SkiaUi stays on MAUI 10.x. What has shipped is in the [changelog](../../CHANGELOG.md); the rest of the 1.x backlog stays in the [roadmap](Roadmap.md#backlog-1x).

**Rules** (from [Requirements.md](Requirements.md#principles)):

- MAUI names and behavior win: a member MAUI has gets MAUI's name, type and defaults.
- Parity never costs performance or architecture. Where MAUI's model works against them, SkiaUi keeps its own API and adds a conversion path in [Migration.md](../Migration.md) and the migration skills (`plugins/skiaui-migration`).
- Both layers: a member with a meaning on Core nodes lands on the Core twin too (`Set*`).
- 1.x keeps the 1.0 API. Parity changes that would break it are listed under [Decisions](#decisions), not made silently.
- Simple and correct first, then fast. A feature a page does not use costs nothing per frame (`scripts/bench.sh --baseline` against 1.1.0).

## Gap inventory

Measured on MAUI 10.0.120 by comparing the public members of 40 MAUI controls (by reflection, now [R1](#phase-r--api-parity-sweep)) with their drawn counterparts, and from the controls' "Differences from MAUI" sections.

| Area | Gap | Phase |
| --- | --- | --- |
| Drawn layouts and content hosts (done in R2) | `CascadeInputTransparent` does not exist on them. [Migration.md](../Migration.md#5-convert-gestures-and-touch-behaviors) says "same meaning", but XAML that sets it does not compile. `IsClippedToBounds` is named `ClipToBounds` | R |
| MAUI 10 additions | `Switch.OffColor`; `CheckBox.Command` / `CommandParameter`; `SafeAreaEdges` on layouts, `ContentView`, `Border`, `ScrollView` | R, S |
| Small members | `StrokeDashPattern` (shapes, `Border`); `IsOpaque` (`Image`, `ImageButton`); `Padding` (`IndicatorView`, `RefreshView`); the read-only bindable `IsPressed`, `IsLoading` and `IsDragging` (for triggers); `CollectionView.UpdateSelectedItems` | R |
| `GestureRecognizers` | Only taps run (1 or 2 taps, primary button). Pan, swipe, pinch and pointer recognizers are reported and not run, and so are taps with `Buttons="Secondary"` | G |
| Templates | `{TemplateBinding}` / `RelativeSource TemplatedParent` do not resolve in drawn templates (MAUI resolves them through its internal `IControlTemplated`): converted to `RelativeSource AncestorType` by the checker. `IndicatorView.IndicatorTemplate` / `IndicatorLayout` are missing | T |
| CollectionView | `ItemsLayout` (`LinearItemsLayout`, `GridItemsLayout`, `"VerticalGrid, 2"`), `ItemSizingStrategy`, MAUI's `ScrollTo(…)` and `ScrollToRequested`, `ItemsUpdatingScrollMode`, a string `Header` / `Footer` / `EmptyView`, snap points | V |
| CarouselView | `ItemsLayout`, `HorizontalScrollBarVisibility` / `VerticalScrollBarVisibility`, `ItemsUpdatingScrollMode`, `ScrollToRequested` | V |
| Platform-specifics | Attached `ios:` / `android:` / `windows:` properties compile on drawn controls but are ignored, including the ones with a drawn meaning: the checker converts those to SkiaUi's members | S |
| Member types | CollectionView `Scrolled` (`ScrolledEventArgs`, MAUI: `ItemsViewScrolledEventArgs`), `SelectionChanged` (`SkUiSelectionChangedEventArgs`, MAUI: `SelectionChangedEventArgs`), `SelectionMode` (`SkUiSelectionMode`) | Held for 2.0 ([D1](#decisions)) |

Not in 1.2: drag and drop and context flyouts (not planned); the calendar and runtime SVG (backlog, new controls); text selection in labels (MAUI's `Label` has none either); collection view reordering (`CanReorderItems`, moved to [1.3](#moved-to-13)); what the [scope review](#scope-review) dropped. The parity test's allowlist ([R1](#phase-r--api-parity-sweep)) lists the MAUI members that stay out on purpose. These include `On<T>()`, because MAUI's platform-specific extension methods are typed to its own control classes (the XAML form is covered in [S2](#phase-s--safe-area-and-platform-specifics)). They also include handler plumbing (`Send*`, `Update*`, `CrossPlatformMeasure` / `CrossPlatformArrange`, `ResolveControlTemplate`) and `Shape.GetPath()`.

## Phase R — API parity sweep

First, because the parity test then guards every later step. Size: S–M.

- **R1 MAUI API parity test** (done). `MauiApiParityTests` reflects 40 MAUI controls and their drawn counterparts, and MAUI's own properties against the Core twins. It fails when a public MAUI property, event, method overload or bindable property field is missing from the drawn type, or when its type cannot be used as MAUI's: a property must read as MAUI's type and take MAUI's values, and an event's arguments must be MAUI's or derive from them. Members MAUI hides with `EditorBrowsable(Never)` are handler plumbing and are not compared. Every other difference has an entry: kept on purpose with a reason, or planned with the step that closes it. A step removes its entries, and an entry that no longer matches a difference fails. On a new MAUI 10.0.x service release the test lists what MAUI added.
- **R2 Input and clipping on drawn containers** (done).
  - `CascadeInputTransparent` with MAUI's meaning on `SkUiLayout`, `SkUiContentView` and the other containers MAUI has it on (scroll view, radio button, swipe view, refresh view, indicator view, content presenter, swipe item view, and the expander, the toolkit's being a content view), and on the Core panels, content views and indicator view (`SetCascadeInputTransparent`). With the default `true`, `InputTransparent` covers the whole subtree, as before. With `false`, only the container lets touches through (`ISkUiInputNode.HitTestsSelf`): its children still hit-test and take keyboard focus, and its own recognizers, hover, tooltip, wheel and scrolling are off. Unlike MAUI, a cascading container does not coerce its children's own `InputTransparent`: MAUI does that through an internal interface (`IInputTransparentContainerElement`), and what the children receive is the same. Semantics hit tests do not read input transparency, so screen readers are unchanged.
  - `IsClippedToBounds` on the same containers: the same bindable property as `ClipToBounds`, with change notifications under both names ([D3](#decisions)).
  - The Migration.md rows and the migration skill are corrected. The XAML checker no longer reports `IsClippedToBounds`.
- **R3 MAUI 10 members** (done).
  - `Switch.OffColor`: the track color while off, passed to the look's switch paint (`SkUiSwitchPaint.OffTrack`, so every look uses it); `null`, MAUI's default, keeps the color scheme's off track. Core: `SetOffColor`.
  - `CheckBox.Command` / `CommandParameter`: run when `IsChecked` changes (tap or code), after `CheckedChanged`, if the command can execute then. A command that cannot execute disables the check box, as in MAUI, through `CanReceiveTap` as on drawn buttons, so `IsEnabled` keeps its value. Core: `SetCommand`.
- **R4 Small members** (done).
  - `StrokeDashPattern`: read-only, from `StrokeDashArray`, a new `float[]` per read as in MAUI (`SkUiStrokeStyle.DashPattern`, also what painting uses). `float[]` on `SkUiBorder` too, where MAUI's is `float[]?`: it is never null.
  - `IsOpaque` on images (and so image buttons): accepted, a hint only. Not on Core images.
  - `Padding` on the indicator views (in the shared indicator model: measure, paint and hit test, on both layers) and on the refresh view (forwarded to its content part; the pull indicator keeps the whole view).
  - Read-only bindable properties for the state MAUI exposes to triggers and bindings: `IsPressedProperty` (buttons), `IsLoadingProperty` (images), `IsDraggingProperty` (carousel). Each is set once per change, never per frame. The scroll view's `ScrollX` / `ScrollY` / `ContentSize` stay CLR properties, because a bindable property would cost a `SetValue` per scroll frame.
  - `CollectionView.UpdateSelectedItems(IList<object>)`: replaces the selection in one change.
  - `SwipeView.OpenRequested` / `CloseRequested` and the carousel's `AnimateCurrentItemChanges` / `AnimatePositionChanges` are handler plumbing that MAUI hides (`EditorBrowsable(Never)`), so they stay out. `Open` / `Close` and `IsScrollAnimated` are already drawn.

Tests: the parity test; hit tests for cascading; `ImplicitStyleTests` for each new bindable property; SkUi-vs-Core pixel tests for `OffColor`.

## Phase G — MAUI gesture recognizers on drawn views (done)

Open since 1.0 as P11b ([EventMechanism.md](EventMechanism.md#maui-gesture-recognizers)). The tap bridge (`SkUiMauiTaps`) becomes a bridge for every recognizer type. A recognizer in a drawn view's `GestureRecognizers` becomes an arena member of that view while it is in the collection; changes to the collection apply from the next press, as for taps today. Views without recognizers allocate nothing. Size: M.

- **G1 `SwipeGestureRecognizer`:** `Direction` (flags; MAUI's default, none, never swipes), `Threshold` (MAUI's default of 100), `Command` / `CommandParameter`, and `Swiped` through MAUI's public `SendSwiped(view, direction)`. Runs on the drawn swipe recognizer, so a fast flick past the touch slop also swipes, as on iOS.
- **G2 `PanGestureRecognizer`:** `PanUpdated` through the public `IPanGestureController` (`SendPanStarted` / `SendPan` / `SendPanCompleted` / `SendPanCanceled`, one gesture id per pan from `PanGestureRecognizer.CurrentId`, as MAUI's platform code). `TotalX` / `TotalY` in DIPs. Arena rules are those of the drawn `PanUpdated`: it claims past the touch slop and the innermost claimant wins, so a pan inside a drawn scroller takes the drags that start on it. `TouchPoints` other than 1 is not run (reported).
- **G3 `PinchGestureRecognizer`:** through the public `IPinchGestureController`. `ScaleOrigin` is a 0–1 fraction of the view, as in MAUI; the drawn `PinchUpdated` keeps DIPs.
- **G4 `PointerGestureRecognizer`:**
  - `PointerEntered` / `PointerExited` / `PointerMoved` come from hover routing (`ISkUiInputNode.PointerHovered`), and `PointerPressed` / `PointerReleased` from presses, through an arena member that observes and never claims (a press another recognizer takes ends with `PointerReleased`). Also `Buttons`, the commands and their parameters. `GetPosition` maps to the view or any element drawn on the same surface; `GetPosition(null)` (the window) is `null`.
  - MAUI's `Send*` methods are internal. They are reached through `UnsafeAccessor` (AOT-safe), as the tap bridge reaches `SendTapped`: probed once, falling back to the commands. A test fails if MAUI changes the signatures.
- **G5 Taps:** `Buttons="Secondary"` and `"Primary, Secondary"`, from the `SkUiPointerButtons` the surfaces already report (mouse and pen on Windows, Mac Catalyst, the iPad pointer, an Android mouse); a press of a button no recognizer wants leaves the tap to the ancestors, while SkiaUi's own `Tapped` still takes every button. `NumberOfTapsRequired` above 2: the drawn tap recognizer counts a sequence up to the most taps a recognizer wants and raises it once with its count.
- **G6 What stays native:** recognizers on a surface root (MAUI runs them) and platform behaviors. The one-time trace line now names only drag and drop, pans with several touch points, taps with no count or no primary or secondary button, and platform behaviors. The migration checker reports the same.

Tests: each recognizer through the arena with synthetic pointers, also inside drawn scroll views and collection items; the samples in MAUI's gesture docs (a pan container, a pinch-to-zoom container), with only the prefix changed. Demo: a gesture page with native and drawn views side by side.

## Phase V — Collection and carousel API by prefix (done)

The collection view's API is SkiaUi's own today ([Migration.md](../Migration.md#lists-collectionview)). Each step adds MAUI's member over the existing engine; the SkiaUi properties stay. Size: M.

- **V1 `ItemsLayout`** (done) on the collection view and the carousel. It takes MAUI's `LinearItemsLayout` / `GridItemsLayout` and the converter strings (`"VerticalList"`, `"HorizontalGrid, 3"`, through MAUI's `ItemsLayoutTypeConverter`). It is a source the view reads, not a writer of other properties ([D4](#decisions)): once the app sets it, it decides the orientation, span and spacings (the carousel: orientation, spacing, `SnapPointsType`, `SnapPointsAlignment`), and the snap points of lists once V4 draws them. `Orientation`, `Span`, `ItemSpacing` / `SpanSpacing` keep their own values and apply when it is not set or cleared; setting both is reported once. It follows the layout object's changes through a weak listener (layouts are often shared, `LinearItemsLayout.Vertical` is static). The carousel's property is MAUI's `LinearItemsLayout` with MAUI's `CarouselLayoutTypeConverter`.
- **V2 `ItemSizingStrategy`** (done): `MeasureAllItems` (the default) or `MeasureFirstItem`, which uses the first row's measured size as the fixed extent in the virtual stack (`SizesFromFirstItem`), measured again whenever the sizes are forgotten (a new width, spacing, orientation). `ItemExtent` wins when set. Grouped lists measure every row, since their group headers are rows too.
- **V3 MAUI's `ScrollTo`** (done): `ScrollTo(index, groupIndex = -1, position, animate)` and `ScrollTo(item, group = null, position, animate)` (void, as in MAUI), plus `ScrollToRequested` with `ScrollToRequestEventArgs`, on both views. The awaitable `ScrollToIndex` / `ScrollToItem` stay.
- **V4 Snap points on the items layout** (done): `SnapPointsType` (`None`, `Mandatory`, `MandatorySingle`) and `SnapPointsAlignment` on lists and grids in both orientations. The list's body is the scroll engine's `ISkUiSnapPointSource`: the virtual stack lists the rows in reach lined up by the alignment (`AddSnapOffsets`, through the same offsets as `ScrollToIndex`, so sticky insets count), plus both ends of the list so a header or footer stays reachable. The fling's end is chosen as it starts and the render-thread spring runs to it, with no re-recording.
- **V5 `ItemsUpdatingScrollMode`** (done): `KeepItemsInView` (the default), `KeepScrollOffset` and `KeepLastItemInView`, on both views, built on the existing scroll anchoring (`KeepScrollOffset` turns the anchoring of collection changes off; `KeepLastItemInView` scrolls to the last row once the scroller laid the change out). The carousel always kept the current item; that became `KeepItemsInView`. MAUI's carousel handlers go back to the first item for `KeepItemsInView`; the drawn carousel keeps 1.x's behavior, which the name describes, and the docs say so.
- **V6 Text in `Header` / `Footer` / `EmptyView`** (done): `Header="Contacts"` and `EmptyView="No results"` show as in MAUI, through a type converter on the properties (`SkUiTextViewTypeConverter`; the empty view's label centered), on both views (the carousel's `EmptyView`). The properties keep their 1.0 type `ISkUiView` ([D5](#decisions)). Data headers (`Header="{Binding Info}"` with a `HeaderTemplate`) convert: the template binds through the list's binding context (`{Binding Info.Title}`), which the checker reports.
- **V7 Carousel scroll bars** (done): `HorizontalScrollBarVisibility` / `VerticalScrollBarVisibility` on the strip. `Default` shows none, as 1.x did (MAUI's is the platform's: none on Android, the indicator on iOS); a looping strip never shows one, as in MAUI. `VisibleViews` is in the parity test's allowlist (rarely used; the backlog keeps it).

Tests: the XAML samples in MAUI's CollectionView and CarouselView docs (layouts, scrolling, snap points, headers and footers, empty views) with only the prefix changed; scroll-anchoring tests for each `ItemsUpdatingScrollMode`. Bench: the existing list scenarios do not move, plus a new scenario with an `ItemsLayout` and snap points.

## Phase T — Templates (done)

Size: S.

- **T3 `IndicatorView.IndicatorTemplate` / `IndicatorLayout`** (done): one drawn template view per indicator in a stack of the indicator view's own, as MAUI's model (bound to its item, or its index without `ItemsSource`, since a linked carousel sets only `Count`), with the selected one marked as MAUI marks it (the indicator colors as backgrounds, the `Selected` visual state). An app's drawn layout as `IndicatorLayout` is shown as it is. The look's painter stays the default and the cheaper path. Not on the Core twin (Core nodes are not templated).
- `TemplateBinding` and MAUI's default radio template are conversions, not steps ([scope review](#scope-review)): the checker rewrites `{TemplateBinding X}` and `RelativeSource TemplatedParent` to `RelativeSource AncestorType` ([Migration.md](../Migration.md#templated-controls)), and `RadioButton.DefaultTemplate` goes into the parity test's allowlist.

## Phase S — Safe area and platform-specifics (done)

Size: S–M.

- **S1 `SafeAreaEdges` on the surface root** (MAUI 10; done): an `SkUiLayout` or `SkUiContentView` that is a surface of its own takes MAUI's `SafeAreaEdges` and its default (MAUI's: `Container` on layouts, `None` on content views; the root ignored the safe area in 1.x). MAUI applies it through an internal interface (`ISafeAreaView2`), so the root pads itself by the window's insets per edge, from the metrics the picker already reads; `SoftInput` uses the keyboard inset that keyboard avoidance already tracks. Drawn views inside a surface accept the property and ignore it, as documented: padding them by their overlap with the insets would make layout depend on where the view is in the window, and every scroll or move would lay it out again. Built as planned: the root refreshes the padding when its native parent arranges it (not in its own relayouts), asking the platform only when an edge keeps clear; the keyboard inset comes from the focus manager's keyboard top (iOS and Mac Catalyst), and the focused field is revealed again after the root made room. Swipe and refresh views accept the property and do not apply it. **Decided:** opt-in. MAUI's layouts default to `Container`, but taking that default would pad every drawn layout root that went edge to edge in 1.x (and double-pad the ones padded by hand), so every drawn container defaults to `None`; `SafeAreaEdges="Container"` gives MAUI's behavior, and the migration docs and skill say to set it where the replaced MAUI layout kept clear of the bars.
- **S2 Platform-specifics** (done, in `check_xaml.py --fix`): no runtime reading. The checker converts the ones with a SkiaUi member (iOS `Entry.CursorColor` → `CaretColor`, Android `Entry.ImeOptions` → `ReturnType`, iOS pickers' `UpdateMode` → `UpdateMode`, iOS `Entry.AdjustsFontSizeToFitWidth` → shrink to fit: the drawn entry has no shrink to fit, so the checker reports it with advice instead), inside `OnPlatform` when the app wants it on one platform only, and reports the rest as ignored. Drawn equivalents of the others (a tap that moves a slider's thumb, swipe transition modes, ripple color, search bar chrome, pull direction) are SkiaUi members on both layers when an app needs them, in the [backlog](Roadmap.md#backlog-1x), not attached properties.

## Scope review

The first plan also had these steps. They were dropped because MAUI does not expose what they port, or because an existing mechanism covers them at no cost to the architecture:

- **Image brushes (phase B).** MAUI 10's `ImageBrush` is internal (not in its public API), so no MAUI XAML or C# creates one; a brush only arrives as one through the Graphics `ImageSourcePaint` conversion, which MAUI itself draws only as a page background on iOS. The "not drawn" trace line and the docs now say that MAUI has no public `ImageBrush` and point to an `SkUiImage` behind the content.
- **A `{sk:TemplateBinding}` markup extension (T1).** MAUI marks `TemplateBinding` obsolete in favor of `RelativeSource TemplatedParent`, and that resolves only through MAUI's internal `IControlTemplated`, so no public route exists. A markup extension of SkiaUi's own would still mean editing the XAML, and would be a second binding resolution that has to follow reparenting; `RelativeSource AncestorType` already does both ([D2](#decisions)).
- **A drawn `RadioButton.DefaultTemplate` (T2).** Apps that restyle a copy of MAUI's template port already: a `ControlTemplate` of drawn ellipses and visual states works on `SkUiRadioButton`.
- **Reading platform-specifics at runtime (S2's first form).** Three of them already have a cross-platform SkiaUi member, which would get a second source of the same state and a precedence rule in every change path; the others are new features that belong on both layers.
- **Padding nested drawn views by the safe area (S1's first form)** and **`VisibleViews` on the carousel (V7)**: see S1 and V7.

### Moved to 1.3

- **V8 Reordering:** `CanReorderItems`, `ReorderCompleted`, `CanMixGroups`. A new feature rather than a port by prefix, and the only L-sized step, so it no longer holds 1.2 back.
  - Lifting: a long press (touch) or a press and drag (mouse) lifts the item. Its render node follows the pointer at composite time, the other items move aside with render-thread animations, and the list auto-scrolls at its edges.
  - Dropping: the drop moves the item in an `IList` source (`ObservableCollection.Move`); between groups only when `CanMixGroups` is set.
  - Accessibility: screen-reader move up / move down actions.
  - The design goes into [ScrollingAndCollectionViews.md](ScrollingAndCollectionViews.md) before the code; a leak scenario for reordering.

## Docs, migration and demo

Done with each step, not at the end:

- [Migration.md](../Migration.md): the parity table checked against MAUI 10.0.120 (its header says 10.0.110), the gesture and collection view sections, `ImageBrush` (none public in MAUI), templated controls and platform-specifics (the checker's conversions), and "Not available yet". The migration skills' references: `gestures.md`, `collection-view.md`, `controls.md`, `gaps.md`; the checker's rewrites for `TemplateBinding`, data headers and platform-specifics.
- The control pages' "Differences" tables; [EventMechanism.md](EventMechanism.md) (P11b), [ScrollingAndCollectionViews.md](ScrollingAndCollectionViews.md) (items layout, snap points).
- The demo's component gallery: MAUI gesture recognizers next to native views, a collection view page for items layouts and snap points.
- [Roadmap.md](Roadmap.md): items leave the backlog as they ship. [Requirements.md](Requirements.md): status. [CHANGELOG.md](../../CHANGELOG.md): an entry under `## Unreleased` per feature.

## Order

| # | Steps | Size | Why here |
| --- | --- | --- | --- |
| 1 | R1–R4 (done) | S–M | The parity test guards everything after it; the CascadeInputTransparent row in the docs was wrong |
| 2 | G1–G6 (done) | M | Gesture recognizers are the most common conversion in ported pages |
| 3 | V1–V3, V6 (done) | M | The collection view's MAUI shape, so list XAML ports by prefix |
| 4 | The checker's conversions: `TemplateBinding`, data headers, platform-specifics; the `ImageBrush` docs (done) | S | What the scope review turned into conversions |
| 5 | V4, V5, V7 (done) | M | On top of V1 |
| 6 | S1 (done) | S–M | Independent |
| 7 | T3 (done) | S | Lower demand |

Independent tracks that can run in parallel: the conversions; V; S; T.

## Decisions

- **D1 Member types that differ from MAUI's.** Three 1.0 members cannot take MAUI's type without breaking the 1.x API:
  - `SkUiCollectionView.Scrolled`: `ScrolledEventArgs`. MAUI's `ItemsViewScrolledEventArgs` (deltas, first / center / last visible index) could be filled by the drawn list.
  - `SelectionChanged`: `SkUiSelectionChangedEventArgs`. MAUI's `SelectionChangedEventArgs` has internal constructors, so it would have to be raised through an accessor, and `PreviousItem` / `CurrentItem` would move elsewhere.
  - `SelectionMode`: `SkUiSelectionMode`, which adds `SingleDeselect`. XAML already ports; C# that names `SelectionMode.Single` does not.

  **Decided:** 1.2 stays non-breaking. The conversions are in [Migration.md](../Migration.md#lists-collectionview) and the migration skill's `collection-view.md`, and the three changes are collected for 2.0 in the [roadmap](Roadmap.md#held-for-20). V1–V7 add no member whose type would need one of them; a step that finds another such member adds it to that list instead of breaking 1.x.
- **D2 The `TemplateBinding` mechanism. Decided:** a conversion, `RelativeSource AncestorType`, rewritten by the checker. MAUI's markup cannot resolve on drawn controls without its internal `IControlTemplated`, MAUI itself marks `TemplateBinding` obsolete, and a markup extension of SkiaUi's own would not spare the XAML edit ([scope review](#scope-review)).
- **D3 `IsClippedToBounds` and `ClipToBounds`:** both names stay. Built in R2 as recommended: MAUI's name on the containers, as an alias of `ClipToBounds`, which stays the drawn name on every view (leaf controls, where MAUI has no member, included).
- **D4 `ItemsLayout` is read, not written through.** Writing the layout object's values into `Orientation`, `Span` and the spacings made XAML attribute order matter (`ItemSpacing="8" ItemsLayout="VerticalList"` ended with spacing 0, the other order with 8) and replaced values the app set. Set, the layout decides; the view's own properties keep their values for when it is cleared.
- **D5 `Header` / `Footer` / `EmptyView` keep `ISkUiView`.** Taking `object` in the bindable properties left the CLR properties reading `null` while the property held data, through all of 1.x, and added a 2.0 change. Text, the common case in MAUI's docs, comes through a type converter; data converts to a template bound through the list's context. Nothing about them is held for 2.0.

## Acceptance

- `MauiApiParityTests` passes, and every allowlist entry gives a reason.
- The XAML samples in MAUI's docs for gestures, CollectionView, CarouselView and `ControlTemplate` load, bind and behave in headless tests with only the prefix changed, plus the documented conversions (D1, D2, D5). No 1.0 member changes type or goes away (D1).
- 0 build warnings with the trim and AOT analyzers; the headless suite and the memory-leak scenarios pass.
- `scripts/bench.sh --baseline 1.1.0` shows no regression in the existing scenarios.
- Device runs: gesture recognizers on Android, iOS, Mac Catalyst and Windows; snap points on a phone; `SafeAreaEdges` on a notched iPhone and an Android phone with edge-to-edge (added to the [verification backlog](Roadmap.md#verification-backlog) if they are still open at release).
