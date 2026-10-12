namespace MauiSkiaUi.CommunityToolkit;

/// <summary>Registers SkiaUi's CommunityToolkit.Maui integration.</summary>
public static class SkUiCommunityToolkitAppBuilderExtensions
{
    /// <summary>
    /// Shows the popups of drawn controls (pickers' lists) in CommunityToolkit.Maui's <c>Popup</c>: makes
    /// <see cref="SkUiToolkitPopupPresenter.Instance"/> the app's <see cref="SkUiPopups.Presenter"/>. Call it with the toolkit's
    /// <c>UseMauiCommunityToolkit()</c> and SkiaUi's <c>UseSkiaUi()</c>; it replaces a presenter set earlier.
    /// </summary>
    public static MauiAppBuilder UseSkiaUiCommunityToolkit(this MauiAppBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        SkUiPopups.Presenter = SkUiToolkitPopupPresenter.Instance;
        return builder;
    }
}
