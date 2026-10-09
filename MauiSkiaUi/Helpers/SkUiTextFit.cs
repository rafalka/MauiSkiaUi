namespace MauiSkiaUi;

/// <summary>A text layout that fits its text to a slot (<see cref="SkUiTextFit"/>): it lays the text out at a trial size.</summary>
internal interface ISkUiTextFitLayout
{
    /// <summary>
    /// Lays <paramref name="text"/> out with <paramref name="style"/> (at its <see cref="SkUiTextStyle.Scale"/> and
    /// <see cref="SkUiTextStyle.Tightening"/>) in a <paramref name="width"/> × <paramref name="height"/> content slot;
    /// <paramref name="estimate"/> is a guess at the largest scale that fits, from these lines (<see cref="double.NaN"/>:
    /// none).
    /// </summary>
    /// <returns>Whether the text fits (<see cref="SkUiTextFit.Check"/>).</returns>
    bool Fits(object text, in SkUiTextStyle style, double width, double height, out double estimate);

    /// <summary>
    /// Lays <paramref name="text"/> out as drawn with the fitted <paramref name="style"/> in a slot <paramref name="width"/>
    /// wide (the layout measure and draw then reuse) and returns the lines' size.
    /// </summary>
    Size Extent(object text, in SkUiTextStyle style, double width);

    /// <summary>Whether <paramref name="text"/> and <paramref name="other"/> lay out alike.</summary>
    bool SameText(object text, object other);
}

/// <summary>
/// Fits text to its slot (labels' <c>ShrinkToFit</c>, <c>GrowToFill</c> and <c>AllowsTightening</c>): the largest text
/// that fits the slot, along one path from the largest to the smallest: grown up to
/// <see cref="SkUiTextStyle.MaximumScale"/>, the text as set, its characters tightened up to
/// <see cref="MaximumTightening"/> (<see cref="SkUiTextStyle.AllowsTightening"/>), then (fully tightened) shrunk down to
/// <see cref="SkUiTextStyle.MinimumScale"/>. A scale applies to every font size and character spacing. The text fits when
/// nothing is truncated or left past <c>MaxLines</c>, no word is broken inside (word wrap), no unwrapped line is wider
/// than the slot and the lines are no taller than it; text that does not fit at the end of the path is drawn there,
/// broken as its <c>LineBreakMode</c> says. Sizes are <see cref="Step"/> DIPs of font size apart and tightenings
/// <see cref="TighteningStep"/> DIPs; the text as set is tried first, then a guess from its overflow or the room left
/// (exact for one line per paragraph), then bisection. The two last slots (the measure's and the arranged one) are
/// remembered.
/// </summary>
internal sealed class SkUiTextFit
{
    /// <summary>Default of labels' <c>MinimumFontScale</c>.</summary>
    internal const double DefaultMinimumScale = 0.5;

    /// <summary>Default of labels' <c>MaximumFontScale</c>.</summary>
    internal const double DefaultMaximumScale = 2;

    /// <summary>The most labels' <c>AllowsTightening</c> takes from the space after each character, in ems (of its font size).</summary>
    internal const float MaximumTightening = 0.05f;

    /// <summary>DIPs of font size between the scales tried: the fitted size is less than a step from the best that fits.</summary>
    internal const double Step = 0.25;

    /// <summary>DIPs per character between the tightenings tried.</summary>
    internal const double TighteningStep = 0.1;

    /// <summary>DIPs the lines may be taller than the slot and still fit (sums of float line heights, a slot rounded by a layout).</summary>
    internal const double HeightTolerance = 0.5;

    /// <summary>
    /// DIPs a trial keeps free at the end of each line: trials lay text out at the base size, and the scaled layout drawn
    /// may measure a rounding error wider.
    /// </summary>
    private const double WidthMargin = 0.01;

    private Entry _recent;
    private Entry _older;

    /// <summary>
    /// A fitted slot: where on the path its text was fitted (<see cref="Path.Last"/>: the end, whether it fits or not) and
    /// the size of the text drawn there.
    /// </summary>
    private readonly record struct Entry(object? Text, SkUiTextStyle Style, double Width, double Height, int Position, Size Extent);

    /// <summary>Forgets the fitted slots (the text or a setting changed).</summary>
    internal void Clear() => _recent = _older = default;

    /// <summary>The content height of a slot <paramref name="height"/> high with <paramref name="padding"/>.</summary>
    internal static double ContentHeight(double height, Thickness padding) =>
        double.IsNaN(height) || double.IsPositiveInfinity(height) ? double.PositiveInfinity : Math.Max(0, height - padding.VerticalThickness);

    /// <summary>
    /// What a trial of <paramref name="style"/> lays out: the text at the base size (scale 1) in a slot
    /// <see cref="TrialWidth"/> wide. Text drawn s times as large breaks into the same lines in a slot s times as wide
    /// (advances, character spacing, tightening and line heights all scale with it), so trials never create fonts at new
    /// sizes; only the scale picked is laid out at its size.
    /// </summary>
    internal static SkUiTextStyle TrialStyle(in SkUiTextStyle style) => style with { Scale = 1 };

