using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// Built-in control look: cross-platform geometry and intrinsic sizes (former <c>SkUiChrome</c>), the same on every platform;
/// <see cref="AndroidSkUiLook"/>, <see cref="IosSkUiLook"/>, <see cref="MacSkUiLook"/> and <see cref="WindowsSkUiLook"/> mimic
/// one platform each (<see cref="ForCurrentPlatform"/>).
/// Subclass or set painter/measure delegates to customize without replacing every control type.
/// State changes animate (<see cref="GetTransitionCore"/>): switch thumbs slide, check marks draw in, radio dots grow,
/// pressed buttons dim or ripple (<see cref="PressEffect"/>) and slider thumbs glide to tapped values.
/// </summary>
public class DefaultSkUiLook : SkUiLook
{
    /// <summary>Shared default look used as the process <see cref="SkUiLook.Current"/>.</summary>
    public static DefaultSkUiLook Instance { get; } = new();

    /// <summary>
    /// The shared look that mimics the platform the app runs on: <see cref="AndroidSkUiLook"/> (<see cref="AndroidMaterial3SkUiLook"/>
    /// when the app turns on MAUI's Material 3), <see cref="IosSkUiLook"/>,
    /// <see cref="MacSkUiLook"/> (Mac Catalyst) or <see cref="WindowsSkUiLook"/>; <see cref="Instance"/> elsewhere (headless).
    /// The default look is the same on every platform; set this as <see cref="SkUiLook.Current"/> (or
    /// <c>UseSkiaUi(options => options.Look = DefaultSkUiLook.ForCurrentPlatform())</c>) for the platform's.
    /// </summary>
    public static DefaultSkUiLook ForCurrentPlatform() =>
        OperatingSystem.IsAndroid() ? AndroidSkUiLook.IsMaterial3Enabled ? AndroidMaterial3SkUiLook.Instance : AndroidSkUiLook.Instance
        : OperatingSystem.IsMacCatalyst() ? MacSkUiLook.Instance
        : OperatingSystem.IsIOS() ? IosSkUiLook.Instance
        : OperatingSystem.IsWindows() ? WindowsSkUiLook.Instance
        : Instance;

    /// <summary>
    /// The color scheme that goes with this look, light or dark: the default palettes here, the platform's in the platform looks
    /// (assign it to <see cref="SkUiColorScheme.Current"/>; the look itself owns shapes, sizes and motion only).
    /// </summary>
    public virtual SkUiColorScheme GetColorScheme(bool isDark) => isDark ? DarkSkUiColorScheme.Instance : LightSkUiColorScheme.Instance;

    /// <summary>
    /// Button and ImageButton press feedback (default <see cref="SkUiPressEffect.Default"/>: the look's, <see cref="BasePressEffect"/>).
    /// Repaint after changing it.
    /// </summary>
    public SkUiPressEffect PressEffect { get; set; }

    /// <summary>The press feedback a <see cref="SkUiPressEffect.Default"/> <see cref="PressEffect"/> resolves to: a dim. Android's look ripples.</summary>
    public virtual SkUiPressEffect BasePressEffect => SkUiPressEffect.Dim;

    /// <summary>The press feedback in effect: <see cref="PressEffect"/>, or the look's when that is <see cref="SkUiPressEffect.Default"/>.</summary>
    public SkUiPressEffect EffectivePressEffect => PressEffect == SkUiPressEffect.Default ? BasePressEffect : PressEffect;

    /// <summary>
    /// Overscroll of scrollers that do not set their own (default <see cref="SkUiOverscrollMode.Default"/>: the look's,
    /// <see cref="BaseOverscroll"/>). Repaint after changing it.
    /// </summary>
    public SkUiOverscrollMode Overscroll { get; set; } = SkUiOverscrollMode.Default;

    /// <summary>
    /// The overscroll an <see cref="SkUiOverscrollMode.Default"/> <see cref="Overscroll"/> resolves to: none (stop at the edge),
    /// the same on every platform. Platform looks use their platform's (<see cref="IosSkUiLook"/>: bounce).
    /// </summary>
    public virtual SkUiOverscrollMode BaseOverscroll => SkUiOverscrollMode.None;

    /// <inheritdoc />
    public override SkUiOverscrollMode DefaultOverscroll => Overscroll == SkUiOverscrollMode.Default ? BaseOverscroll : Overscroll;

    /// <summary>
    /// Pull-to-refresh style of controls that do not set their own (default <see cref="SkUiRefreshStyle.Default"/>: the look's,
    /// <see cref="BaseRefreshStyle"/>).
    /// </summary>
    public SkUiRefreshStyle RefreshStyle { get; set; } = SkUiRefreshStyle.Default;

    /// <summary>
    /// The style a <see cref="SkUiRefreshStyle.Default"/> <see cref="RefreshStyle"/> resolves to: an overlay badge, the same on
    /// every platform. Apple's platform looks use an inline indicator (UIRefreshControl).
    /// </summary>
    public virtual SkUiRefreshStyle BaseRefreshStyle => SkUiRefreshStyle.Overlay;

    /// <inheritdoc />
    public override SkUiRefreshStyle DefaultRefreshStyle => RefreshStyle == SkUiRefreshStyle.Default ? BaseRefreshStyle : RefreshStyle;

    /// <summary>
    /// How picker fields draw (default <see cref="SkUiPickerFieldStyle.Default"/>: the look's,
    /// <see cref="DefaultPickerFieldStyle"/>). Call <see cref="SkUiLook.NotifyChanged"/> after changing it.
    /// </summary>
    public SkUiPickerFieldStyle PickerFieldStyle { get; set; }

    /// <summary>
    /// The picker field a <see cref="SkUiPickerFieldStyle.Default"/> <see cref="PickerFieldStyle"/> draws: an outline, the same
    /// on every platform. Platform looks draw their platform's (<see cref="AndroidSkUiLook"/>: Material's underline).
    /// </summary>
    public virtual SkUiPickerFieldStyle DefaultPickerFieldStyle => SkUiPickerFieldStyle.Outline;

    /// <summary>
    /// Where pickers that do not set their own placement open their list (default <see cref="SkUiPopupPlacement.Default"/>: the
    /// device's, <see cref="DevicePopupPlacement"/>).
    /// </summary>
    public SkUiPopupPlacement PopupPlacement { get; set; }

    /// <summary>
    /// The device's popup placement: anchored as a combo box on desktops (Mac Catalyst, Windows) and tablets; on phones a dialog
    /// in the window, where a long list has room.
    /// </summary>
    public static SkUiPopupPlacement DevicePopupPlacement => IsPhone ? SkUiPopupPlacement.Window : SkUiPopupPlacement.Anchored;

    /// <inheritdoc />
    public override SkUiPopupPlacement DefaultPopupPlacement => PopupPlacement == SkUiPopupPlacement.Default ? DevicePopupPlacement : PopupPlacement;

    /// <summary>Whether the app runs on a phone (Android or iOS; headless runs, without a device, are not).</summary>
    protected static bool IsPhone =>
        (OperatingSystem.IsAndroid() || (OperatingSystem.IsIOS() && !OperatingSystem.IsMacCatalyst())) && DeviceInfo.Current.Idiom == DeviceIdiom.Phone;

    /// <summary>
    /// How indicator views draw (default <see cref="SkUiIndicatorStyle.Default"/>: the look's, <see cref="BaseIndicatorStyle"/>).
    /// Call <see cref="SkUiLook.NotifyChanged"/> after changing it.
    /// </summary>
    public SkUiIndicatorStyle IndicatorStyle { get; set; }

    /// <summary>The style a <see cref="SkUiIndicatorStyle.Default"/> <see cref="IndicatorStyle"/> resolves to: dots. Android's look draws a pill.</summary>
    public virtual SkUiIndicatorStyle BaseIndicatorStyle => SkUiIndicatorStyle.Dots;

    /// <summary>The indicator style in effect: <see cref="IndicatorStyle"/>, or the look's when that is <see cref="SkUiIndicatorStyle.Default"/>.</summary>
    public SkUiIndicatorStyle EffectiveIndicatorStyle => IndicatorStyle == SkUiIndicatorStyle.Default ? BaseIndicatorStyle : IndicatorStyle;

