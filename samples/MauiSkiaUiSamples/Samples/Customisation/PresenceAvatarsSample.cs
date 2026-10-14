using MauiSkiaUi;
using SkiaSharp;

namespace MauiSkiaUiSamples.Samples.Customisation;

/// <summary>
/// HOWTO: draw avatars with presence badges like Microsoft Teams: initials on a pastel color picked by the name, and a
/// badge at the bottom right with an icon for the person's status (a check for Available, a clock for Away, an × for
/// Offline, …).
/// <para>
/// The avatar draws its badge over its border through <see cref="SkUiAvatarView.OnPaintBadge"/>, whose default asks the
/// avatar's <see cref="SkUiAvatarView.BadgePainter"/>, else the look. This example shows both ways to replace it: the team
/// list uses a small subclass (<see cref="PresenceAvatar"/>, with a bindable <c>Presence</c>), and your own avatar at the
/// top sets a <c>BadgePainter</c> on a stock <see cref="SkUiAvatarView"/>. Both draw with <see cref="PresenceBadge"/>.
/// </para>
/// </summary>
public sealed class PresenceAvatarsSample : SamplePage, ISample
{
    public static SampleInfo Info { get; } = new(
        SampleSection.Customisation,
        Title: "Avatars with presence",
        Summary: "Avatars with a status badge like Microsoft Teams': initials on a pastel color picked by the name, and " +
            "an icon for Available, Busy, Do not disturb, Away, Out of office or Offline. Tap an avatar to change its status.",
        HowTo:
        [
            "Show initials of a name: `Text=\"Ada Lovelace\" Initials=\"FirstAndLastWord\"`, colored by the name with " +
                "`AvatarColorMode=\"LightBackground\"`.",
            "Subclass `SkUiAvatarView`, add the data the badge shows (here a bindable `Presence`), override `HasBadge` " +
                "to show the badge, and draw it in `OnPaintBadge(SKCanvas, SkUiAvatarBadgePaint)`.",
            "Or, without a subclass: set `avatar.BadgePainter = (canvas, badge) => …` and call `avatar.InvalidatePaint()` " +
                "when what it draws changes.",
            "Draw in `badge.Bounds`: the look's circle for a badge at `BadgePosition`. Fill it with `badge.Ring` first, " +
                "so the badge seems cut out of the avatar, then the icon inside `badge.RingWidth`."
        ],
        ThingsToKnow:
        [
            "The badge is drawn over the border and is not clipped to the avatar's circle.",
            "It is drawn while the avatar records, so a new status re-records only that avatar: no layout, no other row.",
            "`badge.Ring` is the color scheme's background unless `BadgeBorderColor` is set: set it to the color behind " +
                "the avatar when that is not the page background.",
            "The badge grows with the avatar: the look sizes `badge.Bounds` (about 28 % of the avatar) and `badge.RingWidth`.",
            "Screen readers read the avatar's `Text`. `PresenceAvatar` adds the status to it in `OnPopulateSemantics`.",
            "Painters run on the UI thread: `PresenceBadge` reuses its paints and draws its symbols as lines, allocating nothing."
        ]);

    private static readonly (string Name, Presence Presence)[] Team =
    [
        ("Mia Lindqvist", Presence.Away),
        ("Yusuf Sahin", Presence.Offline),
        ("Rafal Kowalski", Presence.Available),
        ("Ada Lovelace", Presence.Busy),
        ("Grace Hopper", Presence.DoNotDisturb),
        ("Alan Turing", Presence.BeRightBack),
        ("Margaret Hamilton", Presence.OutOfOffice),
        ("Linus Torvalds", Presence.Available)
    ];

    private static readonly Presence[] Statuses =
        [Presence.Available, Presence.Busy, Presence.DoNotDisturb, Presence.BeRightBack, Presence.Away, Presence.OutOfOffice, Presence.Offline];

    public PresenceAvatarsSample() : base(Info) => SampleContent = Build();

