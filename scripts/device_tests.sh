#!/usr/bin/env bash
#
# Run the on-device tests (tests/MauiSkiaUi.DeviceTests: a render check, the hosted-control check and memory leak
# scenarios with real handlers and platform views) and report the results. Builds the app (Release by default), installs it, launches it with --autorun --exit,
# collects its "SKUILEAK" console lines and exits non-zero when anything failed.
# Results: artifacts/device-tests/<timestamp>/{<target>.log,build.log}.
#
#   scripts/device_tests.sh -t maccatalyst
#   scripts/device_tests.sh -t android                         # the only adb device
#   scripts/device_tests.sh -t android -s 2299011508047ece -S ButtonsClicked,NativeOverlays
#   scripts/device_tests.sh -t ios                             # the booted simulator
#   scripts/device_tests.sh -t ios -s <device-udid>            # physical device (devicectl, or mlaunch below iOS 17)
#   scripts/device_tests.sh -t maccatalyst --aot               # Native AOT (fully trimmed) build of the app
#   scripts/device_tests.sh -t android --trim                  # fully trimmed build (no AOT)
#   scripts/device_tests.sh -t maccatalyst -S NativeOverlays --placement-tolerance -1   # the hosted check must fail
#
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
APP_DIR="$REPO_ROOT/tests/MauiSkiaUi.DeviceTests"
PROJECT="$APP_DIR/MauiSkiaUi.DeviceTests.csproj"
APP_ID="com.rkdevel.skiauidevicetests"
ACTIVITY="$APP_ID/skiauidevicetests.MainActivity"
PREFIX="SKUILEAK"

TARGET=""
DEVICE=""
CONFIG="Release"
SCENARIOS=""
OUT=""
TIMEOUT=900
BUILD=true
AOT=false
TRIM=false
PLACEMENT_TOLERANCE=""

usage() {
    cat <<'EOF'
Usage: scripts/device_tests.sh -t TARGET [options]

  -t TARGET        android | ios | maccatalyst
  -s DEVICE        adb serial (android) or simulator / device UDID (ios); default: the only connected one / booted simulator
  -c CONFIG        Release (default) or Debug. Debug may keep instances alive (debugger, Hot Reload)
  -S LIST          comma separated scenario names (default: all)
  -o DIR           output directory (default artifacts/device-tests/<timestamp>)
  --timeout SEC    give up after SEC seconds (default 900)
  --aot            publish the app with Native AOT (implies full trimming); fails on trim / AOT warnings from SkiaUi
  --trim           build the app fully trimmed (TrimMode=full, no AOT); fails on trim warnings from SkiaUi
  --placement-tolerance DIPS
                   override the hosted-control check's tolerance (default 1.01; a negative value must make it fail)
  --no-build       reuse the last build
  -h, --help       this help
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        -t) TARGET="$2"; shift 2 ;;
        -s) DEVICE="$2"; shift 2 ;;
        -c) CONFIG="$2"; shift 2 ;;
        -S) SCENARIOS="$2"; shift 2 ;;
        -o) OUT="$2"; shift 2 ;;
        --timeout) TIMEOUT="$2"; shift 2 ;;
        --no-build) BUILD=false; shift ;;
        --aot) AOT=true; shift ;;
        --trim) TRIM=true; shift ;;
        --placement-tolerance) PLACEMENT_TOLERANCE="$2"; shift 2 ;;
        -h|--help) usage; exit 0 ;;
        *) echo "unknown option $1" >&2; usage; exit 2 ;;
    esac
done
[[ "$TARGET" =~ ^(android|ios|maccatalyst)$ ]] || { usage; exit 2; }

OUT="${OUT:-$REPO_ROOT/artifacts/device-tests/$(date +%Y%m%d-%H%M%S)}"
mkdir -p "$OUT"
LOG="$OUT/$TARGET.log"
log() { echo "[device-tests] $*" >&2; }
# macOS without coreutils has no `timeout`; run unbounded there.
command -v timeout >/dev/null || timeout() { shift; "$@"; }

