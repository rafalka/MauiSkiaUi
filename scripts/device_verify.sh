#!/usr/bin/env bash
#
# Build and launch MauiSkiaUiDemo on a connected Android device/emulator, iOS
# simulator/device, or Mac Catalyst — then print the Testing.md device checklist
# for manual verification outside VS Code / DevFlow MCP.
#
# Device selection UX mirrors runsim.sh-style interactive pickers.
#
set -euo pipefail

resolve_script_dir() {
    local source="${BASH_SOURCE[0]}"
    local dir
    while [[ -L "$source" ]]; do
        dir="$(cd "$(dirname "$source")" && pwd)"
        source="$(readlink "$source")"
        [[ "$source" != /* ]] && source="$dir/$source"
    done
    cd "$(dirname "$source")" && pwd
}

SCRIPT_DIR="$(resolve_script_dir)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
DEMO_PROJECT="$REPO_ROOT/MauiSkiaUiDemo/MauiSkiaUiDemo.csproj"
DEMO_DIR="$REPO_ROOT/MauiSkiaUiDemo"

readonly UUID_RE='[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}'

CONFIGURATION="Debug"
PLATFORM_FILTER=""
DEVICE_QUERY=""
SELECTED_ENTRY=""
RUNTIME_IDENTIFIER=""
LIST_ONLY=false
CHECKLIST_ONLY=false
RUN_ONLY=false
NO_LOGS=false
NO_CHECKLIST=false
FRESH_BUILD=false
WIPE_DEVICE=false
FULL_BUILD=false
TAKE_SCREENSHOT=false
SCREENSHOT_DIR=""
PHASE="all"
MSBUILD_VERBOSITY="m"
APPLICATION_ID=""
TARGET_FRAMEWORK=""
ADB_SERIAL=""

declare -a ENTRIES=()
declare -a EXTRA_MSBUILD_PROPS=()
declare -a RUNNING_ANDROID_AVD_NAMES=()

usage() {
    cat <<'EOF'
Usage: device_verify.sh [options] [DEVICE]

Build and launch MauiSkiaUiDemo for on-device verification (Testing.md), without
VS Code MAUI DevFlow MCP. Prints the manual acceptance checklist after launch.

DEVICE may be:
  - List index (when prompted)
  - iOS Simulator UDID or name (substring match)
  - iOS physical device UDID or name
  - Android adb serial, AVD name, or device model (substring match)
  - maccatalyst / mac / catalyst (Mac Catalyst — no DEVICE needed with -p maccatalyst)

With no DEVICE, prints a numbered list and waits for selection (like runsim.sh).

Options:
  -l, --list          List available devices/simulators/emulators and exit
  -p PLATFORM         Limit to android, ios, or maccatalyst
  -c CONFIG           MSBuild configuration (default: Debug)
  -r RID              Android runtime identifier, e.g. android-arm64
  -C, --fresh         Clean the project first, then deploy
  -R, --release       Release configuration (shorthand for -c Release)
  -U, --wipe          Uninstall the app from the target first
  -F, --full          Android: package/sign/install full APK (-t:Install)
                      instead of relying on -t:Run alone
  --phase N           Checklist phase: 0, 1, 2, overlay, or all (default: all)
  --checklist-only    Print the checklist and exit (no build/deploy)
  --run, --run-only   Launch the installed app only (no rebuild)
  --screenshot [DIR]  Capture a device screenshot after launch (default: tmp/screenshots)
  --no-checklist      Skip printing the verification checklist
  --no-logs           Do not stream app logs after launch
  -P PROP             Extra MSBuild property Name=Value (repeatable)
  -p:Name=Value       Same as -P (dotnet-style)
  -v, --verbose       Increase MSBuild verbosity
  -h, --help          Show this help

Examples:
  ./scripts/device_verify.sh -p android
  ./scripts/device_verify.sh -p ios "iPhone 16"
  ./scripts/device_verify.sh -p maccatalyst --phase overlay
  ./scripts/device_verify.sh --checklist-only --phase 2
  ./scripts/device_verify.sh -l -p ios

See Testing.md → "How to verify device rendering / live-update behavior" and
the Phase 0–2 native acceptance checklists.
EOF
}

normalize_query() {
    echo "$1" | tr '[:upper:]' '[:lower:]'
}

parse_args() {
    local args=()
    while [[ $# -gt 0 ]]; do
        case "$1" in
            -h|--help)
                usage
                exit 0
                ;;
            -l|--list)
                LIST_ONLY=true
                shift
                ;;
            -v|--verbose)
                MSBUILD_VERBOSITY="d"
                shift
                ;;
            -C|--fresh)
                FRESH_BUILD=true
                shift
                ;;
            -R|--release)
                CONFIGURATION="Release"
                shift
                ;;
            -U|--wipe)
                WIPE_DEVICE=true
                shift
                ;;
            -F|--full)
                FULL_BUILD=true
                shift
                ;;
            --checklist-only)
                CHECKLIST_ONLY=true
                shift
                ;;
            --run|--run-only)
                RUN_ONLY=true
                shift
                ;;
            --no-logs)
                NO_LOGS=true
                shift
                ;;
            --no-checklist)
                NO_CHECKLIST=true
                shift
                ;;
            --screenshot)
                TAKE_SCREENSHOT=true
                if [[ $# -ge 2 && "$2" != -* ]]; then
                    SCREENSHOT_DIR="$2"
                    shift 2
                else
                    shift
                fi
                ;;
            --screenshot=*)
                TAKE_SCREENSHOT=true
                SCREENSHOT_DIR="${1#--screenshot=}"
                shift
                ;;
            --phase)
                if [[ $# -lt 2 ]]; then
                    echo "--phase requires 0, 1, 2, overlay, or all" >&2
                    exit 1
                fi
                PHASE="$2"
                shift 2
                ;;
            --phase=*)
                PHASE="${1#--phase=}"
                shift
                ;;
            -p)
                if [[ $# -lt 2 ]]; then
                    echo "-p requires android, ios, or maccatalyst" >&2
                    exit 1
                fi
                PLATFORM_FILTER="$2"
                shift 2
                ;;
            -c)
                if [[ $# -lt 2 ]]; then
                    echo "-c requires a configuration name" >&2
                    exit 1
                fi
                CONFIGURATION="$2"
                shift 2
                ;;
            -r)
                if [[ $# -lt 2 ]]; then
                    echo "-r requires a runtime identifier" >&2
                    exit 1
                fi
                RUNTIME_IDENTIFIER="$2"
                shift 2
                ;;
            -P|--property)
                if [[ $# -lt 2 ]]; then
                    echo "-P/--property requires Name=Value" >&2
                    exit 1
                fi
                EXTRA_MSBUILD_PROPS+=("$2")
                shift 2
                ;;
            --property=*)
                EXTRA_MSBUILD_PROPS+=("${1#--property=}")
                shift
                ;;
            -p:*)
                EXTRA_MSBUILD_PROPS+=("${1#-p:}")
                shift
                ;;
            --)
                shift
                args+=("$@")
                break
                ;;
            -*)
                echo "Unknown option: $1" >&2
                usage >&2
                exit 1
                ;;
            *)
                args+=("$1")
                shift
                ;;
        esac
    done

    if [[ ${#args[@]} -gt 0 ]]; then
        DEVICE_QUERY="${args[0]}"
    fi

    case "$(normalize_query "$PLATFORM_FILTER")" in
        ""|android|ios|maccatalyst|mac|catalyst|macos) ;;
        *)
            echo "Unknown platform: $PLATFORM_FILTER (use android, ios, or maccatalyst)" >&2
            exit 1
            ;;
    esac

    case "$(normalize_query "$PHASE")" in
        0|1|2|overlay|all) ;;
        *)
            echo "Unknown --phase: $PHASE (use 0, 1, 2, overlay, or all)" >&2
            exit 1
            ;;
    esac

    case "$(normalize_query "$PLATFORM_FILTER")" in
        mac|catalyst|macos) PLATFORM_FILTER="maccatalyst" ;;
    esac
}

# --- Android SDK / emulator helpers (from runsim.sh / build_and_upload.sh) ---

setup_android_path() {
    local sdk=""
    if [[ -n "${ANDROID_SDK_ROOT:-}" ]]; then
        sdk="$ANDROID_SDK_ROOT"
    elif [[ -n "${ANDROID_HOME:-}" ]]; then
        sdk="$ANDROID_HOME"
    else
        local candidate
        for candidate in \
            "$HOME/devel/Android/sdk" \
            "$HOME/Library/Android/sdk" \
            "$HOME/Android/Sdk"; do
            if [[ -d "$candidate/platform-tools" ]]; then
                sdk="$candidate"
                break
            fi
        done
    fi

    if [[ -n "$sdk" ]]; then
        export ANDROID_HOME="${ANDROID_HOME:-$sdk}"
        export ANDROID_SDK_ROOT="${ANDROID_SDK_ROOT:-$sdk}"
        export PATH="$sdk/platform-tools:$sdk/emulator:$PATH"
    fi
}

emulator_bin() {
    setup_android_path
    if command -v emulator >/dev/null 2>&1; then
        command -v emulator
        return 0
    fi
    return 1
}

android_emulator_avd_name_from_ps() {
    local serial="$1" console_port
    local -a emulators=() avds_from_ps=()
    local count=0

    console_port="${serial#emulator-}"

    while IFS= read -r line; do
        [[ -z "$line" ]] && continue
        line="${line%%[[:space:]]*}"
        [[ "$line" == emulator-* ]] && emulators+=("$line")
    done < <(adb devices 2>/dev/null | tail -n +2)

    count="${#emulators[@]}"
    [[ "$count" -eq 0 ]] && return 1

    while IFS= read -r args; do
        [[ "$args" != *"-avd "* ]] && continue
        if [[ "$args" =~ -avd[[:space:]]+([^[:space:]]+) ]]; then
            avds_from_ps+=("${BASH_REMATCH[1]}")
        fi
    done < <(ps -eo args= 2>/dev/null | grep -E 'qemu-system|/emulator/' || true)

    if [[ "$count" -eq 1 && ${#avds_from_ps[@]} -eq 1 ]]; then
        printf '%s' "${avds_from_ps[0]}"
        return 0
    fi

    while IFS= read -r args; do
        [[ "$args" != *"-avd "* ]] && continue
        if [[ "$args" =~ -avd[[:space:]]+([^[:space:]]+) ]] \
            && [[ "$args" == *"-port ${console_port}"* ]]; then
            printf '%s' "${BASH_REMATCH[1]}"
            return 0
        fi
    done < <(ps -eo args= 2>/dev/null | grep -E 'qemu-system|/emulator/' || true)

    return 1
}

android_emulator_avd_name() {
    local serial="$1" name line
    setup_android_path
    [[ "$serial" == emulator-* ]] || return 1
    if ! command -v adb >/dev/null 2>&1; then
        return 1
    fi

    for name in ro.kernel.qemu.avd_name ro.boot.qemu.avd_name; do
        line="$(adb -s "$serial" shell getprop "$name" 2>/dev/null | tr -d '\r\n')"
        line="${line#"${line%%[![:space:]]*}"}"
        line="${line%"${line##*[![:space:]]}"}"
        if [[ -n "$line" && "$line" != "OK" ]]; then
            printf '%s' "$line"
            return 0
        fi
    done

    while IFS= read -r line; do
        line="${line//$'\r'/}"
        line="${line#"${line%%[![:space:]]*}"}"
        line="${line%"${line##*[![:space:]]}"}"
        [[ -z "$line" || "$line" == "OK" ]] && continue
        printf '%s' "$line"
        return 0
    done < <(adb -s "$serial" emu avd name 2>/dev/null)

    android_emulator_avd_name_from_ps "$serial"
}

android_avd_mark_running() {
    local avd="$1" name
    for name in "${RUNNING_ANDROID_AVD_NAMES[@]}"; do
        [[ "$name" == "$avd" ]] && return 0
    done
    RUNNING_ANDROID_AVD_NAMES+=("$avd")
}

android_avd_is_listed_running() {
    local avd="$1" name
    for name in "${RUNNING_ANDROID_AVD_NAMES[@]}"; do
        [[ "$name" == "$avd" ]] && return 0
    done
    return 1
}

android_avd_is_running() {
    local avd="$1" serial state running_avd _rest
    if android_avd_is_listed_running "$avd"; then
        return 0
    fi
    setup_android_path
    if ! command -v adb >/dev/null 2>&1; then
        return 1
    fi

    while IFS= read -r line; do
        [[ -z "$line" ]] && continue
        serial="${line%%[[:space:]]*}"
        _rest="${line#"$serial"}"
        _rest="${_rest#"${_rest%%[![:space:]]*}"}"
        state="${_rest%%[[:space:]]*}"
        [[ "$state" == "device" ]] || continue
        [[ "$serial" == emulator-* ]] || continue
        running_avd="$(android_emulator_avd_name "$serial" || true)"
        if [[ "$running_avd" == "$avd" ]]; then
            return 0
        fi
    done < <(adb devices 2>/dev/null | tail -n +2)
    return 1
}

wait_for_emulator_serial() {
    local avd="$1" timeout="${2:-180}" elapsed=0 serial state running_avd _rest
    setup_android_path

    while ((elapsed < timeout)); do
        while IFS= read -r line; do
            [[ -z "$line" ]] && continue
            serial="${line%%[[:space:]]*}"
            _rest="${line#"$serial"}"
            _rest="${_rest#"${_rest%%[![:space:]]*}"}"
            state="${_rest%%[[:space:]]*}"
            [[ "$state" == "device" ]] || continue
            [[ "$serial" == emulator-* ]] || continue
            running_avd="$(android_emulator_avd_name "$serial" || true)"
            if [[ "$running_avd" == "$avd" ]]; then
                echo "$serial"
                return 0
            fi
        done < <(adb devices 2>/dev/null | tail -n +2)
        sleep 2
        elapsed=$((elapsed + 2))
    done

    echo "Timed out waiting for emulator AVD \"$avd\" to appear in adb (after ${timeout}s)" >&2
    return 1
}

start_android_emulator() {
    local avd="$1" emu serial
    if ! emu="$(emulator_bin)"; then
        echo "Android emulator binary not found. Set ANDROID_HOME or install the SDK." >&2
        return 1
    fi

    if android_avd_is_running "$avd"; then
        echo "Emulator already running for AVD: $avd"
        wait_for_emulator_serial "$avd" 5
        return $?
    fi

    echo "Starting Android emulator: $avd"
    "$emu" -avd "$avd" >/dev/null 2>&1 &
    echo "  Waiting for emulator to appear in adb..."
    wait_for_emulator_serial "$avd"
}

# --- Entry loading ---

load_ios_sim_entries() {
    local runtime="" line trimmed name udid state
    while IFS= read -r line; do
        if [[ "$line" =~ ^--[[:space:]]+(.+)[[:space:]]+--$ ]]; then
            runtime="${BASH_REMATCH[1]}"
            continue
        fi
        if [[ "$line" =~ \(([0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12})\)[[:space:]]+\((Booted|Shutdown|Shutting[[:space:]]Down)\)[[:space:]]*$ ]]; then
            udid="${BASH_REMATCH[1]}"
            state="${BASH_REMATCH[2]}"
            trimmed="${line#"${line%%[![:space:]]*}"}"
            name="${trimmed% (${udid})*}"
            ENTRIES+=("ios:sim:${udid}:${name}:${runtime} (${state})")
        fi
    done < <(xcrun simctl list devices available 2>/dev/null)
}

load_ios_device_entries() {
    if ! command -v python3 >/dev/null 2>&1; then
        return 0
    fi

    local json_file
    json_file="$(mktemp)"
    if ! xcrun devicectl list devices --json-output "$json_file" --quiet 2>/dev/null; then
        rm -f "$json_file"
        return 0
    fi

    while IFS= read -r entry; do
        [[ -z "$entry" ]] && continue
        ENTRIES+=("$entry")
    done < <(
        python3 - "$json_file" <<'PY'
import json
import sys

path = sys.argv[1]
with open(path, encoding="utf-8") as f:
    data = json.load(f)

for dev in data.get("result", {}).get("devices", []):
    props = dev.get("deviceProperties", {})
    hw = dev.get("hardwareProperties", {})
    conn = dev.get("connectionProperties", {})
    udid = hw.get("udid") or ""
    name = props.get("name") or udid
    if not udid:
        continue
    tunnel = conn.get("tunnelState") or "unknown"
    model = hw.get("marketingName") or hw.get("productType") or ""
    extra = tunnel
    if model:
        extra = f"{tunnel} — {model}"
    print(f"ios:device:{udid}:{name}:{extra}")
PY
    )
    rm -f "$json_file"
}

load_android_device_entries() {
    setup_android_path
    if ! command -v adb >/dev/null 2>&1; then
        return 0
    fi

    local line serial state rest model avd_name label extra entry_type
    while IFS= read -r line; do
        [[ -z "$line" ]] && continue
        serial="${line%%[[:space:]]*}"
        rest="${line#"$serial"}"
        rest="${rest#"${rest%%[![:space:]]*}"}"
        state="${rest%%[[:space:]]*}"
        [[ "$state" == "device" ]] || continue

        model=""
        if [[ "$rest" =~ model:([^[:space:]]+) ]]; then
            model="${BASH_REMATCH[1]}"
        fi

        avd_name=""
        entry_type="device"
        if [[ "$serial" == emulator-* ]]; then
            avd_name="$(android_emulator_avd_name "$serial" || true)"
            if [[ -n "$avd_name" ]]; then
                entry_type="emulator"
                android_avd_mark_running "$avd_name"
            fi
        fi

        if [[ "$entry_type" == "emulator" ]]; then
            label="${avd_name} (${serial})"
            extra="running"
        elif [[ -n "$model" ]]; then
            label="$model ($serial)"
            extra="$model"
        else
            label="$serial"
            extra="connected"
        fi
        ENTRIES+=("android:${entry_type}:${serial}:${label}:${extra}")
    done < <(adb devices -l 2>/dev/null | tail -n +2)
}

load_android_avd_entries() {
    local emu avd
    if ! emu="$(emulator_bin)"; then
        return 0
    fi

    while IFS= read -r avd; do
        [[ -z "$avd" ]] && continue
        if android_avd_is_listed_running "$avd"; then
            continue
        fi
        if android_avd_is_running "$avd"; then
            continue
        fi
        ENTRIES+=("android:avd:${avd}:${avd}:not running")
    done < <("$emu" -list-avds 2>/dev/null)
}

load_maccatalyst_entry() {
    ENTRIES+=("maccatalyst:local:host:This Mac:Mac Catalyst")
}

load_entries() {
    ENTRIES=()
    RUNNING_ANDROID_AVD_NAMES=()
    local filter
    filter="$(normalize_query "$PLATFORM_FILTER")"

    if [[ -z "$filter" || "$filter" == "ios" ]]; then
        load_ios_sim_entries
        load_ios_device_entries
    fi
    if [[ -z "$filter" || "$filter" == "android" ]]; then
        load_android_device_entries
        load_android_avd_entries
    fi
    if [[ -z "$filter" || "$filter" == "maccatalyst" ]]; then
        load_maccatalyst_entry
    fi
}

entry_platform() { cut -d: -f1 <<<"$1"; }
entry_type() { cut -d: -f2 <<<"$1"; }
entry_id() { cut -d: -f3 <<<"$1"; }
entry_name() { cut -d: -f4 <<<"$1"; }
entry_extra() {
    local rest
    rest="$(cut -d: -f5- <<<"$1")"
    printf '%s' "$rest"
}

platform_label() {
    case "$1" in
        ios) echo "iOS" ;;
        android) echo "Android" ;;
        maccatalyst) echo "Mac Catalyst" ;;
        *) echo "$1" ;;
    esac
}

entry_kind_label() {
    local platform type
    platform="$(entry_platform "$1")"
    type="$(entry_type "$1")"
    case "${platform}:${type}" in
        ios:sim) echo "Simulator" ;;
        ios:device) echo "Device" ;;
        android:device) echo "Device" ;;
        android:emulator) echo "Emulator (AVD)" ;;
        android:avd) echo "Emulator (AVD)" ;;
        maccatalyst:*) echo "App" ;;
        *) echo "$type" ;;
    esac
}

entry_display_name() {
    local entry="$1" name id
    name="$(entry_name "$entry")"
    id="$(entry_id "$entry")"
    if [[ -z "$id" || "$name" == "$id" || "$name" == *"$id"* || "$id" == "host" ]]; then
        printf '%s' "$name"
    else
        printf '%s (%s)' "$name" "$id"
    fi
}

print_entry_line() {
    local index="$1" entry="$2" fd="${3:-1}"
    local platform kind display_name extra
    platform="$(entry_platform "$entry")"
    kind="$(entry_kind_label "$entry")"
    display_name="$(entry_display_name "$entry")"
    extra="$(entry_extra "$entry")"
    if [[ -n "$extra" ]]; then
        printf "  %2d) [%s %s] %s — %s\n" "$index" "$(platform_label "$platform")" "$kind" "$display_name" "$extra" >&"$fd"
    else
        printf "  %2d) [%s %s] %s\n" "$index" "$(platform_label "$platform")" "$kind" "$display_name" >&"$fd"
    fi
}

print_entries() {
    local i=1 entry
    if [[ ${#ENTRIES[@]} -eq 0 ]]; then
        echo "No deploy targets found." >&2
        echo "  iOS simulators: install Xcode runtimes (xcrun simctl list devices)" >&2
        echo "  iOS devices: connect and trust a device (xcrun devicectl list devices)" >&2
        echo "  Android: connect a device, start an emulator, or create an AVD" >&2
        echo "  Mac Catalyst: pass -p maccatalyst" >&2
        return 1
    fi

    for entry in "${ENTRIES[@]}"; do
        print_entry_line "$i" "$entry"
        ((i++)) || true
    done
}

find_matches() {
    local query="$1" entry platform type id name lc_name lc_id lc_query
    local -a matches=()
    lc_query="$(normalize_query "$query")"

    # Convenience aliases for Mac Catalyst
    case "$lc_query" in
        maccatalyst|mac|catalyst|macos|this[[:space:]]mac)
            for entry in "${ENTRIES[@]}"; do
                if [[ "$(entry_platform "$entry")" == "maccatalyst" ]]; then
                    printf '%s\n' "$entry"
                    return 0
                fi
            done
            ;;
    esac

    for entry in "${ENTRIES[@]}"; do
        platform="$(entry_platform "$entry")"
        type="$(entry_type "$entry")"
        id="$(entry_id "$entry")"
        name="$(entry_name "$entry")"
        lc_name="$(normalize_query "$name")"
        lc_id="$(normalize_query "$id")"

        if [[ "$query" =~ ^${UUID_RE}$ ]] && [[ "$(normalize_query "$query")" == "$lc_id" ]]; then
            matches+=("$entry")
            continue
        fi
        if [[ "$lc_id" == "$lc_query" || "$lc_name" == "$lc_query" ]]; then
            matches+=("$entry")
            continue
        fi
        if [[ "$lc_name" == *"$lc_query"* || "$lc_id" == *"$lc_query"* ]]; then
            matches+=("$entry")
        fi
    done

    if [[ ${#matches[@]} -eq 0 ]]; then
        echo "No device matched: $query" >&2
        return 1
    fi
    if [[ ${#matches[@]} -gt 1 ]]; then
        echo "Multiple matches for \"$query\":" >&2
        local i=1 m
        for m in "${matches[@]}"; do
            print_entry_line "$i" "$m" 2
            ((i++)) || true
        done
        echo "Provide a more specific name, serial, or UDID." >&2
        return 1
    fi
    printf '%s\n' "${matches[0]}"
}

prompt_selection() {
    local choice max="${#ENTRIES[@]}"
    echo "Available deploy targets:"
    print_entries || return 1
    echo
    while true; do
        printf "Select device [1-%s] (q to quit): " "$max"
        if ! read -r choice; then
            echo
            return 1
        fi
        case "$choice" in
            q|Q) return 1 ;;
        esac
        if [[ "$choice" =~ ^[0-9]+$ ]] && ((choice >= 1 && choice <= max)); then
            SELECTED_ENTRY="${ENTRIES[$((choice - 1))]}"
            return 0
        fi
        echo "Invalid selection. Enter a number from 1 to $max, or q to quit." >&2
    done
}

resolve_target() {
    if [[ -n "$SELECTED_ENTRY" ]]; then
        return 0
    fi

    # Mac Catalyst with -p maccatalyst and no DEVICE: pick automatically
    if [[ "$(normalize_query "$PLATFORM_FILTER")" == "maccatalyst" && -z "$DEVICE_QUERY" ]]; then
        load_entries
        SELECTED_ENTRY="${ENTRIES[0]}"
        return 0
    fi

    load_entries

    if [[ -n "$DEVICE_QUERY" ]]; then
        if [[ "$DEVICE_QUERY" =~ ^[0-9]+$ ]] && ((DEVICE_QUERY >= 1 && DEVICE_QUERY <= ${#ENTRIES[@]})); then
            SELECTED_ENTRY="${ENTRIES[$((DEVICE_QUERY - 1))]}"
            return 0
        fi
        SELECTED_ENTRY="$(find_matches "$DEVICE_QUERY")"
        return 0
    fi

    prompt_selection
}

prepare_android_avd_target() {
    if [[ "$(entry_platform "$SELECTED_ENTRY")" != "android" ]]; then
        return 0
    fi
    if [[ "$(entry_type "$SELECTED_ENTRY")" != "avd" ]]; then
        return 0
    fi

    local avd serial name
    avd="$(entry_id "$SELECTED_ENTRY")"
    name="$(entry_name "$SELECTED_ENTRY")"
    if ! serial="$(start_android_emulator "$avd")"; then
        echo "Failed to start or detect emulator for AVD: $avd" >&2
        exit 1
    fi
    SELECTED_ENTRY="android:emulator:${serial}:${name} (${serial}):running"
}

ensure_ios_sim_booted() {
    local udid="$1" name="$2"
    echo "Ensuring iOS Simulator is booted: $name ($udid)"
    if ! xcrun simctl boot "$udid" 2>/dev/null; then
        if xcrun simctl list devices | grep -qE "\($udid\) \(Booted\)"; then
            echo "  Already booted."
        else
            echo "Failed to boot iOS Simulator $udid" >&2
            return 1
        fi
    fi
    open -a Simulator --args -CurrentDeviceUDID "$udid" >/dev/null 2>&1 || true
}

ios_device_is_online() {
    local extra="$1"
    case "$(normalize_query "$extra")" in
        *unavailable*|*offline*|*disconnected*) return 1 ;;
    esac
    return 0
}

# --- Project / MSBuild ---

read_application_id() {
    APPLICATION_ID="$(
        dotnet msbuild "$DEMO_PROJECT" -getProperty:ApplicationId -nologo -v:q 2>/dev/null | tr -d '\r\n'
    )"
    if [[ -z "$APPLICATION_ID" ]]; then
        APPLICATION_ID="com.rkdevel.mauiskiauidemo"
    fi
}

resolve_target_framework() {
    local platform
    platform="$(entry_platform "$SELECTED_ENTRY")"
    case "$platform" in
        android) TARGET_FRAMEWORK="net10.0-android" ;;
        ios) TARGET_FRAMEWORK="net10.0-ios" ;;
        maccatalyst) TARGET_FRAMEWORK="net10.0-maccatalyst" ;;
        *)
            echo "Unknown platform: $platform" >&2
            exit 1
            ;;
    esac
}

abi_to_rid() {
    case "$1" in
        arm64-v8a) echo "android-arm64" ;;
        armeabi-v7a) echo "android-arm" ;;
        x86_64) echo "android-x64" ;;
        x86) echo "android-x86" ;;
        *)
            echo "Unknown device ABI: $1" >&2
            exit 1
            ;;
    esac
}

adb_cmd() {
    if [[ -n "$ADB_SERIAL" ]]; then
        adb -s "$ADB_SERIAL" "$@"
    else
        adb "$@"
    fi
}

resolve_android_runtime_identifier() {
    if [[ -n "$RUNTIME_IDENTIFIER" ]]; then
        echo "$RUNTIME_IDENTIFIER"
        return
    fi

    local device_abi=""
    if adb_cmd get-state >/dev/null 2>&1; then
        device_abi="$(adb_cmd shell getprop ro.product.cpu.abi 2>/dev/null | tr -d '\r')"
    fi

    if [[ -n "$device_abi" ]]; then
        abi_to_rid "$device_abi"
        return
    fi

    echo "android-arm64"
}

append_extra_msbuild_props() {
    local -n arr=$1
    local prop
    if [[ ${#EXTRA_MSBUILD_PROPS[@]} -eq 0 ]]; then
        return 0
    fi
    for prop in "${EXTRA_MSBUILD_PROPS[@]}"; do
        arr+=(-p:"$prop")
    done
}

# --- Checklist (from Testing.md) ---

print_checklist() {
    local phase
    phase="$(normalize_query "$PHASE")"

    cat <<EOF

══════════════════════════════════════════════════════════════════════════════
  MauiSkiaUiDemo — device verification checklist (Testing.md)
  Record what you observe. Do not mark items verified without checking them.
══════════════════════════════════════════════════════════════════════════════

Navigation tips (gallery home):
  • Components gallery lists every SkUi* demo (OpenSkUiMauiContentView, …)
  • Toolbar: Composition | Stress | Primitives
  • Shell routes: composition, stress, primitives, demo-SkUiMauiContentView, …

EOF

    if [[ "$phase" == "all" || "$phase" == "0" ]]; then
        cat <<'EOF'
── Phase 0 (Primitives toolbar page) ─────────────────────────────────────────
  [ ] Launch; confirm box, ellipse, line, and software strip appear
  [ ] Scene / hosted descendants: non-zero bounds; only Scene + SoftwareSample
      own handlers
  [ ] Tap box/ellipse; TapStatus increments and fill changes
  [ ] Replay animation; transforms change, then AnimationStatus → Idle and
      root clock/render loop stops
  [ ] Inspect native label/button colors + screenshot (incl. compact viewport)

EOF
    fi

    if [[ "$phase" == "all" || "$phase" == "1" ]]; then
        cat <<'EOF'
── Phase 1 (Composition + Stress) ────────────────────────────────────────────
  [ ] Composition at phone and tablet/desktop sizes; inspect ControlsHost,
      ControlsScroller, EarthImage, AddObservation bounds — one native surface,
      handlerless descendants
  [ ] Offline Earth image, text wrapping, Grid columns, style colors,
      pressed/disabled feedback, observation count binding + reset
  [ ] Pan from a button: no click after threshold; fling settles; new press
      interrupts. Tap after scroll hits translated control. Back to top /
      desktop wheel
  [ ] Stress: scroll to last of 1,000 buttons, tap it, Record/Scroll/Top,
      navigate back — no stale animations or extra surfaces
  [ ] Query runtime colors if possible; screenshot for contrast

EOF
    fi

    if [[ "$phase" == "all" || "$phase" == "2" || "$phase" == "overlay" ]]; then
        cat <<'EOF'
── Phase 2 / overlay (demo-SkUiMauiContentView) ──────────────────────────────
  Open: Components → SkUiMauiContentView  (or route demo-SkUiMauiContentView)

  [ ] Native Editor + WebView overlays render and receive input, positioned
      over the Skia host (not stacked at 0,0)
  [ ] Overlays stay correct after scrolling/resizing the page
  [ ] Type HTML in the Editor → WebView live-updates (red <h1> test) without
      tapping Refresh preview
  [ ] Tap Refresh preview; WebView stays in sync (manual path)
  [ ] Surrounding SkUiLabel / SkUiButton layout correctly beside overlays
  [ ] Nested layout / TranslationX|Y cases if exercising ComputeRootRelativeFrame
  [ ] Basic controls contrast (Switch / CheckBox / RadioButton) and Border
      rounded clip; Stack/Absolute spacing vs native at a few sizes

EOF
    fi

    cat <<'EOF'
Notes:
  • This script uses plain `dotnet build -t:Run` (no VS Code DevFlow injection).
  • DevFlow MCP agents (`maui_tree`, `maui_screenshot`, …) require the VS Code
    MAUI extension path documented in Testing.md — not this script.
  • Android DevFlow broker (if you later attach an agent): 
      adb reverse tcp:19223 tcp:19223
══════════════════════════════════════════════════════════════════════════════
EOF
}

# --- Screenshots ---

capture_screenshot() {
    local out_dir platform type id stamp path
    platform="$(entry_platform "$SELECTED_ENTRY")"
    type="$(entry_type "$SELECTED_ENTRY")"
    id="$(entry_id "$SELECTED_ENTRY")"
    out_dir="${SCREENSHOT_DIR:-$REPO_ROOT/tmp/screenshots}"
    mkdir -p "$out_dir"
    stamp="$(date +%Y%m%d-%H%M%S)"
    path="$out_dir/skiaui-${platform}-${stamp}.png"

    echo "Capturing screenshot → $path"
    case "$platform" in
        android)
            if ! adb_cmd exec-out screencap -p >"$path"; then
                echo "Screenshot failed (adb screencap)." >&2
                return 1
            fi
            ;;
        ios)
            if [[ "$type" == "sim" ]]; then
                if ! xcrun simctl io "$id" screenshot "$path"; then
                    echo "Screenshot failed (simctl io)." >&2
                    return 1
                fi
            else
                echo "Physical iOS screenshots are not automated here; use the device." >&2
                return 1
            fi
            ;;
        maccatalyst)
            if command -v screencapture >/dev/null 2>&1; then
                # Interactive window selection — user clicks the demo window
                echo "Click the MauiSkiaUiDemo window to capture…"
                screencapture -W -o "$path" || {
                    echo "Screenshot cancelled or failed." >&2
                    return 1
                }
            else
                echo "screencapture not available." >&2
                return 1
            fi
            ;;
        *)
            echo "No screenshot support for $platform" >&2
            return 1
            ;;
    esac
    echo "Screenshot saved: $path"
}

# --- Log streaming ---

stream_android_logs() {
    local app_id="$1" pid="" i
    echo
    echo "Streaming logs for $app_id (Ctrl+C to stop)…"

    for (( i = 0; i < 30; i++ )); do
        pid="$(adb_cmd shell pidof -s "$app_id" 2>/dev/null | tr -d '\r\n')"
        if [[ -n "$pid" && "$pid" =~ ^[0-9]+$ ]]; then
            break
        fi
        pid=""
        sleep 0.5
    done

    if [[ -n "$pid" ]]; then
        echo "Filtering logcat to PID $pid"
        adb_cmd logcat -v time --pid="$pid"
    else
        echo "App process not found; filtering .NET / runtime tags"
        adb_cmd logcat -v time '*:S' \
            'Mono:D' 'MonoRuntime:D' 'monodroid:D' 'monodroid-assembly:D' \
            'AndroidRuntime:E' 'DOTNET:D' 'DEBUG:I'
    fi
}

stream_ios_logs() {
    local type="$1" udid="$2" app_id="$3"
    local predicate='processImagePath CONTAINS[c] "MauiSkiaUiDemo" OR subsystem CONTAINS[c] "'"$app_id"'"'

    echo
    echo "Streaming logs for $app_id (Ctrl+C to stop)…"

    if [[ "$type" == "sim" ]]; then
        xcrun simctl spawn "$udid" log stream --style compact --level debug --predicate "$predicate"
    else
        log stream --style compact --level debug --predicate "$predicate"
    fi
}

stream_app_logs() {
    local rc=0
    set +e
    case "$(entry_platform "$SELECTED_ENTRY")" in
        android) stream_android_logs "$APPLICATION_ID" ;;
        ios) stream_ios_logs "$(entry_type "$SELECTED_ENTRY")" "$(entry_id "$SELECTED_ENTRY")" "$APPLICATION_ID" ;;
        maccatalyst)
            echo
            echo "Streaming Mac Catalyst logs (Ctrl+C to stop)…"
            log stream --style compact --level debug --predicate 'processImagePath CONTAINS[c] "MauiSkiaUiDemo"'
            ;;
    esac
    rc=$?
    set -e
    if [[ "$rc" -ne 0 && "$rc" -ne 130 ]]; then
        return "$rc"
    fi
    return 0
}

# --- Deploy ---

wipe_app() {
    local platform type id
    platform="$(entry_platform "$SELECTED_ENTRY")"
    type="$(entry_type "$SELECTED_ENTRY")"
    id="$(entry_id "$SELECTED_ENTRY")"

    echo "Wiping $APPLICATION_ID from target…"
    case "$platform" in
        android)
            adb_cmd uninstall "$APPLICATION_ID" || echo "  (app was not installed)"
            ;;
        ios)
            if [[ "$type" == "sim" ]]; then
                xcrun simctl uninstall "$id" "$APPLICATION_ID" || echo "  (app was not installed)"
            else
                xcrun devicectl device uninstall app --device "$id" "$APPLICATION_ID" \
                    || echo "  (app was not installed or uninstall failed)"
            fi
            ;;
        maccatalyst)
            echo "  Mac Catalyst wipe: remove the app from bin/$CONFIGURATION/$TARGET_FRAMEWORK if needed."
            ;;
    esac
}

deploy_android() {
    ADB_SERIAL="$(entry_id "$SELECTED_ENTRY")"
    local rid
    rid="$(resolve_android_runtime_identifier)"

    local -a cmd=(
        dotnet build "$DEMO_PROJECT"
        -c "$CONFIGURATION"
        -f "$TARGET_FRAMEWORK"
        -r "$rid"
        "-v:$MSBUILD_VERBOSITY"
        -p:"AdbTarget=-s $ADB_SERIAL"
        -p:"AndroidDevice=$ADB_SERIAL"
        -p:"Device=$ADB_SERIAL"
    )
    append_extra_msbuild_props cmd

    echo "Platform         : Android"
    echo "Target framework : $TARGET_FRAMEWORK"
    echo "Configuration    : $CONFIGURATION"
    echo "Runtime (RID)    : $rid"
    echo "Application id   : $APPLICATION_ID"
    echo "ADB serial       : $ADB_SERIAL"
    echo "Mode             : $([[ "$FULL_BUILD" == true ]] && echo 'full Install+Start' || echo 'dotnet build -t:Run')"
    echo

    setup_android_path
    if ! adb_cmd get-state >/dev/null 2>&1; then
        echo "No accessible Android device/emulator for adb serial $ADB_SERIAL" >&2
        exit 1
    fi

    if [[ "$WIPE_DEVICE" == true ]]; then
        wipe_app
    fi

    if [[ "$FRESH_BUILD" == true ]]; then
        echo "Fresh build: cleaning…"
        dotnet clean "$DEMO_PROJECT" -c "$CONFIGURATION" -f "$TARGET_FRAMEWORK" "-v:$MSBUILD_VERBOSITY"
    fi

    if [[ "$FULL_BUILD" == true ]]; then
        echo "Full build: Install + StartAndroidActivity…"
        command "${cmd[@]}" -p:EmbedAssembliesIntoApk=true -t:Install -t:StartAndroidActivity
    else
        echo "Building and launching (-t:Run)…"
        command "${cmd[@]}" -t:Run
    fi
}

deploy_ios() {
    local type id name extra
    type="$(entry_type "$SELECTED_ENTRY")"
    id="$(entry_id "$SELECTED_ENTRY")"
    name="$(entry_name "$SELECTED_ENTRY")"
    extra="$(entry_extra "$SELECTED_ENTRY")"

    local -a cmd=(
        dotnet build "$DEMO_PROJECT"
        -c "$CONFIGURATION"
        -f "$TARGET_FRAMEWORK"
        "-v:$MSBUILD_VERBOSITY"
    )

    local target_label="iOS Simulator"
    if [[ "$type" == "sim" ]]; then
        ensure_ios_sim_booted "$id" "$name"
        local sim_rid="iossimulator-arm64"
        if [[ "$(uname -m)" == "x86_64" ]]; then
            sim_rid="iossimulator-x64"
        fi
        cmd+=(-p:"RuntimeIdentifier=$sim_rid" -p:"_DeviceName=:v2:udid=$id")
    else
        target_label="iOS Device"
        if ! ios_device_is_online "$extra"; then
            echo "Selected iOS device is not connected: $name ($id)" >&2
            echo "  State: $extra" >&2
            exit 1
        fi
        cmd+=(-p:"RuntimeIdentifier=ios-arm64" -p:"_DeviceName=$id")
    fi
    append_extra_msbuild_props cmd

    echo "Platform         : $target_label"
    echo "Target framework : $TARGET_FRAMEWORK"
    echo "Configuration    : $CONFIGURATION"
    echo "Application id   : $APPLICATION_ID"
    echo "Device           : $name ($id)"
    echo

    if [[ "$WIPE_DEVICE" == true ]]; then
        wipe_app
    fi

    if [[ "$FRESH_BUILD" == true ]]; then
        echo "Fresh build: cleaning…"
        dotnet clean "$DEMO_PROJECT" -c "$CONFIGURATION" -f "$TARGET_FRAMEWORK" "-v:$MSBUILD_VERBOSITY"
    fi

    # Build .app first, then Run (Run does not depend on Build).
    echo "Building $target_label…"
    command "${cmd[@]}"

    echo "Launching on $target_label…"
    command "${cmd[@]}" -t:Run
}

deploy_maccatalyst() {
    local -a cmd=(
        dotnet build "$DEMO_PROJECT"
        -c "$CONFIGURATION"
        -f "$TARGET_FRAMEWORK"
        "-v:$MSBUILD_VERBOSITY"
    )
    append_extra_msbuild_props cmd

    echo "Platform         : Mac Catalyst"
    echo "Target framework : $TARGET_FRAMEWORK"
    echo "Configuration    : $CONFIGURATION"
    echo "Application id   : $APPLICATION_ID"
    echo

    if [[ "$FRESH_BUILD" == true ]]; then
        echo "Fresh build: cleaning…"
        dotnet clean "$DEMO_PROJECT" -c "$CONFIGURATION" -f "$TARGET_FRAMEWORK" "-v:$MSBUILD_VERBOSITY"
    fi

    echo "Building Mac Catalyst…"
    command "${cmd[@]}"

    echo "Launching Mac Catalyst…"
    command "${cmd[@]}" -t:Run
}

run_only() {
    case "$(entry_platform "$SELECTED_ENTRY")" in
        android)
            ADB_SERIAL="$(entry_id "$SELECTED_ENTRY")"
            setup_android_path
            local rid
            rid="$(resolve_android_runtime_identifier)"
            local -a cmd=(
                dotnet build "$DEMO_PROJECT"
                -c "$CONFIGURATION"
                -f "$TARGET_FRAMEWORK"
                -r "$rid"
                "-v:$MSBUILD_VERBOSITY"
                -p:"AdbTarget=-s $ADB_SERIAL"
                -p:"AndroidDevice=$ADB_SERIAL"
                -p:"Device=$ADB_SERIAL"
                -t:StartAndroidActivity
            )
            append_extra_msbuild_props cmd
            echo "Launching $APPLICATION_ID on Android ($ADB_SERIAL)…"
            command "${cmd[@]}"
            ;;
        ios)
            deploy_ios
            ;;
        maccatalyst)
            deploy_maccatalyst
            ;;
    esac
}

main() {
    parse_args "$@"

    if [[ ! -f "$DEMO_PROJECT" ]]; then
        echo "Demo project not found: $DEMO_PROJECT" >&2
        exit 1
    fi

    if ! command -v dotnet >/dev/null 2>&1; then
        echo "dotnet CLI not found in PATH" >&2
        exit 1
    fi

    if $CHECKLIST_ONLY; then
        print_checklist
        exit 0
    fi

    if $LIST_ONLY; then
        load_entries
        print_entries
        exit $?
    fi

    resolve_target
    prepare_android_avd_target
    resolve_target_framework
    read_application_id

    echo "Repo             : $REPO_ROOT"
    echo "Project          : $DEMO_PROJECT"
    echo "Selected         : $(platform_label "$(entry_platform "$SELECTED_ENTRY")") / $(entry_display_name "$SELECTED_ENTRY")"
    echo

    if $RUN_ONLY; then
        run_only
    else
        case "$(entry_platform "$SELECTED_ENTRY")" in
            android) deploy_android ;;
            ios) deploy_ios ;;
            maccatalyst) deploy_maccatalyst ;;
            *)
                echo "Unknown deploy target: $SELECTED_ENTRY" >&2
                exit 1
                ;;
        esac
    fi

    if $TAKE_SCREENSHOT; then
        # Give the UI a moment to appear
        sleep 2
        capture_screenshot || true
    fi

    if [[ "$NO_CHECKLIST" != true ]]; then
        print_checklist
    fi

    if [[ "$NO_LOGS" == true ]]; then
        echo
        echo "Done. App should be running — work through the checklist above."
        exit 0
    fi

    stream_app_logs
    echo
    echo "Log streaming stopped."
}

main "$@"