    private static View Build()
    {
        var content = new SkUiVerticalStackLayout { Spacing = 4, Padding = new Thickness(16) };
        content.Children.Add(Me());
        content.Children.Add(Heading("Team"));
        foreach (var (name, presence) in Team)
            content.Children.Add(Row(name, presence));
        content.Children.Add(Heading("Sizes"));
        content.Children.Add(Sizes());
        return new SkUiScrollView { Content = content, HeightRequest = 640, BackgroundColor = SampleColors.Surface };
    }

    /// <summary>Your own avatar: a stock avatar whose badge a <see cref="SkUiAvatarView.BadgePainter"/> draws.</summary>
    private static SkUiView Me()
    {
        var presence = Presence.Available;
        var status = Caption(PresenceBadge.Label(presence));
        var avatar = new SkUiAvatarView
        {
            Text = "Rafal Kukla",
            Initials = SkUiAvatarInitials.FirstAndLastWord,
            AvatarColorMode = SkUiAvatarColorMode.LightBackground,
            FontFamily = SampleFonts.Semibold,
            BorderWidth = 0,
            WidthRequest = 56,
            HeightRequest = 56,
            BadgePainter = (canvas, badge) => PresenceBadge.Draw(canvas, badge.Bounds, badge.Ring, presence)
        };
        avatar.Tapped += (_, _) =>
        {
            presence = PresenceBadge.Next(presence);
            status.Text = PresenceBadge.Label(presence);
            avatar.InvalidatePaint(); // the painter reads a variable the avatar does not know about
        };
        return Row(avatar, "You (tap to change)", status);
    }

    /// <summary>A team member: the <see cref="PresenceAvatar"/> subclass, its badge following its <c>Presence</c> property.</summary>
    private static SkUiView Row(string name, Presence presence)
    {
        var status = Caption(PresenceBadge.Label(presence));
        var avatar = new PresenceAvatar { Text = name, Presence = presence, WidthRequest = 40, HeightRequest = 40 };
        avatar.Tapped += (_, _) =>
        {
            avatar.Presence = PresenceBadge.Next(avatar.Presence);
            status.Text = PresenceBadge.Label(avatar.Presence);
        };
        return Row(avatar, name, status);
    }

    private static SkUiGrid Row(SkUiAvatarView avatar, string name, SkUiLabel status)
    {
        avatar.VerticalOptions = LayoutOptions.Center;
        var texts = new SkUiVerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            Children = { new SkUiLabel { Text = name, FontSize = 15, FontFamily = SampleFonts.Semibold, TextColor = SampleColors.Ink }, status }
        };
        var row = new SkUiGrid
        {
            Padding = new Thickness(0, 6),
            ColumnSpacing = 12,
            ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)]
        };
        row.Children.Add(avatar);
        row.Children.Add(texts);
        Grid.SetColumn(texts, 1);
        return row;
    }

    /// <summary>Every status at several sizes, wrapped to the page's width: the badge grows with the avatar.</summary>
    private static SkUiView Sizes()
    {
        var sizes = new SkUiVerticalStackLayout { Spacing = 12 };
        foreach (var size in new[] { 24d, 32d, 48d, 72d })
        {
            var row = new SkUiWrapLayout { Spacing = 8, RowSpacing = 8 };
            foreach (var presence in Statuses)
                row.Children.Add(new PresenceAvatar { Text = Team[(int)presence % Team.Length].Name, Presence = presence, WidthRequest = size, HeightRequest = size });
            sizes.Children.Add(row);
        }
        return sizes;
    }

    private static SkUiLabel Heading(string text) =>
        new() { Text = text, FontSize = 13, FontFamily = SampleFonts.Semibold, TextColor = SampleColors.Caption, Margin = new Thickness(0, 16, 0, 4) };

    private static SkUiLabel Caption(string text) => new() { Text = text, FontSize = 13, TextColor = SampleColors.Caption };
}

