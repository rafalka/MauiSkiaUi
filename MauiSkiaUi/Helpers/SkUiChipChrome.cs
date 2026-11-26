using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// The chrome both chip layers share (<see cref="SkUiChip"/>, <see cref="Core.SkUiCoreChip"/>): the room the check, badge and
/// remove button take inside the padding, where they sit, the colors, and drawing them with the look (<see cref="SkUiLook.DrawChip"/>)
/// and the badge's text. One per chip: it keeps the badge's text layout.
/// </summary>
internal sealed class SkUiChipChrome(object owner)
{
    private SkUiTextLayout? _badgeLayout;
    private string? _measuredBadge;
    private BadgeFont _measuredFont;
    private bool _measuredScaled;
    private float _measuredLook;
    private double _measuredLookFontSize;
    private int _measuredScaleVersion = -1;
    private double _measuredFontScale;
    private SKSize _badgeSize;

    /// <summary>The badge's font: a family (<c>null</c>: the default), a size (-1: the look's <see cref="SkUiLook.ChipBadgeFontSize"/>) and attributes.</summary>
    public readonly record struct BadgeFont(string? FontFamily, double FontSize, FontAttributes Attributes);

    /// <summary>What the chip's chrome depends on.</summary>
    public readonly record struct State(SkUiChipKind Kind, bool IsToggle, bool IsSelected, bool HasImage, bool ShowsRemove, string? Badge,
        bool RightToLeft, bool FontAutoScaling, BadgeFont Font);

    /// <summary>The parts' rectangles in a chip of a size (empty when a part is not shown); <see cref="RemoveHit"/> is where a tap removes.</summary>
    public readonly record struct Parts(SKRect Check, SKRect Badge, SKRect Remove, SKRect RemoveHit);

    /// <summary>Whether <paramref name="size"/> is a badge font size: finite and positive, or -1 (the look's).</summary>
    public static bool IsBadgeFontSize(double size) => size == -1 || double.IsFinite(size) && size > 0; // exact: -1 is a sentinel

    /// <summary>Throws unless <paramref name="value"/> is a badge font size.</summary>
    public static void ThrowIfNotBadgeFontSize(double value, string name)
    {
        if (!IsBadgeFontSize(value))
            throw new ArgumentOutOfRangeException(name, value, "Must be a finite, positive size, or -1 for the look's.");
    }

    private static SkUiTextStyle BadgeStyle(BadgeFont font, bool scaled)
    {
        var size = font.FontSize < 0 ? SkUiLook.Current.ChipBadgeFontSize : font.FontSize;
        return new SkUiTextStyle(SkUiTypefaces.Resolve(font.FontFamily, font.Attributes), SkUiFontScaling.ScaleFontSize(size, scaled), LineBreakMode.NoWrap,
            null, 1, -1, 0, SkUiTextDirection.Auto, SkUiTextRendering.Default, font.Attributes);
    }

    /// <summary>The badge's pill for <paramref name="text"/>, or empty without one.</summary>
    private SKSize BadgeSize(string? text, BadgeFont font, bool scaled)
    {
        if (string.IsNullOrEmpty(text))
            return SKSize.Empty;
        var look = SkUiLook.Current;
        var icon = look.ChipIconSize;
        // Exact comparisons: cache keys.
        // The system text size and the app look (Version), and the look measuring now (a subtree's FontScale).
        var scaleVersion = SkUiFontScaling.Version;
        if (text != _measuredBadge || font != _measuredFont || scaled != _measuredScaled || icon != _measuredLook || look.ChipBadgeFontSize != _measuredLookFontSize
            || scaleVersion != _measuredScaleVersion || look.FontScale != _measuredFontScale)
        {
            // The layout keeps its lines by style and width, not by text: a new text is broken again.
            var layout = _badgeLayout ??= new SkUiTextLayout(owner);
            layout.Invalidate();
            var size = layout.Measure(text, BadgeStyle(font, scaled), default, double.PositiveInfinity);
            var height = Math.Max(icon, (float)Math.Ceiling(size.Height) + 2);
            // A pill: the text with half its height of room on each side, at least a circle.
            _badgeSize = new SKSize(Math.Max(height, (float)Math.Ceiling(size.Width) + height * 0.6f), height);
            (_measuredBadge, _measuredFont, _measuredScaled, _measuredLook, _measuredLookFontSize, _measuredScaleVersion, _measuredFontScale) =
                (text, font, scaled, icon, look.ChipBadgeFontSize, scaleVersion, look.FontScale);
        }
        return _badgeSize;
    }

