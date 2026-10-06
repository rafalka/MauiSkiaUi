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
/// is on the manual checklist in docs/design/Testing.md.
/// </summary>
internal static class HostedControlsCheck
{
    /// <summary>Platform rounding: Android places views on whole pixels (less than one DIP on any density).</summary>
    private const double Tolerance = 1.01;

    public static async Task<LeakResult> RunAsync()
    {
        var navigation = Shell.Current?.Navigation ?? Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;
        if (navigation is null)
            return new LeakResult("HostedControls", LeakStatus.Fail, "No navigation to host the check page.");
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var problems = new List<string>();
        var steps = 0;
        ContentPage? page = null;
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
                    if (Mismatch(host) is { } mismatch)
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

            // An animated scroll: snapshot mode (Android / Windows) hides the visible native views and draws bitmaps.
            var visibleBefore = form.Hosts.Where(item => !Visible(item.Host).IsEmpty && !item.Host.IsNativeHidden).ToList();
            var scrolled = form.Scroll.ScrollToAsync(0, 40, animated: true);
            await Task.Delay(60);
            if (form.Scroll.IsScrolling)
            {
                steps++;
                foreach (var (name, host) in visibleBefore)
                {
                    if (host.UsesSnapshotWhileScrolling && !host.IsShowingSnapshot)
                        problems.Add($"animated scroll: {name} shows no snapshot while scrolling");
                    if (!host.UsesSnapshotWhileScrolling && Mismatch(host) is { } live)
                        problems.Add($"animated scroll (live): {name} {live}");
                }
            }
            await scrolled;
            await Task.Delay((int)SkUiMauiContentView.SnapshotRestoreDelay.TotalMilliseconds + 250);
            foreach (var (name, host) in form.Hosts)
                if (host.IsShowingSnapshot)
                    problems.Add($"after the animated scroll: {name} still shows its snapshot");
            Check("after the animated scroll");

            // A focused text field stays live while its scroller moves (no typing into a hidden field).
            form.Entry.Focus();
            await Task.Delay(500);
            if (!form.Entry.IsFocused)
                problems.Add("focus: the hosted Entry did not take focus");
            var moved = form.Scroll.ScrollToAsync(0, 0, animated: true);
            await Task.Delay(60);
            if (form.Entry.IsFocused && form.EntryHost.IsShowingSnapshot)
                problems.Add("focus: the focused Entry shows a snapshot while scrolling");
            await moved;
            await Task.Delay(300);
            Check("focused Entry scrolled");
            form.Entry.Unfocus();
            if (MemoryLeakRunner.FocusSink is { } sink)
            {
                // Hand text focus to the test page's own field once this page closes (see LeakScenarioContext.FocusAsync).
                // The same tree on a new page also checks that overlays re-attach when their surface reconnects.
                var closed = page;
                page = null;
                await navigation.PopAsync(animated: false);
                closed.Content = null;
                sink.Focus();
                await Task.Delay(300);
                sink.Unfocus();
                page = new ContentPage { Title = "Hosted controls", BackgroundColor = Colors.White, Content = form.Root };
                await navigation.PushAsync(page, animated: false);
                await Task.Delay(600);
                Check("page shown again");
            }

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
            if (page is not null)
                await navigation.PopAsync(animated: false);
        }
        var platform = OperatingSystem.IsAndroid() || OperatingSystem.IsWindows() ? "snapshots while scrolling" : "live while scrolling";
        return new LeakResult("HostedControls", problems.Count == 0 ? LeakStatus.Pass : LeakStatus.Fail,
            problems.Count == 0 ? $"{steps} steps: native frames, clips and visibility match the drawn tree ({platform})" : string.Join(" · ", problems.Take(8)),
            watch.Elapsed.TotalSeconds);
    }

    /// <summary>Where the platform shows the native view, against where the drawn tree places it; null when they agree.</summary>
    private static string? Mismatch(SkUiMauiContentView host)
    {
        if (host.GetNativePlacement() is not { } placement)
            return "is not attached to the surface";
        var frame = host.ComputeRootRelativeFrame();
        var visible = Visible(host);
        var shown = !visible.IsEmpty && !host.IsNativeHidden;
        if (placement.IsShown != shown)
            return $"is {(placement.IsShown ? "shown" : "hidden")}, expected {(shown ? "shown" : "hidden")} (visible {Format(visible)}, snapshot {host.IsShowingSnapshot})";
        if (visible.IsEmpty)
            return null;
        if (!Near(placement.Frame, frame))
            return $"frame {Format(placement.Frame)}, expected {Format(frame)}";
        if (!Near(placement.Visible, visible))
            return $"visible {Format(placement.Visible)}, expected {Format(visible)}";
        return null;
    }

    private static Rect Visible(SkUiMauiContentView host)
    {
        var visible = host.ComputeRootRelativeFrame().Intersect(host.ComputeRootRelativeClip());
        return visible.Width <= 0 || visible.Height <= 0 ? Rect.Zero : visible;
    }

    private static bool Near(Rect actual, Rect expected) =>
        Math.Abs(actual.X - expected.X) <= Tolerance && Math.Abs(actual.Y - expected.Y) <= Tolerance
        && Math.Abs(actual.Right - expected.Right) <= Tolerance && Math.Abs(actual.Bottom - expected.Bottom) <= Tolerance;

    private static string Format(Rect rect) => $"({rect.X:F1}, {rect.Y:F1}, {rect.Width:F1} × {rect.Height:F1})";

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
            Scroll = new SkUiScrollView { Content = stack };
            Root = new SkUiContentView { Background = Colors.White, Content = Scroll };
            Hosts = [("Entry", EntryHost), ("Editor", editor), ("inner Entry", inInner), ("expander Entry", InExpander), ("WebView", web), ("replaced Entry", Replaced)];
        }

        public SkUiContentView Root { get; }
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
