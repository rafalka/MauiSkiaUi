# Windows validation — instructions for the Claude Code agent

> Temporary file. Delete it (and mention that in the commit) once Windows validation is complete.

You are running on the user's **Windows** machine. The SkiaUi repository was developed on macOS and validated on Android (Galaxy S9) and iOS (simulator + iPhone). **The Windows target has never been compiled.**
- Several features added recently contain Windows-only code paths that nobody has built or run.
- Your job: build, run, check, fix what is broken, and report.

## 1. Context

- **What the repo is:** SkiaUi, a .NET MAUI UI toolkit that draws controls with SkiaSharp. It has two layers:
  - `SkUi*`: MAUI-compatible views, bindable, XAML.
  - `Core`: `SkUiCore*`, lightweight nodes for complex controls, hosted via `SkUiCoreHost`.
- **Projects:**
  - `MauiSkiaUi/`: the library.
  - `MauiSkiaUiDemo/`: the gallery app.
  - `tests/MauiSkiaUi.Tests/`: headless xUnit tests on `net10.0`.
  - `benchmarks/`: headless runner + a MAUI bench app (Android / iOS / Catalyst only).
- **Branch:** `claude_review`. Work and commit there.
- **Read first:** `Development.md`, then as needed:

  | Doc | Topic |
  | --- | --- |
  | `docs/design/RenderingPipeline.md` | Surfaces, render thread, compositor |
  | `docs/design/EventMechanism.md` | Gesture arena, native coordination |
  | `docs/design/ScrollingAndCollectionViews.md` | Scroll engine, nesting, overlays while scrolling |
  | `docs/design/Testing.md` | DevFlow usage and the `dev.skiaui` extension |
  | `docs/design/Benchmarks.md` | Benchmarks |

- **Windows rendering model** (by design): standalone roots use MAUI SkiaSharp views.
  - `HwAccelerated = true` → `SKGLView` (ANGLE); `false` → `SKCanvasView`.
  - Both record, commit **and composite on the UI thread**. There is no render thread on Windows, so "render-thread" flings and animations run on the UI thread there.
  - Touch comes from SkiaSharp's `Touch` event (WinUI pointer events, mouse included) → `SkUiViewHandler.OnMauiTouch` → the drawn gesture arena.

### Windows-only code that has never compiled or run

| File | Windows code |
| --- | --- |
| `MauiSkiaUi/SkUiViewHandler.cs` | `CreateMauiSurface(gpu:)`. `SkUiOverlayContainer` (WinUI `Canvas`): every native overlay is wrapped in a clip `Canvas` sized to its visible rectangle, with `Clip = RectangleGeometry` (clip also limits hit-testing), and hidden with `Visibility.Collapsed` while a snapshot is shown |
| `MauiSkiaUi/Controls/Layouts/SkUiMauiContentView.Platform.cs` | `CaptureWindows`: `RenderTargetBitmap.RenderAsync` → `GetPixelsAsync` → `System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.ToArray` → `SKImage.FromPixelCopy(Bgra8888, Premul)`. **Likely compile risk** (namespace / extension availability) |
| `MauiSkiaUi/Controls/Layouts/SkUiMauiContentView.cs` | `ScrollMode = Auto` resolves to **Snapshot** on Windows (`OperatingSystem.IsWindows()`) |
| `MauiSkiaUi/SkUiDiagnostics.cs` | `GetWindowOrigin`: `TransformToVisual(null).TransformPoint(0,0)` (window bounds / hit tests used by the `dev.skiaui` DevFlow extension) |
| `MauiSkiaUi/Helpers/SkUiUiTicker.cs` | Dispatcher-timer vsync substitute for UI-clock animations |

**Known gap, by design so far:** unlike Android and iOS, **Windows has no native-parent gesture coordination.**
- Android and iOS hold a native `ScrollView` back while a drawn gesture may claim the touch.
- On Windows, a drawn scroller inside a WinUI `ScrollViewer` may lose touch drags to DirectManipulation.
- With a **mouse**, WinUI `ScrollViewer` only scrolls on the wheel, so conflicts are mostly a **touchscreen** matter. Report what you observe; don't build a fix without discussing it (see §6).

## 2. Setup

