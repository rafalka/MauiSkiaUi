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

    /// <summary>The text of the sheet's cancel button (default "Cancel"); set it to localize.</summary>
    public string CancelText { get; set; } = "Cancel";

    /// <inheritdoc />
    public async Task ShowAsync(SkUiPopupRequest request, CancellationToken cancellationToken)
    {
        if (request is not SkUiListPopupRequest list || list.Page is not { } page || list.Items.Count == 0)
        {
            Trace.WriteLine($"SkiaUi: {nameof(SkUiActionSheetPopupPresenter)} shows list requests on a page only: the popup was not shown.");
            return;
        }
        var items = list.Items;
        var buttons = new string[items.Count];
        for (var index = 0; index < buttons.Length; index++)
            buttons[index] = items[index];
        var chosen = await page.DisplayActionSheetAsync(list.Title, CancelText, null, list.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight, buttons);
        // MAUI answers the cancel button's text for a cancel (or a tap outside): an item with that text is not chosen.
        if (chosen is null || chosen == CancelText || cancellationToken.IsCancellationRequested)
            return;
        var chosenIndex = Array.IndexOf(buttons, chosen);
        if (chosenIndex >= 0)
            list.Complete(chosenIndex);
    }
}