    /// <inheritdoc />
    /// <remarks>A <see cref="SkUiIndicatorStyle.Pill"/> is twice the indicator size longer.</remarks>
    public override double GetSelectedIndicatorExtraLength(double indicatorSize, IndicatorShape shape) =>
        EffectiveIndicatorStyle == SkUiIndicatorStyle.Pill ? 2 * indicatorSize : 0;

    /// <inheritdoc />
    /// <remarks>
    /// Circles (or squares) in a blend of the two colors by selection; a <see cref="SkUiIndicatorStyle.Pill"/> rounds the
    /// stretched selected indicator (squares keep a small corner).
    /// </remarks>
    protected override void DrawIndicatorsCore(SKCanvas canvas, SkUiIndicatorPaint paint)
    {
        if (paint.Count <= 0 || paint.IndicatorSize <= 0)
            return;
        var fill = Paint(paint.Color);
        var radius = paint.Shape == IndicatorShape.Circle ? paint.IndicatorSize / 2 : Math.Min(1.5f, paint.IndicatorSize / 4);
        for (var slot = 0; slot < paint.Count; slot++)
        {
            fill.Color = paint.GetColor(slot);
            canvas.DrawRoundRect(paint.GetIndicatorBounds(slot), radius, radius, fill);
        }
    }

    private static readonly Shadow OverlayRefreshShadow = new() { Brush = Colors.Black, Opacity = 0.25f, Radius = 4, Offset = new Point(0, 1) };

    /// <inheritdoc />
    /// <remarks>The overlay badge casts a soft shadow (Android's); inline indicators none.</remarks>
    public override Shadow? GetRefreshIndicatorShadow(SkUiRefreshStyle style) => style == SkUiRefreshStyle.Overlay ? OverlayRefreshShadow : null;

    /// <inheritdoc />
    protected override SkUiTransition GetTransitionCore(SkUiTransitionKind kind) => kind switch
    {
        SkUiTransitionKind.Switch => SkUiTransition.FromMilliseconds(200, Easing.CubicInOut),
        SkUiTransitionKind.CheckBox => SkUiTransition.FromMilliseconds(160, Easing.CubicInOut),
        SkUiTransitionKind.RadioButton => SkUiTransition.FromMilliseconds(160, Easing.CubicInOut),
        SkUiTransitionKind.Press => SkUiTransition.FromMilliseconds(80, Easing.CubicOut),
        SkUiTransitionKind.Release => SkUiTransition.FromMilliseconds(220, Easing.CubicOut),
        // Only rippling looks spend frames on the ripple.
        SkUiTransitionKind.Ripple => EffectivePressEffect == SkUiPressEffect.Ripple ? SkUiTransition.FromMilliseconds(450, Easing.CubicOut) : SkUiTransition.None,
        SkUiTransitionKind.SliderThumb => SkUiTransition.FromMilliseconds(150, Easing.CubicOut),
        SkUiTransitionKind.IndicatorPosition => SkUiTransition.FromMilliseconds(250, Easing.CubicOut),
        SkUiTransitionKind.FieldFocus => SkUiTransition.FromMilliseconds(150, Easing.CubicOut),
        SkUiTransitionKind.PickerOpen => SkUiTransition.FromMilliseconds(200, Easing.CubicInOut),
        // Determinate progress follows Progress at once, as MAUI's ProgressBar (ProgressTo animates).
        _ => SkUiTransition.None
    };

    /// <inheritdoc />
    /// <remarks>
    /// Plain circular corners draw with <c>DrawRoundRect</c> (no path); a look with
    /// <see cref="SkUiLook.CreateCustomRoundRectPath"/> geometry draws that path instead, so it shapes buttons, switches
    /// and other rounded chrome too.
    /// </remarks>
    protected override void DrawRoundedBoxCore(SKCanvas canvas, SKRect bounds, float radius, SKColor fill, SKColor border, float width)
    {
        var paint = Paint(fill);
        var strokeRadius = Math.Max(0, radius - width / 2);
        using var path = CreateCustomRoundRectPath(bounds, new CornerRadius(radius));
        if (path is null)
        {
            // Plain corners: no path allocation.
            if (fill.Alpha != 0)
                canvas.DrawRoundRect(bounds, radius, radius, paint);
            if (width <= 0 || border.Alpha == 0) return;
            bounds.Inflate(-width / 2, -width / 2);
            Stroke(paint, border, width);
            canvas.DrawRoundRect(bounds, strokeRadius, strokeRadius, paint);
            return;
        }
        canvas.DrawPath(path, paint);
        if (width <= 0) return;
        bounds.Inflate(-width / 2, -width / 2);
        Stroke(paint, border, width);
        using var strokePath = CreateRoundRectPath(bounds, strokeRadius);
        canvas.DrawPath(strokePath, paint);
    }

    [ThreadStatic] private static SKPaint? _paint;

    /// <summary>Per-thread reusable fill paint (recording is single-threaded per surface; pictures copy paint state).</summary>
    private static SKPaint Paint(SKColor color)
    {
        var paint = _paint ??= new SKPaint();
        paint.Reset();
        paint.IsAntialias = true;
        paint.Color = color;
        return paint;
    }

    private static void Stroke(SKPaint paint, SKColor color, float width)
    {
        paint.Color = color;
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = width;
    }

    /// <inheritdoc />
    protected override void DrawRoundedBoxCore(SKCanvas canvas, SKRect bounds, CornerRadius radii, SKColor fill, SKColor border, float width)
    {
        if (IsUniformCornerRadius(radii))
        {
            DrawRoundedBoxCore(canvas, bounds, (float)radii.TopLeft, fill, border, width);
            return;
        }

        var paint = Paint(fill);
        using var path = CreateRoundRectPath(bounds, radii);
        canvas.DrawPath(path, paint);
        if (width <= 0) return;
        bounds.Inflate(-width / 2, -width / 2);
        Stroke(paint, border, width);
        using var strokePath = CreateRoundRectPath(bounds, ShrinkCornerRadius(radii, width / 2));
        canvas.DrawPath(strokePath, paint);
    }

    /// <inheritdoc />
    protected override void DrawButtonCore(SKCanvas canvas, SkUiButtonPaint button)
    {
        var dim = EffectivePressEffect == SkUiPressEffect.Dim ? 1 - 0.25f * button.Press.Pressed : 1;
        if (button.FillPaint is { } gradient)
        {
            DrawRoundedBox(canvas, button.Bounds, button.CornerRadii, SkUiShapePainter.WithOpacity(gradient, dim), button.Border, button.BorderWidth);
        }
        else
        {
            var fill = button.Fill;
            if (dim < 1)
                fill = fill.WithAlpha((byte)(fill.Alpha * dim));
            DrawRoundedBox(canvas, button.Bounds, button.CornerRadii, fill, button.Border, button.BorderWidth);
        }
        if (ButtonHasElevationBorder && button.BorderWidth > 0 && button.Border.Alpha > 0)
        {
            // Fluent's elevation border: the bottom edge, corners included, darker than the rest. Drawn over the border, so
            // its opacity is what makes the two together ElevationBorderDarkening times the border's.
            var alpha = button.Border.Alpha / 255f;
            var target = Math.Min(1, alpha * ElevationBorderDarkening);
            var extra = alpha < 1 ? (target - alpha) / (1 - alpha) : 0;
            canvas.Save();
            canvas.ClipRect(new SKRect(button.Bounds.Left, button.Bounds.Bottom - button.BorderWidth, button.Bounds.Right, button.Bounds.Bottom));
            DrawRoundedBox(canvas, button.Bounds, button.CornerRadii, SKColors.Transparent,
                button.Border.WithAlpha((byte)Math.Round(255 * extra)), button.BorderWidth);
            canvas.Restore();
        }
        if (EffectivePressEffect == SkUiPressEffect.Ripple && button.IsEnabled)
            DrawRipple(canvas, button.Bounds, button.CornerRadii, button.Press, RippleColor(button.Fill));
    }

    /// <summary>How much more opaque the bottom edge of an elevation border is (Fluent: #29 against #0F).</summary>
    private const float ElevationBorderDarkening = 2.75f;

    /// <summary>
    /// Whether a button's border is darker along its bottom edge (Fluent's elevation border; default <c>false</c>: one color).
    /// </summary>
    public virtual bool ButtonHasElevationBorder => false;

