using MauiSkiaUi;

namespace MauiSkiaUi.LeakTests;

/// <summary>
/// What a scenario run can do: track objects, and drive input and time the same way headless (pumped frames, a
/// manual gesture clock) and on a device (real frames and timers). Input goes through the surface root like a
/// platform touch (hit-testing, gesture arena, capture, press states).
/// </summary>
public sealed class LeakScenarioContext(Func<TimeSpan, Task> wait, bool isDevice)
{
    private static long s_nextPointer = long.MinValue / 4;
    private readonly List<TrackedObject> _tracked = [];
    private readonly List<TrackedObject> _detached = [];

    /// <summary>Running on a device with handlers and platform views (<c>false</c> in headless tests).</summary>
    public bool IsDevice { get; } = isDevice;

    /// <summary>Objects that must be collected after the page closes.</summary>
    public IReadOnlyList<TrackedObject> Tracked => _tracked;

    /// <summary>Objects removed during the run: they must be collected while the page is still open.</summary>
    public IReadOnlyList<TrackedObject> Detached => _detached;

    /// <summary>Tracks <paramref name="instance"/> (collected once the page closes) and returns it.</summary>
    public T Track<T>(T instance, string? label = null) where T : class
    {
        _tracked.Add(LeakTracker.Track(instance, label));
        return instance;
    }

    /// <summary>
    /// Tracks an object the run has removed from the UI (a child, a replaced surface): it must be collectable while
    /// the rest of the page is still alive. Catches leaks that grow for as long as a long-lived page lives.
    /// </summary>
    public void TrackDetached(object instance, string? label = null) => _detached.Add(LeakTracker.Track(instance, label));

    /// <summary>Lets time pass: real delay on a device; headless, advances the gesture clock and renders frames.</summary>
    public Task WaitAsync(double milliseconds) => wait(TimeSpan.FromMilliseconds(milliseconds));

    /// <summary>One frame's worth of time, so pending layout and commits are applied.</summary>
    public Task SettleAsync() => WaitAsync(IsDevice ? 60 : 16);

    /// <summary>
    /// Waits (rendering) until <paramref name="task"/> ends or <paramref name="timeoutMs"/> passes, without awaiting it
    /// directly. Headless frames take no real time, so work on other threads (image decoding) gets a real-time wait
    /// between frames as well.
    /// </summary>
    public async Task WaitForAsync(Task task, double timeoutMs = 3000)
    {
        for (double waited = 0; !task.IsCompleted && waited < timeoutMs; waited += 16)
        {
            if (!IsDevice)
                ((IAsyncResult)task).AsyncWaitHandle.WaitOne(16);
            await WaitAsync(16);
        }
    }

    /// <summary>A native text input was focused during the run (see <see cref="FocusAsync"/>).</summary>
    public bool FocusedTextInput { get; private set; }

    /// <summary>
    /// Focuses a native control, holds, and unfocuses it. The platform's text input system keeps the last focused
    /// field until another one gains focus (Android's InputMethodManager, MAUI's Apple keyboard helper; plain MAUI
    /// pages behave the same), so the device runner moves focus to its own field after closing the page, as the next
    /// screen of an app would.
    /// </summary>
    public async Task FocusAsync(VisualElement element, double holdMilliseconds = 300)
    {
        FocusedTextInput = true;
        element.Focus();
        await WaitAsync(holdMilliseconds);
        element.Unfocus();
        await WaitAsync(300);
    }

    /// <summary>Taps the center of a drawn element (SkUi* view or Core node).</summary>
    public async Task TapAsync(object element)
    {
        if (SkUiDiagnostics.SimulateTap(element) is null)
            throw new InvalidOperationException($"{LeakTracker.Describe(element)} is not on screen.");
        await SettleAsync();
    }

