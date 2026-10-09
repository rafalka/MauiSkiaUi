using System.Runtime.CompilerServices;

namespace MauiSkiaUi.LeakTests;

/// <summary>A weakly tracked object and what it is, for failure reports.</summary>
public sealed record TrackedObject(WeakReference Reference, string Label);

/// <summary>Weak tracking of objects that must be collected, and the GC loop that checks them.</summary>
public static class LeakTracker
{
    /// <summary>Creates a tracked entry; the label defaults to the type name plus an automation id / text.</summary>
    public static TrackedObject Track(object instance, string? label = null) =>
        new(new WeakReference(instance), label ?? Describe(instance));

    /// <summary>
    /// Tracks <paramref name="root"/> and everything in its visual tree (MAUI views, drawn SkUi* views, Core nodes),
    /// plus each element's handler and platform view and what surface handlers own (renderer, compositor, platform
    /// surfaces, overlay container). Elements <paramref name="exclude"/> accepts (long-lived ones) are skipped.
    /// </summary>
    public static List<TrackedObject> TrackTree(IVisualTreeElement root, Func<object, bool>? exclude = null)
    {
        var tracked = new List<TrackedObject>();
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        void Add(object instance, string? label = null)
        {
            if (seen.Add(instance))
                tracked.Add(Track(instance, label));
        }
        foreach (var element in root.GetVisualTreeDescendants().Prepend(root))
        {
            if (exclude?.Invoke(element) == true)
                continue;
            Add(element);
            if (element is not IElement { Handler: { } handler })
                continue;
            Add(handler, $"{handler.GetType().Name} of {Describe(element)}");
            if (handler.PlatformView is { } platformView)
                Add(platformView, $"{platformView.GetType().Name} of {Describe(element)}");
#if ANDROID || IOS || MACCATALYST || WINDOWS
            if (handler is MauiSkiaUi.SkUiViewHandler surfaceHandler)
                foreach (var (instance, label) in surfaceHandler.GetOwnedObjects())
                    Add(instance, $"{label} of {Describe(element)}");
#endif
        }
        return tracked;
    }

    /// <summary>A short description: type plus automation id or text when present.</summary>
    public static string Describe(object instance)
    {
        var type = instance.GetType().Name;
        var detail = instance switch
        {
            VisualElement { AutomationId: { Length: > 0 } id } => id,
            InputView { Placeholder: { Length: > 0 } placeholder } => placeholder,
            MauiSkiaUi.SkUiLabel { Text: { Length: > 0 } text } => text,
            MauiSkiaUi.Core.SkUiCoreLabel { Text: { Length: > 0 } text } => text,
            _ => null
        };
        return detail is null ? type : $"{type} '{Truncate(detail)}'";
    }

    /// <summary>
    /// Collects garbage until every entry is dead or <paramref name="timeout"/> passes; returns the survivors' labels
    /// (distinct, with counts). Waits between attempts, so native peers released asynchronously (Apple
    /// <c>NSObject</c> finalization on the main thread, the Android GC bridge) get their turn.
    /// </summary>
    public static async Task<IReadOnlyList<string>> WaitForCollectionAsync(IReadOnlyCollection<TrackedObject> tracked, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            FullCollect();
            if (!tracked.Any(entry => entry.Reference.IsAlive) || DateTime.UtcNow >= deadline)
                break;
            await Task.Delay(250);
        }
        return Survivors(tracked);
    }

    /// <summary>
    /// Synchronous variant for headless tests: full collections for up to <paramref name="timeoutMilliseconds"/> of
    /// real time. Background work (an image decode on the thread pool, a finalizer) can hold an object briefly on a
    /// slow machine; a leak keeps it for good.
    /// </summary>
    public static IReadOnlyList<string> CollectNow(IReadOnlyCollection<TrackedObject> tracked, int timeoutMilliseconds = 2000)
    {
        var deadline = Environment.TickCount64 + timeoutMilliseconds;
        FullCollect();
        while (tracked.Any(entry => entry.Reference.IsAlive) && Environment.TickCount64 < deadline)
        {
            Thread.Sleep(20);
            FullCollect();
        }
        return Survivors(tracked);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FullCollect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static IReadOnlyList<string> Survivors(IEnumerable<TrackedObject> tracked) =>
        tracked.Where(entry => entry.Reference.IsAlive)
            .GroupBy(entry => entry.Label)
            .Select(group => group.Count() == 1 ? group.Key : $"{group.Key} ×{group.Count()}")
            .ToList();

    private static string Truncate(string value) => value.Length <= 24 ? value : value[..23] + "…";
}
