#!/usr/bin/env bash
#
# Consumer check of packed SkiaUi packages: restores them into a minimal Android app of one .NET line, with that line's SDK
# (net10: SDK 10 through scripts/sdk10, as .NET 10 apps build; net11: global.json's SDK 11), and asserts that the app gets
# that line's assets (lib/netX.0-android*) and MAUI (Microsoft.Maui.Controls X.*), so a net10 app never pulls MAUI 11.
# Needs the maui-android workload of that SDK. The app lives in a temporary folder outside the repository (no
# Directory.Build.props) and is only restored, not built. It does not set UseMaui: that adds an implicit
# Microsoft.Maui.Controls reference at the workload's own version (10.0.110 / 11.0.0, below the packages' floors).
#
#   scripts/consumer_check.sh -f artifacts/nuget --net 10
#   scripts/consumer_check.sh -f artifacts/nuget --net 11
#
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FEED=""
NET=""

usage() {
    sed -n '3,11p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
    cat <<'EOF'
Options:
  -f, --feed DIR   folder with the SkiaUi .nupkg files
  --net 10|11      the app's .NET line
  -h, --help
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        -f|--feed) FEED="$2"; shift 2 ;;
        --net) NET="$2"; shift 2 ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Unknown option: $1" >&2; usage >&2; exit 2 ;;
    esac
done
[[ -n "$FEED" && -d "$FEED" ]] || { echo "--feed must be a folder with the packages" >&2; exit 2; }
[[ "$NET" =~ ^1[01]$ ]] || { echo "--net takes 10 or 11" >&2; exit 2; }
FEED="$(cd "$FEED" && pwd)"

nupkg="$(find "$FEED" -maxdepth 1 -name 'SkiaUi.Maui.[0-9]*.nupkg' ! -name '*.symbols.nupkg' | sort -V | tail -1)"
[[ -n "$nupkg" ]] || { echo "No SkiaUi.Maui package in $FEED" >&2; exit 1; }
version="$(basename "$nupkg" .nupkg)"
version="${version#SkiaUi.Maui.}"

app="$(mktemp -d)"
trap 'rm -rf "$app"' EXIT
if [[ "$NET" == 10 ]]; then cp "$REPO_ROOT/scripts/sdk10/global.json" "$app/"; else cp "$REPO_ROOT/global.json" "$app/"; fi
cat >"$app/nuget.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$FEED" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
EOF
cat >"$app/ConsumerCheck.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net$NET.0-android</TargetFramework>
    <SupportedOSPlatformVersion>24.0</SupportedOSPlatformVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="SkiaUi.Maui" Version="$version" />
    <PackageReference Include="SkiaUi.Maui.CommunityToolkit" Version="$version" />
    <PackageReference Include="SkiaUi.Maui.Markdown.Math" Version="$version" />
  </ItemGroup>
</Project>
EOF

cd "$app"
sdk="$(dotnet --version)"
[[ "$sdk" == "$NET".* ]] || { echo "net$NET consumer check needs SDK $NET (got $sdk)" >&2; exit 1; }
echo "Consumer check: net$NET.0-android app, SDK $sdk, SkiaUi $version from $FEED"
# A fresh package folder: a cached SkiaUi of the same version from an earlier pack would hide this one.
export NUGET_PACKAGES="$app/packages"
dotnet restore -v q -nologo

python3 - "$app/obj/project.assets.json" "$NET" "$version" <<'PY'
import json, sys
assets, net, version = sys.argv[1], sys.argv[2], sys.argv[3]
data = json.load(open(assets))
(target_name, target), = data["targets"].items()
failures = []
for package in ("SkiaUi.Maui", "SkiaUi.Maui.CommunityToolkit", "SkiaUi.Maui.Markdown", "SkiaUi.Maui.Markdown.Math"):
    entry = target.get(f"{package}/{version}")
    if entry is None:
        failures.append(f"{package} {version} not restored")
        continue
    dlls = [path for path in entry.get("compile", {}) if path.endswith(".dll")]
    wrong = [path for path in dlls if not path.startswith(f"lib/net{net}.0-android")]
    if not dlls or wrong:
        failures.append(f"{package}: compile assets {dlls}, expected lib/net{net}.0-android*")
    else:
        print(f"  {package}: {dlls[0]}")
mauis = [key.split("/")[1] for key in target if key.startswith("Microsoft.Maui.Controls.Core/")]
if [v for v in mauis if not v.startswith(f"{net}.")] or not mauis:
    failures.append(f"Microsoft.Maui.Controls.Core {mauis}, expected {net}.*")
else:
    print(f"  Microsoft.Maui.Controls.Core {mauis[0]} ({target_name})")
if failures:
    print("Consumer check failed:\n  " + "\n  ".join(failures), file=sys.stderr)
    sys.exit(1)
print(f"net{net} app gets the net{net} assets and MAUI {net}.")
PY
