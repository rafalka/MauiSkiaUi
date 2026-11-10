namespace MauiSkiaUi;

/// <summary>How a password field's reveal button (<c>ShowsPasswordRevealButton</c>) shows the text.</summary>
public enum SkUiPasswordRevealMode
{
    /// <summary>A tap or click shows the text; the next one hides it (the default).</summary>
    Toggle,

    /// <summary>
    /// The text shows only while the button is held down: releasing it, or moving off it, masks the text again. Screen readers,
    /// which cannot hold a press, still toggle it (masked again when the field loses focus, with <c>HidesPasswordOnUnfocus</c>).
    /// </summary>
    WhilePressed
}
