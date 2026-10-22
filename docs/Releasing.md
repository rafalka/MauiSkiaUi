# Releasing SkiaUi.Maui

Every release publishes all of the repository's packages with one version: **SkiaUi.Maui** and **SkiaUi.Maui.CommunityToolkit** (which depends on SkiaUi.Maui of the same version). One changelog covers both. A new package added to the release loop of the pack and publish workflows needs, the first time, a nuget.org Trusted Publishing policy that covers it (or the push of its first version fails).

## Branches and versions

**Now (no stable release yet):** there is only `master`, and prereleases are published from it: `1.0.0-Prerelease04`, `1.0.0-Prerelease05`, …

**After the first stable release (`1.0.0`):**

| Branch | Holds | Versions |
| --- | --- | --- |
| `master` | Stable releases | `1.0.0`, `1.1.0`, `1.1.1`, … |
| `devel` | Active development | `1.1.0-Prerelease01`, `1.1.0-Prerelease02`, … |

When `devel` is merged into `master`, the versions on both branches are updated:
- `master` releases the stable version, e.g. `1.1.0-Prerelease03` → `1.1.0` with bump `release`;
- `devel` starts the next prerelease line, e.g. `1.2.0-Prerelease01` with bump `minor` (or `patch`).

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
| `auto` (default) | `release` on `master`, `prerelease` on `devel` (a choice's default can't depend on the branch, so the workflow picks) | Both |
| `prerelease` | `-PrereleaseNN` → `-Prerelease(NN+1)` | `devel`; `master` only before the first stable release |
| `release` | Drops the suffix: `1.1.0-Prerelease03` → `1.1.0` | `master` |
| `patch` / `minor` / `major` | Next `X.Y.Z`; on `devel`, its `-Prerelease01` | Both |
| `none` | Publishes the current, already tagged version again from its tag (e.g. after a failed publish) | Both |

The workflow then:

1. Computes the version with [`scripts/release_version.py`](../scripts/release_version.py). It fails if the version is already on nuget.org, or if its tag already exists. If nuget.org can't be reached, it warns and continues; the push itself would still refuse a duplicate.
2. Updates `Directory.Build.props` and `CHANGELOG.md` in the job's workspace, and packs that on macOS and Windows. The changelog section becomes `PackageReleaseNotes`.
3. After the `nuget.org` environment gate (where reviewers approve, if required), commits `Release <version>` on top of the dispatch commit and pushes it together with the tag `v<version>` (`git push --atomic`). If the branch moved since the run started, the push is rejected and nothing is published or tagged: run it again.
4. Pushes the package to nuget.org (Trusted Publishing), then creates the GitHub release `v<version>` with the notes and the package. Prereleases are marked as such.

**Dry run:** computes the version and packs, but doesn't commit, tag, push to nuget.org or create a release. The packages are uploaded as workflow artifacts.

**If a run fails after the release commit was pushed** (step 4, e.g. a nuget.org outage), run it again with bump `none`: it rebuilds the tagged release commit `v<version>`, even if the branch has moved on since, and publishes it without bumping again. `none` requires that tag.

Preview the next version locally:

```bash
scripts/release_version.py next --bump prerelease --branch master
```

## Setup

See [Development.md](../Development.md#continuous-integration-github-actions) for the `nuget.org` environment and Trusted Publishing.

The `master` ruleset requires pull requests and the `Build & test (Ubuntu)` check; only repository admins and deploy keys bypass it. The workflow therefore pushes its release commit and tag with a deploy key, not the workflow token. One-time setup:

```bash
ssh-keygen -t ed25519 -N "" -C "SkiaUi release" -f skiaui-release-key
gh repo deploy-key add skiaui-release-key.pub --allow-write --title "nuget-publish release push" -R rafalka/MauiSkiaUi
gh secret set RELEASE_DEPLOY_KEY --env nuget.org -R rafalka/MauiSkiaUi < skiaui-release-key
rm skiaui-release-key skiaui-release-key.pub
```

The key is a secret of the `nuget.org` environment, so only a publish run you approved can use it. Without it, the publish job stops before pushing anything. Unlike the workflow token, a push with the key starts other workflows: CI runs on the release commit, and `nuget-pack.yml` packs the `v*` tag as artifacts (nothing is published).
