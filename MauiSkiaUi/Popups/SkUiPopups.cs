using System.Diagnostics;

namespace MauiSkiaUi;

/// <summary>
/// App-wide popup settings of drawn controls: the <see cref="Presenter"/> that shows their popups through the app's popup
/// system. Controls can override it (a picker's <c>PopupPresenter</c>).
/// </summary>
public static class SkUiPopups
{
    private static bool _fallbackReported;

    /// <summary>
    /// The presenter drawn controls show popups with (UI thread); <c>null</c> (default): MAUI's action sheet
    /// (<see cref="SkUiActionSheetPopupPresenter"/>, centered plain item texts: MAUI's UI, not SkiaUi's), with one trace line
    /// the first time. Also set by <c>UseSkiaUi(options =&gt; options.PopupPresenter = …)</c>.
    /// </summary>
    public static ISkUiPopupPresenter? Presenter { get; set; }

    /// <summary>
    /// Shows <paramref name="content"/> in the window, through <paramref name="presenter"/> or the app's <see cref="Presenter"/>, and completes once it is gone; a view of
    /// the app's or a package's on the same contract as a picker's list (<see cref="ISkUiPopupPresenter"/>).
    /// </summary>
    /// <param name="anchor">The drawn view that opens it: its window and surface place the popup.</param>
    /// <param name="content">What to show (a drawn view).</param>
    /// <param name="preferredSize">The size wanted: kept inside the window's safe area, and inside the room a presenter reports while it shows (the part the soft keyboard leaves). Its width is decided at opening.</param>
    /// <param name="presenter">The presenter; <c>null</c>: <see cref="Presenter"/>.</param>
    /// <param name="opened">Gets the request, whose <see cref="SkUiPopupRequest.Cancel"/> closes the popup.</param>
    /// <param name="cancellationToken">Closes the popup, on the thread that shows it. Already cancelled: nothing is shown.</param>
    /// <returns><c>false</c> when nothing was shown: no presenter, no surface, or cancelled. One popup per control at a time is the rule of <see cref="ISkUiPopupPresenter"/>, and the caller's to keep.</returns>
    public static async Task<bool> ShowContentAsync(SkUiView anchor, View content, Size preferredSize, ISkUiPopupPresenter? presenter = null,
        Action<SkUiPopupRequest>? opened = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        ArgumentNullException.ThrowIfNull(content);
        if (cancellationToken.IsCancellationRequested || (presenter ?? Presenter) is not { } shownBy || SkUiDiagnostics.GetSurfaceRoot(anchor) is not { } surfaceRoot)
            return false;
        var look = SkUiLook.Current;
        var rootBounds = SkUiDiagnostics.GetRootBounds(anchor) ?? Rect.Zero;
        var origin = SkUiWindowMetrics.SurfaceOrigin(surfaceRoot);
        var layout = SkUiPopupGeometry.Layout(new SkUiPopupPlacementInput(rootBounds.Offset(origin.X, origin.Y), rootBounds, SkUiWindowMetrics.WindowSize(surfaceRoot),
            SkUiWindowMetrics.SafeAreaInsets(surfaceRoot), look.PopupMargin, look.PopupAnchorGap, SkUiPopupPlacement.Window, LayoutOptions.Center, LayoutOptions.Center,
            preferredSize, double.PositiveInfinity, default, anchor.IsRightToLeft));
        var request = new SkUiPopupRequest(anchor, surfaceRoot, content, layout)
        {
            OverlayColor = look.GetPopupOverlayColor(layout.Placement),
            IsAnimated = !SkUiMotion.IsMotionReduced
        };
        request.CornerRadius = look.GetPopupCornerRadius(layout.Placement);
        request.Shadow = look.GetPopupShadow(layout.Placement);
        SkUiPopupParts.Fit(content, layout.Bounds, request.CornerRadius);
        request.AvailableAreaChanged = () =>
        {
            if (!request.FollowsBounds)
                return;
            var bounds = SkUiPopupGeometry.Fitted(layout, layout.Bounds.Height, request.AvailableArea, look.PopupItemHeight, out _);
            if (bounds == request.Bounds)
                return;
            SkUiPopupParts.Fit(content, bounds, request.CornerRadius);
            request.MoveTo(bounds);
        };
        // A token can be cancelled on any thread, and a presenter closes on the UI thread only. Not through the synchronization context: a token waits there for it to run.
        var dispatcher = Dispatcher.GetForCurrentThread();
        await using var cancel = cancellationToken.Register(() =>
        {
            if (dispatcher?.IsDispatchRequired == true)
                dispatcher.Dispatch(request.Cancel);
            else
                request.Cancel();
        });
        var shown = false;
        try
        {
            opened?.Invoke(request);
            shown = true;
            await shownBy.ShowAsync(request, request.CloseRequested);
        }
        catch (OperationCanceledException)
        {
            // Closed: the same as returning.
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"SkiaUi: showing a popup failed: {exception.Message}");
        }
        finally
        {
            request.MarkCompleted();
        }
        return shown;
    }

    /// <summary>The presenter a control without its own uses: <see cref="Presenter"/>, else the action sheet fallback.</summary>
    internal static ISkUiPopupPresenter Resolve(ISkUiPopupPresenter? own)
    {
        if ((own ?? Presenter) is { } presenter)
            return presenter;
        if (!_fallbackReported)
        {
            _fallbackReported = true;
            Trace.WriteLine($"SkiaUi: no popup presenter is registered ({nameof(SkUiPopups)}.{nameof(Presenter)}, UseSkiaUi): pickers open MAUI's action sheet. "
                + "Register a presenter on your popup system to show SkiaUi's drawn lists.");
        }
        return SkUiActionSheetPopupPresenter.Instance;
    }
}