# ---- devices -------------------------------------------------------------------------------------------------------

ios_is_simulator() { xcrun simctl list devices | grep -q "$DEVICE"; }

case "$TARGET" in
    android)
        if [[ -z "$DEVICE" ]]; then
            DEVICE="$(adb devices | awk 'NR>1 && $2=="device" {print $1}' | head -1)"
            [[ -n "$DEVICE" ]] || { echo "no adb device" >&2; exit 1; }
        fi ;;
    ios)
        if [[ -z "$DEVICE" ]]; then
            DEVICE="$(xcrun simctl list devices booted | grep -Eo '[0-9A-F-]{36}' | head -1)"
            [[ -n "$DEVICE" ]] || { echo "no booted simulator; pass -s UDID" >&2; exit 1; }
        fi ;;
esac

TFM="$(dotnet msbuild "$PROJECT" -getProperty:TargetFrameworks | tr ';' '\n' | grep -- "-$TARGET\$" | head -1)"
[[ -n "$TFM" ]] || { echo "no $TARGET target framework in $PROJECT" >&2; exit 1; }
RID=""
HOST_ARCH="$(uname -m | sed 's/x86_64/x64/')"
if [[ "$TARGET" == ios ]]; then RID=ios-arm64; ios_is_simulator && RID="iossimulator-$HOST_ARCH"; fi
if [[ "$AOT" == true ]]; then
    # Native AOT compiles one runtime identifier.
    [[ "$TARGET" == maccatalyst ]] && RID="maccatalyst-$HOST_ARCH"
    [[ "$TARGET" == android ]] && RID=android-arm64
fi
IOS_MLAUNCH=false
if [[ "$TARGET" == ios && "$RID" == ios-arm64 ]] && ! xcrun devicectl list devices 2>/dev/null | grep -q "$DEVICE"; then
    IOS_MLAUNCH=true # devicectl knows only iOS 17+ devices; older ones launch through mlaunch (dotnet build -t:Run)
fi
if [[ "$IOS_MLAUNCH" == true && ( "$AOT" == true || "$TRIM" == true ) ]]; then
    echo "--aot / --trim are not supported for iOS devices below iOS 17 (mlaunch path); use a simulator or an iOS 17+ device" >&2
    exit 2
fi

# ---- build ---------------------------------------------------------------------------------------------------------

skiaui_warnings() { # build log → trim / AOT warnings attributed to SkiaUi (source path, assembly, package or namespace)
    python3 - "$1" "$REPO_ROOT" <<'PY'
import re, sys
log, root = sys.argv[1], sys.argv[2]
# Library sources: this checkout's MauiSkiaUi/ folder, or /_/MauiSkiaUi/ in deterministic (CI) builds.
skiaui = re.compile(
    "(?:" + re.escape(root) + "|/_)/MauiSkiaUi/"
    r"|Assembly 'MauiSkiaUi'|MauiSkiaUi\.dll|SkiaUi\.Maui"
    r"|(?<![\w.])MauiSkiaUi\.(?!DeviceTests|LeakTests)[A-Z]")  # library types, not the test app's own namespaces
project_context = re.compile(r"\s\[[^\]]*\]\s*$")  # MSBuild's trailing "[<project>::TargetFramework=…]"
seen = set()
for line in open(log, errors="replace"):
    if re.search(r"warning IL\d{4}", line) and skiaui.search(project_context.sub("", line)) and line not in seen:
        seen.add(line)
        print(line.rstrip())
PY
}

