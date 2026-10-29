using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>The row geometry of an indicator view, shared by the paint struct (<see cref="SkUiIndicatorPaint"/>), measuring and hit-testing.</summary>
internal static class SkUiIndicatorLayout
{
    /// <summary>How selected the indicator of item <paramref name="index"/> is for a (fractional) <paramref name="position"/>, 0–1.</summary>
    public static float Selection(int index, float position, int itemCount, bool wraps)
    {
        var distance = Math.Abs(index - position);
        if (wraps && itemCount > 0)
            distance = Math.Min(distance, Math.Abs(itemCount - distance));
        return Math.Max(0, 1 - distance);
    }

    /// <summary>The item of the indicator in <paramref name="slot"/>: counted on from <paramref name="first"/>, past the last item to the first when the selection wraps.</summary>
    public static int Item(int slot, int first, int itemCount, bool wraps) =>
        wraps && itemCount > 0 ? ((first + slot) % itemCount + itemCount) % itemCount : first + slot;

    /// <summary>Where the indicator in <paramref name="slot"/> starts along the row: the ones before it, each longer by its share of the selection.</summary>
    public static float Start(int slot, int first, float position, int itemCount, bool wraps, float size, float spacing, float extra)
    {
        var start = slot * (size + spacing);
        if (extra > 0)
            for (var before = 0; before < slot; before++)
                start += extra * Selection(Item(before, first, itemCount, wraps), position, itemCount, wraps);
        return start;
    }

    /// <summary><paramref name="from"/> blended towards <paramref name="to"/> by <paramref name="amount"/> (0–1).</summary>
    public static SKColor Blend(SKColor from, SKColor to, float amount)
    {
        if (amount <= 0)
            return from;
        if (amount >= 1)
            return to;
        static byte Channel(byte a, byte b, float t) => (byte)Math.Clamp(Math.Round(a + (b - a) * t), 0, 255);
        return new SKColor(Channel(from.Red, to.Red, amount), Channel(from.Green, to.Green, amount),
            Channel(from.Blue, to.Blue, amount), Channel(from.Alpha, to.Alpha, amount));
    }

    /// <summary>How many indicators show for <paramref name="count"/> items.</summary>
    public static int Visible(int count, int maximumVisible) => Math.Max(0, Math.Min(count, maximumVisible));

    /// <summary>
    /// The item of the first indicator shown: a window of <paramref name="visible"/> items around the selected one, kept
    /// within the items; when the selection <paramref name="wraps"/> the window wraps too (it shows the last items and the
    /// first ones together while the selection moves across the wrap).
    /// </summary>
    public static int First(int count, int visible, double position, bool wraps)
    {
        if (visible >= count || visible <= 0)
            return 0;
        var selected = (int)Math.Floor(position + 0.5); // halves up, as the carousel's position
        return wraps ? ((selected - visible / 2) % count + count) % count : Math.Clamp(selected - visible / 2, 0, count - visible);
    }

    /// <summary>The row's length for <paramref name="visible"/> indicators (the selected one <paramref name="extra"/> longer).</summary>
    public static double Length(int visible, double size, double spacing, double extra) =>
        visible <= 0 ? 0 : visible * size + (visible - 1) * spacing + extra;
}

/// <summary>
/// The state and behavior shared by <see cref="SkUiIndicatorView"/> and <see cref="Core.SkUiCoreIndicatorView"/>: the
/// settings the controls copy in, the drawn position (moved by the <see cref="SkUiTransitionKind.IndicatorPosition"/>
/// transition, or by a linked carousel), measuring, drawing and hit-testing.
/// </summary>
internal sealed class SkUiIndicatorModel(ISkUiTransitionHost host)
{
    private SkUiTween? _tween;
    private double? _driven;

    public int Position;
    public int Count;
    public double IndicatorSize = 6;
    public double Spacing = -1;
    public IndicatorShape Shape = IndicatorShape.Circle;
    public int MaximumVisible = int.MaxValue;
    public bool HideSingle = true;
    public StackOrientation Orientation = StackOrientation.Horizontal;
    /// <summary>Space around the row (MAUI's <c>IndicatorView.Padding</c>): added to the measured size; the row is centered inside it.</summary>
    public Thickness Padding;
    public Color IndicatorColor = null!;
    public Color SelectedColor = null!;

    /// <summary>The selection wraps from the last item to the first (set by a looping carousel).</summary>
    public bool Wraps;

    /// <summary>When the previous / next buttons show.</summary>
    public SkUiIndicatorButtonVisibility NavigationButtons;