    /// <summary>How switches draw: a filled pill with a round thumb (iOS), or an outlined track whose thumb grows when on (Material, Fluent).</summary>
    public virtual SkUiSwitchStyle SwitchStyle => SkUiSwitchStyle.Pill;

    /// <summary>The width of a <see cref="SkUiSwitchStyle.Pill"/> switch's thumb, DIPs, or 0 for a circle (iOS 26's thumb is a capsule).</summary>
    public virtual float SwitchThumbWidth => 0;

    /// <summary>The gap between a <see cref="SkUiSwitchStyle.Pill"/> switch's thumb and the track's edge, DIPs.</summary>
    public virtual float SwitchThumbInset => 2;

    /// <summary>
    /// The height of a switch's track, centered in the control, DIPs (default 0: the control's height; Material 2: 14, Material 3:
    /// 32 in a 48 dp control). Used by the <see cref="SkUiSwitchStyle.Outlined"/> and <see cref="SkUiSwitchStyle.ThumbOverTrack"/> styles.
    /// </summary>
    public virtual float SwitchTrackHeight => 0;

    /// <summary>The diameter of a <see cref="SkUiSwitchStyle.ThumbOverTrack"/> switch's thumb, DIPs.</summary>
    public virtual float SwitchThumbDiameter => 20;

    /// <summary>The width of a <see cref="SkUiSwitchStyle.ThumbOverTrack"/> switch's track, centered in the control, DIPs.</summary>
    public virtual float SwitchTrackWidth => 23;

    /// <summary>How far a <see cref="SkUiSwitchStyle.ThumbOverTrack"/> switch's thumb center moves from off to on, DIPs (centered in the control).</summary>
    public virtual float SwitchThumbTravel => 19.5f;

    /// <summary>The <see cref="SkUiSwitchStyle.Outlined"/> thumb's radius when off, as a fraction of the track's height.</summary>
    public virtual float SwitchOffThumbRadiusFraction => 0.3f;

    /// <summary>The <see cref="SkUiSwitchStyle.Outlined"/> thumb's radius when on, as a fraction of the track's height.</summary>
    public virtual float SwitchOnThumbRadiusFraction => 0.3f;

    /// <summary>The width of the <see cref="SkUiSwitchStyle.Outlined"/> track's outline when off.</summary>
    public virtual float SwitchOutlineWidth => 1;

    /// <inheritdoc />
    protected override void DrawSwitchCore(SKCanvas canvas, SkUiSwitchPaint toggle)
    {
        if (SwitchStyle == SkUiSwitchStyle.Outlined)
        {
            DrawOutlinedSwitch(canvas, toggle);
            return;
        }
        if (SwitchStyle == SkUiSwitchStyle.ThumbOverTrack)
        {
            DrawThumbOverTrackSwitch(canvas, toggle);
            return;
        }
        var bounds = toggle.Bounds;
        var visual = toggle.Visual;
        var radius = bounds.Height / 2;
        // Indeterminate: halfway between off and on, thumb in the middle.
        var track = visual.Blend(toggle.OffTrack, toggle.OnTrack, Mix(toggle.OffTrack, toggle.OnTrack, 0.5f));
        DrawRoundedBox(canvas, bounds, radius, track, SKColors.Transparent, 0);
        var thumbRadius = radius - SwitchThumbInset;
        // A round thumb, or a capsule (iOS 26) as wide as SwitchThumbWidth.
        var halfWidth = SwitchThumbWidth > 0 ? SwitchThumbWidth / 2 : thumbRadius;
        var start = bounds.Left + SwitchThumbInset + halfWidth;
        var end = bounds.Right - SwitchThumbInset - halfWidth;
        var thumbX = visual.Blend(start, end, bounds.MidX);
        // Pressed: the thumb stretches towards the middle (iOS-like).
        var stretch = thumbRadius * 0.35f * visual.Pressed;
        var position = end > start ? (thumbX - start) / (end - start) : 0;
        var thumb = new SKRect(thumbX - halfWidth - stretch * position, bounds.MidY - thumbRadius,
            thumbX + halfWidth + stretch * (1 - position), bounds.MidY + thumbRadius);
        canvas.DrawRoundRect(thumb, thumbRadius, thumbRadius, Paint(toggle.Thumb));
    }

    /// <summary>The track of an <see cref="SkUiSwitchStyle.Outlined"/> switch is an outline while off and fills when on; the thumb takes the track's color while off.</summary>
    private void DrawOutlinedSwitch(SKCanvas canvas, SkUiSwitchPaint toggle)
    {
        var bounds = toggle.Bounds;
        if (SwitchTrackHeight > 0 && SwitchTrackHeight < bounds.Height)
            bounds = new SKRect(bounds.Left, bounds.MidY - SwitchTrackHeight / 2, bounds.Right, bounds.MidY + SwitchTrackHeight / 2);
        var visual = toggle.Visual;
        var radius = bounds.Height / 2;
        var on = visual.Blend(0, 1, 0.5f);
        var outline = SwitchOutlineWidth * (1 - on);
        var fill = toggle.OnTrack.WithAlpha((byte)(toggle.OnTrack.Alpha * on));
        var border = toggle.OffTrack.WithAlpha((byte)(toggle.OffTrack.Alpha * (1 - on)));
        DrawRoundedBox(canvas, bounds, radius, fill, border, outline);
        var thumbRadius = bounds.Height * (SwitchOffThumbRadiusFraction + (SwitchOnThumbRadiusFraction - SwitchOffThumbRadiusFraction) * on);
        // Pressed: the thumb grows (Material's pressed handle).
        thumbRadius += bounds.Height * 0.06f * visual.Pressed;
        var inset = radius;
        var thumbX = visual.Blend(bounds.Left + inset, bounds.Right - inset, bounds.MidX);
        canvas.DrawCircle(thumbX, bounds.MidY, thumbRadius, Paint(Mix(toggle.OffTrack, toggle.Thumb, on)));
    }

    /// <summary>
    /// Material 2's switch: a thin track of half the on color (the off track color while off) under a larger round thumb that
    /// takes the on color, with a soft shadow; the thumb's center travels between the track's rounded ends.
    /// </summary>
    private void DrawThumbOverTrackSwitch(SKCanvas canvas, SkUiSwitchPaint toggle)
    {
        var bounds = toggle.Bounds;
        var visual = toggle.Visual;
        var on = visual.Blend(0, 1, 0.5f);
        var thumbRadius = Math.Min(SwitchThumbDiameter, bounds.Height) / 2;
        var trackHeight = SwitchTrackHeight > 0 ? Math.Min(SwitchTrackHeight, bounds.Height) : bounds.Height;
        var trackHalf = Math.Min(SwitchTrackWidth, bounds.Width) / 2;
        var travelHalf = Math.Min(SwitchThumbTravel / 2, bounds.Width / 2 - thumbRadius);
        var start = bounds.MidX - travelHalf;
        var end = bounds.MidX + travelHalf;
        var track = new SKRect(bounds.MidX - trackHalf, bounds.MidY - trackHeight / 2, bounds.MidX + trackHalf, bounds.MidY + trackHeight / 2);
        var onTrack = toggle.OnTrack.WithAlpha((byte)(toggle.OnTrack.Alpha / 2));
        DrawRoundedBox(canvas, track, trackHeight / 2, Mix(toggle.OffTrack, onTrack, on), SKColors.Transparent, 0);
        var thumbX = visual.Blend(start, end, (start + end) / 2);
        var color = Mix(toggle.Thumb, toggle.OnTrack, on);
        if (visual.Pressed > 0)
            canvas.DrawCircle(thumbX, bounds.MidY, thumbRadius * 2, Paint(color.WithAlpha((byte)(31 * visual.Pressed))));
        if (toggle.IsEnabled)
        {
            // A soft drop shadow: two faint circles below the thumb.
            canvas.DrawCircle(thumbX, bounds.MidY + 1, thumbRadius + 1, Paint(new SKColor(0, 0, 0, 20)));
            canvas.DrawCircle(thumbX, bounds.MidY + 0.5f, thumbRadius + 0.5f, Paint(new SKColor(0, 0, 0, 36)));
        }
        canvas.DrawCircle(thumbX, bounds.MidY, thumbRadius, Paint(color));
    }

    /// <summary>The corner radius of a check box of <paramref name="size"/>: a rounded square, or a circle (iOS) at half the size.</summary>
    public virtual float GetCheckBoxCornerRadius(float size) => size * 0.2f;

