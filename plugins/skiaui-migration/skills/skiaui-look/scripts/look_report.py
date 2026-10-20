#!/usr/bin/env python3
"""Summarize SkUiLookComparisonPage reports from app logs.

Reads lines containing "[SkUiLookComparison]" (as written by SkiaUi's look comparison page to the console, logcat or a
pasted "Copy report") from each file and prints one Markdown table per report: the rows whose sizes differ first, with the
differences in DIPs. Exits with 1 when any row differs, so it can gate a CI step.

Usage: look_report.py LOG [LOG ...]   ("-" reads standard input)
"""

import re
import sys

TAG = "[SkUiLookComparison]"
HEADER = re.compile(r"look=(?P<look>.*?) platform=(?P<platform>\S+) idiom=(?P<idiom>\S+) appStyles=(?P<styles>\S+) lookColors=(?P<colors>\S+)")
ROW = re.compile(r"(?P<name>.+?): native=(?P<nw>[\d.]+)x(?P<nh>[\d.]+) skiaui=(?P<sw>[\d.]+)x(?P<sh>[\d.]+) (?P<verdict>same|DIFFERS)")


def reports(lines):
    """Yields (header, rows) for each report; a later report replaces an earlier one of the same look and options."""
    header, rows = None, []
    for line in lines:
        if TAG not in line:
            continue
        text = line.split(TAG, 1)[1].strip()
        match = HEADER.search(text)
        if match:
            if header is not None:
                yield header, rows
            header, rows = match.groupdict(), []
            continue
        match = ROW.search(text)
        if match and header is not None:
            rows.append(match.groupdict())
    if header is not None:
        yield header, rows


def latest(lines):
    """The last report of each look, platform and options (the page reports again after every layout)."""
    result = {}
    for header, rows in reports(lines):
        result[(header["look"], header["platform"], header["styles"], header["colors"])] = (header, rows)
    return list(result.values())


def table(header, rows):
    out = [
        f"### {header['look']} on {header['platform']} ({header['idiom']}), app styles {header['styles']}, look colors {header['colors']}",
        "",
        "| Control | MAUI | SkiaUi | Δ width | Δ height | |",
        "| --- | --- | --- | --- | --- | --- |",
    ]
    ordered = sorted(rows, key=lambda row: row["verdict"] == "same")
    for row in ordered:
        dw = float(row["sw"]) - float(row["nw"])
        dh = float(row["sh"]) - float(row["nh"])
        mark = "" if row["verdict"] == "same" else "differs"
        out.append(f"| {row['name']} | {row['nw']} × {row['nh']} | {row['sw']} × {row['sh']} | {dw:+.1f} | {dh:+.1f} | {mark} |")
    same = sum(row["verdict"] == "same" for row in rows)
    out += ["", f"{same} of {len(rows)} rows match.", ""]
    return "\n".join(out)


def main(paths):
    if not paths:
        print(__doc__.strip(), file=sys.stderr)
        return 2
    differs = False
    for path in paths:
        with (sys.stdin if path == "-" else open(path, encoding="utf-8", errors="replace")) as stream:
            found = latest(stream)
        if not found:
            print(f"{path}: no {TAG} report found", file=sys.stderr)
            continue
        print(f"## {path}\n")
        for header, rows in found:
            print(table(header, rows))
            differs = differs or any(row["verdict"] != "same" for row in rows)
    return 1 if differs else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
