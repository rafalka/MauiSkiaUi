using System.Collections.Concurrent;
using System.Diagnostics;
using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// Writes <see cref="SkUiDiagnostics"/> input / scrolling traces to a file, for diagnosing behavior on devices we
/// cannot debug. Enabled by a <c>trace.on</c> file next to the executable or <c>SKUI_TRACE=1</c>. The log goes to
/// <c>logs/skui-trace-*.log</c> next to the executable (the app data folder when that is not writable).
/// </summary>
internal static class DemoTrace
{
    private static readonly BlockingCollection<string> s_lines = new(new ConcurrentQueue<string>(), 100_000);
    private static readonly Stopwatch s_clock = Stopwatch.StartNew();

    public static string? LogPath { get; private set; }

    public static void StartIfRequested()
    {
        if (Environment.GetEnvironmentVariable("SKUI_TRACE") != "1"
            && !File.Exists(Path.Combine(AppContext.BaseDirectory, "trace.on")))
            return;
        var name = $"skui-trace-{DateTime.Now:yyyyMMdd-HHmmss}.log";
        StreamWriter? writer = null;
        foreach (var folder in new[] { Path.Combine(AppContext.BaseDirectory, "logs"), Path.Combine(FileSystem.AppDataDirectory, "logs") })
        {
            try
            {
                Directory.CreateDirectory(folder);
                LogPath = Path.Combine(folder, name);
                // Readable while the app runs.
                writer = new StreamWriter(new FileStream(LogPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = false };
                break;
            }
            catch (Exception)
            {
                writer = null; // not writable: next folder
            }
        }
        if (writer is null)
            return;
        var thread = new Thread(() => WriteLoop(writer)) { IsBackground = true, Name = "SkUi trace writer" };
        thread.Start();
        SkUiDiagnostics.Trace = Enqueue;
        Enqueue($"trace start {DateTime.Now:O} version {typeof(DemoTrace).Assembly.GetName().Version} os {Environment.OSVersion}");
    }

    public static void Enqueue(string message) =>
        s_lines.TryAdd($"{s_clock.Elapsed.TotalMilliseconds,10:F1} [{Environment.CurrentManagedThreadId,2}] {message}");

    private static void WriteLoop(StreamWriter writer)
    {
        foreach (var line in s_lines.GetConsumingEnumerable())
        {
            writer.WriteLine(line);
            // Flush when the queue drains, so the file is current if the app is closed or crashes.
            if (s_lines.Count == 0)
                writer.Flush();
        }
    }
}
