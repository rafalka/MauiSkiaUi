# Accessibility, keyboard focus and font scaling (P10, N8)

A drawn surface is one native view, so screen readers, keyboards and the system text size see nothing of the drawn tree unless SkiaUi exposes it. P10 adds three things, for both layers:

- a **semantics tree** per surface, mapped to each platform's accessibility API (TalkBack, VoiceOver, Narrator);
- **keyboard focus** inside a surface (MAUI's `Focus()` / `Unfocus()` / `IsFocused`, tab order, activation and arrow keys, a focus ring drawn by the look);
- **OS font scaling** of drawn text (MAUI's `FontAutoScalingEnabled`).

User docs: [SkUiView.md](../controls/SkUiView.md#accessibility-and-keyboard), [SkUiCore.md](../controls/SkUiCore.md#accessibility-and-keyboard).

## Semantics tree

Built by `SkUiSemanticsTree.Build(root)` from the unified render tree (`ISkUiRenderable`: SkUi* views and Core nodes alike), on the UI thread. Every drawn node implements `ISkUiAccessibleNode`:

| Member | SkUi* (`SkUiView.Accessibility.cs`) | Core (`SkUiCoreNode.Accessibility.cs`) |
| --- | --- | --- |
| `GetSemantics(info)` | tap / long-press actions and enabled state, then `OnPopulateSemantics` (controls add role, text, state), then MAUI's `SemanticProperties` (`Description`, `Hint`, `HeadingLevel`) and `AutomationProperties` (`Name`, `HelpText`, `IsInAccessibleTree`, `ExcludedWithChildren`) | the same, with the node's `SemanticDescription`, `SemanticHint`, `SemanticHeadingLevel`, `IsInAccessibleTree`, `ExcludedWithChildren` |
| `PerformSemanticsAction` / `SetSemanticsValue` | `OnSemanticsAction` (Activate runs `OnTapped`: `Tapped`, `TappedCommand`, a control's own action), `OnSemanticsSetValue` | `OnSemanticsAction` (Activate runs the `Tapped` handlers and the intrinsic tap) |
| Focus | `IsTabStop` / `TabIndex` (SkiaUi's own bindables: MAUI 10 has none), MAUI's `IsFocused` | `IsTabStop` / `TabIndex` / `IsFocused`, `Focused` / `Unfocused` |

`OnPopulateSemantics` / `OnSemanticsAction` / `OnSemanticsSetValue` are `protected virtual`, so app controls describe themselves the same way (`SkUiSemanticsInfo`: `Role`, `Text`, `Value`, `CheckState`, `Range`, `IsEnabled`, `Actions`, `IsHorizontal`).

**Which nodes are elements** (`Builder.IsElement`), after native platforms:

- Controls (any role but `None`, `Text`, `Image`) always; text with text; images and plain containers only with a description, a heading level or an action. `IsInAccessibleTree` forces either way (children are still walked); `ExcludedWithChildren` drops the subtree.
- A **disabled** node (pointers and keys blocked) disables its subtree's elements too, and actions on them do nothing.
- An **actionable** element (an Activate / LongPress action, or a button, toggle or slider role) reads the text of its non-actionable descendant elements as its name, joined with ", ", and they are not elements themselves (Android merges clickable groups the same way); actionable descendants stay separate elements, children of it. A **description** replaces the text; then descendant text is dropped (iOS: a labelled container is one element).
- A hosted `SkUiMauiContentView` is a `IsNative` placeholder: platforms read the native view itself.
- Scroll views are container elements (`ScrollView` role, page actions); their children are their elements.

**Order and bounds.** Reading order is the drawn tree's (children in paint order). Bounds are surface DIPs of the **visible part**: the node's rectangle through every transform (`SkUiRenderProps.Matrix`), scroll offsets and overscroll scale (`ChildrenMatrix`), intersected with ancestors' `ClipToBounds`, children clips and `Clip` geometry bounds; pinned children (scroll bars) skip the children space. Elements scrolled or clipped out of view are left out: screen readers scroll to reach them (TalkBack past the last visible element, VoiceOver three-finger swipes), as with recycled native lists.

**Ids** are stable per node (`SkUiRenderState.SemanticsId`, assigned on first use and kept across rebuilds and surfaces), so platform objects and screen-reader focus survive rebuilds.

## Keeping it current

`SkUiSemanticsOwner` keeps one surface's tree for its platform bridge (`SkUiView.SemanticsOwner` on the root). It is created only when the platform asks (Android: an accessibility service queries; Apple: a client reads `accessibilityElements`; Windows: a UIA client creates the peer), so surfaces without assistive technology pay nothing.

- **Invalidation:** every committed frame (`SkUiFrameRenderer.PresentFrame`), every scroll offset change (`SkUiScrollController.UpdateState`, also render-thread motion reported back), MAUI semantic property changes (`SkUiView.OnPropertyChanged`), Core semantic setters, focus changes. The tree is rebuilt lazily on the next read.
- **Reports:** while the bridge listens, changes are coalesced over 100 ms (`ReportDelay`) and diffed against the last reported tree (`Diff`: structure changed, ids whose content changed). Headless, without a dispatcher, `Flush` reports on demand.

## Cost and switching it off

**Unused** (no assistive technology reads the surface), the semantics tree does not exist: a committed frame costs a null check, a scroll offset change a walk to the root, a property change a few name comparisons; every `SkUiView` subscribes one event at construction, every Core node carries ~30 bytes of focus and `Tag` fields. Keyboard focus is always on (one manager per surface; detach / hide checks return at once unless something is focused).

**Read** (a screen reader, or any accessibility client: Android password managers and automation apps turn `AccessibilityManager.IsEnabled` on; Mac window managers query the tree): a rebuild walks the visible part of the drawn tree. Subtrees outside the visible area that clip to their bounds are skipped (long scrolled lists cost what is on screen); measured headless with 2,000+ nodes before that culling, 1.6 ms / 41 KB per rebuild. Changes are reported once the tree has been quiet for `ReportDelay` (100 ms), at least every `MaxReportDelay` (500 ms) while it keeps changing (a scroll, a fling, an animation), so continuous motion does not rebuild for every frame. `HasFocusableNodes` (asked by UIKit's focus engine) stops at the first focusable node and allocates nothing; the tab order (Tab only) is sorted without LINQ.

**Switches** (font scaling is not affected by either):
- `SkUiView.IsAccessibilityEnabled` (bindable, default true): `false` hides the view's subtree from screen readers and keyboard focus. On a surface root the handler also removes the platform bridge (TalkBack helper, VoiceOver elements, Narrator peers) and the container's keyboard participation (Android focusable, Windows tab stop; on Apple the container is no focus item without focusable nodes), and the root's own `SemanticProperties` are mapped onto the native view by MAUI's `ViewHandler.MapSemantics`, as for any MAUI view. Switching back attaches them again. For decorative or self-described surfaces: charts, game canvases, backgrounds.
- `SkUiAccessibility.IsEnabled` (static, default true): the same for every surface, for apps that guarantee no assistive technology (kiosks, games). Focus is cleared at once; live surfaces follow `SkUiAccessibility.Changed`.
- MAUI's `AutomationProperties.ExcludedWithChildren` on a root also empties the tree (keyboard focus stays).
- Checked on Mac Catalyst (2026-10-04, the demo's switch): off, the drawn elements leave the accessibility tree; on again, they are read again.

## Platform bridges

| | Android (`SkUiAccessibility.Android.cs`) | iOS / Mac Catalyst (`SkUiAccessibility.Apple.cs`) | Windows (`SkUiAccessibility.Windows.cs`) |
| --- | --- | --- | --- |
| Host | `SkUiAccessibilityHelper : ExploreByTouchHelper` on the surface container (`SkUiOverlayContainer`); the surface view is not important for accessibility | the container exports `accessibilityElements`: `SkUiAccessibilityElement : UIAccessibilityElement` per element (frames in container space), hosted native views in their place | the container's `SkUiSurfaceAutomationPeer` returns one `SkUiElementAutomationPeer` per element, then the native views' peers; the surface panel is `AccessibilityView.Raw` |
| Structure | virtual views with virtual parents (`SetParent` / `AddChild`): TalkBack scrolls the scrollable ancestor of the focused node | flat, in reading order (a scroller without a name is not an element) | peers nest like the tree; `GetPeerFromPointCore` hit-tests |
| Roles | framework class names (`android.widget.Button`, `CheckBox`, `Switch`, `SeekBar`, …), checkable / checked, `RangeInfo`, heading, hint (26+), `StateDescription` for values (30+) | traits: `Button`, `StaticText`, `Image`, `Header`, `Adjustable`, `NotEnabled`, `Selected` (checked radio); check box and switch borrow `UISwitch`'s traits with a `1` / `0` value, as MAUI's check box | `AutomationControlType`, heading level, help text, automation id |
| Actions | click, long click, scroll forward / backward (sliders step, scrollers page), set progress (24+), keyboard focus | `accessibilityActivate` (exported: informal protocol), increment / decrement, `accessibilityScroll:` (pages the innermost scrollable ancestor; vertical directions are the scroll bar's: Down = forward, as Flutter maps them) | Invoke, Toggle, SelectionItem (radio buttons), RangeValue, `SetFocusCore` |
| Updates | `InvalidateRoot` on structure changes, `InvalidateVirtualView` per changed element; the helper's keyboard focus follows the drawn focus (focused state and focus actions on the nodes) | layout-changed notification on structure changes; elements updated in place | `StructureChanged` / property-changed events (name, toggle state, value), `AutomationFocusChanged` for keyboard focus |
| Scroll into view | when an element gets accessibility focus | `accessibilityElementDidBecomeFocused` | — |
| `SetSemanticFocus` | `ACTION_ACCESSIBILITY_FOCUS` on the virtual view | layout-changed notification with the element | `AutomationFocusChanged` |

MAUI's `Semantics` mapping of the root view is replaced (`SkUiViewHandler` mapper): applied to the container it would make it one element hiding the drawn ones (iOS); the root's semantics are part of the drawn tree instead. `SkUiView.SetSemanticFocus()` (and Core's) takes the place of MAUI's `SetSemanticFocus` extension, which needs a native view; as an instance method it wins over the extension in app code.

## Keyboard focus

`SkUiFocusManager` (one per surface root, `SkUiView.FocusManager`) holds at most one focused drawn node while the surface has the platform's keyboard focus.

- **MAUI's `Focus()` / `Unfocus()`** on handler-less views (and on the root through its handler's focus mapping) go to `VisualElement.FocusChangeRequested`, which `SkUiView` handles; `IsFocused` is set through `IView.IsFocused`, which raises `Focused` / `Unfocused` and the `Focused` / `Unfocused` visual states. Core: `Focus()` / `Unfocus()` / `IsFocused` / `Focused` / `Unfocused`.
- **Focusable:** visible, enabled (and a command that can execute), `IsTabStop`, interactive (`TakesKeyboardFocus`: a tap handler, a control's intrinsic tap, sliders), with no hidden, disabled or input-transparent ancestor. Labels and plain containers are not (as on Windows); `Focus()` on a surface root that is not focusable itself focuses its first node.
- **Tab order:** `TabIndex`, then tree order. Tab / Shift+Tab move within the surface and return "unhandled" at either end, so the platform moves on to the next native control; tabbing into a surface focuses its first (or last) node, and a surface with nothing focusable passes focus on.
- **Keys:** Space / Enter activate (the Activate action; auto-repeats of a held key are used but do not activate again); arrows adjust a range element (Right / Up raise, Left raises in right-to-left layouts), otherwise scroll the focused node's innermost scroller (or the surface's first one) by 40 DIPs; Page Up / Down scroll by 87.5 % of the viewport; Home / End jump a slider to its bounds or a scroller to its ends.
- **Focus ring:** drawn by the look (`SkUiLook.DrawFocusRing`, `SkUiFocusRingPaint`, `DefaultSkUiLook.FocusRingThickness`) in the focused node's overlay, inside its bounds (leaf controls clip to them), while focus came from the keyboard: after a key press, or programmatic focus while the keyboard was used last. A pointer press hides it (focus-visible).
- **Native focus:** the container takes it for a focused drawn node (Android: focusable, focusable in touch mode only while a drawn node is focused, so taps never take focus from a hosted Entry; Apple: first responder, hardware keys through `PressesBegan`; Windows: a tab stop without system focus visuals, keys through `KeyDown`). Losing it (another native control took focus; on Windows from the synchronous `LosingFocus`, as `GotFocus` / `LostFocus` arrive late) unfocuses the drawn node, and `Unfocus()` gives it up (Android: clears focus, iOS: resigns first responder; Windows keeps it). Every `Focus()` asks the platform again instead of trusting the last focus event. Android's own `requestFocus()` reports `FOCUS_DOWN`, which is not read as Tab into the surface.
- **UIKit's focus system (iOS / Mac Catalyst):** the container is a focus item (`CanBecomeFocused` while a drawn node can take focus), its own focus group (Tab stops on it; arrows stay inside it) and has no system focus effect (the look draws the ring). When the focus system focuses it, it also becomes first responder (keys reach `PressesBegan`) and focuses the first drawn node, or the last when focus comes from after it (a Previous / Up / Left / Last heading; Mac Catalyst reports no heading for Tab, so the previously focused view's position decides: below the surface, or level and to its right, is after). Inside, Tab / Shift+Tab are claimed by key commands with `WantsPriorityOverSystemBehavior`: `CanPerform` accepts them while the drawn focus can move that way (`SkUiFocusManager.CanMoveFocus`), so UIKit leaves the key to the container and the drawn focus moves; past the last / first node it declines and UIKit moves focus to the next native control. Arrows a drawn node uses (slider, scroller) keep focus (`ShouldUpdateFocus`). `Focus()` also asks the focus system to focus the container. As native controls there, Tab reaches the surface only where the system lets it reach buttons: macOS System Settings › Keyboard › Keyboard navigation (otherwise Tab moves between text fields only; UIKit reads the setting at launch, so restart the app after changing it), iPad with a hardware keyboard (Full Keyboard Access for everything). Once a drawn node has focus (`Focus()`), Tab, Space / Enter and arrows work whatever the setting. Escape clears the system focus (UIKit). MAUI's Picker on Mac Catalyst opens its selection sheet when it gets focus, which keeps Tab inside the sheet. Checked on Mac Catalyst 2026-10-04: Tab through every drawn control of both layers (also inside a drawn scroller) and out after the last, Shift+Tab out before the first, Space.
- **Pointer focus:** on Windows a press focuses the interactive node it hits (or its nearest focusable ancestor) without the ring, as WinUI's controls (`SkUiFocusManager.PointerPressFocuses`, set by the handler on Windows only); on Apple and Android a click or tap does not move keyboard focus, as native controls there.
- **Scroll into view:** focusing a node scrolls every drawn scroller around it (`SkUiSemantics.BringIntoView`, MAUI's `MakeVisible` rule).
- Focus is cleared when the focused node is hidden, disabled, removed or loses its tab stop (`SkUiFocusManager.ValidateAll`, from the detach and visibility paths of both layers).

## Font scaling

`SkUiFontScaling.ScaleFontSize(size)` is the size drawn for a font size in DIPs: Android's `sp` conversion (`TypedValue.ApplyDimension`, so Android 14's non-linear curve applies), Apple's `UIFontMetrics.DefaultMetrics` (Dynamic Type, the body style's curve as MAUI's `GetScaledFont`), Windows' `UISettings.TextScaleFactor` (linear). Results are cached per size and cleared on change (Android configuration callbacks, the content size category notification, `TextScaleFactorChanged`). `Factor` overrides the system with a linear factor (tests).

**App-wide prescale:** `SkUiLook.FontScale` (default 1) multiplies every text size first, also of text without `FontAutoScalingEnabled`; text that auto scales then gets the system scale of the prescaled size (`system(size × FontScale)`: the same as `FontScale × system scale` where the system scales linearly; on Android 14+ the non-linear curve sees the prescaled size, as if the app had used that font size). Setting it on the current look raises `SkUiLook.CurrentChanged`: surfaces re-measure and the font-scaling version bumps (rich text rebuilds); swapping looks does the same.

- Applied where text styles are built: `SkUiLabel` / `SkUiCoreLabel` (so buttons too), spans and HTML runs (`SkUiHtml.ToRichText(…, fontAutoScalingEnabled)`), `SkUiRadioButton` text content and its presented label, and font images (`SkUiFontImageSource.DrawnSize`, scaled when the source is created: a later change applies to newly created sources). Line caches are keyed by the style, which carries the scaled size; rich text caches compare `SkUiFontScaling.Version`.
- `Changed` makes every live surface re-measure and redraw (the same path as a look change).

## Verification

- Headless: `AccessibilityTests` (tree rules, both layers, bounds through scrolling, actions, ids and change reports, hit-testing), `KeyboardFocusTests` (MAUI focus API, Core focus, tab order, keys, ring pixels, native focus hooks, scroll into view), `FontScalingTests`; leak scenario `AccessibleFocused`.
- Mac Catalyst (2026-10-03): the demo's **Accessibility** page through the macOS accessibility API (what VoiceOver reads): every drawn element with its role, name, value and hint, the hosted Entry in place, Core nodes; press, increment and page-scroll actions; Tab / Shift+Tab / Space / Enter from a hardware keyboard with the ring.
- Windows compiles on macOS against the real WinAppSDK references: `dotnet build MauiSkiaUi -f net10.0-windows10.0.19041.0 -p:TargetFrameworks=net10.0-windows10.0.19041.0 -p:EnableWindowsTargeting=true -p:AppxGeneratePriEnabled=false -p:EnableCoreMrtTooling=false -p:EnableMsixTooling=false`.
- Not yet run: TalkBack, VoiceOver on iOS, Narrator; keyboards on Android and Windows (checklist in [Testing.md](Testing.md#accessibility-and-keyboard-p10)).

## Left open

- Links inside HTML / span text as their own elements (they are read as part of the label), custom actions (`UIAccessibilityCustomAction`, Android custom actions), live regions / announcements of drawn content.
- Arrow-key navigation between radio buttons of a group, and XY (spatial) focus navigation.
- iPad keyboard navigation (implemented through the same focus-system path as Mac Catalyst) is not verified yet.
- Windows' text scaling is applied linearly; WinUI grows large text less.
- Android: MAUI maps a root's `AutomationId` to the surface container's content description (what Appium finds it by), so TalkBack may read that id on the container itself.
- Font images keep the text size of when their source was created.
