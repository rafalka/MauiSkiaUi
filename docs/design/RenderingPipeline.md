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
| `SkUiRenderProps` | Frame offset/size, translation, rotation, scale, anchor, opacity, `ClipToBounds`, children offset and clip, content spin, ink overflow | Nothing is recorded; composite-time only |

Children are composited by the engine between the Content and Overlay pictures. Containers express how their children are drawn through properties instead of drawing them in `OnPaintContent`:

- **Scroll offset:** `SkUiScrollView` sets `ChildrenOffset` and a viewport `ChildrenClipRect`.
- **Rounded clip:** `SkUiBorder` / `SkUiCoreBorder` set `ChildrenClipPath`.
- **Spinning content:** activity indicators set `ContentSpinPeriod`, and the compositor rotates the recorded arc.

### Invalidation rules

| Change | Flag | Re-record? |
| --- | --- | --- |
| `InvalidatePaint()` (colors, text, chrome) | `Content` | This node |
| Arrange moves the node | `Props` | No |
| Arrange resizes the node | `Props` | This node (content depends on size) |
| Transform / opacity / `ClipToBounds` / scroll offset | `Props` | No |
| Child added, removed or reordered | `Children` | No (new children record once) |
| Detach from the tree | subtree reset | Re-recorded on reattach; the compositor disposes the old pictures |

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
- Detaching a node cancels its animations. Content spin needs no bookkeeping: it runs only while the node is actually drawn, so culled or hidden spinners stop requesting frames.

## Hit-testing and input

Input arrives on the UI thread and is hit-tested against UI-side state (frames, transforms, `ScrollX` / `ScrollY`). During a render-thread animation that state is at most one reported frame behind.

The GPU surfaces deliver native multi-pointer touches (pointer ids, DIP coordinates) plus wheel / trackpad scroll. The software path uses SkiaSharp touch events.

## Shadows (FR-20) — what the pipeline already provides

- `SkUiRenderProps.Overflow` / `VisualBounds`: ink bounds outside the layout rect. Culling, picture cull rects and opacity layers already use them.
- `ClipToBounds` is opt-in for layouts (MAUI parity). A child's shadow is therefore not cut off by its parent unless the parent opts in, and a shadow can be drawn before the node's own clip.
- A shadow's properties can become render-node properties, animated like opacity. Its blurred raster can be cached per node, keyed by shape / size / radius, independently of the content picture.

## Not done yet / next steps

- Raster caching of stable subtrees (Flutter-style: rasterize after N stable frames, with a per-frame budget) on top of the per-node pictures.
- Recording off the UI thread for Core-only subtrees (Core nodes are not `BindableObject`s).
- Hit-testing against render-thread transforms during running animations.

## References

- Flutter `PipelineOwner` / repaint boundaries / `markNeedsCompositedLayerUpdate` (composite-time property updates).
- Avalonia compositor: batched UI → render-thread commits, render-thread composition animations.
- Uno Skia: per-visual `SKPicture`, UI-thread recording plus render-thread replay, `UnoSKMetalView` (paused `MTKView` + display link).
- DrawnUi `SKMetalViewRetained` (shared Metal context across views).
