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
/// window turns. An anchored or absolute one (a combo box) sits at <see cref="SkUiPopupRequest.Bounds"/> through its margins,
/// over a transparent page. The toolkit's popup page may be inset from the window (on iOS by the bottom safe area), so once
/// shown every popup is moved by the difference between where it landed and <see cref="SkUiPopupRequest.Bounds"/>.
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

    /// <summary>The popup's corner radius (the drawn content is clipped to it); default 10.</summary>
    public double CornerRadius { get; init; } = 10;

    /// <summary>The popup's shadow; default a soft drop shadow. <c>null</c>: none.</summary>
    public Shadow? Shadow { get; init; } = new() { Brush = Brush.Black, Opacity = 0.25f, Radius = 16, Offset = new Point(0, 4) };

    /// <inheritdoc />
    public async Task ShowAsync(SkUiPopupRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Page is not { } page || request.Content is not { } content || cancellationToken.IsCancellationRequested)
            return;
        var (popup, options) = Create(request, content);
        var bounds = request.Bounds;
        popup.Opened += (_, _) => CorrectOffset(popup, content, bounds);
        await using var registration = cancellationToken.Register(() => _ = CloseAsync(popup));
        await page.ShowPopupAsync(popup, options, CancellationToken.None);
    }

    /// <summary>The toolkit popup and options showing <paramref name="content"/> where <paramref name="request"/> asks.</summary>
    internal (Popup Popup, PopupOptions Options) Create(SkUiPopupRequest request, View content)
    {
        var positioned = IsPositioned(request);
        var bounds = request.Bounds;
        var popup = new Popup
        {
            Content = content,
            Padding = 0,
            // Opaque, as the drawn content: Android casts a view's shadow from its background's outline (a clear one casts none).
            BackgroundColor = content.BackgroundColor ?? SkUiColors.DefaultBackground,
            CanBeDismissedByTappingOutsideOfPopup = request.IsDismissible,
            HorizontalOptions = positioned ? LayoutOptions.Start : request.HorizontalOptions,
            VerticalOptions = positioned ? LayoutOptions.Start : request.VerticalOptions,
            Margin = positioned ? new Thickness(bounds.Left, bounds.Top, 0, 0) : WindowMargin(request)
        };
        var options = new PopupOptions
        {
            CanBeDismissedByTappingOutsideOfPopup = request.IsDismissible,
            PageOverlayColor = request.OverlayColor ?? Colors.Transparent,
            Shape = new RoundRectangle { CornerRadius = CornerRadius, StrokeThickness = 0 },
            Shadow = Shadow
        };
        return (popup, options);
    }

    /// <summary>Whether the popup goes at <see cref="SkUiPopupRequest.Bounds"/> (anchored, absolute) rather than aligned in the window.</summary>
    private static bool IsPositioned(SkUiPopupRequest request) => request.Placement != SkUiPopupPlacement.Window;

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
    /// Moving both opposite margins moves the popup whatever its alignment (start, center, end, fill).
    /// </summary>
    private static void CorrectOffset(Popup popup, View content, Rect bounds)
    {
        if (SkUiDiagnostics.GetWindowBounds(content) is not { } shown)
            return;
        var dx = bounds.Left - shown.Left;
        var dy = bounds.Top - shown.Top;
        if (Math.Abs(dx) < 0.5 && Math.Abs(dy) < 0.5)
            return;
        popup.Margin = Moved(popup.Margin, dx, dy);
    }

    /// <summary>Margins that move a popup by (<paramref name="dx"/>, <paramref name="dy"/>) whatever its alignment.</summary>
    internal static Thickness Moved(Thickness margin, double dx, double dy) =>
        new(margin.Left + dx, margin.Top + dy, margin.Right - dx, margin.Bottom - dy);

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
