namespace MauiSkiaUi;

/// <summary>
/// Shows the popups of drawn controls (a picker's list) through the app's own popup system: MAUI's built-in UI,
/// CommunityToolkit's <c>Popup</c>, Syncfusion's <c>SfPopup</c>, Telerik's <c>RadPopup</c>, a navigation service of the app's,
/// an overlay of its own. SkiaUi ships no popup windows: register a presenter app-wide (<see cref="SkUiPopups.Presenter"/>,
/// also from <c>UseSkiaUi</c>) or per control (<c>PopupPresenter</c>).
/// </summary>
/// <remarks>
/// <para>The contract, in full:</para>
/// <list type="number">
/// <item><b>Show</b> the request's <see cref="SkUiPopupRequest.Content"/>, a ready MAUI view (a drawn surface), placed by
/// <see cref="SkUiPopupRequest.Bounds"/> (window coordinates, computed by SkiaUi for the placement) or by the anchor and
/// placement details, on <see cref="SkUiPopupRequest.Page"/> or <see cref="SkUiPopupRequest.Window"/>. A presenter that shows
/// its own UI instead (a platform list, a vendor picker) reads a typed request it knows (<see cref="SkUiListPopupRequest"/>:
/// item texts, the selected index) and completes it with the choice (<see cref="SkUiPopupRequest{TResult}.Complete"/>).</item>
/// <item><b>Close</b> the popup when the token <see cref="ShowAsync"/> is given is cancelled: the content completed the request (an
/// item was chosen) or the control closed it (<c>IsOpen = false</c>, the control disabled, hidden or removed, an anchored
/// popup's anchor scrolled or its window resized). The token is also <see cref="SkUiPopupRequest.CloseRequested"/>.</item>
/// <item><b>Return</b> (complete the task) once the popup is gone; a dismissal by the user (a tap outside, Back, Escape) just
/// closes and returns, without a result. Returning or throwing <see cref="OperationCanceledException"/> both mean closed.</item>
/// <item><b>Keep</b> nothing afterwards: once the task completed, the request drops its anchor and content, so a presenter
/// that keeps the request (a popup stack) keeps no control alive.</item>
/// </list>
/// <para>One popup per control at a time; <see cref="ShowAsync"/> is called on the UI thread.</para>
/// </remarks>
public interface ISkUiPopupPresenter
{
    /// <summary>Shows the popup <paramref name="request"/> describes; completes when it is gone (see the remarks for the contract).</summary>
    /// <param name="request">What to show, where, and how it completes.</param>
    /// <param name="cancellationToken">Cancelled when the popup must close now.</param>
    Task ShowAsync(SkUiPopupRequest request, CancellationToken cancellationToken);
}
