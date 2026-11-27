namespace MauiSkiaUi;

/// <summary>
/// A popup presenter that shows the popups of drawn controls (a picker's list, wheels or calendar; <see cref="SkUiPopups.ShowContentAsync"/>)
/// in an <see cref="SkUiSheet"/> over the anchor's surface, so they need no popup library: a bottom sheet fitted to the content on
/// compact widths and a dialog on wide ones (<see cref="SkUiSheetPresentation.Auto"/>). Register it app-wide
/// (<see cref="SkUiPopups.Presenter"/>, <c>UseSkiaUi(options =&gt; options.PopupPresenter = new SkUiSheetPopupPresenter())</c>) or per
/// control (<c>PopupPresenter</c>).
/// </summary>
/// <remarks>
/// The sheet is drawn inside the anchor's surface, so it cannot cover hosted native views or native chrome outside it; popups of a
/// presenter on the app's popup system can. A dismissible request (<see cref="SkUiPopupRequest.IsDismissible"/>) closes on a scrim tap or
/// a drag away; Back and Escape close any. The content is laid out at the sheet's width and keeps the height SkiaUi measured for it.
/// </remarks>
public sealed class SkUiSheetPopupPresenter : ISkUiPopupPresenter
{
    /// <summary>How the sheets show (default <see cref="SkUiSheetPresentation.Auto"/>: a sheet on compact widths, a dialog on wide ones).</summary>
    public SkUiSheetPresentation Presentation { get; init; } = SkUiSheetPresentation.Auto;

    /// <inheritdoc />
    public async Task ShowAsync(SkUiPopupRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var root = request.PlatformAnchor as SkUiView ?? (request.Anchor is { } anchor ? SkUiDiagnostics.GetSurfaceRoot(anchor) : null);
        if (cancellationToken.IsCancellationRequested || request.Content is not SkUiView content || root is null || content.Parent is not null)
            return;
        // Sized for a window popup: the sheet gives it its width, and draws its own corners.
        content.WidthRequest = -1;
        content.Clip = null;
        var sheet = new SkUiSheet
        {
            Content = content,
            Detents = [SkUiSheetDetent.FitContent],
            Presentation = Presentation,
            DismissOnScrimTap = request.IsDismissible,
            IsDragEnabled = request.IsDismissible
        };
        if (request.OverlayColor is { } overlay)
            sheet.ScrimColor = overlay;
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sheet.Closed += (_, _) =>
        {
            root.SurfaceOverlay.Remove(sheet);
            sheet.Content = null; // the request drops its content once it completes
            closed.TrySetResult();
        };
        root.SurfaceOverlay.Add(sheet);
        sheet.IsOpen = true;
        // A token can be cancelled on any thread; the sheet closes on the UI thread. Registered once open: a token cancelled meanwhile
        // closes it right here.
        var dispatcher = Microsoft.Maui.Dispatching.Dispatcher.GetForCurrentThread();
        await using var registration = cancellationToken.Register(() =>
        {
            if (dispatcher?.IsDispatchRequired == true)
                dispatcher.Dispatch(() => _ = sheet.CloseAsync());
            else
                _ = sheet.CloseAsync();
        });
        await closed.Task.ConfigureAwait(true);
    }
}
