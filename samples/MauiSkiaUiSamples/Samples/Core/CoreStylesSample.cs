using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUiSamples.Samples.Core;

/// <summary>
/// HOWTO: styles for Core nodes. Core nodes have no MAUI styles; the code-first equivalents are style methods (extension methods
/// that call <c>Set*</c>, <see cref="CoreStyles"/>) and factory methods the app creates its nodes through (<see cref="CoreUi"/>), the
/// counterpart of an implicit style. App-wide defaults stay with the look and the color scheme (docs/controls/SkUiCore.md,
/// "Styling Core nodes").
/// <para>
/// The page builds two order cards from the factory, overrides one value locally on one button, and restyles the live buttons when
/// the "Compact buttons" switch changes, by calling a style method with a parameter again.
/// </para>
/// </summary>
public sealed class CoreStylesSample : SamplePage, ISample
{
    public static SampleInfo Info { get; } = new(
        SampleSection.Core,
        Title: "Styles for Core nodes",
        Summary: "Style methods and factory methods in place of MAUI styles: order cards whose buttons and labels share named styles, " +
                 "one local override, and a density switch that restyles the live buttons.",
        HowTo:
        [
            "A style is an extension method, generic in the node's type so the chain keeps it: " +
                "`public static T Primary<T>(this T button) where T : SkUiCoreButton { button.ButtonBase().SetFillColor(accent); return button; }`.",
            "`BasedOn`: call another style method first (`Primary` and `Secondary` both start with `ButtonBase`).",
            "Implicit styles: create the app's nodes through factory methods (`CoreUi.PrimaryButton(\"Pay\", Pay)`) that apply the style.",
            "A value set after the style wins: `CoreUi.SecondaryButton(\"Cancel order\", …).SetTextColor(danger)`.",
            "Styles can take parameters: `button.Density(compact)`; call it again to restyle a live node."
        ],
        ThingsToKnow:
        [
            "A style method sets values once and does not track them: restyling means calling it again on the nodes you keep.",
            "App-wide defaults (the palette, corner radii, minimum heights, painters) belong to `SkUiColorScheme` and `SkUiLook`: they reach " +
                "both layers and SkiaUi's own nodes, which an app's factory never creates.",
            "Leave colors that should follow light and dark to the color scheme instead of setting them in a style."
        ]);

    private static readonly Color Danger = Color.FromArgb("#B3261E");

    // The buttons the density switch restyles.
    private readonly List<SkUiCoreButton> _buttons = [];

    public CoreStylesSample() : base(Info)
    {
        var compact = new SkUiCoreSwitch();
        compact.Toggled += (_, args) =>
        {
            foreach (var button in _buttons)
                button.Density(args.Value);
        };
        var settings = new SkUiCoreHorizontalStackLayout()
            .SetSpacing(12)
            .Add(CoreUi.Body("Compact buttons"))
            .Add(compact);

        var page = new SkUiCoreVerticalStackLayout()
            .SetSpacing(16)
            .Add(settings)
            .Add(Card("Order #1042", "3 items · ready for pickup", "Pick up"))
            .Add(Card("Order #1043", "1 item · on its way", "Track"));
        page.SetPadding(new Thickness(16));
        page.SetBackground(SampleColors.Page);
        SampleContent = new SkUiCoreHost().SetContent(page);
    }

    private SkUiCoreNode Card(string title, string status, string action)
    {
        var primary = CoreUi.PrimaryButton(action, () => { });
        var cancel = CoreUi.SecondaryButton("Cancel order", () => { });
        // A local value over the factory's style, as a local value wins over a MAUI style.
        cancel.SetTextColor(Danger).SetBorderColor(Danger);
        _buttons.Add(primary);
        _buttons.Add(cancel);
        var card = new SkUiCoreVerticalStackLayout()
            .SetSpacing(8)
            .Add(CoreUi.Title(title))
            .Add(CoreUi.Caption(status))
            .Add(new SkUiCoreHorizontalStackLayout().SetSpacing(8).Add(primary).Add(cancel));
        card.SetPadding(new Thickness(16));
        card.SetBackground(SampleColors.Surface);
        return card;
    }
}

/// <summary>
/// The sample's style methods: each sets a group of properties and returns the node, typed as it came in. <see cref="ButtonBase"/>
/// is the base the button styles call first (MAUI's <c>BasedOn</c>).
/// </summary>
public static class CoreStyles
{
    public static T ButtonBase<T>(this T button) where T : SkUiCoreButton
    {
        button.SetCornerRadius(10).SetFontAttributes(FontAttributes.Bold).SetBorderWidth(1);
        return button.Density(compact: false);
    }

    public static T Primary<T>(this T button) where T : SkUiCoreButton
    {
        button.ButtonBase().SetFillColor(SampleColors.Accent).SetBorderColor(SampleColors.Accent).SetTextColor(Colors.White);
        return button;
    }

    public static T Secondary<T>(this T button) where T : SkUiCoreButton
    {
        button.ButtonBase().SetFillColor(SampleColors.Surface).SetBorderColor(SampleColors.Accent).SetTextColor(SampleColors.Accent);
        return button;
    }

    /// <summary>A style with a parameter (MAUI styles have none): the buttons' size, set again when the setting changes.</summary>
    public static T Density<T>(this T button, bool compact) where T : SkUiCoreButton
    {
        button.SetPadding(compact ? new Thickness(10, 4) : new Thickness(16, 10)).SetFontSize(compact ? 13 : 15);
        return button;
    }

    public static T Title<T>(this T label) where T : SkUiCoreLabel
    {
        label.SetFontSize(18).SetFontAttributes(FontAttributes.Bold).SetTextColor(SampleColors.Ink);
        return label;
    }

    public static T Caption<T>(this T label) where T : SkUiCoreLabel
    {
        label.SetFontSize(14).SetTextColor(SampleColors.Caption);
        return label;
    }
}

/// <summary>
/// The sample's factory methods, the Core counterpart of implicit styles: every node made here gets its style, and what the caller
/// sets afterwards still wins.
/// </summary>
public static class CoreUi
{
    public static SkUiCoreButton PrimaryButton(string text, Action clicked) => Button(new SkUiCoreButton().Primary(), text, clicked);

    public static SkUiCoreButton SecondaryButton(string text, Action clicked) => Button(new SkUiCoreButton().Secondary(), text, clicked);

    public static SkUiCoreLabel Title(string text) => Label(new SkUiCoreLabel().Title(), text);

    public static SkUiCoreLabel Caption(string text) => Label(new SkUiCoreLabel().Caption(), text);

    public static SkUiCoreLabel Body(string text) => Label(new SkUiCoreLabel().SetFontSize(15).SetTextColor(SampleColors.Ink), text);

    private static SkUiCoreButton Button(SkUiCoreButton button, string text, Action clicked)
    {
        button.SetText(text);
        return button.SetClicked(clicked);
    }

    private static SkUiCoreLabel Label(SkUiCoreLabel label, string text)
    {
        label.SetText(text);
        label.SetVerticalAlignment(LayoutAlignment.Center);
        return label;
    }
}
