#!/usr/bin/env bash
#
# Smoke run of the demo and samples apps on Windows: builds them, launches each with SKUI_TOUR=1 (tests/Shared/PageTour:
# opens every page, scrolls its scrollers to the end and back, logs every exception with the page it happened on) and
# summarizes the log. Exits non-zero when an app crashed, did not finish, or threw an exception not in the known list
# below (also one the code caught: first-chance exceptions are reported too).
# Results: artifacts/page-tour/<timestamp>/{demo,samples}.log (+ build.log).
#
#   scripts/page_tour.sh                    # both apps, Debug
#   scripts/page_tour.sh -a samples -d 500  # one app, shorter stay on each page
#   scripts/page_tour.sh --no-build --all   # reuse the last build; list known exceptions too
#
# Pages are opened and scrolled only: nothing is tapped, so popups, pickers and dialogs are not covered.
#
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TFM="net10.0-windows10.0.19041.0"

APPS="demo,samples"
CONFIG="Debug"
DWELL=""
OUT=""
TIMEOUT=900
BUILD=true
ALL=false

usage() {
    cat <<'EOF'
Usage: scripts/page_tour.sh [options]

  -a LIST          demo, samples or demo,samples (default)
  -c CONFIG        Debug (default) or Release
  -d MS            how long each page stays open (default 1200)
  -o DIR           output directory (default artifacts/page-tour/<timestamp>)
  --timeout SEC    give up on an app after SEC seconds (default 900)
  --all            also list the known exceptions (DevFlow's broker, cancellations, ...)
  --no-build       reuse the last build
  -h, --help       this help
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        -a) APPS="$2"; shift 2 ;;
        -c) CONFIG="$2"; shift 2 ;;
        -d) DWELL="$2"; shift 2 ;;
        -o) OUT="$2"; shift 2 ;;
        --timeout) TIMEOUT="$2"; shift 2 ;;
        --all) ALL=true; shift ;;
        --no-build) BUILD=false; shift ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Unknown option: $1" >&2; usage >&2; exit 2 ;;
    esac
done

case "$(uname -s)" in
    MINGW*|MSYS*|CYGWIN*) ;;
    *) echo "page_tour.sh runs the Windows builds: run it from Git Bash on Windows." >&2; exit 2 ;;
esac

OUT="${OUT:-$REPO_ROOT/artifacts/page-tour/$(date +%Y%m%d-%H%M%S)}"
mkdir -p "$OUT"

project_dir() {
    case "$1" in
        demo) echo "$REPO_ROOT/MauiSkiaUiDemo" ;;
        samples) echo "$REPO_ROOT/samples/MauiSkiaUiSamples" ;;
        *) echo "Unknown app: $1" >&2; exit 2 ;;
    esac
}

# The SDK picks the runtime identifier (win-x64, win-arm64) from the host: ask MSBuild where the build goes.
executable() {
    local target
    target="$(dotnet msbuild "$(project_dir "$1")" -getProperty:TargetPath -p:TargetFramework="$TFM" -p:Configuration="$CONFIG" | tr -d '\r')"
    target="$(cygpath -u "$target")"
    echo "${target%.dll}.exe"
}

