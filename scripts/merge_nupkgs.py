#!/usr/bin/env python3
"""Merges the macOS and Windows packs of every SkiaUi package into multi-TFM packages (NuGet pack / publish workflows).

Usage: merge_nupkgs.py MACOS_DIR WINDOWS_DIR OUT_DIR

Each .nupkg / .snupkg of MACOS_DIR (net10.0, Android, iOS, Mac Catalyst) is copied to OUT_DIR with the lib/, ref/ and
runtimes/ entries of the Windows pack of the same file name (net10.0-windows) added. Both folders must hold the same
packages (SkiaUi.Maui, SkiaUi.Maui.CommunityToolkit, ...).
"""

import pathlib
import shutil
import sys
import zipfile

PAYLOAD = ("lib/", "ref/", "runtimes/")


def main(macos_dir: pathlib.Path, win_dir: pathlib.Path, out_dir: pathlib.Path) -> None:
    out_dir.mkdir(parents=True, exist_ok=True)
    macos = sorted(path for path in macos_dir.iterdir() if path.suffix in (".nupkg", ".snupkg"))
    windows = {path.name for path in win_dir.iterdir() if path.suffix in (".nupkg", ".snupkg")}
    if not macos:
        raise SystemExit(f"No packages under {macos_dir}")
    if {path.name for path in macos} != windows:
        raise SystemExit(f"The macOS and Windows packs differ: {sorted(path.name for path in macos)} vs {sorted(windows)}")
    for base in macos:
        dest = out_dir / base.name
        shutil.copy2(base, dest)
        with zipfile.ZipFile(dest, "a") as zdst, zipfile.ZipFile(win_dir / base.name, "r") as zsrc:
            existing = set(zdst.namelist())
            for info in zsrc.infolist():
                # The Windows TFM's payload; metadata (nuspec, readme, icon) is the macOS pack's.
                if info.filename not in existing and info.filename.startswith(PAYLOAD):
                    zdst.writestr(info, zsrc.read(info.filename))
                    print(f"merged {base.name}: {info.filename}")
        print(f"wrote {dest}")


if __name__ == "__main__":
    if len(sys.argv) != 4:
        raise SystemExit(__doc__)
    main(*(pathlib.Path(arg) for arg in sys.argv[1:]))
