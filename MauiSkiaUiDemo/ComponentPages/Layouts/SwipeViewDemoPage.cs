using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiSwipeView"/> (C1) next to MAUI's <see cref="SwipeView"/>, with the same <see cref="SwipeItems"/> on both:
/// swipe the card left or right (or down, with top items on). Editors switch the right items between reveal and execute,
/// what an invoked item does, and the threshold; the status lines report the swipe events and invoked items.
/// </summary>
public sealed class SwipeViewDemoPage : ComponentDemoPage
{
    private const string Favorite = "Favorite";
    private const string Archive = "Archive";
    private const string Delete = "Delete";
    private const string Check = "Check";
    private const string OpenLeft = "Open left";
    private const string OpenRight = "Open right";
    private const string CloseItems = "Close";

    private readonly SkUiSwipeView _skia;
    private readonly SwipeView _native;
    private string _skiaLast = "";
    private string _nativeLast = "";
    private SwipeMode _rightMode;
    private SwipeBehaviorOnInvoked _behavior;

    public SwipeViewDemoPage()
        : base(nameof(SkUiSwipeView), new SkUiSwipeView(), new SwipeView(), widthRange: (200, 400, 300), heightRange: (48, 160, 72))
    {
        _skia = (SkUiSwipeView)SkiaControl;
        _native = (SwipeView)NativeControl!;
        _skia.Content = SkiaCard();
        _native.Content = NativeCard();
        _skia.SwipeStarted += (_, args) => SkiaStatus($"started {args.SwipeDirection}");
        _skia.SwipeEnded += (_, args) => SkiaStatus($"ended {args.SwipeDirection}, open {args.IsOpen}");
        _native.SwipeStarted += (_, args) => NativeStatus($"started {args.SwipeDirection}");
        _native.SwipeEnded += (_, args) => NativeStatus($"ended {args.SwipeDirection}, open {args.IsOpen}");

        Toggle(nameof(SkUiSwipeView.LeftItems), true,
            value => { _skia.LeftItems = value ? LeftItems(SkiaStatus) : []; _native.LeftItems = value ? LeftItems(NativeStatus) : []; ApplyItemOptions(); },
            () => _skia.LeftItems.Count > 0, () => _native.LeftItems.Count > 0);
        Toggle(nameof(SkUiSwipeView.RightItems), true,
            value => { _skia.RightItems = value ? RightItems(SkiaStatus) : []; _native.RightItems = value ? RightItems(NativeStatus) : []; ApplyItemOptions(); },
            () => _skia.RightItems.Count > 0, () => _native.RightItems.Count > 0);
        Toggle(nameof(SkUiSwipeView.TopItems), false,
            value => { _skia.TopItems = value ? SkiaTopItems() : []; _native.TopItems = value ? NativeTopItems() : []; ApplyItemOptions(); },
            () => _skia.TopItems.Count > 0, () => _native.TopItems.Count > 0);
        Choice("RightItems." + nameof(SwipeItems.Mode), [SwipeMode.Reveal, SwipeMode.Execute], SwipeMode.Reveal,
            value => { _rightMode = value; ApplyItemOptions(); },
            () => _skia.RightItems.Mode, () => _native.RightItems.Mode);
        Choice(nameof(SwipeItems.SwipeBehaviorOnInvoked), Enum.GetValues<SwipeBehaviorOnInvoked>(), SwipeBehaviorOnInvoked.Auto,
            value => { _behavior = value; ApplyItemOptions(); },
            () => _skia.RightItems.SwipeBehaviorOnInvoked, () => _native.RightItems.SwipeBehaviorOnInvoked);
        Number(nameof(SkUiSwipeView.Threshold), 0, 300, 0, value => { _skia.Threshold = value; _native.Threshold = value; },
            () => _skia.Threshold, () => _native.Threshold, whole: true);
        ActionButton(OpenLeft, () => { _skia.Open(OpenSwipeItem.LeftItems); _native.Open(OpenSwipeItem.LeftItems); });
        ActionButton(OpenRight, () => { _skia.Open(OpenSwipeItem.RightItems); _native.Open(OpenSwipeItem.RightItems); });
        ActionButton(CloseItems, () => { _skia.Close(); _native.Close(); });
        OnReset(() =>
        {
            _skia.Close(animated: false);
            _native.Close(animated: false);
        });
    }

