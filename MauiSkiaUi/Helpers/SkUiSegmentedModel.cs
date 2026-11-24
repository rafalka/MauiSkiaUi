using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>What the shared segmented engine (<see cref="SkUiSegmentedModel"/>) needs from its control, on either layer.</summary>
internal interface ISkUiSegmentedOwner : ISkUiTransitionHost
{
    /// <summary>The control's arranged size.</summary>
    Size ControlSize { get; }

    /// <summary>Whether the control lays out right to left (the first segment at the right end).</summary>
    bool IsRightToLeft { get; }

    void InvalidateMeasure();
    void InvalidatePaint();
    void InvalidateSemantics();
}

/// <summary>
/// The engine both segmented controls share (<c>SkUiSegmentedControl</c>, <c>SkUiCoreSegmentedControl</c>): the segments'
/// texts and icons, measured on the shared text engine and placed by <see cref="SkUiSegmentedEngine"/>, the look's chrome
/// (<see cref="SkUiLook.DrawSegmented"/>) with the texts and icons over it, the selection marker animated between segments
/// (<see cref="SkUiTransitionKind.SegmentSelection"/>; at once under reduce motion), and the pressed, hovered and focused
/// states. The selected index stays the control's: it tells the model with <see cref="Select"/>.
/// </summary>
internal sealed class SkUiSegmentedModel
{
    private sealed class Segment
    {
        public string Text = string.Empty;
        public ImageSource? Icon;
        public bool IsEnabled = true;
        public SkUiTextLayout? Layout;
        public SkUiImageSlot? IconSlot;
        public Size TextSize = new(-1, -1); // measured with the current style; negative: not yet
    }

    private readonly ISkUiSegmentedOwner _owner;
    private readonly List<Segment> _segments = [];
    private readonly SkUiSegmentedEngine _engine = new();
    private readonly SkUiTween _selection;
    private readonly SkUiTween _press;
    private readonly SkUiTween _focus;
    // Reused for each drawing's paint (no allocation per frame of an animation).
    private readonly List<SKRect> _rects = [];
    private readonly List<float> _contents = [];
    private readonly List<bool> _enabled = [];
    private double[] _widths = [];
    private SKRect _from;          // the marker where a change started (it slides from there)
    private int _previous = -1;    // the segment a change leaves
    private int _pressed = -1;
    private int _hovered = -1;
    private bool _measured;
    private (SkUiLook Look, SkUiColorScheme Scheme, SkUiSegmentedColors Colors)? _colors;

    public SkUiSegmentedModel(ISkUiSegmentedOwner owner)
    {
        _owner = owner;
        _selection = new SkUiTween(owner);
        _selection.Jump(1);
        _press = new SkUiTween(owner);
        _focus = new SkUiTween(owner);
    }

    #region State the controls copy in

    public bool IsEnabled = true;
    public Color? TextColor;
    public Color? SelectedTextColor;
    public Color? SelectionColor;
    public Color? TrackColor;

    /// <summary>The track's corner radius in DIPs; negative: the look's.</summary>
    public double CornerRadius = -1;

    public string? FontFamily;

    /// <summary>The font size set on the control, or NaN for the look's.</summary>
    public double FontSize = double.NaN;

    public FontAttributes FontAttributes;
    public bool FontAutoScalingEnabled = true;

    private SkUiSegmentWidthMode _widthMode;

    /// <summary>How the segments share the width.</summary>
    public SkUiSegmentWidthMode WidthMode
    {
        get => _widthMode;
        set
        {
            if (_widthMode == value)
                return;
            _widthMode = value;
            Changed();
        }
    }

    /// <summary>The selected segment, or -1 (the control's, copied in by <see cref="Select"/>).</summary>
    public int Selected { get; private set; } = -1;

    /// <summary>How far the selection change has come, 0–1 (tests).</summary>
    internal float SelectionProgress => _selection.Value;

    /// <summary>The number of segments.</summary>
    public int Count => _segments.Count;

    /// <summary>The text of segment <paramref name="index"/>.</summary>
    public string TextOf(int index) => _segments[index].Text;

    /// <summary>Whether segment <paramref name="index"/> can be chosen (it and the control are enabled).</summary>
    public bool IsSegmentEnabled(int index) => IsEnabled && index >= 0 && index < _segments.Count && _segments[index].IsEnabled;

    #endregion

    #region Segments

