using MauiSkiaUi.LeakTests;

namespace MauiSkiaUi.DeviceTests;

/// <summary>
/// Hosted-control regression check (A6) with real handlers and platform views: Entry, Editor and WebView hosted in a
/// drawn scroller, in a nested horizontal scroller and in an expander. After each step, every native view is read back
/// from the platform (<see cref="SkUiMauiContentView.GetNativePlacement"/>) and compared with where the drawn tree
/// says it is: its frame, its visible part (scroll viewports) and whether it shows (snapshots, collapsed content).
/// Steps: first layout, instant scrolls of both scrollers, an animated scroll (snapshots on Android / Windows, live on
/// Apple) and the restore after it, a focused Entry staying live while scrolling, expanding and collapsing, and
/// replacing a hosted control. What it cannot see (the soft keyboard, IME composition, touch nesting in a WebView)
/// is on the manual checklist in docs/design/Testing.md. The comparison is
/// <see cref="SkUiMauiContentView.FindNativePlacementMismatch"/> (shared with the overlay demo page); its tolerance can
/// be overridden (<c>--placement-tolerance</c>) to prove the check fails on real mismatches.
/// </summary>
internal static class HostedControlsCheck
{
    public static async Task<LeakResult> RunAsync()
    {
        var navigation = Shell.Current?.Navigation ?? Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;
        if (navigation is null)
            return new LeakResult("HostedControls", LeakStatus.Fail, "No navigation to host the check page.");
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var problems = new List<string>();
        var steps = 0;
        ContentPage? page = null;
        var tolerance = SkUiMauiContentView.NativePlacementTolerance;
        if (DeviceTestOptions.Current.PlacementTolerance is { } overridden)
            SkUiMauiContentView.NativePlacementTolerance = overridden;
        try
        {
            var form = new Form();
            page = new ContentPage { Title = "Hosted controls", BackgroundColor = Colors.White, Content = form.Root };
            await navigation.PushAsync(page, animated: false);
            await Task.Delay(1200); // first layout, handlers, the WebView's first load

            void Check(string step)
            {
                steps++;
                foreach (var (name, host) in form.Hosts)
                    if (host.FindNativePlacementMismatch() is { } mismatch)
                        problems.Add($"{step}: {name} {mismatch}");
            }

            Check("first layout");
            if (!form.InExpander.IsNativeHidden)
                problems.Add("first layout: the collapsed expander's Entry is not hidden");

            await form.Scroll.ScrollToAsync(0, 260, animated: false);
            await Task.Delay(150);
            Check("instant scroll");
            await form.Inner.ScrollToAsync(200, 0, animated: false);
            await Task.Delay(150);
            Check("inner scroll");

            // An animated scroll to the end: snapshot mode (Android / Windows) hides the visible native views and
            // draws bitmaps; live mode (Apple) keeps them placed. The motion must be seen, else nothing was checked.
            var visibleBefore = form.Hosts.Where(item => Visible(item.Host) && !item.Host.IsNativeHidden).ToList();
            var motion = await WatchMotionAsync(form.Scroll.ScrollToAsync(0, form.MaxScrollY, animated: true), form.Scroll, () =>
            {
                var pending = false;
                foreach (var (name, host) in visibleBefore)
                {
                    if (!host.UsesSnapshotWhileScrolling)
                    {
                        if (host.FindNativePlacementMismatch() is { } live)
                            problems.Add($"animated scroll (live): {name} {live}");
                    }
                    else if (!host.IsShowingSnapshot)
                    {
                        pending = true; // Windows captures asynchronously
                    }
                }
                return pending;
            });
            if (!motion.Seen)
                problems.Add("animated scroll: no motion observed, nothing was checked");
            else if (motion.Pending)
                problems.Add($"animated scroll: {string.Join(", ", visibleBefore.Where(item => item.Host.UsesSnapshotWhileScrolling && !item.Host.IsShowingSnapshot).Select(item => item.Name))} showed no snapshot while scrolling");
            steps++;
            await Task.Delay((int)SkUiMauiContentView.SnapshotRestoreDelay.TotalMilliseconds + 250);
            foreach (var (name, host) in form.Hosts)
                if (host.IsShowingSnapshot)
                    problems.Add($"after the animated scroll: {name} still shows its snapshot");
            Check("after the animated scroll");

            // A focused text field keeps its focus and stays live while its scroller moves (no typing into a hidden field).
            await form.Scroll.ScrollToAsync(0, 0, animated: false);
            await Task.Delay(150);
            form.Entry.Focus();
            await Task.Delay(500);
            if (!form.Entry.IsFocused)
            {
                problems.Add("focus: the hosted Entry did not take focus");
            }
            else
            {
                var lostFocus = false;
                var focusMotion = await WatchMotionAsync(form.Scroll.ScrollToAsync(0, 160, animated: true), form.Scroll, () =>
                {
                    if (!form.Entry.IsFocused)
                        lostFocus = true;
                    else if (form.EntryHost.IsShowingSnapshot)
                        problems.Add("focus: the focused Entry shows a snapshot while scrolling");
                    return true; // watch the whole motion
                });
                if (!focusMotion.Seen)
                    problems.Add("focus: no motion observed, nothing was checked");
                if (lostFocus || !form.Entry.IsFocused)
                    problems.Add("focus: the Entry lost focus while its scroller moved");
                await form.Scroll.ScrollToAsync(0, 0, animated: false);
                await Task.Delay(300);
                Check("focused Entry scrolled");
            }
            form.Entry.Unfocus();

            // The same tree on a new page: overlays detach with the closed page's surface and attach to the new one.
            var closed = page;
            page = null;
            await navigation.PopAsync(animated: false);
            closed.Content = null;
            if (MemoryLeakRunner.FocusSink is { } sink)
            {
                // Hand text focus to the test page's own field (see LeakScenarioContext.FocusAsync).
                sink.Focus();
                await Task.Delay(300);
                sink.Unfocus();
            }
            foreach (var (name, host) in form.Hosts)
                if (host.GetNativePlacement() is not null)
                    problems.Add($"page closed: {name} is still attached");
            page = new ContentPage { Title = "Hosted controls", BackgroundColor = Colors.White, Content = form.Root };
            await navigation.PushAsync(page, animated: false);
            await Task.Delay(600);
            Check("page shown again");

            form.Expander.IsExpanded = true;
            await Task.Delay((int)form.Expander.AnimationLength + 400);
            if (form.InExpander.IsNativeHidden)
                problems.Add("expanded: the expander's Entry is still hidden");
            Check("expanded");
            form.Expander.IsExpanded = false;
            await Task.Delay((int)form.Expander.AnimationLength + 400);
            if (!form.InExpander.IsNativeHidden || form.InExpander.GetNativePlacement() is { IsShown: true })
                problems.Add("collapsed: the expander's Entry still shows");

            form.Replaced.Content = new Entry { Placeholder = "Replacement" };
            await Task.Delay(300);
            Check("content replaced");
        }
        catch (Exception exception)
        {
            problems.Add($"{exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            SkUiMauiContentView.NativePlacementTolerance = tolerance;
            if (page is not null)
                await navigation.PopAsync(animated: false);
        }
        var platform = OperatingSystem.IsAndroid() || OperatingSystem.IsWindows() ? "snapshots while scrolling" : "live while scrolling";
        return new LeakResult("HostedControls", problems.Count == 0 ? LeakStatus.Pass : LeakStatus.Fail,
            problems.Count == 0 ? $"{steps} steps: native frames, clips and visibility match the drawn tree ({platform})" : string.Join(" · ", problems.Distinct().Take(8)),
            watch.Elapsed.TotalSeconds);
    }

    /// <summary>
    /// Runs <paramref name="check"/> at once (the scroll starts synchronously) and on every frame while
    /// <paramref name="scroll"/> moves, until it returns false or the motion ends; then awaits the scroll.
    /// <c>Seen</c>: the scroller was seen moving; <c>Pending</c>: the last check still waited for something.
    /// </summary>
    private static async Task<(bool Seen, bool Pending)> WatchMotionAsync(Task scrolled, SkUiScrollView scroll, Func<bool> check)
    {
        var seen = false;
        var pending = false;
        for (var waited = 0; scroll.IsScrolling && waited < 2000; waited += 16)
        {
            seen = true;
            pending = check();
            if (!pending)
                break;
            await Task.Delay(16);
        }
        await scrolled;
        return (seen, pending);
    }

    private static bool Visible(SkUiMauiContentView host)
    {
        var visible = host.ComputeRootRelativeFrame().Intersect(host.ComputeRootRelativeClip());
        return visible.Width > 0 && visible.Height > 0;
    }

    /// <summary>
    /// The check page: a drawn scroller with an Entry, an Editor, a horizontal scroller holding an Entry, a collapsed
    /// expander holding an Entry, a WebView and an Entry that gets replaced, with drawn rows between them.
    /// </summary>
    private sealed class Form
    {
        public Form()
        {
            Entry = new Entry { Placeholder = "Hosted Entry" };
            EntryHost = Host(Entry, 44);
            var editor = Host(new Editor { Text = "Hosted Editor\nsecond line\nthird line" }, 110);
            var inInner = Host(new Entry { Placeholder = "Entry in a horizontal scroller" }, 44);
            inInner.WidthRequest = 160;
            Inner = new SkUiScrollView
            {
                Orientation = ScrollOrientation.Horizontal, HeightRequest = 56,
                Content = new SkUiHorizontalStackLayout { Spacing = 8, Children = { Row(250, 44), inInner, Row(500, 44) } }
            };
            InExpander = Host(new Entry { Placeholder = "Entry in an expander" }, 44);
            Expander = new SkUiExpander
            {
                Header = new SkUiLabel { Text = "Expander", FontSize = 16, TextColor = LeakColors.Ink, HeightRequest = 40, Padding = new Thickness(8) },
                Content = InExpander
            };
            var web = Host(new WebView { Source = new HtmlWebViewSource { Html = "<html><body style='font:20px sans-serif;background:#cde'><h2>Hosted WebView</h2><p>Line</p><p>Line</p><p>Line</p></body></html>" } }, 180);
            Replaced = Host(new Entry { Placeholder = "Entry to replace" }, 44);
            var stack = new SkUiVerticalStackLayout
            {
                Spacing = 10, Padding = new Thickness(12),
                Children = { EntryHost, Row(0, 120), editor, Row(0, 80), Inner, Row(0, 80), Expander, Row(0, 80), web, Row(0, 80), Replaced, Row(0, 900) }
            };
            _column = stack;
            Scroll = new SkUiScrollView { Content = stack };
            Root = new SkUiContentView { Background = Colors.White, Content = Scroll };
            Hosts = [("Entry", EntryHost), ("Editor", editor), ("inner Entry", inInner), ("expander Entry", InExpander), ("WebView", web), ("replaced Entry", Replaced)];
        }

        private readonly SkUiVerticalStackLayout _column;

        public SkUiContentView Root { get; }

        /// <summary>The scroll offset of the column's end.</summary>
        public double MaxScrollY => Math.Max(0, _column.Height - Scroll.Height);
        public SkUiScrollView Scroll { get; }
        public SkUiScrollView Inner { get; }
        public SkUiExpander Expander { get; }
        public Entry Entry { get; }
        public SkUiMauiContentView EntryHost { get; }
        public SkUiMauiContentView InExpander { get; }
        public SkUiMauiContentView Replaced { get; }
        public List<(string Name, SkUiMauiContentView Host)> Hosts { get; }

        private static SkUiMauiContentView Host(View content, double height) => new() { Content = content, HeightRequest = height };

        private static SkUiBox Row(double width, double height)
        {
            var box = new SkUiBox { Color = LeakColors.Surface, HeightRequest = height };
            if (width > 0)
                box.WidthRequest = width;
            return box;
        }
    }
}