build() {
    log "building $CONFIG $TFM${RID:+ ($RID)}"
    rm -rf "$APP_DIR/bin/$CONFIG/$TFM"
    # Incremental iOS (AOT) builds after a library change can crash at launch ("Failed to load AOT module").
    [[ "$TARGET" == ios ]] && rm -rf "$APP_DIR/obj/$CONFIG/$TFM"
    # Only this target framework (also for the library): restore then needs no other platform's workload.
    local args=(-c "$CONFIG" -f "$TFM" "-p:TargetFrameworks=$TFM" "$PROJECT" -v q -nologo)
    [[ -n "$RID" ]] && args+=(-p:RuntimeIdentifier=$RID)
    local command=build
    if [[ "$AOT" == true ]]; then
        args+=(-p:PublishAot=true)
        if [[ "$RID" == iossimulator-* ]]; then
            args+=(-p:_IsPublishing=true) # the iOS SDK refuses `publish` for simulators; a publishing build still compiles AOT
        else
            command=publish
        fi
    fi
    [[ "$TRIM" == true ]] && args+=(-p:TrimMode=full)
    # Each trim warning on its own line (not one per assembly), so the SkiaUi check below sees them.
    [[ "$AOT" == true || "$TRIM" == true ]] && args+=(-p:TrimmerSingleWarn=false)
    [[ "$TARGET" == android && "$AOT" != true ]] && args+=(-t:SignAndroidPackage)
    dotnet "$command" "${args[@]}" >"$OUT/build.log" 2>&1 || { tail -30 "$OUT/build.log" >&2; echo "build failed ($OUT/build.log)" >&2; exit 1; }
    if [[ "$AOT" == true || "$TRIM" == true ]]; then
        # Trim / AOT warnings the app build reports for SkiaUi code: apps using SkiaUi would see the same.
        local warnings
        warnings="$(skiaui_warnings "$OUT/build.log")"
        if [[ -n "$warnings" ]]; then
            echo "$warnings" >&2
            echo "trim / AOT warnings from SkiaUi ($OUT/build.log)" >&2
            exit 1
        fi
        log "no trim / AOT warnings from SkiaUi"
    fi
}

output() { # glob below bin/<config>/<tfm> (or its <rid> folder, where a publish puts it)
    local match
    # shellcheck disable=SC2086
    match="$(ls -d "$APP_DIR"/bin/$CONFIG/$TFM/$1 "$APP_DIR"/bin/$CONFIG/$TFM/*/$1 2>/dev/null | head -1)"
    [[ -n "$match" ]] || { echo "build output $1 not found under $APP_DIR/bin/$CONFIG/$TFM" >&2; exit 1; }
    echo "$match"
}

# ---- run -----------------------------------------------------------------------------------------------------------

app_args=(--autorun --exit)
[[ -n "$SCENARIOS" ]] && app_args+=(--scenarios "$SCENARIOS")
[[ -n "$PLACEMENT_TOLERANCE" ]] && app_args+=(--placement-tolerance "$PLACEMENT_TOLERANCE")

filter() { grep "$PREFIX" | sed "s/.*$PREFIX/$PREFIX/"; }

run_android() {
    local apk
    apk="$(output "*-Signed.apk")"
    adb -s "$DEVICE" install -r "$apk" >/dev/null
    adb -s "$DEVICE" shell am force-stop "$APP_ID"
    adb -s "$DEVICE" logcat -c
    local extras=(--ez autorun true --ez exit true)
    [[ -n "$SCENARIOS" ]] && extras+=(--es scenarios "$SCENARIOS")
    [[ -n "$PLACEMENT_TOLERANCE" ]] && extras+=(--es placementTolerance "$PLACEMENT_TOLERANCE")
    adb -s "$DEVICE" shell am start -n "$ACTIVITY" "${extras[@]}" >/dev/null
    local waited=0
    until adb -s "$DEVICE" logcat -d | grep -q "${PREFIX}_DONE"; do
        sleep 2; waited=$((waited + 2))
        if [[ $waited -ge $TIMEOUT ]]; then log "timeout"; break; fi
        if [[ $waited -ge 20 ]] && ! adb -s "$DEVICE" shell pidof "$APP_ID" >/dev/null; then
            # It may have finished and exited since the check above.
            adb -s "$DEVICE" logcat -d | grep -q "${PREFIX}_DONE" || log "the app is not running (crash?)"
            break
        fi
    done
    adb -s "$DEVICE" logcat -d | filter >"$LOG" || true
    adb -s "$DEVICE" shell am force-stop "$APP_ID"
}

