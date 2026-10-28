# Archive

History of SkiaUi's design: the considerations and decisions that shaped it, reviews, measurements, the plan that took it to 1.0 and the 1.2 plan. These documents are kept as they were and are **not** updated: names, statuses and links in them may be out of date. The current documentation starts at [docs/README.md](../README.md).

| Document | What it was |
| --- | --- |
| [ImplementationPlan-1.0.md](ImplementationPlan-1.0.md) | The delivery plan up to 1.0: phases P (MAUI parity), A (page shells), B (virtual lists), C (list chrome, carousel, pickers), D (drawn text input), L (platform looks), M (migration blockers), U (app UI on SkiaUi), and the designs of E (SVG) and F (calendar), with each item's acceptance checks |
| [ImplementationPlan-1.2.md](ImplementationPlan-1.2.md) | The 1.2 plan for MAUI parity: phases R (API parity sweep), G (gesture recognizers), V (collection and carousel API), T (templates), S (safe area and platform-specifics), the scope review and decisions D1–D5; what it left open moved to the [roadmap](../design/Roadmap.md) |
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
