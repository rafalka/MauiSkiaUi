# SkUiScrollView

Single-surface scroller with pan, fling, wheel, overscroll, scroll bars, and programmatic scroll APIs.

**MAUI counterpart:** [`ScrollView`](https://learn.microsoft.com/dotnet/maui/user-interface/controls/scrollview)

## How it works

Extends [`SkUiContentView`](SkUiContentView.md). Measures content unconstrained on enabled axes; content keeps a stable arranged frame (`max(measured extent, viewport)`). Across the scroll axis, content with an explicit size (`WidthRequest` of a vertical scroller's content, `HeightRequest` of a horizontal one's) keeps it even when it is larger than the viewport, and is clipped, not scrolled, as MAUI's ScrollView does on its platforms (same on `SkUiCoreScrollView`). With an effective right-to-left `FlowDirection`, content is mirrored and a horizontal scroller starts at the right end (`ScrollX` stays a physical offset, 0 = left).

**Sizing:** without a `HeightRequest` (`WidthRequest` for horizontal scrolling), a scroll view is as large as its content until a limit stops it: the parent's constraint or `MaximumHeightRequest` / `MaximumWidthRequest`. Below the limit it behaves as if it were not there (nothing to scroll); at the limit it stops growing and scrolls. Typical limits: a page or `ContentView` of a fixed size, a star row with `VerticalOptions="Start"` (the scroller then takes its content's height up to the row's), or a maximum size inside a stack. A vertical stack or an `Auto` grid row gives no limit along its axis (MAUI's layouts measure their children there with infinite space), so a scroller placed directly in one takes its whole content, as MAUI's ScrollView does; give it a maximum size there.

**The scroll offset is a composite-time children translation.** Scrolling never re-records content. Each child keeps its own retained picture, so an animating child re-records only itself, and off-screen children are culled by the compositor.

**Fling, `ScrollToAsync` and `AnimateScrollTo` run on the render thread** (GPU surfaces), so they stay smooth while the UI thread is busy. The render thread reports offsets back every frame, which keeps `ScrollX` / `ScrollY`, `Scrolled`, hit-testing and native overlays in sync. A new touch stops the motion at the last shown offset.

**Drags take part in the gesture arena** ([EventMechanism.md](../design/EventMechanism.md)):
- **Taps:** content taps win unless the pointer moves more than the touch slop (10 DIPs) along an enabled axis, in a direction the scroller can still move.
- **Press feedback:** a button inside shows its pressed state after `SkUiGestureSettings.PressDelay` (100 ms) unless a scroll starts first, so starting a scroll never flashes buttons.
- **Drags inside the scroller:** controls inside can still drag along the other axis (sliders, swipe rows, horizontal carousels).

**Nested scrollers:**
- Orthogonal scrollers each take their own axis.
- With the same axis, the inner scroller moves first and the rest of the drag chains to the outer one.
- A fling goes to the innermost scroller that can move that way. The wheel and trackpad scroll both axes (`SkUiTouchEvent.WheelDelta` / `WheelDeltaX`; Shift + wheel and tilt wheels on Windows): each axis goes to the innermost scroller that can move that way, and a plain mouse wheel scrolls a horizontal-only scroller.
- Inside a native MAUI `ScrollView`, the drawn scroller goes first and the native one takes over at the drawn edge.

**Overscroll** (`Overscroll`, a SkiaUi extension; `Default` follows the look, and the default look follows the platform: `Bounce` on iOS and Mac Catalyst, `Stretch` on Android, `None` elsewhere):
- What a drag cannot scroll, after the outer scrollers took what they can, pulls the dragged scroller past its edge: a rubber band with growing resistance (`Bounce`), or a stretch away from the pulled edge (`Stretch`). Dragging back takes the pull back first; releasing springs back on the render thread.
- A fling that reaches the edge runs past it with its velocity there (at most 15 % of the viewport) and settles; with `None` it stops at the edge.
- At an edge a scroller takes an outward drag only when no outer drawn scroller and no native ancestor (a MAUI `ScrollView` around the surface) can still scroll that way, so nested chaining and native hand-over are unchanged.
- `ScrollX` / `ScrollY` stay within the content (no `Scrolled` while overscrolled); native overlays follow a bounce.

