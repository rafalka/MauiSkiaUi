using System.Diagnostics;
using System.Reflection;

namespace MauiSkiaUiSamples;

/// <summary>
/// Opens an example's source file in the IDE that built the app, on Mac Catalyst and Windows (running from an IDE is
/// the usual case). The project records that IDE at build time (assembly metadata, see MauiSkiaUiSamples.csproj):
/// <list type="bullet">
/// <item>macOS: the bundle id of the app that started the build (Rider, Visual Studio Code, Cursor, …); the file is
/// opened with <c>open -b &lt;id&gt;</c>. Built from a terminal: the default app for <c>.cs</c> files.</item>
/// <item>Windows: Visual Studio (<c>devenv /edit</c>), Visual Studio Code (<c>vscode://file/</c>), else the default app.</item>
/// </list>
/// The Mac Catalyst app is not sandboxed (Platforms/MacCatalyst/Entitlements.plist) so it can see and open the file.
/// </summary>
public static class SourceEditor
{
    private static readonly Dictionary<string, string> KnownApps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["com.jetbrains.rider"] = "Rider",
        ["com.microsoft.VSCode"] = "Visual Studio Code",
        ["com.todesktop.230313mzl4w4u92"] = "Cursor",
        ["com.microsoft.visual-studio"] = "Visual Studio",
        ["com.apple.dt.Xcode"] = "Xcode"
    };

    /// <summary>Terminals start builds too, but are no editors: open the file in the default app instead.</summary>
    private static readonly HashSet<string> Terminals = new(StringComparer.OrdinalIgnoreCase)
    {
        "com.apple.Terminal", "com.googlecode.iterm2", "dev.warp.Warp-Stable", "net.kovidgoyal.kitty",
        "com.github.wez.wezterm", "io.alacritty", "com.mitchellh.ghostty"
    };

    /// <summary>The toolbar title for opening <paramref name="path"/>, or <c>null</c> when it cannot be opened here.</summary>
    public static string? ActionTitle(string path)
    {
#if MACCATALYST || WINDOWS
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;
        return EditorName is { } name ? $"Open in {name}" : "Open source file";
#else
        return null;
#endif
    }

    /// <summary>Opens <paramref name="path"/> in the IDE that built the app (or the default app).</summary>
    public static async Task OpenAsync(string path)
    {
        try
        {
#if MACCATALYST
            string[] arguments = Metadata("SampleBuildAppId") is { } id && !Terminals.Contains(id) ? ["-b", id, path] : [path];
            // Mac Catalyst runs .NET's Unix process code, so starting `open` works; the platform analyzer only sees an
            // iOS-like target.
#pragma warning disable CA1416
            using var process = Process.Start("/usr/bin/open", arguments);
#pragma warning restore CA1416
#elif WINDOWS
            if (Metadata("SampleBuildDevEnv") is { } devEnvDir)
                using (Process.Start(Path.Combine(devEnvDir, "devenv.exe"), ["/edit", path])) { }
            else if (Metadata("SampleBuildVsCode") is not null)
            {
                // Started from VS Code's debugger, this process inherits the extension host's ELECTRON_RUN_AS_NODE=1,
                // and so would the Code.exe the shell starts for the URL: it would run as plain Node.js and exit.
                Environment.SetEnvironmentVariable("ELECTRON_RUN_AS_NODE", null);
                using (Process.Start(new ProcessStartInfo("vscode://file/" + path.Replace('\\', '/')) { UseShellExecute = true })) { }
            }
            else
                using (Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })) { }
#endif
        }
        catch (Exception error)
        {
            if (Application.Current?.Windows.FirstOrDefault()?.Page is { } page)
                await page.DisplayAlertAsync("Cannot open the source file", error.Message, "OK");
        }
        await Task.CompletedTask;
    }

    private static string? EditorName
    {
        get
        {
#if MACCATALYST
            return Metadata("SampleBuildAppId") is { } id && KnownApps.TryGetValue(id, out var name) ? name : null;
#elif WINDOWS
            return Metadata("SampleBuildDevEnv") is not null ? "Visual Studio"
                : Metadata("SampleBuildVsCode") is not null ? "Visual Studio Code"
                : null;
#else
            return null;
#endif
        }
    }

    private static string? Metadata(string key) =>
        typeof(SourceEditor).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == key)?.Value is { Length: > 0 } value ? value : null;
}
