namespace MauiSkiaUi;

/// <summary>Options of <c>UseSkiaUi</c>.</summary>
public sealed class SkUiOptions
{
    /// <summary>The app-wide popup presenter (<see cref="SkUiPopups.Presenter"/>); <c>null</c>: MAUI's action sheet.</summary>
    public ISkUiPopupPresenter? PopupPresenter { get; set; }

    /// <summary>
    /// The app-wide look (<see cref="SkUiLook.Current"/>); <c>null</c>: <see cref="DefaultSkUiLook"/>, the same on every platform.
    /// <see cref="DefaultSkUiLook.ForCurrentPlatform"/> mimics the platform the app runs on.
    /// </summary>
    public SkUiLook? Look { get; set; }
}