1. **.NET SDK 10.0.400** (see `global.json`; `rollForward: latestPatch`): `dotnet --info`.
2. **MAUI workload:**
   - Install: `dotnet workload install maui`, or verify with `dotnet workload list`.
   - The Windows App SDK comes with it. Visual Studio is not required.
3. **Repo:**
   - `git fetch` and `git checkout claude_review`. The branch must have been pushed from the Mac; if it's missing, stop and tell the user.
   - `git log --oneline -5`: expect this file's commit near the top.
4. **DevFlow CLI**, for UI automation:
   - Install: `dotnet tool install -g microsoft.maui.cli --prerelease` (the Mac uses `0.1.0-preview.12.26421.1`; match that if possible).
   - The Debug demo includes the DevFlow agent (`MAUI_DEVFLOW`), listening on port 9223.
5. **Shell:** use PowerShell. `scripts/*.sh` need Git Bash. Direct `dotnet` commands are given below.

## 3. Build and headless tests

```powershell
# Library for Windows (plus the other TFMs this host can build)
dotnet build MauiSkiaUi/MauiSkiaUi.csproj -c Debug -f net10.0-windows10.0.19041.0
# Whole solution (use -m:1 if you see "file is being used by another process" on deps.json)
dotnet build SkiaUi.slnx -c Debug -m:1
# Headless tests (net10.0) — expect 241 passing
dotnet test tests/MauiSkiaUi.Tests/MauiSkiaUi.Tests.csproj
# Headless benchmark sanity run (no Bash needed)
dotnet run -c Release --project benchmarks/MauiSkiaUi.Benchmarks -- --runs 3
```

- **Compile errors:** fix them in the Windows-only code paths first, and keep the fixes minimal and idiomatic (see §7).
- **Test failures:** if a test that passes on macOS fails on Windows (e.g. font or pixel differences), analyze it before changing anything. Is it a Windows-specific rendering difference (fonts, antialiasing) or a real bug? Don't loosen assertions without explaining why in the commit.

## 4. Run the demo

```powershell
dotnet build MauiSkiaUiDemo/MauiSkiaUiDemo.csproj -c Debug -f net10.0-windows10.0.19041.0 -t:Run
```

(Unpackaged app: `WindowsPackageType=None`.)

### Automation

- **Agents:** `maui devflow list`. Add `-ap 9223 -ah localhost` if discovery fails.
- **Navigation:** `maui devflow ui navigate <route>`. Routes:
  - Shell: `//components`, `//core`, `//composition`, `//nesting`, `//look`, `//stress`, `//primitives`.
  - Demo pages: `demo-<Name>`, e.g. `demo-SkUiButton`, `demo-SkUiScrollView`, `demo-OverlaysInScrollView`, `demo-SkUiMauiContentView`, `demo-SkUiCoreScrollView`, `demo-SkUiCoreGrid`.
- **Drawn elements:** use the demo's DevFlow extension, not `ui tree` (which has no bounds for drawn nodes):
  ```powershell
  maui devflow extensions call dev.skiaui tree                                    # type, automationId, text, window bounds (DIPs)
  maui devflow extensions call dev.skiaui tap '{"automationId":"AddObservation"}' # press+release through the drawn tree
  maui devflow extensions call dev.skiaui hit '{"x":120,"y":300}'
  ```
- **Native elements:** `maui devflow ui tree --format compact`, `ui tap <AutomationId>`, `ui set-property <AutomationId> <Property> <Value>`.
  - Demo editors use AutomationId `Edit<Name>`, e.g. `EditScrollMode`, `EditHwAccelerated`.
  - Action buttons use their title without spaces, e.g. `ScrolltoWebView`.