run_ios() {
    if ios_is_simulator; then
        xcrun simctl install "$DEVICE" "$(output "$RID/*.app")"
        timeout "$TIMEOUT" xcrun simctl launch --console --terminate-running-process "$DEVICE" "$APP_ID" "${app_args[@]}" 2>&1 | filter >"$LOG" || true
    elif [[ "$IOS_MLAUNCH" == true ]]; then
        local mlaunch_args="" stdout="$OUT/ios.stdout"
        for arg in "${app_args[@]}"; do mlaunch_args+="--argument=$arg;"; done
        timeout "$TIMEOUT" dotnet build -t:Run -c "$CONFIG" -f "$TFM" -p:RuntimeIdentifier=ios-arm64 "-p:_DeviceName=$DEVICE" \
            "-p:MlaunchAdditionalArgumentsProperty=\"${mlaunch_args%;}\"" "-p:StandardOutputPath=$stdout" -p:WaitForExit=true \
            "$PROJECT" -v q -nologo >>"$OUT/build.log" 2>&1 || true
        filter <"$stdout" >"$LOG" || true
    else
        xcrun devicectl device install app --device "$DEVICE" "$(output "ios-arm64/*.app")" >/dev/null
        timeout "$TIMEOUT" xcrun devicectl device process launch --console --terminate-existing --device "$DEVICE" "$APP_ID" "${app_args[@]}" 2>&1 | filter >"$LOG" || true
    fi
}

run_maccatalyst() {
    local binary
    binary="$(output "maccatalyst-$HOST_ARCH/*.app/Contents/MacOS/MauiSkiaUi.DeviceTests")"
    timeout "$TIMEOUT" "$binary" "${app_args[@]}" 2>&1 | filter >"$LOG" || true
}

# mlaunch builds and launches in one step; the other paths build first.
if [[ "$BUILD" == true && "$IOS_MLAUNCH" != true ]]; then build; fi
log "running on $TARGET${DEVICE:+ ($DEVICE)}"
"run_$TARGET"

# ---- report --------------------------------------------------------------------------------------------------------

python3 - "$LOG" <<'PY'
import json, sys
lines = open(sys.argv[1]).read().splitlines()
results, detector, render, hosted, done, errors = [], None, None, None, None, []
for line in lines:
    tag, _, payload = line.partition(" ")
    if tag == "SKUILEAK":
        results.append(json.loads(payload))
    elif tag == "SKUILEAK_DETECTOR":
        detector = json.loads(payload)
    elif tag == "SKUILEAK_RENDER":
        render = json.loads(payload)
    elif tag == "SKUILEAK_HOSTED":
        hosted = json.loads(payload)
    elif tag == "SKUILEAK_DONE":
        done = payload
    elif tag == "SKUILEAK_START":
        print(payload)
    elif tag == "SKUILEAK_ERROR":
        errors.append(payload)
for check, name in ((render, "rendering"), (hosted, "hosted"), (detector, "detector")):
    if check:
        print(f"{name:9} {check['Status']:4}  {check['Details']}")
for r in results:
    print(f"{r['Status']:4}  {r['Name']:22} {r['Seconds']:5.1f}s  {r['Details']}")
failed = [r for r in results if r["Status"] != "Pass"]
for e in errors:
    print("ERROR", e)
if done is None:
    print("the run did not finish (crash or timeout); see the log")
ok = (done is not None and not failed and not errors
      and all(check is not None and check["Status"] == "Pass" for check in (detector, render, hosted)))
print(f"\n{len(results) - len(failed)}/{len(results)} passed" + ("" if ok else " — FAILED"))
sys.exit(0 if ok else 1)
PY