    /// <summary>The mode and invoke behavior chosen, on the items of both views (also on items toggled on later).</summary>
    private void ApplyItemOptions()
    {
        _skia.RightItems.Mode = _native.RightItems.Mode = _rightMode;
        foreach (var items in new[] { _skia.LeftItems, _skia.RightItems, _skia.TopItems, _native.LeftItems, _native.RightItems, _native.TopItems })
            items.SwipeBehaviorOnInvoked = _behavior;
    }

    /// <summary>MAUI's own item types serve both views: the drawn one draws them.</summary>
    private static SwipeItems LeftItems(Action<string> report) =>
    [
        Item(Favorite, "★", Color.FromArgb("#2E7D32"), report)
    ];

    private static SwipeItems RightItems(Action<string> report) =>
    [
        Item(Archive, "▤", Color.FromArgb("#546E7A"), report),
        Item(Delete, "✕", DemoColors.SampleA, report)
    ];

    private static SwipeItem Item(string text, string glyph, Color background, Action<string> report)
    {
        var item = new SwipeItem
        {
            Text = text,
            BackgroundColor = background,
            IconImageSource = new FontImageSource { Glyph = glyph, FontFamily = DemoFonts.OpenSansRegular, Size = 20, Color = Colors.White }
        };
        item.Invoked += (_, _) => report($"invoked {text}");
        return item;
    }

    /// <summary>A drawn item view (<see cref="SkUiSwipeItemView"/>) above the content.</summary>
    private SwipeItems SkiaTopItems()
    {
        var item = new SkUiSwipeItemView
        {
            Background = DemoColors.SoftSurface,
            Content = new SkUiLabel
            {
                Text = Check, TextColor = Ink, FontAttributes = FontAttributes.Bold, Padding = new Thickness(12),
                HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center
            }
        };
        item.Invoked += (_, _) => SkiaStatus($"invoked {Check}");
        return [item];
    }

    /// <summary>MAUI's item view (native) above the content.</summary>
    private SwipeItems NativeTopItems()
    {
        var item = new SwipeItemView
        {
            Background = DemoColors.SoftSurface,
            Content = new Label
            {
                Text = Check, TextColor = Ink, FontAttributes = FontAttributes.Bold, Padding = new Thickness(12),
                HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center
            }
        };
        item.Invoked += (_, _) => NativeStatus($"invoked {Check}");
        return [item];
    }

    private const string CardTitle = "Swipe me";
    private const string CardHint = "Left or right; down with top items";

    private static SkUiBorder SkiaCard() => new()
    {
        Background = Colors.White, StrokeThickness = 0, Padding = new Thickness(16, 8),
        Content = new SkUiVerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new SkUiLabel { Text = CardTitle, TextColor = Ink, FontSize = 16, FontAttributes = FontAttributes.Bold },
                new SkUiLabel { Text = CardHint, TextColor = DemoColors.Caption, FontSize = 12 }
            }
        }
    };

    private static Border NativeCard() => new()
    {
        Background = Colors.White, StrokeThickness = 0, Padding = new Thickness(16, 8),
        Content = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = CardTitle, TextColor = Ink, FontSize = 16, FontAttributes = FontAttributes.Bold },
                new Label { Text = CardHint, TextColor = DemoColors.Caption, FontSize = 12 }
            }
        }
    };

    private void SkiaStatus(string text)
    {
        _skiaLast = text;
        Feedback(_skiaLast, _nativeLast);
    }

    private void NativeStatus(string text)
    {
        _nativeLast = text;
        Feedback(_skiaLast, _nativeLast);
    }
}
