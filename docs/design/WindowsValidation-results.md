# Windows validation — results

First build and run of SkiaUi on Windows, following the Windows validation instructions (`WINDOWS_VALIDATION.md`, added in f9a0503 and removed after validation). Date: 2026-09-27. Branch `claude_review`.

## Environment

| | |
| --- | --- |
| OS | Windows 11 Pro 10.0.26200 |
| GPU | NVIDIA GeForce RTX 4080 (32.0.15.9186) + Intel UHD Graphics 770 (31.0.101.3616) |
| Displays | 2560×1440 at 150 % (primary, used for the tests), 1920×1080 at 100 % |
| .NET SDK | 10.0.401 (`global.json` 10.0.400, `latestPatch`) |
| Workload | `maui-windows` 10.0.20/10.0.100; MAUI packages 10.0.101 |
| SkiaSharp | 4.152.1 (Views.WinUI: `SKGLView` = ANGLE `SKSwapChainPanel`, `SKCanvasView` = `SKXamlCanvas`) |
| DevFlow CLI | 0.1.0-preview.12.26421.1 (same as the Mac) |
| Input | Mouse only (Win32 `mouse_event`); no touchscreen |

The demo ran as a Debug build (DevFlow agent included). Most checks were done in a 900×650 or 1000×800 DIP window, so that the drawn scrollers have content to scroll.

## Results

**OK** = works as documented; **Fixed** = was broken, fixed on this branch (commit); **Not tested** = see the note.

### A. Build and basics

| # | Item | Result | Notes |
| --- | --- | --- | --- |
| 1 | Library, demo, tests build; tests pass | **Fixed** → OK | Demo did not build: `ApplicationDisplayVersion` `1.0.0-Prerelease03` is not numeric (aa20a13). `ArabicShapesWithJoiningAndLamAlefLigature` failed: the Windows fallback font (Segoe UI) forms lam-alef from two glyphs; assertion now checks the substitution (0cbcc70). Solution build: 0 errors, 1 existing warning (`SKTypeface.GetGlyph` obsolete in a test). **241/241** tests pass. Windows-only code (`CaptureWindows`, overlay container, window origin, ticker) compiled as written. |
| 2 | App starts; Shell pages open | **Fixed** → OK | All seven Shell pages open without exceptions. There were a hang and a crash, both fixed (items 3–4 below and the GPU notes). |

### B. Rendering and input on demo pages

| # | Item | Result | Notes |
| --- | --- | --- | --- |
| 3 | `demo-SkUiButton` | **Fixed** → OK | GPU surfaces were blank on Windows (3e8938c, 47200f7). Press state is visible while the mouse is down (the look lightens the button, it does not darken it). `Clicks` count is correct on GPU and software. |
| 4 | `//composition` | OK | Mouse drag follows exactly (100 DIP → 100 DIP), a quick drag flings to the end, wheel = 120 DIP/notch. "Add observation" counts; "Reset observations" resets. |
| 5 | `demo-SkUiScrollView` | OK | Drag, fling, wheel; taps on rows click; a drag that starts on a row button scrolls and does not click it. GPU and software. |
| 6 | `demo-SkUiCoreScrollView` | OK | Carousel horizontal drag (200 → 200); same-axis nested panel scrolls to its end (178), and the rest of the drag moves the page (122); row swipe left / right, long press, double tap all reported. GPU and software. Pinch: **not testable** (mouse). |
| 7 | RTL | **Fixed** → OK | Drawn surfaces showed mirror-image text (54efe9f). Now layout mirrors, text reads correctly, and the carousel starts at its right end. GPU and software. |

### C. Native overlays

| # | Item | Result | Notes |
| --- | --- | --- | --- |
| 8 | `demo-SkUiMauiContentView` | **Fixed** → OK | Overlays sat ~460 DIP to the right after the window resized (71070ec) and vanished after toggling `HwAccelerated` (2729f2e). Editor editable; typing updates the WebView live. The black WebView was WebView2 following Windows dark mode; the demo now wraps the snippet in a light document, and its preview area is taller and full width (1f20bf4). |
| 9 | `demo-OverlaysInScrollView` (Snapshot) | **Fixed** → OK | At rest: placed and clipped; a click on the header over a clipped Entry focuses nothing, a click on its visible part focuses it. Dragging: red-outlined snapshots move with the list, `frozen N/7`. Release: natives return, `frozen 0/7`. **WebView snapshot was blank** (`RenderTargetBitmap`); it now uses `CapturePreviewAsync` and shows the page (98c5f41). A focused Entry / WebView stays live. Stall 2 s after a fling: snapshots stay aligned; the fling itself also pauses (no render thread on Windows, by design) and continues afterwards. Live mode: overlays follow, no snapshots. GPU and software. |
| 10 | Diagnostics extension | OK | `dev.skiaui tree` bounds matched the screen for every element used to aim the mouse (also after RTL, which was off before 54efe9f). `tap` clicks a drawn button (`Clicks: 2`); on an element scrolled out of its viewport it correctly refuses (409). `hit` returns the right element. Minor issues below. |