    /// <summary>The number of segments changed: removed segments' resources go, added ones start empty.</summary>
    public void SetCount(int count)
    {
        while (_segments.Count > count)
        {
            var removed = _segments[^1];
            removed.Layout?.Release();
            removed.IconSlot?.Dispose();
            _segments.RemoveAt(_segments.Count - 1);
        }
        while (_segments.Count < count)
            _segments.Add(new Segment());
        _pressed = _hovered = -1;
        Changed();
    }

    /// <summary>Segment <paramref name="index"/> shows <paramref name="text"/> and <paramref name="icon"/>, enabled or not.</summary>
    public void SetSegment(int index, string? text, ImageSource? icon, bool enabled)
    {
        var segment = _segments[index];
        text ??= string.Empty;
        var changed = false;
        if (!string.Equals(segment.Text, text, StringComparison.Ordinal))
        {
            segment.Text = text;
            segment.TextSize = new Size(-1, -1);
            changed = true;
        }
        if (!ReferenceEquals(segment.Icon, icon))
        {
            segment.Icon = icon;
            if (icon is not null || segment.IconSlot is not null)
                (segment.IconSlot ??= new SkUiImageSlot(_owner, _owner.InvalidatePaint)).Load(SkUiImageSource.FromMaui(icon), default);
            changed = true;
        }
        if (segment.IsEnabled != enabled)
        {
            segment.IsEnabled = enabled;
            _owner.InvalidatePaint();
            _owner.InvalidateSemantics();
        }
        if (changed)
            Changed();
    }

    /// <summary>The font or the look changed: the texts are measured again.</summary>
    public void TextStyleChanged()
    {
        foreach (var segment in _segments)
        {
            segment.Layout?.Invalidate();
            segment.TextSize = new Size(-1, -1);
        }
        Changed();
    }

    /// <summary>The colors set on the control changed.</summary>
    public void ColorsChanged() => _owner.InvalidatePaint();

    private void Changed()
    {
        _measured = false;
        _owner.InvalidateMeasure();
        _owner.InvalidatePaint();
        _owner.InvalidateSemantics();
    }

    #endregion

    #region Selection

    /// <summary>
    /// <paramref name="index"/> is selected now: the marker slides (or fades) there from where it is, when
    /// <paramref name="animate"/> and the look animates; else it moves at once.
    /// </summary>
    public void Select(int index, bool animate)
    {
        if (index == Selected)
            return;
        var from = MarkerNow();
        _previous = Selected;
        Selected = index;
        _from = from;
        if (animate && _previous >= 0 && index >= 0 && !from.IsEmpty)
        {
            _selection.Jump(0);
            _selection.AnimateTo(1, SkUiTransitionKind.SegmentSelection);
        }
        else
        {
            _selection.Jump(1);
        }
        _owner.InvalidatePaint();
        _owner.InvalidateSemantics();
    }

    /// <summary>The marker's rectangle as drawn now (between two segments while it moves).</summary>
    private SKRect MarkerNow()
    {
        if (Selected < 0 || Selected >= _engine.Count || _engine.Length <= 0)
            return SKRect.Empty;
        var target = ToSk(_engine.Segment(Selected));
        var progress = _selection.Value;
        if (progress >= 1 || _from.IsEmpty)
            return target;
        return new SKRect(_from.Left + (target.Left - _from.Left) * progress, _from.Top + (target.Top - _from.Top) * progress,
            _from.Right + (target.Right - _from.Right) * progress, _from.Bottom + (target.Bottom - _from.Bottom) * progress);
    }

    #endregion

    #region Layout

    private SkUiTextStyle TextStyle => new(SkUiTypefaces.Resolve(FontFamily, FontAttributes),
        SkUiFontScaling.ScaleFontSize(SkUiLook.FontSizeOrDefault(FontSize), FontAutoScalingEnabled),
        LineBreakMode.TailTruncation, null, 1, -1, 0, _owner.IsRightToLeft ? SkUiTextDirection.RightToLeft : SkUiTextDirection.Auto,
        SkUiTextRendering.Default, FontAttributes);

    private Size TextSize(Segment segment, in SkUiTextStyle style)
    {
        if (segment.TextSize.Width >= 0)
            return segment.TextSize;
        if (segment.Text.Length == 0)
            return segment.TextSize = Size.Zero;
        var layout = segment.Layout ??= new SkUiTextLayout(_owner);
        var size = layout.Measure(segment.Text, style, default, double.PositiveInfinity);
        return segment.TextSize = new Size(Math.Ceiling(size.Width), Math.Ceiling(size.Height));
    }

    /// <summary>The room an icon takes before the text (none without one).</summary>
    private static double IconPart(Segment segment, in SkUiSegmentedMetrics metrics) =>
        segment.Icon is null ? 0 : metrics.IconSize + (segment.Text.Length > 0 ? metrics.IconSpacing : 0);

