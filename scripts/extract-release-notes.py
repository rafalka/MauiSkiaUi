#!/usr/bin/env python3
"""Copy one CHANGELOG.md version section for NuGet PackageReleaseNotes.

Headings must be exactly ``## <version>`` (see CHANGELOG.md). The next ``## `` heading
ends the section. A link to the changelog is appended so nuget.org can point at the full history.
"""

from __future__ import annotations

import sys
from pathlib import Path

CHANGELOG_URL = "https://github.com/rafalka/MauiSkiaUi/blob/master/CHANGELOG.md"


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
    notes = "\n".join(body).strip()
    if not notes:
        raise SystemExit(f"Changelog section {heading!r} is empty")
    return f"{notes}\n\nFull changelog: {CHANGELOG_URL}\n"


def main() -> None:
    if len(sys.argv) != 4:
        raise SystemExit(f"Usage: {Path(sys.argv[0]).name} <version> <CHANGELOG.md> <output.txt>")
    version, source, dest = sys.argv[1], Path(sys.argv[2]), Path(sys.argv[3])
    notes = extract(source.read_text(encoding="utf-8"), version)
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_text(notes, encoding="utf-8")
    print(f"Wrote release notes for {version} ({len(notes)} chars) to {dest}")


if __name__ == "__main__":
    main()