    /// <summary>The width of a check box's border.</summary>
    public virtual float CheckBoxBorderWidth => 1.5f;

    /// <summary>The radius of a radio button's dot as a fraction of its size.</summary>
    public virtual float RadioDotRadiusFraction => 0.28f;

    /// <summary>The width of a radio button's ring as a fraction of its size.</summary>
    public virtual float RadioRingFraction => 0.08f;

    /// <summary>The thickness of a slider's track (Material 3's handle style: 16).</summary>
    public virtual float SliderTrackHeight => 4;

    /// <summary>How sliders draw: a round or capsule thumb on a track, or Material 3's bar handle between two track parts.</summary>
    public virtual SkUiSliderStyle SliderStyle => SkUiSliderStyle.Thumb;

    /// <summary>The width of a <see cref="SkUiSliderStyle.Thumb"/> slider's thumb (default its height, a circle; iOS 26's is a capsule).</summary>
    public virtual float SliderThumbWidth => 2 * SliderThumbRadius;

    /// <summary>Where a slider's track starts and ends inside its bounds, DIPs (default where the thumb's center travels; iOS: 0, the full length).</summary>
    public virtual float SliderTrackInset => SliderThumbTravelInset;

    /// <summary>The size of a <see cref="SkUiSliderStyle.Handle"/> slider's bar handle, DIPs.</summary>
    public virtual Size SliderHandleSize => new(4, 44);

    /// <summary>The gap on each side of a <see cref="SkUiSliderStyle.Handle"/> slider's handle, DIPs.</summary>
    public virtual float SliderHandleGap => 6;

    /// <summary>Whether a slider's thumb casts a soft shadow (iOS, the Mac).</summary>
    public virtual bool SliderThumbHasShadow => false;

    /// <summary>
    /// The radius of the dot of the thumb's color inside a light, outlined thumb, DIPs (default 0: a thumb filled with its
    /// color; Fluent: a 20 epx white thumb around a 5 epx dot, which shrinks while pressed).
    /// </summary>
    public virtual float SliderThumbInnerRadius => 0;

    /// <inheritdoc />
    protected override void DrawCheckBoxCore(SKCanvas canvas, SkUiCheckBoxPaint box)
    {
        var inset = CheckBoxInset;
        if (inset > 0 && box.Size > 2 * inset)
        {
            canvas.Save();
            canvas.Translate(inset, inset);
            DrawCheckBoxBox(canvas, box with { Size = box.Size - 2 * inset });
            canvas.Restore();
            return;
        }
        DrawCheckBoxBox(canvas, box);
    }

    /// <summary>The space around a check box's box inside its size, DIPs (default 0; Android: an 18 dp box in 32 dp).</summary>
    public virtual float CheckBoxInset => 0;

    /// <summary>The space around a radio button's ring inside its size, DIPs (default 0; Android: a 20 dp ring in 32 dp).</summary>
    public virtual float RadioButtonInset => 0;

    private void DrawCheckBoxBox(SKCanvas canvas, SkUiCheckBoxPaint box)
    {
        var size = box.Size;
        var visual = box.Visual;
        var on = 1 - visual.Weight(SkUiCheckState.Unchecked);
        var bounds = new SKRect(0, 0, size, size);
        DrawRoundedBox(canvas, bounds, GetCheckBoxCornerRadius(size), Mix(box.Background, box.Color, on), Mix(box.Border, box.Color, on), CheckBoxBorderWidth);
        var check = visual.Weight(SkUiCheckState.Checked);
        var dash = visual.Weight(SkUiCheckState.Indeterminate);
        if (check <= 0 && dash <= 0) return;
        var paint = Paint(SKColors.White);
        Stroke(paint, SKColors.White, size * CheckMarkStrokeFraction);
        paint.StrokeCap = SKStrokeCap.Round;
        paint.StrokeJoin = SKStrokeJoin.Round;
        var scale = CheckMarkScale;
        var save = canvas.Save();
        if (Math.Abs(scale - 1) > float.Epsilon)
        {
            // A smaller mark about the box's center, with the same stroke width.
            canvas.Scale(scale, scale, size / 2, size / 2);
            paint.StrokeWidth /= scale;
        }
        if (dash > 0)
        {
            // The dash grows from the middle.
            var half = size * 0.24f * dash;
            canvas.DrawLine(size * 0.5f - half, size * 0.5f, size * 0.5f + half, size * 0.5f, paint);
        }
        if (check > 0)
            DrawCheckMark(canvas, size, check, paint);
        canvas.RestoreToCount(save);
    }

    /// <summary>The width of a check box's check mark and dash as a fraction of the box's size (default 0.12; Fluent's thin glyph: 0.065).</summary>
    public virtual float CheckMarkStrokeFraction => 0.12f;

    /// <summary>The size of a check box's check mark and dash relative to the default's, scaled about the box's center (default 1).</summary>
    public virtual float CheckMarkScale => 1;

    /// <summary>The check mark's two strokes, drawn in up to <paramref name="amount"/> of their length (0–1).</summary>
    private static void DrawCheckMark(SKCanvas canvas, float size, float amount, SKPaint paint)
    {
        var a = new SKPoint(size * 0.22f, size * 0.55f);
        var b = new SKPoint(size * 0.42f, size * 0.75f);
        var c = new SKPoint(size * 0.8f, size * 0.28f);
        var first = SKPoint.Distance(a, b);
        var drawn = (first + SKPoint.Distance(b, c)) * Math.Clamp(amount, 0, 1);
        if (drawn <= first)
        {
            canvas.DrawLine(a, Lerp(a, b, drawn / first), paint);
            return;
        }
        using var builder = new SKPathBuilder();
        builder.MoveTo(a);
        builder.LineTo(b);
        builder.LineTo(Lerp(b, c, (drawn - first) / SKPoint.Distance(b, c)));
        using var path = builder.Detach();
        canvas.DrawPath(path, paint);
    }

    /// <inheritdoc />
    protected override void DrawRadioButtonCore(SKCanvas canvas, SkUiRadioButtonPaint radio)
    {
        var inset = RadioButtonInset;
        if (inset > 0 && radio.Size > 2 * inset)
        {
            canvas.Save();
            canvas.Translate(inset, inset);
            DrawRadioRing(canvas, radio with { Size = radio.Size - 2 * inset });
            canvas.Restore();
            return;
        }
        DrawRadioRing(canvas, radio);
    }

    /// <summary>How radio buttons draw: a ring with a dot of its color (default), or Fluent's ring that fills around a light dot.</summary>
    public virtual SkUiRadioButtonStyle RadioButtonStyle => SkUiRadioButtonStyle.Dot;

    private void DrawRadioRing(SKCanvas canvas, SkUiRadioButtonPaint radio)
    {
        var size = radio.Size;
        var visual = radio.Visual;
        var center = size / 2;
        if (RadioButtonStyle == SkUiRadioButtonStyle.Filled)
        {
            DrawFilledRadio(canvas, radio);
            return;
        }
        var paint = Paint(SKColors.Transparent);
        Stroke(paint, Mix(radio.Ring, radio.Color, 1 - visual.Weight(SkUiCheckState.Unchecked)), size * RadioRingFraction);
        canvas.DrawCircle(center, center, center - paint.StrokeWidth / 2, paint);
        // The dot (or the indeterminate bar) grows from the center.
        var dot = visual.Weight(SkUiCheckState.Checked);
        if (dot > 0)
            canvas.DrawCircle(center, center, size * RadioDotRadiusFraction * dot, Paint(radio.Color));
        var bar = visual.Weight(SkUiCheckState.Indeterminate);
        if (bar > 0)
        {
            var half = size * 0.14f;
            var length = size * 0.26f * bar;
            canvas.DrawRoundRect(new SKRect(center - length, center - half / 2, center + length, center + half / 2), half / 2, half / 2, Paint(radio.Color));
        }
    }

