# SkUiShimmer

A skeleton loading view: while data loads it shows a skeleton of the page or part of it (the bones) with an animation over it, then the content. The skeleton is hand-made (`SkeletonContent`) or the content itself, each control drawing its bones ([below](#a-skeleton-from-the-content)). `SkUiCoreShimmer` is the Core twin and draws the same pixels; it is the recommended way to build loading screens. `SkUiShimmerBone` / `SkUiCoreShimmerBone` are bones that shimmer by themselves among content that does not (`Mode="Bones"`), also in a skeleton laid over a page that stays in MAUI.

**Counterpart:** none in MAUI or the Community Toolkit: SkiaUi's own API. The skeleton is yours (no built-in layouts). Porting a hand-made skeleton view: [Migration.md](../Migration.md#shimmer).

## How it works

`SkUiShimmer` derives from [`SkUiContentView`](SkUiContentView.md) (`SkUiCoreShimmer` from `SkUiCoreNode`). It owns two children and shows one:

- **While `IsLoading`** (the default: a shimmer is usually created while its data loads): `SkeletonContent`, any drawn views of either layer. Their shapes are the bones: boxes, rounded rectangles, ellipses, borders, shapes, also labels and images. Without a `SkeletonContent` (nor `SkeletonContentTemplate`), `Content` is shown as the skeleton ([a skeleton from the content](#a-skeleton-from-the-content)).
- **Otherwise:** `Content`. The side not shown is detached from the drawn tree, so its pictures are released.

**What is a bone** depends on `Mode`:

- **`Silhouette`** (default): everything the skeleton draws. The bones are recorded once; the shimmer draws them as one group, then a tint in `BoneColor` over them (the bones' own colors are replaced; their shapes, antialiased edges and opacity stay), then the effect.
- **`Bones`**: only the skeleton's `SkUiShimmerBone`s / `SkUiCoreShimmerBone`s, rectangles with a `CornerRadius` (a circle when it is half their size). Everything else draws as it is: cards, dividers, backgrounds, text, a Cancel button between the bones. Each bone draws itself with the effect; the wave is one band crossing all of them in the shimmer's coordinates. See [Bones](#bones-a-skeleton-with-chrome).

The effect, driven by the render clock:

| `Effect` | What moves | One pass (`Duration`) | Default `RepeatMode` / `Easing` |
| --- | --- | --- | --- |
| `Wave` (default) | A band of `HighlightColor`, `WaveWidth` wide, crosses the bones in `WaveDirection`, drawn only where bones are | The band enters at one edge and leaves at the other | Restart / linear |
| `Pulse` | The bones' opacity, between `PulseOpacity` and 1 | Fades in (back out in the next pass) | Reverse / `SinInOut` |
| `Color` | The bones' color, from `BoneColor` to `HighlightColor` | Shifts to `HighlightColor` (back in the next pass) | Reverse / `SinInOut` |
| `None` | Nothing: the bones in `BoneColor` | | |

Nothing is recorded and the UI thread does nothing per frame: the compositor draws the effect from the clock through generic drawables (the shimmer's over its group, or each bone's; [RenderingPipeline.md](../design/RenderingPipeline.md#composite-time-effects)). A shimmer requests frames only while it is drawn: one scrolled out of view, on a hidden page, or showing its content costs nothing.

**Every shimmer is in phase.** The effect follows one render clock shared by every surface, not the moment a shimmer started: shimmers started at different times, recycled list rows and shimmers on different surfaces all pulse and sweep together. With `WaveRegion="Surface"` the wave crosses the whole surface instead of each shimmer, so separate shimmers (cards, rows) show one wave, as if they were one.

**Reduce motion** (`SkUiMotion`) shows the bones still, in `BoneColor`.

### One shimmer per skeleton

The cost of a silhouette shimmer per frame is one group layer, whatever the effect and whatever is inside it. Put one shimmer around the whole skeleton (a page, a card, a list of rows), not one around each bone:

- 60 placeholder rows (an avatar circle and two bars each, 180 bones) in one `SkUiCoreShimmer`: one group, one effect, the bones recorded once (benchmark `shimmer-page`). On a Galaxy S9 the wave runs at 60 fps with 2.4 ms of render-thread work per frame on average (1.9 ms for the same bones standing still).
- A shimmer per row (`shimmer-rows`) is also synchronized and records nothing per frame, but each row is a group and a bindable view: 5.7 ms per frame on the S9 for the same 60 rows, and building the screen takes twice as long. Use it where rows load one by one (collection view rows bound to their own item's state).
- A list of placeholder rows: add the row's bones several times to a stack (a loop in C#, a `BindableLayout` in XAML); each row is recorded once.

### Bones: a skeleton with chrome

A skeleton often has parts that must not shimmer: white cards with a border around the bones, a divider, a footer, a Cancel button for a slow load. With `Mode="Bones"` only `SkUiShimmerBone`s (Core: `SkUiCoreShimmerBone`) animate, and the rest of the skeleton draws as it is:

- **No layer:** each bone fills its rounded rectangle (shifted or pulsed); while the band crosses it, the fill is the bone color with the band over it in one shader (corners of different radii: the band clipped to the shape). A page of 33 bones costs 33 small fills per frame: on a Galaxy S9, 60 cards of bones (`shimmer-bones`) cost the same render-thread time as one silhouette layer, 2.6 ms per frame at 60 fps.
- **One wave:** the band is positioned in the shimmer's coordinates (`WaveRegion="Surface"`: the surface's), so it crosses every bone in one sweep, also while the skeleton scrolls.
- **Colors:** `BoneColor` (else the look's); a bone's own `Color` wins.
- A bone outside a shimmer, or in a silhouette, stands still (in a silhouette it is tinted with everything else).

**A skeleton over a page that stays in MAUI.** Lay a shimmer with only `SkeletonContent` over the MAUI page in the same `Grid` cell. Its skeleton is one drawn surface, whatever its size: one platform view instead of one per bone. Give the skeleton the page's background, fade it out and hide it once it has faded (`IsShowingSkeleton` raises `PropertyChanged`), so the page gets the taps:

```xml
<Grid>
    <local:LeaderboardView />  <!-- MAUI, unchanged -->
    <sk:SkUiShimmer Mode="Bones" IsLoading="{Binding IsLoading}" BeforeStateChangeAnimation="FadeOut 350 CubicInOut"
                    IsVisible="{Binding IsShowingSkeleton, Source={RelativeSource Self}, x:DataType=sk:SkUiShimmer}">
        <sk:SkUiShimmer.SkeletonContent>
            <sk:SkUiVerticalStackLayout BackgroundColor="{StaticResource PageBackground}" Padding="24" Spacing="16">
                <sk:SkUiBorder Stroke="{StaticResource Gray200}" StrokeShape="RoundRectangle 8" BackgroundColor="White" Padding="16">
                    <sk:SkUiGrid ColumnDefinitions="Auto,*,Auto" ColumnSpacing="12">
                        <sk:SkUiShimmerBone WidthRequest="32" HeightRequest="32" CornerRadius="16" />
                        <sk:SkUiShimmerBone Grid.Column="1" WidthRequest="100" HeightRequest="18" CornerRadius="4" HorizontalOptions="Start" />
                        <sk:SkUiShimmerBone Grid.Column="2" WidthRequest="60" HeightRequest="18" CornerRadius="4" />
                    </sk:SkUiGrid>
                </sk:SkUiBorder>
                <!-- … -->
                <sk:SkUiButton Text="Cancel" Command="{Binding CancelCommand}" HorizontalOptions="Center" />
            </sk:SkUiVerticalStackLayout>
        </sk:SkUiShimmer.SkeletonContent>
    </sk:SkUiShimmer>
</Grid>
```

The samples app's **Loading skeleton over a MAUI page** is a whole leaderboard built this way.

### A skeleton from the content

Leave `SkeletonContent` out and the content is its own skeleton while loading (`ShowsContentAsSkeleton`): every control in it draws its bones instead of itself, so the skeleton has the size and shape of what loads, without a second layout to keep in step (as SwiftUI's `redacted(reason: .placeholder)` and Flutter's `skeletonizer`).

```xml
<sk:SkUiShimmer Mode="Bones" IsLoading="{Binding IsLoading}" AfterStateChangeAnimation="FadeIn 250">
    <sk:SkUiVerticalStackLayout Padding="12" Spacing="12">
        <sk:SkUiLabel Text="Team updates" FontSize="20" sk:SkUiShimmer.Skeleton="Keep" />
        <sk:SkUiBorder Stroke="{StaticResource Gray200}" StrokeShape="RoundRectangle 8" BackgroundColor="White" Padding="12">
            <sk:SkUiGrid ColumnDefinitions="Auto,*" ColumnSpacing="12">
                <sk:SkUiAvatarView Text="{Binding Author}" WidthRequest="40" HeightRequest="40" CornerRadius="20" />
                <sk:SkUiVerticalStackLayout Grid.Column="1" Spacing="6">
                    <sk:SkUiLabel Text="{Binding Author}" FontAttributes="Bold" />
                    <sk:SkUiLabel Text="{Binding Message}" sk:SkUiShimmer.SkeletonLines="2" />
                </sk:SkUiVerticalStackLayout>
            </sk:SkUiGrid>
        </sk:SkUiBorder>
    </sk:SkUiVerticalStackLayout>
</sk:SkUiShimmer>
```

**What each control draws** (the look decides the exact bones, `AddSkeletonBones`):

| Control | Bones |
| --- | --- |
| Labels | A bar per laid-out line (plain text, spans, HTML), as wide as the line and aligned as it (right to left too), 60 % of the line spacing high. A label with a background or a border is a chip: its shape |
| Empty labels | Placeholder lines: the label measures as `SkUiShimmer.SkeletonLines` lines of its font (default 1; 0: none) as wide as it may be, the last of several 60 % wide. Bindings that are empty while loading keep a sensible skeleton |
| Buttons, image buttons, boxes, images, avatars, pickers, borders without content | Their shape: the rectangle with their corner radii (and their `Clip`); ellipses a circle |
| Switches, check boxes, radio buttons (with their text), sliders, progress bars | Their outline: the track, the box, the circle, the bar |
| Activity indicators | A circle while running; nothing while stopped |
| Hosted native views (`SkUiMauiContentView`) | Their rectangle; the native view is hidden meanwhile |
| Views with children: layouts, borders and content views with content, scrollers, collection views, carousels | Nothing: a container. With `Mode="Bones"` its own drawing (a card's background and border) draws as it is around its children's bones; in a silhouette nothing of it draws. Scroll bars and pull-to-refresh indicators are hidden |

**Per view, `SkUiShimmer.Skeleton`** (Core: `node.SetValue(SkUiCoreShimmer.SkeletonProperty, …)`):

| Value | The view |
| --- | --- |
| `Default` | Its own bones as above |
| `Bone` | One bone, its rectangle, for itself and what is in it (a whole row as a block) |
| `Keep` | Draws as it is, and what is in it: a title known before the data, an icon. In a silhouette it is tinted with the bones |
| `Hidden` | Draws nothing, nor what is in it; it keeps its space |
| `Children` | Nothing of its own (not even a card's chrome in `Bones` mode); its children draw their bones |

`Hidden` and `Bone` win over what is inside them; the nearest `Keep` applies to views without a value of their own. `SkUiShimmerBone`s in the content draw themselves as usual. A shimmer inside the content decides for its own content (a row's shimmer that has loaded shows its row).

**Behavior.**

- The bones are the controls' drawables, drawn on the render thread with the shimmer's effect as `SkUiShimmerBone`s are (`Mode="Bones"`: each with no layer, one wave across all of them) or tinted with the silhouette (`Silhouette`). Nothing is recorded per frame. Entering and leaving the skeleton records each control once; a control's bones are made again only when its size, text or skeleton changes.
- The content takes no input while it is a skeleton: pointers and keyboard focus skip it (taps, and scrollers inside it). Views around the shimmer react as usual, so to scroll a long skeleton put the shimmer inside the scroller (`SkUiScrollView` → `SkUiShimmer` → the page), not the scroller inside the shimmer; put a Cancel button outside the shimmer too, or use a hand-made skeleton. Screen readers read one "Loading" element.
- Data that arrives while loading (rows added to a stack, texts set) shows as bones until `IsLoading` turns false.
- **An empty collection view shows placeholder items** instead of its `EmptyView` (`ShowsPlaceholders`): views of its `ItemTemplate` without an item (no binding context, a `DataTemplateSelector` asked with `null`), so their texts draw placeholder lines and their shapes bones, laid out with its `Span`, `ItemSpacing` and `SpanSpacing`. `SkUiShimmer.SkeletonItemCount` on the list sets how many (default -1: enough rows to fill it, three when it is not bounded along its axis; 0: none, the empty view shows). They are not items: no selection, no events, no load-more; they go as soon as items come or the skeleton ends, and the empty view shows if the list is still empty.
- The switch to the content is the same views drawing themselves again. `BeforeStateChangeAnimation="FadeOut 200"` fades the bones out and shows the content at once; add `AfterStateChangeAnimation="FadeIn 200"` to fade it in.
- Not in a skeleton (no shimmer showing its content), controls check nothing: one static read per recording.

### Loading in stages

Pages often load in steps: a first request brings the list (names only), then a request per row brings its details. Use one shimmer per step, each its content's own skeleton:

1. **The page:** a shimmer around the page, `IsLoading` bound to the first request. Its empty collection view shows placeholder rows of its own template meanwhile.
2. **Each row's details:** a shimmer inside the item template around the parts that load later, `IsLoading` bound to the row (`IsLoadingDetails`). A shimmer decides for its own content: once the page has loaded, rows show their names while their details are still bones. Placeholder rows, which have no item, show their details as bones too.
3. Give every shimmer `WaveRegion="Surface"`, so the page's and the rows' waves are one wave.

```xml
<sk:SkUiShimmer Mode="Bones" WaveRegion="Surface" IsLoading="{Binding IsLoadingTeam}">
    <sk:SkUiCollectionView ItemsSource="{Binding Members}">
        <sk:SkUiCollectionView.ItemTemplate>
            <DataTemplate x:DataType="local:MemberRow">
                <sk:SkUiVerticalStackLayout>
                    <sk:SkUiLabel Text="{Binding Name}" FontAttributes="Bold" />
                    <sk:SkUiShimmer Mode="Bones" WaveRegion="Surface" IsLoading="{Binding IsLoadingDetails}">
                        <sk:SkUiLabel Text="{Binding Activity}" sk:SkUiShimmer.SkeletonLines="2" />
                    </sk:SkUiShimmer>
                </sk:SkUiVerticalStackLayout>
            </DataTemplate>
        </sk:SkUiCollectionView.ItemTemplate>
    </sk:SkUiCollectionView>
</sk:SkUiShimmer>
```

The samples app's **Loading in stages** is a team page loaded this way when Load is tapped (one label before), with simulated requests (one for the team, then one per member, three at a time).

### Core bones, one bound property

Bones never change while loading and need no bindings, so make them plain Core nodes (`SkUiCoreBox`, `SkUiCoreBorder`, `SkUiCoreShimmerBone`, …): no bindable store, no styles, no handlers. Inside an SkUi page, put the Core skeleton in an `SkUiCoreHost` as the `SkeletonContent`, or make the whole loading view a `SkUiCoreShimmer` in an `SkUiCoreHost`. Bind only `IsLoading` (SkUi) or set it from code (Core).

## How to use

```xml
<!-- A card: bound loading state, a fade to the content -->
<sk:SkUiShimmer IsLoading="{Binding IsLoading}" BeforeStateChangeAnimation="FadeOut 350 CubicInOut">
    <sk:SkUiShimmer.SkeletonContent>
        <sk:SkUiHorizontalStackLayout Padding="12" Spacing="12">
            <sk:SkUiBox WidthRequest="48" HeightRequest="48" CornerRadius="24" Color="Black" />
            <sk:SkUiVerticalStackLayout Spacing="8" VerticalOptions="Center">
                <sk:SkUiBox WidthRequest="140" HeightRequest="16" CornerRadius="4" Color="Black" />
                <sk:SkUiBox WidthRequest="200" HeightRequest="12" CornerRadius="4" Color="Black" />
            </sk:SkUiVerticalStackLayout>
        </sk:SkUiHorizontalStackLayout>
    </sk:SkUiShimmer.SkeletonContent>
    <sk:SkUiGrid> <!-- the loaded card --> </sk:SkUiGrid>
</sk:SkUiShimmer>

<!-- Placeholder rows: the row's bones repeated by BindableLayout (PlaceholderRows: any list of eight items) -->
<sk:SkUiShimmer IsLoading="{Binding IsLoading}">
    <sk:SkUiShimmer.SkeletonContent>
        <sk:SkUiVerticalStackLayout BindableLayout.ItemsSource="{Binding PlaceholderRows}">
            <BindableLayout.ItemTemplate>
                <DataTemplate>
                    <sk:SkUiHorizontalStackLayout Padding="12,8" Spacing="10">
                        <sk:SkUiBox WidthRequest="32" HeightRequest="32" CornerRadius="16" Color="Black" />
                        <sk:SkUiBox WidthRequest="160" HeightRequest="12" CornerRadius="4" Color="Black" VerticalOptions="Center" />
                    </sk:SkUiHorizontalStackLayout>
                </DataTemplate>
            </BindableLayout.ItemTemplate>
        </sk:SkUiVerticalStackLayout>
    </sk:SkUiShimmer.SkeletonContent>
    <sk:SkUiCollectionView ItemsSource="{Binding Items}" />
</sk:SkUiShimmer>
```

```csharp
// A Core loading screen: one shimmer, one layer, every bone recorded once
var rows = new SkUiCoreVerticalStackLayout();
for (var i = 0; i < 8; i++)
    rows.Add(new SkUiCoreHorizontalStackLayout { Padding = new Thickness(12, 8), Spacing = 10 }
        .Add(new SkUiCoreBox { Color = Colors.Black, CornerRadius = 16, Width = 32, Height = 32 })
        .Add(new SkUiCoreBox { Color = Colors.Black, CornerRadius = 4, Width = 160, Height = 12, VerticalAlignment = LayoutAlignment.Center }));
var shimmer = new SkUiCoreShimmer { SkeletonContent = rows, Content = loadedList }
    .SetBeforeStateChangeAnimation(SkUiViewAnimation.FadeOut(350, Easing.CubicInOut));
host.SetContent(shimmer);
// … when the data is there:
shimmer.IsLoading = false;
```

In a silhouette the bones' color does not matter (`BoneColor` replaces it), but they must draw something: give boxes a `Color`.

### Animations of a hand-made skeleton view

Skeleton views built from MAUI shapes and `Animation`s (one animation per box) port as settings, with one effect for the whole skeleton and every box in phase:

| Hand-made animation | `SkUiShimmer` settings |
| --- | --- |
| Opacity `0.3 + 0.7 · (0.5 − 0.5 · cos 2πt)` over 1.5 s, repeating | `Effect="Pulse" PulseOpacity="0.3" Duration="750"` (passes of 750 ms there and back; the default easing `SinInOut` is that cosine) |
| Color from a start to an end color linearly in 750 ms, starting again | `Effect="Color" BoneColor="{start}" HighlightColor="{end}" Duration="750" RepeatMode="Restart" Easing="Linear"` |
| The same, going back smoothly | `Effect="Color" BoneColor="{start}" HighlightColor="{end}" Duration="750"` |
| A fade out of the skeleton when loading ends (350 ms, `CubicInOut`) | `BeforeStateChangeAnimation="FadeOut 350 CubicInOut"` |
| `IsLoading` passed down to every box | `IsLoading` on the one shimmer |
| Pausing while the page is off screen | Nothing to do: a shimmer that is not drawn requests no frames |
| Android hardware layers to keep many boxes from saturating the render thread | Nothing to do: the bones are recorded once and drawn as one group (or draw themselves, with no layer) |
| Skeleton boxes inside cards of the real layout, around a Cancel button | `Mode="Bones"` with `SkUiShimmerBone`s in the same places |

## Key properties

| Property | Default | Notes |
| --- | --- | --- |
| `SkeletonContent` | `null` | The bones (SkUi: also `SkeletonContentTemplate`, created the first time the skeleton shows). Without one, `Content` is the skeleton (`ShowsContentAsSkeleton`) |
| `Content` | `null` | Shown once no longer loading (SkUi: also `ContentTemplate`, `ContentLoading`) |
| `IsLoading` | `true` | Skeleton or content. `IsShowingSkeleton` follows it after a switch animation (raises `PropertyChanged`); `IsSwitching` while one runs |
| `Mode` | `Silhouette` | `Silhouette`: everything in the skeleton is a bone. `Bones`: only `SkUiShimmerBone`s / `SkUiCoreShimmerBone`s, the rest draws as it is |
| `Effect` | `Wave` | `Wave`, `Pulse`, `Color`, `None` |
| `BoneColor` | `null` | The bones' color; `null`: the look's (from the color scheme). Transparent keeps the bones' own colors |
| `HighlightColor` | `null` | The wave's color and the color a `Color` shift goes to; `null`: the look's |
| `WaveWidth` | 200 | The band's width in DIPs |
| `WaveDirection` | `Default` | `Default`: the reading direction (right to left in RTL layouts), tilted by the look's `ShimmerWaveTilt` (20°, the top leads). `LeftToRight`, `RightToLeft`, `TopToBottom`, `BottomToTop` move straight |
| `WaveRegion` | `Shimmer` | `Shimmer`: the wave crosses this shimmer per pass. `Surface`: it crosses the whole surface, shared with the other shimmers on it |
| `Duration` | 1000 | One pass in milliseconds. A reversing cycle takes two passes |
| `RepeatMode` | `Default` | `Restart` or `Reverse` after each pass; `Default`: the effect's own (a wave restarts, a pulse and a color shift go back) |
| `Easing` | `null` | The curve of each pass; `null`: linear for a wave, `SinInOut` otherwise. Runs on the render thread (MAUI's easings are pure functions) |
| `PulseOpacity` | 0.4 | The bones' opacity at a pulse's low point, 0–1 |
| `BeforeStateChangeAnimation` / `AfterStateChangeAnimation` | `null` | `SkUiViewAnimation`s on the child that leaves and the one that comes, as [`SkUiStateContainer`](SkUiStateContainer.md)'s (XAML `"FadeOut 350 CubicInOut"`). Core: uniform `Scale` only |

Attached, for views in a skeleton made from the content: `SkUiShimmer.Skeleton` (`SkUiSkeleton`: `Default`, `Bone`, `Keep`, `Hidden`, `Children`) `SkUiShimmer.SkeletonLines` (an empty label's placeholder lines, default 1) and, on a collection view, `SkUiShimmer.SkeletonItemCount` (its placeholder items while empty, default -1: fill it); `GetSkeleton` / `SetSkeleton`, `GetSkeletonLines` / `SetSkeletonLines`, `GetSkeletonItemCount` / `SetSkeletonItemCount`. Core: `SkUiCoreShimmer.SkeletonProperty` and `SkeletonLinesProperty` (`node.SetValue(…)`).

Fluent setters: `SetSkeletonContent`, `SetIsLoading`, `SetMode`, `SetEffect`, `SetBoneColor`, `SetHighlightColor`, `SetWaveWidth`, `SetWaveDirection`, `SetWaveRegion`, `SetDuration`, `SetRepeatMode`, `SetEasing`, `SetPulseOpacity`; Core also `SetContent`, `SetPadding`, `SetBeforeStateChangeAnimation`, `SetAfterStateChangeAnimation`. Invalid values (a width or duration that is not positive, an opacity outside 0–1) are ignored by the bindable properties and rejected by the setters.

**`SkUiShimmerBone` / `SkUiCoreShimmerBone`:** `CornerRadius` (per corner; larger radii are scaled down), `Color` (`null`: the shimmer's `BoneColor`, else the look's). Measures 40 × 40 unless sized, as MAUI's `BoxView`. Fluent `SetColor`, `SetCornerRadius`.

### Look

- `GetShimmerColors()` returns `SkUiShimmerColors(Bone, Highlight)`: by default bones halfway between the scheme's `TrackOff` and its background, a wave lighter than the bones.
- `ShimmerWaveTilt`: the default direction's tilt in degrees.
- `AddSkeletonBones(SkUiSkeletonPart, IList<SkUiSkeletonBone>)` / `SkeletonBones`: the bones of a control's part in a skeleton from the content. The part's `Kind` (`Shape`, `TextLine`, `Switch`, `CheckBox`, `RadioButton`, `Slider`, `ProgressBar`, `ActivityIndicator`), its `Bounds` and `CornerRadius` in the control's coordinates; a bone is a rectangle with corner radii. Default: shapes as they are, a text line as a bar 60 % of its spacing with 4 DIP corners, a switch's track as a pill, a check box's box, a radio button's or an activity indicator's circle, a slider's track as high as its thumb's radius, a progress bar's track.
- `CreateShimmerWaveShader(SkUiShimmerWavePaint)` / `ShimmerWaveShader`: the wave band's shader for its `Bounds` (x across the band, in the direction it moves). The default is a soft gradient, transparent at both edges. The shader is made once when the wave's settings or size change (one per bone in `Bones` mode) and the band filled with it is moved on the render thread, drawn only where bones are: a look's wave is as cheap as the default one.

## Integration

- **State container:** a shimmer is a drawn view, so it can be the loading state view of an [`SkUiStateContainer`](SkUiStateContainer.md) (a shimmer with only `SkeletonContent`), with the container's fade.
- **Collection views:** a shimmer around the list, its content the skeleton: while it is empty, placeholder items of its own template ([above](#a-skeleton-from-the-content)); a shimmer around the list with hand-made placeholder rows (above); or a shimmer per row bound to the row's own loading state, with `WaveRegion="Surface"` so the rows show one wave.

## Accessibility

While the skeleton shows, the shimmer is one element, a progress bar read as "Loading" (`SkUiStrings.LoadingText`, translatable); the bones are not read. Once loaded, the content is read as usual.

## Not built yet

Device runs of the skeleton from the content ([ImplementationPlan.md](../design/ImplementationPlan.md)).

## Related

Gallery: `ShimmerDemoPage` (playground with presets: the default wave, a skeleton's fade, a color shift), `ShimmerBonesDemoPage` (cards with bones, bones or silhouette), `CoreShimmerDemoPage` (a feed loading, on the Core flyout), `CoreShimmerBonesDemoPage` (Core cards with bones), `ShimmerContentDemoPage` (a feed that is its own skeleton, its empty member list showing placeholder rows, next to a hand-made skeleton), `ShimmerRowsDemoPage` (a collection view of shimmering rows). Samples app: **Loading skeleton over a MAUI page**, **Loading in stages**. Leak scenario `ShimmersShown`, benchmarks `shimmer-page`, `shimmer-rows`, `shimmer-bones` and `shimmer-content`; tests `ShimmerContentSkeletonTests`.