### D. Native nesting

| # | Item | Result | Notes |
| --- | --- | --- | --- |
| 11 | Mouse | **Fixed** (rendering) → OK | GPU surfaces inside the native `ScrollView` were blank, then stretched after a resize (47200f7); now they draw correctly. Run 2026-09-28, GPU and software, same results:<br>• Drag in the drawn list (150) and the Core list (130): the list scrolls, the page doesn't.<br>• Wheel over a list: the list scrolls to its end (367 / 340), then further wheel input scrolls the page (0 → 283, 472 → 755).<br>• Wheel over native filler: the page scrolls.<br>• Carousel: horizontal drag scrolls it.<br>• Swipe row: swipe and tap reported.<br>• Mouse **drags** on native filler, or vertical drags on the carousel, don't scroll the page. That's expected with a mouse: WinUI `ScrollViewer` pans only for touch / pen.<br>• A **vertical wheel over the horizontal carousel scrolls the carousel** (0 → 240), not the page. See open problems. |
| 12 | Touch | **Not tested** | No touchscreen. This is where the known gap (no native-parent coordination on Windows) would show. |

### E. Performance (Debug build — the DevFlow agent is Debug-only)

| # | Item | Result |
| --- | --- | --- |
| 13 | `//stress`, 1,000 children, animate off | See the table below. New data, not pass / fail. S9 numbers in the CHANGELOG are Release. |
| 14 | Headless benchmark (`--runs 3`, Release) | See the table below. |

Stress page, "Overall (start → UI idle)", ms, three runs (two for HW off):

| Layer | HW on | HW off |
| --- | --- | --- |
| SkUi* | 40.6 / 34.4 / 23.9 (layout + first frame 16–27) | 25.5 / 36.4 |
| Core | 26.1 / 16.4 / 18.7 (layout + first frame 13–19) | 14.6 / 12.6 |
| Native MAUI | 1070.7 / 1119.8 / 1080.2 (add to page ~590, layout + first frame ~480) | — |

Headless benchmark, median of 3, ms:

| Scenario | generate | measure | arrange | record | composite | update | allocKB |
| --- | --- | --- | --- | --- | --- | --- | --- |
| core-labels | 1.6 | 2.2 | 2.4 | 2.4 | 0.6 | – | 2821 |
| skui-labels | 7.1 | 2.4 | 4.3 | 3.2 | 0.6 | – | 10358 |
| core-buttons | 4.0 | 3.5 | 4.0 | 6.4 | 1.3 | – | 3282 |
| skui-buttons | 8.6 | 2.4 | 4.1 | 4.9 | 0.9 | – | 11217 |
| mixed-script-labels | 4.2 | 8.9 | 2.2 | 1.4 | 0.5 | – | 7245 |
| core-labels-simple | 2.1 | 1.8 | 2.3 | 2.1 | 0.5 | – | 2842 |
| core-labels-update | 1.6 | 2.1 | 2.3 | 2.3 | 0.5 | 9.6 | 3977 |
| skui-labels-update | 7.5 | 2.5 | 4.0 | 3.1 | 0.6 | 11.8 | 11516 |

## Fixes

| Commit | Fix | Scope |
| --- | --- | --- |
| aa20a13 | Numeric `ApplicationDisplayVersion` (demo did not build for Windows) | Build (all platforms: display version is now `1.0.0`) |
| 0cbcc70 | Lam-alef test accepts Segoe UI's two-glyph ligature | Test |
| 3e8938c | `SkUiView.ArrangeOverride` re-applies the cached frame (`#if WINDOWS`); root containers stayed 0×0, GPU panels never painted | Windows |
| 47200f7 | Measure the root container before `Arrange`; repaint while the GL target does not match the panel after a resize | Windows |
| c8e650f | Continuous frames paced to `CompositionTarget.Rendering`; back-to-back repaints starved the UI thread (app "not responding", closed by Windows) | Windows |
| d714673 | Continuous frames of unloaded surfaces stop, and restart on `Loaded`; left-behind pages kept painting at 120 fps. The load-time repaint also fixed Look-page previews that never showed | Windows |
| bcfcb86 | Keep `opengl32.dll` loaded: Skia's GL interface calls into an unloaded opengl32 (native fail-fast, ~1 in 4 navigations to a GPU page). Found from a full dump with cdb | Windows |
| 54efe9f | Root container pinned to `LeftToRight`; WinUI mirrored the surface pixels on top of SkUi's RTL layout | Windows |
| 71070ec | Overlays re-sync when an ancestor moves without resizing | **Shared** — re-verify on Android / iOS |
| 2729f2e | Overlays re-attach when their content moves to another root | **Shared** — re-verify on Android / iOS |
| 98c5f41 | WebView2 snapshots via `CoreWebView2.CapturePreviewAsync` | Windows |

