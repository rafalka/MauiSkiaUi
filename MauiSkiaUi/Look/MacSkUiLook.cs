namespace MauiSkiaUi;

/// <summary>
/// A look that mimics macOS: <see cref="DefaultSkUiLook"/> with the Mac's choices where it has them. It draws the same on every
/// platform; <see cref="DefaultSkUiLook.ForCurrentPlatform"/> picks it on Mac Catalyst.
/// </summary>
/// <remarks>
/// So far the default look's choices match the Mac's: outlined picker fields whose lists open anchored to the field, as a
/// pop-up button's menu. The Mac's own control shapes and sizes come with the platform looks phase
/// (docs/design/ImplementationPlan.md).
/// </remarks>
public class MacSkUiLook : DefaultSkUiLook
{
    /// <summary>A shared instance (set it as <see cref="SkUiLook.Current"/>).</summary>
    public new static MacSkUiLook Instance { get; } = new();
}
