# .NET 10 line

The repository's `global.json` selects SDK 11, which builds both lines (net10 with MAUI 10, net11 with MAUI 11). This folder is for working on the **.NET 10 line only**, with SDK 10, including on machines that have no SDK 11:

- `global.json` selects SDK 10.0.4xx (the newest installed patch of 10.0.400).
- `SkiaUi.Net10.slnx` lists the same projects as `../SkiaUi.slnx`. On SDK 10, `Directory.Build.props` builds only the net10 TFMs (MAUI 10, Community Toolkit 15), including the net10 iOS and Mac Catalyst app heads, which only SDK 10's workload builds with Xcode 27.2.

## Opening it

- **Visual Studio, Rider:** open `net10/SkiaUi.Net10.slnx`. The IDE takes the SDK from the `global.json` next to the solution.
- **VS Code (C# Dev Kit):** open the `net10` folder, so the workspace's `global.json` is this one.
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

The device scripts use this folder by themselves for net10 iOS / Mac Catalyst apps (`--net 10`), and so does `scripts/consumer_check.sh --net 10`.

## Keeping it in sync

`SkiaUi.Net10.slnx` is generated. After adding or removing a project in `../SkiaUi.slnx`, run:

```bash
python3 scripts/net10_solution.py
```

CI fails when it is out of date (`--check`).
