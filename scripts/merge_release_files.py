#!/usr/bin/env python3
"""Git merge driver for the files every release changes (docs/Releasing.md#hotfixes).

  merge_release_files.py changelog %O %A %B   # CHANGELOG.md
  merge_release_files.py version   %O %A %B   # Directory.Build.props

Git runs it with the merge base (%O), our side (%A, also where the result goes) and their side (%B). It is wired up
in .gitattributes and enabled per clone (the back-merge workflow does it on its runner):

  git config merge.skiaui-changelog.driver "python3 scripts/merge_release_files.py changelog %O %A %B"
  git config merge.skiaui-version.driver "python3 scripts/merge_release_files.py version %O %A %B"

Merging master into devel after a hotfix release, both sides changed the same lines on purpose:

- ``changelog``: master moved its ``## Unreleased`` entries into a version section, devel added entries of its own.
  The ``## Unreleased`` entries are merged one bullet (with its nested lines) at a time, three-way, per subsection; a
  version section only one side has is kept where that side has it. Sections both sides changed differently, and a
  changed preamble on both sides, are left to git's line merge.
- ``version``: each side bumped ``<Version>``. The higher version is kept (a prerelease is below its stable version),
  and the rest of the file is merged by lines.

Exit 0 with the merged file in %A; otherwise git's conflict markers are in %A and the exit code is non-zero.
"""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path

from release_version import PROPS_RE, UNRELEASED, parse

SUBSECTIONS = ["### New features", "### Breaking changes", "### Fixes", "### Other"]


class Unmergeable(Exception):
    pass


def read(path: str) -> str:
    return Path(path).read_text(encoding="utf-8")


def line_merge(base: str, ours: str, theirs: str, result: str) -> int:
    """git's own three-way line merge of the given texts into ``result``; returns the number of conflicts."""
    scratch = Path(result).parent
    paths = []
    for name, text in (("ours", ours), ("base", base), ("theirs", theirs)):
        path = scratch / f".merge-release-files.{name}"
        path.write_text(text, encoding="utf-8")
        paths.append(str(path))
    try:
        merged = subprocess.run(["git", "merge-file", "-p", "-L", "ours", "-L", "base", "-L", "theirs", *paths],
                                capture_output=True, text=True, encoding="utf-8")
        if merged.returncode < 0 or merged.returncode > 127:
            raise SystemExit(f"error: git merge-file failed: {merged.stderr.strip()}")
        Path(result).write_text(merged.stdout, encoding="utf-8")
        return merged.returncode
    finally:
        for path in paths:
            Path(path).unlink(missing_ok=True)


def pick(base, ours, theirs):
    """The three-way result of one unit: the side that changed it, or raises when both changed it differently."""
    if ours == theirs or theirs == base:
        return ours
    if ours == base:
        return theirs
    raise Unmergeable


# --- changelog ---------------------------------------------------------------------------------------------------

def split_sections(text: str) -> tuple[list[str], list[tuple[str, list[str]]]]:
    """The preamble lines and the ``## `` sections (heading, body lines) in order."""
    lines = text.splitlines()
    preamble: list[str] = []
    sections: list[tuple[str, list[str]]] = []
    for line in lines:
        if line.startswith("## "):
            sections.append((line.strip(), []))
        elif sections:
            sections[-1][1].append(line)
        else:
            preamble.append(line)
    headings = [heading for heading, _ in sections]
    if len(set(headings)) != len(headings) or UNRELEASED not in headings:
        raise Unmergeable
    return preamble, sections


def split_entries(body: list[str]) -> dict[str, list[str]]:
    """Unreleased entries by subsection ("" before the first one): a top-level line with its indented lines."""
    groups: dict[str, list[str]] = {"": []}
    current = ""
    pending_blank = False
    for line in body:
        if line.startswith("### "):
            current = line.strip()
            if current in groups:
                raise Unmergeable
            groups[current] = []
            pending_blank = False
        elif not line.strip():
            pending_blank = True
        elif line[0] in " \t" and groups[current]:
            groups[current][-1] += ("\n" if pending_blank else "") + "\n" + line
            pending_blank = False
        else:
            groups[current].append(line)
            pending_blank = False
    return groups


