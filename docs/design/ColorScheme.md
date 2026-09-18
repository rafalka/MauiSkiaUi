# Color scheme (default palette)

Design notes for **FR-19** in [Requirements.md](Requirements.md).

This is **not**:

- **Control look** ([ControlLook.md](ControlLook.md) / FR-18) — shapes, painters, default widths/heights
- **MAUI styles** (FR-12) — per-control `Style` / `VisualState` / resource setters on bindable properties
- MAUI `AppTheme` / `AppThemeBinding` as the SkiaUi API (apps may still sync a scheme from system light/dark if they want)

## Naming

| Term | Means |
| --- | --- |
| **Color scheme** (`SkUiColorScheme`) | Shared **default palette** for Core and MAUI-compatible SkiaUi controls (accent, backgrounds, muted/track/disabled, default text, …) |
| **Control look** | Geometry / drawing / default sizes |
| **MAUI styles** | Explicit property values on individual controls via `Style` / VSM |

Prefer **“color scheme”** or **“palette”** in public API and docs. Avoid calling schemes “themes” so they are not confused with MAUI theming or control look.

## Goal

One **extensible scheme object** supplies the default colors that both `SkUi*` and `SkUiCore*` use when a control property is not explicitly set. Apps can:

1. **Replace the entire scheme** — e.g. light pack, dark pack, brand pack.
2. **Change particular colors** — e.g. only `Accent`, or only `DefaultBackground` — via property setters on the scheme instance, subclass overrides, or (open) replaceable slots.

Today’s static `SkUiColors` (Accent, Muted, TrackOff, Disabled) is the seed for the **default** scheme.

## Proposed API shape (sketch — names open)

```csharp
public class SkUiColorScheme
{
    public virtual Color Accent { get; set; }
    public virtual Color DefaultBackground { get; set; }
    public virtual Color DefaultForeground { get; set; }  // default text / icons
    public virtual Color Muted { get; set; }
    public virtual Color TrackOff { get; set; }
    public virtual Color Disabled { get; set; }
    // … additional tokens as controls need them
}

// Built-in packs
public sealed class LightSkUiColorScheme : SkUiColorScheme { … }  // today’s SkUiColors values
public sealed class DarkSkUiColorScheme : SkUiColorScheme { … }

// App-wide current scheme (exact static/DI API open — mirror control look).
SkUiColorScheme.Current = new DarkSkUiColorScheme();

// Partial change without a new pack:
SkUiColorScheme.Current.Accent = Color.FromArgb("#FF5722");

// Or subclass / copy:
var scheme = new LightSkUiColorScheme { Accent = Colors.Purple };
```

Controls initialize (or resolve) defaults from the **active** scheme:

```csharp
// Sketch — construction / first paint
_onColor = scheme.Accent;
_fillColor = scheme.Accent;
_trackOff = scheme.TrackOff;
```

## Resolution / precedence (proposed)

**For a given control property** (e.g. `SkUiButton.FillColor`):

1. Value **explicitly set** by the app (CLR setter, fluent `Set*`, XAML attribute, or FR-12 `Style` / VSM) wins.
2. Else **control-local scheme** (optional) if set.
3. Else **tree / host scheme** (optional) if set.
4. Else **`SkUiColorScheme.Current`**.
5. Else built-in **light / default** scheme.

Exact semantics for “explicitly set” vs “still following scheme” are open (e.g. unset sentinel, or apply scheme only at construction). Document the chosen rule so live scheme swaps behave predictably.

Changing the active scheme must invalidate paint for controls that still follow scheme defaults (and optionally push updated defaults into controls that opted in to live tracking).

## Relationship to FR-12 and FR-18

```
SkUiColorScheme  →  default Color values (accent, bg, …)
SkUiLook         →  how those colors are painted + default W×H
MAUI Style/VSM   →  optional per-control property overrides (SkUi* only)
```

- Core has **no** MAUI styles; Core depends on **scheme + look** (+ explicit `Set*`) for appearance.
- MAUI-compatible controls use scheme/look for defaults, then FR-12 for XAML-friendly overrides.

## Built-in packs (roadmap)

| Pack | Intent |
| --- | --- |
| Light / default | Current `SkUiColors` values |
| Dark | Dark surfaces + adjusted accent/muted/disabled |
| App / library packs | Brand palettes |

## Core

Core and MAUI-compatible controls **must** resolve the same scheme so dual-layer demos stay color-aligned. Scheme types live where both layers can reference them (with look).

## Non-goals

- Replacing MAUI `Style` for per-control customization (FR-12 remains).
- Implementing full Material / Cupertino token graphs.
- Automatically mirroring OS light/dark unless the app wires that (optional helper later).

## Checklist (implementation)

- [x] Promote today’s `SkUiColors` into a public **`SkUiColorScheme`** (default / light pack) plus **`DarkSkUiColorScheme`**.
- [x] App can set global current scheme; optional per-control / per-tree override (global `Current` shipped; per-tree deferred).
- [x] App can change individual scheme colors (properties) without replacing the whole pack.
- [x] Wire Core + MAUI control **defaults** through the active scheme (`SkUiColors` accessors + construction snapshots).
- [x] Document precedence: explicit property wins; paint-time tokens via `SkUiColors` follow `Current`; construction fields snapshot `Current`.
- [x] Scheme / token change raises events; app should invalidate paint for scheme-following UI (no automatic tree walk in v1).
- [ ] Optional per-control / per-tree scheme attachment.
- [x] Gallery sample: swap light/dark + change Accent (`LookAndColorSchemePage`, route `look`).
