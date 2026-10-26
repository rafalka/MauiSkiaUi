# SkiaUi 1.2 implementation plan: MAUI parity

**Goal:** more MAUI pages port by changing the XAML prefix. The MAUI XAML that has to be converted member by member today (gesture recognizers, the collection view's items layout and scroll API, control templates with `TemplateBinding`, image brushes, MAUI 10's new members) loads and behaves as on MAUI.

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
| Templates | `{TemplateBinding}` / `RelativeSource TemplatedParent` do not resolve in drawn templates (MAUI resolves them through its internal `IControlTemplated`). `RadioButton.DefaultTemplate` builds MAUI views. `IndicatorView.IndicatorTemplate` / `IndicatorLayout` are missing | T |
| CollectionView | `ItemsLayout` (`LinearItemsLayout`, `GridItemsLayout`, `"VerticalGrid, 2"`), `ItemSizingStrategy`, MAUI's `ScrollTo(…)` and `ScrollToRequested`, `ItemsUpdatingScrollMode`, a string or object `Header` / `Footer` / `EmptyView`, snap points, `CanReorderItems` / `ReorderCompleted` / `CanMixGroups` | V |
| CarouselView | `ItemsLayout`, `HorizontalScrollBarVisibility` / `VerticalScrollBarVisibility`, `ItemsUpdatingScrollMode`, `VisibleViews`, `ScrollToRequested` | V |
| Brushes | `ImageBrush` is not drawn (backgrounds, shape fill and stroke, border stroke) | B |
| Platform-specifics | Attached `ios:` / `android:` / `windows:` properties compile on drawn controls but are ignored, including the ones with a drawn meaning | S |
| Member types | CollectionView `Scrolled` (`ScrolledEventArgs`, MAUI: `ItemsViewScrolledEventArgs`), `SelectionChanged` (`SkUiSelectionChangedEventArgs`, MAUI: `SelectionChangedEventArgs`), `SelectionMode` (`SkUiSelectionMode`) | Held for 2.0 ([D1](#decisions)) |

Not in 1.2: drag and drop and context flyouts (not planned); the calendar and runtime SVG (backlog, new controls); text selection in labels (MAUI's `Label` has none either). The parity test's allowlist ([R1](#phase-r--api-parity-sweep)) lists the MAUI members that stay out on purpose. These include `On<T>()`, because MAUI's platform-specific extension methods are typed to its own control classes (the XAML form is covered in [S2](#phase-s--safe-area-and-platform-specifics)). They also include handler plumbing (`Send*`, `Update*`, `CrossPlatformMeasure` / `CrossPlatformArrange`, `ResolveControlTemplate`) and `Shape.GetPath()`.

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

## Phase V — Collection and carousel API by prefix

The collection view's API is SkiaUi's own today ([Migration.md](../Migration.md#lists-collectionview)). Each step adds MAUI's member over the existing engine; the SkiaUi properties stay. Size: M, plus L for V8.

- **V1 `ItemsLayout`** (done) on the collection view and the carousel. It takes MAUI's `LinearItemsLayout` / `GridItemsLayout` and the converter strings (`"VerticalList"`, `"HorizontalGrid, 3"`, through MAUI's `ItemsLayoutTypeConverter`). It writes `Orientation`, `Span`, `ItemSpacing` / `SpanSpacing` (the carousel: `Orientation`, `ItemSpacing`, `SnapPointsType`, `SnapPointsAlignment`), and the snap points of lists once V4 draws them. It follows the layout object's changes through a weak listener (layouts are often shared, `LinearItemsLayout.Vertical` is static). Rule: the last one set wins, whether the layout object or a property. The carousel's property is MAUI's `LinearItemsLayout` with MAUI's `CarouselLayoutTypeConverter`.
- **V2 `ItemSizingStrategy`** (done): `MeasureAllItems` (the default) or `MeasureFirstItem`, which uses the first row's measured size as the fixed extent in the virtual stack (`SizesFromFirstItem`), measured again whenever the sizes are forgotten (a new width, spacing, orientation). `ItemExtent` wins when set. Grouped lists measure every row, since their group headers are rows too.
- **V3 MAUI's `ScrollTo`** (done): `ScrollTo(index, groupIndex = -1, position, animate)` and `ScrollTo(item, group = null, position, animate)` (void, as in MAUI), plus `ScrollToRequested` with `ScrollToRequestEventArgs`, on both views. The awaitable `ScrollToIndex` / `ScrollToItem` stay.
- **V4 Snap points on the items layout:** `SnapPointsType` (`None`, `Mandatory`, `MandatorySingle`) and `SnapPointsAlignment` on lists and grids in both orientations. The virtual stack uses the scroll engine's snap points: the fling's end is chosen on the render thread, with no re-recording.
- **V5 `ItemsUpdatingScrollMode`:** `KeepItemsInView` (the default), `KeepScrollOffset` and `KeepLastItemInView`, on both views, built on the existing scroll anchoring. The carousel always keeps the current item today; that becomes `KeepItemsInView`.
- **V6 Data in `Header` / `Footer` / `EmptyView`** (done): shown as in MAUI. A string becomes a drawn label (the empty view's centered); an object with a template uses that template with the object as its binding context. Applies to both views (the carousel's `EmptyView`). A type converter would not cover bindings, so the bindable properties take `object` (binary-compatible: the fields stay `BindableProperty`, and nothing could set a non-view before), refusing native views as before, and the content slot builds the view for data. The CLR properties keep the 1.0 type `ISkUiView` (they read `null` for data): C# that assigns data uses `SetValue`, and the change to `object` joins the [held for 2.0](Roadmap.md#held-for-20) list ([D1](#decisions)).
- **V7 Carousel members:**
  - Scroll bars on the strip.
  - `VisibleViews`: kept up to date only once read or observed, so carousels that never use it pay nothing.
- **V8 Reordering:** `CanReorderItems`, `ReorderCompleted`, `CanMixGroups`. The largest step, and it may slip to 1.3.
  - Lifting: a long press (touch) or a press and drag (mouse) lifts the item. Its render node follows the pointer at composite time, the other items move aside with render-thread animations, and the list auto-scrolls at its edges.
  - Dropping: the drop moves the item in an `IList` source (`ObservableCollection.Move`); between groups only when `CanMixGroups` is set.
  - Accessibility: screen-reader move up / move down actions.
  - The design goes into [ScrollingAndCollectionViews.md](ScrollingAndCollectionViews.md) before the code.

Tests: the XAML samples in MAUI's CollectionView and CarouselView docs (layouts, scrolling, snap points, headers and footers, empty views) with only the prefix changed; scroll-anchoring tests for each `ItemsUpdatingScrollMode`; a leak scenario for reordering. Bench: the existing list scenarios do not move, plus a new scenario with an `ItemsLayout` and snap points.

## Phase T — Templates

Size: M.

- **T1 `TemplateBinding` in drawn control templates.** MAUI resolves `{TemplateBinding}` and `RelativeSource TemplatedParent` by walking parents to an `IControlTemplated`, an internal interface that drawn controls cannot implement.
  - Spike first: can the compiled and runtime XAML bindings be resolved for drawn templates without MAUI internals? If not, SkiaUi adds its own markup extension, `{sk:TemplateBinding Path}`. It resolves the templated control by MAUI's rule: the nearest ancestor with a `ControlTemplate`, skipping one for each content presenter on the way. MAUI's `TemplateBinding` options (converter, mode, string format) are supported.
  - Acceptance: the `ControlTemplate` samples in MAUI's docs (a card view) work with at most the prefix added to `TemplateBinding`. [D2](#decisions).
- **T2 A drawn `RadioButton` default template:** `SkUiRadioButton.DefaultTemplate`, a drawn template that looks like the look's radio button. Apps that restyle a copy of MAUI's default template (ellipses, `Checked` / `Unchecked` states) then port.
- **T3 `IndicatorView.IndicatorTemplate` / `IndicatorLayout`:** one drawn template view per indicator, as MAUI's model, with the selected one marked as MAUI marks it. The look's painter stays the default and the cheaper path.

## Phase B — Image brushes

Size: M.

- **B1 `ImageBrush`** drawn as an image shader wherever SkiaUi draws a brush: view `Background`, `SkUiBox`, the border's fill and stroke, shape `Fill` / `Stroke`, label and button chrome. The image comes through the shared loader and cache, with one lease per view that uses it. Loading the image re-records only those views. Scaling follows MAUI's (checked against MAUI on each platform). The "not drawn" trace line goes.
- **B2 Core:** an image paint for Core nodes (`SetBackground` with an image source, shapes' fill and stroke).

## Phase S — Safe area and platform-specifics

Size: M.

- **S1 `SafeAreaEdges`** (MAUI 10) on drawn layouts, content views, the border and the scroll view. Spike first: MAUI applies it through an internal interface (`ISafeAreaView2`). The surface root may get MAUI's own handling through it; if not, SkiaUi pads the root itself. Nested drawn views pad by the part of the window's safe-area insets they overlap (the picker's bottom pane already reads them). `SoftInput` uses the keyboard inset that keyboard avoidance already tracks.
- **S2 Platform-specifics with a drawn meaning,** read as the attached properties they are, on their platform only, as in MAUI. SkiaUi's own cross-platform member wins when it is set.

  | Platform-specific | Drawn meaning |
  | --- | --- |
  | iOS `Entry.CursorColor` | `CaretColor` |
  | iOS `Entry.AdjustsFontSizeToFitWidth` | Shrink to fit on a single-line field |
  | iOS `Picker` / `DatePicker` / `TimePicker.UpdateMode` | `UpdateMode` |
  | iOS `Slider.UpdateOnTap` | A tap on the track moves the thumb |
  | iOS / Android `SwipeView.SwipeTransitionMode` | `Reveal` / `Drag` of the items |
  | iOS `SearchBar.SearchBarStyle` | Minimal / prominent field chrome |
  | Android `Entry.ImeOptions` | `ReturnType` |
  | Android `Button.RippleColor` / `ImageButton.RippleColor` | The press ripple's color |
  | Windows `RefreshView.RefreshPullDirection` | Pulling from other edges ([roadmap](Roadmap.md#lists-and-scrolling)) |

  The rest (native shadows, `Elevation`, access keys, legacy color mode, blur) stay ignored, and the control pages list them.

## Docs, migration and demo

Done with each step, not at the end:

- [Migration.md](../Migration.md): the parity table checked against MAUI 10.0.120 (its header says 10.0.110), the gesture and collection view sections, and "Not available yet". The migration skills' references: `gestures.md`, `collection-view.md`, `controls.md`, `gaps.md`.
- The control pages' "Differences" tables; [EventMechanism.md](EventMechanism.md) (P11b), [ScrollingAndCollectionViews.md](ScrollingAndCollectionViews.md) (items layout, snap points, reordering), [RenderingPipeline.md](RenderingPipeline.md) (image brushes, if they change ownership rules).
- The demo's component gallery: MAUI gesture recognizers next to native views, a collection view page for items layouts, snap points and reordering, `ImageBrush` on the brushes page.
- [Roadmap.md](Roadmap.md): items leave the backlog as they ship. [Requirements.md](Requirements.md): status. [CHANGELOG.md](../../CHANGELOG.md): an entry under `## Unreleased` per feature.

## Order

| # | Steps | Size | Why here |
| --- | --- | --- | --- |
| 1 | R1–R4 | S–M | The parity test guards everything after it; the CascadeInputTransparent row in the docs is wrong today |
| 2 | G1–G6 | M | Gesture recognizers are the most common conversion in ported pages |
| 3 | V1–V3, V6 | M | The collection view's MAUI shape, so list XAML ports by prefix |
| 4 | T1 (spike, then the build) | M | `TemplateBinding` blocks porting templated controls |
| 5 | B1–B2 | M | Independent of the rest; can run alongside 3–4 |
| 6 | V4, V5, V7 | M | On top of V1 |
| 7 | S1 (spike, then the build), S2 | M | Independent |
| 8 | T2, T3 | S–M | Lower demand |
| 9 | V8 | L | The largest step; ships in 1.2 if ready, otherwise 1.3 |

Independent tracks that can run in parallel: R and B; G; V; T and S. V8 starts only after V1–V5.

## Decisions

- **D1 Member types that differ from MAUI's.** Three 1.0 members cannot take MAUI's type without breaking the 1.x API:
  - `SkUiCollectionView.Scrolled`: `ScrolledEventArgs`. MAUI's `ItemsViewScrolledEventArgs` (deltas, first / center / last visible index) could be filled by the drawn list.
  - `SelectionChanged`: `SkUiSelectionChangedEventArgs`. MAUI's `SelectionChangedEventArgs` has internal constructors, so it would have to be raised through an accessor, and `PreviousItem` / `CurrentItem` would move elsewhere.
  - `SelectionMode`: `SkUiSelectionMode`, which adds `SingleDeselect`. XAML already ports; C# that names `SelectionMode.Single` does not.

  **Decided:** 1.2 stays non-breaking. The conversions are in [Migration.md](../Migration.md#lists-collectionview) and the migration skill's `collection-view.md`, and the three changes are collected for 2.0 in the [roadmap](Roadmap.md#held-for-20). V1–V7 add no member whose type would need one of them; a step that finds another such member adds it to that list instead of breaking 1.x.
- **D2 The `TemplateBinding` mechanism** (open), after the T1 spike: MAUI's markup if it can be made to resolve, else `{sk:TemplateBinding}`.
- **D3 `IsClippedToBounds` and `ClipToBounds`:** both names stay. Built in R2 as recommended: MAUI's name on the containers, as an alias of `ClipToBounds`, which stays the drawn name on every view (leaf controls, where MAUI has no member, included).

## Acceptance

- `MauiApiParityTests` passes, and every allowlist entry gives a reason.
- The XAML samples in MAUI's docs for gestures, CollectionView, CarouselView and `ControlTemplate` load, bind and behave in headless tests with only the prefix changed, plus the documented conversions (D1, D2). No 1.0 member changes type or goes away (D1).
- 0 build warnings with the trim and AOT analyzers; the headless suite and the memory-leak scenarios pass.
- `scripts/bench.sh --baseline 1.1.0` shows no regression in the existing scenarios.
- Device runs: gesture recognizers on Android, iOS, Mac Catalyst and Windows; snap points and reordering feel on a phone (added to the [verification backlog](Roadmap.md#verification-backlog) if they are still open at release).
