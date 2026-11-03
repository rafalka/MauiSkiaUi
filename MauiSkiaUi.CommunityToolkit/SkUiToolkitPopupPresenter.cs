using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls.Shapes;

namespace MauiSkiaUi.CommunityToolkit;

/// <summary>
/// Shows SkiaUi's popups (a picker's drawn list) with CommunityToolkit.Maui's <see cref="Popup"/>: an
/// <see cref="ISkUiPopupPresenter"/> on the toolkit's popup system. Register it app-wide with
/// <see cref="SkUiCommunityToolkitAppBuilderExtensions.UseSkiaUiCommunityToolkit"/> (the app also calls the toolkit's
/// <c>UseMauiCommunityToolkit()</c>), or per control (a picker's <c>PopupPresenter</c>).
/// </summary>
/// <remarks>
/// <para>
/// Toolkit popups (v2) are modal pages placed by alignment and margins. A window popup (a centered dialog, a bottom sheet) is
/// aligned with the request's <see cref="SkUiPopupRequest.HorizontalOptions"/> / <see cref="SkUiPopupRequest.VerticalOptions"/>
/// inside the window's safe area, over a page dimmed by <see cref="SkUiPopupRequest.OverlayColor"/>, and stays aligned as the
/// window turns. An anchored or absolute one (a combo box) sits at <see cref="SkUiPopupRequest.Bounds"/> through its margins, over a
/// transparent page (on Windows without the layer fill MAUI puts behind modal pages, which would wash the page out); a bottom pane is aligned to the page's bottom (so its height always fits), square and undimmed. The toolkit's popup page may be inset from the window (on iOS by the bottom safe area), so once
/// shown every popup is moved by the difference between where it landed and <see cref="SkUiPopupRequest.Bounds"/>. It follows
/// <see cref="SkUiPopupRequest.BoundsChanged"/> (an anchored list shrinking to the rows a search shows, a popup fitting the room the
/// keyboard leaves) by moving a popup at its bounds as much as they moved, and others by their alignment. The popup page keeps its
/// bottom clear of the soft keyboard (<c>SafeAreaEdges</c> with <c>SoftInput</c>, MAUI's own inset handling), and the room it leaves
/// is reported with <see cref="SkUiPopupRequest.SetAvailableArea"/>; on Android, where MAUI leaves the page under the keyboard in the
/// default <c>adjustPan</c>, the room also ends at the keyboard's inset in the popup's window, and window popups and the bottom pane
/// rise above it.
/// </para>
/// <para>
/// A tap outside closes it (when <see cref="SkUiPopupRequest.IsDismissible"/>), as does Back on Android; when SkiaUi asks the
/// popup to close (an item chosen, the control closed it), the token closes it with <see cref="Popup.CloseAsync"/>.
/// </para>
/// </remarks>
public sealed class SkUiToolkitPopupPresenter : ISkUiPopupPresenter
{
    /// <summary>The shared presenter (it keeps no state between popups).</summary>
    public static SkUiToolkitPopupPresenter Instance { get; } = new();

    /// <summary>
    /// The popup's corner radius for every popup, in place of the look's (<see cref="SkUiPopupRequest.CornerRadius"/>, which
    /// the drawn content is clipped to); <c>NaN</c> (default): the look's. Bottom panes are square either way.
    /// </summary>
    public double CornerRadius { get; init; } = double.NaN;

    /// <summary>
    /// The popup's shadow for every popup, in place of the look's (<see cref="SkUiPopupRequest.Shadow"/>); <c>null</c> once
    /// set: none. Unset (default): the look's.
    /// </summary>
    public Shadow? Shadow
    {
        get;
        init
        {
            field = value;
            _hasShadow = true;
        }
    }

    private readonly bool _hasShadow;

