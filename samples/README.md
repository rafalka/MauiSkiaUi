# SkiaUi samples app

`samples/MauiSkiaUiSamples` (app id `com.rkdevel.skiauisamples`) shows **how to use** SkiaUi, one example per page, grouped in sections (Customisation, …). The demo app (`MauiSkiaUiDemo`) is the component gallery for exploring and verifying controls; this app is for learning how to build things with them.

## How an example is organised

Each example is **one source file** under `Samples/<Section>/`. The file holds:

1. **`public static SampleInfo Info`** at the top. The app displays it:
   - `Title`: the page title.
   - `Summary`: one or two sentences, shown under the title and in the list.
   - `HowTo`: numbered steps, in a collapsible panel.
   - `ThingsToKnow`: caveats, alternatives and easy-to-miss behavior, in a collapsible panel.

   Plain text; wrap code in backticks (`` `SkUiLook.Current` ``) to show it in a monospace font. Keep it short.
2. **XML and code comments** with the full details, for people reading the file.
3. **The page** (`SamplePage` subclass implementing `ISample`) and whatever the example needs (a look, a control).

The project embeds every file under `Samples/`, and each page's **Source** button shows its own file. What people read in the app is the code that runs.

**XAML examples** are a XAML page and its code-behind (`<Name>Sample.xaml` + `<Name>Sample.xaml.cs`): the page's root is `samples:SamplePage`, its content is the live example (`SamplePage.SampleContent` is the content property), and `Info` with whatever the example needs lives in the code-behind. The Source page shows the XAML first, with a toolbar button to switch to the code-behind. XAML is compiled by the source generator (`MauiXamlInflator=SourceGen`), in the app and in the test project.

**The Source page:**
- **Highlighting:** the file is highlighted with [ColorCode](https://github.com/CommunityToolkit/ColorCode-Universal) (C# → HTML) in a web view: it scrolls both ways and text can be selected. The page title is the file name.
- **Which file:** `SampleInfo` captures the example's source path with `[CallerFilePath]`. Its file name finds the embedded copy, and the full path lets the page open it.
- **"Open in …":** on Mac Catalyst and Windows, when that file exists (the app runs on the machine that built it), a toolbar button opens it in the IDE that built the app:
  - The build records that IDE as assembly metadata (see `MauiSkiaUiSamples.csproj` and `Infrastructure/SourceEditor.cs`).
  - **macOS:** the bundle id of the app that started the build (Rider, Visual Studio Code, Cursor, Xcode…), opened with `open -b`.
  - **Windows:** Visual Studio (`devenv /edit`) or Visual Studio Code (`vscode://file/`).
  - **Otherwise:** a terminal build, or an unknown IDE, opens the file in the default app for `.cs` files.
  - The Mac Catalyst app is **not sandboxed** for this. It is a development tool, not for the Mac App Store.

Why plain strings instead of HTML:
- they render with native labels on every platform, with no web view and no escaping;
- they stay readable in the source;
- a test checks them: every example has all four parts, balanced backticks, and an embedded source.

## Adding an example

1. Add `Samples/<Section>/<Name>Sample.cs` with a `public sealed class <Name>Sample : SamplePage, ISample`. Declare `Info` first, pass it to `base(Info)` and set `SampleContent`. Create `Info` in the example's own file, so `[CallerFilePath]` records that file. For a XAML example, add `<Name>Sample.xaml` (root `<samples:SamplePage x:Class="MauiSkiaUiSamples.Samples.<Section>.<Name>Sample">`) and declare `Info` in `<Name>Sample.xaml.cs`, whose constructor calls `base(Info)` and `InitializeComponent()`.
2. Register it in `Infrastructure/SampleCatalog.cs`: `SampleEntry.For<NameSample>()`. The list is explicit, with no reflection, so trimming and Native AOT stay safe.
3. A new section goes in `SampleSection` (`DisplayName`, `Description`). Sections without examples are hidden.
4. If the example uses its own look, override `Look`. The page makes it current only while it is shown, because looks are app-wide.
5. Run `dotnet test tests/MauiSkiaUi.Tests`. `SamplesTests` builds every example headlessly (the test project links `Infrastructure/` and `Samples/`); add a test for what your example shows if it can break.

## Running

```bash
dotnet build samples/MauiSkiaUiSamples/MauiSkiaUiSamples.csproj -f net10.0-maccatalyst -t:Run
dotnet build samples/MauiSkiaUiSamples/MauiSkiaUiSamples.csproj -f net10.0-android -t:Run
```

`-t:Run` can launch the previous build without rebuilding. After changes, build first, then run.
