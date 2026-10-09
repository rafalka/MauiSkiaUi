using System.Collections.ObjectModel;
using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiRefreshView"/> (C2) next to MAUI's <see cref="RefreshView"/>, on the same messages: pull the top down to
/// refresh (a new message arrives after a delay). The content is a scroll view, a header above a scroll view (the header
/// pulls too while the list is at its top), or a short stack without a scroller. SkiaUi-only editors: the indicator's
/// style (overlay badge or inline, iOS), the trigger distance, mouse pulls, and automatic completion (the drawn view ends
/// the refresh when a deferral taken in <c>Refreshing</c> completes; MAUI's is ended by the page).
/// </summary>
public sealed class RefreshViewDemoPage : ComponentDemoPage
{
    private const string ScrollContent = "Scroll view";
    private const string HeaderContent = "Header and scroll view";
    private const string ShortContent = "Short content (no scroller)";
    private const string HeaderText = "Inbox";

    private readonly SkUiRefreshView _skia;
    private readonly RefreshView _native;
    private readonly ObservableCollection<string> _messages = [];
    private readonly ObservableCollection<string> _few = [];
    private int _next;
    private int _skiaRefreshes;
    private int _nativeRefreshes;
    private double _delay = 1.5;

    public RefreshViewDemoPage() : base(nameof(SkUiRefreshView), new SkUiRefreshView(), new RefreshView(),
        widthRange: (160, 400, 300), heightRange: (160, 600, 360))
    {
        _skia = (SkUiRefreshView)SkiaControl;
        _native = (RefreshView)NativeControl!;
        for (var index = 0; index < 30; index++)
            _messages.Add(Message());
        for (var index = 0; index < 3; index++)
            _few.Add(Message());
        ShowContent(ScrollContent);

        _skia.Refreshing += (_, args) =>
        {
            _skiaRefreshes++;
            if (_skia.RefreshCompletion == SkUiRefreshCompletion.Automatic)
            {
                var deferral = args.GetDeferral();
                Fetch(deferral.Complete);
            }
            else
            {
                Fetch(() => _skia.IsRefreshing = false);
            }
        };
        _native.Refreshing += (_, _) => { _nativeRefreshes++; Fetch(() => _native.IsRefreshing = false); };
        Status();

        Choice("Content", [ScrollContent, HeaderContent, ShortContent], ScrollContent, ShowContent, () => ContentName(_skia.Content));
        Toggle(nameof(SkUiRefreshView.IsRefreshEnabled), true,
            value => { _skia.IsRefreshEnabled = value; _native.IsRefreshEnabled = value; },
            () => _skia.IsRefreshEnabled, () => _native.IsRefreshEnabled);
        ColorEditor(nameof(SkUiRefreshView.RefreshColor), Accent,
            value => { _skia.RefreshColor = value; _native.RefreshColor = value; },
            () => _skia.RefreshColor ?? Accent, () => _native.RefreshColor ?? Accent);
        Number("Refresh takes (s)", 0.5, 5, _delay, value => _delay = value, () => _delay);
        // SkiaUi only.
        Choice(nameof(SkUiRefreshView.RefreshStyle), Enum.GetValues<SkUiRefreshStyle>(), SkUiRefreshStyle.Default,
            value => _skia.RefreshStyle = value, () => _skia.RefreshStyle);
        Number(nameof(SkUiRefreshView.RefreshTriggerDistance), 0, 200, 0, value => _skia.RefreshTriggerDistance = value,
            () => _skia.RefreshTriggerDistance, whole: true);
        Toggle(nameof(SkUiRefreshView.IsMousePullEnabled), false, value => _skia.IsMousePullEnabled = value, () => _skia.IsMousePullEnabled);
        Choice(nameof(SkUiRefreshView.RefreshCompletion), Enum.GetValues<SkUiRefreshCompletion>(), SkUiRefreshCompletion.Manual,
            value => _skia.RefreshCompletion = value, () => _skia.RefreshCompletion);
        ActionButton("Refresh", () => { _skia.IsRefreshing = true; _native.IsRefreshing = true; });
        ActionButton("Stop", () => { _skia.IsRefreshing = false; _native.IsRefreshing = false; });
        OnReset(() =>
        {
            _skia.IsRefreshing = false;
            _native.IsRefreshing = false;
        });
    }

    private string Message() => $"Message {++_next}";

    private void ShowContent(string name)
    {
        _skia.Content = name switch
        {
            HeaderContent => SkiaWithHeader(SkiaScroller(_messages)),
            ShortContent => SkiaStack(_few),
            _ => SkiaScroller(_messages)
        };
        _native.Content = name switch
        {
            HeaderContent => NativeWithHeader(NativeScroller(_messages)),
            ShortContent => NativeStack(_few),
            _ => NativeScroller(_messages)
        };
    }

    private static string ContentName(ISkUiView? content) => content switch
    {
        SkUiGrid => HeaderContent,
        SkUiScrollView => ScrollContent,
        _ => ShortContent
    };

    /// <summary>A simulated fetch: a new message on top after the delay.</summary>
    private void Fetch(Action done) => Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(_delay), () =>
    {
        _messages.Insert(0, Message());
        _few.Insert(0, _messages[0]);
        _few.RemoveAt(_few.Count - 1);
        done();
        Status();
    });

    private void Status() =>
        Feedback($"refreshed {_skiaRefreshes} times · newest: {_messages[0]}", $"refreshed {_nativeRefreshes} times");

    private static SkUiScrollView SkiaScroller(ObservableCollection<string> messages) => new() { Content = SkiaStack(messages) };

    private static SkUiVerticalStackLayout SkiaStack(ObservableCollection<string> messages)
    {
        var stack = new SkUiVerticalStackLayout { Padding = new Thickness(12, 4) };
        BindableLayout.SetItemTemplate(stack, new DataTemplate(() =>
        {
            var row = new SkUiLabel { FontSize = 15, TextColor = Ink, Padding = new Thickness(4, 12) };
            row.SetBinding(SkUiLabel.TextProperty, Binding.SelfPath);
            return row;
        }));
        BindableLayout.SetItemsSource(stack, messages);
        return stack;
    }

    private static SkUiGrid SkiaWithHeader(SkUiScrollView scroller)
    {
        var grid = new SkUiGrid { RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)] };
        grid.Children.Add(new SkUiLabel
        {
            Text = HeaderText, FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Ink,
            Padding = new Thickness(16, 10), Background = DemoColors.SoftSurface
        });
        Grid.SetRow(scroller, 1);
        grid.Children.Add(scroller);
        return grid;
    }

    private static ScrollView NativeScroller(ObservableCollection<string> messages) => new() { Content = NativeStack(messages) };

    private static VerticalStackLayout NativeStack(ObservableCollection<string> messages)
    {
        var stack = new VerticalStackLayout { Padding = new Thickness(12, 4) };
        BindableLayout.SetItemTemplate(stack, new DataTemplate(() =>
        {
            var row = new Label { FontSize = 15, TextColor = Ink, Padding = new Thickness(4, 12) };
            row.SetBinding(Label.TextProperty, Binding.SelfPath);
            return row;
        }));
        BindableLayout.SetItemsSource(stack, messages);
        return stack;
    }

    private static Grid NativeWithHeader(ScrollView scroller)
    {
        var grid = new Grid { RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)] };
        grid.Children.Add(new Label
        {
            Text = HeaderText, FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Ink,
            Padding = new Thickness(16, 10), Background = DemoColors.SoftSurface
        });
        Grid.SetRow(scroller, 1);
        grid.Children.Add(scroller);
        return grid;
    }
}
