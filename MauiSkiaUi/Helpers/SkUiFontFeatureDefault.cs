namespace MauiSkiaUi;

/// <summary>Whether text is shaped with an OpenType feature when <c>FontFeatures</c> does not list it.</summary>
public enum SkUiFontFeatureDefault
{
    /// <summary>Off: applied only when listed (<c>tnum</c>, <c>smcp</c>, <c>ss01</c>).</summary>
    Off,

    /// <summary>On: applied unless listed off (<c>kern</c>, <c>liga</c>, <c>calt</c>, <c>clig</c>, <c>rand</c>).</summary>
    On,

    /// <summary>
    /// Not a reader's choice: applied by the shaper where the script or the layout needs it (Arabic joining forms, Indic
    /// conjuncts, mark placement, localized forms; turning it off can break the text), or of no use to SkiaUi's horizontal
    /// text (vertical forms, the font's size information).
    /// </summary>
    Required
}
