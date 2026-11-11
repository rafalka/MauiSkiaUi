---
name: skiaui-devflow
description: Adds SkiaUi support to .NET MAUI DevFlow in an app that uses SkiaUi (SkiaUi.Maui, SkUi* controls), so DevFlow and AI agents can find, tap and hit-test drawn controls, which have no platform views and are invisible or untappable through DevFlow's own tree. Copies the dev.skiaui agent extension into the app, registers it with AddMauiDevFlowAgent, builds and checks it with the maui devflow CLI. Also explains how to drive drawn UI with it (tree, tap, tapAt, hit) and how to read a tap's result. Use when asked to make a SkiaUi app controllable by DevFlow, when DevFlow cannot see or tap SkUi* controls, or when automating or debugging a SkiaUi app with DevFlow.
argument-hint: "[app project path]"
---

# Drive SkiaUi's drawn controls with DevFlow

SkiaUi draws a whole page (or a part of it) on one surface: `SkUi*` views and Core nodes have no platform views. DevFlow's own tree therefore reports them with parent-relative frames, has no bounds for Core nodes, and its tap reaches only the surface. The `dev.skiaui` extension in this skill fixes that through `SkUiDiagnostics`: a tree of drawn elements with window bounds, taps delivered through the surface like a real touch, and hit-testing.

The extension is one source file, [assets/SkiaUiDevFlowExtension.cs](assets/SkiaUiDevFlowExtension.cs). The app owns the copy and the DevFlow version (the DevFlow agent is a preview package, so SkiaUi does not ship it in a NuGet package). The SkiaUi demo app compiles the same file, so it builds against the SkiaUi release it came with.

## Rules

1. **Debug only.** DevFlow opens a network port into the app. The agent package reference, the compile symbol and the registration stay limited to Debug (or whatever configuration the app already uses for DevFlow), never Release.
2. **Don't fork the logic.** Copy the file as it is. Change only its namespace (optional) and, if the app guards DevFlow with another symbol, the `#if` line. Re-copy it when SkiaUi is upgraded.
3. **Needs SkiaUi.Maui 1.10.2 or later** (`SkUiDiagnostics.SimulateTapAt`, `SkUiSimulatedTap`). On an older SkiaUi, upgrade it; don't rewrite the taps.

## Add it to the app

1. **Find the app project and check its references.** It references `SkiaUi.Maui` (or a project reference to `MauiSkiaUi`) and, for DevFlow, `Microsoft.Maui.DevFlow.Agent` with `builder.AddMauiDevFlowAgent(...)` in `MauiProgram.cs`. If DevFlow isn't set up yet, set it up first (`maui devflow init`, or DevFlow's onboarding skill), then come back.
2. **Copy the file** `${CLAUDE_SKILL_DIR}/assets/SkiaUiDevFlowExtension.cs` into the app project, e.g. `Diagnostics/SkiaUiDevFlowExtension.cs`. SDK-style MAUI projects compile it without a csproj change. Optionally change `namespace SkiaUiDevFlow;` to the app's namespace.
3. **Make the compile symbol match.** The file is wrapped in `#if MAUI_DEVFLOW`. Check how the app guards its DevFlow code:
   - If it already defines `MAUI_DEVFLOW`, there's nothing to do.
   - Otherwise, define the symbol in the configuration that references the agent, next to that reference:
     ```xml
     <PropertyGroup Condition="'$(Configuration)' == 'Debug'">
       <DefineConstants>$(DefineConstants);MAUI_DEVFLOW</DefineConstants>
     </PropertyGroup>
     ```
   - Or, if the app guards DevFlow with `#if DEBUG` and the agent is referenced in every Debug build, change the file's `#if` line to the same guard.
4. **Register the extension** where the agent is added:
   ```csharp
   #if MAUI_DEVFLOW
   builder.AddMauiDevFlowAgent(SkiaUiDevFlowExtension.Register);
   #endif
   ```
   If the app already passes its own options callback, call it from there: `builder.AddMauiDevFlowAgent(options => { /* app's own setup */ SkiaUiDevFlowExtension.Register(options); });`. Add `using SkiaUiDevFlow;` (or the namespace you chose).
5. **Build** the app for one platform in Debug, e.g. `dotnet build <app>.csproj -f net10.0-android`. Fix compile errors only in the app's wiring, not in the file (a `SimulateTapAt` / `SkUiSimulatedTap` error means SkiaUi is older than 1.10.2).
6. **Check it on a running app** (DevFlow's usual launch and connect flow), on a page with drawn controls:
   ```bash
   maui devflow extensions call dev.skiaui tree
   maui devflow extensions call dev.skiaui tap '{"automationId":"<an id from tree>"}'
   ```
   The tree lists `SkUi*` / `SkUiCore*` types with window bounds, and the tap response has `"taken": true` for a button or toggle.

## Tools

| Tool | Parameters | Returns |
| --- | --- | --- |
| `tree` | none | Drawn elements of the page on screen (top modal page, else the Shell's or navigation page's current page): `type`, `automationId`, `text`, `bounds` (window DIPs), `parent` |
| `tap` | `automationId`, `text`, `type` (all optional filters, combined), `index` (nth match, default 0), `force` | Taps the match's center; returns `tapped`, `hit` and the tap result below. 404 when nothing matches, 422 when the element is clipped or detached |
| `tapAt` | `x`, `y` (window DIPs), `force` | The same at a point (for elements without an id or text) |
| `hit` | `x`, `y` (window DIPs) | The deepest drawn element at the point |

## Reading a tap's result

A tap is pressed through the surface, as a real touch. It is **released only when a drawn tap recognizer takes it** (a button, a toggle, a tap gesture, a slider or stepper tap, a text field). Otherwise it's cancelled, so a missed tap leaves no fling, no pressed state and no action.

- `taken: true`: a drawn tap recognizer took the tap and its action ran (`Clicked`, the command, the toggle). `winner` / `winnerOwner` name the recognizer and its element.
- `taken: false`: nothing drawn completes a tap there, e.g. a label inside a scroller, a disabled control, or plain text. `winner` is whatever led the press (a scroller's pan) or null. Look for the real target in `tree` (the row or button around the text) and tap that instead.
- `passedToNative: true`: no drawn recognizer took the touch, so on a device a real touch would reach native views around the surface (a MAUI `TapGestureRecognizer` on the surface's parent, a native control behind it). Drive those with DevFlow's own tools, not this extension.
- `force: true` releases the press even when it isn't a tap (e.g. to check that a disabled control ignores taps). Don't use it to make a failed tap look successful.

Report a tap to the user as done only when `taken` is true.

## Making apps easy to drive

- Give drawn controls an `AutomationId` (`SkUi*`: the property; Core nodes: `SetAutomationId`), so `tap` can match them without coordinates.
- `text` matches a control's `Text` property exactly. Combine it with `type` when several elements share a text.
- Elements on pages under the visible one aren't listed. Navigate first, then call `tree` again.
