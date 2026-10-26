# SkiaUi documentation

## Using SkiaUi

| Document | For |
| --- | --- |
| [README](../README.md) | What SkiaUi is, installation, a first page |
| [Controls](controls/README.md) | One page per control and layout: what it adds to its MAUI counterpart, its own API, samples |
| [Migration guide](Migration.md) | Porting MAUI pages to SkiaUi, step by step; what is not available yet |
| [Performance](Performance.md) | Device measurements against native MAUI, and the cost of each drawing effect |
| [Samples](../samples/README.md) | The samples app |
| [Changelog](../CHANGELOG.md) | What each release changed |
| Migration skills ([plugins/skiaui-migration](../plugins/skiaui-migration/README.md)) | Claude Code skills that audit and port MAUI pages |

## Contributing

| Document | For |
| --- | --- |
| [CONTRIBUTING](../CONTRIBUTING.md) | Reporting bugs, proposing features, opening pull requests |
| [Development](../Development.md) | Solution layout, building, CI, adding a control |
| [Releasing](Releasing.md) | Versions, branches, the changelog and the publish workflow |
| [Architecture](design/Architecture.md) | How SkiaUi works: start here |
| [Requirements](design/Requirements.md) | Goals, scope, principles, requirement status |
| [Roadmap](design/Roadmap.md) | 1.0.0, 1.1.0, 1.2.0 and the backlog |
| [Implementation plan 1.2](design/ImplementationPlan-1.2.md) | MAUI parity: phases, order, decisions |

Design docs, by mechanism:

| Document | Covers |
| --- | --- |
| [LayoutSystem](design/LayoutSystem.md) | Measure / arrange, hosted vs standalone views, MAUI layout managers, right to left |
| [RenderingPipeline](design/RenderingPipeline.md) | Retained render tree, UI and render threads, surfaces, animation tiers, shadows and clips, composite-time effects |
| [EventMechanism](design/EventMechanism.md) | Gesture arena, recognizers, hover and tooltips, native coordination |
| [ScrollingAndCollectionViews](design/ScrollingAndCollectionViews.md) | Scroll engine, virtual stack, collection view, pull to refresh, carousel |
| [TextInput](design/TextInput.md) | Editing engine, platform input proxies, text fields and their extensions |
| [ControlLook](design/ControlLook.md) | Looks: painters, default sizes, transitions, platform looks |
| [ColorScheme](design/ColorScheme.md) | Default palettes |
| [Popups](design/Popups.md) | The popup presenter contract and integration packages |
| [Accessibility](design/Accessibility.md) | Semantics tree, platform bridges, keyboard focus, font scaling |
| [Localization](design/Localization.md) | SkiaUi's own texts |
| [Testing](design/Testing.md) | Test suites, memory-leak scenarios, device checklists and verification |
| [Benchmarks](design/Benchmarks.md) | Headless and device benchmarks (`scripts/bench.sh`) |

## Archive

[archive/](archive/README.md): design history (decisions, reviews, the 1.0 plan and requirement checklists). Kept as written, not updated.