    /// <inheritdoc />
    public async Task ShowAsync(SkUiPopupRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Page is not { } page || request.Content is not { } content || cancellationToken.IsCancellationRequested)
            return;
        var (popup, options) = Create(request, content);
        var bounds = request.Bounds;
        // A search resizes an anchored popup, and every popup fits into the room the keyboard leaves: the height follows the content's;
        // a popup at its bounds moves by as much as they moved, one aligned in the page (window popups, the bottom pane) by its
        // alignment in the page, which keeps clear of the keyboard.
        void OnBoundsChanged(object? sender, EventArgs args)
        {
            var moved = request.Bounds;
            if (IsAtBounds(request))
                popup.Margin = MovedAtStart(popup.Margin, moved.Left - bounds.Left, moved.Top - bounds.Top);
            bounds = moved;
        }
        request.BoundsChanged += OnBoundsChanged;
        View? room = null;
        Page? roomPage = null;
        // The keyboard's cover of the page that the page does not keep clear of itself, which aligned popups already rose by.
        var covered = 0.0;
#if ANDROID
        Android.Views.View? keyboardView = null;
        // The observer of the window the page is attached to: a view's observer before it is attached, or after it is detached, is
        // another one, so the listener is added on attach and removed from the same observer on detach.
        Android.Views.ViewTreeObserver? layoutObserver = null;
        void Observe()
        {
            if (layoutObserver is null && keyboardView?.ViewTreeObserver is { IsAlive: true } observer)
            {
                layoutObserver = observer;
                observer.GlobalLayout += OnRoomChanged;
            }
        }
        void StopObserving()
        {
            if (layoutObserver is { IsAlive: true } observer)
                observer.GlobalLayout -= OnRoomChanged;
            layoutObserver = null;
        }
        void OnAttached(object? sender, Android.Views.View.ViewAttachedToWindowEventArgs args) => Observe();
        void OnDetached(object? sender, Android.Views.View.ViewDetachedFromWindowEventArgs args) => StopObserving();
#endif
        // The popup page keeps its bottom clear of the soft keyboard (MAUI's SafeAreaEdges); the room left is reported to SkiaUi. The
        // layout's frame is read (in the page, which fills the window): its platform view gets its new frame only after this event.
        void OnRoomChanged(object? sender, EventArgs args)
        {
            if (room is not { Width: > 0, Height: > 0 } || roomPage is null || PlatformWindowBounds(roomPage) is not { } page)
                return;
            var frame = room.Frame.Offset(page.X, page.Y);
            var area = frame;
#if ANDROID
            if (keyboardView is not null && KeyboardTop(keyboardView) is { } keyboardTop)
                area = AboveKeyboard(frame, keyboardTop);
#endif
            if (!IsAtBounds(request))
            {
                var lift = AlignedLift(request, frame, area, covered);
                covered = frame.Bottom - area.Bottom;
                popup.Margin = new Thickness(popup.Margin.Left, popup.Margin.Top, popup.Margin.Right, popup.Margin.Bottom + lift);
            }
            request.SetAvailableArea(area);
        }
        void OnHandlerChanged(object? sender, EventArgs args)
        {
            popup.HandlerChanged -= OnHandlerChanged;
            if (PageOf(popup) is not ContentPage { Content: { } layout } popupPage)
                return;
            // Edge to edge, as the margins expect, but for the keyboard: on iOS a page keeps clear of the safe area by default, and
            // the toolkit's layout (a grid) keeps clear of the bars on every platform.
            if (!popupPage.IsSet(ContentPage.SafeAreaEdgesProperty))
                popupPage.SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.SoftInput);
            if (layout is Layout grid && !grid.IsSet(Layout.SafeAreaEdgesProperty))
                grid.SafeAreaEdges = SafeAreaEdges.None;
            room = layout;
            roomPage = popupPage;
            room.SizeChanged += OnRoomChanged;
#if ANDROID
            // Android pans the window for the keyboard by default (adjustPan), and MAUI then leaves the page's size as it is: the
            // keyboard's inset in the popup's own window is read after each layout too (the app's pages are not touched).
            if (popupPage.Handler?.PlatformView is Android.Views.View platform)
            {
                keyboardView = platform;
                platform.ViewAttachedToWindow += OnAttached;
                platform.ViewDetachedFromWindow += OnDetached;
                if (platform.IsAttachedToWindow)
                    Observe();
            }
#endif
        }
        popup.HandlerChanged += OnHandlerChanged;
        popup.Opened += (_, _) =>
        {
            // A close asked for before the toolkit attached the popup could not close it then (it throws until the popup is
            // shown, and CloseAsync ignores that): close it now.
            if (cancellationToken.IsCancellationRequested)
            {
                _ = CloseAsync(popup);
                return;
            }
            CorrectOffset(popup, content, bounds, IsAtBounds(request));
#if WINDOWS
            ClearModalLayerFill(popup);
#endif
        };
        await using var registration = cancellationToken.Register(() => _ = CloseAsync(popup));
        try
        {
            await page.ShowPopupAsync(popup, options, CancellationToken.None);
        }
        finally
        {
            request.BoundsChanged -= OnBoundsChanged;
            popup.HandlerChanged -= OnHandlerChanged;
            if (room is not null)
                room.SizeChanged -= OnRoomChanged;
#if ANDROID
            StopObserving();
            if (keyboardView is not null)
            {
                keyboardView.ViewAttachedToWindow -= OnAttached;
                keyboardView.ViewDetachedFromWindow -= OnDetached;
            }
#endif
        }
    }

    /// <summary>The toolkit popup and options showing <paramref name="content"/> where <paramref name="request"/> asks.</summary>
    internal (Popup Popup, PopupOptions Options) Create(SkUiPopupRequest request, View content)
    {
        var positioned = IsPositioned(request);
        var pane = request.Placement == SkUiPopupPlacement.BottomPane;
        var bounds = request.Bounds;
        var popup = new Popup
        {
            Content = content,
            Padding = 0,
            // Opaque, as the drawn content: Android casts a view's shadow from its background's outline (a clear one casts none).
            BackgroundColor = content.BackgroundColor ?? SkUiColors.DefaultBackground,
            CanBeDismissedByTappingOutsideOfPopup = request.IsDismissible,
            // A pane is aligned to the popup page's bottom, so its whole height fits there even when the page is inset (iOS: by the
            // safe area); once shown it moves down to the window's edge.
            HorizontalOptions = positioned ? LayoutOptions.Start : request.HorizontalOptions,
            VerticalOptions = pane ? LayoutOptions.End : positioned ? LayoutOptions.Start : request.VerticalOptions,
            Margin = Kept(pane ? new Thickness(bounds.Left, 0, 0, 0) : positioned ? new Thickness(bounds.Left, bounds.Top, 0, 0) : WindowMargin(request))
        };
        var options = new PopupOptions
        {
            CanBeDismissedByTappingOutsideOfPopup = request.IsDismissible,
            PageOverlayColor = request.OverlayColor ?? Colors.Transparent,
            // The content's corners; a bottom pane is square, docked to the window's edges.
            Shape = new RoundRectangle
            {
                CornerRadius = pane ? 0 : double.IsNaN(CornerRadius) ? request.CornerRadius : CornerRadius,
                StrokeThickness = 0
            },
            Shadow = _hasShadow ? Shadow : request.Shadow
        };
        return (popup, options);
    }

    /// <summary>A margin too small to see, which stands for none.</summary>
    private const double NoMargin = 0.001;

    /// <summary>
    /// <paramref name="margin"/> as the toolkit keeps it: it replaces an empty margin (all zero: a pane, a popup at the window's
    /// origin) with its default one (30 on every side), so none is a margin too small to see.
    /// </summary>
    internal static Thickness Kept(Thickness margin) => margin.IsEmpty ? new Thickness(margin.Left, NoMargin, margin.Right, margin.Bottom) : margin;

    /// <summary>Whether the popup goes at <see cref="SkUiPopupRequest.Bounds"/> (anchored, absolute) rather than aligned in the window.</summary>
    private static bool IsPositioned(SkUiPopupRequest request) => request.Placement != SkUiPopupPlacement.Window;

    /// <summary>Whether the popup is placed at <see cref="SkUiPopupRequest.Bounds"/> by its margins (anchored, absolute), not aligned in the page.</summary>
    private static bool IsAtBounds(SkUiPopupRequest request) => request.Placement is SkUiPopupPlacement.Anchored or SkUiPopupPlacement.Absolute;

    /// <summary>The part of the window's bottom safe area inset inside <paramref name="area"/> (none once the keyboard covers it).</summary>
    private static double KeptSafeBottom(SkUiPopupRequest request, Rect area)
    {
        var below = request.WindowSize.Height - area.Bottom;
        return area is { Width: > 0, Height: > 0 } && request.WindowSize.Height > 0 && below > 0
            ? Math.Max(0, request.SafeAreaInsets.Bottom - below)
            : request.SafeAreaInsets.Bottom;
    }

    /// <summary>The part of <paramref name="area"/> (window DIPs) above a soft keyboard beginning at <paramref name="keyboardTop"/>.</summary>
    internal static Rect AboveKeyboard(Rect area, double keyboardTop) =>
        keyboardTop < area.Bottom ? new Rect(area.X, area.Y, area.Width, Math.Max(0, keyboardTop - area.Y)) : area;

    /// <summary>
    /// How far a popup aligned in the page (a window popup, the bottom pane) rises as the room becomes <paramref name="area"/>
    /// in a page laid out to <paramref name="frame"/>, <paramref name="covered"/> having been covered before: by the
    /// keyboard's cover of the page that the page does not keep clear of itself (Android's adjustPan), and for a window popup by
    /// the part of the bottom safe area the keyboard covers now (its margin keeps none over the home indicator).
    /// </summary>
    internal static double AlignedLift(SkUiPopupRequest request, Rect frame, Rect area, double covered)
    {
        var lift = frame.Bottom - area.Bottom - covered;
        if (request.Placement == SkUiPopupPlacement.Window)
            lift += KeptSafeBottom(request, area) - KeptSafeBottom(request, request.AvailableArea);
        return lift;
    }

    private static Page? PageOf(Element element)
    {
        var current = element.Parent;
        while (current is not null and not Page)
            current = current.Parent;
        return current as Page;
    }