- **Screenshots:** `maui devflow ui screenshot` (see `--help` for the output path). Look at them; don't infer visuals from code.
- **The mutation lease:** mutating DevFlow calls (tap, set-property, navigate) take a lease of about 10 s. A second CLI call right after may fail with "Another DevFlow session is driving this app". Wait about 12 s and retry.
- **Real pointer input:** drags and wheel have no DevFlow equivalent for drawn content. Use Win32 `SendInput` from PowerShell; it needs no special permission in the same desktop session.
  - `dev.skiaui tree` bounds are in window-content DIPs.
  - Convert them with the window's DPI scale (`GetDpiForWindow(hwnd)/96`) and the client origin (`ClientToScreen`).
  - First **calibrate**: click the center of a known native button, e.g. the toolbar's "Stall 2s", or a drawn button reported by `dev.skiaui tree`. Confirm it reacted before trusting the mapping; the MAUI Windows title bar may shift content.

  ```powershell
  Add-Type @"
  using System; using System.Runtime.InteropServices;
  public static class Mouse {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, int data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hwnd, ref POINT p);
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    public const uint DOWN = 0x2, UP = 0x4, WHEEL = 0x800;
  }
  "@
  $hwnd = (Get-Process MauiSkiaUiDemo).MainWindowHandle
  $scale = [Mouse]::GetDpiForWindow($hwnd) / 96.0
  $origin = New-Object Mouse+POINT; [Mouse]::ClientToScreen($hwnd, [ref]$origin) | Out-Null
  function To-Screen([double]$dipX, [double]$dipY) { @([int]($origin.X + $dipX * $scale), [int]($origin.Y + $dipY * $scale)) }
  function Drag([double]$x1, [double]$y1, [double]$x2, [double]$y2, [int]$steps = 20, [int]$ms = 15) {
    $a = To-Screen $x1 $y1; [Mouse]::SetCursorPos($a[0], $a[1]); [Mouse]::mouse_event([Mouse]::DOWN, 0, 0, 0, [UIntPtr]::Zero)
    for ($i = 1; $i -le $steps; $i++) { $p = To-Screen ($x1 + ($x2 - $x1) * $i / $steps) ($y1 + ($y2 - $y1) * $i / $steps); [Mouse]::SetCursorPos($p[0], $p[1]); Start-Sleep -Milliseconds $ms }
    [Mouse]::mouse_event([Mouse]::UP, 0, 0, 0, [UIntPtr]::Zero)
  }
  function Wheel([double]$x, [double]$y, [int]$delta = -120) { $p = To-Screen $x $y; [Mouse]::SetCursorPos($p[0], $p[1]); [Mouse]::mouse_event([Mouse]::WHEEL, 0, 0, $delta, [UIntPtr]::Zero) }
  function Click([double]$x, [double]$y) { $p = To-Screen $x $y; [Mouse]::SetCursorPos($p[0], $p[1]); [Mouse]::mouse_event([Mouse]::DOWN, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 60; [Mouse]::mouse_event([Mouse]::UP, 0, 0, 0, [UIntPtr]::Zero) }
  ```

  A quick drag (few steps, short sleeps) produces a fling; a slow drag doesn't. If the user has a touchscreen, you can also ask them to try the touch cases by hand.

## 5. What to check

Record for every item: **OK / broken (what you saw) / not testable**.
- **Evidence:** attach screenshots where visual.
- **Surface types:** check both GPU (`HwAccelerated` on, the default) and software (toggle `EditHwAccelerated` on demo pages; the "Native nesting" page has a *Software surfaces* switch).

**A. Build and basics**
1. Library, demo and tests build; tests pass (§3).
2. The app starts; Components, Core, Composition, Look & colors, Stress and Primitives pages open without exceptions (watch the console / `maui devflow logs`).

**B. Rendering and input on demo pages**

3. **`demo-SkUiButton`:** press feedback (the button darkens while the mouse is down), click count / `Clicked`. Also with `EditHwAccelerated` off.
4. **`//composition`:**
   - drag the drawn scroller (mouse drag), and use the mouse wheel on it;
   - "Add observation" increments "Observations: N";
   - "Reset observations" (a tappable label) resets it.
5. **`demo-SkUiScrollView`:**
   - drag, fling (quick drag), wheel;
   - taps on rows while not dragging;
   - a drag that starts on a row button scrolls and does **not** click it.
6. **`demo-SkUiCoreScrollView`** (Core "ScrollView + gestures"):
   - horizontal carousel (horizontal drag), same-direction nested panel (drag inside it past its end; the rest of the drag moves the page);
   - row swipe left / right, long press (hold ~0.5 s), double tap;
   - the feedback line reports each gesture.
   - Pinch needs touch; mark it not testable with a mouse.
7. **RTL:** on any demo page, set FlowDirection to RightToLeft (the `EditFlowDirection` picker). Layout mirrors, and a horizontal scroller starts at the right end.

**C. Native overlays** (Windows-specific code)