    /// <summary>
    /// Fluent's radio button: a thin ring (<see cref="RadioRingFraction"/>) that thickens into the checked color around a white dot
    /// of <see cref="RadioDotRadiusFraction"/>, which shrinks while pressed. Indeterminate draws the white bar instead of the dot.
    /// </summary>
    private void DrawFilledRadio(SKCanvas canvas, SkUiRadioButtonPaint radio)
    {
        var size = radio.Size;
        var visual = radio.Visual;
        var center = size / 2;
        var on = 1 - visual.Weight(SkUiCheckState.Unchecked);
        var dot = size * RadioDotRadiusFraction * (1 - 0.15f * visual.Pressed);
        var thin = size * RadioRingFraction;
        var width = thin + (center - dot - thin) * on;
        var paint = Paint(SKColors.Transparent);
        Stroke(paint, Mix(radio.Ring, radio.Color, on), width);
        canvas.DrawCircle(center, center, center - width / 2, paint);
        var light = SKColors.White.WithAlpha((byte)(radio.IsEnabled ? 255 : 160));
        var check = visual.Weight(SkUiCheckState.Checked);
        if (check > 0)
            canvas.DrawCircle(center, center, dot, Paint(light.WithAlpha((byte)(light.Alpha * check))));
        var bar = visual.Weight(SkUiCheckState.Indeterminate);
        if (bar > 0)
        {
            // The center fills with the checked color; a white bar across it.
            canvas.DrawCircle(center, center, center - width + 0.5f, Paint(radio.Color.WithAlpha((byte)(radio.Color.Alpha * bar))));
            var half = size * 0.07f;
            var length = size * 0.26f * bar;
            canvas.DrawRoundRect(new SKRect(center - length, center - half, center + length, center + half), half, half, Paint(light));
        }
    }

    /// <inheritdoc />
    protected override void DrawSliderCore(SKCanvas canvas, SkUiSliderPaint slider)
    {
        var bounds = slider.Bounds;
        var radius = SliderThumbRadius;
        var travel = SliderThumbTravelInset;
        var start = bounds.Left + travel;
        var end = Math.Max(start, bounds.Right - travel);
        var thumbX = start + (end - start) * Math.Clamp(slider.Fraction, 0, 1);
        var trackStart = bounds.Left + SliderTrackInset;
        var trackEnd = Math.Max(trackStart, bounds.Right - SliderTrackInset);
        var track = SliderTrackHeight;
        var top = bounds.MidY - track / 2;
        if (SliderStyle == SkUiSliderStyle.Handle)
        {
            DrawHandleSlider(canvas, slider, thumbX, new SKRect(trackStart, top, trackEnd, top + track));
            return;
        }
        DrawRoundedBox(canvas, new SKRect(trackStart, top, trackEnd, top + track), track / 2, slider.MaximumTrack, SKColors.Transparent, 0);
        if (thumbX > trackStart)
            DrawRoundedBox(canvas, new SKRect(trackStart, top, thumbX, top + track), track / 2, slider.MinimumTrack, SKColors.Transparent, 0);
        if (slider.HasThumbImage)
            return; // the control draws the image
        if (SliderThumbInnerRadius > 0)
        {
            // Fluent's thumb: a white disc with a faint outline around a dot of the thumb's color.
            canvas.DrawCircle(thumbX, bounds.MidY, radius, Paint(SKColors.White));
            var outline = Paint(SKColors.Transparent);
            Stroke(outline, new SKColor(0, 0, 0, 0x1A), 1);
            canvas.DrawCircle(thumbX, bounds.MidY, radius - 0.5f, outline);
            canvas.DrawCircle(thumbX, bounds.MidY, SliderThumbInnerRadius * (1 - 0.17f * slider.Pressed), Paint(slider.Thumb));
            return;
        }
        if (slider.Pressed > 0)
            canvas.DrawCircle(thumbX, bounds.MidY, radius * (1 + 0.8f * slider.Pressed),
                Paint(slider.Thumb.WithAlpha((byte)(slider.Thumb.Alpha / 5 * slider.Pressed))));
        // A circle, or a capsule as wide as SliderThumbWidth.
        var halfWidth = Math.Max(radius, SliderThumbWidth / 2);
        var thumb = new SKRect(thumbX - halfWidth, bounds.MidY - radius, thumbX + halfWidth, bounds.MidY + radius);
        if (SliderThumbHasShadow)
        {
            var shadow = thumb;
            shadow.Inflate(0.5f, 0.5f);
            shadow.Offset(0, 1);
            canvas.DrawRoundRect(shadow, radius + 0.5f, radius + 0.5f, Paint(new SKColor(0, 0, 0, 40)));
        }
        canvas.DrawRoundRect(thumb, radius, radius, Paint(slider.Thumb));
    }

    /// <summary>The inner corner radius of a <see cref="SkUiSliderStyle.Handle"/> slider's track parts at the handle, DIPs.</summary>
    private const float HandleTrackInnerRadius = 2;

    /// <summary>
    /// Material 3's slider: the active track up to a gap before a bar handle, the inactive track after a gap beyond it, and a
    /// stop dot at the end of the inactive track. The handle narrows while pressed.
    /// </summary>
    private void DrawHandleSlider(SKCanvas canvas, SkUiSliderPaint slider, float thumbX, SKRect track)
    {
        var outer = track.Height / 2;
        var handle = SliderHandleSize;
        var half = (float)handle.Width / 2;
        var gap = SliderHandleGap;
        var activeEnd = thumbX - half - gap;
        if (activeEnd > track.Left)
            DrawRoundedBox(canvas, new SKRect(track.Left, track.Top, activeEnd, track.Bottom),
                new CornerRadius(outer, HandleTrackInnerRadius, outer, HandleTrackInnerRadius), slider.MinimumTrack, SKColors.Transparent, 0);
        var inactiveStart = thumbX + half + gap;
        if (inactiveStart < track.Right)
        {
            DrawRoundedBox(canvas, new SKRect(inactiveStart, track.Top, track.Right, track.Bottom),
                new CornerRadius(HandleTrackInnerRadius, outer, HandleTrackInnerRadius, outer), slider.MaximumTrack, SKColors.Transparent, 0);
            var dot = track.Right - outer;
            if (dot - inactiveStart > outer)
                canvas.DrawCircle(dot, track.MidY, 2, Paint(slider.MinimumTrack));
        }
        if (slider.HasThumbImage)
            return; // the control draws the image
        var width = half * (1 - 0.5f * slider.Pressed);
        var height = (float)handle.Height / 2;
        canvas.DrawRoundRect(new SKRect(thumbX - width, track.MidY - height, thumbX + width, track.MidY + height), width, width, Paint(slider.Thumb));
    }

    /// <inheritdoc />
    protected override void DrawProgressBarCore(SKCanvas canvas, SkUiProgressBarPaint bar)
    {
        var bounds = bar.Bounds;
        if (ProgressBarTrackHeight > 0 && ProgressBarTrackHeight < bounds.Height)
        {
            // A thinner bar centered in a taller control (Android's 4 dp bar in 16 dp).
            var top = bounds.MidY - ProgressBarTrackHeight / 2;
            bounds = new SKRect(bounds.Left, top, bounds.Right, top + ProgressBarTrackHeight);
            bar = bar with { Bounds = bounds };
        }
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        var radius = GetProgressBarCornerRadius(bounds.Height);
        if (!bar.IsIndeterminate && ProgressBarGap > 0)
        {
            DrawGappedProgress(canvas, bar, Math.Clamp(bar.Progress, 0, 1), radius);
            return;
        }
        // The track: as thick as the bar, or a thinner line centered under the fill (Fluent).
        var trackBounds = bounds;
        if (ProgressBarTrackLineHeight > 0 && ProgressBarTrackLineHeight < bounds.Height)
        {
            var top = bounds.MidY - ProgressBarTrackLineHeight / 2;
            trackBounds = new SKRect(bounds.Left, top, bounds.Right, top + ProgressBarTrackLineHeight);
        }
        if (bar.IsIndeterminate)
        {
            // Square, unantialiased track: copies tiled one bar width apart meet without a seam (the compositor
            // clips the rounded ends).
            using var track = new SKPaint { Color = bar.Track };
            canvas.DrawRect(trackBounds, track);
        }
        else
        {
            DrawRoundedBox(canvas, trackBounds, GetProgressBarCornerRadius(trackBounds.Height), bar.Track, SKColors.Transparent, 0);
        }
        var fraction = bar.IsIndeterminate ? bar.SegmentFraction : Math.Clamp(bar.Progress, 0, 1);
        if (fraction <= 0) return;
        DrawRoundedBox(canvas, new SKRect(bounds.Left, bounds.Top, bounds.Left + bounds.Width * fraction, bounds.Bottom), radius, bar.Fill, SKColors.Transparent, 0);
    }

