#!/usr/bin/env python3
"""Print the API check's baseline: the newest stable SkiaUi.Maui on nuget.org with the major version of the version
being packed, other than that version itself.

    python3 scripts/api_baseline.py 1.11.0      # -> 1.10.3 (while 1.11.0 is not published yet, and after)
    python3 scripts/api_baseline.py 2.0.0-Prerelease01   # -> nothing: a new major may break the API, so no check

The pack workflows pass it as -p:SkUiApiBaseline (Directory.Build.props), so a pack fails when it removes public API
that release has; an empty output turns the check off. scripts/api_compat.sh runs the same check locally against the
newest release tag.
"""
import json
import sys
import urllib.request

INDEX = "https://api.nuget.org/v3-flatcontainer/skiaui.maui/index.json"


def version_key(version: str) -> tuple[int, ...]:
    return tuple(int(part) for part in version.split("."))


def main() -> int:
    packing = sys.argv[1] if len(sys.argv) > 1 else ""
    major = packing.split(".", 1)[0] if packing else ""
    with urllib.request.urlopen(INDEX, timeout=60) as response:
        versions = json.load(response)["versions"]
    stable = [v for v in versions
              if "-" not in v and "+" not in v and v != packing and (not major or v.split(".", 1)[0] == major)]
    if not stable:
        print(f"No stable SkiaUi.Maui {major + '.x ' if major else ''}release on nuget.org: no API baseline.", file=sys.stderr)
        return 0
    print(max(stable, key=version_key))
    return 0


if __name__ == "__main__":
    sys.exit(main())