    /// <summary>A mouse or pen hovers over the view.</summary>
    public bool IsPointerOver;

    // Where along the row a hovering pointer is (left-to-right, local), or NaN.
    private float _hoverAlong = float.NaN;

    /// <summary>A carousel drives the drawn position (<see cref="Drive"/>): position changes do not animate on their own.</summary>
    public bool IsDriven;

    /// <summary>The gap between indicators: the control's, or the look's.</summary>
    public double ResolvedSpacing => Spacing >= 0 ? Spacing : SkUiLook.Current.DefaultIndicatorSpacing;

    /// <summary>How much longer the selected indicator is (the look's).</summary>
    public double ExtraLength => SkUiLook.Current.GetSelectedIndicatorExtraLength(IndicatorSize, Shape);

    /// <summary>Whether nothing is drawn: no items, or a single one with <see cref="HideSingle"/>.</summary>
    public bool IsHidden => Count <= 0 || (HideSingle && Count == 1);

    /// <summary>The position drawn: the carousel's, a transition's, or <see cref="Position"/>.</summary>
    public double Displayed => _driven ?? (_tween is { IsRunning: true } tween ? tween.Value : ClampedPosition);

    /// <summary><see cref="Position"/> within the items.</summary>
    public int ClampedPosition => Count <= 0 ? 0 : Math.Clamp(Position, 0, Count - 1);

    /// <summary><see cref="Position"/> changed: the selection moves there with the look's transition (unless a carousel drives it).</summary>
    public void PositionChanged(int previous)
    {
        if (IsDriven)
            return;
        var transition = host.TransitionLook.GetTransition(SkUiTransitionKind.IndicatorPosition);
        var target = ClampedPosition;
        if (transition.IsNone || Count <= 1)
        {
            _tween?.Jump(target);
            return;
        }
        _tween ??= new SkUiTween(host);
        if (!_tween.IsRunning)
            _tween.Jump(Count <= 0 ? 0 : Math.Clamp(previous, 0, Count - 1));
        _tween.AnimateTo(target, transition);
    }

    /// <summary>A carousel's scroll position (fractional, in items) to draw; <c>null</c> draws <see cref="Position"/> again. Returns whether it changed.</summary>
    public bool Drive(double? position)
    {
        if (Nullable.Equals(_driven, position))
            return false;
        _driven = position;
        _tween?.Jump(ClampedPosition);
        return true;
    }

    /// <summary>The room each previous / next button takes at an end of the row: none while they are collapsed.</summary>
    private double ButtonSize => NavigationButtons == SkUiIndicatorButtonVisibility.Collapsed ? 0 : SkUiLook.Current.IndicatorNavigationButtonSize;

    public Size Measure(double widthConstraint, double heightConstraint)
    {
        var visible = SkUiIndicatorLayout.Visible(Count, MaximumVisible);
        var button = visible > 0 ? ButtonSize : 0;
        var length = SkUiIndicatorLayout.Length(visible, IndicatorSize, ResolvedSpacing, visible > 0 ? ExtraLength : 0) + 2 * button;
        var thickness = visible > 0 ? Math.Max(IndicatorSize, button) : 0;
        var (width, height) = Orientation == StackOrientation.Horizontal ? (length, thickness) : (thickness, length);
        return new Size(width + Padding.HorizontalThickness, height + Padding.VerticalThickness);
    }

