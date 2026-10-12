#!/usr/bin/env python3
"""Merges the macOS and Windows packs of every SkiaUi package into multi-TFM packages (NuGet pack / publish workflows).

Usage: merge_nupkgs.py MACOS_DIR WINDOWS_DIR OUT_DIR

Each .nupkg / .snupkg of MACOS_DIR (net10.0, Android, iOS, Mac Catalyst) is written to OUT_DIR with the Windows pack of the
same file name (net10.0-windows) merged in: its lib/, ref/ and runtimes/ entries, the nuspec's dependency and framework
reference groups of the target frameworks the macOS pack lacks, and the content types of new file extensions. Other metadata
(id, version, readme, icon) is the macOS pack's. Both folders must hold the same packages (SkiaUi.Maui,
SkiaUi.Maui.CommunityToolkit, ...), and every Windows pack must add a Windows target framework, or the merge fails.
"""

import pathlib
import sys
import xml.etree.ElementTree as ET
import zipfile

PAYLOAD = ("lib/", "ref/", "runtimes/")
CONTENT_TYPES = "[Content_Types].xml"
CONTENT_TYPES_NS = "http://schemas.openxmlformats.org/package/2006/content-types"
# nuspec sections whose <group targetFramework="..."> children are per target framework.
GROUPED_SECTIONS = ("dependencies", "frameworkReferences", "frameworkAssemblies")


def namespace(element: ET.Element) -> str:
    return element.tag[1:].split("}")[0] if element.tag.startswith("{") else ""


def qualified(ns: str, name: str) -> str:
    return f"{{{ns}}}{name}" if ns else name


def to_bytes(root: ET.Element) -> bytes:
    ET.register_namespace("", namespace(root))
    return ET.tostring(root, encoding="utf-8", xml_declaration=True)


def merge_nuspec(base: bytes, windows: bytes, package: str) -> bytes:
    """The macOS nuspec with the Windows nuspec's groups for target frameworks it has none for."""
    base_root = ET.fromstring(base)
    win_root = ET.fromstring(windows)
    ns = namespace(base_root)
    base_meta = base_root.find(qualified(ns, "metadata"))
    win_meta = win_root.find(qualified(namespace(win_root), "metadata"))
    if base_meta is None or win_meta is None:
        raise SystemExit(f"{package}: a nuspec has no <metadata>")
    for section in GROUPED_SECTIONS:
        win_section = win_meta.find(qualified(namespace(win_root), section))
        if win_section is None:
            continue
        base_section = base_meta.find(qualified(ns, section))
        if base_section is None:
            base_section = ET.SubElement(base_meta, qualified(ns, section))
        known = {group.get("targetFramework", "").lower() for group in base_section}
        for group in win_section:
            framework = group.get("targetFramework", "")
            if framework.lower() in known:
                continue
            # Re-qualify into the macOS nuspec's namespace (the two packs may use different schema versions).
            copy = ET.fromstring(ET.tostring(group))
            for element in copy.iter():
                element.tag = qualified(ns, element.tag.split("}")[-1])
            base_section.append(copy)
            print(f"merged {package}: {section} group {framework}")
    return to_bytes(base_root)


def merge_content_types(base: bytes, windows: bytes) -> bytes:
    """The macOS content types with a default for every extension only the Windows pack has."""
    base_root = ET.fromstring(base)
    win_root = ET.fromstring(windows)
    default = qualified(CONTENT_TYPES_NS, "Default")
    known = {element.get("Extension", "").lower() for element in base_root.iter(default)}
    for element in win_root.iter(default):
        if element.get("Extension", "").lower() not in known:
            base_root.append(ET.fromstring(ET.tostring(element)))
    return to_bytes(base_root)


def is_windows_payload(name: str) -> bool:
    parts = name.split("/")
    return name.startswith(PAYLOAD) and any("-windows" in part for part in parts[1:3])


def merge(base_path: pathlib.Path, win_path: pathlib.Path, dest: pathlib.Path) -> None:
    package = base_path.name
    with zipfile.ZipFile(base_path) as zbase, zipfile.ZipFile(win_path) as zwin, \
            zipfile.ZipFile(dest, "w", zipfile.ZIP_DEFLATED) as zout:
        base_names = set(zbase.namelist())
        win_names = set(zwin.namelist())
        nuspecs = [name for name in base_names if "/" not in name and name.endswith(".nuspec")]
        win_nuspecs = [name for name in win_names if "/" not in name and name.endswith(".nuspec")]
        for info in zbase.infolist():
            data = zbase.read(info.filename)
            if info.filename in nuspecs and win_nuspecs:
                data = merge_nuspec(data, zwin.read(win_nuspecs[0]), package)
            elif info.filename == CONTENT_TYPES and CONTENT_TYPES in win_names:
                data = merge_content_types(data, zwin.read(CONTENT_TYPES))
            zout.writestr(info, data)
        merged = 0
        for info in zwin.infolist():
            # The Windows TFM's payload; metadata (nuspec, readme, icon) is the macOS pack's (merged above).
            if info.filename not in base_names and info.filename.startswith(PAYLOAD):
                zout.writestr(info, zwin.read(info.filename))
                merged += is_windows_payload(info.filename)
                print(f"merged {package}: {info.filename}")
    if merged == 0:
        dest.unlink()
        raise SystemExit(f"{package}: the Windows pack added no net*-windows* files under {', '.join(PAYLOAD)}")
    print(f"wrote {dest}")


def main(macos_dir: pathlib.Path, win_dir: pathlib.Path, out_dir: pathlib.Path) -> None:
    out_dir.mkdir(parents=True, exist_ok=True)
    macos = sorted(path for path in macos_dir.iterdir() if path.suffix in (".nupkg", ".snupkg"))
    windows = {path.name for path in win_dir.iterdir() if path.suffix in (".nupkg", ".snupkg")}
    if not macos:
        raise SystemExit(f"No packages under {macos_dir}")
    if {path.name for path in macos} != windows:
        raise SystemExit(f"The macOS and Windows packs differ: {sorted(path.name for path in macos)} vs {sorted(windows)}")
    for base in macos:
        merge(base, win_dir / base.name, out_dir / base.name)


if __name__ == "__main__":
    if len(sys.argv) != 4:
        raise SystemExit(__doc__)
    main(*(pathlib.Path(arg) for arg in sys.argv[1:]))