8. **`demo-SkUiMauiContentView`:** the Editor and WebView render and are editable; typing in the Editor updates the WebView.
9. **`demo-OverlaysInScrollView`**, "Native overlays in ScrollView"; `ScrollMode = Auto` means **Snapshot** on Windows:
   - **At rest:** Entries, Editor, WebView and the nested-carousel Entry are placed correctly and **clipped**. A partly scrolled-out overlay must not cover the teal drawn header or footer, and must not take clicks outside the viewport (click the header over a clipped Entry: nothing should focus).
   - **While dragging** (slow mouse drag): overlays show as **snapshots** with a red outline (`HighlightSnapshots` is on). They move with the drawn content, and the status line says `frozen N/7`.
   - **After release:** native views return about 150 ms later, at the right positions, with `frozen 0/7`.
   - **WebView snapshot:** it shows the page content, not a blank or black rectangle. `RenderTargetBitmap` can't capture some WebView2 content; if it's blank, report that. A fallback might be needed.
   - **Focus:** focus an Entry and type; while it is focused, scrolling must keep it live (no red outline).
   - **Stall:** fling, then click the toolbar's "Stall 2s". Frozen overlays stay aligned with the list; in Live mode, compare.
   - **Live mode:** set `EditScrollMode` to Live (`maui devflow ui set-property EditScrollMode SelectedIndex 2`). Overlays follow while scrolling (they may lag); no snapshots.
   - **Both surface types:** repeat with `EditHwAccelerated` off.
10. **Diagnostics extension:** `dev.skiaui tree` bounds match the screenshots (position and size of drawn elements), and `dev.skiaui tap` clicks a drawn button (verify with its feedback).

**D. Native nesting** (`//nesting`: drawn surfaces inside a native MAUI `ScrollView`)

11. **With the mouse:**
    - drag inside the drawn list and the Core list: the lists scroll, the page doesn't;
    - wheel over them: the list scrolls, and at its end the page should scroll on further wheel input (report what happens);
    - wheel over native filler: the page scrolls;
    - carousel horizontal drag; swipe row swipe and click.
12. **Touch:** if a touchscreen is available, ask the user to repeat item 11 with a finger. This is where the known gap (§1) would show up.

**E. Performance sanity**

13. **`//stress`:** run the Core and SkUi* stress cases a few times and record the numbers from the page. For reference, S9 Release numbers are in `CHANGELOG.md`; Windows numbers are new data, not a pass / fail.
14. **Headless benchmark:** keep the output of the run from §3.

## 6. What to fix, and what not

- **Fix:**
  - compile errors;
  - crashes;
  - clearly wrong Windows behavior in the Windows-only paths above: overlay positions and clipping, snapshot capture, window origin, surface creation;
  - obvious regressions against the documented behavior.
- **Don't** redesign. In particular, **don't implement Windows native-parent gesture coordination** (DirectManipulation / `ManipulationMode`) without asking the user. Describe the observed problem and a proposed approach instead.
- **Out of scope:** Android, iOS and Apple-specific code. It is verified on devices you don't have, so leave it alone unless a shared-code fix is unavoidable. If one is, keep the change minimal and note it for re-verification on the other platforms.
- **Every fix:** rebuild, rerun the headless tests, and re-check the affected item.

## 7. Conventions

- **Code:** match the surrounding style (file-scoped namespaces, `var`, expression bodies where the file uses them, XML docs on public members, comments only where they explain *why*).
- **Build hygiene:** no new warnings.
- **Commits:** small commits on `claude_review`, message = what + why, ending with the attribution line the harness gives you (the Mac sessions used `Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>`). Don't push unless the user asks.
- **Docs:** when behavior is verified or changed, update the relevant design doc: `EventMechanism.md` "Verification", `ScrollingAndCollectionViews.md` "Implemented", the `Requirements.md` checkboxes, and `CHANGELOG.md` under "Unreleased".

## 8. Report

Write `docs/design/WindowsValidation-results.md` with:
- the environment (Windows version, GPU, display scale, SDK / workload versions);
- the table of items A–E (OK / broken / not testable + notes);
- the fixes made (commit ids);
- open problems with proposed next steps.

Summarize it for the user in chat at the end. Keep this instruction file until the user confirms Windows validation is complete.
