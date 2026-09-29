namespace MauiSkiaUi;

/// <summary>State of a toggle control (check box, switch, radio button).</summary>
public enum SkUiCheckState
{
    /// <summary>Off.</summary>
    Unchecked,

    /// <summary>On.</summary>
    Checked,

    /// <summary>
    /// Neither on nor off, e.g. a "select all" check box whose group is partly checked. Usually set by the app;
    /// users reach it by tapping only when the control's <c>IsThreeState</c> is set.
    /// </summary>
    Indeterminate
}

/// <summary>State transitions shared by the SkUi* and Core toggle controls.</summary>
internal static class SkUiCheckStates
{
    /// <summary>
    /// The state a tap moves to. Three-state controls cycle Unchecked → Checked → Indeterminate → Unchecked (like
    /// WinUI); two-state ones go Checked → Unchecked and otherwise to Checked (an app-set Indeterminate included).
    /// </summary>
    public static SkUiCheckState Next(SkUiCheckState current, bool isThreeState) => current switch
    {
        SkUiCheckState.Unchecked => SkUiCheckState.Checked,
        SkUiCheckState.Checked => isThreeState ? SkUiCheckState.Indeterminate : SkUiCheckState.Unchecked,
        _ => isThreeState ? SkUiCheckState.Unchecked : SkUiCheckState.Checked
    };

    /// <summary>The <c>IsChecked</c> view of a state: only <see cref="SkUiCheckState.Checked"/> is checked.</summary>
    public static bool IsChecked(SkUiCheckState state) => state == SkUiCheckState.Checked;

    /// <summary>The state an <c>IsChecked</c> assignment means.</summary>
    public static SkUiCheckState FromIsChecked(bool value) => value ? SkUiCheckState.Checked : SkUiCheckState.Unchecked;
}
