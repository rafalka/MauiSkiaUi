#!/usr/bin/env bash
#
# Run SkiaUi benchmarks (docs/design/Benchmarks.md) on the working tree and optionally on a baseline git ref,
# then compare. Results: artifacts/bench/<timestamp>/{current,baseline}.json + compare.txt.
#
#   scripts/bench.sh                                   # headless, all scenarios, working tree only
#   scripts/bench.sh --baseline HEAD                   # headless: uncommitted changes vs last commit
#   scripts/bench.sh -t android -s 2299011508047ece --baseline master -S core-labels,skui-labels
#   scripts/bench.sh -t ios -s <simulator-udid> -n 8
#   scripts/bench.sh -t maccatalyst
#
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
APP_ID="com.rkdevel.skiauibench"
ACTIVITY="$APP_ID/skiauibench.MainActivity"

TARGET="headless"
DEVICE=""
RUNS=6
WARMUP=1
SCENARIOS=""
BASELINE=""
ROUNDS=1
THRESHOLD=5
OUT=""
TIMEOUT=900

usage() {
    cat <<'EOF'
Usage: scripts/bench.sh [options]

  -t TARGET        headless (default) | android | ios | maccatalyst
  -s DEVICE        adb serial (android) or simulator / device UDID (ios); default: the only connected one
  -n RUNS          measured iterations per scenario (default 6)
  -w WARMUP        warm-up iterations per scenario (default 1)
  -S LIST          comma separated scenario names (default: all; headless skips [device] scenarios)
  -b, --baseline REF
                   also benchmark git REF (e.g. HEAD, master, a sha) in a worktree with the current
                   benchmarks/ folder copied in, then compare REF → working tree
  -r ROUNDS        alternate baseline/current this many times and pool the samples (default 1)
  --threshold PCT  minimum median change reported as faster/slower (default 5)
  -o DIR           output directory (default artifacts/bench/<timestamp>)
  -l, --list       list scenarios and exit
  -h, --help       this help
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        -t) TARGET="$2"; shift 2 ;;
        -s) DEVICE="$2"; shift 2 ;;
        -n) RUNS="$2"; shift 2 ;;
        -w) WARMUP="$2"; shift 2 ;;
        -S) SCENARIOS="$2"; shift 2 ;;
        -b|--baseline) BASELINE="$2"; shift 2 ;;
        -r) ROUNDS="$2"; shift 2 ;;
        --threshold) THRESHOLD="$2"; shift 2 ;;
        -o) OUT="$2"; shift 2 ;;
        -l|--list) exec dotnet run -c Release --project "$REPO_ROOT/benchmarks/MauiSkiaUi.Benchmarks" -- --list ;;
        -h|--help) usage; exit 0 ;;
        *) echo "unknown option $1" >&2; usage; exit 2 ;;
    esac
done

OUT="${OUT:-$REPO_ROOT/artifacts/bench/$(date +%Y%m%d-%H%M%S)}"
mkdir -p "$OUT"
log() { echo "[bench] $*" >&2; }
# macOS without coreutils has no `timeout`; run unbounded there.
command -v timeout >/dev/null || timeout() { shift; "$@"; }

# ---- sources -------------------------------------------------------------------------------------------------------

source_dir() { # label → repo directory to build
    if [[ "$1" == "current" ]]; then echo "$REPO_ROOT"; return; fi
    local sha dir
    sha="$(git -C "$REPO_ROOT" rev-parse --short "$BASELINE")"
    dir="$REPO_ROOT/artifacts/bench/worktrees/$sha"
    if [[ ! -d "$dir" ]]; then
        log "creating baseline worktree $BASELINE ($sha)"
        git -C "$REPO_ROOT" worktree add --detach "$dir" "$sha" >/dev/null 2>&1
    fi
    # Same scenarios / runners on both sides: overlay the working tree's benchmarks folder.
    rsync -a --delete --exclude bin --exclude obj "$REPO_ROOT/benchmarks/" "$dir/benchmarks/"
    echo "$dir"
}

# ---- device helpers ------------------------------------------------------------------------------------------------

pick_android() {
    [[ -n "$DEVICE" ]] && return
    DEVICE="$(adb devices | awk 'NR>1 && $2=="device" {print $1}' | head -1)"
    [[ -n "$DEVICE" ]] || { echo "no adb device" >&2; exit 1; }
}