/// <summary>A person's status, as Microsoft Teams shows it.</summary>
public enum Presence
{
    /// <summary>No badge.</summary>
    None,
    Available,
    Busy,
    DoNotDisturb,
    BeRightBack,
    Away,
    OutOfOffice,
    Offline
}

/// <summary>
/// An avatar with initials on a pastel color picked by the name, and a presence badge: <see cref="HasBadge"/> and
/// <see cref="OnPaintBadge"/> overridden to draw <see cref="Presence"/> with <see cref="PresenceBadge"/>.
/// </summary>
public sealed class PresenceAvatar : SkUiAvatarView
{
    /// <summary>Bindable <see cref="Presence"/>.</summary>
    public static readonly BindableProperty PresenceProperty = BindableProperty.Create(nameof(Presence), typeof(Presence), typeof(PresenceAvatar), Presence.None,
        propertyChanged: (view, _, _) => ((PresenceAvatar)view).OnPresenceChanged());

    public PresenceAvatar()
    {
        Initials = SkUiAvatarInitials.FirstAndLastWord;
        AvatarColorMode = SkUiAvatarColorMode.LightBackground;
        FontFamily = SampleFonts.Semibold;
        BorderWidth = 0;
    }

    /// <summary>The status the badge shows (<see cref="MauiSkiaUiSamples.Samples.Customisation.Presence.None"/>: no badge).</summary>
    public Presence Presence { get => (Presence)GetValue(PresenceProperty); set => SetValue(PresenceProperty, value); }

    private void OnPresenceChanged()
    {
        InvalidatePaint(); // re-records this avatar only
        InvalidateSemantics();
    }

    /// <inheritdoc />
    protected override bool HasBadge => Presence != Presence.None;

    /// <inheritdoc />
    protected override void OnPaintBadge(SKCanvas canvas, SkUiAvatarBadgePaint badge) =>
        PresenceBadge.Draw(canvas, badge.Bounds, badge.Ring, Presence);

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        if (Presence != Presence.None)
            info.Text = $"{Text}, {PresenceBadge.Label(Presence)}";
    }
}

/// <summary>Draws Microsoft Teams' presence icons: a colored dot with a white symbol, or a grey ring with an × for Offline.</summary>
public static class PresenceBadge
{
    private const string AvailableText = "Available";
    private const string BusyText = "Busy";
    private const string DoNotDisturbText = "Do not disturb";
    private const string BeRightBackText = "Be right back";
    private const string AwayText = "Away";
    private const string OutOfOfficeText = "Out of office";
    private const string OfflineText = "Offline";

    private static readonly SKColor Green = new(0x6B, 0xB7, 0x00);
    private static readonly SKColor Red = new(0xC4, 0x31, 0x4B);
    private static readonly SKColor Amber = new(0xFF, 0xAA, 0x44);
    private static readonly SKColor Purple = new(0xB4, 0x00, 0x9E);
    private static readonly SKColor Grey = new(0x8A, 0x88, 0x86);

