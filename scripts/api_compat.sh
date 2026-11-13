#!/usr/bin/env bash
#
# API compatibility check of the four packages, with the SDK's package validation (ApiCompat) at pack time:
#   - nothing public from the baseline release is gone (the 1.x API promise; additions are fine),
#   - each framework keeps what the older one it is compatible with has (net11.0-ios has all of net10.0-ios's API).
# Each package is packed with -p:SkUiApiBaseline (Directory.Build.props), which downloads the baseline from nuget.org.
# A pack holds the TFMs its OS builds (macOS: net10.0 / net11.0, Android, iOS, Mac Catalyst; Windows: the Windows TFMs and
# the others it can build), so the baseline's frameworks it has no TFM for are skipped; CI runs this on both OSes.
# Packages land in artifacts/api-compat/ (gitignored). Exits non-zero on the first difference.
#
#   scripts/api_compat.sh                        # baseline: the newest release tag (v1.10.3 → 1.10.3)
#   scripts/api_compat.sh --baseline 1.10.0
#   scripts/api_compat.sh -p MauiSkiaUi          # one project
#   scripts/api_compat.sh --net 11               # one line (-p:SkUiNet; the in-package comparison then has nothing to pair)
#
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECTS=(MauiSkiaUi MauiSkiaUi.CommunityToolkit MauiSkiaUi.Markdown MauiSkiaUi.Markdown.Math)
BASELINE=""
NET=""
ONLY=""
OUT="$REPO_ROOT/artifacts/api-compat"

usage() {
    sed -n '3,15p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
    cat <<'EOF'
Options:
  -b, --baseline VERSION   released version to compare with (default: newest vX.Y.Z tag)
  -p, --project NAME       check one project (MauiSkiaUi, MauiSkiaUi.CommunityToolkit, MauiSkiaUi.Markdown, MauiSkiaUi.Markdown.Math)
      --net 10|11          limit the pack to one line
  -o, --out DIR            where the packages go (default: artifacts/api-compat)
  -h, --help
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        -b|--baseline) BASELINE="$2"; shift 2 ;;
        -p|--project) ONLY="$2"; shift 2 ;;
        --net) NET="$2"; shift 2 ;;
        -o|--out) OUT="$2"; shift 2 ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Unknown option: $1" >&2; usage >&2; exit 2 ;;
    esac
done

if [[ -z "$BASELINE" ]]; then
    BASELINE="$(git -C "$REPO_ROOT" tag --list 'v[0-9]*' --sort=-v:refname | grep -E '^v[0-9]+\.[0-9]+\.[0-9]+$' | head -1 | sed 's/^v//')"
    [[ -n "$BASELINE" ]] || { echo "No release tag found; pass --baseline." >&2; exit 2; }
fi
if [[ -n "$NET" && "$NET" != 10 && "$NET" != 11 ]]; then
    echo "--net takes 10 or 11" >&2; exit 2
fi
if [[ -n "$ONLY" ]]; then
    [[ -f "$REPO_ROOT/$ONLY/$ONLY.csproj" ]] || { echo "No project $ONLY" >&2; exit 2; }
    PROJECTS=("$ONLY")
fi

ARGS=(-c Release -o "$OUT" -v q -clp:ErrorsOnly -nologo "-p:SkUiApiBaseline=$BASELINE")
[[ -n "$NET" ]] && ARGS+=("-p:SkUiNet=$NET")

mkdir -p "$OUT"
echo "API check against $BASELINE${NET:+ (net$NET only)}"
for project in "${PROJECTS[@]}"; do
    echo "  $project"
    # The validation skips a package it already checked (a semaphore in obj): drop it so every run compares.
    find "$REPO_ROOT/$project/obj" -name 'Microsoft.NET.ApiCompat.ValidatePackage.semaphore' -delete 2>/dev/null || true
    if ! dotnet pack "$REPO_ROOT/$project/$project.csproj" "${ARGS[@]}"; then
        echo "API check failed for $project (against $BASELINE)." >&2
        exit 1
    fi
done
echo "No API differences: nothing removed since $BASELINE, and net11 keeps all of net10's API."