ios_is_simulator() { xcrun simctl list devices | grep -q "$DEVICE"; }

pick_ios() {
    [[ -n "$DEVICE" ]] && return
    DEVICE="$(xcrun simctl list devices booted | grep -Eo '[0-9A-F-]{36}' | head -1)"
    [[ -n "$DEVICE" ]] || { echo "no booted simulator; pass -s UDID" >&2; exit 1; }
}

app_args() { # label → launch arguments for the bench app / headless runner
    local args=(--runs "$RUNS" --warmup "$WARMUP" --label "$1")
    [[ -n "$SCENARIOS" ]] && args+=(--scenarios "$SCENARIOS")
    echo "${args[@]}"
}

# ---- build + run ---------------------------------------------------------------------------------------------------

BUILT=" "

build() { # label dir
    local label="$1" dir="$2" project="$2/benchmarks/MauiSkiaUiBench/MauiSkiaUiBench.csproj"
    [[ "$BUILT" == *" $label "* ]] && return
    log "building $label ($TARGET)"
    case "$TARGET" in
        headless) dotnet build -c Release "$dir/benchmarks/MauiSkiaUi.Benchmarks" -v q -nologo >"$OUT/$label.build.log" 2>&1 ;;
        android) dotnet build -c Release -f net10.0-android -t:SignAndroidPackage "$project" -v q -nologo >"$OUT/$label.build.log" 2>&1 ;;
        ios)
            # Incremental iOS (AOT) builds after a library change can crash at launch ("Failed to load AOT module"):
            # always build the bench app clean.
            rm -rf "$dir/benchmarks/MauiSkiaUiBench/bin/Release/net10.0-ios" "$dir/benchmarks/MauiSkiaUiBench/obj/Release/net10.0-ios"
            local rid=ios-arm64
            ios_is_simulator && rid=iossimulator-arm64
            dotnet build -c Release -f net10.0-ios -p:RuntimeIdentifier=$rid "$project" -v q -nologo >"$OUT/$label.build.log" 2>&1 ;;
        maccatalyst) dotnet build -c Release -f net10.0-maccatalyst "$project" -v q -nologo >"$OUT/$label.build.log" 2>&1 ;;
    esac || { tail -30 "$OUT/$label.build.log" >&2; echo "build of $label failed ($OUT/$label.build.log)" >&2; exit 1; }
    BUILT="$BUILT$label "
}

run_android() { # label dir round
    local label="$1" dir="$2" round="$3" apk logfile="$OUT/$1.round$3.log"
    apk="$(ls "$dir"/benchmarks/MauiSkiaUiBench/bin/Release/net10.0-android/*-Signed.apk | head -1)"
    adb -s "$DEVICE" install -r "$apk" >/dev/null
    adb -s "$DEVICE" shell am force-stop "$APP_ID"
    adb -s "$DEVICE" logcat -c
    local extras=(--ez autorun true --ei runs "$RUNS" --ei warmup "$WARMUP" --es label "$label")
    [[ -n "$SCENARIOS" ]] && extras+=(--es scenarios "$SCENARIOS")
    adb -s "$DEVICE" shell am start -n "$ACTIVITY" "${extras[@]}" >/dev/null
    local waited=0
    until adb -s "$DEVICE" logcat -d | grep -q "SKUIBENCH_DONE"; do
        sleep 2; waited=$((waited + 2))
        if [[ $waited -ge $TIMEOUT ]]; then log "timeout waiting for $label"; break; fi
        if [[ $waited -ge 20 ]] && ! adb -s "$DEVICE" shell pidof "$APP_ID" >/dev/null; then log "$label: app is not running (crash?)"; break; fi
    done
    adb -s "$DEVICE" logcat -d | grep "SKUIBENCH" | sed 's/.*SKUIBENCH/SKUIBENCH/' >"$logfile"
    adb -s "$DEVICE" shell am force-stop "$APP_ID"
    echo "$logfile"
}

