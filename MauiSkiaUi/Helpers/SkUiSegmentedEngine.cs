namespace MauiSkiaUi;

/// <summary>
/// The row geometry of a segmented control and of a tab view's strip, with no view types: each segment's rectangle from the
/// segments' content widths, the width mode, the arranged width and the direction, and the selection marker's rectangle
/// between two segments (an animated change) or at a fractional position (a strip following its panes). Shared by
/// <c>SkUiSegmentedControl</c>, <c>SkUiCoreSegmentedControl</c> and <c>SkUiTabView</c>; the look measures the contents and
/// draws from the rectangles.
/// </summary>
/// <remarks>
/// The row is laid out inside <see cref="Inset"/> on every side (a track's padding), with <see cref="Spacing"/> between
/// segments. Segments are narrower than their content when the row is (in proportion to their content in
/// <see cref="SkUiSegmentWidthMode.Content"/> mode), unless <see cref="Overflows"/>: then they keep their content widths and
/// the row is longer than the arranged width (a strip that scrolls). Arrays are reused: laying out again allocates nothing
/// while the count stays the same.
/// </remarks>
internal sealed class SkUiSegmentedEngine
{
    private double[] _content = [];
    private double[] _x = [];
    private double[] _width = [];
    private int _count;
    private double _height;

    /// <summary>The number of segments.</summary>
    public int Count => _count;

    /// <summary>How the segments share the width.</summary>
    public SkUiSegmentWidthMode Mode { get; set; }

    /// <summary>The gap between segments in DIPs.</summary>
    public double Spacing { get; set; }

    /// <summary>The room between the row's edges and its segments, on every side, in DIPs.</summary>
    public double Inset { get; set; }

    /// <summary>Whether segments keep their content widths when the row is narrower, the row then being longer than its arranged width.</summary>
    public bool Overflows { get; set; }

    /// <summary>Whether the row runs from the right (the first segment at the right end).</summary>
    public bool Mirrored { get; set; }

    /// <summary>The row's length after <see cref="Arrange"/>: the arranged width, or more when the segments overflow.</summary>
    public double Length { get; private set; }

    /// <summary>The height the segments were last arranged at.</summary>
    public double Height => _height;

    /// <summary>The segments' content widths (the look's measure of each segment's text and icon, with its padding).</summary>
    public void SetContentWidths(ReadOnlySpan<double> widths)
    {
        if (_content.Length < widths.Length)
        {
            _content = new double[widths.Length];
            _x = new double[widths.Length];
            _width = new double[widths.Length];
        }
        _count = widths.Length;
        for (var index = 0; index < widths.Length; index++)
            _content[index] = double.IsFinite(widths[index]) ? Math.Max(0, widths[index]) : 0;
    }

    /// <summary>The content width of segment <paramref name="index"/>.</summary>
    public double ContentWidth(int index) => _content[index];

    /// <summary>The row's width for its content: every segment as wide as the widest in <see cref="SkUiSegmentWidthMode.Equal"/> mode, else each its own.</summary>
    public double DesiredWidth()
    {
        if (_count == 0)
            return 2 * Inset;
        var segments = 0d;
        if (Mode == SkUiSegmentWidthMode.Equal)
        {
            var widest = 0d;
            for (var index = 0; index < _count; index++)
                widest = Math.Max(widest, _content[index]);
            segments = widest * _count;
        }
        else
        {
            for (var index = 0; index < _count; index++)
                segments += _content[index];
        }
        return segments + (_count - 1) * Spacing + 2 * Inset;
    }

    /// <summary>Lays the segments out in a row <paramref name="width"/> wide and <paramref name="height"/> high.</summary>
    public void Arrange(double width, double height)
    {
        _height = Math.Max(0, height);
        width = double.IsFinite(width) ? Math.Max(0, width) : DesiredWidth();
        Length = width;
        if (_count == 0)
            return;
        var inner = Math.Max(0, width - 2 * Inset - (_count - 1) * Spacing);
        var total = 0d;
        for (var index = 0; index < _count; index++)
            total += _content[index];
        if (Overflows && total > inner)
        {
            // Too long for the row: each keeps its content width, and the row grows (it scrolls).
            for (var index = 0; index < _count; index++)
                _width[index] = _content[index];
            Length = total + 2 * Inset + (_count - 1) * Spacing;
        }
        else if (Mode == SkUiSegmentWidthMode.Equal || total <= 0)
        {
            for (var index = 0; index < _count; index++)
                _width[index] = inner / _count;
        }
        else
        {
            // Wider or narrower than the content: each segment's share of the row is its share of the content.
            var scale = inner / total;
            for (var index = 0; index < _count; index++)
                _width[index] = _content[index] * scale;
        }
        var x = Inset;
        for (var index = 0; index < _count; index++)
        {
            _x[index] = Mirrored ? Length - x - _width[index] : x;
            x += _width[index] + Spacing;
        }
    }

    /// <summary>Segment <paramref name="index"/>'s rectangle (after <see cref="Arrange"/>).</summary>
    public Rect Segment(int index) => new(_x[index], Inset, _width[index], Math.Max(0, _height - 2 * Inset));

    /// <summary>
    /// The selection marker's rectangle on its way from segment <paramref name="from"/> to segment <paramref name="to"/>,
    /// <paramref name="progress"/> of the way (0–1): position and width blend, so it also changes width between segments
    /// of different widths. No previous segment (<paramref name="from"/> -1) puts it on <paramref name="to"/>; no selection
    /// (<paramref name="to"/> -1) is an empty rectangle.
    /// </summary>
    public Rect Selection(int from, int to, double progress)
    {
        if (to < 0 || to >= _count)
            return Rect.Zero;
        var target = Segment(to);
        if (from < 0 || from >= _count || from == to)
            return target;
        return Blend(Segment(from), target, Math.Clamp(progress, 0, 1));
    }

    /// <summary>The selection marker's rectangle at a fractional <paramref name="position"/> (1.5: halfway from the second segment to the third), within the segments.</summary>
    public Rect SelectionAt(double position)
    {
        if (_count == 0 || !double.IsFinite(position))
            return Rect.Zero;
        position = Math.Clamp(position, 0, _count - 1);
        var index = (int)Math.Floor(position);
        var fraction = position - index;
        return index >= _count - 1 || fraction <= 0 ? Segment(index) : Blend(Segment(index), Segment(index + 1), fraction);
    }

    /// <summary>
    /// The segment at <paramref name="x"/> along the row: the gaps and insets belong to the nearest segment, so every point
    /// of the row hits one; -1 outside the row or without segments.
    /// </summary>
    public int HitTest(double x)
    {
        if (_count == 0 || x < 0 || x > Length)
            return -1;
        var best = 0;
        var nearest = double.PositiveInfinity;
        for (var index = 0; index < _count; index++)
        {
            var start = _x[index];
            var end = start + _width[index];
            if (x >= start && x <= end)
                return index;
            var distance = x < start ? start - x : x - end;
            if (distance < nearest)
            {
                nearest = distance;
                best = index;
            }
        }
        return best;
    }

    private static Rect Blend(Rect from, Rect to, double amount) =>
        new(from.X + (to.X - from.X) * amount, from.Y + (to.Y - from.Y) * amount,
            from.Width + (to.Width - from.Width) * amount, from.Height + (to.Height - from.Height) * amount);
}
