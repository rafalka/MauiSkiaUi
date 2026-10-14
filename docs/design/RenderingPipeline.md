# SkiaUi rendering pipeline and threading model

This document is the source of truth for how a frame is produced and which thread does what (NFR-6). It supersedes the "v1 full-tree redraw" model that [DrawingMechanism.md](DrawingMechanism.md) used to describe. Layer semantics, clipping, and transparency rules in that document still apply.

## Goal

The MAUI UI thread is often overloaded (bindings, layout, app code). SkiaUi keeps as much work as possible off it:

- **UI thread:** only what must read MAUI objects — layout, recording the nodes whose content changed, and committing the result.
- **Render thread:** compositing, rasterization, and every composite-time animation. Scrolling, fling, spinners and `AnimateAsync` keep their frame rate while the UI thread is blocked.

## Frame flow

```
UI thread                                             Render thread (Metal / GL)
─────────                                             ─────────────────────────
property change ─► SkUiRenderInvalidation.Mark
                   (marks node, stops at first
                    already-marked ancestor)
                        │ first mark per frame
                        ▼
              SkUiFrameRenderer.RequestFrame (coalesced)
                        │ Dispatcher
                        ▼
              PresentFrame: SkUiRenderRecorder.Sync
                - walks dirty paths only
                - re-records pictures of nodes with
                  Content dirty (or size change)
                - snapshots composite props
                - collects queued render animations
                        │ SkUiRenderBatch
                        ▼
              SkUiCompositor.Commit ─── lock-free handoff ──► Render(canvas, time)
              surface.RequestRender ──────────────────────►   apply batches
                                                               tick render animations
                                                               draw retained tree
                                                               ◄── feedback posted to UI
                                                                   (scroll offsets, completion)
```

## Retained render tree

Every drawn node (`SkUiView`, `SkUiCoreNode`) implements `ISkUiRenderable`. It owns an `SkUiRenderState` on the UI side, which feeds an `SkUiRenderNode` on the render side. Nothing on the render thread touches MAUI objects.

| Render node field | Source | Changing it costs |
| --- | --- | --- |
| `Before` picture | Background + Content phases (`PaintBackground` / `OnPaintBackground`, `OnPaintContent`) | Re-record this node only |
| `After` picture | Overlay phase | Re-record this node only |
| Children | `GetRenderChildren` (paint order, ZIndex) | Children list resend, no re-record |
| `SkUiRenderProps` | Frame offset/size, translation, rotation, perspective tilt, scale, anchor, opacity, `ClipToBounds`, clip path (`Clip`), shadow, children offset, scale and clip, pinned flag, scroll link, item effect and depth order, content spin, blend mode, children layer and opacity, surface anchoring, timeline, ink overflow | Nothing is recorded; composite-time only |

Children are composited by the engine between the Content and Overlay pictures. Containers express how their children are drawn through properties instead of drawing them in `OnPaintContent`:

