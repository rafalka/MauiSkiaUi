using System.Diagnostics;
using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUiTour;

/// <summary>
/// Scripted smoke run (<c>SKUI_TOUR=1</c>): opens every page of an app in turn, scrolls its scrollers to the end and
/// back, and writes every exception (also first-chance ones the code catches) to stdout with the page it happened on.
/// <c>SKUI_TOUR_DWELL</c>: milliseconds each page stays open (default 1200). The app quits when done.
/// <see href="../../../scripts/page_tour.sh">scripts/page_tour.sh</see> runs it on Windows and summarizes the log.
/// Linked into the demo and samples apps.
/// </summary>
internal static class PageTour
{
    private const string FirstChance = "first-chance";
    private const int DefaultDwell = 1200;
    private static readonly object Gate = new();
    [ThreadStatic] private static bool _inLog;
    private static string _page = "startup";
    private static bool _started;

    public static bool Requested { get; } = Environment.GetEnvironmentVariable("SKUI_TOUR") == "1";

    /// <summary>One page: <paramref name="Open"/> navigates to it, <paramref name="Close"/> (optional) back.</summary>
    public sealed record Step(string Name, Func<Task> Open, Func<Task>? Close = null);

    /// <summary>Logs exceptions from now on; call first thing in <c>CreateMauiApp</c>, so startup is covered too.</summary>
    public static void HookExceptionsIfRequested()
    {
        if (!Requested) return;
        AppDomain.CurrentDomain.FirstChanceException += (_, e) => Log(FirstChance, e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log("UNHANDLED", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log("UNOBSERVED", e.Exception);
            e.SetObserved();
        };
    }

    /// <summary>Runs <paramref name="steps"/> once <paramref name="shell"/> is loaded.</summary>
    public static void StartIfRequested(Shell shell, Func<IReadOnlyList<Step>> steps)
    {
        if (Requested)
            shell.Loaded += (_, _) => Run(shell, steps());
    }

    private static async void Run(Shell shell, IReadOnlyList<Step> steps)
    {
        if (_started) return;
        _started = true;
#if WINDOWS
        // Keeps the tour going after an exception that would otherwise end the app.
        Microsoft.UI.Xaml.Application.Current.UnhandledException += (_, e) =>
        {
            Log("UNHANDLED-UI", e.Exception);
            e.Handled = true;
        };
#endif
        var dwell = int.TryParse(Environment.GetEnvironmentVariable("SKUI_TOUR_DWELL"), out var ms) ? ms : DefaultDwell;
        Console.WriteLine($"[Tour] steps {steps.Count}");
        await Task.Delay(1500);
        foreach (var step in steps)
        {
            _page = step.Name;
            Console.WriteLine($"[Tour] open {step.Name}");
            try
            {
                await step.Open();
                await Task.Delay(dwell);
                await ScrollAll(shell.CurrentPage);
                await Task.Delay(300);
                if (step.Close is not null)
                    await step.Close();
            }
            catch (Exception ex)
            {
                Log("STEP-FAILED", ex);
            }
        }
        _page = "end";
        Console.WriteLine("[Tour] done");
        await Task.Delay(500);
        Application.Current?.Quit();
    }

    private static void Log(string kind, Exception? ex)
    {
        if (_inLog || ex is null) return;
        _inLog = true;
        try
        {
            // A first-chance exception's own trace has only the throwing frame: the current stack shows who threw it.
            // Cancellations are frequent and known by their type alone (page_tour.sh): no stack, whose line numbers read PDBs.
            var firstChance = kind == FirstChance;
            var stack = "";
            if (!firstChance || ex is not OperationCanceledException)
            {
                var trace = firstChance ? new StackTrace(2, true).ToString() : new StackTrace(ex, true).ToString();
                stack = "\n" + string.Join("\n", trace.Split('\n').Take(firstChance ? 25 : 14));
            }
            lock (Gate)
                Console.WriteLine($"[Tour] {kind} on '{_page}' thread {Environment.CurrentManagedThreadId}: {ex.GetType().FullName}: {ex.Message}{stack}\n[Tour] end");
        }
        catch
        {
            // Logging must never throw into the code that raised the exception.
        }
        finally
        {
            _inLog = false;
        }
    }

    private static async Task ScrollAll(Page? page)
    {
        if (page is null) return;
        foreach (var element in Descendants(page))
        {
            try
            {
                switch (element)
                {
                    // Offsets past the end are clamped, on both axes (also the one a scroller does not scroll).
                    case SkUiScrollView scroll:
                        await scroll.ScrollToAsync(1e6, 1e6, false);
                        await Task.Delay(150);
                        await scroll.ScrollToAsync(0, 0, false);
                        break;
                    case SkUiCoreScrollView scroll:
                        await scroll.ScrollToAsync(1e6, 1e6, false);
                        await Task.Delay(150);
                        await scroll.ScrollToAsync(0, 0, false);
                        break;
                    case SkUiCollectionView list:
                        await list.ScrollToAsync(1e6, false);
                        await Task.Delay(150);
                        await list.ScrollToAsync(0, false);
                        break;
                    case SkUiCarouselView carousel when carousel.ItemCount > 1:
                        var position = carousel.Position;
                        await carousel.ScrollToIndex(carousel.ItemCount - 1, false);
                        await Task.Delay(150);
                        await carousel.ScrollToIndex(Math.Clamp(position, 0, carousel.ItemCount - 1), false);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log("SCROLL-FAILED", ex);
            }
        }
    }

    /// <summary>The visual tree: also the Core nodes under an <see cref="SkUiCoreHost"/>, which are not logical children.</summary>
    private static IEnumerable<IVisualTreeElement> Descendants(IVisualTreeElement root)
    {
        var stack = new Stack<IVisualTreeElement>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var element = stack.Pop();
            yield return element;
            foreach (var child in element.GetVisualChildren().ToList())
                stack.Push(child);
        }
    }
}
