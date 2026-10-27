# Releasing SkiaUi.Maui

Every release publishes all of the repository's packages with one version: **SkiaUi.Maui** and **SkiaUi.Maui.CommunityToolkit** (which depends on SkiaUi.Maui of the same version). One changelog covers both. A new package added to the release loop of the pack and publish workflows needs, the first time, a nuget.org Trusted Publishing policy that covers it (or the push of its first version fails).

## Branches and versions

1.0.0 was the first stable release. Since then:

| Branch | Holds | Versions |
| --- | --- | --- |
| `master` | The latest stable release, and hotfixes on top of it | `1.1.0`, `1.2.0`, `1.2.1`, … |
| `devel` | Active development; pull requests target it. Prereleases and stable releases are published from it | `1.2.0-Prerelease01`, …, `1.2.0`, `1.3.0-Prerelease01`, … |

Changes flow one way: feature branches → `devel` (squash-merged pull requests) → `master`. `devel` never goes to `master` through a pull request: a squash merge would put a commit on `master` that `devel` does not have, and every release would then need `master` merged back. Instead, `master` is moved forward to each stable release commit made on `devel`:

1. Prereleases: run the publish workflow from `devel` with bump `auto` (`1.2.0-Prerelease01` → `1.2.0-Prerelease02`).
2. Stable release: run it from `devel` with bump `release` (`1.2.0-Prerelease03` → `1.2.0`). The release commit `Release 1.2.0`, its tag `v1.2.0` and `master` are pushed together: `master` is fast-forwarded to the release commit, so `master` and `devel` are the same commit and nothing needs merging back.
3. The next prerelease line: bump `auto` on `devel` at a stable version is `minor` (`1.2.0` → `1.3.0-Prerelease01`); use `patch` for a `1.2.1-Prerelease01` line.

