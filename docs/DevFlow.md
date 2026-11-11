# Driving SkiaUi apps with DevFlow

[.NET MAUI DevFlow](https://github.com/dotnet/maui-labs) lets tools and AI agents inspect and drive a running MAUI app through an in-app agent. SkiaUi draws a page, or part of it, on one surface, and its drawn elements (`SkUi*` views and Core nodes) have no platform views. So DevFlow's own tree and taps don't work for them:

- **`SkUi*` views:** DevFlow lists them, but with frames relative to their parent, not to the window.
- **Core nodes:** listed (they are visual tree elements), but with no bounds or text.
- **Taps:** `ui tap` fails for both, since neither has a platform view to tap.

SkiaUi provides a DevFlow agent extension, `dev.skiaui`, built on [`SkUiDiagnostics`](controls/SkUiCore.md). It gives a tree of drawn elements with window bounds, taps delivered through the surface like a real touch, and hit-testing. The extension is one source file that the app copies: [SkiaUiDevFlowExtension.cs](../plugins/skiaui-devflow/skills/skiaui-devflow/assets/SkiaUiDevFlowExtension.cs). The DevFlow agent is a preview package with a changing API, so SkiaUi doesn't ship the extension as a NuGet package. The app owns its copy and its DevFlow version. The demo app compiles the same file, so the copy in each SkiaUi release builds against that release.

For AI coding agents, the **[skiaui-devflow skill](../plugins/skiaui-devflow/README.md)** does the setup below and knows how to read the tools' results (Claude Code: `/plugin marketplace add rafalka/MauiSkiaUi`, then `/plugin install skiaui-devflow@skiaui`).

## Setup

Requires SkiaUi.Maui 1.10.2 or later, and DevFlow already set up in the app (`Microsoft.Maui.DevFlow.Agent` referenced and `builder.AddMauiDevFlowAgent()` called, e.g. by `maui devflow init`).

1. Copy `SkiaUiDevFlowExtension.cs` into the app project, e.g. as `Diagnostics/SkiaUiDevFlowExtension.cs`. You can change its namespace (`SkiaUiDevFlow`); leave the rest as it is, and copy the file again when you upgrade SkiaUi.
2. The file is wrapped in `#if MAUI_DEVFLOW`. Define that symbol in the configuration that references the agent (Debug; DevFlow opens a port into the app, so never Release):
   ```xml
   <PropertyGroup Condition="'$(Configuration)' == 'Debug'">
     <DefineConstants>$(DefineConstants);MAUI_DEVFLOW</DefineConstants>
   </PropertyGroup>
   <ItemGroup Condition="'$(Configuration)' == 'Debug'">
     <PackageReference Include="Microsoft.Maui.DevFlow.Agent" Version="…" />
   </ItemGroup>
   ```
   Or change the file's `#if` to the symbol the app already guards DevFlow with.
3. Register it where the agent is added, in `MauiProgram.cs`:
   ```csharp
   #if MAUI_DEVFLOW
   builder.AddMauiDevFlowAgent(SkiaUiDevFlowExtension.Register);
   #endif
   ```
   With an options callback of your own: `builder.AddMauiDevFlowAgent(options => { …; SkiaUiDevFlowExtension.Register(options); })`.
4. Run the app in Debug and check it on a page with drawn controls: `maui devflow extensions call dev.skiaui tree`.

## Tools

```bash
maui devflow extensions call dev.skiaui tree                                    # drawn elements: type, automationId, text, window bounds
maui devflow extensions call dev.skiaui tap '{"automationId":"AddObservation"}' # tap a drawn element at its center
maui devflow extensions call dev.skiaui tap '{"type":"SkUiCoreLabel","text":"Cell 1"}'
maui devflow extensions call dev.skiaui tapAt '{"x":201,"y":709}'               # tap the drawn content at a window point
maui devflow extensions call dev.skiaui hit '{"x":201,"y":709}'                 # deepest drawn element at a window point
```

| Tool | Parameters | Returns |
| --- | --- | --- |
| `tree` | none | Drawn elements of the page on screen: `type`, `automationId`, `text`, `bounds` (window DIPs), `parent` |
| `tap` | `automationId`, `text` (exact `Text`), `type` (class name): filters, combined; `index` (the nth match, default 0); `force` | The matched element (`tapped`), the element actually hit (`hit`; it differs when something covers the center) and the tap result |
| `tapAt` | `x`, `y` (window DIPs); `force` | `hit` and the tap result |
| `hit` | `x`, `y` (window DIPs) | The deepest drawn element at the point |

- **Coordinates:** window DIPs, the same as native elements in DevFlow's `ui tree`.
- **Scope:** every tool looks only at the page on screen (the top modal page, else the Shell's or navigation page's current page). Pages lower in the navigation stack, or in other Shell sections, keep their surfaces but are ignored.
- **Errors:** **404** when nothing matches. **422** when the element is clipped away or detached ("not on screen"). **409** always means DevFlow's mutation lease.
- **Covered elements:** a press that another element would take (drawn over the target, or at its center while the target is scrolled out of its scroller's viewport) is cancelled, even with `force`, and `coveredBy` names that element: scroll the target into view or tap the other element on purpose. Passive content drawn over the target (a label laid over a tappable border, an input-transparent overlay) lets the tap through, as for a finger.
- **Lease:** taps are mutations, so they wait for DevFlow's mutation lease. A lease taken by an earlier CLI call (e.g. `ui navigate`) expires after about 10 s.

## Tap results

A tap is a press delivered through the surface root (hit-testing, the gesture arena, press states, `Clicked` and commands), the same as a finger. The press is **released only when a drawn tap recognizer would take it**: a button, a toggle, a tap gesture, a slider or stepper tap, a text field, also one around a scroller, a pan or a swipe view (they give up a release that did not move). Otherwise it's cancelled, so a missed tap leaves no fling, no pressed state and no action. The result comes from `SkUiDiagnostics.SimulateTap(element, releaseOnlyIfTap)` / `SimulateTapAt` (`SkUiSimulatedTap`):

| Field | Meaning |
| --- | --- |
| `taken` | A drawn tap recognizer took the tap, and its action ran. Report an automated tap as done only when this is `true` |
| `winner`, `winnerOwner` | The recognizer that won the press (when it was cancelled, the one leading it: a scroller around a label) and its element; `null` when nothing drawn took part (a disabled control, plain text, a native view) |
| `released` | The press was released (`false`: cancelled) |
| `coveredBy` | `tap` only: the element that would have taken the press instead of the target (it was cancelled); `null` otherwise |
| `passedToNative` | On a device, a real touch would be left to native views around the surface: a MAUI `TapGestureRecognizer` on the surface's parent, or a native view hosted on the surface (`SkUiMauiContentView`, which is not pressed). On iOS and Mac Catalyst that is when no drawn tap is pending and no drawn gesture claimed the touch; on Android and Windows, only when the surface did not take the press at all (a scroller around a label or a disabled control take it without a tap). Use DevFlow's own tools (`ui tap`) for those |

`force: true` releases the press even when it isn't a tap, e.g. to check that a disabled control ignores taps.

## Making an app easy to drive

- Give drawn controls an `AutomationId` (`SkUi*` views: the property; Core nodes: `SetAutomationId`), so taps can find them without text or coordinates.
- When a tap reports `taken: false` on some text, the tappable element is usually around it (a row, a button). Find it in `tree` and tap that instead.
- UI-test frameworks that read the accessibility tree (Appium, Maestro) find drawn elements without this extension: see [Migration.md](Migration.md) (UI tests) and [Accessibility.md](design/Accessibility.md).
