# .NET 10 line

The repository's `global.json` selects SDK 11, which builds both lines (net10 with MAUI 10, net11 with MAUI 11). This folder is for working on the **.NET 10 line only**, with SDK 10, including on machines that have no SDK 11:

- `global.json` selects SDK 10.0.4xx (the newest installed patch of 10.0.400).
- `SkiaUi.Net10.slnx` lists the same projects as `../SkiaUi.slnx`. On SDK 10, `Directory.Build.props` builds only the net10 TFMs (MAUI 10, Community Toolkit 15), including the net10 iOS and Mac Catalyst app heads, which only SDK 10's workload builds with Xcode 27.2.

## Opening it

- **Rider:** open `net10/SkiaUi.Net10.slnx`. It takes the SDK from the `global.json` next to the solution (checked).
- **Visual Studio:** open `net10/SkiaUi.Net10.slnx`; it should take the SDK the same way (not checked yet).
- **VS Code (C# Dev Kit):** it builds with the SDK the repository root's `global.json` selects, whichever solution is open, and opening the `net10` folder shows only this folder. Copy this folder's `global.json` over the root one, reload the window and keep the root folder open (`SkiaUi.slnx` then builds the net10 line only); put it back before committing:

  ```bash
  cp net10/global.json global.json        # .NET 10 line
  git restore global.json                 # back to SDK 11
  ```
- **Command line:** run `dotnet` from this folder; the host reads `global.json` from the current directory, not from the solution's path:

  ```bash
  cd net10
  dotnet build SkiaUi.Net10.slnx
  dotnet test ../tests/MauiSkiaUi.Tests/MauiSkiaUi.Tests.csproj
  ```

The MAUI workload must be installed for SDK 10 (workload set 10.0.404, Xcode 27.2). Install it from this folder, so the command runs under SDK 10 and updates SDK 10's band:

```bash
cd net10
sudo dotnet workload install maui --version 10.0.404
```

The device scripts (`scripts/device_tests.sh`, `scripts/device_verify.sh`, `scripts/bench.sh`) use this folder by themselves for net10 iOS / Mac Catalyst apps (`--net 10`), and so does `scripts/consumer_check.sh --net 10`.

## Keeping it in sync

`SkiaUi.Net10.slnx` is generated. After adding or removing a project in `../SkiaUi.slnx`, run:

```bash
python3 scripts/net10_solution.py
```

CI fails when it is out of date (`--check`).
