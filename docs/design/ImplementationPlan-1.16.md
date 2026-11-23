# SkiaUi 1.16 implementation plan: scroll-linked effects

**Goal:** a view follows a scroller on the render thread, with no re-recording and no UI-thread work per frame: a header that collapses as the list scrolls, a parallax image, a toolbar that fades in, a header that stretches when the list is pulled past its top, and views that hide when the list scrolls down and come back when it scrolls up anywhere (a sticky header or footer, a floating button, an extended button that shrinks to its icon). The app says which offset or which direction drives which property over which range; SkiaUi does the rest on both layers.

**Status:** proposed 23 November 2026; amended the same day with scroll-direction effects ([D10](#decisions)), clip edges and shapes ([D6](#decisions)), a corrected reduce-motion rule ([D7](#decisions)) and the [recipes](#recipes) for the use cases they serve. Implementation started on `1.16-implementation` (23 November 2026), which took the decisions as agreed: A1 done (the scroll bar thumb on the channel list); A2 done (translation, scale, opacity, clips with their edges and corners, outline shadows cast by the clip and content shadows drawn live from the clipped content, the immediate painter the same); A3 done (a pull past an edge counts as the distance shown, bounced or stretched, the same on both threads); A4 done (pointers and screen readers follow the linked opacity and clip at the UI side's offset, read when they ask, so hit-testing needs no state; refreshing the semantics tree and re-committing a linked node on each scroll report is B2's, where the effect subscribes to its scroller); A5 done (`SkUiRenderReveal`, with the refinements noted under [D10](#decisions)); B1 to B4 done (see [what phase B settled](#what-phase-b-settled)); docs (the scroll view page's recipes, the design docs, migration, changelog, FR-36) and the gallery's Scroll effects page done; headless benches done (`skui-scroll-effects`, `core-scroll-effects`, their `-none` references and `skui-scroll-reveal`; [Benchmarks.md](Benchmarks.md#116-scroll-linked-effects-23-november-2026)); B5 done (`SnapsToEnds`: ranges registered on the scroller, consulted when a drag, fling or wheel scroll comes to rest, with or without snap points; a fling that would stop inside one becomes the snap spring, others stay flings); device benches on the Galaxy S9 and the iPhone 8 done ([Benchmarks.md](Benchmarks.md#116-on-the-galaxy-s9-and-the-iphone-8-23-november-2026): no regression without effects; the event-based alternative lags two to three frames during flings on the S9, up to fifteen, and one on the iPhone 8); the feel checks by hand are open. Kept as built after the measurements (23 November 2026).

**Scope:** F2 of the [review plan](ImplementationPlan-PerformanceAndControls.md#f2-scroll-linked-effects), which this plan replaces where the two differ, plus the backlog's render-thread effects linked to overscroll ([Lists and scrolling](Roadmap.md#lists-and-scrolling)). The review's controls planned for 1.16 (F1, F5, F6) move to 1.17 ([D1](#decisions)).

**Baseline:** 1.15.1 (net10 with MAUI 10.0.130 and net11 with MAUI 11.0.10, SDK 11.0.100, SkiaSharp 4.154.0).

**Rules** (from [Requirements.md](Requirements.md#principles)):

- 1.x keeps the 1.0 API: everything here is additive. The engine change in phase A is internal.
- Both layers: one effect class, one engine, used by `SkUiView` and `SkUiCoreNode` alike.
- NFR-2: a scroll frame of a page with effects records nothing and allocates nothing on either thread.

## Decisions

Agreed when implementation started (23 November 2026).

- **D1 1.16 is scroll-linked effects alone.** The review's D10 had 1.16 as F1, F5, F6 and 1.17 as F2, F3. F2 comes first now: it is one engine change with a small public surface, and the sheet (F3) builds on it. New order: 1.16 F2; 1.17 F1, F5, F6; 1.18 F3; 1.19 F4 phase 1; then F7 to F10 as apps ask.
- **D2 One list of links per node, replacing the single translation link.** `SkUiRenderProps.Link` (`SkUiRenderLink`: translation only, one per node) becomes `Links`, an immutable array of channels, each `clamp(curve(offset))` onto one property. The scroll bar thumb, the only user today, moves to it unchanged. The carousel's `ItemEffect` stays apart: it computes a whole placement from an effect object and sorts by depth, which a list of curves cannot express.
- **D3 `SkUiScrollEffect` is a plain class, not a `BindableObject`.** It is shared by both layers, like `SkUiCarouselEffect`: `INotifyPropertyChanged`, fluent `Set*` that throw on invalid input, CLR properties settable from XAML (`Source="{x:Reference List}"`). Its values are render-thread inputs, sampled when the node commits; a change re-commits the node's props, never its picture. Data binding of `From` / `To` is not supported in 1.16 (an app that needs it replaces the effect).
- **D4 The input is the offset with overscroll.** A bounce already moves the children offset past its range; a stretch scales the children instead, so the compositor turns the scale back into the distance it shows (`SkUiOverscroll`'s inverse). An effect whose input range goes below 0 (pull past the top) therefore works in both modes. Outside `InputStart`…`InputEnd` the output holds `From` or `To`.
- **D5 How effects combine.** With the node's own values: translation adds to `TranslationX` / `TranslationY`, `Scale*` and `Opacity` multiply. Several effects on one property: translations add, scales and opacities multiply, clips take the smaller. Order of effects never matters.
- **D6 Clipping, not resizing.** `ClipHeight` / `ClipWidth` clip the node (content, children, overlay, shadow and hit-testing) to N DIPs; the node keeps its layout size and its siblings do not move. A collapsing header sits over the scroller, and the scroller's `ViewportInsets` (or its content's top padding) leave the header's expanded height. An effect that changes layout would re-measure every frame and is [not in 1.16](#not-in-116).
  - `ClipEdge` says which part stays: `Start` (default: the top / leading part), `End` (the bottom / trailing part: a button pinned to the bottom-right shrinks toward its corner) or `Center`. Leading and trailing follow `FlowDirection`.
  - `ClipShape` says how the clip's corners look: `Rectangle` (default), `RoundRectangle` with `ClipCornerRadius`, or `Capsule` (the radius is half the clip's shorter side at every frame, so a pill stays a pill while it shrinks).
  - A node's outline shadow follows the clip (the compositor draws it as the clipped rounded rectangle, with no path per frame); a content shadow is clipped with the content.
- **D7 Reduce motion: fades stay, motion goes.** Apple and Android both offer cross-fades as the replacement for motion, so `Opacity` and the clips keep running under `SkUiMotion.IsMotionReduced`. What reduce motion is for is parallax and zooming: `Scale*` effects, and translations that do not move exactly with the content, hold their `From` value (the page as designed at rest). A direction effect's animated show and hide ([D10](#decisions)) is instant for translation and scale and still fades for opacity. `ReducedMotion` on the effect (`Default`, `Run`, `HoldFrom`) overrides the rule for one effect. Read when the node commits. (The first version of this decision held `To` for fades, which would leave a button that fades out on scroll invisible for good.)
- **D8 Snapping is a cut-line step** ([B5](#b5-snap-to-the-ends-cut-line)). A collapsing toolbar that stops half open looks unfinished on Android (`CoordinatorLayout`'s `snap` flag) and iOS (large titles). It ships in 1.16 if B1 to B4 land with time left; otherwise it goes to the backlog.
- **D9 Release:** `1.16.0`, no prerelease, unless A1 shows a slower frame in the scroll benches that A2 cannot win back.
- **D10 Scroll direction is an input of its own, `SkUiScrollReveal`.** Hiding a view while the list scrolls down and showing it on any scroll up cannot be written as a function of the offset: it depends on how far the list moved since the direction last changed. A reveal is a small object that turns a scroller's movement into a progress from 0 (shown) to 1 (hidden); an effect with `Reveal` set maps that progress from `From` to `To` instead of the offset. Several effects (on several nodes) can share one reveal, so a button, its icon and its label move as one.
  - **`Tracked`** (default): the progress moves with the scroll, `Distance` DIPs from shown to hidden (Chrome's toolbar, Android's `enterAlways`). When the scroller comes to rest halfway, it settles on the nearer end (`SettlesWhenIdle`, default on).
  - **`Triggered`:** after `Threshold` DIPs (default 24) in one direction, the view animates fully in or out over `Duration` (default the look's short transition), whatever the scroll does after that (Material's bottom app bar and floating button, `HideBottomViewOnScrollBehavior`).
  - **Near the start** the view is always shown (`ShowsNearStart`, default on): the progress cannot exceed `offset / Distance`, so a header that collapses by direction is fully open whenever the list is back at its top and never leaves a gap above the first row.
  - **What is not a scroll does not count:** anchoring corrections (items inserted above) and overscroll (the offset is held between 0 and the range the UI side gives) do not move the progress. A jump of more than two viewports in one frame resets it to shown. (Built: the render thread cannot tell a `ScrollTo` without animation from a drag, both being offsets the UI set, so a short one counts as a scroll; the start band still shows the view after a `ScrollTo` the top.)
  - **The app can drive it:** `Show()` / `Hide()` (animated), and `IsShown` (UI side, raises `PropertyChanged`) for an app that also changes something else when the bar goes.
  - The progress is state, so it lives on the render thread (like a drawable's state) and is what is drawn. (Built: the UI side reads the progress the render thread last drew, a frame behind at most, instead of running the rule a second time, so the two cannot drift apart; the end of a timed move is reported to the UI thread with the frame's reports. The progress advances when a node that reads it is drawn, idempotently within a frame.)

## Facts that shape the plan

- **The link today.** `SkUiRenderLink` (`Rendering/SkUiRenderProps.cs`) is per axis `clamp(Factor · offset + Base, Min, Max)` onto the translation. The compositor evaluates it in `DrawNode` through `Follow` (`Rendering/SkUiCompositor.cs`) against `Source.Props.ChildrenOffsetX/Y`, so it follows render-thread flings. Its one user is the scroll bar thumb (`Core/SkUiCoreScrollBar.cs`, `Thumb.OnGetRenderProps`), which also writes the same translation on the UI side for hit-testing. A disposed source leaves the committed translation.
- **The source need not be an ancestor.** `Follow` reads the source node's props directly, so a header that is a sibling of the scroller can follow it. What must hold: the source's render-thread animations are ticked before any node is drawn (`TickAnimations` runs at the start of the frame); A2 checks it with a sibling drawn before its source.
- **Carousel items** use `SkUiItemEffectLink` (`ItemEffect`) instead, with `SortsChildrenByDepth`. Untouched here.
- **Overscroll** (`Controls/Scrolling/SkUiOverscroll.cs`): `Bounce` moves the children offset past its range; `Stretch` writes `ChildrenScaleX/Y` about `ChildrenScaleOrigin` (`1 + StretchFactor · |overscroll| / length`).
- **The UI thread hears about render-thread motion.** Flings, springs and tweens report their offsets back (`ApplyRenderScroll` in `SkUiScrollController`), which raises the internal `Scrolled` event. The sticky group header uses these reports to move on the UI side; the effects use them for hit-testing and semantics, never for drawing.
- **Composite-time properties** already include translation, scale, opacity, clip rectangles and the `Pinned` placement; clipping a node by a changing height is new (`ChildrenClipRect` clips children only, `ClipPath` is a committed path).
- **Render-thread state has a precedent.** `SkUiRenderDrawable` (`Rendering/SkUiRenderDrawable.cs`) is committed by the UI, then owned and advanced by the compositor with the shared clock; a reveal's state follows the same ownership. The compositor already learns of anchoring corrections per update (`ShiftScroll` in `SkUiCompositor.cs`), which is where a reveal excludes them; `SkUiScrollController.MovingChanged` tells the UI side when the scroller comes to rest (the settle request).
- **The collection view's sticky parts are drawn over its scroller** (`IsStickyHeader`, `IsStickyFooter`: one host each, over the scroller, which fills the view). Rows scroll behind them, so a sticky header that is clipped or moved away uncovers rows, not a gap. Their sizes are the scroller's `Insets`: scroll bars, `ScrollTo` and snap points keep treating that band as covered while the part is hidden (as Android's `CoordinatorLayout` does); the docs say so.
- **Scrollers come in several shapes:** `SkUiScrollView`, `SkUiCoreScrollView`, the virtual stacks, `SkUiCollectionView` (a scroller inside), `SkUiRefreshView` (its content's first vertical scroller), `SkUiZoomView`. Each has one `SkUiScrollController` and a render node whose children offset is the scroll offset.

## API

`SkUiScrollEffect` (namespace `MauiSkiaUi`):

| Member | Type | Default | Notes |
| --- | --- | --- | --- |
| `Source` | `ISkUiScrollEffectSource?` | `null` | The scroller. `null`: the nearest scrolling ancestor. Implemented by the scroll views (both layers), the virtual stacks, `SkUiCollectionView`, `SkUiRefreshView` and `SkUiZoomView`; each resolves to its own scroller |
| `Orientation` | `ScrollOrientation` | `Vertical` | Which offset drives it (`Both` is invalid) |
| `InputStart`, `InputEnd` | `double` | `0`, `0` | Offset range in DIPs; may go below 0 or past the end (overscroll, [D4](#decisions)). Equal values make a step at that offset. Ignored with `Reveal` |
| `Reveal` | `SkUiScrollReveal?` | `null` | Set: the input is the reveal's progress, 0 (shown) to 1 (hidden), instead of the offset ([D10](#decisions)) |
| `Property` | `SkUiScrollEffectProperty` | `TranslationY` | `TranslationX`, `TranslationY`, `Opacity`, `Scale`, `ScaleX`, `ScaleY`, `ClipHeight`, `ClipWidth` |
| `From`, `To` | `double` | `0`, `0` | Output at `InputStart` and `InputEnd` (with `Reveal`: shown and hidden), held outside |
| `Easing` | `Easing?` | `null` | Linear when `null`; sampled into a 33-point table for the render thread |
| `ClipEdge` | `SkUiClipEdge` | `Start` | Clips only: `Start`, `End`, `Center` ([D6](#decisions)) |
| `ClipShape`, `ClipCornerRadius` | `SkUiClipShape`, `double` | `Rectangle`, `0` | Clips only: `Rectangle`, `RoundRectangle`, `Capsule` |
| `ReducedMotion` | `SkUiScrollEffectReducedMotion` | `Default` | `Default` ([D7](#decisions)), `Run`, `HoldFrom` |
| `IsEnabled` | `bool` | `true` | Off: the property shows the node's own value |

`SkUiScrollReveal` (a plain class like the effect, [D10](#decisions)):

| Member | Type | Default | Notes |
| --- | --- | --- | --- |
| `Source` | `ISkUiScrollEffectSource?` | `null` | `null`: the nearest scrolling ancestor of the nodes it drives (they must agree; the first one wins, with a warning) |
| `Orientation` | `ScrollOrientation` | `Vertical` | |
| `Response` | `SkUiScrollRevealResponse` | `Tracked` | `Tracked` (follows the scroll) or `Triggered` (animates in or out) |
| `Distance` | `double` | `56` | Tracked: DIPs of scroll from shown to hidden; also the near-start band |
| `Threshold` | `double` | `24` | Triggered: DIPs in one direction that start a show or hide |
| `Duration` | `TimeSpan` | the look's | Triggered, settle, `Show` / `Hide` |
| `SettlesWhenIdle` | `bool` | `true` | Tracked: at rest halfway, finish showing or hiding |
| `ShowsNearStart` | `bool` | `true` | Fully shown at the start of the list |
| `IsShown` | `bool` (read-only) | `true` | UI side; `PropertyChanged` when it flips |
| `Show()`, `Hide()` | methods | | Animated; the next scroll takes over again |

- **SkUi\* layer:** `SkUiView.ScrollEffects` (`IList<SkUiScrollEffect>`, created on first read, so views without effects pay one null field), in XAML as `<SkUiView.ScrollEffects>`; `SkUiView.AddScrollEffect(effect)` for fluent code.
- **Core layer:** `SkUiCoreNode.AddScrollEffect(effect)`, `RemoveScrollEffect(effect)`, `ScrollEffects` (read-only).
- An effect belongs to one node at a time (adding it to a second throws, as a gesture recognizer does).
- Helpers for the common recipes, as static factories: `SkUiScrollEffect.Parallax(factor)`, `FadeIn(start, end)`, `CollapseTo(expanded, collapsed)` (a `ClipHeight` plus a `TranslationY` pair), and `SkUiScrollEffect.HideOnScroll(reveal, distance, edge)` (a translation by the view's size plus margin toward `edge`, driven by a reveal). The recipes below show what each one builds.

## Recipes

The use cases the API is checked against. Each becomes a section of the docs and a tab of the gallery page, and each has a test that scrolls it on the render thread and compares the drawn result with the expected props.

**1. Header that collapses with the offset** (collapsing toolbar, parallax picture, stretch on pull):

```xml
<Grid>
    <skui:SkUiCollectionView x:Name="List" ViewportInsets="0,240,0,0" ItemsSource="{Binding Items}" />
    <skui:SkUiImage Source="hero.jpg" HeightRequest="240" VerticalOptions="Start" Aspect="AspectFill">
        <skui:SkUiView.ScrollEffects>
            <!-- collapse from 240 to 56, the picture moving at half the speed -->
            <skui:SkUiScrollEffect Source="{x:Reference List}" Property="ClipHeight" InputStart="0" InputEnd="184" From="240" To="56" />
            <skui:SkUiScrollEffect Source="{x:Reference List}" Property="TranslationY" InputStart="0" InputEnd="184" From="0" To="-92" />
            <!-- stretch when pulled past the top -->
            <skui:SkUiScrollEffect Source="{x:Reference List}" Property="Scale" InputStart="0" InputEnd="-200" From="1" To="1.5" />
        </skui:SkUiView.ScrollEffects>
    </skui:SkUiImage>
</Grid>
```

**2. Collection view header and footer by scroll direction.** The header shrinks from 120 to 56 while the list scrolls down and opens again on any scroll up (and always at the top); the sticky footer slides out down and back up. No `Source`: the nearest scroller is the collection view. Rows scroll behind both, so nothing leaves a gap.

```xml
<skui:SkUiCollectionView ItemsSource="{Binding Items}" IsStickyHeader="True" IsStickyFooter="True">
    <skui:SkUiCollectionView.Header>
        <local:SearchHeader HeightRequest="120">
            <skui:SkUiView.ScrollEffects>
                <skui:SkUiScrollEffect Property="ClipHeight" From="120" To="56">
                    <skui:SkUiScrollEffect.Reveal>
                        <skui:SkUiScrollReveal Distance="64" />
                    </skui:SkUiScrollEffect.Reveal>
                </skui:SkUiScrollEffect>
            </skui:SkUiView.ScrollEffects>
        </local:SearchHeader>
    </skui:SkUiCollectionView.Header>
    <skui:SkUiCollectionView.Footer>
        <local:BottomBar HeightRequest="64">
            <skui:SkUiView.ScrollEffects>
                <skui:SkUiScrollEffect Property="TranslationY" From="0" To="64">
                    <skui:SkUiScrollEffect.Reveal>
                        <skui:SkUiScrollReveal Response="Triggered" />
                    </skui:SkUiScrollEffect.Reveal>
                </skui:SkUiScrollEffect>
            </skui:SkUiView.ScrollEffects>
        </local:BottomBar>
    </skui:SkUiCollectionView.Footer>
</skui:SkUiCollectionView>
```

A header given as data with a `HeaderTemplate` takes the same effects on the template's root view. A header that is not sticky scrolls away with the rows; direction effects on it are allowed but pointless, and the docs say to make it sticky.

**3. Any view that hides: a floating button.** Over the list in a grid, sliding out by its height and margin while the list scrolls down, back on scroll up, with a fade (which stays under reduce motion while the slide becomes instant):

```xml
<Grid>
    <skui:SkUiCollectionView x:Name="List" ItemsSource="{Binding Items}" />
    <skui:SkUiButton Text="+" WidthRequest="56" HeightRequest="56" CornerRadius="28" Margin="16"
                     HorizontalOptions="End" VerticalOptions="End">
        <skui:SkUiView.ScrollEffects>
            <skui:SkUiScrollEffect Property="TranslationY" From="0" To="88" Reveal="{StaticResource FabReveal}" />
            <skui:SkUiScrollEffect Property="Opacity" From="1" To="0" Reveal="{StaticResource FabReveal}" />
        </skui:SkUiView.ScrollEffects>
    </skui:SkUiButton>
</Grid>
<!-- in the page's resources: <skui:SkUiScrollReveal x:Key="FabReveal" Source="{x:Reference List}" Response="Triggered" /> -->
```

The same works offset-driven (gone after the first 200 DIPs: `InputStart="0" InputEnd="200"`, no reveal) and on any view, on both layers.

**4. A view that shrinks: an extended button to its icon.** A capsule pinned to the bottom-right keeps its trailing end while its width goes from 160 to 56; the icon moves with the leading edge and the label fades. One shared reveal keeps the three in step:

```xml
<skui:SkUiBorder StrokeShape="RoundRectangle 28" WidthRequest="160" HeightRequest="56" HorizontalOptions="End" VerticalOptions="End">
    <skui:SkUiView.ScrollEffects>
        <skui:SkUiScrollEffect Property="ClipWidth" ClipEdge="End" ClipShape="Capsule" From="160" To="56" Reveal="{StaticResource ComposeReveal}" />
    </skui:SkUiView.ScrollEffects>
    <skui:SkUiGrid ColumnDefinitions="56,*">
        <skui:SkUiImage Source="edit.svg">
            <skui:SkUiView.ScrollEffects>
                <skui:SkUiScrollEffect Property="TranslationX" From="0" To="104" Reveal="{StaticResource ComposeReveal}" />
            </skui:SkUiView.ScrollEffects>
        </skui:SkUiImage>
        <skui:SkUiLabel Grid.Column="1" Text="Compose" VerticalOptions="Center">
            <skui:SkUiView.ScrollEffects>
                <skui:SkUiScrollEffect Property="Opacity" From="1" To="0" Reveal="{StaticResource ComposeReveal}" />
            </skui:SkUiView.ScrollEffects>
        </skui:SkUiLabel>
    </skui:SkUiGrid>
</skui:SkUiBorder>
```

The border's shadow follows the capsule as it shrinks ([D6](#decisions)); taps outside the clipped capsule go to what is under it. In right to left, `End` is the left side and the icon's translation mirrors.

### What phase B settled

Where the built API differs from the tables and recipes above (they stay as first written; this list wins):

- **Sources:** `SkUiScrollView`, `SkUiCoreScrollView`, `SkUiCollectionView` (its list's scroller) and `SkUiRefreshView` (the scroller it pulls). The virtual stacks scroll inside a scroll view, which is the source; `SkUiZoomView` is not one.
- **Factories:** `Parallax(rate, range = 100,000)` (a view outside the scroller moving up at `rate` times the scroll speed), `FadeIn(start, end)`, `CollapseTo(expanded, collapsed)` (one `ClipHeight` effect over the first `expanded − collapsed` DIPs; pair it with `Parallax` for a moving picture), `HideOnScroll(reveal, translationY)`. The SkUi\* layer also has `AddScrollEffects(params ReadOnlySpan<…>)`, and both layers `RemoveScrollEffect`.
- **Right to left:** clip edges follow the flow direction (`Start` keeps the leading side); `TranslationX` effects do not mirror, as MAUI's `TranslationX` does not. In recipe 4, an app in right to left gives the icon a negative `To`.
- **`IsShown`** is whether the reveal is nearer shown than hidden (progress below 0.5).
- **`SkUiScrollRevealResponse.Manual`** and **`WhileScrolling`** (added after the demo review): `Manual` moves only on `Show()` / `Hide()`, for the app's own rules; `WhileScrolling` hides in step with any scroll (distance, either direction) on the render thread and shows, animated, when the scroller comes to rest. A floating button hidden from `IsScrolling` with `Show()` / `Hide()` was tried first: it was not in step with the scroll, which only the render thread can be.
- **A shared reveal without a `Source`** resolves from the first view it drives; a reveal used by views outside the list (a floating button) needs its `Source`, which XAML can set by `x:Reference` from a resource.
- **Invalid values throw** (`ArgumentOutOfRangeException`) from the CLR setters too, as the effect is a plain class (D3).
- **Recipe 1 as written above is wrong:** a clip and a translation on the same view move together, so the part kept slides off the top. The clip goes on the header container, which stays put and clips its children, and the translation on the picture inside it (found on the Mac Catalyst check, 23 November 2026; the docs and the gallery page use that form).

## Phase A: the engine (internal)

### A1 A list of links

- `SkUiRenderLink` becomes a channel: `Source`, axis, target property, input start and end, output from and to, and an optional sampled curve (`float[]`, shared between nodes with the same easing); or, for an effect with a reveal, the reveal's render state in place of the source and input range ([A5](#a5-reveals-on-the-render-thread)). `SkUiRenderProps.Links` is an immutable array of them; the node builds a new array only when its effects change, so a commit of unchanged effects reuses it (reference compare in the props diff).
- The scroll bar thumb's link becomes one channel (factor and range expressed as input and output ranges).
- *Check:* every scrolling, collection view and scroll bar test passes; `scripts/bench.sh --baseline HEAD -S skui-scroll,core-scroll,skui-collection` shows no slower frame.

### A2 Compositor evaluation

- `DrawNode` evaluates the channels against each source's current children offset: translation and scale into the matrix (`GetMatrix` with overrides), opacity into the layer alpha (skip the node at 0), clips in local coordinates before the content: the rectangle placed by `ClipEdge` (mirrored in right to left), with `ClipShape` corners as an `SKRoundRect` (no path per frame). An outline shadow is drawn as that clipped round rectangle; a content shadow is clipped with the content. Culling uses the clipped bounds.
- No allocation per frame: the evaluation works on stack values; the curve tables are created on the UI side.
- *Check:* `SkUiFrameRenderer` tests: offset in, matrix / opacity / clip out, for each property, each clip edge (left to right and right to left) and each clip shape; a shrinking capsule's shadow follows it (pixels); a render-thread fling moves a linked sibling drawn before its source; zero re-records (`SkUiRenderStatistics`) and zero allocations over a fling.

### A3 Overscroll as input

- The input for a channel is the offset plus the overscroll shown: a bounce is already in the offset; a stretch is recovered from `ChildrenScale*` and `ChildrenScaleOrigin` ([D4](#decisions)).
- *Check:* a test per overscroll mode: pulled 80 DIPs past the top, an effect with `InputStart=-200`, `InputEnd=0` gives the same output in both modes (within the rubber band's rounding).

### A4 The UI side

- On every scroll report (`Scrolled`, also each reported frame of a render-thread motion) the node evaluates its channels with the same code and writes the results where hit-testing, the immediate painter and the semantics tree read them; no `InvalidateRender` of content.
- A node clipped to 0, faded to 0 or moved outside its parent's clip is not hit and is hidden from screen readers; one partly clipped is hit only in its visible part (the clip's rounded corners included).
- *Check:* immediate painter equals compositor output (pixel compare at three offsets); hit-test and semantics tests.

### A5 Reveals on the render thread

- A reveal's render side (`SkUiRevealState`): its settings, committed by the UI, and its progress and last input, owned and advanced by the compositor once per frame before any node is drawn (so every node it drives sees the same progress), as drawables are. Channels refer to it instead of an offset range.
- Each frame: the source's input (offset with overscroll removed, anchoring corrections from `ShiftScroll` subtracted) gives a delta. `Tracked` adds `delta / Distance` to the progress; `Triggered` sums the run in one direction and starts a timed move to 0 or 1 past `Threshold`; both cap the progress at `offset / Distance` near the start, and a jump of more than a viewport resets it. A timed move (trigger, settle, `Show` / `Hide`) uses the shared clock and keeps frames coming until it ends, like any render-thread animation.
- Requests from the UI (settle when the scroller comes to rest, `Show`, `Hide`) are commits with a serial number, so a request is acted on once. The end of a timed move is reported back with the frame's reports; the UI side takes that value.
- *Check:* `SkUiFrameRenderer` tests: a drag down by `Distance` hides, a drag up by half shows half, at rest it settles, a trigger past the threshold runs its duration while the scroll stops, a correction of 500 DIPs (items inserted above) does not move it, `ScrollTo` the top shows it, two nodes on one reveal draw the same progress; the UI side's value equals the drawn one after a drag, a fling and a settle; zero allocations per frame.

## Phase B: the public API

### B1 `SkUiScrollEffect`

- The class per [the API](#api), its enums (`SkUiScrollEffectProperty`, `SkUiClipEdge`, `SkUiClipShape`, `SkUiScrollEffectReducedMotion`), validation through `SkUiValidate` (non-finite numbers, `Orientation=Both`, negative clips and radii), the easing table, `PropertyChanged` with static event args, the factories.
- `SkUiScrollReveal` per [the API](#api), `SkUiScrollRevealResponse`, `Show()` / `Hide()`, `IsShown`; usable from XAML inline (`<SkUiScrollEffect.Reveal>`) and as a resource shared by several effects.
- *Check:* `ScrollEffectTests` and `ScrollRevealTests` (validation, factories, a change raises once, the table matches the easing at its points, `IsShown` flips once per hide).

### B2 SkUi\* layer

- `ScrollEffects` collection on `SkUiView` (a partial file, `SkUiView.ScrollEffects.cs`), `AddScrollEffect`.
- Source resolution: the effect's `Source`, or the nearest ancestor implementing `ISkUiScrollEffectSource`; done when the view joins a drawn tree, when `Source` changes, and when the collection view or refresh view replaces its scroller. The view subscribes to that scroller's `Scrolled` through `SkUiWeakEvent`, and lets go when it leaves the tree (`ReleaseDrawingResources` path) or the effect is removed. A reveal resolves its source from the first node it drives, counts its nodes (its render state goes with the last one), and sends a settle request when the scroller's `MovingChanged` reports rest. On each scroll report the view invalidates its props (so the committed `LinkValues` stay current for a source that goes) and the surface's semantics tree (a linked sibling of the scroller changes without the scroller's own subtree changing).
- *Check:* API tests (XAML with `x:Reference`, nearest ancestor, a source added after the view, a source removed, effects in a collection view's sticky header and footer and in a `HeaderTemplate` root, one reveal shared by three nodes); `ImplicitStyleTests` unaffected; leak scenario `ScrollEffectsAttached` (a page of effects linked to a list, popped) in `tests/Shared/MemoryLeaks`.

### B3 Core layer

- `AddScrollEffect`, `RemoveScrollEffect`, `ScrollEffects` on `SkUiCoreNode`, the same resolution (Core ancestors, then through `SkUiCoreHost` into the SkUi\* tree, so a Core header can follow a SkUi\* list).
- *Check:* the B2 tests on Core nodes; a test drawing the same page on both layers and comparing pixels.

### B4 Reduce motion

- Per [D7](#decisions).
- *Check:* with `SkUiMotion.ReduceMotion = true`: a fade and a clip still follow the offset; a parallax translation and a scale hold `From`; a triggered hide moves the button at once and fades it over `Duration`; `ReducedMotion="Run"` and `"HoldFrom"` override.

### B5 Snap to the ends (cut line)

- `SkUiScrollEffect.SnapsToEnds`: when a drag or fling would stop strictly between `InputStart` and `InputEnd`, the scroller settles on the nearer end instead. Implemented as two snap offsets the effect contributes to its scroller's snap query (`ISkUiSnapPointSource` chain), so it rides the existing snap code and works on both layers.
- *Check:* a test per direction; a list with its own snap points keeps them outside the effect's range.

## Docs, demo and release

1. **Docs:** a "Scroll-linked effects" section in [SkUiScrollView.md](../controls/SkUiScrollView.md) with the [recipes](#recipes) (offset and direction, the overscroll stretch, the covered band of a hidden sticky part, reduce motion), links from [SkUiView.md](../controls/SkUiView.md), [SkUiCore.md](../controls/SkUiCore.md) and [SkUiCollectionView.md](../controls/SkUiCollectionView.md); [RenderingPipeline.md](RenderingPipeline.md) (links per node, evaluation, ownership); [ScrollingAndCollectionViews.md](ScrollingAndCollectionViews.md). [Migration.md](../Migration.md) and the migration skills: MAUI has no equivalent; the section maps the common hand-written pattern (`Scrolled` handler setting `TranslationY` / `Opacity`) and Xamarin's or vendors' parallax views to effects.
2. **Demo:** a component gallery page "Scroll effects" with a tab per [recipe](#recipes) (collapsing header with parallax and pull-to-stretch, collection view header and footer by direction, hiding floating button, extended button shrinking to its icon) plus a fading toolbar, on SkUi\* and Core.
3. **Bench:** `skui-scroll-effects` and `core-scroll-effects`: a 1,000-row collection view (Core: virtual stack) with a collapsing header, and `skui-scroll-reveal`: the same list with a direction-driven sticky header, footer and floating button, scrolled down and up as the list scenarios are; options set through `TrySet` so a `--baseline` build of 1.15 compiles.
4. **Device checks** (the [verification backlog](Roadmap.md#verification-backlog) if not done before release): the gallery page on the Galaxy S9 and the iPhone 8, frame times while flinging with and without the header, the feel of the stretch, of a tracked header settling and of a triggered button, and of B5's snap.
5. **Release:** changelog (`### New features`), [Roadmap.md](Roadmap.md) (1.16 section; the overscroll item leaves the backlog; F1, F5, F6 under 1.17), [Requirements.md](Requirements.md) (scroll-linked effects with the scrolling requirements), the review plan's F2 marked shipped.

## Order and sizes

| Step | Size | Depends on | Notes |
| --- | --- | --- | --- |
| A1 | S | | Internal; touches `SkUiRenderProps.cs`, `SkUiCompositor.cs`, `SkUiCoreScrollBar.cs` |
| A2 | M | A1 | The step that decides the perf budget |
| A3 | S | A2 | |
| A4 | M | A2 | Hit-testing and semantics on both layers |
| A5 | M | A2 | Reveals: render-thread state, requests and reports |
| B1 | S | | Can run beside phase A |
| B2, B3 | M | A4, A5, B1 | B3 after B2 (shared resolution code) |
| B4 | S | B2 | |
| B5 | S | B2 | Cut line ([D8](#decisions)) |
| Docs, demo, bench | M | B3 | Bench scenario can be written after A2 |

One branch, `1.16-implementation`, from `devel` after 1.15.1. Phase A and B1 can go to separate agents; everything from B2 on touches the same files and runs one at a time.

## Not in 1.16

- **Effects that change layout** (a header whose shrinking height moves its siblings): one measure and arrange per frame on the UI thread. Clip-and-translate with `ViewportInsets` covers the common pages.
- **Insets that follow a hidden sticky part:** the scroll bars, `ScrollTo` and snap points keep the band of a hidden sticky header or footer as covered ([facts](#facts-that-shape-the-plan)). Making `Insets` follow a reveal is a later step if an app needs it.
- **Clips of any shape:** rectangles, round rectangles and capsules only; a shape clip that follows the scroll would build a path per frame.
- **Other properties:** rotation, colors, blur, a child's corner radius. The channel list can take more properties later without an API change.
- **Other inputs:** a carousel's position, a drag, a sheet's detent (the sheet in 1.18 adds its own source through `ISkUiScrollEffectSource`), time-based scroll-driven animations.
- **Sticky group headers and the collection view's sticky parts** keep their UI-side placement; moving them to links is a later performance step if the benches ask for it.

## Verification

- Headless suite green on net10 and net11, library build at 0 warnings.
- `skui-scroll-effects` and `skui-scroll-reveal` within 5 % of the same list without the header (render time per frame), zero re-records and zero allocations per scroll frame.
- Each [recipe](#recipes) has its test and its gallery tab.
- Leak scenario passes headless and on devices (`scripts/device_tests.sh`).
- The gallery page checked on the S9 and the iPhone 8 (or the check left on the verification backlog, named in the release notes' section).

**Done when:** a 1,000-row list with a collapsing header, and the same list with a direction-driven header, footer and floating button, scroll and fling with zero re-records, the render time per frame within 5 % of the list without them, on both layers, with the four recipes documented and in the gallery.
