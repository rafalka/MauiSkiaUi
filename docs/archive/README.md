# Archive

History of SkiaUi's design: the considerations and decisions that shaped it, reviews, measurements, the plan that took it to 1.0 and the release plans from 1.2 to 1.18. These documents are kept as they were and are **not** updated: names, statuses and links in them may be out of date. The current documentation starts at [docs/README.md](../README.md).

| Document | What it was |
| --- | --- |
| [ImplementationPlan-1.0.md](ImplementationPlan-1.0.md) | The delivery plan up to 1.0: phases P (MAUI parity), A (page shells), B (virtual lists), C (list chrome, carousel, pickers), D (drawn text input), L (platform looks), M (migration blockers), U (app UI on SkiaUi), and the designs of E (SVG) and F (calendar), with each item's acceptance checks |
| [ImplementationPlan-1.2.md](ImplementationPlan-1.2.md) | The 1.2 plan for MAUI parity: phases R (API parity sweep), G (gesture recognizers), V (collection and carousel API), T (templates), S (safe area and platform-specifics), the scope review and decisions D1–D5; what it left open moved to the [roadmap](../design/Roadmap.md) |
| [ImplementationPlan-1.4.md](ImplementationPlan-1.4.md) | The 1.4 plan for images and look details: phases I (images: cache keys, downsampling to the view, download progress, fade-in, Android vector drawables) and K (look details), with its order and decisions |
| [ImplementationPlan-1.6.md](ImplementationPlan-1.6.md) | The 1.6 plan for viewport insets (phase V) and the hosted native views' clip fix (#81) |
| [ImplementationPlan-1.7.md](ImplementationPlan-1.7.md) | The 1.7 plan for vector icons: the icon SVG reader, its `LoadError`s and what it leaves to full SVG |
| [ImplementationPlan-1.8.md](ImplementationPlan-1.8.md) | The 1.8 plan for Markdown: the optional package, its blocks and extensions, and its device checks |
| [ImplementationPlan-1.9.md](ImplementationPlan-1.9.md) | The 1.9 plan: performance, the tile layout, Core containers and password reveal |
| [ImplementationPlan-1.10.md](ImplementationPlan-1.10.md) | The 1.10 plan: Community Toolkit layouts and collection view parity (keyboard, extended selection, animated changes) |
| [ImplementationPlan-1.11.md](ImplementationPlan-1.11.md) | The 1.11 plan for .NET 11 and MAUI 11: two lines from one SDK, the toolchain pins and MAUI 11's new members |
| [ImplementationPlan-1.12.md](ImplementationPlan-1.12.md) | The 1.12 plan: inline picker wheels and pickers of your own |
| [ImplementationPlan-1.13.md](ImplementationPlan-1.13.md) | The 1.13 plan for the calendar |
| [ImplementationPlan-1.14.md](ImplementationPlan-1.14.md) | The 1.14 plan for performance: track A of the November 2026 review, waves 1 to 3 |
| [ImplementationPlan-1.15.md](ImplementationPlan-1.15.md) | The 1.15 plan: the calendar finished (device checks, the date and time popup, platform looks, other calendar systems) |
| [ImplementationPlan-1.16.md](ImplementationPlan-1.16.md) | The 1.16 plan for scroll-linked effects (F2 of the November 2026 review) |
| [ImplementationPlan-1.17.md](ImplementationPlan-1.17.md) | The 1.17 plan: carousel parity, the segmented control and the tab view (F1) |
| [ImplementationPlan-1.18.md](ImplementationPlan-1.18.md) | The 1.18 plan: the sheet (F3), chips (F5), custom tab headers and the requests from the time-clock screen set (#128 to #136): font features, press scale, swipe view groups, hit slop, progress ring, stacked bar, live regions |
| [Requirements-1.0.md](Requirements-1.0.md) | The requirement checklists FR-1 – FR-33 and NFR-1 – NFR-6, item by item, and the decisions taken |
| [CoreRequirements.md](CoreRequirements.md) | The Core layer's requirements, motivation and decisions |
| [ArchitectureReview-2026-09.md](ArchitectureReview-2026-09.md) | The September 2026 architecture review (findings N1–N13) and the state-change animation measurements |
| [DrawingMechanism.md](DrawingMechanism.md) | The first paint design (layers, clipping, transparency, caching options), before the retained compositor |
| [AnimationMechanism.md](AnimationMechanism.md) | The animation design and its reference research (tiers, clock policy) |
| [LayoutSystem.md](LayoutSystem.md) | The layout design notes and checklist (the current version is [design/LayoutSystem.md](../design/LayoutSystem.md)) |
| [ScrollingAndCollectionViews-requirements.md](ScrollingAndCollectionViews-requirements.md) | How peer platforms scroll, the FR-21 / FR-22 requirements, delivery order and checklist |
| [Testing-strategy-and-DevFlow-notes.md](Testing-strategy-and-DevFlow-notes.md) | The test strategy written before the test projects existed; DevFlow agent issues up to September 2026 |
| [Benchmark-findings.md](Benchmark-findings.md) | Performance investigations and their fixes |
| [WindowsValidation-results.md](WindowsValidation-results.md) | The first Windows validation session |
| [Development-implementation-log.md](Development-implementation-log.md) | The running summary of delivered behavior kept in `Development.md` during the prereleases |
