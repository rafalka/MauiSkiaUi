using MauiSkiaUi;
using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUiDemo;

/// <summary>
/// Measures how far a view moved from a scroller's offset lags the content (1.16): a header over a 10,000-row list moves up
/// at half the scroll speed, either from the list's <c>Scrolled</c> event (setting <c>TranslationY</c> on the UI thread) or
/// from a scroll effect (evaluated on the render thread). A probe drawn with the header reads, on the render thread in each
/// frame, where the header is drawn and the offset the list is drawn at in that same frame; the difference from where the
/// offset puts the header is the lag (DIPs, and frames of scrolling). Each mode flings the list and samples 1.5 s; the busy
/// modes add 8 ms of UI-thread work per frame, as a busy app has.
/// </summary>
/// <remarks>
/// Scripted runs: <c>SKUI_SCROLL_LAG=run</c> runs every mode once the page shows (Android: <c>-e SKUI_DEMO_ROUTE scroll-lag
/// -e SKUI_SCROLL_LAG run</c>); results go to the console as <c>[ScrollLag]</c> lines, ending with <c>[ScrollLag] done</c>.
/// </remarks>
public sealed class ScrollLagPage : ContentPage
{
    private const float Rate = 0.5f;
    private const double FlingVelocity = 6000;
    private static readonly TimeSpan SampleWindow = TimeSpan.FromSeconds(1.5);

    private readonly SkUiGrid _root = new();
    private readonly SkUiCollectionView _list;
    private readonly ProbeBox _header;
    private readonly SkUiLabel _status = new() { Margin = new Thickness(12), FontSize = 13, TextColor = Colors.Black, VerticalOptions = LayoutOptions.End, BackgroundColor = Colors.White };
    private readonly SkUiScrollEffect _effect = SkUiScrollEffect.Parallax(Rate);
    private bool _eventDriven;
    private bool _started;

