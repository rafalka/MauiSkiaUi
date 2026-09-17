using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Lightweight SkiaUi node with no MAUI <c>View</c> / <c>BindableObject</c> identity.
/// Intended for composing complex controls and dense trees; host via <see cref="SkUiCoreHost"/>.
/// </summary>
public interface ISkUiCoreNode
{
    /// <summary>Parent in the Core tree, or <c>null</c> when unparented / hosted by <see cref="SkUiCoreHost"/>.</summary>
    ISkUiCoreNode? Parent { get; }

    /// <summary>Arranged bounds in the parent's local DIP coordinates.</summary>
    Rect Frame { get; }

    /// <summary>Last desired size from <see cref="Measure"/>, including any margin reserved by the node.</summary>
    Size DesiredSize { get; }

    /// <summary>When <c>false</c>, the node is skipped for measure, arrange, paint, and hit-testing.</summary>
    bool IsVisible { get; }

    /// <summary>Measures this node under the given DIP constraints and stores <see cref="DesiredSize"/>.</summary>
    Size Measure(double widthConstraint, double heightConstraint);

    /// <summary>Assigns <see cref="Frame"/> and lays out descendants in local DIP coordinates.</summary>
    void Arrange(Rect bounds);

    /// <summary>Paints this node at the current canvas origin (caller translates to <see cref="Frame"/>).</summary>
    void Paint(SKCanvas canvas);

    /// <summary>Delivers a pointer sample in this node's local DIP coordinates.</summary>
    bool Touch(SkUiTouchEvent touch);

    /// <summary>Requests that ancestors remeasure / rearrange this subtree.</summary>
    void InvalidateMeasure();

    /// <summary>Requests a redraw of this subtree without necessarily remeasuring.</summary>
    void InvalidatePaint();
}