- **Scroll offset:** `SkUiScrollView` sets `ChildrenOffset` and a viewport `ChildrenClipRect`. Overscroll uses the same children transform: a bounce is an offset past its range, a stretch is a `ChildrenScaleX` / `ChildrenScaleY` about `ChildrenScaleOrigin` (both animatable on the render thread).
- **Pinned children:** a child with `Pinned` is placed in its parent's local coordinates, so the children offset, scale and clip do not apply to it (only the parent's own clips); pinned children draw above, and are hit before, their siblings. Scroll bars are pinned Core nodes, also in a scroller's reserved gutter outside its children clip.
- **Scroll-linked children:** a `Link` (`SkUiRenderLink`) makes a node's translation `clamp(factor · offset + base, min, max)` of an ancestor's children offset, evaluated by the compositor every frame. A scroll bar thumb follows a render-thread fling this way with no UI-thread work; the UI side writes the same translation for hit-testing and the immediate painter. Pinned headers and parallax can use the same mechanism.
- **Item effects:** an `ItemEffect` (`SkUiItemEffectLink`, a carousel's) makes a node's whole placement a function of an ancestor scroller's children offset: the item's position `factor · offset + base` goes through the carousel's effect (`SkUiCarouselEffect.GetItemTransform`, pure and allocation-free) into translation, scale, rotation, a perspective tilt (`RotationX` / `RotationY` with `CameraDistance`, part of the props' matrix: a projective 2D matrix, also inverted for hit-testing) and an opacity the compositor multiplies in. A parent with `SortsChildrenByDepth` draws such children back to front by the effect's depth (sorted per frame on the stack). The UI side writes the same placement, but not the opacity, so an item faded out is still recorded and shows as soon as the render thread fades it in; its render children list is sorted by the same depth, so nearer items are hit first.
- **Scroll corrections:** when content before the viewport changes size (a virtual list measuring items it estimated), the scroller's offset moves by the same amount. While a fling, animated scroll or snap runs on the render thread, the correction travels with the committed frame (`SkUiRenderUpdate.ScrollShift`) and the compositor shifts the running motion and the shown offset when it applies that frame, so the new layout and the corrected offset show together and the motion continues ([ScrollingAndCollectionViews.md](ScrollingAndCollectionViews.md#virtual-stack-implemented)).
- **Rounded clip:** `SkUiBorder` / `SkUiCoreBorder` set `ChildrenClipPath`.
- **Spinning content:** activity indicators set `ContentSpinPeriod`, and the compositor rotates the recorded arc.
- **Grouped and blended children, clock-driven values:** `ChildrenLayer`, `BlendMode`, `SurfaceAnchored` and `Timeline` ([below](#composite-time-effects)) compose effects out of plain nodes, such as a shimmer's skeleton.

### Invalidation rules

| Change | Flag | Re-record? |
| --- | --- | --- |
| `InvalidatePaint()` (colors, text, chrome) | `Content` | This node |
| Arrange moves the node | `Props` | No |
| Arrange resizes the node | `Props` | This node (content depends on size) |
| Transform / opacity / `ClipToBounds` / scroll offset | `Props` | No |
| Child added, removed or reordered | `Children` | No (new children record once) |
| Detach from the tree | subtree reset | Re-recorded on reattach; the compositor disposes the old pictures and effects; nodes release what they keep only for drawing (text blobs, recording-only paths) |

The first mark in a frame walks up to the root. Later marks stop at the first ancestor that is already marked. N changes therefore cost O(N + depth), not O(N × depth).

The immediate `ISkUiView.Paint(SKCanvas)` / `ISkUiCoreNode.Paint` path (`SkUiImmediatePainter`) composes the same phases from live objects in the same order. It is used for snapshots and tests, and a test checks that its output matches the compositor's pixel for pixel.

## Threads per surface

| Surface | Created when | UI thread | Render thread |
| --- | --- | --- | --- |
| **iOS / Mac Catalyst GPU** (`SkUiMetalView`) | `HwAccelerated = true` | record + commit, touch | Shared `SkUiMetalRenderLoop` thread: one `MTLDevice` / queue / `GRContext`; `CADisplayLink` on its own run loop, paused when idle. A commit that arrives while idle (nothing presented for half a refresh period) is rendered at once instead of on the next tick; surfaces render nothing before their first commit |
| **Android GPU** (`SkUiGlTextureView`) | `HwAccelerated = true` | record + commit, touch | The view's GL thread (`GLTextureView`, render-when-dirty; while animating, one frame per vsync from a `Choreographer` on a dedicated looper thread (`SkUiVsync`), since a `TextureView` swap does not block) |
| **Software** (`SKCanvasView`) | `HwAccelerated = false` | record + commit + composite | — |
| **Windows** (`SKGLView` / `SKCanvasView`) | either | record + commit + composite; continuous frames paced to `CompositionTarget.Rendering`, stopped while unloaded | — |

Metal replaces SkiaSharp's MAUI `SKGLView` on Apple platforms. That view is backed by the deprecated GLKView/OpenGL ES, and it needed a full-frame offscreen blit to avoid ghost strokes. Rendering pauses in the background and resumes in the foreground, because iOS forbids GPU work in the background.

## Animation tiers

| Tier | API | Thread | Examples |
| --- | --- | --- | --- |
| **Render-thread** (preferred) | `SkUiView.AnimateAsync`, `SkUiCoreNode.AnimateAsync`; internal `SkUiRenderTween` / `SkUiRenderFling`; `ContentSpinPeriod` | Render | Opacity, translation, rotation, scale; fling; `ScrollToAsync`; spinners |
| **UI clock** | `SkUiAnimationClock.Start(Action<double>, …)` | UI (vsync ticker: Choreographer / CADisplayLink in common modes / dispatcher) | Anything that must change arbitrary properties (colors, sizes); every tick re-records the affected nodes |

Render-thread animation rules:

- Values are reported back to the UI thread: every frame for scroll offsets, at completion for `AnimateAsync`. The reported value is *acknowledged* (`SkUiRenderState.Acknowledge`), so writing it back to the bindable property does not count as a UI change.
- If the UI explicitly sets an animated property, the animation is cancelled (superseded) and the UI's value wins.
- A new animation of the same property cancels the previous one.
- Detaching a node cancels its animations. Content spin, content slide and timelines need no bookkeeping: they run only while the node is actually drawn, so culled or hidden spinners and shimmers stop requesting frames.
- **One clock** (`SkUiRenderClock`): every surface's render thread composites with the same monotonic time (the Metal loop, GL views, Windows and software surfaces, and headless renderers by default). Animations that follow the clock instead of their own start (spinners, sliding content, shimmers) are in phase on every surface.

## Hit-testing and input

Input arrives on the UI thread and is hit-tested against UI-side state (frames, transforms, `ScrollX` / `ScrollY`). During a render-thread animation that state is at most one reported frame behind.

The GPU surfaces deliver native multi-pointer touches (pointer ids, DIP coordinates) plus wheel / trackpad scroll. The software path uses SkiaSharp touch events.

## Shadows and clips (FR-20, FR-11)

Both are render-node properties (`SkUiRenderProps.Shadow`, `ClipPath`), resolved on the UI thread by `SkUiVisualEffects` (one per node that has either) and reused while nothing they depend on changes, so committing the same frame twice commits the same objects.

Per node the compositor draws: transform → cull by `InkBounds` (visual bounds plus the shadow) → opacity layer (`LayerBounds`, which includes the shadow) → **shadow** → body (`ClipToBounds`, `ClipPath`, content, children, overlay). The shadow is outside the node's own clips; a parent's clips apply.

| Shadow kind | When | How it is drawn |
| --- | --- | --- |
| Outline | The node's own fill is opaque (`ISkUiShadowCaster.CreateShadowOutline`: background rectangle, label / button chrome, border outline with its opaque stroke, box, a shape's opaque fill and undashed opaque stroke), intersected with `ClipPath` | The path blurred by a mask filter (Skia caches blur masks; analytic on the GPU for rounded rectangles) |
| Content | Anything else (text, images, translucent or dashed shapes, layouts without an opaque background) | The alpha of the node's body (with its children) blurred and filled with the shadow's color or gradient (`SkUiShadowPainter`). The compositor rasterizes it into a per-node raster once the subtree has not changed since the last frame, and reuses it until the subtree's version, the shadow or the density changes; while the subtree changes from frame to frame (or has spinning content) it is drawn live through layers. A shadow drawn live because it just changed requests one more frame, so it is rasterized while idle instead of in the first frame of the next scroll; at most `MaxShadowRasterizationsPerFrame` (3) are rasterized per frame, the rest follow in the next frames |

**Subtree versions.** On the render thread every applied update and animation tick bumps `SkUiRenderNode.Version` on the affected node and its ancestors (once per pass). A node's own offset, transform and opacity only bump its ancestors, since the cached raster is in the node's own coordinates: moving, fading or scaling a shadowed node, and scrolling the list around it, reuse its raster. `SkUiCompositor.ShadowRasterizations` / `LiveShadows` count both paths for tests.

Shadow and clip changes are `Props` changes: no re-record. The compositor disposes the clip paths (`ClipPath`, `ChildrenClipPath`, `ContentClipPath`) and shadow objects (outline, shader, blur filters) a commit replaces, on the render thread between frames: the UI side keeps only its newest ones, so nothing else uses them. It also disposes a node's committed ones when the node is removed, when the compositor is disposed and when a batch is never applied: a render node leaves the tree only when its UI node is reset (detached, surface torn down), and a reset node forgets them (`ISkUiRenderable.ReleaseDrawingResources`) and makes new ones, so none is committed or read again. Nothing is left to the finalizer, whose disposals would contend with drawing.

## Composite-time effects

Four generic render props let controls build effects out of ordinary nodes that are recorded once, with nothing recorded per frame. The engine knows no control that uses them.

| Prop | What the compositor does |
| --- | --- |
| `BlendMode` | Composites the node onto what its parent drew before it with this mode (through a layer bounded to the node; `SrcOver`: none) |
| `ChildrenLayer`, `ChildrenOpacity` | Draws the node's children into one layer on its bounds, composited with `ChildrenOpacity`: the children's blend modes act within the group only, and the group fades as one |
| `SurfaceAnchored` | Places the node in surface DIPs instead of its parent's children space (the ancestors' clips and layers still apply): one gradient across separate views |
| `Timeline` (`SkUiRenderTimeline`) | Drives translation, rotation, scale, opacity or children opacity from the render clock: from / to values per pass, `Reverse`, an easing; evaluated each time the node is drawn, replacing the committed values |

