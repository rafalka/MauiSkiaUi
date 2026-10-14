# SkUiShimmer

A skeleton loading view: while data loads it shows a skeleton of the page or part of it (the bones) with an animation over it, then the content. `SkUiCoreShimmer` is the Core twin and draws the same pixels; it is the recommended way to build loading screens. `SkUiShimmerBone` / `SkUiCoreShimmerBone` are bones that shimmer by themselves among content that does not (`Mode="Bones"`), also in a skeleton laid over a page that stays in MAUI.

**Counterpart:** none in MAUI or the Community Toolkit: SkiaUi's own API. The skeleton is yours (no built-in layouts). Porting a hand-made skeleton view: [Migration.md](../Migration.md#shimmer).

## How it works

`SkUiShimmer` derives from [`SkUiContentView`](SkUiContentView.md) (`SkUiCoreShimmer` from `SkUiCoreNode`). It owns two children and shows one:

- **While `IsLoading`** (the default: a shimmer is usually created while its data loads): `SkeletonContent`, any drawn views of either layer. Their shapes are the bones: boxes, rounded rectangles, ellipses, borders, shapes, also labels and images.
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
| `SkeletonContent` | `null` | The bones (SkUi: also `SkeletonContentTemplate`, created the first time the skeleton shows) |
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

Fluent setters: `SetSkeletonContent`, `SetIsLoading`, `SetMode`, `SetEffect`, `SetBoneColor`, `SetHighlightColor`, `SetWaveWidth`, `SetWaveDirection`, `SetWaveRegion`, `SetDuration`, `SetRepeatMode`, `SetEasing`, `SetPulseOpacity`; Core also `SetContent`, `SetPadding`, `SetBeforeStateChangeAnimation`, `SetAfterStateChangeAnimation`. Invalid values (a width or duration that is not positive, an opacity outside 0–1) are ignored by the bindable properties and rejected by the setters.

**`SkUiShimmerBone` / `SkUiCoreShimmerBone`:** `CornerRadius` (per corner; larger radii are scaled down), `Color` (`null`: the shimmer's `BoneColor`, else the look's). Measures 40 × 40 unless sized, as MAUI's `BoxView`. Fluent `SetColor`, `SetCornerRadius`.

### Look

- `GetShimmerColors()` returns `SkUiShimmerColors(Bone, Highlight)`: by default bones halfway between the scheme's `TrackOff` and its background, a wave lighter than the bones.
- `ShimmerWaveTilt`: the default direction's tilt in degrees.
- `CreateShimmerWaveShader(SkUiShimmerWavePaint)` / `ShimmerWaveShader`: the wave band's shader for its `Bounds` (x across the band, in the direction it moves). The default is a soft gradient, transparent at both edges. The shader is made once when the wave's settings or size change (one per bone in `Bones` mode) and the band filled with it is moved on the render thread, drawn only where bones are: a look's wave is as cheap as the default one.

## Integration

- **State container:** a shimmer is a drawn view, so it can be the loading state view of an [`SkUiStateContainer`](SkUiStateContainer.md) (a shimmer with only `SkeletonContent`), with the container's fade.
- **Collection views:** a shimmer around the list with placeholder rows (above), or a shimmer per row bound to the row's own loading state, with `WaveRegion="Surface"` so the rows show one wave.

## Accessibility

While the skeleton shows, the shimmer is one element, a progress bar read as "Loading" (`SkUiStrings.LoadingText`, translatable); the bones are not read. Once loaded, the content is read as usual.

## Not built yet

C9b ([ImplementationPlan.md](../design/ImplementationPlan.md)): a skeleton made from the content itself (each control drawing its own bones: label lines, image and chrome shapes), placeholder rows over a collection view's item template.

## Related

Gallery: `ShimmerDemoPage` (playground with presets: the default wave, a skeleton's fade, a color shift), `ShimmerBonesDemoPage` (cards with bones, bones or silhouette), `CoreShimmerDemoPage` (a feed loading, on the Core flyout), `CoreShimmerBonesDemoPage` (Core cards with bones), `ShimmerRowsDemoPage` (a collection view of shimmering rows). Samples app: **Loading skeleton over a MAUI page**. Leak scenario `ShimmersShown`, benchmarks `shimmer-page`, `shimmer-rows` and `shimmer-bones`.
