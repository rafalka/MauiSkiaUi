namespace MauiSkiaUi;

/// <summary>
/// A look that mimics Windows (Fluent): <see cref="DefaultSkUiLook"/> with Windows' choices where it has them. It draws the same
/// on every platform; <see cref="DefaultSkUiLook.ForCurrentPlatform"/> picks it on Windows.
/// </summary>
/// <remarks>
/// So far the default look's choices match Windows': outlined picker fields whose lists open anchored to the field, as a
/// combo box. Fluent's own control shapes and sizes come with the platform looks phase (docs/design/ImplementationPlan.md).
/// </remarks>
public class WindowsSkUiLook : DefaultSkUiLook
{
    /// <summary>A shared instance (set it as <see cref="SkUiLook.Current"/>).</summary>
    public new static WindowsSkUiLook Instance { get; } = new();
}
