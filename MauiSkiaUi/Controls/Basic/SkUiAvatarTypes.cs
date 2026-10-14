using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>Whether an avatar shows its <c>Text</c> as it is or initials made from it.</summary>
public enum SkUiAvatarInitials
{
    /// <summary>The text as it is (with its <c>TextTransform</c>), as the Community Toolkit's avatar.</summary>
    None,

    /// <summary>The first letter of the first word ("Ada Lovelace": "A").</summary>
    FirstWord,

    /// <summary>The first letters of the first and the last word ("Ada Lovelace": "AL"); one word gives one letter.</summary>
    FirstAndLastWord
}

/// <summary>Where an avatar's colors come from when its background is not set.</summary>
public enum SkUiAvatarColorMode
{
    /// <summary>The look's default background and text colors, the same for every avatar.</summary>
    Default,

    /// <summary>A light background with dark text, picked from the look's palette by the avatar's text.</summary>
    LightBackground,

    /// <summary>A dark background with light text, picked from the look's palette by the avatar's text.</summary>
    DarkBackground
}

/// <summary>The corner of an avatar its badge sits on.</summary>
public enum SkUiAvatarBadgePosition
{
    /// <summary>The bottom right corner (default).</summary>
    BottomRight,

    /// <summary>The top right corner.</summary>
    TopRight,

    /// <summary>The bottom left corner.</summary>
    BottomLeft,

    /// <summary>The top left corner.</summary>
    TopLeft
}

/// <summary>An avatar's background and text colors (<see cref="SkUiLook.GetAvatarColors"/>).</summary>
/// <param name="Background">Fills the avatar's shape when its background is not set.</param>
/// <param name="Text">The text's color when <c>TextColor</c> is not set.</param>
public readonly record struct SkUiAvatarColors(Color Background, Color Text);

/// <summary>
/// What is drawn for an avatar's badge, over its border, in the avatar's local DIPs: by the avatar's <c>BadgePainter</c>
/// or <c>OnPaintBadge</c>, else by the look (<see cref="SkUiLook.DrawAvatarBadge"/>). A painter may draw anywhere (a pill
/// with a count growing from <see cref="Bounds"/>, an icon), not only in <see cref="Bounds"/>.
/// </summary>
/// <param name="Avatar">The avatar's bounds.</param>
/// <param name="Position">The corner the badge sits on.</param>
/// <param name="Bounds">The look's square for a dot at that corner, ring included (<see cref="SkUiLook.GetAvatarBadgeBounds"/>).</param>
/// <param name="Color">The avatar's <c>BadgeColor</c> (transparent when only a painter draws the badge).</param>
/// <param name="Ring">The ring around the badge that separates it from the avatar (<c>BadgeBorderColor</c>, else the scheme's background).</param>
/// <param name="RingWidth">The look's ring width in DIPs for a badge of that size.</param>
public readonly record struct SkUiAvatarBadgePaint(SKRect Avatar, SkUiAvatarBadgePosition Position, SKRect Bounds, SKColor Color, SKColor Ring, float RingWidth);
