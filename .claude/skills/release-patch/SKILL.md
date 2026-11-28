---
name: release-patch
description: Publish a bugfix release (X.Y.Z+1) of the SkiaUi packages from devel, then update the changelog, roadmap and docs and close the fixed issues
disable-model-invocation: true
argument-hint: "[expected version, e.g. 1.18.1]"
---
Bugfix release from `devel` with the publish workflow's bump `patch` (1.18.0 → 1.18.1). It ships everything `devel` has since the last release, and `master` moves to it. Procedure and rules: [docs/Releasing.md](../../../docs/Releasing.md). A minor release with a plan is `/release`.

Invoking this skill is the maintainer's go-ahead to publish the version the pre-flight computes and to approve the `nuget.org` environment gate for that run. It covers nothing else: if the pre-flight finds anything missing or unverified, stop and ask before dispatching.

## 1. Pre-flight: is a patch the right release?

Report a table (check, result, evidence). Don't dispatch until all of these hold.

- **Version:** `scripts/release_version.py next --bump patch --branch devel`. If `$ARGUMENTS` names a version and it differs, stop and ask. If `devel` is in a prerelease line, the workflow refuses `patch`: stop and explain the [hotfix route](../../../docs/Releasing.md#hotfixes) (a PR into `master`, then the publish workflow from `master` with bump `auto`). Don't start it without the maintainer's word.
- **Branches:** on `devel` with a clean tree, `git fetch`, local `devel` equals `origin/devel`, and `git rev-list --count origin/devel..origin/master` is 0.
- **What ships:** `git log vPREV..origin/devel --oneline`. Every commit should be a fix, docs, samples or tests. A feature commit or a `### New features` entry is allowed (1.15.1 had one) but needs to be named in the report and asked about. `### Breaking changes`, or a commit from a planned minor release (a `docs/design/ImplementationPlan-X.Y.md` for the next minor that has started), means stop: that is `/release` material.
- **Changelog:** `## Unreleased` has an entry for each commit above, with only the allowed headings in order and the issue numbers.
- **Fixes:** each fixed issue (`gh issue view`) has its merged PR on `devel` and a regression test that fails without the fix (name the test). A fix verified only on a device needs that device check named, or it is reported as UNVERIFIED.
- **Build and tests:** CI on the `devel` head commit passed (`gh run list --workflow ci.yml --branch devel -L 5 --json headSha,status,conclusion`). Locally, the library builds with 0 warnings and the headless suite passes.

Something missing, broken or unverified → stop, report, ask. Everything holds → go on without asking.

## 2. Publish

Follow [publish.md](../release/publish.md) with `BUMP=patch`.

## 3. Docs after the release

- **Changelog:** `## X.Y.Z` replaced `## Unreleased`, and an empty `## Unreleased` is above it. Check the heading order and that every shipped commit is covered.
- **Roadmap:** the shipped section's heading names the patch (`### 1.18.0 and 1.18.1: … (shipped)`, as 1.13.1 and 1.15.1 did), and its status line says in one sentence what the patch fixed, with the issue links. Verification-backlog items the fixes closed are removed or marked done.
- **Other docs:** control pages or design docs that described a fixed bug as a limitation or known issue no longer do. Migration docs and `plugins/skiaui-migration` are updated if a fix changed MAUI parity.
- No plan moves to the archive, since a patch has no plan of its own. Don't edit `docs/archive/`.

## 4. Push and close

- Commit (`Docs for the X.Y.Z release: …`, with the attribution trailer) and push straight to `devel`. Never force-push, and never push to `master`.
- Check that the bugs and enhancements this release fixed are closed: follow [publish.md](../release/publish.md) step 7. Never write "released" before step 5 confirmed the version is live.
- Report: version, links (nuget.org, GitHub release, workflow run), the pre-flight table, the docs commit, an issues table (number, title, state before and after, what was done), and anything UNVERIFIED and why.