    /// <summary>Gives the engine each segment's content width and the look's insets; returns the tallest text.</summary>
    private double PrepareEngine(in SkUiSegmentedMetrics metrics)
    {
        var style = TextStyle;
        if (_widths.Length < _segments.Count)
            _widths = new double[_segments.Count];
        var tallest = 0d;
        for (var index = 0; index < _segments.Count; index++)
        {
            var segment = _segments[index];
            var text = TextSize(segment, style);
            tallest = Math.Max(tallest, Math.Max(text.Height, segment.Icon is null ? 0 : metrics.IconSize));
            _widths[index] = text.Width + IconPart(segment, metrics) + 2 * metrics.SegmentPadding;
        }
        _engine.Mode = _widthMode;
        _engine.Inset = metrics.Inset;
        _engine.Spacing = metrics.Spacing;
        _engine.Mirrored = _owner.IsRightToLeft;
        _engine.SetContentWidths(_widths.AsSpan(0, _segments.Count));
        _measured = true;
        return tallest;
    }

    /// <summary>The control's size: its segments' content (each as wide as the widest in equal mode), at least the look's height.</summary>
    public Size Measure(double widthConstraint, double heightConstraint)
    {
        var metrics = SkUiLook.Current.SegmentedMetrics;
        var tallest = PrepareEngine(metrics);
        // Larger text (a font size, the system's text size) makes the control taller than the look's height.
        var height = Math.Max(metrics.Height, Math.Ceiling(tallest + 6 + 2 * metrics.Inset));
        var width = _engine.DesiredWidth();
        return new Size(Math.Min(widthConstraint, width), Math.Min(heightConstraint, height));
    }

    /// <summary>Lays the segments out for the control's current size (after a measure; also when drawing without one).</summary>
    private void Arrange(float width, float height)
    {
        if (!_measured || _engine.Mirrored != _owner.IsRightToLeft)
            PrepareEngine(SkUiLook.Current.SegmentedMetrics);
        _engine.Arrange(width, height);
    }

    /// <summary>The segment at <paramref name="local"/>, or -1 (outside the control, or no segments).</summary>
    public int HitTest(Point local)
    {
        var size = _owner.ControlSize;
        if (local.Y < 0 || local.Y > size.Height)
            return -1;
        Arrange((float)size.Width, (float)size.Height);
        return _engine.HitTest(local.X);
    }

    /// <summary>Segment <paramref name="index"/>'s rectangle in the control (semantics, tests).</summary>
    public Rect SegmentBounds(int index)
    {
        var size = _owner.ControlSize;
        Arrange((float)size.Width, (float)size.Height);
        return index >= 0 && index < _engine.Count ? _engine.Segment(index) : Rect.Zero;
    }

    #endregion

    #region Drawing

    private SkUiSegmentedColors LookColors(SkUiLook look)
    {
        var scheme = SkUiColorScheme.Current;
        if (_colors is { } cached && ReferenceEquals(cached.Look, look) && ReferenceEquals(cached.Scheme, scheme))
            return cached.Colors;
        var colors = look.GetSegmentedColors(scheme);
        _colors = (look, scheme, colors);
        return colors;
    }