    /// <summary>The room the chrome takes inside the padding: the check at the start, the badge and the × at the end (mirrored right to left).</summary>
    public Thickness Inset(in State state)
    {
        var look = SkUiLook.Current;
        var icon = look.ChipIconSize;
        var gap = look.ChipIconSpacing;
        var start = ShowsCheck(state) ? icon + gap : 0;
        var end = 0f;
        var badge = BadgeSize(state.Badge, state.Font, state.FontAutoScaling);
        if (badge.Width > 0)
            end += gap + badge.Width;
        if (state.ShowsRemove)
            end += gap + icon;
        if (start == 0 && end == 0)
            return default;
        return state.RightToLeft ? new Thickness(end, 0, start, 0) : new Thickness(start, 0, end, 0);
    }

    /// <summary>A selected toggle chip without an image shows a check where the image would be.</summary>
    private static bool ShowsCheck(in State state) => state is { IsToggle: true, IsSelected: true, HasImage: false };

    /// <summary>Where the parts go in a chip of <paramref name="width"/> × <paramref name="height"/> with <paramref name="padding"/> (the chip's own).</summary>
    public Parts Layout(in State state, float width, float height, Thickness padding)
    {
        var look = SkUiLook.Current;
        var icon = look.ChipIconSize;
        var gap = look.ChipIconSpacing;
        var midY = height / 2;
        // Left to right; mirrored below.
        var start = (float)(state.RightToLeft ? padding.Right : padding.Left);
        var endEdge = width - (float)(state.RightToLeft ? padding.Left : padding.Right);
        var check = ShowsCheck(state) ? new SKRect(start, midY - icon / 2, start + icon, midY + icon / 2) : SKRect.Empty;
        var remove = SKRect.Empty;
        var hit = SKRect.Empty;
        var x = endEdge;
        if (state.ShowsRemove)
        {
            remove = new SKRect(x - icon, midY - icon / 2, x, midY + icon / 2);
            hit = new SKRect(x - icon - gap, 0, width, height);
            x -= icon + gap;
        }
        var badgeSize = BadgeSize(state.Badge, state.Font, state.FontAutoScaling);
        var badge = badgeSize.Width > 0 ? new SKRect(x - badgeSize.Width, midY - badgeSize.Height / 2, x, midY + badgeSize.Height / 2) : SKRect.Empty;
        if (state.RightToLeft)
        {
            check = Mirror(check, width);
            remove = Mirror(remove, width);
            hit = Mirror(hit, width);
            badge = Mirror(badge, width);
        }
        return new Parts(check, badge, remove, hit);
    }

    private static SKRect Mirror(SKRect rect, float width) => rect.IsEmpty ? rect : new SKRect(width - rect.Right, rect.Top, width - rect.Left, rect.Bottom);

    /// <summary>Draws the chrome and the badge's text.</summary>
    public void Draw(SKCanvas canvas, in State state, float width, float height, Thickness padding, CornerRadius radii, Color? background,
        Color? selectedColor, Color? strokeColor, Color foreground, Color? badgeColor, Color? badgeTextColor, SkUiPressVisual press, Point pressPosition,
        bool pressed, bool enabled)
    {
        var look = SkUiLook.Current;
        var parts = Layout(state, width, height, padding);
        var fill = state.IsSelected ? selectedColor ?? SkUiColors.Accent.WithAlpha(0.2f) : background ?? Colors.Transparent;
        var stroke = strokeColor ?? (state.IsSelected ? Colors.Transparent : SkUiColors.Muted.WithAlpha(0.5f));
        var removePressed = pressed && !parts.RemoveHit.IsEmpty && parts.RemoveHit.Contains((float)pressPosition.X, (float)pressPosition.Y);
        look.DrawChip(canvas, new SkUiChipPaint(new SKRect(0, 0, width, height), radii, state.Kind, state.IsSelected, Sk(fill, enabled),
            Sk(stroke, enabled), look.ChipStrokeWidth, Sk(foreground, enabled), removePressed ? SkUiPressVisual.None : press, parts.Check,
            parts.Badge, Sk(badgeColor ?? SkUiColors.ButtonBackground, enabled), parts.Remove, removePressed, enabled));
        if (parts.Badge.IsEmpty || _badgeLayout is not { } layout || state.Badge is not { } text)
            return;
        var paint = SkUiTextResources.TextPaint;
        paint.Color = Sk(badgeTextColor ?? SkUiColors.ButtonForeground, enabled);
        var save = canvas.Save();
        canvas.Translate(parts.Badge.Left, parts.Badge.Top);
        layout.Draw(canvas, text, BadgeStyle(state.Font, state.FontAutoScaling), default, parts.Badge.Width, parts.Badge.Height, TextAlignment.Center, TextAlignment.Center, paint);
        canvas.RestoreToCount(save);
    }

    private static SKColor Sk(Color color, bool enabled) => SkUiToggleDrawing.ToSkColor(SkUiColors.EnabledTint(color, enabled));

    /// <summary>Releases the badge's shaped text (the chip left its drawn parent).</summary>
    public void Release() => _badgeLayout?.Release();

    /// <summary>Fonts or scaling changed: the badge is measured again.</summary>
    public void Invalidate()
    {
        _badgeLayout?.Invalidate();
        _measuredBadge = null;
    }
}