    /// <summary>Two taps within the double-tap timeout.</summary>
    public async Task DoubleTapAsync(object element)
    {
        var (root, center) = Locate(element);
        var id = ++s_nextPointer;
        var start = Now();
        Touch(root, id, SkUiTouchAction.Pressed, center, start);
        Touch(root, id, SkUiTouchAction.Released, center, start + TimeSpan.FromMilliseconds(40));
        await WaitAsync(60);
        id = ++s_nextPointer;
        Touch(root, id, SkUiTouchAction.Pressed, center, start + TimeSpan.FromMilliseconds(100));
        Touch(root, id, SkUiTouchAction.Released, center, start + TimeSpan.FromMilliseconds(140));
        await SettleAsync();
    }

    /// <summary>Press, hold past the long-press duration (optionally released), all on the element's center.</summary>
    public async Task LongPressAsync(object element, bool release = true)
    {
        var (root, center) = Locate(element);
        var id = ++s_nextPointer;
        var start = Now();
        Touch(root, id, SkUiTouchAction.Pressed, center, start);
        await WaitAsync(SkUiGestureSettings.LongPressDuration.TotalMilliseconds + 150);
        if (release)
            Touch(root, id, SkUiTouchAction.Released, center, start + SkUiGestureSettings.LongPressDuration + TimeSpan.FromMilliseconds(150));
        await SettleAsync();
    }

    /// <summary>
    /// Drags from the element's center by (<paramref name="dx"/>, <paramref name="dy"/>) over <paramref name="durationMs"/>;
    /// a short duration ends in a fling. With <paramref name="release"/> false the pointer stays down (e.g. a page
    /// closed mid-drag).
    /// </summary>
    public async Task DragAsync(object element, double dx, double dy, double durationMs = 240, int steps = 8, bool release = true)
    {
        var (root, center) = Locate(element);
        var id = ++s_nextPointer;
        var start = Now();
        Touch(root, id, SkUiTouchAction.Pressed, center, start);
        for (var step = 1; step <= steps; step++)
        {
            await WaitAsync(durationMs / steps);
            var t = (double)step / steps;
            Touch(root, id, SkUiTouchAction.Moved, new Point(center.X + dx * t, center.Y + dy * t), start + TimeSpan.FromMilliseconds(durationMs * t));
        }
        if (release)
            Touch(root, id, SkUiTouchAction.Released, new Point(center.X + dx, center.Y + dy), start + TimeSpan.FromMilliseconds(durationMs + 8));
        await SettleAsync();
    }

    /// <summary>Two pointers around the element's center moving apart by <paramref name="spread"/> DIPs each.</summary>
    public async Task PinchAsync(object element, double spread = 60, int steps = 6)
    {
        var (root, center) = Locate(element);
        var first = ++s_nextPointer;
        var second = ++s_nextPointer;
        var start = Now();
        Touch(root, first, SkUiTouchAction.Pressed, new Point(center.X - 20, center.Y), start);
        Touch(root, second, SkUiTouchAction.Pressed, new Point(center.X + 20, center.Y), start);
        for (var step = 1; step <= steps; step++)
        {
            await WaitAsync(16);
            var offset = 20 + spread * step / steps;
            var time = start + TimeSpan.FromMilliseconds(16 * step);
            Touch(root, first, SkUiTouchAction.Moved, new Point(center.X - offset, center.Y), time);
            Touch(root, second, SkUiTouchAction.Moved, new Point(center.X + offset, center.Y), time);
        }
        var end = start + TimeSpan.FromMilliseconds(16 * steps + 8);
        Touch(root, first, SkUiTouchAction.Released, new Point(center.X - 20 - spread, center.Y), end);
        Touch(root, second, SkUiTouchAction.Released, new Point(center.X + 20 + spread, center.Y), end);
        await SettleAsync();
    }

    private static (SkUiView Root, Point Center) Locate(object element)
    {
        if (SkUiDiagnostics.GetSurfaceRoot(element) is not { } root || SkUiDiagnostics.GetRootBounds(element) is not { } bounds)
            throw new InvalidOperationException($"{LeakTracker.Describe(element)} is not on a drawn surface.");
        return (root, bounds.Center);
    }

    private static void Touch(SkUiView root, long id, SkUiTouchAction action, Point position, TimeSpan time) =>
        root.Touch(new SkUiTouchEvent(id, action, position, time));

    private static TimeSpan Now() => TimeSpan.FromMilliseconds(Environment.TickCount64);
}
