## What and why

<!-- What this PR changes and why. Link the issue: "Fixes #123". -->

## How it was tested

<!-- Tests added or changed; platforms and devices you ran it on; screenshots for visual changes. -->

## Checklist

- [ ] `dotnet test tests/MauiSkiaUi.Tests/MauiSkiaUi.Tests.csproj` passes, and the library builds with 0 warnings
- [ ] Tests cover the change
- [ ] Both layers (`SkUi*` and `SkUiCore*`) are updated where the control exists on both
- [ ] Entry under `## Unreleased` in `CHANGELOG.md`
- [ ] Docs updated (`docs/controls/`, `docs/Migration.md` when MAUI parity changes, demo gallery page for a new control)
- [ ] For hot-path changes: `scripts/bench.sh --baseline HEAD` result pasted below

<!-- See CONTRIBUTING.md for details. -->
