#!/usr/bin/env python3
"""Compute and apply the next SkiaUi.Maui version (docs/Releasing.md).

  release_version.py next  --bump prerelease --branch master   # prints the next version
  release_version.py apply --version 1.0.0-Prerelease06         # Directory.Build.props + CHANGELOG.md

Versions are ``X.Y.Z`` (stable) or ``X.Y.Z-PrereleaseNN`` (two-digit NN). Bumps:

  prerelease  X.Y.Z-PrereleaseNN -> X.Y.Z-Prerelease(NN+1)
  release     X.Y.Z-PrereleaseNN -> X.Y.Z
  patch/minor/major               -> next X.Y.Z; on devel, its -Prerelease01
  none        publish the current version again (e.g. retry a failed publish)

Branch rules: devel publishes prereleases only; master publishes stable versions only, except while no stable
version exists yet (current version still a prerelease), when prereleases come from master too.
``apply`` renames CHANGELOG.md's ``## Unreleased`` section to the version (refusing an empty one) and opens a new
empty ``## Unreleased`` above it.
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PROPS = ROOT / "Directory.Build.props"
CHANGELOG = ROOT / "CHANGELOG.md"
VERSION_RE = re.compile(r"^(\d+)\.(\d+)\.(\d+)(?:-Prerelease(\d+))?$")
PROPS_RE = re.compile(r"<Version>([^<]*)</Version>")
UNRELEASED = "## Unreleased"


def fail(message: str) -> None:
    raise SystemExit(f"error: {message}")


def parse(version: str) -> tuple[int, int, int, int | None]:
    match = VERSION_RE.match(version)
    if not match:
        fail(f"version {version!r} is not X.Y.Z or X.Y.Z-PrereleaseNN")
    major, minor, patch, pre = match.groups()
    return int(major), int(minor), int(patch), int(pre) if pre is not None else None


def fmt(major: int, minor: int, patch: int, pre: int | None) -> str:
    return f"{major}.{minor}.{patch}" + (f"-Prerelease{pre:02d}" if pre is not None else "")


def current_version() -> str:
    match = PROPS_RE.search(PROPS.read_text(encoding="utf-8"))
    if not match:
        fail(f"no <Version> in {PROPS}")
    return match.group(1).strip()


def next_version(current: str, bump: str, branch: str) -> str:
    major, minor, patch, pre = parse(current)
    if branch not in ("master", "devel"):
        fail(f"releases are published from master or devel, not {branch!r}")
    if bump == "none":
        new = current
    elif bump == "prerelease":
        if pre is None:
            fail(f"{current} is stable: start a prerelease line with patch / minor / major (on devel)")
        new = fmt(major, minor, patch, pre + 1)
    elif bump == "release":
        if pre is None:
            fail(f"{current} is already stable: use patch / minor / major")
        new = fmt(major, minor, patch, None)
    elif bump in ("patch", "minor", "major"):
        if bump == "major":
            major, minor, patch = major + 1, 0, 0
        elif bump == "minor":
            minor, patch = minor + 1, 0
        else:
            patch += 1
        new = fmt(major, minor, patch, 1 if branch == "devel" else None)
    else:
        fail(f"unknown bump {bump!r}")

    new_pre = parse(new)[3]
    if branch == "devel" and new_pre is None:
        fail(f"devel publishes prereleases only ({new} is stable); merge to master to release it")
    if branch == "master" and new_pre is not None and pre is None:
        fail(f"master publishes stable versions only ({new} is a prerelease); publish prereleases from devel")
    return new


def apply(version: str) -> None:
    parse(version)
    props = PROPS.read_text(encoding="utf-8")
    PROPS.write_text(PROPS_RE.sub(f"<Version>{version}</Version>", props, count=1), encoding="utf-8")

    lines = CHANGELOG.read_text(encoding="utf-8").splitlines()
    if any(line.strip() == f"## {version}" for line in lines):
        fail(f"CHANGELOG.md already has a '## {version}' section")
    try:
        start = next(i for i, line in enumerate(lines) if line.strip() == UNRELEASED)
    except StopIteration:
        fail(f"CHANGELOG.md has no '{UNRELEASED}' section")
    end = next((i for i in range(start + 1, len(lines)) if lines[i].startswith("## ")), len(lines))
    if not any(line.strip() for line in lines[start + 1:end]):
        fail(f"the '{UNRELEASED}' section of CHANGELOG.md is empty: nothing to release")
    lines[start:start + 1] = [UNRELEASED, "", f"## {version}"]
    CHANGELOG.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)
    nxt = sub.add_parser("next", help="print the next version")
    nxt.add_argument("--bump", required=True, choices=["prerelease", "release", "patch", "minor", "major", "none"])
    nxt.add_argument("--branch", required=True)
    nxt.add_argument("--current", help="current version (default: Directory.Build.props)")
    app = sub.add_parser("apply", help="write the version to Directory.Build.props and CHANGELOG.md")
    app.add_argument("--version", required=True)
    args = parser.parse_args()
    if args.command == "next":
        print(next_version(args.current or current_version(), args.bump, args.branch))
    else:
        apply(args.version)
        print(f"applied {args.version}", file=sys.stderr)


if __name__ == "__main__":
    main()