#if ANDROID
    /// <summary>Where the soft keyboard begins in <paramref name="view"/>'s window (DIPs); <c>null</c> while it is hidden.</summary>
    private static double? KeyboardTop(Android.Views.View view)
    {
        var ime = AndroidX.Core.View.WindowInsetsCompat.Type.Ime();
        if (AndroidX.Core.View.ViewCompat.GetRootWindowInsets(view) is not { } insets || !insets.IsVisible(ime)
            || insets.GetInsets(ime) is not { } keyboard || view.RootView is not { Height: > 0 } root)
            return null;
        var density = view.Resources?.DisplayMetrics?.Density ?? 1;
        return (root.Height - keyboard.Bottom) / density;
    }
#endif

    /// <summary>The window bounds (DIPs) of <paramref name="view"/>'s platform view; <c>null</c> without one in a window.</summary>
    private static Rect? PlatformWindowBounds(VisualElement view)
    {
#if IOS || MACCATALYST
        if (view.Handler?.PlatformView is UIKit.UIView { Window: not null } platform)
        {
            var rect = platform.ConvertRectToView(platform.Bounds, null);
            return new Rect(rect.X, rect.Y, rect.Width, rect.Height);
        }
#elif ANDROID
        if (view.Handler?.PlatformView is Android.Views.View { IsAttachedToWindow: true } platform)
        {
            var location = new int[2];
            platform.GetLocationInWindow(location);
            var density = platform.Resources?.DisplayMetrics?.Density ?? 1;
            return new Rect(location[0] / density, location[1] / density, platform.Width / density, platform.Height / density);
        }
#elif WINDOWS
        if (view.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement { XamlRoot: not null } platform)
        {
            var origin = platform.TransformToVisual(null).TransformPoint(new global::Windows.Foundation.Point(0, 0));
            return new Rect(origin.X, origin.Y, platform.ActualWidth, platform.ActualHeight);
        }
#endif
        _ = view;
        return null;
    }

    /// <summary>A window popup's margin: the look's, inside the safe area.</summary>
    private static Thickness WindowMargin(SkUiPopupRequest request)
    {
        var margin = request.Margin;
        var safe = request.SafeAreaInsets;
        return new Thickness(margin.Left + safe.Left, margin.Top + safe.Top, margin.Right + safe.Right, margin.Bottom + safe.Bottom);
    }

    /// <summary>
    /// Moves a shown popup to where SkiaUi put it (<paramref name="bounds"/>), by how far it landed from there: the popup page
    /// may be inset from the window (the toolkit's page is inset by the bottom safe area on iOS; edge to edge elsewhere).
    /// Moving both opposite margins moves the popup whatever its alignment (start, center, end, fill); one at its bounds (start-aligned)
    /// moves by its left and top margins only.
    /// </summary>
    private static void CorrectOffset(Popup popup, View content, Rect bounds, bool atStart)
    {
        if (SkUiDiagnostics.GetWindowBounds(content) is not { } shown)
            return;
        var dx = bounds.Left - shown.Left;
        var dy = bounds.Top - shown.Top;
        if (Math.Abs(dx) < 0.5 && Math.Abs(dy) < 0.5)
            return;
        popup.Margin = atStart ? MovedAtStart(popup.Margin, dx, dy) : Moved(popup.Margin, dx, dy);
    }

    /// <summary>
    /// Margins that move a start-aligned popup (anchored, absolute) by (<paramref name="dx"/>, <paramref name="dy"/>): only the left
    /// and top ones, so the popup page's layout never runs out of room for it (an end margin grown by a move up would squeeze it).
    /// </summary>
    internal static Thickness MovedAtStart(Thickness margin, double dx, double dy) => margin with { Left = margin.Left + dx, Top = margin.Top + dy };

    /// <summary>Margins that move a popup by (<paramref name="dx"/>, <paramref name="dy"/>) whatever its alignment.</summary>
    internal static Thickness Moved(Thickness margin, double dx, double dy) =>
        new(margin.Left + dx, margin.Top + dy, margin.Right - dx, margin.Bottom - dy);