/// <summary>
/// Shows list requests (<see cref="SkUiListPopupRequest"/>, a picker's) as MAUI's action sheet on the anchor's page
/// (<see cref="Page.DisplayActionSheetAsync(string, string, string, string[])"/>): centered, plain item texts, no
/// selection mark, MAUI's own UI. The fallback when no presenter is registered; requests of other kinds are not shown.
/// </summary>
/// <remarks>
/// An action sheet cannot be closed by code: when the control closes the popup (<c>IsOpen = false</c>), the sheet stays until
/// the user answers, and that answer is ignored. Items with the same text cannot be told apart (the first of them is chosen),
/// and an item with the text of the cancel button (<see cref="CancelText"/>) cannot be chosen.
/// </remarks>
public sealed class SkUiActionSheetPopupPresenter : ISkUiPopupPresenter
{
    /// <summary>The shared presenter.</summary>
    public static SkUiActionSheetPopupPresenter Instance { get; } = new();

    /// <summary>The text of the sheet's cancel button; <c>null</c> (default): the request's, <see cref="SkUiPopupRequest.CancelText"/> (<see cref="SkUiStrings.CancelText"/> unless the control sets its own).</summary>
    public string? CancelText { get; set; }

    /// <inheritdoc />
    public async Task ShowAsync(SkUiPopupRequest request, CancellationToken cancellationToken)
    {
        if (request is not SkUiListPopupRequest list || list.Page is not { } page || list.Items.Count == 0)
        {
            Trace.WriteLine($"SkiaUi: {nameof(SkUiActionSheetPopupPresenter)} shows lists to choose one item from, on a page only: the popup was not shown (register a presenter for several choices and wheels).");
            return;
        }
        var items = list.Items;
        var buttons = new string[items.Count];
        for (var index = 0; index < buttons.Length; index++)
            buttons[index] = items[index];
        var cancel = CancelText ?? list.CancelText;
        var chosen = await page.DisplayActionSheetAsync(list.Title, cancel, null, list.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight, buttons);
        // MAUI answers the cancel button's text for a cancel (or a tap outside): an item with that text is not chosen.
        if (chosen is null || chosen == cancel || cancellationToken.IsCancellationRequested)
            return;
        var chosenIndex = Array.IndexOf(buttons, chosen);
        if (chosenIndex < 0)
            return;
        // The sheet has no OK button: a tap on an item is the choice and the acceptance at once (its cancel button is Cancel), so a
        // choice that waits for OK (RequiresAccept) is highlighted and accepted.
        if (list.RequiresAccept)
        {
            if (list.SetCurrentIndex(chosenIndex))
                list.Accept();
        }
        else
        {
            list.Complete(chosenIndex);
        }
    }
}
