namespace MauiSkiaUi;

/// <summary>Where a numeric entry's − and + buttons sit in the field (its <c>ButtonPlacement</c>).</summary>
public enum SkUiStepButtonPlacement
{
    /// <summary>No buttons (default): keys, the wheel and screen readers still step.</summary>
    None,

    /// <summary>Both at the end side (the right, the left in right-to-left layouts): − then + side by side, or + above − stacked.</summary>
    End,

    /// <summary>Both at the start side.</summary>
    Start,

    /// <summary>− at the start side and + at the end side; stacked: + in a bar above the text and − in a bar below it.</summary>
    Split
}

/// <summary>When a numeric entry's typed text becomes its <c>Value</c> (its <c>ValueChangeMode</c>).</summary>
public enum SkUiValueChangeMode
{
    /// <summary>When the user is done (default): Return, focus leaving the field, a button or a key step.</summary>
    OnCompleted,

    /// <summary>On every edit that makes a number within the range (and on completion, as <see cref="OnCompleted"/>).</summary>
    OnKeystroke
}
