# Releasing SkiaUi.Maui

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

Write entries under `## Unreleased` at the top of [CHANGELOG.md](../CHANGELOG.md). Releasing renames that section to the new version and opens a new empty `## Unreleased` above it, so nobody needs to know the next version number while working. A release with an empty `## Unreleased` is refused.

## Publishing

Run the **NuGet publish** workflow ([nuget-publish.yml](../.github/workflows/nuget-publish.yml)) from GitHub Actions. Choose the branch (`master` or `devel`) in "Use workflow from", and pick a **bump**:

| Bump | Effect | Allowed on |
| --- | --- | --- |
| `prerelease` | `-PrereleaseNN` → `-Prerelease(NN+1)` | `devel`; `master` only before the first stable release |
| `release` | Drops the suffix: `1.1.0-Prerelease03` → `1.1.0` | `master` |
| `patch` / `minor` / `major` | Next `X.Y.Z`; on `devel`, its `-Prerelease01` | Both |
| `none` | Publishes the current version again (e.g. after a failed publish) | Both |

The workflow then:

1. Computes the version with [`scripts/release_version.py`](../scripts/release_version.py). It fails if the version is already on nuget.org, or if its tag already exists.
2. Updates `Directory.Build.props` and `CHANGELOG.md`, commits `Release <version>` to the branch, and tags it `v<version>`.
3. Packs that commit on macOS and Windows and merges the packages. The changelog section becomes `PackageReleaseNotes`.
4. Pushes the package to nuget.org (Trusted Publishing), then creates the GitHub release `v<version>` with the notes and the package. Prereleases are marked as such.

**Dry run:** computes the version and packs, but doesn't commit, tag, push to nuget.org or create a release. The packages are uploaded as workflow artifacts.

**If a run fails after the release commit was pushed** (e.g. while packing), run it again with bump `none`: it publishes the committed version without bumping again.

Preview the next version locally:

```bash
scripts/release_version.py next --bump prerelease --branch master
```

**Setup:** see [Development.md](../Development.md#continuous-integration-github-actions) for the `nuget.org` environment and Trusted Publishing. The workflow pushes its release commit with the workflow token. If `master` or `devel` becomes a protected branch, allow GitHub Actions to push to it.
