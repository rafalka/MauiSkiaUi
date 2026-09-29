# Control look (shape / chrome)

Design notes for **FR-18** in [Requirements.md](Requirements.md).

This is **not**:

- **Color scheme** ([ColorScheme.md](ColorScheme.md) / FR-19) — default palette (accent, backgrounds, …)
- **MAUI styles** (FR-12) — per-control `Style` / `VisualState` / resource setters

## Naming

| Term | Means | Does **not** mean |
| --- | --- | --- |
| **Control look** (`SkUiLook`) | Default **geometry and drawing** of Skia-drawn controls (switch pill vs Material track, radio ring vs filled disc, spinner arc vs dots, corner radii, checkmark path) **and**, where applicable, **default intrinsic width/height** | Color palettes; MAUI `Style` setters |
| **Color scheme** (FR-19) | Default colors for Core + MAUI controls | How a RadioButton is *drawn* |
| **Styles / VisualStates** (FR-12) | Bindable property overrides on individual `SkUi*` controls | Replacing shared geometry or the shared palette |

Prefer saying **“look”** or **“control look”** in docs and API. Avoid calling look packs “themes.”

## Goal

One **extensible look object** supplies the shared Skia painters **and default sizes** that both MAUI-compatible `SkUi*` and Core `SkUiCore*` controls use (replacing the former sealed `SkUiChrome` helpers + hardcoded `MeasureContent` sizes such as Switch 51×31). Apps can:

1. **Replace the entire look** — e.g. Android-like / iOS-like / Fluent / custom brand pack (shapes **and** default sizes where they differ).
2. **Override one control’s drawing and/or default size** — subclass and override `DrawRadioButton` / `MeasureRadioButton`, or replace a single painter / size delegate, without forking every control type.

Colors come from the **color scheme** (FR-19) and/or explicit control properties / FR-12 styles; the look receives already-resolved `SKColor`s and decides **shape**. Default **width/height** (intrinsic measure) come from the look when applicable.

## API shape (sketch)

```csharp
// Public, subclassable. Built-in DefaultSkUiLook holds the default geometry + sizes (formerly SkUiChrome).
public class SkUiLook
{
    // Controls call the public, non-virtual entry points; each uses its painter delegate when set,
    // else the protected virtual *Core method that subclasses override. Each takes one paint struct
    // (room to grow without new overloads). Colors arrive resolved (disabled dimming applied).
    public void DrawSwitch(SKCanvas canvas, SkUiSwitchPaint toggle) { … }            // → SwitchPainter ?? DrawSwitchCore
    public void DrawCheckBox(SKCanvas canvas, SkUiCheckBoxPaint box) { … }           // → CheckBoxPainter ?? DrawCheckBoxCore
    public void DrawRadioButton(SKCanvas canvas, SkUiRadioButtonPaint radio) { … }   // → RadioButtonPainter ?? DrawRadioButtonCore
    public void DrawButton(SKCanvas canvas, SkUiButtonPaint button) { … }            // fill, border, press feedback → ButtonPainter ?? DrawButtonCore
    public void DrawPressOverlay(SKCanvas canvas, SkUiPressOverlayPaint overlay) { … } // over content: ImageButton, ShowsPressEffect → PressOverlayPainter ?? DrawPressOverlayCore
    // Slider / ProgressBar draw in horizontal left-to-right coordinates: the controls rotate the canvas for
    // vertical sliders and mirror RTL.
    public void DrawSlider(SKCanvas canvas, SkUiSliderPaint slider) { … }            // → SliderPainter ?? DrawSliderCore
    public void DrawProgressBar(SKCanvas canvas, SkUiProgressBarPaint bar) { … }     // → ProgressBarPainter ?? DrawProgressBarCore
    public void DrawActivityIndicator(…) { … }                                      // → ActivityIndicatorPainter ?? DrawActivityIndicatorCore
    public virtual float SliderThumbRadius => 10;         // input maps touches to the thumb's center
    public virtual float IndeterminateProgressSegment => 0.35f;
    public virtual float IndeterminateProgressPeriod => 1.5f; // seconds; the compositor slides the bar on the render thread
    public virtual float GetProgressBarCornerRadius(float height) => height / 2;
    public void DrawRoundedBox(…) { … }      // float radius and CornerRadius overloads → DrawRoundedBoxCore
    public virtual SKPath? CreateCustomRoundRectPath(SKRect bounds, CornerRadius radii) => null; // custom corner geometry (null = plain)
    public SKPath CreateRoundRectPath(…) { … } // uniform or per-corner: custom geometry, else plain corners
    public void DrawImage(…) { … }           // → ImagePainter ?? DrawImageCore

    // State-change transitions (below): duration + easing per kind; None while reduce motion is on.
    public SkUiTransition GetTransition(SkUiTransitionKind kind) { … }  // → TransitionProvider ?? GetTransitionCore

    // Default intrinsic sizes (DIPs) — public size tokens; Measure* reads these unless a size delegate is set.
    public virtual Size DefaultSwitchSize => new(51, 31);
    public virtual Size DefaultCheckBoxSize => new(24, 24);
    public virtual Size DefaultRadioButtonSize => new(24, 24);
    public virtual Size DefaultActivityIndicatorSize => new(36, 36);
    public virtual double DefaultSliderThickness => 32;   // and DefaultSliderLength when unconstrained
    public virtual double DefaultProgressBarHeight => 4;  // and DefaultProgressBarLength
    public virtual double DefaultButtonMinimumHeight => 44;
    public virtual double DefaultButtonCornerRadius => 6;

    public Size MeasureSwitch(double widthConstraint, double heightConstraint) =>
        SwitchMeasure?.Invoke(widthConstraint, heightConstraint) ?? MeasureSwitchCore(widthConstraint, heightConstraint);
    protected virtual Size MeasureSwitchCore(double widthConstraint, double heightConstraint) => DefaultSwitchSize;
    // … same pattern for CheckBox / RadioButton / ActivityIndicator
}

// App-wide current look.
SkUiLook.Current = new MaterialSkUiLook();

// Partial replace via subclass:
sealed class AppLook : DefaultSkUiLook
{
    protected override void DrawRadioButtonCore(SKCanvas canvas, SkUiRadioButtonPaint radio) { /* brand radio */ }
    public override Size DefaultSwitchSize => new(52, 32); // larger track
}

// Or per-painter / per-size delegates on the look instance:
look.RadioButtonPainter = (canvas, radio) => { … };
look.SwitchMeasure = (w, h) => new Size(52, 32);
```