    /// <summary>The height of a progress bar's track, centered in the control, DIPs (default 0: the control's height; Android: 4 in 16).</summary>
    public virtual float ProgressBarTrackHeight => 0;

    /// <summary>
    /// The height of the track behind the fill, a line centered in the bar, DIPs (default 0: as thick as the bar; Fluent: a
    /// 1 epx line under the 3 epx fill).
    /// </summary>
    public virtual float ProgressBarTrackLineHeight => 0;

    /// <summary>The gap between a determinate progress bar's fill and its track, DIPs (default 0: the fill covers the track; Material 3: 4).</summary>
    public virtual float ProgressBarGap => 0;

    /// <summary>Whether a determinate progress bar with a <see cref="ProgressBarGap"/> ends in a dot of the fill color (Material 3's stop indicator).</summary>
    public virtual bool ProgressBarHasStopIndicator => false;

    /// <summary>Material 3's progress bar: the fill, a gap, the rest of the track and a stop dot at its end.</summary>
    private void DrawGappedProgress(SKCanvas canvas, SkUiProgressBarPaint bar, float fraction, float radius)
    {
        var bounds = bar.Bounds;
        var fillEnd = bounds.Left + bounds.Width * fraction;
        if (fraction > 0)
            DrawRoundedBox(canvas, new SKRect(bounds.Left, bounds.Top, fillEnd, bounds.Bottom), radius, bar.Fill, SKColors.Transparent, 0);
        var trackStart = fraction > 0 ? fillEnd + ProgressBarGap : bounds.Left;
        if (trackStart >= bounds.Right) return;
        DrawRoundedBox(canvas, new SKRect(trackStart, bounds.Top, bounds.Right, bounds.Bottom), radius, bar.Track, SKColors.Transparent, 0);
        if (ProgressBarHasStopIndicator && bounds.Right - trackStart > bounds.Height)
            canvas.DrawCircle(bounds.Right - bounds.Height / 2, bounds.MidY, bounds.Height / 2, Paint(bar.Fill));
    }

    /// <inheritdoc />
    protected override void DrawActivityIndicatorCore(SKCanvas canvas, float width, float height, float sweepStart, SKPaint paint)
    {
        if (width <= 0 || height <= 0) return;
        if (ActivityIndicatorStyle == SkUiActivityIndicatorStyle.Spokes)
        {
            DrawSpokes(canvas, width, height, paint);
            return;
        }
        // A circle centered in the slot (MAUI-like), so rotating the recorded arc about the center on the
        // render thread is equivalent to advancing sweepStart.
        var diameter = Math.Max(0, Math.Min(width, height) - 2 * ActivityIndicatorInset);
        var strokeWidth = GetActivityIndicatorStrokeWidth(diameter);
        paint.StrokeWidth = strokeWidth;
        var left = (width - diameter) / 2;
        var top = (height - diameter) / 2;
        var bounds = new SKRect(
            left + strokeWidth / 2,
            top + strokeWidth / 2,
            left + diameter - strokeWidth / 2,
            top + diameter - strokeWidth / 2);
        if (ActivityIndicatorStyle == SkUiActivityIndicatorStyle.Ring)
        {
            // Fluent's indeterminate progress ring: a short arc, no track.
            canvas.DrawArc(bounds, sweepStart, 120, false, paint);
            return;
        }
        canvas.DrawArc(bounds, sweepStart, 270, false, paint);
    }

    /// <summary>How activity indicators draw: an arc (Material), spokes that fade (iOS) or a short arc without a track (Fluent).</summary>
    public virtual SkUiActivityIndicatorStyle ActivityIndicatorStyle => SkUiActivityIndicatorStyle.Arc;

    /// <summary>The number of spokes of an <see cref="SkUiActivityIndicatorStyle.Spokes"/> indicator (iOS: 8).</summary>
    public virtual int ActivityIndicatorSpokeCount => 12;

    /// <summary>The stroke width of an arc or ring activity indicator whose circle is <paramref name="diameter"/> across.</summary>
    public virtual float GetActivityIndicatorStrokeWidth(float diameter) => Math.Max(2, diameter * 0.1f);

    /// <summary>The gap between an arc or ring activity indicator's circle and its bounds, DIPs (Material: 4).</summary>
    public virtual float ActivityIndicatorInset => 0;

    private void DrawSpokes(SKCanvas canvas, float width, float height, SKPaint paint)
    {
        var spokeCount = Math.Max(1, ActivityIndicatorSpokeCount);
        var diameter = Math.Min(width, height);
        var outer = diameter / 2;
        var inner = outer * 0.55f;
        paint.StrokeWidth = Math.Max(1.5f, diameter * 0.09f);
        var color = paint.Color;
        for (var spoke = 0; spoke < spokeCount; spoke++)
        {
            // The first spoke is the brightest; the following ones fade (the render thread rotates the picture).
            paint.Color = color.WithAlpha((byte)(color.Alpha * (1 - 0.8f * spoke / spokeCount)));
            var angle = -Math.PI / 2 + spoke * 2 * Math.PI / spokeCount;
            var cos = (float)Math.Cos(angle);
            var sin = (float)Math.Sin(angle);
            canvas.DrawLine(width / 2 + cos * inner, height / 2 + sin * inner, width / 2 + cos * outer, height / 2 + sin * outer, paint);
        }
        paint.Color = color;
    }

    /// <inheritdoc />
    protected override void DrawImageCore(SKCanvas canvas, SKImage image, float viewWidth, float viewHeight, Aspect aspect)
    {
        var destination = ComputeImageDestination(viewWidth, viewHeight, image.Width, image.Height, aspect);
        canvas.DrawImage(image, destination, new SKSamplingOptions(SKFilterMode.Linear));
    }

    /// <inheritdoc />
    /// <remarks>A rounded bar, fully rounded across its thickness; darker while dragged.</remarks>
    protected override void DrawScrollBarCore(SKCanvas canvas, SkUiScrollBarPaint bar)
    {
        var radius = Math.Min(bar.Bounds.Width, bar.Bounds.Height) / 2;
        var color = bar.IsPressed ? bar.Color.WithAlpha((byte)Math.Min(255, bar.Color.Alpha * 1.6f)) : bar.Color;
        canvas.DrawRoundRect(bar.Bounds, radius, radius, Paint(color));
    }

    /// <inheritdoc />
    /// <remarks>A rounded strip in the thumb's color at a quarter of its opacity.</remarks>
    protected override void DrawScrollBarTrackCore(SKCanvas canvas, SkUiScrollBarTrackPaint track)
    {
        var radius = Math.Min(track.Bounds.Width, track.Bounds.Height) / 2;
        canvas.DrawRoundRect(track.Bounds, radius, radius, Paint(track.Color.WithAlpha((byte)(track.Color.Alpha / 4))));
    }

    /// <inheritdoc />
    protected override void DrawPressOverlayCore(SKCanvas canvas, SkUiPressOverlayPaint overlay)
    {
        // Over arbitrary content (images, cards): a dark tint or a dark ripple shows on light and mid-tone content alike.
        var alpha = !overlay.IsEnabled ? 96 : EffectivePressEffect == SkUiPressEffect.Dim ? 48 * overlay.Press.Pressed : 0;
        if (alpha > 0)
        {
            var paint = Paint(new SKColor(0, 0, 0, (byte)alpha));
            if (IsUniformCornerRadius(overlay.CornerRadii) && overlay.CornerRadii.TopLeft <= 0)
            {
                canvas.DrawRect(overlay.Bounds, paint);
            }
            else
            {
                using var path = CreateRoundRectPath(overlay.Bounds, overlay.CornerRadii);
                canvas.DrawPath(path, paint);
            }
        }
        if (EffectivePressEffect == SkUiPressEffect.Ripple && overlay.IsEnabled)
            DrawRipple(canvas, overlay.Bounds, overlay.CornerRadii, overlay.Press, new SKColor(0, 0, 0, 56));
    }

    /// <summary>How focus rings draw: two lines that show on any fill (Fluent) or one soft thick line, a halo (the Mac).</summary>
    public virtual SkUiFocusRingStyle FocusRingStyle => SkUiFocusRingStyle.Double;