    /// <summary>
    /// The paint struct for a view of <paramref name="width"/> × <paramref name="height"/>, the row centered inside its
    /// <see cref="Padding"/> (left-to-right coordinates).
    /// </summary>
    public SkUiIndicatorPaint CreatePaint(float width, float height, bool enabled)
    {
        var visible = SkUiIndicatorLayout.Visible(Count, MaximumVisible);
        var displayed = Displayed;
        var first = SkUiIndicatorLayout.First(Count, visible, displayed, Wraps);
        var extra = (float)ExtraLength;
        var length = (float)SkUiIndicatorLayout.Length(visible, IndicatorSize, ResolvedSpacing, extra);
        var horizontal = Orientation == StackOrientation.Horizontal;
        var size = (float)IndicatorSize;
        var button = visible > 0 ? (float)ButtonSize : 0;
        // The buttons sit at the row's ends; the row with them is centered inside the padding.
        var (rowWidth, rowHeight) = horizontal ? (length, size) : (size, length);
        var (outerWidth, outerHeight) = horizontal ? (length + 2 * button, size) : (size, length + 2 * button);
        var left = (float)Padding.Left + (width - (float)Padding.HorizontalThickness - outerWidth) / 2 + (horizontal ? button : 0);
        var top = (float)Padding.Top + (height - (float)Padding.VerticalThickness - outerHeight) / 2 + (horizontal ? 0 : button);
        var bounds = new SKRect(left, top, left + rowWidth, top + rowHeight);
        var color = SkUiShapePainter.ToSkColor(IndicatorColor);
        var selected = SkUiShapePainter.ToSkColor(SelectedColor);
        if (!enabled)
        {
            color = color.WithAlpha((byte)(color.Alpha / 2));
            selected = selected.WithAlpha((byte)(selected.Alpha / 2));
        }
        var paint = new SkUiIndicatorPaint(bounds, visible, first, (float)displayed, Count, size, (float)ResolvedSpacing, extra,
            Shape, color, selected, Orientation, Wraps, enabled);
        if (button > 0)
        {
            var shown = NavigationButtons == SkUiIndicatorButtonVisibility.Visible || IsPointerOver;
            paint = paint with
            {
                PreviousButtonBounds = shown && CanStep(-1) ? ButtonBounds(bounds, button, before: true, horizontal) : SKRect.Empty,
                NextButtonBounds = shown && CanStep(1) ? ButtonBounds(bounds, button, before: false, horizontal) : SKRect.Empty
            };
        }
        return float.IsNaN(_hoverAlong) ? paint : paint with { HoveredSlot = SlotAt(paint, _hoverAlong) };
    }

    /// <summary>A button's square at the start (<paramref name="before"/>) or end of the row, centered across it.</summary>
    private static SKRect ButtonBounds(SKRect row, float size, bool before, bool horizontal) => horizontal
        ? (before ? new SKRect(row.Left - size, row.MidY - size / 2, row.Left, row.MidY + size / 2)
            : new SKRect(row.Right, row.MidY - size / 2, row.Right + size, row.MidY + size / 2))
        : (before ? new SKRect(row.MidX - size / 2, row.Top - size, row.MidX + size / 2, row.Top)
            : new SKRect(row.MidX - size / 2, row.Bottom, row.MidX + size / 2, row.Bottom + size));

    /// <summary>Whether the selection can move by <paramref name="step"/> (±1): not past the ends, unless it wraps.</summary>
    private bool CanStep(int step) => Count > 1 && (Wraps || (step < 0 ? ClampedPosition > 0 : ClampedPosition < Count - 1));

    /// <summary>The position <paramref name="step"/> (±1) items on, wrapping when the selection wraps, or -1.</summary>
    public int Step(int step) => !CanStep(step) ? -1 : ((ClampedPosition + step) % Count + Count) % Count;

    /// <summary>The slot whose indicator covers <paramref name="along"/> (with half the spacing either side), or -1.</summary>
    private static int SlotAt(in SkUiIndicatorPaint paint, float along)
    {
        var horizontal = paint.Orientation == StackOrientation.Horizontal;
        for (var slot = 0; slot < paint.Count; slot++)
        {
            var rect = paint.GetIndicatorBounds(slot);
            var (start, end) = horizontal ? (rect.Left, rect.Right) : (rect.Top, rect.Bottom);
            if (along >= start - paint.Spacing / 2 && along < end + paint.Spacing / 2)
                return slot;
        }
        return -1;
    }

    /// <summary>
    /// A hovering pointer moved to <paramref name="point"/> (local), or left (<c>null</c>). Returns whether the drawing changed:
    /// the pointed-at indicator, or the buttons showing on hover.
    /// </summary>
    public bool Hover(Point? point, float width, float height, bool rightToLeft)
    {
        var over = point is not null;
        var along = point is { } at ? Along(at, width, rightToLeft) : float.NaN;
        var before = float.IsNaN(_hoverAlong) ? -1 : SlotAt(CreatePaint(width, height, enabled: true), _hoverAlong);
        var buttonsChange = over != IsPointerOver && NavigationButtons == SkUiIndicatorButtonVisibility.VisibleOnPointerOver;
        IsPointerOver = over;
        _hoverAlong = along;
        var after = float.IsNaN(along) ? -1 : SlotAt(CreatePaint(width, height, enabled: true), along);
        return buttonsChange || before != after;
    }

    /// <summary>The coordinate along the row of <paramref name="point"/>, in the left-to-right space the row is drawn in.</summary>
    private float Along(Point point, float width, bool rightToLeft) =>
        (float)(Orientation == StackOrientation.Horizontal ? (rightToLeft ? width - point.X : point.X) : point.Y);

