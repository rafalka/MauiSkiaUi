#!/usr/bin/env python3
"""Write net10/SkiaUi.Net10.slnx from SkiaUi.slnx: the same projects through ../ paths, next to net10/global.json (SDK 10).

    python3 scripts/net10_solution.py          # rewrite it after a project is added to or removed from SkiaUi.slnx
    python3 scripts/net10_solution.py --check  # CI: exit 1 when it is out of date

IDEs take the SDK from the global.json next to the solution, so opening net10/SkiaUi.Net10.slnx uses SDK 10, and
Directory.Build.props then builds the .NET 10 line only (net10/README.md).
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "SkiaUi.slnx"
TARGET = ROOT / "net10" / "SkiaUi.Net10.slnx"
HEADER = "<!-- Generated from ../SkiaUi.slnx by scripts/net10_solution.py: edit SkiaUi.slnx and run the script. -->\n"


def expected() -> str:
    text = SOURCE.read_text(encoding="utf-8")
    return HEADER + re.sub(r'Path="(?!\.\./)', 'Path="../', text)


def main() -> int:
    content = expected()
    if "--check" in sys.argv[1:]:
        if not TARGET.exists() or TARGET.read_text(encoding="utf-8") != content:
            print(f"{TARGET.relative_to(ROOT)} is out of date: run python3 scripts/net10_solution.py", file=sys.stderr)
            return 1
        print(f"{TARGET.relative_to(ROOT)} matches SkiaUi.slnx")
        return 0
    TARGET.write_text(content, encoding="utf-8")
    print(f"wrote {TARGET.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