    /// <summary>The width a trial of <paramref name="style"/> lays the text out in, for a slot <paramref name="width"/> wide (see <see cref="TrialStyle"/>).</summary>
    internal static double TrialWidth(double width, in SkUiTextStyle style) =>
        double.IsFinite(width) ? Math.Max(0, width - WidthMargin) / style.Scale : width;

    /// <summary>
    /// Whether a trial of <paramref name="style"/> fits a <paramref name="width"/> × <paramref name="height"/> slot: its lines
    /// show all the text (<paramref name="showsAll"/>) and, <paramref name="linesHeight"/> high at the base size, are no
    /// taller than the slot at the style's scale. <paramref name="estimate"/> guesses the largest scale that fits: the
    /// slot's width over the widest paragraph (one line per paragraph), its height over the lines' (wrapped lines get
    /// fewer as well as lower: about the square root).
    /// </summary>
    internal static bool Check(bool showsAll, float linesHeight, float widestParagraph, in SkUiTextStyle style, double width, double height, out double estimate)
    {
        var (scale, tall) = (style.Scale, (double)linesHeight * style.Scale);
        var ratio = double.PositiveInfinity;
        if (!style.Wraps && widestParagraph > 0 && double.IsFinite(width))
            ratio = Math.Max(0, width - WidthMargin) / (widestParagraph * scale);
        if (tall > 0 && double.IsFinite(height))
        {
            var room = (height + HeightTolerance) / tall;
            ratio = Math.Min(ratio, style.Wraps ? Math.Sqrt(room) : room);
        }
        estimate = double.IsFinite(ratio) ? scale * ratio : double.NaN;
        return showsAll && !(tall > height + HeightTolerance);
    }

    /// <summary>
    /// <paramref name="style"/> with the scale and tightening <paramref name="layout"/> draws <paramref name="text"/> at in a
    /// <paramref name="width"/> × <paramref name="height"/> content slot. Leaves the layout's lines at any size tried.
    /// </summary>
    internal SkUiTextStyle Fit(ISkUiTextFitLayout layout, object text, in SkUiTextStyle style, double width, double height)
    {
        // The rendering resolved: a new SkUiTextOptions.DefaultRendering lays text out anew without invalidating labels.
        var key = style with
        {
            Scale = 1,
            Tightening = 0,
            Rendering = style.Rendering == SkUiTextRendering.Default ? SkUiTextOptions.DefaultRendering : style.Rendering
        };
        var path = new Path(key);
        // The slot fitted last (the measure's or the arranged one).
        if (Matches(_recent, layout, text, key) && _recent.Width == width && _recent.Height == height)
            return path.At(_recent.Position);
        if (Matches(_older, layout, text, key) && _older.Width == width && _older.Height == height)
        {
            (_recent, _older) = (_older, _recent);
            return path.At(_recent.Position);
        }
        // A slot no larger than one fitted: the text only fits further along the path, so that position is still the answer
        // when it fits. Arranged at the measured size (or anything at least as large as the text drawn), the lines are the
        // same: greedy breaking gives the same lines at any width from the widest line up, so no trial is needed.
        foreach (var entry in (ReadOnlySpan<Entry>)[_recent, _older])
        {
            if (!Matches(entry, layout, text, key) || width > entry.Width || height > entry.Height)
                continue;
            if (entry.Position == path.Last || (width >= entry.Extent.Width && height + HeightTolerance >= entry.Extent.Height))
                return Remember(entry with { Width = width, Height = height }, path);
            if (layout.Fits(text, path.At(entry.Position), width, height, out _))
                return Remember(Measured(layout, text, key, width, height, entry.Position, path), path);
        }
        return Remember(Measured(layout, text, key, width, height, Search(layout, text, path, width, height), path), path);
    }

    private static Entry Measured(ISkUiTextFitLayout layout, object text, in SkUiTextStyle key, double width, double height, int position, in Path path) =>
        new(text, key, width, height, position, layout.Extent(text, path.At(position), width));

    private static bool Matches(in Entry entry, ISkUiTextFitLayout layout, object text, in SkUiTextStyle style) =>
        entry.Text is { } fitted && entry.Style == style && (ReferenceEquals(fitted, text) || layout.SameText(fitted, text));

    private SkUiTextStyle Remember(in Entry entry, in Path path)
    {
        _older = _recent;
        _recent = entry;
        return path.At(entry.Position);
    }