def merge_entries(base: list[str], ours: list[str], theirs: list[str]) -> list[str]:
    """Ours in order, without what theirs removed since the base, then what theirs added."""
    removed = set(base) - set(theirs)
    added = [entry for entry in theirs if entry not in base and entry not in ours]
    return [entry for entry in ours if entry not in removed] + added


def merge_unreleased(base: list[str], ours: list[str], theirs: list[str]) -> list[str]:
    if ours == theirs or theirs == base:
        return ours
    if ours == base:
        return theirs
    groups = [split_entries(body) for body in (base, ours, theirs)]
    names = [name for name in ["", *SUBSECTIONS] if any(name in group for group in groups)]
    names += [name for group in groups for name in group if name not in names]
    body = [""]
    for name in names:
        entries = merge_entries(*(group.get(name, []) for group in groups))
        if not entries:
            continue
        if name:
            body += [name, ""]
        body += "\n".join(entries).split("\n") + [""]
    return body


def merge_order(ours: list[str], theirs: list[str]) -> list[str]:
    """Our headings in order, each heading only theirs has placed before the next heading both have."""
    result = list(ours)
    for i, heading in enumerate(theirs):
        if heading in result:
            continue
        following = next((h for h in theirs[i + 1:] if h in ours), None)
        result.insert(result.index(following) if following else len(result), heading)
    return result


def merge_changelog(base: str, ours: str, theirs: str) -> str:
    (base_pre, base_secs), (our_pre, our_secs), (their_pre, their_secs) = (
        split_sections(text) for text in (base, ours, theirs))
    base_map, our_map, their_map = dict(base_secs), dict(our_secs), dict(their_secs)
    lines = pick(base_pre, our_pre, their_pre)
    for heading in merge_order([h for h, _ in our_secs], [h for h, _ in their_secs]):
        if heading == UNRELEASED:
            body = merge_unreleased(base_map.get(heading, []), our_map[heading], their_map[heading])
        elif heading in our_map and heading in their_map:
            body = pick(base_map.get(heading), our_map[heading], their_map[heading])
        elif heading in base_map:
            raise Unmergeable  # a version section one side deleted
        else:
            body = our_map.get(heading, their_map.get(heading))
        lines += [heading, *body]
    return "\n".join(lines) + "\n"


# --- version -----------------------------------------------------------------------------------------------------

def version_key(version: str) -> tuple[int, int, int, int]:
    major, minor, patch, pre = parse(version)
    return major, minor, patch, pre if pre is not None else sys.maxsize


def merge_version(base: str, ours: str, theirs: str) -> tuple[str, str, str]:
    """The three texts with the higher of the two sides' ``<Version>`` in all of them."""
    versions = [PROPS_RE.search(text) for text in (ours, theirs)]
    if not all(versions):
        return base, ours, theirs
    keep = max((match.group(1).strip() for match in versions), key=version_key)
    return tuple(PROPS_RE.sub(f"<Version>{keep}</Version>", text, count=1) for text in (base, ours, theirs))


def main() -> int:
    if len(sys.argv) != 5 or sys.argv[1] not in ("changelog", "version"):
        print(__doc__, file=sys.stderr)
        return 2
    kind, base_path, our_path, their_path = sys.argv[1:]
    base, ours, theirs = read(base_path), read(our_path), read(their_path)
    if kind == "changelog":
        try:
            Path(our_path).write_text(merge_changelog(base, ours, theirs), encoding="utf-8")
            return 0
        except Unmergeable:
            pass
    else:
        base, ours, theirs = merge_version(base, ours, theirs)
    return min(line_merge(base, ours, theirs, our_path), 1)


if __name__ == "__main__":
    sys.exit(main())