A stable release from `devel` is refused while `master` has commits `devel` does not (a hotfix not merged back yet, see [Hotfixes](#hotfixes)).

Up to 1.1.0, `devel` was merged into `master` by pull request and stable releases were published from `master`; `master` was merged back into `devel` after each.

Before 1.0.0 there was only `master`, which published `1.0.0-Prerelease01` … `1.0.0-Prerelease15`.

Version format: `X.Y.Z`, or `X.Y.Z-PrereleaseNN` with a two-digit `NN`. `Version` in [Directory.Build.props](../Directory.Build.props) is the **last released** version of the branch; the demo app's display version is its numeric part.

## Changelog

Write entries under `## Unreleased` at the top of [CHANGELOG.md](../CHANGELOG.md), grouped under `### New features`, `### Breaking changes`, `### Fixes` and `### Other` (other headings are refused). Releasing renames that section to the new version and opens a new empty `## Unreleased` above it, so nobody needs to know the next version number while working. A release whose `## Unreleased` has no entries is refused.

[`scripts/extract-release-notes.py`](../scripts/extract-release-notes.py) turns the section into release notes:

- **NuGet** (`PackageReleaseNotes`, shown on nuget.org as plain text): a link to the section at the release tag, the titles of the new features (a feature bullet's text up to its first `:` or `;`) and the breaking changes in full, with the Markdown stripped. Fixes and Other are left to the link.
- **GitHub release** (`--format markdown`): the whole section.

## Publishing

Run the **NuGet publish** workflow ([nuget-publish.yml](../.github/workflows/nuget-publish.yml)) from GitHub Actions. Choose the branch (`master` or `devel`) in "Use workflow from", and pick a **bump** (`auto` fits the usual case):

| Bump | Effect | Allowed on |
| --- | --- | --- |
| `auto` (default) | On `devel`: `prerelease`, or `minor` at a stable version. On `master`: `patch` (a hotfix release) | Both |
| `prerelease` | `-PrereleaseNN` → `-Prerelease(NN+1)` | `devel` (from a prerelease version) |
| `release` | Drops the suffix: `1.2.0-Prerelease03` → `1.2.0`. From `devel`, also moves `master` to the release | `devel` |
| `patch` / `minor` / `major` | Next `X.Y.Z`; on `devel`, its `-Prerelease01` | Both |
| `none` | Publishes the current, already tagged version again from its tag (e.g. after a failed publish) | Both |

The workflow then:

1. Computes the version with [`scripts/release_version.py`](../scripts/release_version.py). It fails if the version is already on nuget.org, or if its tag already exists. If nuget.org can't be reached, it warns and continues; the push itself would still refuse a duplicate.
2. Updates `Directory.Build.props` and `CHANGELOG.md` in the job's workspace, and packs that on macOS and Windows. The changelog section becomes `PackageReleaseNotes`.
3. After the `nuget.org` environment gate (where reviewers approve, if required), commits `Release <version>` on top of the dispatch commit and pushes it together with the tag `v<version>` (`git push --atomic`). A stable release from `devel` pushes `master` to the same commit in that push, as a fast-forward (never forced). If a branch moved since the run started, the push is rejected and nothing is published or tagged: run it again.
4. Pushes the package to nuget.org (Trusted Publishing), then creates the GitHub release `v<version>` with the notes and the package. Prereleases are marked as such. GitHub adds the source code (zip and tar.gz) to every release.
5. Builds the demo and samples apps from the tag ([demo-publish.yml](../.github/workflows/demo-publish.yml)) and attaches them to the release: `SkiaUi-Demo-<version>-android-arm64.apk`, `SkiaUi-Demo-<version>-maccatalyst.zip`, and the same for `SkiaUi-Samples`. This runs after the package is published, so a failed app build does not hold it back: run **Demo publish** with `release_tag` = `v<version>` to attach them again. The same run attaches apps to an older release.

**Dry run:** computes the version and packs, but doesn't commit, tag, push to nuget.org or create a release. The packages are uploaded as workflow artifacts.

**If a run fails after the release commit was pushed** (step 4, e.g. a nuget.org outage), run it again with bump `none`: it rebuilds the tagged release commit `v<version>`, even if the branch has moved on since, and publishes it without bumping again. `none` requires that tag.

Preview the next version locally:

```bash
scripts/release_version.py next --bump auto --branch devel
```

## Hotfixes

A fix that can't wait for the next release from `devel` goes to `master`:

1. Branch from `master` (it is the latest stable release), fix, and open a pull request into `master`; add the changelog entry under `## Unreleased` as usual.
2. Run the publish workflow from `master` with bump `auto` (`patch`: `1.2.0` → `1.2.1`).

`master` now has commits `devel` does not. The **Back-merge** workflow ([back-merge.yml](../.github/workflows/back-merge.yml)) runs on every push to `master` and merges `master` into `devel` (a merge commit, pushed straight to `devel`), then starts CI on it. After a release from `devel` there is nothing to merge and it ends at once.

Both sides change the release files, so they go through a merge driver, [`scripts/merge_release_files.py`](../scripts/merge_release_files.py), wired up in [.gitattributes](../.gitattributes):

- `CHANGELOG.md`: the `## Unreleased` entries are merged one bullet at a time. An entry `master` moved into its `## 1.2.1` section leaves `devel`'s `## Unreleased` too; `devel`'s own entries stay. Version sections only one side has are kept (`master`'s `## 1.2.1` goes below `devel`'s `## 1.3.0-Prerelease01`, above `## 1.2.0`).
- `Directory.Build.props`: the higher `<Version>` of the two is kept (`1.3.0-Prerelease01` over `1.2.1`).

Any other conflict (a hotfix touching code `devel` changed too) fails the run, and its summary lists the files and the commands for merging by hand. Push that merge straight to `devel`: a squash-merged pull request would leave `master` out of `devel`'s history. To use the driver for local merges too, set it up once per clone:

```bash
git config merge.skiaui-changelog.driver "python3 scripts/merge_release_files.py changelog %O %A %B"
git config merge.skiaui-version.driver "python3 scripts/merge_release_files.py version %O %A %B"
```

Without it, git merges these files by lines, as before.

## Setup

See [Development.md](../Development.md#continuous-integration-github-actions) for the `nuget.org` environment and Trusted Publishing.

The `master` ruleset requires pull requests and the `Build & test (Ubuntu)` check; only repository admins and deploy keys bypass it. The workflow therefore pushes its release commit and tag with a deploy key, not the workflow token. One-time setup:

```bash
ssh-keygen -t ed25519 -N "" -C "SkiaUi release" -f skiaui-release-key
gh repo deploy-key add skiaui-release-key.pub --allow-write --title "nuget-publish release push" -R rafalka/MauiSkiaUi
gh secret set RELEASE_DEPLOY_KEY --env nuget.org -R rafalka/MauiSkiaUi < skiaui-release-key
rm skiaui-release-key skiaui-release-key.pub
```

The key is a secret of the `nuget.org` environment, so only a publish run you approved can use it. Without it, the publish job stops before pushing anything. Unlike the workflow token, a push with the key starts other workflows: CI runs on the release commit, `nuget-pack.yml` packs the `v*` tag as artifacts (nothing is published), and the back-merge workflow checks `master` against `devel`.

The back-merge workflow pushes to `devel` with the workflow token: the `devel` ruleset blocks only deletion and force pushes, so it needs no key, and it starts CI on `devel` with `gh workflow run` (a token push starts no workflows).
