namespace MauiSkiaUi;

/// <summary>How a segmented control's segments (or a tab view's headers) share the row's width.</summary>
public enum SkUiSegmentWidthMode
{
    /// <summary>Every segment as wide as the widest one's content; arranged wider, the width is split equally.</summary>
    Equal,

    /// <summary>Each segment as wide as its content; arranged wider, the extra room is shared out in proportion.</summary>
    Content
}
