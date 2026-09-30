namespace MauiSkiaUiDemo;

/// <summary>
/// Read-only editor row for the shrink layout demos: how much of the available space (the gray preview area) the
/// layout (white) takes, and whether it is behaving as a plain stack or shrinking its shrinkable children.
/// </summary>
internal sealed class ShrinkLayoutDemoState
{
    private readonly bool _vertical;

    public ShrinkLayoutDemoState(bool vertical)
    {
        _vertical = vertical;
        View = new Label { TextColor = DemoColors.Ink, FontSize = 13, LineBreakMode = LineBreakMode.WordWrap, AutomationId = "ShrinkState" };
    }

    public Label View { get; }

    public void Update(Size layout, Size available)
    {
        var (used, space, axis) = _vertical ? (layout.Height, available.Height, "height") : (layout.Width, available.Width, "width");
        if (used <= 0 || space <= 0)
            return;
        View.Text = used < space - 0.5
            ? $"Enough space: the layout takes only the {used:F0} of {space:F0} DIPs of {axis} it needs, like a stack layout."
            : $"Not enough space: the layout fills all {space:F0} DIPs of {axis} and shrinks the children with a Shrink factor (Auto: only those larger than the average).";
    }
}