    /// <summary>The button at <paramref name="point"/> (local): -1 previous, 1 next, 0 none (or not showing).</summary>
    public int HitTestButton(Point point, float width, float height, bool rightToLeft)
    {
        if (IsHidden || NavigationButtons == SkUiIndicatorButtonVisibility.Collapsed)
            return 0;
        var paint = CreatePaint(width, height, enabled: true);
        var x = (float)(rightToLeft && Orientation == StackOrientation.Horizontal ? width - point.X : point.X);
        var y = (float)point.Y;
        return paint.PreviousButtonBounds.Contains(x, y) ? -1 : paint.NextButtonBounds.Contains(x, y) ? 1 : 0;
    }

    /// <summary>The position an arrow key asks for (Left / Up previous, Right / Down next, mirrored right-to-left), or -1.</summary>
    public int KeyTarget(in SkUiKeyEvent key, bool rightToLeft)
    {
        if (key.HasCommandModifier)
            return -1;
        var horizontal = Orientation == StackOrientation.Horizontal;
        var step = key.Key switch
        {
            SkUiKey.Left when horizontal => rightToLeft ? 1 : -1,
            SkUiKey.Right when horizontal => rightToLeft ? -1 : 1,
            SkUiKey.Up when !horizontal => -1,
            SkUiKey.Down when !horizontal => 1,
            SkUiKey.Home => Count > 0 && ClampedPosition > 0 ? -ClampedPosition : 0,
            SkUiKey.End => Count > 0 && ClampedPosition < Count - 1 ? Count - 1 - ClampedPosition : 0,
            _ => 0
        };
        if (step == 0)
            return -1;
        return key.Key is SkUiKey.Home or SkUiKey.End ? ClampedPosition + step : Step(step);
    }

    public void Draw(SKCanvas canvas, float width, float height, bool rightToLeft, bool enabled)
    {
        if (IsHidden)
            return;
        var save = canvas.Save();
        if (rightToLeft && Orientation == StackOrientation.Horizontal)
            canvas.Scale(-1, 1, width / 2, 0);
        SkUiLook.Current.DrawIndicators(canvas, CreatePaint(width, height, enabled));
        canvas.RestoreToCount(save);
    }

    /// <summary>The item of the indicator nearest to <paramref name="point"/> along the row, or -1 when nothing shows.</summary>
    public int HitTest(Point point, float width, float height, bool rightToLeft)
    {
        if (IsHidden)
            return -1;
        var paint = CreatePaint(width, height, enabled: true);
        var horizontal = Orientation == StackOrientation.Horizontal;
        var along = Along(point, width, rightToLeft);
        var best = -1;
        var bestDistance = float.MaxValue;
        for (var slot = 0; slot < paint.Count; slot++)
        {
            var rect = paint.GetIndicatorBounds(slot);
            var center = horizontal ? rect.MidX : rect.MidY;
            var distance = Math.Abs(center - along);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = paint.GetItem(slot);
            }
        }
        return best;
    }

    /// <summary>The semantics of an indicator row: an adjustable position among the items.</summary>
    public void PopulateSemantics(SkUiSemanticsInfo info)
    {
        info.Role = SkUiSemanticsRole.Slider;
        info.IsHorizontal = Orientation == StackOrientation.Horizontal;
        if (Count <= 0)
            return;
        info.Range = new SkUiSemanticsRange(0, Count - 1, ClampedPosition);
        if (Count > 1)
            info.Actions |= SkUiSemanticsActions.Increment | SkUiSemanticsActions.Decrement;
    }

    /// <summary>The position a semantics action or value asks for, or -1 when it does not move.</summary>
    public int SemanticsTarget(SkUiSemanticsActions action) => action switch
    {
        SkUiSemanticsActions.Increment when Count > 1 => Wraps ? (ClampedPosition + 1) % Count : Math.Min(Count - 1, ClampedPosition + 1),
        SkUiSemanticsActions.Decrement when Count > 1 => Wraps ? (ClampedPosition + Count - 1) % Count : Math.Max(0, ClampedPosition - 1),
        _ => -1
    };

    /// <summary>The position for a semantics value (rounded, within the items), or -1.</summary>
    public int SemanticsValue(double value) => Count <= 0 || !double.IsFinite(value) ? -1 : Math.Clamp((int)Math.Round(value), 0, Count - 1);
}
