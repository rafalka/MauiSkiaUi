# SkUiAvatarView

A person's picture, or their initials while there is none, in a round (or any) border, with an optional status badge. `SkUiCoreAvatar` is the Core twin and draws the same pixels.

**Counterpart:** Community Toolkit [`AvatarView`](https://learn.microsoft.com/dotnet/communitytoolkit/maui/views/avatarview). The toolkit's XAML ports by changing the prefix. The control is in the main package (`SkiaUi.Maui`) and does not need the Community Toolkit.

## How it works

`SkUiAvatarView` derives from [`SkUiBorder`](SkUiBorder.md) (`SkUiCoreAvatar` from `SkUiCoreBorder`), as the toolkit's view derives from MAUI's `Border`. It has no child views. The avatar is one drawn node that draws its own content:

- **The image, once it has loaded.** `ImageSource` loads through the shared loader and cache ([SkUiImage.md](SkUiImage.md)). The image is decoded just large enough to cover the content area (inside the padding and the border). Avatars of one size share their cache entries, so a list of repeated faces decodes each face once. An avatar later arranged larger decodes the image again for the larger size and keeps showing the smaller one meanwhile. The image is drawn with `Aspect` (default `AspectFill`, the toolkit's).
- **Otherwise the text:** `Text` (default `"?"`) as it is with `TextTransform`, or its initials when `Initials` is set and it has a letter or digit. The text shows while the image loads and when it fails. It is drawn by the shared text engine (shaping, fallback fonts, emoji), centered.
- **Clip:** the image and the text are clipped to the inside of the stroke (`StrokeShape`, or the rounded rectangle). The stroke is drawn over them.
- **Recycled list rows:** a new `Text` or `ImageSource` re-records only this node. Neither the text nor a loaded image changes the avatar's size, so the list is not laid out again. The exception is text in a set `FontSize`, whose size the avatar measures.

### The toolkit's defaults

| | Toolkit `AvatarView` | `SkUiAvatarView` |
| --- | --- | --- |
| Size | `WidthRequest` / `HeightRequest` 48 set by the constructor | 48 × 48 as the intrinsic size (`SkUiLook.DefaultAvatarSize`), so sizes from styles apply |
| Alignment | `Center` both ways | The same (set by the constructor, as the toolkit's) |
| Border | `BorderColor` white, `BorderWidth` 1, mapped to `Stroke` / `StrokeThickness` | The same. `Stroke` and `StrokeThickness` win over them once set (also from a style) |
| Corners | `CornerRadius` 24 | Fully rounded (a circle at any size) until `CornerRadius` is set. At 48 × 48 it is the same circle |
| Padding | 1 | 1 until `Padding` is set |
| Background | Transparent | The look's color for `AvatarColorMode` until `Background` / `BackgroundColor` is set (Transparent included) |
| Text color | The platform's label color | The look's color for `AvatarColorMode` until `TextColor` is set |
| Font size | -1: the platform's default label size | -1: sized to the avatar (40 % of the content area's smaller side, `SkUiLook.GetAvatarFontSize`), and text too wide shrinks to fit (down to a quarter). A set size follows `FontAutoScalingEnabled` |

Unlike the toolkit's constructor, which sets `Padding`, `Stroke` and the sizes as local values that style setters cannot replace, the avatar sets only the alignment locally. Everything else comes from defaults, so implicit and explicit styles work.

## How to use

```xml
<!-- The toolkit's samples, prefix changed -->
<sk:SkUiAvatarView Text="ZS" />
<sk:SkUiAvatarView BackgroundColor="Red" BorderColor="White" BorderWidth="5" CornerRadius="8"
                   ImageSource="dotnet_bot.png" Text="ZS" TextColor="White" />

<!-- A contact row: initials of the name, colors by the name, a status badge -->
<sk:SkUiAvatarView Text="{Binding Name}" Initials="FirstAndLastWord" ImageSource="{Binding Photo}"
                   AvatarColorMode="LightBackground" BadgeColor="{Binding StatusColor}"
                   WidthRequest="40" HeightRequest="40" />
```

```csharp
var core = new SkUiCoreAvatar()
    .SetText("Ada Lovelace")
    .SetInitials(SkUiAvatarInitials.FirstAndLastWord)
    .SetAvatarColorMode(SkUiAvatarColorMode.DarkBackground)
    .SetImageSource(SkUiImageSource.FromUri(new Uri("https://example.com/ada.jpg")))
    .SetBadge(Colors.LimeGreen);
```

## Key properties

| Property | Default | Notes |
| --- | --- | --- |
| `Text` | `"?"` | Shown without an image, as it is or as its initials (`Initials`). Screen readers read it whole and `AvatarColorMode` picks the colors by it, so a person's name goes here |
| `ImageSource` | `null` | Any MAUI source (`SkUiImageSource` on Core); shown once loaded |
| `TextColor` | `null` | `null`: the look's for `AvatarColorMode` |
| `TextTransform`, `FontFamily`, `FontAttributes`, `FontSize`, `FontAutoScalingEnabled`, `CharacterSpacing` | | As the toolkit's. `FontSize` negative: sized to the avatar |
| `BorderColor` / `BorderWidth` | white / 1 | The toolkit's names for `Stroke` / `StrokeThickness` (those win once set) |
| `CornerRadius`, `StrokeShape`, `Stroke`, `StrokeThickness`, `StrokeDashArray`, …, `Background`, `Padding`, `Shadow`, `Clip` | | From [`SkUiBorder`](SkUiBorder.md). `StrokeShape="Ellipse"`, `"RoundRectangle 8"` or any shape |
| `Initials` | `None` | `None`: `Text` as it is (the toolkit's). `FirstWord`: the first word's initial ("Ada Lovelace": "A"). `FirstAndLastWord`: the first and the last word's ("AL"; one word: one letter). A text without a letter or digit shows as it is |
| `AvatarColorMode` | `Default` | `Default`: the scheme's `TrackOff` background with `DefaultForeground` text. `LightBackground` / `DarkBackground`: a hue picked by `Text`, the same for the same text in every run (`SkUiLook.AvatarPaletteIndex`) |
| `Aspect` | `AspectFill` | How the image fills the content area |
| `BadgeColor` | `null` | A status dot over the border, in a ring of `BadgeBorderColor` (default: the scheme's background) |
| `BadgePosition` | `BottomRight` | `BottomRight`, `TopRight`, `BottomLeft`, `TopLeft`. On a circle the badge sits on the outline at 45° |
| `BadgePainter` | `null` | Draws this avatar's badge instead of the look ([Badge](#badge)); a badge shows when it or `BadgeColor` is set |
| `IsShowingImage` | | Read-only: whether the loaded image is drawn |

Fluent setters: `SetText`, `SetImageSource`, `SetTextColor`, `SetTextTransform`, `SetFont(family, size, attributes)`, `SetFontAutoScalingEnabled`, `SetCharacterSpacing`, `SetBorderColor`, `SetBorderWidth`, `SetInitials`, `SetAvatarColorMode`, `SetAspect`, `SetBadge(color, position, borderColor)`, `SetBadgePainter`. Core also returns `SkUiCoreAvatar` from `SetStroke(Color)`, `SetStrokeThickness`, `SetCornerRadius`, `SetPadding` and `SetBackgroundColor`. Core has no `BorderColor` / `BorderWidth`: use `SetStroke` / `SetStrokeThickness`.

### Initials

The first letter or digit of each word, as a whole grapheme (accents, surrogate pairs), upper case in the current culture. Words without one (emoji, punctuation) are skipped. A text with no letter or digit (the default `"?"`) shows as it is, with `TextTransform`. Initials are always upper case; `TextTransform` applies to text shown as it is.

### Badge

The badge is drawn over the border, so it is not clipped to the avatar's shape. It shows while `BadgeColor` or `BadgePainter` is set. Three ways to change what it looks like:

- **Per avatar:** `BadgePainter`, a delegate `(SKCanvas, SkUiAvatarBadgePaint)`. It gets the avatar's bounds, `Position`, the look's dot `Bounds` at that corner, `Color` (`BadgeColor`, or transparent), `Ring` and `RingWidth`. It may draw anywhere, for example a pill with a count growing from the dot. It runs while the avatar records: leave the canvas as it was, and call `InvalidatePaint()` when what it draws changes (a new count).
- **In a subclass:** override `OnPaintBadge(canvas, paint)` (the default calls `BadgePainter`, else the look), and `HasBadge` to show a badge without `BadgeColor`.
- **For every avatar:** the look's `AvatarBadgePainter` or `DrawAvatarBadgeCore` ([Look](#look)).

```csharp
avatar.BadgePainter = (canvas, badge) =>
{
    using var paint = new SKPaint { IsAntialias = true, Color = SKColors.Red };
    canvas.DrawCircle(badge.Bounds.MidX, badge.Bounds.MidY, badge.Bounds.Width / 2, paint);
    // … the count in white over it
};
```

A badge made of drawn views (a label with a count) is not a property of the avatar, because the avatar clips its children to its shape. Put the avatar and the badge view in a `SkUiGrid` cell instead.

### Look

`SkUiLook` draws the avatar's defaults and its badge:

- `DefaultAvatarSize` and `GetAvatarFontSize(Size)`.
- `GetAvatarColors(mode, key)` returns `SkUiAvatarColors(Background, Text)`.
- `GetAvatarBadgeBounds(avatar, position, cornerRadius)` and `GetAvatarBadgeRingWidth(diameter)`.
- `DrawAvatarBadge` (`AvatarBadgePainter`, `SkUiAvatarBadgePaint`).

A look with its own palette can override `GetAvatarColors` and pick by `AvatarPaletteIndex(key, count)`.

## Accessibility

An image described by the whole `Text` (the name, not its initials), unless `SemanticProperties.Description` (Core: `SemanticDescription`) is set. An empty `Text` leaves a decorative image, not read.

## Differences from the toolkit's AvatarView

| Topic | SkiaUi |
| --- | --- |
| Structure | One drawn node, no `Image` / `Label` children. The `Content` of the border is free: a drawn view set there is drawn over the image or text, clipped to the stroke |
| Image clip | Follows every `StrokeShape` on every platform. The toolkit clips with the shape on iOS / Mac Catalyst and with a rounded rectangle elsewhere |
| Defaults | Fully rounded corners, a background and text color from the look, text sized to the avatar ([above](#the-toolkits-defaults)). Set `CornerRadius="24"`, `BackgroundColor="Transparent"` or `FontSize` to match the toolkit's exactly |
| `Font` | Not a property: set `FontFamily`, `FontSize` and `FontAttributes` |
| `IImage` / `ILabel` members | Not exposed (`IsOpaque`, `LineHeight`, `TextDecorations`, …) |
| Gestures | The arena's (`Tapped`, `TapGestureRecognizer`, …), as every drawn view |

Other avatar controls map in [Migration.md](../Migration.md#avatar-view). Not built: group avatars, built-in avatar characters, size and shape presets (use sizes and `CornerRadius`), a general badge view around any content (draw counts and icons with `BadgePainter`).

## Related

Gallery: `AvatarViewDemoPage` (next to the toolkit's `AvatarView`), `AvatarRowsDemoPage` (a contact list in a collection view). Samples app: "Avatars with presence" (`PresenceAvatarsSample`: Microsoft Teams-style presence badges, through a subclass and through `BadgePainter`). Leak scenario `AvatarsShown`, benchmark `skui-avatars`.
