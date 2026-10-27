using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// The (?) popups of a component page's editors: one bubble drawn over the editors' surface, next to the editor's (?) button.
/// A tap on the button opens it until the next tap anywhere (or a scroll); a hovering pointer (desktops, an iPad's
/// trackpad) opens it after a moment over the editor's title or button, as a tooltip would, and closes it once the pointer
/// left both them and the bubble.
/// </summary>
internal sealed class EditorHelpBubble
{
    private const double MaximumWidth = 380;
    private const double Gap = 4;
    private const double Inset = 8;
    private static readonly TimeSpan HoverOpenDelay = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan HoverCloseDelay = TimeSpan.FromMilliseconds(250);

    private readonly SkUiGrid _area;
    private readonly SkUiBox _dismiss;
    private readonly SkUiBorder _bubble;
    private readonly SkUiScrollView _scroll;
    private readonly SkUiVerticalStackLayout _content;
    private readonly SkUiLabel _title;
    private readonly SkUiLabel _source;
    private readonly SkUiLabel _body;
    private View? _header;
    private bool _pinned;
    private int _hoverVersion;

    /// <summary>Adds the bubble (and the layer that closes it) over the whole of <paramref name="area"/>, the editors' surface.</summary>
    public EditorHelpBubble(SkUiGrid area)
    {
        _area = area;
        var rows = Math.Max(1, area.RowDefinitions.Count);
        _dismiss = new SkUiBox { Color = Colors.Transparent, IsVisible = false, AutomationId = "EditorHelpDismiss" };
        _dismiss.Tapped += (_, _) => Hide();
        _title = new SkUiLabel { TextColor = DemoColors.Ink, FontSize = 14, FontFamily = DemoFonts.OpenSansSemibold };
        _source = new SkUiLabel { TextColor = DemoColors.Caption, FontSize = 11, FontFamily = DemoFonts.OpenSansRegular };
        _body = new SkUiLabel
        {
            TextColor = DemoColors.Ink, FontSize = 13, FontFamily = DemoFonts.OpenSansRegular, TextType = TextType.Html,
            LineHeight = 1.15, AutomationId = "EditorHelpText"
        };
        _body.LinkTapped += (_, args) => _ = Launcher.TryOpenAsync(args.Href);
        _content = new SkUiVerticalStackLayout { Spacing = 4, Children = { _title, _source, _body } };
        _scroll = new SkUiScrollView { Content = _content };
        _bubble = new SkUiBorder
        {
            Content = _scroll, Padding = new Thickness(12, 10), BackgroundColor = Colors.White, Stroke = DemoColors.Border, StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Shadow = new Shadow { Brush = Colors.Black, Offset = new Point(0, 4), Radius = 12, Opacity = 0.18f },
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start, IsVisible = false, AutomationId = "EditorHelp"
        };
        _bubble.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SkUiView.IsPointerOver) && !_bubble.IsPointerOver)
                HideAfterHover();
        };
        Grid.SetRowSpan(_dismiss, rows);
        Grid.SetRowSpan(_bubble, rows);
        area.Children.Add(_dismiss);
        area.Children.Add(_bubble);
    }

    /// <summary>Whether the bubble is shown.</summary>
    public bool IsOpen => _bubble.IsVisible;

    /// <summary>The description shown (HTML); empty when closed.</summary>
    public string Text => IsOpen ? _body.Text ?? string.Empty : string.Empty;

    /// <summary>
    /// The (?) button for an editor's <paramref name="header"/> (its title row, which also opens the bubble on hover) with
    /// <paramref name="help"/>.
    /// </summary>
    public SkUiButton CreateButton(View header, string title, EditorHelpEntry help)
    {
        var button = new SkUiButton
        {
            Text = "?", FontSize = 12, FontFamily = DemoFonts.OpenSansSemibold, TextColor = DemoColors.Caption, Background = Colors.Transparent,
            BorderColor = DemoColors.Caption, BorderWidth = 1, CornerRadius = 10, Padding = 0, WidthRequest = 20, HeightRequest = 20,
            MinimumHeightRequest = 0, MinimumWidthRequest = 0, VerticalOptions = LayoutOptions.Center, AutomationId = "Help" + title.Replace(" ", "")
        };
        SemanticProperties.SetDescription(button, "About " + title);
        button.Clicked += (_, _) =>
        {
            if (IsOpen && _pinned && ReferenceEquals(_header, header))
                Hide();
            else
                Show(header, button, title, help, pinned: true);
        };
        header.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(SkUiView.IsPointerOver) || _pinned)
                return;
            if (((SkUiView)header).IsPointerOver)
            {
                var version = ++_hoverVersion;
                Delay(HoverOpenDelay, () =>
                {
                    if (version == _hoverVersion && !_pinned && ((SkUiView)header).IsPointerOver)
                        Show(header, button, title, help, pinned: false);
                });
            }
            else
            {
                HideAfterHover();
            }
        };
        return button;
    }

    /// <summary>Shows <paramref name="help"/> by <paramref name="anchor"/>; pinned until the next tap, else while hovered.</summary>
    public void Show(View header, View anchor, string title, EditorHelpEntry help, bool pinned)
    {
        _hoverVersion++;
        _header = header;
        _pinned = pinned;
        _title.Text = title;
        _source.Text = help.Source ?? "Demo scenario";
        _body.Text = help.Html;
        _scroll.ScrollToAsync(0, 0, false);
        _dismiss.IsVisible = pinned;
        // Shown before it is measured: a hidden bubble's text is collapsed and measures nothing.
        _bubble.IsVisible = true;
        Place(anchor);
    }

    /// <summary>Closes the bubble.</summary>
    public void Hide()
    {
        _hoverVersion++;
        _pinned = false;
        _header = null;
        _dismiss.IsVisible = false;
        _bubble.IsVisible = false;
    }

    /// <summary>
    /// Below the anchor (start-aligned with it), or above it when only that fits; where neither does, on the roomier side,
    /// as tall as fits there, scrolling its text.
    /// </summary>
    private void Place(View anchor)
    {
        var areaWidth = _area.Width > 0 ? _area.Width : MaximumWidth + 2 * Inset;
        var areaHeight = _area.Height > 0 ? _area.Height : double.PositiveInfinity;
        var width = Math.Min(MaximumWidth, areaWidth - 2 * Inset);
        var chrome = _bubble.Padding.VerticalThickness + 2 * _bubble.StrokeThickness;
        var contentWidth = width - _bubble.Padding.HorizontalThickness - 2 * _bubble.StrokeThickness;
        var height = ((IView)_content).Measure(contentWidth, double.PositiveInfinity).Height + chrome;
        var bounds = SkUiDiagnostics.GetRootBounds(anchor) ?? new Rect(Inset, Inset, 0, 0);
        var below = areaHeight - bounds.Bottom - Gap - Inset;
        var above = bounds.Top - Gap - Inset;
        double top;
        if (height <= below || below >= above)
        {
            height = Math.Min(height, Math.Max(below, 0));
            top = bounds.Bottom + Gap;
        }
        else
        {
            height = Math.Min(height, above);
            top = bounds.Top - Gap - height;
        }
        var left = Math.Clamp(bounds.Left - Inset, Inset, Math.Max(Inset, areaWidth - width - Inset));
        _bubble.WidthRequest = width;
        _bubble.HeightRequest = height;
        _bubble.Margin = new Thickness(left, top, 0, 0);
    }

    /// <summary>A bubble opened by hovering closes once the pointer is over neither its editor's title nor the bubble.</summary>
    private void HideAfterHover()
    {
        var version = ++_hoverVersion;
        Delay(HoverCloseDelay, () =>
        {
            if (version == _hoverVersion && !_pinned && !_bubble.IsPointerOver && _header is SkUiView { IsPointerOver: false })
                Hide();
        });
    }

    private void Delay(TimeSpan delay, Action action) => _area.Dispatcher?.DispatchDelayed(delay, action);
}