**Scroll bars** (MAUI's `VerticalScrollBarVisibility` / `HorizontalScrollBarVisibility`): `Default` shows a bar while the offset changes and fades it out after `SkUiLook.ScrollBarFadeDelay` (500 ms) over `ScrollBarFadeDuration` (250 ms); `Always` shows it while the content overflows; `Never` hides it. Drawn by `SkUiLook.DrawScrollBar` (`ScrollBarThickness`, `ScrollBarMargin`, `ScrollBarMinimumThumbLength`; color: the scheme's foreground at 40 %, or the bar's `ThumbColor`), on the left in right-to-left layouts; both bars leave the corner free.
- **Overlay or gutter:** a `Default` bar draws over the content. An `Always` bar reserves a gutter beside it (`SkUiLook.ScrollBarReservedThickness`, 12 DIPs: the expanded bar and its margins), on the right (left in right-to-left layouts) or at the bottom: the content is measured, arranged and clipped in the rest (the scrollport), and the bar sits in the gutter. The gutter is reserved whenever the axis scrolls, also while the content fits (the bar then hides), as CSS `scrollbar-gutter: stable`, so the layout never flips with its own result. Offsets, `ScrollToAsync` positions and native overlays use the scrollport.
- **Core components:** each bar is a [`SkUiCoreScrollBar`](SkUiCore.md#scroll-bars) (`VerticalScrollBar`, `HorizontalScrollBar`): a Core node pinned to the viewport whose thumb is placed by a scroll link, so the compositor moves it during flings and fades it with no recording and no UI-thread work. Style it with `ThumbColor` and `IsInteractive`; read `IsExpanded` / `IsDragging`.
- **Desktop:** while a mouse, trackpad or pen hovers the 16 DIP strip along the edge (`ScrollBarHitThickness`), the bar shows, widens to `ScrollBarExpandedThickness` (8) with its track (`DrawScrollBarTrack`) and takes presses: drag the thumb to scroll, press the track to page one viewport towards the press. Touch never hovers, so touches on the strip scroll and tap the content as usual.
- **Placed bars:** `new SkUiCoreScrollBar(scrollView, ScrollOrientation.Vertical)` follows a scroller from anywhere in the same surface (in a `SkUiCoreHost`, a Core layout beside a card); set its `Visibility` and the scroller's own bar to `Never`.

**Snap points** (`SnapPointsType`, `SnapPointsAlignment`: MAUI's CollectionView enums; a SkiaUi extension, MAUI's ScrollView has none): the children of the content are the snap targets, lined up with the viewport's start, center or end. With `Mandatory`, drags, flings and wheel / trackpad scrolling (after a 150 ms pause) end on the snap point nearest to where the motion would stop; with `MandatorySingle` a swipe moves one snap point from where the drag started, and a slow drag settles on the nearest (a carousel). The settle is a render-thread spring that starts at the release velocity and never passes its target. Programmatic scrolls do not snap.

**Viewport insets** (SkiaUi extension; also on `SkUiCoreScrollView`, `SetViewportInsets`): an edge-to-edge page draws translucent bars over the scroller and says what they cover with `ViewportInsets` (`Thickness`, physical edges, default none). The content is arranged inside the insets and the extent grows by them (`ContentSize` includes them), so it scrolls under the bars and nothing is covered at the ends of the range.

- **What uses the band they leave:** the scroll bars' track, `SnapPointsAlignment`, `ScrollToAsync(element / node, position)`, keyboard and screen-reader reveal, keyboard paging. `ScrollToAsync(x, y)` stays raw offsets.
- **Across the scroll axis** they pad the content and keep the scroll bar out of them (a landscape notch); an `Always` bar's gutter moves in with the content.
- **The keyboard** covers a bottom bar, so of the two the larger counts, not their sum.
- **Changing them while scrolled** moves the offset by the change, so what shows stays in place; a scroller at the start of its range stays there, which is also why bars measured after the first layout need no care.
- **`Padding` keeps its meaning**, space inside the scrolled content (as in MAUI): use it for room that is not covered by anything.
- **Hosted native views** ([`SkUiMauiContentView`](SkUiMauiContentView.md)) show above the drawn surface, so they are clipped to the band: one scrolled under a translucent bar is cut off at the bar instead of showing through it.
- **`ISkUiViewportInsets`** is implemented by the scroll views and `SkUiCollectionView`: an app's inset helper can take any of them without knowing its type.

```xml
<sk:SkUiGrid>
    <sk:SkUiScrollView x:Name="Scroller"><!-- content --></sk:SkUiScrollView>
    <sk:SkUiContentView x:Name="TopBar" VerticalOptions="Start" Background="#C80F766E"><!-- title --></sk:SkUiContentView>
    <sk:SkUiContentView x:Name="BottomBar" VerticalOptions="End" Background="#C80F766E"><!-- actions --></sk:SkUiContentView>
</sk:SkUiGrid>
```

```csharp
void UpdateInsets(object? sender, EventArgs args) =>
    Scroller.ViewportInsets = new Thickness(0, TopBar.Height, 0, BottomBar.Height);
TopBar.SizeChanged += UpdateInsets;
BottomBar.SizeChanged += UpdateInsets;
```

The bars size themselves for the notch from the surface root's [`SafeAreaInsets`](SkUiLayout.md#safe-area). The gallery's "Edge-to-edge scrolling" page shows every behavior; the samples app's "Order list" example opens a full page that follows the insets through a rotation ([OrdersEdgeToEdgePage](../../samples/MauiSkiaUiSamples/Samples/Controls/OrdersEdgeToEdgePage.xaml.cs)).

**Overscroll events** (SkiaUi extensions; also on `SkUiCoreScrollView`), for effects and gestures at the edges (stretchy headers, pull to dismiss, pull to load the next page, a refresh of your own; for MAUI's pull-to-refresh wrap the scroller in [`SkUiRefreshView`](SkUiRefreshView.md)):
- `Overscrolled` reports `OverscrollX` / `OverscrollY` (DIPs shown past the edges; negative past the top or left) and `IsDragging` whenever they change: while a drag pulls past an edge, while a fling bounces, and while the content springs back (once per frame, reported from the render thread), ending at 0. One argument instance is reused per scroller: read it in the handler. Changing translation, scale, rotation or opacity from it re-records nothing.
- `PullReleased` reports the edge and the distance when a drag is released past an edge, before the content springs back (once per edge).
- `PullEdges` (`SkUiScrollEdges`: `Top`, `Bottom`, `Left`, `Right`, flags) lets drags pull past those edges also when the content does not overflow or `Overscroll` is `None`. The pull has the rubber band's resistance and moves the content only as the overscroll mode draws it, so with `None` the content stays and only the events tell. Edges of an axis the scroller does not scroll never pull. Left and right are physical, also in right-to-left layouts.
- Without `PullEdges`, the events still report what `Bounce` and `Stretch` show.

```csharp
// A header image that stretches while the top is pulled (composite-time: nothing re-records).
scroller.Overscrolled += (_, e) =>
{
    var pull = Math.Max(0, -e.OverscrollY);
    header.AnchorY = 1;
    header.ScaleY = 1 + pull / header.Height;
};
// Pull down far enough to close the page.
scroller.PullEdges = SkUiScrollEdges.Top;
scroller.PullReleased += async (_, e) =>
{
    if (e.Edge == SkUiScrollEdges.Top && e.Distance > 120)
        await Navigation.PopModalAsync();
};
```

**Scrolling to an element** (MAUI's API): `ScrollToAsync(Element, ScrollToPosition, bool)` scrolls so that a drawn descendant, a Core node under a `SkUiCoreHost` (`ScrollToAsync(SkUiCoreNode, …)`), or a MAUI view inside a `SkUiMauiContentView` is at the `Start`, `Center` or `End` of the viewport; `MakeVisible` scrolls only when it is not fully visible, aligning its end when it begins after the viewport's start and its start otherwise (MAUI's rule: an element larger than the viewport moves even when part of it shows). Positions come from the layout (offsets of nested scrollers included, transforms ignored, as MAUI) and are clamped; before the first layout the request waits for it. `GetScrollPositionForElement` returns the offset without scrolling. Both `ScrollToAsync` overloads raise MAUI's `ScrollToRequested` with MAUI's `ScrollToRequestedEventArgs`; with `Orientation="Neither"` they do nothing.

Native overlays register with ancestor scrollers for O(overlays) offset sync. See [RenderingPipeline.md](../design/RenderingPipeline.md) and [ScrollingAndCollectionViews.md](../design/ScrollingAndCollectionViews.md).


## Shared conventions

All SkiaUi controls inherit [`SkUiView`](SkUiView.md) behavior:

- **Coordinates** use DIPs. Paint and touch share the same local space as measure/arrange.
- **BindableProperty + fluent `Set*` setters:** a `Set*` setter is the property setter in fluent form (`label.SetText("a").SetFontSize(20)`): getters read the bindable store, as in MAUI, so bindings, triggers and `x:Reference` see every change (FR-10). Invalid values: `Set*` throws; XAML, bindings, styles and the property setter ignore them with a logged warning, as MAUI does.
- **`StartUpdating` / `EndUpdating`** batch layout and paint invalidation.
- **Gestures** use SkiaUi's gesture arena (`Tapped` / `TappedCommand`, `DoubleTapped`, `LongPressed`, `Swiped`, `PanUpdated`, `PinchUpdated`, custom recognizers in `Gestures`). MAUI's `GestureRecognizers` run on the arena too: tap (any `NumberOfTapsRequired`, primary or secondary button), swipe, pan, pinch and pointer recognizers; drag and drop is not run and is reported once as a `Trace` line. See [EventMechanism.md](../design/EventMechanism.md#maui-gesture-recognizers).
- **Hosted vs standalone:** when nested under another SkiaUi parent, the node has no platform handler and paints into the root surface. See [LayoutSystem.md](../design/LayoutSystem.md).


## How to use

```xml
<sk:SkUiScrollView Orientation="Vertical" Padding="16">
  <sk:SkUiVerticalStackLayout Spacing="8">
    <!-- long content -->
  </sk:SkUiVerticalStackLayout>
</sk:SkUiScrollView>
```

```xml
<sk:SkUiScrollView Orientation="Both" VerticalScrollBarVisibility="Always" HorizontalScrollBarVisibility="Never"
                   Overscroll="Bounce">
  …
</sk:SkUiScrollView>
```

```csharp
await scroller.ScrollToAsync(0, 400, animated: true);
await scroller.ScrollToAsync(finalLabel, ScrollToPosition.Center, animated: true);
```

## Scroll-linked effects

(1.16) A view's translation, scale, opacity or a clip can follow a scroller, on the render thread: a header that collapses as the list scrolls, a parallax picture, a toolbar title that fades in, a header that grows when the list is pulled past its top, and views that hide while the list scrolls down and come back on any scroll up. Nothing is recorded again while the scroller moves, and the views follow render-thread flings. Works on both layers: `SkUiView.ScrollEffects` / `AddScrollEffect`, `SkUiCoreNode.AddScrollEffect`.

**`SkUiScrollEffect`** maps an offset range onto one property:

| Member | Default | Notes |
| --- | --- | --- |
| `Source` | `null` | The scroller: a `SkUiScrollView`, `SkUiCoreScrollView`, `SkUiCollectionView` (its list) or `SkUiRefreshView` (the scroller it pulls). `null`: the view's nearest scrolling ancestor (for a collection view's header or footer, the collection view) |
| `Orientation` | `Vertical` | Which offset drives it |
| `InputStart`, `InputEnd` | `0`, `0` | The offset range in DIPs. Offsets include the overscroll shown, bounce or stretch: below 0 is a pull past the start. Equal values make a step |
| `Property` | `TranslationY` | `TranslationX`, `TranslationY`, `Opacity`, `Scale`, `ScaleX`, `ScaleY`, `ClipHeight`, `ClipWidth` |
| `From`, `To` | `0`, `0` | The output at `InputStart` and `InputEnd`, held outside |
| `Easing` | linear | Sampled at 33 points for the render thread |
| `ClipEdge` | `Start` | Clips: the part that stays (`Start` top or leading, `End` bottom or trailing, `Center`) |
| `ClipShape`, `ClipCornerRadius` | `Rectangle` | Clips: `Rectangle`, `RoundRectangle`, `Capsule` (a pill at every size) |
| `Reveal` | `null` | A [scroll-direction input](#views-that-hide-by-scroll-direction) instead of the offset |
| `SnapsToEnds` | `false` | A drag, fling or wheel scroll never comes to rest strictly inside the input range: it settles on the nearer end (a header ends fully open or collapsed), with or without the scroller's snap points; programmatic scrolls are left alone. Offset effects only |
| `ReducedMotion` | `Default` | See [reduced motion](#reduced-motion) |
| `IsEnabled` | `true` | Off: the property shows the view's own value |

Effects combine with each other and with the view's own values: translations add, scales and opacities multiply, clips take the smaller. A clip does not change layout: the view keeps its size and its neighbors stay put. It clips the view's content, children and shadow (an outline shadow follows the clip's shape), and taps and screen readers see only the part that shows; a view faded to 0 is not hit and not read. An effect belongs to one view at a time. Factories: `Parallax(rate)`, `FadeIn(start, end)`, `CollapseTo(expanded, collapsed)`, `HideOnScroll(reveal, translationY)`.

### Collapsing header

The header sits over the list; the list's `ViewportInsets` leave its expanded height, so the rows start below it and scroll under it as it collapses. Put the clip on the header, which stays in place, and move what is inside it: a clip clips the view's children, while a clip and a translation on the same view move together (the part kept slides away with it).

```xml
<sk:SkUiGrid>
  <sk:SkUiCollectionView x:Name="List" ViewportInsets="0,240,0,0" ItemsSource="{Binding Items}" />
  <sk:SkUiGrid HeightRequest="240" VerticalOptions="Start" InputTransparent="True" IsClippedToBounds="False">
    <sk:SkUiView.ScrollEffects>
      <!-- 240 → 56 over the first 184 DIPs, and on at the same slope into a pull (424 at -184), so a growing picture is not cut -->
      <sk:SkUiScrollEffect Source="{x:Reference List}" Property="ClipHeight" InputStart="-184" InputEnd="184" From="424" To="56" />
      <!-- never left half open: a separate clip over 0…184 that settles on its ends (it never clips below the one above) -->
      <sk:SkUiScrollEffect Source="{x:Reference List}" Property="ClipHeight" InputStart="0" InputEnd="184" From="240" To="56" SnapsToEnds="True" />
    </sk:SkUiView.ScrollEffects>
    <sk:SkUiImage Source="hero.jpg" Aspect="AspectFill" AnchorY="0">
      <sk:SkUiView.ScrollEffects>
        <!-- moves up at half speed inside the shrinking header -->
        <sk:SkUiScrollEffect Source="{x:Reference List}" Property="TranslationY" InputStart="0" InputEnd="184" From="0" To="-92" />
        <!-- grows when the list is pulled past its top -->
        <sk:SkUiScrollEffect Source="{x:Reference List}" Property="Scale" InputStart="-200" InputEnd="0" From="1.5" To="1" />
      </sk:SkUiView.ScrollEffects>
    </sk:SkUiImage>
    <sk:SkUiLabel Text="Title" VerticalOptions="End" Margin="16,0,16,14">
      <sk:SkUiView.ScrollEffects>
        <!-- moves with the content, to the bottom of the collapsed bar -->
        <sk:SkUiScrollEffect Source="{x:Reference List}" Property="TranslationY" InputStart="0" InputEnd="184" From="0" To="-184" />
      </sk:SkUiView.ScrollEffects>
    </sk:SkUiLabel>
  </sk:SkUiGrid>
</sk:SkUiGrid>
```

`SkUiScrollEffect.CollapseTo(240, 56)` is the clip alone (holding 240 during a pull).

A toolbar title that fades in once the list's big title has scrolled under it: `<sk:SkUiScrollEffect Source="{x:Reference List}" Property="Opacity" InputStart="20" InputEnd="60" From="0" To="1" />`.

### Views that hide by scroll direction

**`SkUiScrollReveal`** turns the scroller's movement into a progress from shown to hidden; an effect with `Reveal` maps it from `From` (shown) to `To` (hidden).

| Member | Default | Notes |
| --- | --- | --- |
| `Source`, `Orientation` | `null`, `Vertical` | As on the effect; `null` resolves from the first view it drives |
| `Response` | `Tracked` | `Tracked`: follows the scroll, `Distance` DIPs from shown to hidden. `Triggered`: after `Threshold` DIPs in one direction, moves fully in or out over `Duration` |
| `Distance`, `Threshold`, `Duration` | `56`, `24`, 200 ms | |
| `SettlesWhenIdle` | `true` | Tracked: at rest halfway, finishes showing or hiding |
| `ShowsNearStart` | `true` | Fully shown at the start of the list: within `Distance` of it, never more hidden than the offset allows, so a header never leaves a gap above the first row |
| `IsShown` | | Read-only, raises `PropertyChanged`; `Show()` / `Hide()` animate, and the next scroll takes over |

Rows inserted above (anchoring) and pulls past the ends do not move a reveal; a jump of more than two viewports shows its views again. Share one reveal between views so they move as one.

A collection view's sticky header that shrinks while the list scrolls down and opens again on any scroll up, and a sticky footer that slides out (no `Source`: the nearest scroller of a header is its collection view; rows scroll behind both, so nothing leaves a gap):

```xml
<sk:SkUiCollectionView ItemsSource="{Binding Items}" IsStickyHeader="True" IsStickyFooter="True">
  <sk:SkUiCollectionView.Header>
    <local:SearchHeader HeightRequest="120">
      <sk:SkUiView.ScrollEffects>
        <sk:SkUiScrollEffect Property="ClipHeight" From="120" To="56">
          <sk:SkUiScrollEffect.Reveal>
            <sk:SkUiScrollReveal Distance="64" />
          </sk:SkUiScrollEffect.Reveal>
        </sk:SkUiScrollEffect>
      </sk:SkUiView.ScrollEffects>
    </local:SearchHeader>
  </sk:SkUiCollectionView.Header>
  <sk:SkUiCollectionView.Footer>
    <local:BottomBar HeightRequest="64">
      <sk:SkUiView.ScrollEffects>
        <sk:SkUiScrollEffect Property="TranslationY" From="0" To="64">
          <sk:SkUiScrollEffect.Reveal>
            <sk:SkUiScrollReveal Response="Triggered" />
          </sk:SkUiScrollEffect.Reveal>
        </sk:SkUiScrollEffect>
      </sk:SkUiView.ScrollEffects>
    </local:BottomBar>
  </sk:SkUiCollectionView.Footer>
</sk:SkUiCollectionView>
```

A header given as data with a `HeaderTemplate` takes the effects on the template's root. While a sticky part is hidden, the scroll bar, `ScrollTo` and snap points still treat its band as covered.

A floating button over the list (outside it, so the shared reveal names its `Source`), sliding out and fading:

```xml
<ContentPage.Resources>
  <sk:SkUiScrollReveal x:Key="FabReveal" Source="{x:Reference List}" Response="Triggered" />
</ContentPage.Resources>
…
<sk:SkUiButton Text="+" WidthRequest="56" HeightRequest="56" CornerRadius="28" Margin="16" HorizontalOptions="End" VerticalOptions="End">
  <sk:SkUiView.ScrollEffects>
    <sk:SkUiScrollEffect Property="TranslationY" From="0" To="88" Reveal="{StaticResource FabReveal}" />
    <sk:SkUiScrollEffect Property="Opacity" From="1" To="0" Reveal="{StaticResource FabReveal}" />
  </sk:SkUiView.ScrollEffects>
</sk:SkUiButton>
```

An extended button that shrinks to its icon: the capsule keeps its trailing end (`ClipWidth`, `ClipEdge="End"`, `ClipShape="Capsule"`, 160 → 56), the icon follows the leading edge (`TranslationX` 0 → 104; negative in right to left, as `TranslationX` does not mirror) and the label fades, all on one shared reveal. The button's shadow shrinks with the capsule.

### Reduced motion

Under `SkUiMotion.IsMotionReduced`, fades and clips run; scales, and translations that do not move exactly with the content (parallax), hold `From`; a triggered reveal's show and hide jump halfway for translation and scale while its fades still run. `ReducedMotion="Run"` or `"HoldFrom"` overrides this for one effect.

## Key properties / APIs

`Orientation`, `ScrollX`, `ScrollY`, `ContentSize`, `ViewportInsets`, `HorizontalScrollBarVisibility`, `VerticalScrollBarVisibility`, `VerticalScrollBar`, `HorizontalScrollBar`, `Overscroll`, `SnapPointsType`, `SnapPointsAlignment`, `PullEdges`, `OverscrollX`, `OverscrollY`, `Overscrolled`, `PullReleased`, `IsScrolling`, `Scrolled`, `ScrollToRequested`, `ScrollTo`, `ScrollToAsync` (offset, element, Core node), `GetScrollPositionForElement`, `AnimateScrollTo`. Core: [`SkUiCoreScrollView`](SkUiCore.md) has the same features (`Set*` setters, `ScrollToAsync(SkUiCoreNode, …)`, `GetScrollPositionForNode`).

## Differences from MAUI ScrollView

| Topic | SkiaUi |
| --- | --- |
| Hosting | SkiaUi content on the shared surface (prefer this over nesting MAUI ScrollView around SkiaUi) |
| Scroll bars | Drawn by the look, the same on every platform; `Default` fades as on mobile, and a hovering pointer expands the bar for dragging and paging (desktop); bars can be styled and placed by the app |
| Bounce / overscroll | Per look or per scroller (`Overscroll`), not only the platform's |
| Snap points | `SnapPointsType` / `SnapPointsAlignment` on the content's children (MAUI has them on CollectionView only) |
| Scroll-linked effects | [`ScrollEffects`](#scroll-linked-effects) on any view, on the render thread (MAUI: a `Scrolled` handler setting `TranslationY` / `Opacity` on the UI thread) |
| Overscroll events | `Overscrolled`, `PullReleased` and `PullEdges` for effects and gestures at the edges; MAUI has none (a `RefreshView` around the scroller ports as `SkUiRefreshView`) |
| Content insets | `ViewportInsets` says what bars drawn over the scroller cover, for every behavior (iOS `contentInset` / Android `clipToPadding="false"`; MAUI has no API) |
| Nested scrolling | Supported (axis-aware, chaining, fling hand-off; also with Core `SkUiCoreScrollView` and native ancestors) |
| `ScrollToAsync` tasks | Cancelled (not completed) when a newer scroll, a drag or unloading supersedes them |
| Overlay snapshot while scrolling | `SkUiMauiContentView.ScrollMode` (snapshot on Android / Windows, live on Apple by default) |
| `ControlTemplate` | MAUI's ScrollView has none; this one ([inherited](SkUiContentView.md#controltemplate)) scrolls the whole template, wrapper and content. For a fixed wrapper, put the scroll view inside the template |

## Related

[ScrollingAndCollectionViews.md](../design/ScrollingAndCollectionViews.md) · [SkUiRefreshView](SkUiRefreshView.md) · Gallery: `ScrollViewDemoPage` (`PullEdges`, overscroll in the status line), `ScrollEffectsDemoPage` (the scroll-linked recipes) · Samples app: *Scroll effects* (`TripScrollEffectsSample`: a trip's page combining them, with layered parallax, sticky chips, slide-in cards and a back-to-top button) · Tests: `ScrollViewOverscrollTests`, `ScrollEffectTests`, `ScrollRevealTests`, `ScrollLinkTests`
