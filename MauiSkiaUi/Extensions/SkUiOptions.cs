namespace MauiSkiaUi;

/// <summary>
/// Options of <c>UseSkiaUi</c>. Each option set replaces the app-wide setting; one left <c>null</c> leaves it as it is, so
/// integrations registered before or after (<c>UseSkiaUiCommunityToolkit()</c> sets the presenter) are kept.
/// </summary>
public sealed class SkUiOptions
{
    /// <summary>
    /// The app-wide popup presenter (<see cref="SkUiPopups.Presenter"/>); <c>null</c>: unchanged (by default none: MAUI's action
    /// sheet).
    /// </summary>
    public ISkUiPopupPresenter? PopupPresenter { get; set; }

    /// <summary>
    /// The app-wide look (<see cref="SkUiLook.Current"/>); <c>null</c>: unchanged (by default <see cref="DefaultSkUiLook"/>, the
    /// same on every platform). <see cref="DefaultSkUiLook.ForCurrentPlatform"/> mimics the platform the app runs on.
    /// </summary>
    public SkUiLook? Look { get; set; }

    /// <summary>The app-wide texts, to translate SkiaUi's own (<see cref="SkUiStrings.Current"/>); <c>null</c>: unchanged (English).</summary>
    public SkUiStrings? Strings { get; set; }
}