    /// <summary>Thickness of the default focus ring's outer line, DIPs.</summary>
    public virtual float FocusRingThickness => 2;

    /// <inheritdoc />
    /// <remarks>
    /// Two lines inside the bounds, following the control's corners: the foreground color outside and a one-DIP line of the
    /// background color inside it, so the ring shows on light, dark and accent fills alike (as Windows' focus visuals).
    /// </remarks>
    protected override void DrawFocusRingCore(SKCanvas canvas, SkUiFocusRingPaint ring)
    {
        var width = FocusRingThickness;
        var bounds = ring.Bounds;
        if (bounds.Width <= 2 * width || bounds.Height <= 2 * width)
            return;
        var paint = Paint(ring.Color);
        if (FocusRingStyle == SkUiFocusRingStyle.Halo)
        {
            // The Mac's halo: one soft, thick line.
            DrawRingLine(canvas, paint, bounds, ring.CornerRadii, width, 0, ring.Color.WithAlpha((byte)(ring.Color.Alpha * 0.65f)));
            return;
        }
        DrawRingLine(canvas, paint, bounds, ring.CornerRadii, width, 0, ring.Color);
        DrawRingLine(canvas, paint, bounds, ring.CornerRadii, 1, width, ring.Contrast);
    }

    private void DrawRingLine(SKCanvas canvas, SKPaint paint, SKRect bounds, CornerRadius radii, float width, float inset, SKColor color)
    {
        var outset = inset + width / 2;
        bounds.Inflate(-outset, -outset);
        Stroke(paint, color, width);
        var shrunk = new CornerRadius(Math.Max(0, radii.TopLeft - outset), Math.Max(0, radii.TopRight - outset),
            Math.Max(0, radii.BottomLeft - outset), Math.Max(0, radii.BottomRight - outset));
        if (IsUniformCornerRadius(shrunk))
        {
            canvas.DrawRoundRect(bounds, (float)shrunk.TopLeft, (float)shrunk.TopLeft, paint);
            return;
        }
        using var path = CreateRoundRectPath(bounds, shrunk);
        canvas.DrawPath(path, paint);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <see cref="PickerFieldStyle"/>'s chrome: idle in the border color (darker while a pointer is over it), in the accent
    /// color and thicker while focused or open; a light press overlay; a chevron that turns while the popup opens; dimmed
    /// when disabled.
    /// </remarks>
    protected override void DrawPickerFieldCore(SKCanvas canvas, SkUiPickerFieldPaint field)
    {
        var bounds = field.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;
        var style = PickerFieldStyle == SkUiPickerFieldStyle.Default ? DefaultPickerFieldStyle : PickerFieldStyle;
        DrawFieldFrame(canvas, bounds, style, field.Accent, field.Border, field.Foreground, field.Focused, field.IsPointerOver, field.IsEnabled);
        var radius = FieldCornerRadius;
        if (field.IsEnabled && field.Press.Pressed > 0)
        {
            var press = Paint(field.Foreground.WithAlpha((byte)(18 * field.Press.Pressed)));
            if (style is SkUiPickerFieldStyle.Outline or SkUiPickerFieldStyle.PopUpButton or SkUiPickerFieldStyle.ComboBox)
                canvas.DrawRoundRect(bounds, radius, radius, press);
            else
                canvas.DrawRect(bounds, press);
        }
        DrawChevron(canvas, field, style);
    }

    /// <summary>The opacity of disabled field chrome.</summary>
    private const float DisabledAlpha = 0.38f;

    /// <summary>The corner radius of outlined, pop-up button, combo box and filled fields and the pressed overlay, DIPs (default 6).</summary>
    public virtual float FieldCornerRadius => 6;

    /// <summary>
    /// A field's frame (pickers and text fields): idle in the border color (darker while a pointer is over it), in the accent
    /// color and thicker while focused; in <paramref name="error"/> when set (a text field's error), thicker while focused; dimmed
    /// when disabled.
    /// </summary>
    private void DrawFieldFrame(SKCanvas canvas, SKRect bounds, SkUiPickerFieldStyle style, SKColor accent, SKColor border, SKColor foreground,
        float focused, bool pointerOver, bool enabled, SKColor? error = null, bool textBox = false)
    {
        var focus = enabled ? Math.Clamp(focused, 0, 1) : 0;
        if (style == SkUiPickerFieldStyle.ComboBox)
        {
            DrawFluentField(canvas, bounds, accent, foreground, focus, pointerOver, enabled, error, textBox);
            return;
        }
        var idle = pointerOver && enabled ? Mix(border, foreground, 0.35f) : border;
        var line = error ?? Mix(idle, accent, focus);
        if (!enabled)
            line = line.WithAlpha((byte)(line.Alpha * DisabledAlpha));
        var paint = Paint(line);
        var radius = FieldCornerRadius;
        switch (style)
        {
            case SkUiPickerFieldStyle.Outline or SkUiPickerFieldStyle.PopUpButton:
            {
                if (style == SkUiPickerFieldStyle.PopUpButton)
                {
                    // A control-colored fill: the pop-up button's.
                    var tint = Mix(border, foreground, pointerOver && enabled ? 0.2f : 0);
                    canvas.DrawRoundRect(bounds, radius, radius, Paint(tint.WithAlpha((byte)(enabled ? 28 : 14))));
                }
                var width = 1 + focus;
                var ring = bounds;
                ring.Inflate(-width / 2, -width / 2);
                Stroke(paint, line, width);
                canvas.DrawRoundRect(ring, radius - width / 2, radius - width / 2, paint);
                break;
            }
            case SkUiPickerFieldStyle.Underline or SkUiPickerFieldStyle.Filled:
            {
                if (style == SkUiPickerFieldStyle.Filled)
                {
                    var tint = Mix(border, foreground, pointerOver && enabled ? 0.2f : 0);
                    paint.Color = tint.WithAlpha((byte)(enabled ? 40 : 20));
                    using var fill = new SKRoundRect(bounds, radius);
                    fill.SetRectRadii(bounds, [new SKPoint(radius, radius), new SKPoint(radius, radius), default, default]);
                    canvas.DrawRoundRect(fill, paint);
                    paint.Color = line;
                }
                var height = 1 + focus;
                canvas.DrawRect(new SKRect(bounds.Left, bounds.Bottom - height, bounds.Right, bounds.Bottom), paint);
                break;
            }
        }
    }

    /// <summary>
    /// Fluent's combo box and text box: a light, rounded fill (lighter under a pointer, opaque in a focused text box), a faint
    /// outline whose bottom edge is darker (a combo box's elevation border, a text box's strong line), and a 2-DIP accent line
    /// along the bottom while focused. Fluent's control fill and stroke colors: black or white (on dark text) at fixed opacities.
    /// </summary>
    private void DrawFluentField(SKCanvas canvas, SKRect bounds, SKColor accent, SKColor foreground, float focus, bool pointerOver,
        bool enabled, SKColor? error, bool textBox)
    {
        var dark = IsLight(foreground);
        var dim = enabled ? 1 : DisabledAlpha;
        var ink = dark ? SKColors.White : SKColors.Black;
        SKColor Shade(SKColor color, int alpha) => color.WithAlpha((byte)(alpha * dim));
        var radius = FieldCornerRadius;
        // ControlFillColorDefault, Secondary under a pointer, InputActive while a text box is focused.
        var idle = dark ? Shade(SKColors.White, pointerOver ? 0x15 : 0x0F) : Shade(pointerOver ? new SKColor(0xF9, 0xF9, 0xF9) : SKColors.White, pointerOver ? 0x80 : 0xB3);
        var fill = textBox ? Mix(idle, dark ? Shade(new SKColor(0x1E, 0x1E, 0x1E), 0xB3) : Shade(SKColors.White, 0xFF), focus) : idle;
        canvas.DrawRoundRect(bounds, radius, radius, Paint(fill));
        var ring = bounds;
        ring.Inflate(-0.5f, -0.5f);
        var paint = Paint(SKColors.Transparent);
        Stroke(paint, error is { } outline ? Shade(outline, outline.Alpha) : Shade(ink, dark ? 0x12 : 0x0F), 1);
        canvas.DrawRoundRect(ring, radius - 0.5f, radius - 0.5f, paint);
        if (error is null)
        {
            // The bottom edge, corners included: ControlStrongStrokeColorDefault (text box) or the elevation border's darker end.
            var bottom = textBox ? Shade(ink, dark ? 0x8B : 0x72) : Shade(ink, dark ? 0x18 : 0x29);
            canvas.Save();
            canvas.ClipRect(new SKRect(bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom));
            Stroke(paint, bottom, 1);
            canvas.DrawRoundRect(ring, radius - 0.5f, radius - 0.5f, paint);
            canvas.Restore();
        }
        if (focus <= 0)
            return;
        // Focused: an accent line along the bottom, inside the rounded corners.
        canvas.Save();
        using (var shape = new SKRoundRect(bounds, radius))
            canvas.ClipRoundRect(shape, antialias: true);
        var line = error ?? accent;
        canvas.DrawRect(new SKRect(bounds.Left, bounds.Bottom - 2, bounds.Right, bounds.Bottom), Paint(line.WithAlpha((byte)(line.Alpha * focus))));
        canvas.Restore();
    }

    /// <summary>Whether <paramref name="color"/> is light (light text: a dark theme).</summary>
    private static bool IsLight(SKColor color) => 0.299f * color.Red + 0.587f * color.Green + 0.114f * color.Blue > 128;

    /// <summary>
    /// How text fields draw (default <see cref="SkUiTextFieldStyle.Default"/>: the look's, <see cref="DefaultTextFieldStyle"/>). Call
    /// <see cref="SkUiLook.NotifyChanged"/> after changing it.
    /// </summary>
    public SkUiTextFieldStyle TextFieldStyle { get; set; }

    /// <summary>
    /// The text field a <see cref="SkUiTextFieldStyle.Default"/> <see cref="TextFieldStyle"/> draws: an outline, the same on every
    /// platform. Platform looks draw their platform's (<see cref="AndroidSkUiLook"/>: Material's underline).
    /// </summary>
    public virtual SkUiTextFieldStyle DefaultTextFieldStyle => SkUiTextFieldStyle.Outline;

    /// <inheritdoc />
    /// <remarks>
    /// <see cref="TextFieldStyle"/>'s chrome, as picker fields': idle in the border color (darker while a pointer is over it), in the
    /// accent color and thicker while focused; in the error color with an error (thicker while focused); dimmed when disabled.
    /// </remarks>
    protected override void DrawTextFieldCore(SKCanvas canvas, SkUiTextFieldPaint field)
    {
        var bounds = field.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;
        var textFieldStyle = TextFieldStyle == SkUiTextFieldStyle.Default ? DefaultTextFieldStyle : TextFieldStyle;
        var style = textFieldStyle switch
        {
            SkUiTextFieldStyle.Underline => SkUiPickerFieldStyle.Underline,
            SkUiTextFieldStyle.Filled => SkUiPickerFieldStyle.Filled,
            SkUiTextFieldStyle.None => SkUiPickerFieldStyle.None,
            // Fluent's text box is drawn as its combo box, with a stronger bottom line.
            SkUiTextFieldStyle.TextBox => SkUiPickerFieldStyle.ComboBox,
            _ => SkUiPickerFieldStyle.Outline
        };
        DrawFieldFrame(canvas, bounds, style, field.Accent, field.Border, field.Foreground, field.Focused, field.IsPointerOver, field.IsEnabled,
            field.HasError ? field.Error : null, textFieldStyle == SkUiTextFieldStyle.TextBox);
    }

    /// <summary>A down-pointing chevron centered in the field's chevron bounds, turned upwards as the popup opens.</summary>
    private static void DrawChevron(SKCanvas canvas, SkUiPickerFieldPaint field, SkUiPickerFieldStyle style)
    {
        var area = field.ChevronBounds;
        if (area.Width <= 0 || area.Height <= 0)
            return;
        var color = field.IsEnabled ? field.Foreground.WithAlpha((byte)(field.Foreground.Alpha * 0.7f)) : field.Foreground.WithAlpha((byte)(field.Foreground.Alpha * DisabledAlpha));
        var paint = Paint(color);
        Stroke(paint, color, 1.75f);
        paint.StrokeCap = SKStrokeCap.Round;
        paint.StrokeJoin = SKStrokeJoin.Round;
        var half = Math.Min(5f, Math.Min(area.Width, area.Height) / 3);
        if (style == SkUiPickerFieldStyle.ComboBox)
        {
            // Fluent's chevron glyph: 8 DIPs wide, a thin stroke.
            half = Math.Min(4f, half);
            paint.StrokeWidth = 1;
        }
        if (style == SkUiPickerFieldStyle.PopUpButton)
        {
            // The Mac's pop-up button: a chevron up and a chevron down.
            half *= 0.6f;
            canvas.DrawLine(area.MidX - half, area.MidY - half * 0.5f - 2, area.MidX, area.MidY - half * 1.5f - 2, paint);
            canvas.DrawLine(area.MidX, area.MidY - half * 1.5f - 2, area.MidX + half, area.MidY - half * 0.5f - 2, paint);
            canvas.DrawLine(area.MidX - half, area.MidY + half * 0.5f + 2, area.MidX, area.MidY + half * 1.5f + 2, paint);
            canvas.DrawLine(area.MidX, area.MidY + half * 1.5f + 2, area.MidX + half, area.MidY + half * 0.5f + 2, paint);
            return;
        }
        canvas.Save();
        canvas.Translate(area.MidX, area.MidY);
        canvas.RotateDegrees(180 * Math.Clamp(field.Open, 0, 1));
        canvas.DrawLine(-half, -half / 2, 0, half / 2, paint);
        canvas.DrawLine(0, half / 2, half, -half / 2, paint);
        canvas.Restore();
    }

    /// <summary>
    /// A Material-like ripple: a circle spreading from the press point to the farthest corner, fading after the
    /// release, over a light press overlay; clipped to the control's rounded shape.
    /// </summary>
    protected void DrawRipple(SKCanvas canvas, SKRect bounds, CornerRadius radii, SkUiPressVisual press, SKColor color)
    {
        if (!press.HasRipple && press.Pressed <= 0) return;
        var save = canvas.Save();
        using (var clip = CreateRoundRectPath(bounds, radii))
            canvas.ClipPath(clip, antialias: true);
        if (press.Pressed > 0)
            canvas.DrawRect(bounds, Paint(color.WithAlpha((byte)(color.Alpha * 0.4f * press.Pressed))));
        if (press.HasRipple)
        {
            var origin = press.Origin;
            var reach = Math.Max(
                Math.Max(SKPoint.Distance(origin, new SKPoint(bounds.Left, bounds.Top)), SKPoint.Distance(origin, new SKPoint(bounds.Right, bounds.Top))),
                Math.Max(SKPoint.Distance(origin, new SKPoint(bounds.Left, bounds.Bottom)), SKPoint.Distance(origin, new SKPoint(bounds.Right, bounds.Bottom))));
            var radius = reach * (0.1f + 0.9f * press.Ripple);
            canvas.DrawCircle(origin, radius, Paint(color.WithAlpha((byte)(color.Alpha * (1 - press.RippleFade)))));
        }
        canvas.RestoreToCount(save);
    }

    /// <summary>Ripple color over <paramref name="fill"/>: dark over light fills, light over dark ones.</summary>
    private static SKColor RippleColor(SKColor fill) =>
        0.299f * fill.Red + 0.587f * fill.Green + 0.114f * fill.Blue > 160 && fill.Alpha > 64
            ? new SKColor(0, 0, 0, 60)
            : new SKColor(255, 255, 255, 90);

    private static SKPoint Lerp(SKPoint from, SKPoint to, float amount) =>
        new(from.X + (to.X - from.X) * amount, from.Y + (to.Y - from.Y) * amount);

    /// <summary>Blends two colors (straight alpha): 0 gives <paramref name="from"/>, 1 <paramref name="to"/>.</summary>
    protected static SKColor Mix(SKColor from, SKColor to, float amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        if (amount <= 0) return from;
        if (amount >= 1) return to;
        static byte Channel(byte a, byte b, float t) => (byte)Math.Round(a + (b - a) * t);
        return new SKColor(Channel(from.Red, to.Red, amount), Channel(from.Green, to.Green, amount),
            Channel(from.Blue, to.Blue, amount), Channel(from.Alpha, to.Alpha, amount));
    }
}
