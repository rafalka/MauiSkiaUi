# Contributing to SkiaUi

Thanks for helping. Bug reports, reproductions, docs fixes and pull requests are all welcome.

By taking part you agree to follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## Asking a question

Use [Discussions](https://github.com/rafalka/MauiSkiaUi/discussions) for how-to questions, ideas you want to talk through, and showing what you built. Issues are for bugs and concrete feature requests. See [SUPPORT.md](SUPPORT.md).

## Reporting a bug

1. Check that it still happens with the [latest `SkiaUi.Maui` package](https://www.nuget.org/packages/SkiaUi.Maui), and search the [open issues](https://github.com/rafalka/MauiSkiaUi/issues) first.
2. Open a [bug report](https://github.com/rafalka/MauiSkiaUi/issues/new?template=bug_report.yml). The form asks for:
   - the platform and device or simulator (Android, iOS, Mac Catalyst, Windows), with the OS version;
   - the `SkiaUi.Maui`, .NET MAUI and SkiaSharp versions;
   - the layer: `SkUi*` controls (XAML, bindable properties) or `SkUiCore*` nodes (fluent `Set*`);
   - the smallest code or XAML that shows the problem, what you expected, and what happened. A screenshot or a link to a small repo helps a lot.
3. Does the same thing work with the matching native MAUI control? Say so. SkiaUi aims at MAUI's behavior, and that tells us whether it is a parity gap or a drawing bug.

Security problems are not reported as issues. See [SECURITY.md](SECURITY.md).

## Requesting a feature

Open a [feature request](https://github.com/rafalka/MauiSkiaUi/issues/new?template=feature_request.yml). Describe the problem it solves, and name the MAUI or Community Toolkit API it matches, if any. For a larger change (a new control, a new API surface, a change to layout or rendering), agree on the design in the issue before writing the code.

## Pull requests

### Before you start

- Small fixes (typos, docs, an obvious bug with a test) can go straight to a PR.
- For anything bigger, comment on the issue first so that two people don't work on the same thing, and so that the design fits the architecture ([docs/design/Architecture.md](docs/design/Architecture.md)).
- Issues labeled [`good first issue`](https://github.com/rafalka/MauiSkiaUi/labels/good%20first%20issue) and [`help wanted`](https://github.com/rafalka/MauiSkiaUi/labels/help%20wanted) are open for anyone.

### Workflow

1. Fork the repository and create a branch from `devel`, the development branch (`master` holds stable releases).
2. Set up the toolchain: the .NET SDK from [global.json](global.json) and the MAUI workload ([Development.md](Development.md#prerequisites)). Stay on MAUI 10.x.
3. Make the change. Keep a PR to one topic; unrelated cleanups go in their own PR.
4. Build and test:

   ```bash
   dotnet build MauiSkiaUi/MauiSkiaUi.csproj -f net10.0
   dotnet test tests/MauiSkiaUi.Tests/MauiSkiaUi.Tests.csproj
   ```

   The library build must have **0 warnings** (trim/AOT analyzer warnings fail it).
5. Push to your fork and open a pull request against `devel`. Fill in the template.
6. CI runs the headless tests and the platform builds. A first-time contributor's run starts after a maintainer approves it.
7. A maintainer reviews the PR. When it is approved, it is squash-merged into `devel`, and the PR title becomes the commit message. Stable releases are published from `devel`, and `master` moves to them; only hotfix PRs target `master`.

### What a good PR includes

- **Tests** for the fix or feature: headless xUnit in `tests/MauiSkiaUi.Tests`, on real MAUI objects with offscreen Skia. Text tests use the bundled font (`SkUiTestHelpers.UseBundledFont()`), because Linux CI has no system fonts.
- **Both layers.** Most controls exist as `SkUi*` and `SkUiCore*` and share one engine. A change to one usually belongs on the other too.
- **A changelog entry** under `## Unreleased` in [CHANGELOG.md](CHANGELOG.md), in the right subsection (`### New features`, `### Breaking changes`, `### Fixes`, `### Other`).
- **Docs** for user-visible changes: the control's page in [docs/controls/](docs/controls/README.md), and [docs/Migration.md](docs/Migration.md) when MAUI parity changes. A new control also gets a page in the demo's component gallery (`MauiSkiaUiDemo/ComponentPages`).
- **Performance** for changes on hot paths (layout, recording, compositing, text): compare against `devel` with `scripts/bench.sh --baseline HEAD` and paste the verdict table into the PR.

### Code style

- JetBrains ReSharper's default C# style and inspections: `_camelCase` private fields, `PascalCase` constants and `static readonly` fields, `var` for locals.
- Compare computed floating-point values with a named tolerance, not `==`.
- The property pattern, allocation rules and other conventions are in [CLAUDE.md](CLAUDE.md#code-style). It is written for AI coding agents, but it is the most compact list of the house rules.

### Using AI tools

You may use AI coding assistants. You are responsible for every line you submit: read it, build it, test it, and be ready to explain it in review.

## License

SkiaUi is [MIT licensed](LICENSE). By submitting a contribution you agree that it is licensed under the same terms.
