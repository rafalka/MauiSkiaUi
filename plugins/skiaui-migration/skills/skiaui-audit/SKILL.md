---
name: skiaui-audit
description: Surveys a .NET MAUI app for a migration to SkiaUi's drawn controls (SkiaUi.Maui) and writes a migration plan - which screens to port first, what blocks them (CollectionView, SwipeView, third-party controls, custom handlers), which shared custom controls to port, and how much gesture and TouchBehavior code needs converting. Read-only. Use when asked whether, where or how much of a MAUI app to move to SkiaUi, or to estimate a SkiaUi migration.
argument-hint: "[app folder]"
---

# Audit a MAUI app for SkiaUi

Produce a migration plan the team can act on. Do not change any code.

## Background

SkiaUi replaces a **subtree** of a MAUI page with controls drawn on one Skia surface; pages, Shell, navigation and view models stay. It pays off on screens with many views (deep layouts, cards, dashboards, forms), where native MAUI spends time creating a handler and a platform view per control. Most MAUI layouts and basic controls have drawn equivalents with MAUI's property names. Native-only controls (Entry, Editor, pickers, WebView, third-party controls) stay native inside a `SkUiMauiContentView`; drawn Entry, Editor and SearchBar are planned but not available yet. `BindableLayout` works on drawn layouts (templates and empty views must be drawn views too). `CollectionView` lists port to the virtualized `SkUiCollectionView` (single and multiple selection, groups, grids, horizontal lists, header / footer, empty view, load more, pull-to-refresh; SkiaUi's own API, converted member by member); reorderable lists and snap points stay native. Not available yet: `SwipeView`, `RefreshView` around content other than a list, `CarouselView`, `IndicatorView`, `Stepper`. `TapGestureRecognizer` runs on drawn views; other recognizers, `TouchBehavior` (a platform behavior) and effects must be converted to drawn gesture events.

## Steps

1. **Inventory.** Run the script on the app's root folder (all projects):

   ```bash
   python3 "${CLAUDE_SKILL_DIR}/scripts/audit_maui_app.py" --top 30 <app folder> > skiaui-inventory.md
   ```

   (Without `CLAUDE_SKILL_DIR`, run it from this skill's `scripts` folder.) It counts controls by migration path, gesture input in XAML and code, `BindableLayout`, templates, custom handlers / mapper changes / renderers / effects, third-party controls, custom control usage with base types, and ranks XAML files by size with their blockers.
2. **Read the inventory critically.** Counts are textual. Ignore debug, test and sample pages when ranking. Note that a page's real size includes the custom controls it uses.
3. **Open the top candidates** (5–10 pages without blockers, or with blockers at the edge of the page) and confirm:
   - where a single drawn region would go (usually the scroll view body);
   - which custom controls they use and what those are built from;
   - code-behind that creates views, adds gestures or uses `BindableLayout` in code;
   - custom handlers / mapper changes that affect controls inside the region (find them through the inventory's "Structure and platform code" section).
4. **Check shared building blocks.** The most used custom controls decide the cost: a `ContentView` composite ports to a `SkUiContentView` subclass cheaply; a control with a custom handler, renderer, effect or third-party base needs redrawing or stays native. Port shared controls before pages.
5. **Size the gesture work** from the inventory: taps need nothing; each swipe / pan / pinch recognizer, `TouchBehavior` (and subclass) use and effect is a conversion. Long presses usually come from `TouchBehavior` `LongPressCommand` or platform code. MAUI view animations (`FadeTo`, `TranslateTo`, `new Animation`) convert one line each to render-thread `AnimateAsync` / `SkUiViewAnimation`; size animations stay MAUI.
6. **Write the plan** (Markdown, in the conversation or a file the user names) with these sections:
   - **Summary:** is a migration worthwhile, and where; the main blockers.
   - **First screens:** 3–5 pages in order, each with its drawn region, what stays native, blockers, gesture conversions and custom controls to port. Prefer large pages without lists.
   - **Shared controls to port first:** name, uses, base type, approach (drawn composite, drawn from scratch, Core nodes, stays native).
   - **Blocked until SkiaUi adds:** screens that depend on a reorderable `CollectionView` or one with snap points (the script counts those apart from the lists that port to `SkUiCollectionView`), `SwipeView`, `RefreshView` around content other than a list, `CarouselView`, with counts.
   - **Gesture and behavior conversions:** counts per kind and the files with the non-trivial ones (pan, pinch, swipe paging).
   - **Platform code to revisit:** custom handlers and mapper changes whose effect must be reproduced with drawn properties.
   - **Risks and checks:** native islands that drawn content would need to cover, third-party controls inside candidate regions, measuring before / after.
7. **Point to the next step:** the `skiaui-migrate` skill converts a page; the human guide is `docs/Migration.md` in the SkiaUi repository.

Keep the plan factual: cite files, and say when a number is an estimate.