A timeline's position is the shared clock's time in passes (`passes % 1`, or a triangle over two passes when reversing), not the time since it was committed, so nodes with the same timing are in phase on every surface, whenever they were shown or recycled. A node with a timeline requests the next frame whenever it is visited, also when it is itself outside the clip (a moving node passes out of view and back), and never while its parent is culled or hidden. Hit-testing and semantics keep the committed values, as for a carousel item's effect.

**A shimmer (FR-31)** is built from them ([SkUiShimmer.md](../controls/SkUiShimmer.md)): the shimmer node has `ChildrenLayer`; its children are the skeleton's bones and, over them, internal nodes recorded once (`SkUiShimmerPart`): a tint rectangle drawn source-in (the bones in one color, their alpha kept), a color-shift rectangle (`BlendMode` `SrcATop`, an opacity timeline) and the wave band, recorded once with the look's shader in a source-atop paint (so it needs no layer; a translation timeline, `SurfaceAnchored` for the surface-wide region). A pulse is a timeline of the shimmer's `ChildrenOpacity`. Per frame: the children layer only, for every effect but the color shift (its rectangle's opacity is animated, so it is composited through a layer of its own).

The immediate painter evaluates timelines at time zero (a still frame) and draws surface-anchored nodes in their parent's space. Content shadows of an ancestor of a surface-anchored node rasterize it in local coordinates.