    public ScrollLagPage()
    {
        Title = "Scroll lag";
        _list = new SkUiCollectionView
        {
            Background = Colors.White,
            ViewportInsets = new Thickness(0, 120, 0, 0),
            ItemsSource = Enumerable.Range(0, 10_000).ToList(),
            ItemTemplate = new DataTemplate(() =>
            {
                var label = new SkUiLabel { Padding = new Thickness(16, 12), FontSize = 15, TextColor = Colors.Black };
                label.SetBinding(SkUiLabel.TextProperty, Binding.SelfPath, stringFormat: "Row {0:0000}");
                return label;
            })
        };
        // Tall (and see-through below its first 120 DIPs) so it stays on screen, and drawn, while it moves up 1,500 DIPs: the
        // compositor culls a view that left the screen, and its probe with it.
        _header = new ProbeBox(this)
        {
            Background = new LinearGradientBrush([new(Color.FromArgb("#0F766E"), 0), new(Color.FromArgb("#0F766E"), 0.02f), new(Colors.Transparent, 0.02f)], new Point(0, 0), new Point(0, 1)),
            HeightRequest = 6000, VerticalOptions = LayoutOptions.Start, InputTransparent = true
        };
        _list.Scrolled += (_, _) =>
        {
            if (_eventDriven)
                _header.TranslationY = -Rate * _list.ScrollY;
        };
        _effect.Source = _list;
        _root.Children.Add(_list);
        _root.Children.Add(_header);
        _root.Children.Add(_status);
        var run = new Button { Text = "Run", HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Start, Margin = new Thickness(12) };
        run.Clicked += async (_, _) => await RunAllAsync();
        Content = new Grid { Children = { _root, run } };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_started || Environment.GetEnvironmentVariable("SKUI_SCROLL_LAG") != "run")
            return;
        _started = true;
        Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(2), async () => await RunAllAsync());
    }

    private SkUiScrollController Scroller => ((ISkUiScrollEffectSource)_list).ScrollEffectHost!.Scroller;

    private SkUiRenderNode ScrollerNode => ((ISkUiScrollEffectSource)_list).ScrollEffectHost!.RenderState.Node;

    private async Task RunAllAsync()
    {
        string[] modes =
        [
            "fling event", "fling effect", "fling event-busy", "fling effect-busy",
            "scroll event", "scroll effect", "scroll event-busy", "scroll effect-busy",
            "drag event", "drag effect", "drag event-busy", "drag effect-busy"
        ];
        foreach (var mode in modes)
            await RunAsync(mode);
        Console.WriteLine("[ScrollLag] done");
        _status.Text = "done";
    }

    private async Task RunAsync(string mode)
    {
        _eventDriven = mode.Contains("event", StringComparison.Ordinal);
        if (_eventDriven)
            _header.RemoveScrollEffect(_effect);
        else if (!_header.ScrollEffects.Contains(_effect))
            _header.AddScrollEffect(_effect);
        Scroller.SetOffset(0, 0);
        _header.TranslationY = 0;
        await Task.Delay(600);
        var busy = mode.EndsWith("-busy", StringComparison.Ordinal);
        using var load = busy ? new UiLoad(Dispatcher) : null;
        _header.Probe.Start();
        IDispatcherTimer? drag = null;
        if (mode.StartsWith("fling", StringComparison.Ordinal))
        {
            // A fling: render-thread motion, offsets reported back to the UI thread.
            Scroller.StartFling(new Point(0, FlingVelocity));
        }
        else if (mode.StartsWith("scroll", StringComparison.Ordinal))
        {
            // An animated scroll (render-thread tween) over the window.
            _ = Scroller.AnimateTo(0, 3000, TimeSpan.FromSeconds(1.2));
        }
        else
        {
            // A drag: the UI thread sets the offset (20 DIPs a frame), as a finger moving does.
            drag = Dispatcher.CreateTimer();
            drag.Interval = TimeSpan.FromMilliseconds(16);
            drag.Tick += (_, _) => Scroller.SetOffset(0, Scroller.Y + 20);
            drag.Start();
        }
        await Task.Delay(SampleWindow);
        drag?.Stop();
        var samples = _header.Probe.Stop();
        var line = Summarize(mode, samples);
        Console.WriteLine("[ScrollLag] " + line);
        _status.Text = line;
        Scroller.StopMotion();
    }

    /// <summary>Mean, 95th percentile and largest lag over the frames that scrolled, in DIPs and in frames of scrolling.</summary>
    private static string Summarize(string mode, List<(float Error, float Step)> samples)
    {
        var moving = samples.Where(sample => Math.Abs(sample.Step) > 0.5f).ToList();
        if (moving.Count == 0)
            return $"{mode} frames=0";
        var errors = moving.Select(sample => Math.Abs(sample.Error)).OrderBy(error => error).ToList();
        // How many frames of scrolling the header is behind: its error over what the header moves per frame.
        var frames = moving.Select(sample => Math.Abs(sample.Error) / (Rate * Math.Abs(sample.Step))).OrderBy(value => value).ToList();
        static float P95(List<float> sorted) => sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * 0.95))];
        return FormattableString.Invariant(
            $"{mode} frames={moving.Count} lagDip mean={errors.Average():F1} p95={P95(errors):F1} max={errors[^1]:F1} lagFrames mean={frames.Average():F2} p95={P95(frames):F2} max={frames[^1]:F2} stepDip mean={moving.Average(sample => Math.Abs(sample.Step)):F1}");
    }

    /// <summary>The header, with a probe drawn with it on the render thread.</summary>
    private sealed class ProbeBox(ScrollLagPage page) : SkUiBox
    {
        public LagProbe Probe { get; } = new(page);

        internal override void OnGetRenderProps(ref SkUiRenderProps props)
        {
            base.OnGetRenderProps(ref props);
            props.Drawable = Probe;
        }
    }

    /// <summary>
    /// Records, each time the header is drawn, the header's drawn translation minus the one the scroller's offset drawn in the
    /// same frame gives (render thread), and how far that offset moved since the last frame.
    /// </summary>
    private sealed class LagProbe(ScrollLagPage page) : SkUiRenderDrawable
    {
        private readonly (float Error, float Step)[] _samples = new (float, float)[2048];
        private TimeSpan _first, _last;
        private volatile bool _recording;
        private int _count;
        private float _lastOffset = float.NaN;
        private SkUiRenderNode? _scroller;

        public override bool IsAnimated => false;

        public void Start()
        {
            _scroller = page.ScrollerNode;
            _count = 0;
            _lastOffset = float.NaN;
            _recording = true;
        }

        public List<(float Error, float Step)> Stop()
        {
            _recording = false;
            Thread.MemoryBarrier();
            Console.WriteLine(FormattableString.Invariant($"[ScrollLag] probe samples={_count} span={(_last - _first).TotalMilliseconds:F0}ms"));
            return _samples.Take(Math.Min(_count, _samples.Length)).ToList();
        }

        public override void Draw(SKCanvas canvas, in SkUiRenderDrawContext context)
        {
            if (!_recording || _scroller is not { } scroller || context.Now == TimeSpan.Zero)
                return;
            var offset = scroller.Props.ChildrenOffsetY;
            var scale = context.Surface.ScaleY;
            // The header's top in surface DIPs (it is laid out at the top of the surface root): its drawn translation.
            var drawn = (canvas.TotalMatrix.TransY - context.Surface.TransY) / scale;
            var expected = -Rate * offset;
            var step = float.IsNaN(_lastOffset) ? 0 : offset - _lastOffset;
            _lastOffset = offset;
            if (_count == 0)
                _first = context.Now;
            _last = context.Now;
            var index = _count++;
            if (index < _samples.Length)
                _samples[index] = (drawn - expected, step);
        }
    }

    /// <summary>8 ms of UI-thread work every 16 ms, as a busy app has (binding, layout, logic).</summary>
    private sealed class UiLoad : IDisposable
    {
        private readonly IDispatcherTimer _timer;

        public UiLoad(IDispatcher dispatcher)
        {
            _timer = dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(16);
            _timer.Tick += (_, _) =>
            {
                var until = Environment.TickCount64 + 8;
                while (Environment.TickCount64 < until) { }
            };
            _timer.Start();
        }

        public void Dispose() => _timer.Stop();
    }
}
