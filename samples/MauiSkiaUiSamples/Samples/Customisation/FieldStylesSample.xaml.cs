namespace MauiSkiaUiSamples.Samples.Customisation;

/// <summary>
/// HOWTO: mix field styles on one form. The page's XAML (FieldStylesSample.xaml) shows:
/// <list type="number">
/// <item>A pill search bar: the look's search field with the app's <c>FillColor</c> and <c>CornerRadius</c>.</item>
/// <item>A filled pill entry from a style: <c>FieldStyle="Outline"</c> with <c>BorderWidth="0"</c>, a <c>FillColor</c>,
/// <c>CornerRadii</c> and a <c>TextPadding</c> of its own.</item>
/// <item>An underlined entry on any look (<c>FieldStyle="Underline"</c>), its text starting at the line's start.</item>
/// <item>An outlined picker in the accent color (<c>BorderColor</c>, <c>CornerRadius</c>).</item>
/// <item>An editor without chrome (<c>FieldStyle="None"</c>) inside the app's own border.</item>
/// </list>
/// </summary>
public sealed partial class FieldStylesSample : SamplePage, ISample
{
    public static SampleInfo Info { get; } = new(
        SampleSection.Customisation,
        Title: "Field styles in one form",
        Summary: "Bordered, borderless, underlined and pill fields on one form: each text field and picker sets its own style, " +
                 "fill, border, corner radii and text padding over the look's.",
        HowTo:
        [
            "Pick a field's chrome: `FieldStyle=\"Underline\"` (also `Outline`, `Filled`, `TextBox`, `None`; pickers add " +
                "`PopUpButton` and `ComboBox`). `Default` is the look's.",
            "Change the idle chrome: `FillColor`, `BorderColor`, `BorderWidth` (0: no idle outline), `CornerRadius` or " +
                "`CornerRadii` (a large radius gives a pill).",
            "Move the text: `TextPadding=\"18,10\"` replaces the look's padding on entries, editors, search bars and numeric entries.",
            "Share it: put the setters in a `Style`, as the name field does."
        ],
        ThingsToKnow:
        [
            "Focus, errors and the disabled state still look as the look draws them: a focused outline turns to the accent color " +
                "and thickens, an invalid field shows its error color even with `BorderWidth=\"0\"`.",
            "`FillColor` is drawn as set while disabled too; use a `Disabled` visual state for another fill.",
            "A search bar's `Default` style is the look's search field; any other style draws that text field with the icon in it.",
            "For one style on every field of a screen, a subtree look (`SkUiLookScope`) is less to write."
        ]);

    public FieldStylesSample() : base(Info) => InitializeComponent();
}
