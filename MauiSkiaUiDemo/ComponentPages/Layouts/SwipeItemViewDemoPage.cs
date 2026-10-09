using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiSwipeItemView"/>: swipe items of drawn views, next to MAUI's <see cref="SwipeItemView"/> (native views).
/// The left item is a reply button laid out by the app (as wide as it measures); the right one holds a switch that stays
/// interactive while the items are open. A toggle makes the reply command unable to execute, which disables its item.
/// Both views start open on the left.
/// </summary>
public sealed class SwipeItemViewDemoPage : ComponentDemoPage
{
    private const string Reply = "Reply";
    private const string Mute = "Mute";
    private const string OpenLeft = "Open left";
    private const string OpenRight = "Open right";
    private const string CloseItems = "Close";
    private const string CardText = "Swipe for custom items";

    private readonly SkUiSwipeView _skia;
    private readonly SwipeView _native;
    private readonly Command _reply;
    private bool _canReply = true;
    private int _replies;

    public SwipeItemViewDemoPage()
        : base(nameof(SkUiSwipeItemView), new SkUiSwipeView(), new SwipeView(), widthRange: (200, 400, 300), heightRange: (48, 160, 72))
    {
        _skia = (SkUiSwipeView)SkiaControl;
        _native = (SwipeView)NativeControl!;
        _reply = new Command(() => Feedback($"replies: {++_replies}", $"replies: {_replies}"), () => _canReply);

        _skia.Content = new SkUiBorder
        {
            Background = Colors.White, StrokeThickness = 0, Padding = new Thickness(16, 8),
            Content = new SkUiLabel { Text = CardText, TextColor = Ink, FontSize = 16, VerticalTextAlignment = TextAlignment.Center }
        };
        _skia.LeftItems = [SkiaReplyItem()];
        _skia.RightItems = [SkiaMuteItem()];
        _native.Content = new Border
        {
            Background = Colors.White, StrokeThickness = 0, Padding = new Thickness(16, 8),
            Content = new Label { Text = CardText, TextColor = Ink, FontSize = 16, VerticalTextAlignment = TextAlignment.Center }
        };
        _native.LeftItems = [NativeReplyItem()];
        _native.RightItems = [NativeMuteItem()];
        _skia.Open(OpenSwipeItem.LeftItems, animated: false);
        Loaded += (_, _) => _native.Open(OpenSwipeItem.LeftItems, animated: false);

        Choice("LeftItems." + nameof(SwipeItems.Mode), [SwipeMode.Reveal, SwipeMode.Execute], SwipeMode.Reveal,
            value => { _skia.LeftItems.Mode = value; _native.LeftItems.Mode = value; },
            () => _skia.LeftItems.Mode, () => _native.LeftItems.Mode);
        Toggle("Reply can execute", true, value => { _canReply = value; _reply.ChangeCanExecute(); }, () => _canReply);
        ActionButton(OpenLeft, () => { _skia.Open(OpenSwipeItem.LeftItems); _native.Open(OpenSwipeItem.LeftItems); });
        ActionButton(OpenRight, () => { _skia.Open(OpenSwipeItem.RightItems); _native.Open(OpenSwipeItem.RightItems); });
        ActionButton(CloseItems, () => { _skia.Close(); _native.Close(); });
    }

    private SkUiSwipeItemView SkiaReplyItem() => new()
    {
        Command = _reply,
        Background = DemoColors.SampleB,
        Padding = new Thickness(20, 0),
        Content = new SkUiVerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new SkUiLabel { Text = "↩", FontSize = 22, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center },
                new SkUiLabel { Text = Reply, FontSize = 13, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center }
            }
        }
    };

    private SwipeItemView NativeReplyItem() => new()
    {
        Command = _reply,
        Background = DemoColors.SampleB,
        Padding = new Thickness(20, 0),
        Content = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = "↩", FontSize = 22, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center },
                new Label { Text = Reply, FontSize = 13, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center }
            }
        }
    };

    private static SkUiSwipeItemView SkiaMuteItem() => new()
    {
        Background = DemoColors.SoftSurface,
        Padding = new Thickness(12, 0),
        Content = new SkUiHorizontalStackLayout
        {
            Spacing = 8, VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new SkUiLabel { Text = Mute, FontSize = 13, TextColor = Ink, VerticalOptions = LayoutOptions.Center },
                new SkUiSwitch { VerticalOptions = LayoutOptions.Center }
            }
        }
    };

    private static SwipeItemView NativeMuteItem() => new()
    {
        Background = DemoColors.SoftSurface,
        Padding = new Thickness(12, 0),
        Content = new HorizontalStackLayout
        {
            Spacing = 8, VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = Mute, FontSize = 13, TextColor = Ink, VerticalOptions = LayoutOptions.Center },
                new Switch { VerticalOptions = LayoutOptions.Center }
            }
        }
    };
}
