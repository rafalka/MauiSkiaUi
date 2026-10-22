# Color scheme (default palette)

The default palette of SkiaUi's controls (FR-19 in [Requirements.md](Requirements.md#functional-requirements)). Shapes and sizes are the look's: [ControlLook.md](ControlLook.md).

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
2. **Change particular colors** — e.g. only `Accent`, or only `DefaultBackground` — via property setters on the scheme instance or subclass overrides.

`SkUiColors` reads tokens from the active scheme at paint time.

## API

```csharp
public class SkUiColorScheme
{
    public virtual Color Accent { get; set; }
    public virtual Color DefaultBackground { get; set; }
    public virtual Color DefaultForeground { get; set; }  // default text / icons
    public virtual Color Muted { get; set; }
    public virtual Color TrackOff { get; set; }
    public virtual Color Disabled { get; set; }
    public virtual Color Error { get; set; }  // invalid input: text fields' HasError chrome and message (D10)
    // Control tokens: until set, each follows the scheme's fallback (another token or a fixed color)
    public virtual Color ButtonBackground { get; set; }   // Accent; iOS / Mac: transparent; Windows: Fluent's control fill
    public virtual Color ButtonForeground { get; set; }   // white; iOS / Mac: Accent; Windows: DefaultForeground
    public virtual Color ButtonBorder { get; set; }       // transparent; Windows: Fluent's control stroke
    public virtual Color ActivityIndicator { get; set; }  // Muted (iOS's gray spinner); Android, Windows: Accent
    // … additional tokens as controls need them
}

// Built-in packs
public sealed class LightSkUiColorScheme : SkUiColorScheme { … }  // today’s SkUiColors values
public sealed class DarkSkUiColorScheme : SkUiColorScheme { … }

// App-wide current scheme.
SkUiColorScheme.Current = new DarkSkUiColorScheme();

// Partial change without a new pack:
SkUiColorScheme.Current.Accent = Color.FromArgb("#FF5722");

// Or subclass / copy:
var scheme = new LightSkUiColorScheme { Accent = Colors.Purple };
```

Controls initialize (or resolve) defaults from the **active** scheme:

```csharp
// When a control is created
_onColor = scheme.Accent;
_fillColor = scheme.Accent;
_trackOff = scheme.TrackOff;
```

## Resolution

**For a given control color** (e.g. `SkUiButton.FillColor`):

1. A value **set** by the app (CLR setter, fluent `Set*`, XAML attribute, MAUI `Style` / visual state) wins.
2. Else the active scheme, `SkUiColorScheme.Current` (`LightSkUiColorScheme.Instance` by default).

Controls snapshot the scheme colors they keep in fields (accents, button and indicator tokens) when they are created, so an explicit value simply overwrites the snapshot; tokens read at paint time through `SkUiColors` follow `Current` live. Replacing `Current` raises `CurrentChanged`, and live surfaces re-measure and redraw their drawn trees; controls created earlier keep the colors they snapshotted. Per-control and per-tree schemes are not implemented ([roadmap](Roadmap.md#looks-colors-and-motion)).

## Relationship to FR-12 and FR-18

```
SkUiColorScheme  →  default Color values (accent, bg, …)
SkUiLook         →  how those colors are painted + default W×H
MAUI Style/VSM   →  optional per-control property overrides (SkUi* only)
```

- Core has **no** MAUI styles; Core depends on **scheme + look** (+ explicit `Set*`) for appearance.
- MAUI-compatible controls use scheme/look for defaults, then FR-12 for XAML-friendly overrides.

## Built-in schemes

| Pack | Intent |
| --- | --- |
| `LightSkUiColorScheme` | The default |
| `DarkSkUiColorScheme` | Dark surfaces, adjusted accent / muted / disabled |
| Platform packs | `IosSkUiColorScheme`, `MacSkUiColorScheme`, `AndroidSkUiColorScheme`, `AndroidMaterial3SkUiColorScheme`, `WindowsSkUiColorScheme` (`DefaultSkUiLook.GetColorScheme(isDark)`) |
| App schemes | Brand palettes: subclass or copy a scheme |

**Control tokens.** `ButtonBackground`, `ButtonForeground`, `ButtonBorder` and `ActivityIndicator` give buttons (both layers) and activity indicators the colors MAUI's native controls have on each platform, measured with `SkUiLookComparisonPage`: iOS's (and Mac Catalyst's) system button has no fill and tinted text; Android's buttons fill with the accent and its spinner takes it; Windows' standard button is light (`#B3FFFFFF`, dark `#0FFFFFFF`) with the primary text color and a faint 1 epx border (`#0F000000`, dark `#18FFFFFF`; the look's `DefaultButtonBorderWidth` and `ButtonHasElevationBorder` draw it, darker along the bottom), and its progress ring takes the accent. Until set, a token follows its fallback, so `WithAccent` and later `Accent` changes carry into it; once set, it keeps its value. `CopyFrom` copies the colors they have. Like the other defaults, controls read them when they are created; explicit values win.

## Core

Core and MAUI-compatible controls **must** resolve the same scheme so dual-layer demos stay color-aligned. Scheme types live where both layers can reference them (with look).

## Non-goals

- Replacing MAUI `Style` for per-control customization (FR-12 remains).
- Implementing full Material / Cupertino token graphs.
- Automatically mirroring the OS light / dark mode: the app sets the scheme (see [ControlLook.md](ControlLook.md#built-in-looks)).