    // Painters run on the UI thread: one fill and one stroke, reused. Symbols are round-capped lines, so no paths.
    private static readonly SKPaint Fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint Stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };

    /// <summary>The status as Teams names it.</summary>
    public static string Label(Presence presence) => presence switch
    {
        Presence.Available => AvailableText,
        Presence.Busy => BusyText,
        Presence.DoNotDisturb => DoNotDisturbText,
        Presence.BeRightBack => BeRightBackText,
        Presence.Away => AwayText,
        Presence.OutOfOffice => OutOfOfficeText,
        Presence.Offline => OfflineText,
        _ => string.Empty
    };

    /// <summary>The next status a tap sets.</summary>
    public static Presence Next(Presence presence) => presence == Presence.Offline ? Presence.Available : presence + 1;

    /// <summary>
    /// Draws <paramref name="presence"/> in <paramref name="bounds"/>: a disc of <paramref name="ring"/> (the color behind
    /// the avatar, so the badge looks cut out of it), then the icon inside it.
    /// </summary>
    public static void Draw(SKCanvas canvas, SKRect bounds, SKColor ring, Presence presence)
    {
        if (presence == Presence.None || bounds.IsEmpty)
            return;
        var center = new SKPoint(bounds.MidX, bounds.MidY);
        var outer = Math.Min(bounds.Width, bounds.Height) / 2;
        var r = outer * 0.8f; // the icon inside a ring of the background
        Fill.Color = ring;
        canvas.DrawCircle(center, outer, Fill);
        Stroke.Color = SKColors.White;
        Stroke.StrokeWidth = Math.Max(1, r * 0.24f);
        switch (presence)
        {
            case Presence.Available: // a white check on green
                Disc(canvas, center, r, Green);
                Polyline(canvas, center, r, (-0.42f, 0.02f), (-0.12f, 0.32f), (0.42f, -0.28f));
                break;
            case Presence.Busy: // a red dot
                Disc(canvas, center, r, Red);
                break;
            case Presence.DoNotDisturb: // a white bar on red
                Disc(canvas, center, r, Red);
                canvas.DrawLine(center.X - r * 0.45f, center.Y, center.X + r * 0.45f, center.Y, Stroke);
                break;
            case Presence.BeRightBack: // a clock on an amber ring
                Ring(canvas, center, r, Amber);
                Clock(canvas, center, r, Amber);
                break;
            case Presence.Away: // a white clock on amber
                Disc(canvas, center, r, Amber);
                Clock(canvas, center, r, SKColors.White);
                break;
            case Presence.OutOfOffice: // a white arrow to the left on purple
                Disc(canvas, center, r, Purple);
                Polyline(canvas, center, r, (0.42f, 0), (-0.38f, 0));
                Polyline(canvas, center, r, (-0.05f, -0.34f), (-0.4f, 0), (-0.05f, 0.34f));
                break;
            case Presence.Offline: // a grey × in a grey ring
                Ring(canvas, center, r, Grey);
                Stroke.Color = Grey;
                Stroke.StrokeWidth = Math.Max(1, r * 0.18f);
                var arm = r * 0.32f;
                canvas.DrawLine(center.X - arm, center.Y - arm, center.X + arm, center.Y + arm, Stroke);
                canvas.DrawLine(center.X - arm, center.Y + arm, center.X + arm, center.Y - arm, Stroke);
                break;
        }
    }

    private static void Disc(SKCanvas canvas, SKPoint center, float radius, SKColor color)
    {
        Fill.Color = color;
        canvas.DrawCircle(center, radius, Fill);
    }

    private static void Ring(SKCanvas canvas, SKPoint center, float radius, SKColor color)
    {
        var width = Math.Max(1, radius * 0.18f);
        Stroke.Color = color;
        Stroke.StrokeWidth = width;
        canvas.DrawCircle(center, radius - width / 2, Stroke);
        Stroke.Color = SKColors.White;
    }

    /// <summary>Clock hands: up from the centre, then down to the right.</summary>
    private static void Clock(SKCanvas canvas, SKPoint center, float radius, SKColor color)
    {
        Stroke.Color = color;
        Stroke.StrokeWidth = Math.Max(1, radius * 0.2f);
        Polyline(canvas, center, radius, (-0.05f, -0.5f), (-0.05f, 0.05f), (0.4f, 0.3f));
    }

    /// <summary>Lines through <paramref name="points"/>, given as fractions of <paramref name="radius"/> from <paramref name="center"/>.</summary>
    private static void Polyline(SKCanvas canvas, SKPoint center, float radius, params ReadOnlySpan<(float X, float Y)> points)
    {
        for (var index = 1; index < points.Length; index++)
            canvas.DrawLine(center.X + points[index - 1].X * radius, center.Y + points[index - 1].Y * radius,
                center.X + points[index].X * radius, center.Y + points[index].Y * radius, Stroke);
    }
}
