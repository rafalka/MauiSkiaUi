#!/usr/bin/env python3
"""SkiaUi benchmark results: collect device logs into JSON and compare result files.

  bench_compare.py collect OUT.json --runner android --label current LOG [LOG ...]
      Parse "SKUIBENCH {json}" lines (device logcat / console output) into a results file.
  bench_compare.py merge OUT.json IN.json [IN.json ...]
      Concatenate the samples of several results files (e.g. alternating rounds).
  bench_compare.py show RESULTS.json
      Median table of one results file.
  bench_compare.py BASELINE.json CURRENT.json [--threshold 5] [--min-ms 0.25] [--markdown] [--json]
      Median per scenario / metric, delta %, and a verdict:
        faster / slower  the medians differ by more than --threshold % (and, for timings, by at least --min-ms)
                         AND the interquartile ranges do not overlap
        ~                within noise
      Exit code is 0; scripts and agents read the table (or --json for machine output).

See docs/design/Benchmarks.md.
"""
import json
import re
import statistics
import sys
from datetime import datetime, timezone

# Metric key in the sample → (column name, lower is better).
METRICS = [
    ("Generate", "generate", True),
    ("Add", "add", True),
    ("Measure", "measure", True),
    ("Arrange", "arrange", True),
    ("Record", "record", True),
    ("Composite", "composite", True),
    ("FirstFrame", "firstFrame", True),
    ("Update", "update", True),
    ("MotionFps", "motionFps", False),
    ("MotionAvgRenderMs", "motionAvgRenderMs", True),
    ("MotionMaxRenderMs", "motionMaxRenderMs", True),
    ("MotionUiFps", "motionUiFps", False),
    ("MotionAvgUiMs", "motionAvgUiMs", True),
    ("MotionMaxUiMs", "motionMaxUiMs", True),
    ("AllocatedBytes", "allocKB", True),
]

LINE = re.compile(r"SKUIBENCH (\{.*\})\s*$")


def load(path):
    with open(path, encoding="utf-8") as handle:
        return json.load(handle)


def values(samples, scenario, key):
    result = []
    for sample in samples:
        if sample.get("Scenario") != scenario:
            continue
        value = sample.get(key)
        if value is None:
            continue
        result.append(value / 1024.0 if key == "AllocatedBytes" else float(value))
    return sorted(result)


def quartiles(sorted_values):
    if len(sorted_values) < 4:
        return sorted_values[0], sorted_values[-1]
    q = statistics.quantiles(sorted_values, n=4)
    return q[0], q[2]


def scenarios_of(*documents):
    seen = []
    for document in documents:
        for sample in document["samples"]:
            if sample["Scenario"] not in seen:
                seen.append(sample["Scenario"])
    return seen


def collect(out, runner, label, logs):
    samples = []
    for log in logs:
        with open(log, encoding="utf-8", errors="replace") as handle:
            for line in handle:
                match = LINE.search(line)
                if match:
                    samples.append(json.loads(match.group(1)))
    if not samples:
        sys.exit(f"no SKUIBENCH samples found in {', '.join(logs)}")
    write(out, runner, label, samples)
    print(f"{out}: {len(samples)} samples")


def merge(out, inputs):
    documents = [load(path) for path in inputs]
    samples = [sample for document in documents for sample in document["samples"]]
    write(out, documents[0].get("runner", "?"), documents[0].get("label", "?"), samples)


def write(out, runner, label, samples):
    document = {
        "runner": runner,
        "label": label,
        "created": datetime.now(timezone.utc).isoformat(),
        "samples": samples,
    }
    with open(out, "w", encoding="utf-8") as handle:
        json.dump(document, handle, indent=2)


def show(path):
    document = load(path)
    samples = document["samples"]
    print(f"# {document.get('label')} ({document.get('runner')})")
    for scenario in scenarios_of(document):
        cells = []
        for key, name, _ in METRICS:
            data = values(samples, scenario, key)
            if data:
                cells.append(f"{name}={statistics.median(data):.1f}")
        runs = sum(1 for s in samples if s["Scenario"] == scenario)
        print(f"{scenario:<22} n={runs:<3} " + "  ".join(cells))


