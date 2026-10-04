#!/usr/bin/env python3
"""Copy one CHANGELOG.md version section into release notes.

Headings must be exactly ``## <version>`` (see CHANGELOG.md). The next ``## `` heading ends the section.

``--format nuget`` (default) writes NuGet ``PackageReleaseNotes``, which nuget.org shows as plain text: a link to the
version's section, the titles of its ``### New features`` and its ``### Breaking changes``. Markdown is stripped.
A section without ``###`` subsections (versions up to 1.0.0-Prerelease07) is written whole, as plain text.

``--format markdown`` writes the whole section with a link to the changelog, for the GitHub release.
"""

from __future__ import annotations

import argparse
import re
from pathlib import Path

REPO_URL = "https://github.com/rafalka/MauiSkiaUi"
CHANGELOG_URL = f"{REPO_URL}/blob/master/CHANGELOG.md"

FEATURES = "New features"
BREAKING = "Breaking changes"
SUBSECTIONS = (FEATURES, BREAKING, "Fixes", "Other")

NOTHING_NEW = "No new features or breaking changes. Fixes and other changes are in the full release notes."


def extract(markdown: str, version: str) -> str:
    heading = f"## {version}"
    lines = markdown.splitlines()
    start = next((i + 1 for i, line in enumerate(lines) if line.strip() == heading), None)
    if start is None:
        raise SystemExit(f"No changelog section {heading!r}")
    body: list[str] = []
    for line in lines[start:]:
        if line.startswith("## "):
            break
        body.append(line)
    section = "\n".join(body).strip()
    if not section:
        raise SystemExit(f"Changelog section {heading!r} is empty")
    return section


def markdown_notes(section: str) -> str:
    return f"{section}\n\nFull changelog: {CHANGELOG_URL}\n"


def nuget_notes(section: str, version: str) -> str:
    link = f"Full release notes: {REPO_URL}/blob/v{version}/CHANGELOG.md#{github_anchor(version)}"
    groups = subsections(section)
    if groups is None:
        return f"{link}\n\n{plain_text(section)}\n"
    blocks = []
    features = [f"- {feature_title(item[0])}" for item in bullets(groups.get(FEATURES, []))]
    if features:
        blocks.append("\n".join([FEATURES, *features]))
    breaking = [breaking_text(item) for item in bullets(groups.get(BREAKING, []))]
    if breaking:
        blocks.append("\n".join([BREAKING, *breaking]))
    return "\n\n".join([link, *(blocks or [NOTHING_NEW])]) + "\n"


def github_anchor(heading: str) -> str:
    """GitHub's id for a Markdown heading: lowercase, punctuation other than '-' and '_' dropped, spaces to '-'."""
    return re.sub(r"[^\w\- ]", "", heading.strip().lower()).replace(" ", "-")


def subsections(section: str) -> dict[str, list[str]] | None:
    """The section's lines by ``### `` heading, or None when it has none (a section from before the subsections)."""
    groups: dict[str, list[str]] = {}
    current: list[str] | None = None
    preamble: list[str] = []
    for line in section.splitlines():
        if line.startswith("### "):
            name = line[4:].strip()
            if name not in SUBSECTIONS:
                raise SystemExit(f"Unknown changelog subsection {line!r}; use one of: {', '.join(SUBSECTIONS)}")
            if name in groups:
                raise SystemExit(f"Changelog subsection {line!r} appears twice in one version")
            current = groups[name] = []
        elif current is None:
            preamble.append(line)
        else:
            current.append(line)
    if not groups:
        return None
    if any(line.strip() for line in preamble):
        raise SystemExit("Changelog text before the first ### subsection would be dropped; move it into one")
    return groups


def bullets(lines: list[str]) -> list[list[str]]:
    """Top-level bullets, each as its lines (the bullet, then nested bullets and continuation lines)."""
    items: list[list[str]] = []
    for line in lines:
        if not line.strip():
            continue
        if line.startswith("- "):
            items.append([line])
        elif items:
            items[-1].append(line)
        else:
            raise SystemExit(f"Changelog line outside a bullet: {line!r}")
    return items


def feature_title(bullet: str) -> str:
    """A feature bullet's title: its text up to the first ': ' or '; ' outside code and parentheses."""
    text = bullet[2:].strip().replace(":**", "**:", 1)
    depth = 0
    in_code = False
    for i, ch in enumerate(text):
        if ch == "`":
            in_code = not in_code
        elif in_code:
            continue
        elif ch in "([":
            depth += 1
        elif ch in ")]":
            depth -= 1
        elif ch in ":;" and depth == 0 and text[i + 1:i + 2] in ("", " "):
            text = text[:i]
            break
    return plain_text(text).rstrip(" .,")


def breaking_text(item: list[str]) -> str:
    first = re.sub(r"^- \*\*Breaking(?: changes?)?:?\*\*:?\s*", "- ", item[0])
    return plain_text("\n".join([first, *item[1:]]))


def plain_text(markdown: str) -> str:
    # "([Doc.md](docs/Doc.md))" / "([A.md](…), [B.md](…))" pointers into the repo
    text = re.sub(r"\s*\((?:\[[^\]]*\]\([^)]*\)(?:,\s*|\s+and\s+)?)+\)", "", markdown)
    text = re.sub(r"\[([^\]]*)\]\([^)]*\)", r"\1", text)
    return text.replace("**", "").replace("__", "").replace("`", "")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("version")
    parser.add_argument("changelog", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--format", choices=("nuget", "markdown"), default="nuget")
    args = parser.parse_args()
    section = extract(args.changelog.read_text(encoding="utf-8"), args.version)
    notes = markdown_notes(section) if args.format == "markdown" else nuget_notes(section, args.version)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(notes, encoding="utf-8")
    print(f"Wrote {args.format} release notes for {args.version} ({len(notes)} chars) to {args.output}")


if __name__ == "__main__":
    main()