#if WINDOWS
    /// <summary>
    /// MAUI shows a modal page on Windows inside a navigation view of its own, whose content area has Fluent's layer fill (white
    /// at 50 % in the light theme): over a transparent popup page it washes out the whole page below, also under an anchored
    /// popup, which dims nothing, and on top of a window popup's <see cref="SkUiPopupRequest.OverlayColor"/>. Clears the
    /// backgrounds between the popup's page and that modal's root (the popup's own frame is inside the page, not touched).
    /// </summary>
    private static void ClearModalLayerFill(Popup popup)
    {
        Element? element = popup;
        while (element is not null and not Page)
            element = element.Parent;
        if (element?.Handler?.PlatformView is not Microsoft.UI.Xaml.DependencyObject pageView)
            return;
        for (var node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(pageView);
             node is not null and not Microsoft.Maui.Platform.WindowRootView;
             node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node))
        {
            if (node is Microsoft.UI.Xaml.Controls.Panel { Background: not null } panel)
                panel.Background = null;
        }
    }
#endif

    private static async Task CloseAsync(Popup popup)
    {
        try
        {
            await popup.CloseAsync();
        }
        catch (InvalidOperationException)
        {
            // Already closing (a tap outside at the same time), or blocked by a modal page above it: it closes with it.
        }
    }
}