def compare(baseline_path, current_path, threshold, min_ms, markdown, as_json):
    baseline, current = load(baseline_path), load(current_path)
    rows = []
    for scenario in scenarios_of(baseline, current):
        for key, name, lower_is_better in METRICS:
            a = values(baseline["samples"], scenario, key)
            b = values(current["samples"], scenario, key)
            if not a or not b:
                continue
            ma, mb = statistics.median(a), statistics.median(b)
            delta = (mb - ma) / ma * 100 if ma else 0.0
            a_lo, a_hi = quartiles(a)
            b_lo, b_hi = quartiles(b)
            separated = b_lo > a_hi or b_hi < a_lo
            verdict = "~"
            timing = key not in ("MotionFps", "AllocatedBytes")
            if separated and abs(delta) >= threshold and (not timing or abs(mb - ma) >= min_ms):
                improved = (delta < 0) == lower_is_better
                verdict = "faster" if improved else "slower"
            rows.append({"scenario": scenario, "metric": name, "baseline": round(ma, 2), "current": round(mb, 2),
                         "deltaPercent": round(delta, 1), "verdict": verdict, "n": [len(a), len(b)]})

    if as_json:
        print(json.dumps(rows, indent=2))
        return
    title = f"{baseline.get('label')} → {current.get('label')} ({current.get('runner')}, threshold {threshold:g}%)"
    if markdown:
        print(f"**{title}**\n")
        print("| scenario | metric | baseline | current | Δ % | verdict |")
        print("|---|---|---:|---:|---:|---|")
        for r in rows:
            print(f"| {r['scenario']} | {r['metric']} | {r['baseline']:.1f} | {r['current']:.1f} | {r['deltaPercent']:+.1f} | {r['verdict']} |")
    else:
        print(title)
        print(f"{'scenario':<22} {'metric':<18} {'baseline':>9} {'current':>9} {'Δ %':>7}  verdict")
        for r in rows:
            print(f"{r['scenario']:<22} {r['metric']:<18} {r['baseline']:>9.1f} {r['current']:>9.1f} {r['deltaPercent']:>+7.1f}  {r['verdict']}")
    slower = [r for r in rows if r["verdict"] == "slower"]
    faster = [r for r in rows if r["verdict"] == "faster"]
    print(f"\n{len(faster)} faster, {len(slower)} slower, {len(rows) - len(faster) - len(slower)} within noise")


def main(argv):
    if not argv or argv[0] in ("-h", "--help"):
        print(__doc__)
        return
    command = argv[0]
    if command == "collect":
        out, rest = argv[1], argv[2:]
        runner, label, logs = "device", "current", []
        index = 0
        while index < len(rest):
            if rest[index] == "--runner":
                runner = rest[index + 1]; index += 2
            elif rest[index] == "--label":
                label = rest[index + 1]; index += 2
            else:
                logs.append(rest[index]); index += 1
        collect(out, runner, label, logs)
    elif command == "merge":
        merge(argv[1], argv[2:])
    elif command == "show":
        show(argv[1])
    else:
        threshold, min_ms, markdown, as_json, files = 5.0, 0.25, False, False, []
        index = 0
        while index < len(argv):
            if argv[index] == "--threshold":
                threshold = float(argv[index + 1]); index += 2
            elif argv[index] == "--min-ms":
                min_ms = float(argv[index + 1]); index += 2
            elif argv[index] == "--markdown":
                markdown = True; index += 1
            elif argv[index] == "--json":
                as_json = True; index += 1
            else:
                files.append(argv[index]); index += 1
        if len(files) != 2:
            sys.exit("usage: bench_compare.py BASELINE.json CURRENT.json [--threshold 5] [--min-ms 0.25] [--markdown] [--json]")
        compare(files[0], files[1], threshold, min_ms, markdown, as_json)


if __name__ == "__main__":
    main(sys.argv[1:])