    /// <summary>Draws the look's chrome, then each segment's icon and text in the look's color for how selected it is.</summary>
    public void Draw(SKCanvas canvas, float width, float height)
    {
        if (width <= 0 || height <= 0)
            return;
        var look = SkUiLook.Current;
        var metrics = look.SegmentedMetrics;
        Arrange(width, height);
        _rects.Clear();
        _contents.Clear();
        _enabled.Clear();
        for (var index = 0; index < _segments.Count; index++)
        {
            var segment = _segments[index];
            _rects.Add(ToSk(_engine.Segment(index)));
            _contents.Add((float)(Math.Max(0, segment.TextSize.Width) + IconPart(segment, metrics)));
            _enabled.Add(segment.IsEnabled);
        }
        var colors = LookColors(look);
        var changing = _selection.Value < 1 && _previous >= 0;
        var paint = new SkUiSegmentedPaint
        {
            Bounds = new SKRect(0, 0, width, height),
            Segments = _rects,
            ContentWidths = _contents,
            EnabledSegments = _enabled,
            Selection = MarkerNow(),
            SelectedIndex = Selected,
            PreviousIndex = changing ? _previous : -1,
            Progress = _selection.Value,
            Track = ToSk(TrackColor ?? colors.Track),
            SelectionColor = ToSk(SelectionColor ?? colors.Selection),
            Text = ToSk(TextColor ?? colors.Text),
            SelectedText = ToSk(SelectedTextColor ?? colors.SelectedText),
            Border = ToSk(colors.Border),
            Accent = ToSk(SkUiColors.Accent),
            CornerRadius = (float)(CornerRadius >= 0 ? CornerRadius : metrics.CornerRadius),
            PressedIndex = _pressed,
            Pressed = _press.Value,
            HoveredIndex = _hovered,
            Focused = _focus.Value,
            IsEnabled = IsEnabled,
            IsRightToLeft = _owner.IsRightToLeft
        };
        look.DrawSegmented(canvas, paint);
        var style = TextStyle;
        var rtl = _owner.IsRightToLeft;
        for (var index = 0; index < _segments.Count; index++)
        {
            var segment = _segments[index];
            var rect = _rects[index];
            var color = look.GetSegmentTextColor(paint, index);
            var leading = look.GetSegmentLeadingWidth(paint, index);
            var icon = (float)IconPart(segment, metrics);
            var text = TextSize(segment, style);
            // The text shrinks (truncated) when the segment is narrower than its content.
            var room = Math.Max(0, rect.Width - 2 * (float)metrics.SegmentPadding - leading - icon);
            var textWidth = Math.Min((float)text.Width, room);
            var content = icon + textWidth;
            // Centered together with the check's room; the check (and the icon) at the start side.
            var start = rtl ? rect.MidX + (content + leading) / 2 - leading : rect.MidX - (content + leading) / 2 + leading;
            if (segment.IconSlot is { } slot && segment.Icon is not null)
            {
                var size = (float)metrics.IconSize;
                var left = rtl ? start - size : start;
                var area = new SKRect(left, rect.MidY - size / 2, left + size, rect.MidY + size / 2);
                var layer = color.Alpha < 255 ? canvas.SaveLayer(Paint(color.Alpha)) : -1;
                slot.Paint(canvas, area, Aspect.AspectFit);
                if (layer >= 0)
                    canvas.RestoreToCount(layer);
            }
            if (segment.Layout is { } layout && textWidth > 0)
            {
                var textLeft = rtl ? start - icon - textWidth : start + icon;
                var textPaint = SkUiTextResources.TextPaint;
                textPaint.Color = color;
                var save = canvas.Save();
                canvas.Translate(textLeft, rect.Top);
                layout.Draw(canvas, segment.Text, style, default, textWidth, rect.Height, TextAlignment.Center, TextAlignment.Center, textPaint);
                canvas.RestoreToCount(save);
            }
        }
    }

    [ThreadStatic] private static SKPaint? _layerPaint;

    private static SKPaint Paint(byte alpha)
    {
        var paint = _layerPaint ??= new SKPaint();
        paint.Color = SKColors.Black.WithAlpha(alpha);
        return paint;
    }

    private static SKRect ToSk(Rect rect) => new((float)rect.Left, (float)rect.Top, (float)rect.Right, (float)rect.Bottom);

    private static SKColor ToSk(Color color) => SkUiToggleDrawing.ToSkColor(color);

    #endregion

    #region Input

    /// <summary>Segment <paramref name="index"/> is pressed (-1: none); an enabled one darkens.</summary>
    public void Press(int index)
    {
        if (!IsSegmentEnabled(index))
            return;
        _pressed = index;
        _press.AnimateTo(1, SkUiTransitionKind.Press);
    }

    /// <summary>The press ended (a tap, or cancelled).</summary>
    public void Release()
    {
        if (_pressed >= 0)
            _press.AnimateTo(0, SkUiTransitionKind.Release);
    }

    /// <summary>A hovering pointer moved to <paramref name="local"/> (or left, <c>null</c>); returns whether the hovered segment changed.</summary>
    public bool Hover(Point? local)
    {
        var index = local is { } point ? HitTest(point) : -1;
        if (!IsSegmentEnabled(index))
            index = -1;
        if (index == _hovered)
            return false;
        _hovered = index;
        return true;
    }

    /// <summary>The control gained or lost keyboard focus.</summary>
    public void SetFocused(bool focused)
    {
        _focus.AnimateTo(focused ? 1f : 0f, SkUiTransitionKind.FieldFocus);
        if (!focused)
            Release();
        _owner.InvalidatePaint();
    }

    /// <summary>The control left its surface: the texts' blobs go (the icons stay loaded).</summary>
    public void ReleaseDrawingResources()
    {
        foreach (var segment in _segments)
            segment.Layout?.Release();
    }

    #endregion
}