Controls call **`SkUiLook.Current` (or an inherited / attached look)** for both paint and default measure instead of a sealed static `SkUiChrome` and hardcoded sizes. Keep a thin compatibility façade if needed during migration.

### State-change transitions (FR-26)

Looks draw every point of a transition, not only the resting states. Switches, check boxes and radio buttons slide or draw in between states; buttons dim or ripple while pressed; slider thumbs glide to tapped values; progress bars can smooth `Progress` changes.

**What a painter receives.**

| Paint struct | Transition data | Helpers |
| --- | --- | --- |
| `SkUiSwitchPaint`, `SkUiCheckBoxPaint`, `SkUiRadioButtonPaint` | `Visual` (`SkUiToggleVisual`): `State` (target), `From`, `Progress` (eased, 1 = settled), `Pressed` (0–1) | `Weight(state)`: how much of a state shows (the weights add up to 1). `Blend(u, c, i)`: numbers or colors per state, blended |
| `SkUiButtonPaint`, `SkUiPressOverlayPaint` | `Press` (`SkUiPressVisual`): `Pressed` (0–1), `Origin` (press point), `Ripple` (spread 0–1), `RippleFade` (0 visible, 1 gone) | `HasRipple` |
| `SkUiSliderPaint` | `Fraction` is the drawn position (glides after a tap); `Pressed` (0–1) while dragged | — |
| `SkUiProgressBarPaint` | `Progress` is the drawn fill | — |

Drawing the blend makes interruptions free: a switch toggled back mid-way reverses from where it is.

**How long, which curve.** `GetTransition(kind)` returns a `SkUiTransition(Duration, Easing)`; `None` (zero duration) turns a transition off. Kinds: `Switch`, `CheckBox`, `RadioButton`, `Press`, `Release`, `Ripple`, `SliderThumb`, `Progress`.

| Kind | `DefaultSkUiLook` |
| --- | --- |
| Switch | 200 ms, cubic in-out |
| CheckBox, RadioButton | 160 ms, cubic in-out |
| Press / Release | 80 ms / 220 ms, cubic out (a quick tap still shows its full press) |
| Ripple | 450 ms, cubic out (only with `PressEffect = Ripple`, else none: no frames spent) |
| SliderThumb | 150 ms, cubic out (taps only; drags and code follow at once) |
| Progress | None, as MAUI's `ProgressBar` (`ProgressTo` animates on its own) |

**Press feedback on any control.** `ShowsPressEffect` (every `SkUiView` and Core node) draws the look's press overlay (`DrawPressOverlay`) over the node and its children while it is pressed, clipped to its rounded shape (a label's `CornerRadii`, a border's `CornerRadius`). This is for containers that act as one button: a card, or a composite button built from several Core nodes. The node needs a tap handler (`Tapped`, or `TappedCommand` on SkUi*) to be pressed. A button inside it still takes its own presses. `DefaultSkUiLook` dims the content or spreads a dark ripple.