run_ios() { # label dir round
    local label="$1" dir="$2" round="$3" logfile="$OUT/$1.round$3.log" app
    read -r -a args <<<"$(app_args "$label")"
    if ios_is_simulator; then
        app="$(ls -d "$dir"/benchmarks/MauiSkiaUiBench/bin/Release/net10.0-ios/iossimulator-arm64/*.app | head -1)"
        xcrun simctl install "$DEVICE" "$app"
        timeout "$TIMEOUT" xcrun simctl launch --console --terminate-running-process "$DEVICE" "$APP_ID" --autorun --exit "${args[@]}" 2>&1 \
            | grep "SKUIBENCH" | sed 's/.*SKUIBENCH/SKUIBENCH/' >"$logfile" || true
    else
        app="$(ls -d "$dir"/benchmarks/MauiSkiaUiBench/bin/Release/net10.0-ios/ios-arm64/*.app | head -1)"
        xcrun devicectl device install app --device "$DEVICE" "$app" >/dev/null
        timeout "$TIMEOUT" xcrun devicectl device process launch --console --terminate-existing --device "$DEVICE" "$APP_ID" --autorun --exit "${args[@]}" 2>&1 \
            | grep "SKUIBENCH" | sed 's/.*SKUIBENCH/SKUIBENCH/' >"$logfile" || true
    fi
    echo "$logfile"
}

run_maccatalyst() { # label dir round
    local label="$1" dir="$2" round="$3" logfile="$OUT/$1.round$3.log" binary
    read -r -a args <<<"$(app_args "$label")"
    binary="$(ls "$dir"/benchmarks/MauiSkiaUiBench/bin/Release/net10.0-maccatalyst/maccatalyst-"$(uname -m | sed 's/x86_64/x64/')"/*.app/Contents/MacOS/MauiSkiaUiBench | head -1)"
    timeout "$TIMEOUT" "$binary" --autorun --exit "${args[@]}" 2>&1 | grep "SKUIBENCH" | sed 's/.*SKUIBENCH/SKUIBENCH/' >"$logfile" || true
    echo "$logfile"
}

run_headless() { # label dir round
    local label="$1" dir="$2" round="$3" json="$OUT/$1.round$3.json"
    read -r -a args <<<"$(app_args "$label")"
    # Fully optimized JIT from the first iteration: tiering otherwise makes early samples order dependent.
    DOTNET_TieredCompilation=0 DOTNET_TieredPGO=0 dotnet run -c Release --no-build --project "$dir/benchmarks/MauiSkiaUi.Benchmarks" -- "${args[@]}" --json "$json" >"$OUT/$label.round$round.log"
    echo "$json"
}

RESULTS_baseline=""
RESULTS_current=""

run_side() { # label round
    local label="$1" round="$2" dir produced
    dir="$(source_dir "$label")"
    build "$label" "$dir"
    log "running $label (round $round)"
    case "$TARGET" in
        headless) produced="$(run_headless "$label" "$dir" "$round")" ;;
        android) produced="$(run_android "$label" "$dir" "$round")" ;;
        ios) produced="$(run_ios "$label" "$dir" "$round")" ;;
        maccatalyst) produced="$(run_maccatalyst "$label" "$dir" "$round")" ;;
    esac
    if [[ "$produced" == *.log ]]; then
        grep -E "SKUIBENCH_(WARN|ERROR)" "$produced" >&2 || true
        python3 "$REPO_ROOT/scripts/bench_compare.py" collect "${produced%.log}.json" --runner "$TARGET" --label "$label" "$produced" >/dev/null
        produced="${produced%.log}.json"
    fi
    eval "RESULTS_$label=\"\$RESULTS_$label $produced\""
}

case "$TARGET" in
    android) pick_android; log "android device $DEVICE" ;;
    ios) pick_ios; log "ios device $DEVICE" ;;
    headless|maccatalyst) ;;
    *) echo "unknown target $TARGET" >&2; exit 2 ;;
esac

SIDES=(current)
[[ -n "$BASELINE" ]] && SIDES=(baseline current)
for round in $(seq 1 "$ROUNDS"); do
    for side in "${SIDES[@]}"; do run_side "$side" "$round"; done
done

for side in "${SIDES[@]}"; do
    # shellcheck disable=SC2086
    eval "files=\$RESULTS_$side"
    python3 "$REPO_ROOT/scripts/bench_compare.py" merge "$OUT/$side.json" $files
done

if [[ -n "$BASELINE" ]]; then
    python3 "$REPO_ROOT/scripts/bench_compare.py" "$OUT/baseline.json" "$OUT/current.json" --threshold "$THRESHOLD" | tee "$OUT/compare.txt"
else
    python3 "$REPO_ROOT/scripts/bench_compare.py" show "$OUT/current.json" | tee "$OUT/summary.txt"
fi
log "results in $OUT"