    /// <summary>
    /// The first position on <paramref name="path"/> at which the text fits (the largest text), or its end. The text as set
    /// is tried first; then all the tightening (when it did not fit); then a guess from the overflow or the room left,
    /// from which steps double away until the answer is bracketed (a guess is usually right or a step off); then bisection.
    /// </summary>
    private static int Search(ISkUiTextFitLayout layout, object text, in Path path, double width, double height)
    {
        var (low, high) = (-1, path.Last + 1); // low does not fit (-1: none known), high fits (Last + 1: none known)
        var guess = path.Natural;
        var fromEstimate = false;
        var (direction, stride) = (0, 1); // stepping from a guess: -1 towards larger text (it fit), +1 towards smaller
        while (high - low > 1)
        {
            if (!(guess > low && guess < high))
                (guess, fromEstimate, direction) = (low + (high - low) / 2, false, 0);
            var position = guess;
            var fits = layout.Fits(text, path.At(position), width, height, out var estimate);
            if (fits) high = position; else low = position;
            // Without wrapping the estimate is exact (widths and heights scale with the text): a larger text does not fit.
            if (fits && !path.Wraps && position > 0 && path.At(position - 1).Scale > estimate)
                low = position - 1;
            var away = fits ? -1 : 1; // where the answer is from here
            if (fromEstimate || direction != 0)
            {
                if (fromEstimate)
                    (direction, stride, fromEstimate) = (away, 1, false);
                else if (away == direction)
                    stride *= 2;
                else
                    direction = 0; // bracketed
                guess = direction == 0 ? -1 : position + direction * stride;
            }
            else if (position == path.Natural && !fits && path.Tightenings > 0)
                guess = path.Natural + path.Tightenings; // tightening alone, as far as it goes
            else if (!path.IsPartlyTightened(position) && double.IsFinite(estimate))
            {
                guess = path.PositionOf(estimate);
                // The guess is where it was tried (the text as set already fills the slot): step from here.
                if (guess == position)
                    (direction, stride, guess) = (away, 1, position + away);
                else
                    fromEstimate = true;
            }
            else
                guess = -1;
        }
        return Math.Min(high, path.Last);
    }

    /// <summary>
    /// The sizes a style fits through, from the largest to the smallest: <see cref="Grows"/> scales from
    /// <see cref="SkUiTextStyle.MaximumScale"/> down to above 1, the text as set (<see cref="Natural"/>),
    /// <see cref="Tightenings"/> tightenings at scale 1 up to <see cref="MaximumTightening"/>, then
    /// <see cref="Shrinks"/> scales below 1 down to <see cref="SkUiTextStyle.MinimumScale"/>, fully tightened.
    /// </summary>
    private readonly struct Path
    {
        private readonly SkUiTextStyle _style;

        internal Path(in SkUiTextStyle style)
        {
            _style = style;
            Grows = style.MaximumScale > 1 ? Steps((style.MaximumScale - 1) * style.FontSize / Step) : 0;
            Tightenings = style.AllowsTightening ? Steps(MaximumTightening * style.FontSize / TighteningStep) : 0;
            Shrinks = style.MinimumScale < 1 ? Steps((1 - style.MinimumScale) * style.FontSize / Step) : 0;
        }

        /// <summary>Whether the text wraps (<see cref="SkUiTextStyle.Wraps"/>): estimates are guesses, not exact.</summary>
        internal bool Wraps => _style.Wraps;

        internal int Grows { get; }
        internal int Tightenings { get; }
        internal int Shrinks { get; }
        internal int Natural => Grows;
        internal int Last => Grows + Tightenings + Shrinks;

        /// <summary>Whether <paramref name="position"/> tightens less than fully (an estimate's scale does not apply to it).</summary>
        internal bool IsPartlyTightened(int position) => position > Natural && position < Natural + Tightenings;

        /// <summary>The style at <paramref name="position"/>.</summary>
        internal SkUiTextStyle At(int position)
        {
            if (position < Natural)
                return _style with { Scale = (float)(_style.MaximumScale - (_style.MaximumScale - 1.0) * position / Grows) };
            if (position == Natural)
                return _style;
            var tightened = position - Natural;
            if (tightened <= Tightenings)
                return _style with { Tightening = (float)((double)MaximumTightening * tightened / Tightenings) };
            var shrunk = tightened - Tightenings;
            return _style with
            {
                Scale = (float)(1 - (1.0 - _style.MinimumScale) * shrunk / Shrinks),
                Tightening = _style.AllowsTightening ? MaximumTightening : 0
            };
        }

        /// <summary>The first position whose scale is at most <paramref name="scale"/> (growing, as set or shrinking).</summary>
        internal int PositionOf(double scale)
        {
            if (scale >= 1)
                return Grows == 0 ? Natural : Math.Clamp((int)Math.Ceiling((_style.MaximumScale - scale) / (_style.MaximumScale - 1) * Grows), 0, Natural);
            return Shrinks == 0 ? Last : Natural + Tightenings + Math.Clamp((int)Math.Ceiling((1 - scale) / (1 - _style.MinimumScale) * Shrinks), 1, Shrinks);
        }

        private static int Steps(double steps) => Math.Max(1, (int)Math.Ceiling(steps));
    }
}