## Open problems and proposed next steps

1. **D.12 touch** needs a touchscreen. Also still unexplained: in one early run the native "Native nesting" page sat at offset 829 without any input. It wasn't seen again after the fixes.
   - **Vertical wheel over a horizontal-only drawn scroller** scrolls it horizontally, so a page that's being wheel-scrolled stops as soon as a carousel passes under the cursor. That's convenient for mice without horizontal wheels, but it traps page scrolling. Proposal: a vertical wheel only moves a scroller that can move vertically; horizontal needs Shift+wheel or a horizontal wheel / trackpad (part of the "axis-aware wheel" item in EventMechanism.md). This is shared code, so decide before changing it.
2. **Native-parent coordination on Windows** (known gap, not built): with a mouse it only matters for the wheel. Proposed approach once touch results exist: while a drawn gesture may claim the pointer, set `ManipulationMode = None` on the surface (keeps DirectManipulation from starting a pan on the ancestor `ScrollViewer`), and use `ScrollViewer.CancelDirectManipulations` if it already started. That mirrors Android's `RequestDisallowInterceptTouchEvent`.
3. ~~**WebView black background**~~ **Resolved** (1f20bf4): not SkiaUi. `DefaultBackgroundColor` was white; WebView2 follows the Windows dark theme and renders an unstyled page dark. The demo now sets a light document around the edited snippet.
4. ~~**Drag that starts on a native TextBox**~~ **Resolved** for touch / pen (verified on a touchscreen, 2026-09-28). Original report: the drag selected text instead of scrolling the drawn list (WinUI TextBox captures the pointer).
   - **Mobile:** it didn't scroll there either, until drags from overlays were handed to drawn scrollers on Android and iOS / Catalyst (`DispatchFromOverlay`; see EventMechanism.md "Drags that start on a native overlay").
   - **Windows:** implemented for touch and pen (`OverlayDragWatcher` in `SkUiOverlayContainer`): `handledEventsToo` pointer handlers on the overlay's clip canvas feed `DispatchFromOverlay`, and the clip captures the pointer once the drawn tree claims (the TextBox then loses capture). Mouse drags stay native, so they still select text.
5. ~~**`dev.skiaui` extension**~~ **Resolved**: `tree` / `tap` / `hit` are limited to the page on screen, and "not on screen" returns 422. Original report:
   - `tree` / `tap` include elements of pages that aren't visible (navigation stack, other Shell tabs). `tap` then picks the first match, which may be the hidden one. Filter to loaded pages, or return the visible match first.
   - "Not on screen" returns 409, which the CLI shows only as "409 (Conflict)", the same code as the DevFlow lease. A different code (e.g. 422) would avoid confusing the two.
6. **`SKSwapChainPanel` workarounds** (opengl32 pin, stale-size repaint) depend on SkiaSharp 4.152 / ANGLE behavior. Worth reporting upstream; re-check them when SkiaSharp is updated. During a live window-resize drag, one stretched frame may still show.
7. **Stress numbers are Debug.** For Release numbers, drive the stress page without DevFlow (e.g. keyboard / mouse input, or an auto-run switch).
8. ~~`demo-SkUiMauiContentView` reports `Bounds 320 x 156`~~ **Resolved** (1f20bf4): the demo's preview area was 206 DIPs high; it is now 520, and the preview starts at the full page width.

## Notes for the next Windows session

- **Mouse moves:** use `mouse_event(MOVE | ABSOLUTE | VIRTUALDESK)`. `SetCursorPos` alone produces no WinUI `PointerMoved`, so drags arrive as press + release only. The input snippet in the removed instructions (`WINDOWS_VALIDATION.md` §4, see f9a0503) needs this change. Make PowerShell per-monitor DPI aware (`SetThreadDpiAwarenessContext(-4)`) before converting coordinates.
- **Screenshots:** `maui devflow ui screenshot` uses `RenderTargetBitmap`, which shows GPU surfaces blank. Use a screen capture or `PrintWindow(PW_RENDERFULLCONTENT)`.
- **Tooling quirks:**
  - The DevFlow agent may listen on 10223 (broker-assigned), not 9223.
  - Git Bash rewrites `//route` to `/route`; set `MSYS_NO_PATHCONV=1`.
  - Windows PowerShell 5.1 mangles JSON arguments to native commands; call the `dev.skiaui` tools from Git Bash.
- **Stale page instances:** navigating to a `demo-*` route again pushes a second page instance, and `set-property` by AutomationId may then hit the hidden one. Relaunch per page.
- **Crash dumps:** with the JIT-debugger dialog up, the crashed process can be dumped (`MiniDumpWriteDump`) and read with `dotnet-dump` (managed) and WinDbg's `cdb` (native; winget `Microsoft.WinDbg`). Killing the dialog leaves an unkillable zombie process.