# Known exceptions, not failures. A block (one logged exception with its stack) matching one of these is skipped.
#   DevFlow's agent (Debug demo) retrying its broker on localhost:19223 when no broker runs.
#   Cancellations: image loads of views scrolled away or pages left, timers stopped.
#   MAUI's packaged-app probe (Package.Current) in an unpackaged app.
#   SkUiColumnsModel.Compose: a Format naming columns not added yet, caught (columns are added one by one).
summarize() {
    awk -v all="$ALL" '
        function known(block, header) {
            if (header !~ /\[Tour\] first-chance/) return ""
            if (block ~ /Microsoft\.Maui\.DevFlow|localhost:19223/) return "DevFlow broker"
            if (header ~ /(SocketException|WebSocketException): (No connection could be made|Unable to connect)/ && block !~ /MauiSkiaUi/) return "DevFlow broker"
            if (header ~ /(OperationCanceledException|TaskCanceledException)/) return "cancellation"
            if (block ~ /AppInfoUtils\.get_IsPackagedApp/) return "MAUI packaged-app probe"
            if (header ~ /FormatException/ && block ~ /SkUiColumnsModel\.Compose/) return "SkUiColumnsModel format (caught)"
            return ""
        }
        function flush(   reason, key, name, start, frame, n, lines, i) {
            if (header == "") return
            reason = known(block, header)
            if (reason != "") ignored[reason]++
            if (reason == "" || all == "true") {
                # Header: [Tour] <kind> on "<page>" thread <id>: <type>: <message> (single quotes around the page).
                # The page name ends at the first closing quote followed by " thread <id>: ": names and messages may
                # contain quotes. Group by kind, type and message, and the first frame of our code.
                match(header, /^\[Tour\] [A-Za-z-]+ on \x27/)
                start = RLENGTH + 1
                match(header, /\x27 thread [0-9]+: /)
                name = substr(header, start, RSTART - start)
                key = substr(header, 1, start - 6) ": " substr(header, RSTART + RLENGTH)
                frame = ""
                n = split(block, lines, "\n")
                for (i = 1; i <= n; i++)
                    if (lines[i] ~ /at (MauiSkiaUi|MauiSkiaUiDemo|MauiSkiaUiSamples)\./ && lines[i] !~ /MauiSkiaUiTour/) { frame = lines[i]; break }
                key = key (reason != "" ? "  [known: " reason "]" : "") "\n" frame
                if (!(key in count)) { order[++keys] = key; page[key] = name }
                count[key]++
                if (reason == "") failures++
            }
            header = ""; block = ""
        }
        /^\[Tour\] steps / { steps = $3; next }
        /^\[Tour\] open / { opened++; next }
        /^\[Tour\] done/ { done = 1; next }
        /^\[Tour\] end$/ { flush(); next }
        /^\[Tour\] [A-Za-z-]+ on \x27/ { flush(); header = $0; block = $0; next }
        header != "" { block = block "\n" $0 }
        END {
            flush()
            if (steps == "") print "  the tour did not start (SKUI_TOUR not picked up, or the app failed at startup)"
            else printf "  pages opened: %d of %d%s\n", opened, steps, done ? "" : "  (did not finish: crashed or timed out)"
            for (i = 1; i <= keys; i++) printf "  %4dx (first on %s) %s\n", count[order[i]], page[order[i]], order[i]
            for (reason in ignored) printf "  known, not reported: %d x %s\n", ignored[reason], reason
            exit (failures > 0 || steps == "" || !done || opened < steps) ? 1 : 0
        }
    ' "$1"
}

IFS=',' read -r -a APP_LIST <<< "$APPS"
for app in "${APP_LIST[@]}"; do project_dir "$app" > /dev/null; done

if $BUILD; then
    for app in "${APP_LIST[@]}"; do
        echo "Building $app ($CONFIG)..."
        if ! dotnet build "$(project_dir "$app")" -f "$TFM" -c "$CONFIG" -v q -clp:ErrorsOnly >> "$OUT/build.log" 2>&1; then
            tail -20 "$OUT/build.log" >&2
            exit 1
        fi
    done
fi

status=0
for app in "${APP_LIST[@]}"; do
    exe="$(executable "$app")"
    [[ -f "$exe" ]] || { echo "Not built: $exe" >&2; exit 1; }
    echo "Touring $app..."
    code=0
    SKUI_TOUR=1 SKUI_TOUR_DWELL="$DWELL" timeout "$TIMEOUT" "$exe" > "$OUT/$app.log" 2>&1 || code=$?
    # A crash after the tour finished (closing down) leaves a complete log: only the exit code shows it.
    [[ $code -eq 0 ]] || echo "  exit code $code"
    if summarize "$OUT/$app.log" && [[ $code -eq 0 ]]; then echo "  OK"; else echo "  FAILED"; status=1; fi
done
echo "Logs: $OUT"
exit $status
