using System.Diagnostics;
using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiVirtualScrollView"/> next to MAUI's <see cref="CollectionView"/> with the same items: only the items near
/// the viewport exist, views are recycled, and items of different heights are measured as they come into view.
/// </summary>
public sealed class VirtualScrollViewDemoPage : ComponentDemoPage
{
    private readonly SkUiVirtualScrollView _skia;
    private readonly CollectionView _native;
    private int _created;
    private string _lastScroll = "";
    private bool _autoRunStarted;
    private int _count = 10_000;
    private bool _variable = true;

    public VirtualScrollViewDemoPage() : base(nameof(SkUiVirtualScrollView), new SkUiVirtualScrollView(), new CollectionView(),
        widthRange: (160, 400, 300), heightRange: (160, 600, 320))
    {
        _skia = (SkUiVirtualScrollView)SkiaControl;
        _native = (CollectionView)NativeControl!;
        _skia.ItemTemplate = new DataTemplate(() => { _created++; return new SkiaRow(); });
        _native.ItemTemplate = new DataTemplate(() => new NativeRow());
        _native.ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical);
        _skia.VisibleRangeChanged += (_, _) => Status();
        _skia.Scrolled += (_, _) => Status();

        Number("ItemCount", 0, 100_000, _count, value => { _count = (int)value; Rebuild(); }, () => _skia.Items.ItemCount, () => ((IList<DemoRow>)_native.ItemsSource).Count, whole: true);
        Toggle("Variable heights", _variable, value => { _variable = value; Rebuild(); }, () => _variable);
        // SkiaUi only: every row 56 DIPs, positions known without measuring.
        Toggle(nameof(SkUiVirtualScrollView.ItemExtent) + " = 56", false, value => _skia.ItemExtent = value ? 56 : 0, () => _skia.ItemExtent > 0);
        Number(nameof(SkUiVirtualScrollView.Spacing), 0, 16, 0,
            value => { _skia.Spacing = value; ((LinearItemsLayout)_native.ItemsLayout).ItemSpacing = value; },
            () => _skia.Spacing, () => ((LinearItemsLayout)_native.ItemsLayout).ItemSpacing);
        Number(nameof(SkUiVirtualScrollView.PrefetchFactor), 0, 3, 1, value => _skia.PrefetchFactor = value, () => _skia.PrefetchFactor);
        Number(nameof(SkUiVirtualScrollView.ReleaseFactor), 0, 6, 2, value => _skia.ReleaseFactor = value, () => _skia.ReleaseFactor);
        var position = ScrollToPosition.Start;
        Choice(nameof(ScrollToPosition), Enum.GetValues<ScrollToPosition>(), ScrollToPosition.Start, value => position = value, () => position);
        ActionButton("Scroll to the middle item", () => ScrollBoth(_count / 2, position));
        ActionButton("Scroll to start", () => ScrollBoth(0, ScrollToPosition.Start));
        OnReset(() => _skia.ScrollTo(0, 0));
    }

    private void Rebuild()
    {
        var rows = new DemoRow[_count];
        for (var index = 0; index < rows.Length; index++)
            rows[index] = new DemoRow(index, _variable ? 44 + index % 4 * 18 : 56, (index % 3) switch { 0 => Accent, 1 => DemoColors.SampleA, _ => DemoColors.SampleB });
        _skia.ItemsSource = rows;
        _native.ItemsSource = rows;
    }

    private async void ScrollBoth(int index, ScrollToPosition position)
    {
        if (index >= _count)
            return;
        try
        {
            await ScrollSkia(index, position);
            if (_native.Handler is not null)
                _native.ScrollTo(index, position: position, animate: true);
        }
        catch (OperationCanceledException)
        {
            // A drag or another scroll took over.
        }
    }

    /// <summary>Scrolls the drawn list (animated) and shows how long it took until it landed, with the items realized on the way.</summary>
    private async Task<double> ScrollSkia(int index, ScrollToPosition position)
    {
        var created = _created;
        var realized = 0;
        void Count(object? sender, SkUiVirtualItemEventArgs args) => realized++;
        _skia.ItemRealized += Count;
        var watch = Stopwatch.StartNew();
        try
        {
            await _skia.ScrollToIndex(index, position, animated: true);
        }
        finally
        {
            _skia.ItemRealized -= Count;
        }
        var ms = watch.Elapsed.TotalMilliseconds;
        _lastScroll = $"to {index}: {ms:F0} ms, {realized} realized, {_created - created} created";
        Console.WriteLine($"[VirtualScroll] scroll {_lastScroll}; first visible {_skia.FirstVisibleIndex}");
        Status();
        return ms;
    }

    /// <summary>Scripted runs (<c>SKUI_VIRTUAL_SCROLL=run</c>): scrolls to the middle and back a few times and logs the times.</summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_autoRunStarted || Environment.GetEnvironmentVariable("SKUI_VIRTUAL_SCROLL") != "run")
            return;
        _autoRunStarted = true;
        Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(2), async () =>
        {
            var times = new List<double>();
            for (var round = 0; round < 5; round++)
            {
                times.Add(await ScrollSkia(_count / 2, ScrollToPosition.Start));
                await Task.Delay(500);
                times.Add(await ScrollSkia(0, ScrollToPosition.Start));
                await Task.Delay(500);
            }
            Console.WriteLine($"[VirtualScroll] done: {_count} items, median {times.Order().ElementAt(times.Count / 2):F0} ms, max {times.Max():F0} ms");
        });
    }

    private void Status()
    {
        var (first, last) = _skia.Items.RealizedRange;
        Feedback($"Visible {_skia.FirstVisibleIndex}–{_skia.LastVisibleIndex} · realized {first}–{last} · {_created} views created"
            + (_lastScroll.Length > 0 ? $" · last scroll {_lastScroll}" : ""));
    }

    internal sealed record DemoRow(int Index, double Height, Color Color);

    /// <summary>A recycled row: it takes its item from the binding context (no bindings to evaluate).</summary>
    private sealed class SkiaRow : SkUiHorizontalStackLayout
    {
        private readonly SkUiBox _swatch = new() { WidthRequest = 6 };
        private readonly SkUiLabel _text = new() { FontSize = 14, TextColor = Ink, VerticalTextAlignment = TextAlignment.Center, Margin = new Thickness(12, 0) };

        public SkiaRow()
        {
            Children.Add(_swatch);
            Children.Add(_text);
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is not DemoRow row)
                return;
            HeightRequest = row.Height;
            _swatch.Color = row.Color;
            _text.Text = $"Item {row.Index} · {row.Height:F0} DIPs";
        }
    }

    private sealed class NativeRow : ContentView
    {
        private readonly BoxView _swatch = new() { WidthRequest = 6 };
        private readonly Label _text = new() { FontSize = 14, TextColor = Ink, VerticalTextAlignment = TextAlignment.Center, Margin = new Thickness(12, 0) };

        public NativeRow() => Content = new HorizontalStackLayout { Children = { _swatch, _text } };

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is not DemoRow row)
                return;
            HeightRequest = row.Height;
            _swatch.Color = row.Color;
            _text.Text = $"Item {row.Index} · {row.Height:F0} DIPs";
        }
    }
}