## GPU pipeline warm-up

The first frame that uses a new GPU pipeline (a combination of shape, paint, clip and layer) stalls while the GPU compiles it: 25–65 ms on Metal, measured with the frame trace below, until the OS shader cache holds it. `SkUiGpuWarmUp` draws the operations SkiaUi uses (rectangles and rounded rectangles, gradients, paths, gradient and dashed strokes, arcs, text, images, clips, opacity and blur layers, blurred outlines; plain, rotated and scaled) into a small offscreen GPU surface and flushes it, one step per frame once the surface has drawn nothing for a second (Metal: the shared context, on idle display-link ticks; Android: each GL view's context, on renders it schedules). An app's first screens, which draw several frames in a row, never wait for it; a later interaction waits for at most one step. On a cold shader cache it removes the stalls of the common pipelines (rectangles, rounded rectangles, gradients, clips, card shadows); combinations it does not draw still compile on first use.

## Frame trace (diagnostics builds)

`SkUiView.StartFrameTrace()` / `StopFrameTrace()` (internal, `SKUI_DIAGNOSTICS`) record per frame: apply, animations, draw, Skia flush and present (Metal), batches and updates, garbage collections, content shadows rasterized, and the interval since the previous frame. The demo's **Effects stress** page reports each scroll's slowest frames with them. Radius converts to sigma as Android and Skia do (`SKMaskFilter.ConvertRadiusToSigma`). The immediate painter draws content shadows live, giving the same pixels (tested).

## Not done yet / next steps

- Raster caching of stable subtrees (Flutter-style: rasterize after N stable frames, with a per-frame budget) on top of the per-node pictures.
- Recording off the UI thread for Core-only subtrees (Core nodes are not `BindableObject`s).
- Hit-testing against render-thread transforms during running animations.

## References

- Flutter `PipelineOwner` / repaint boundaries / `markNeedsCompositedLayerUpdate` (composite-time property updates).
- Avalonia compositor: batched UI → render-thread commits, render-thread composition animations.
- Uno Skia: per-visual `SKPicture`, UI-thread recording plus render-thread replay, `UnoSKMetalView` (paused `MTKView` + display link).
- DrawnUi `SKMetalViewRetained` (shared Metal context across views).