Override `GetTransitionCore` in a subclass, or set `TransitionProvider` on a look instance. `DefaultSkUiLook.PressEffect` picks `Dim` (default: the fill fades by a quarter) or `Ripple` (a circle spreads from the press point, clipped to the button's rounded shape, and fades after the release).

**Reduce motion.** `SkUiMotion.IsMotionReduced` follows the OS setting (iOS / Mac Catalyst Reduce Motion, Android "Remove animations", Windows animation effects off); `SkUiMotion.ReduceMotion` overrides it. While reduced, `GetTransition` returns `None` for every kind. Activity animations (spinners, the indeterminate progress bar) keep running, since they show that work is in progress.

**Where it runs.**
- Transitions run on the surface's UI-thread animation clock. Each frame re-records only the animating control's picture (ancestors are not re-recorded), and the rest of the tree is composited from retained pictures. Painters stay ordinary single-threaded code.
- Controls animate only after they have been drawn on a surface. A state set before a page appears (a constructor, a binding) shows at once.
- Stopping the clock (the page closes, the surface is replaced) jumps every transition to its end state.
- The decision and its measurements: [ArchitectureReview.md](ArchitectureReview.md#state-change-animations).

### Default width / height

Where a control has a meaningful **intrinsic** or **default** size (Switch, CheckBox, RadioButton, ActivityIndicator, button minimum height, default corner radius, …):

- The **active look** owns those defaults.
- Apps may override them by replacing the look, subclassing and overriding the measure/size members, or replacing a size delegate.
- Explicit control size requests (`Width` / `Height` / `WidthRequest` / `MinimumHeight`, fluent `SetWidth`, …) still win over look defaults for that instance.
- Changing a look’s sizes must **invalidate measure** (and paint when drawing also changes).

## Resolution order (proposed)

1. **Control-local look** (optional property / attached) if set.
2. Else **tree / host look** (optional — e.g. on `SkUiCoreHost` or root `SkUiContentView`) if set.
3. Else **`SkUiLook.Current`** (process / app default).
4. Else built-in **`DefaultSkUiLook`**.

Changing the active look must invalidate paint and, when default sizes differ, measure.

## Relationship to layers (FR-9) and color scheme (FR-19)

Look methods are the **shared Content/Background chrome implementations**. Controls still own:

- Which phase calls which painter (`OnPaintContent` vs `PaintBackground` / `PaintOverlay`).
- Property state (`CheckState`, colors, `IsEnabled` alpha) and the transition state they pass to the look.
- Interaction / commands.

Looks do **not** replace `PaintBackground` / `OnPaintContent` hooks; they are what those hooks call for stock geometry. Looks do **not** own the palette — that is **FR-19**.

## Built-in packs (roadmap)

| Pack | Intent |
| --- | --- |
| `DefaultSkUiLook` | Current cross-platform geometry and sizes (the former `SkUiChrome` painters + hardcoded measures) |
| Platform-inspired packs (later) | Android-like, iOS-like, etc. — approximate shapes **and** typical default sizes |
| App / library packs | Brand-specific subclasses or delegate replacements |

Shipping every OS-faithful pack is **not** required for FR-18 v1; v1 ships the extensibility model + default look (including overridable default sizes).

## Core

Core and MAUI-compatible controls **must** use the same look resolution so Stress / dual-layer demos stay visually aligned. Look types live in a namespace both layers can reference (likely next to Core or in the shared helpers area once Core is a separate assembly).

## Non-goals

- Owning color tokens (FR-19 / [ColorScheme.md](ColorScheme.md)).
- Replacing FR-12 MAUI `Style` for per-control property overrides.
- Full native design-system parity (Material 3 / Cupertino every detail).
- Per-frame look allocation (look instances should be long-lived; hot path = virtual call / delegate invoke only).

## Checklist (implementation)

- [x] Promote today’s `SkUiChrome` painters into a public subclassable **`SkUiLook`** / **`DefaultSkUiLook`**.
- [x] Look exposes **default width/height** (and related defaults such as corner radius / min height) for controls that use intrinsic sizes; controls’ `MeasureContent` reads the active look when applicable.
- [x] App can set global current look (`SkUiLook.Current`).
- [x] Single-control override via virtual method **or** replaceable painter / size delegate (drawing and/or default size).
- [x] Wire `SkUi*` and `SkUiCore*` Content painters **and** default measures through the active look.
- [x] Document naming vs FR-12 / FR-19; tests cover swap look + override painter and measure.
- [x] Look change raises `CurrentChanged`; app should invalidate measure/paint when sizes change (no automatic tree walk in v1).
- [x] State-change transitions: paint structs carry the transition, `GetTransition` / `TransitionProvider`, reduce motion (FR-26).
- [ ] Optional per-control / per-tree look attachment.
- [x] Gallery sample page for look packs (`LookAndColorSchemePage`, route `look`).
