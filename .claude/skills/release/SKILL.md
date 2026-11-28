---
name: release
description: Publish a stable minor release (X.Y.0) of the SkiaUi packages from devel, then bring the roadmap, plans, changelog and docs up to date
disable-model-invocation: true
argument-hint: "[expected version, e.g. 1.19.0]"
---
Full release from `devel` with the publish workflow's bump `release` (1.18.0 → 1.19.0, or the end of a prerelease line). Procedure and rules: [docs/Releasing.md](../../../docs/Releasing.md). A bugfix-only release is `/release-patch`.

Invoking this skill is the maintainer's go-ahead to publish the version the pre-flight computes and to approve the `nuget.org` environment gate for that run. It covers nothing else: if the pre-flight finds anything missing or unverified, stop and ask before dispatching.

## 1. Pre-flight: is everything planned for this release in?

Don't dispatch anything until all of these hold. Report them as a table (check, result, evidence).

- **Version:** `scripts/release_version.py next --bump release --branch devel`. If `$ARGUMENTS` names a version and it differs, stop and ask.
- **Branches:** on `devel` with a clean tree, `git fetch`, local `devel` equals `origin/devel`. `git rev-list --count origin/devel..origin/master` is 0 (a hotfix not merged back blocks a release from `devel`).
- **The plan:** `docs/design/ImplementationPlan-X.Y.md` for the new version. Go through every phase and step. Each one has its implementing commit or PR on `devel` (`git log vPREV..origin/devel`), and its Verification items are done or listed as open. Do the same for the roadmap's `### X.Y.0` section in [Roadmap.md](../../../docs/design/Roadmap.md) and for every issue the plan or roadmap names (`gh issue view`: fixed by a merged PR or a commit on `devel`). A step marked as a cut line may be left out, but say so.
- **Open work:** `gh pr list --base devel` shows no open PR that the plan counts on. List any others and ask only if one looks like it belongs in the release.
- **Changelog:** `## Unreleased` has entries, only the allowed headings (`### New features`, `### Breaking changes`, `### Fixes`, `### Other`, in that order), and an entry for each planned feature and fixed issue. Every merged PR since the last tag is covered (`git log vPREV..origin/devel --oneline`). Breaking changes in a minor release need the maintainer's explicit OK.
- **Build and tests:** CI on the `devel` head commit passed (`gh run list --workflow ci.yml --branch devel -L 5 --json headSha,status,conclusion`, matched to the head SHA). Locally, `dotnet build MauiSkiaUi/MauiSkiaUi.csproj -f net10.0 -v q -clp:ErrorsOnly` with 0 warnings and the headless suite passing.
- **Device checks:** list the plan's device verifications that are still open. They don't block the release, since they go to the roadmap's verification backlog, but they belong in the report.

Something planned is missing, broken or unverified → stop, report, ask. Everything holds → go on without asking.

## 2. Publish

Follow [publish.md](publish.md) with `BUMP=release`.

## 3. Docs after the release

The commit `Docs for the X.Y.0 release: …` (see `6656b946` for 1.18.0) covers:

- **Roadmap:** the `### X.Y.0` heading gets `(shipped)`, and its status line becomes "released <date of the GitHub release>" with what shipped and what was cut. Open device checks move to `## Verification backlog`. Items that shipped leave `## Backlog (1.x)`. If the next release is already planned, its section is there.
- **Requirements:** the FR/NFR items the release closed are marked shipped in [Requirements.md](../../../docs/design/Requirements.md).
- **Plan to the archive:** `git mv docs/design/ImplementationPlan-X.Y.md docs/archive/`. Fix its relative links (`](Roadmap.md` → `](../design/Roadmap.md`, and so on) and every link to it from elsewhere (`grep -rn 'ImplementationPlan-X.Y' --include=*.md . --exclude-dir=artifacts --exclude-dir=.claude`). Set its **Status** to "released in X.Y.0 (<date>)", with open checks pointing to the verification backlog. Add its row to [docs/archive/README.md](../../../docs/archive/README.md). In umbrella plans that stay in `docs/design` (`ImplementationPlan-PerformanceAndControls.md`, `ImplementationPlan-FullSvg.md`), mark the items as "Shipped in X.Y". Archive an umbrella plan only when all of it has shipped.
- **Changelog:** the workflow renamed `## Unreleased` to `## X.Y.0` and opened an empty `## Unreleased`. Check the section's heading order, the bold feature titles, the issue numbers, and that nothing from the pre-flight's PR list is missing. Fix the wording here. The release notes already on nuget.org stay as published.
- **Other docs:** every new control has a page in [docs/controls](../../../docs/controls/README.md) and the index lists it. The design docs the release touched no longer say "planned", "not yet" or "in X.Y" for what shipped (`grep -rn 'X\.Y' docs --include=*.md`, outside `docs/archive`). [Migration.md](../../../docs/Migration.md) and `plugins/skiaui-migration` are updated where MAUI parity changed. [Benchmarks.md](../../../docs/design/Benchmarks.md) has the release's bench record if perf-relevant code changed (`scripts/bench.sh --baseline vPREV` on the scenarios it touched). Paragraphs about holding a release back (the "Before X" kind in Releasing.md) are removed once they are past. README's feature list includes the new controls.
- Don't edit `docs/archive/` beyond the moved plan and the archive README: it is history.

Build nothing the docs don't need. Run the headless suite only if code changed.

## 4. Push and close

- Commit the docs (with the attribution trailer) and push straight to `devel`. The `devel` ruleset allows direct pushes, and this is how release docs land. Never force-push, and never push to `master`.
- Check that the bugs and enhancements this release implemented are closed: follow [publish.md](publish.md) step 7. Never write "released" before step 5 confirmed the version is live.
- Report: version, links (nuget.org, GitHub release, workflow run), the pre-flight table, what the docs commit changed, an issues table (number, title, state before and after, what was done), open device checks, and anything that is UNVERIFIED and why.

## Not in this skill

Hotfixes from `master` and prerelease lines (bumps `auto`, `prerelease`, `minor`, `major`) follow [docs/Releasing.md](../../../docs/Releasing.md) by hand.
