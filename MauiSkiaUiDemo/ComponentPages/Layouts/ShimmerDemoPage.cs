using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUiDemo;

/// <summary>
/// Property playground for <see cref="SkUiShimmer"/>: a profile card and its skeleton (Core bones in one host), with presets
/// for the default wave and the two animations of a hand-made skeleton view (an opacity pulse and a color shift), and the
/// switch to the content with or without a fade.
/// </summary>
public sealed class ShimmerDemoPage : ComponentDemoPage
{
    private const string LookColor = "Look";
    private const string BlueGray = "Blue gray";
    private const string OwnColors = "Bones' own";
    private const string LightBlue = "Light blue";
    private const string EffectsOwn = "Effect's own";
    private const string Linear = "Linear";
    private const string SinInOut = "SinInOut";
    private const string CubicInOut = "CubicInOut";
    private const string NoFade = "None";
    private const string FadeOut = "FadeOut 350 CubicInOut";
    private const string CrossFade = "FadeOut 150 + FadeIn 250";
    private const string DurationName = nameof(SkUiShimmer.Duration);
    private const string EffectName = nameof(SkUiShimmer.Effect);
    private const string RepeatModeName = nameof(SkUiShimmer.RepeatMode);
    private const string EasingName = nameof(SkUiShimmer.Easing);
    private const string PulseOpacityName = nameof(SkUiShimmer.PulseOpacity);
    private const string HighlightColorName = nameof(SkUiShimmer.HighlightColor);
    private const string WaveWidthName = nameof(SkUiShimmer.WaveWidth);
    private static readonly string[] Fills = [LookColor, BlueGray, OwnColors];
    private static readonly string[] HighlightColors = [LookColor, LightBlue];
    private static readonly string[] Easings = [EffectsOwn, Linear, SinInOut, CubicInOut];
    private static readonly string[] Fades = [NoFade, FadeOut, CrossFade];

    public ShimmerDemoPage()
        : base(nameof(SkUiShimmer), Create(), widthRange: (160, 400, 300), heightRange: (60, 400, 120))
    {
        var shimmer = (SkUiShimmer)SkiaControl;
        var fade = NoFade;
        Toggle(nameof(SkUiShimmer.IsLoading), true, value => shimmer.IsLoading = value, () => shimmer.IsLoading);
        Choice(EffectName, [SkUiShimmerEffect.Wave, SkUiShimmerEffect.Pulse, SkUiShimmerEffect.Color, SkUiShimmerEffect.None], SkUiShimmerEffect.Wave,
            value => shimmer.Effect = value, () => shimmer.Effect);
        Choice(nameof(SkUiShimmer.WaveDirection),
            [SkUiShimmerWaveDirection.Default, SkUiShimmerWaveDirection.LeftToRight, SkUiShimmerWaveDirection.RightToLeft, SkUiShimmerWaveDirection.TopToBottom, SkUiShimmerWaveDirection.BottomToTop],
            SkUiShimmerWaveDirection.Default, value => shimmer.WaveDirection = value, () => shimmer.WaveDirection);
        Number(WaveWidthName, 20, 400, 200, value => shimmer.WaveWidth = value, () => shimmer.WaveWidth, whole: true);
        Number(DurationName, 200, 3000, 1000, value => shimmer.Duration = value, () => shimmer.Duration, whole: true);
        Choice(RepeatModeName, [SkUiShimmerRepeatMode.Default, SkUiShimmerRepeatMode.Restart, SkUiShimmerRepeatMode.Reverse], SkUiShimmerRepeatMode.Default,
            value => shimmer.RepeatMode = value, () => shimmer.RepeatMode);
        Choice(EasingName, Easings, EffectsOwn, value => shimmer.Easing = EasingOf(value), () => NameOf(shimmer.Easing));
        Number(PulseOpacityName, 0, 1, 0.4, value => shimmer.PulseOpacity = value, () => shimmer.PulseOpacity);
        Choice(nameof(SkUiShimmer.BoneColor), Fills, LookColor, value => shimmer.BoneColor = value switch
        {
            BlueGray => Color.FromArgb("#D5DEE8"),
            OwnColors => Colors.Transparent,
            _ => null
        }, () => shimmer.BoneColor is null ? LookColor : shimmer.BoneColor.Alpha == 0 ? OwnColors : BlueGray);
        Choice(HighlightColorName, HighlightColors, LookColor, value => shimmer.HighlightColor = value == LightBlue ? Color.FromArgb("#EEF4FA") : null,
            () => shimmer.HighlightColor is null ? LookColor : LightBlue);
        Choice("Switch animation", Fades, NoFade, value =>
        {
            fade = value;
            (shimmer.BeforeStateChangeAnimation, shimmer.AfterStateChangeAnimation) = value switch
            {
                FadeOut => (SkUiViewAnimation.FadeOut(350, Easing.CubicInOut), (SkUiViewAnimation?)null),
                CrossFade => (SkUiViewAnimation.FadeOut(150), SkUiViewAnimation.FadeIn(250)),
                _ => ((SkUiViewAnimation?)null, (SkUiViewAnimation?)null)
            };
        }, () => fade);
        // Presets move the editors above.
        ActionButton("Preset: default wave", () =>
        {
            SetChoice(EffectName, SkUiShimmerEffect.Wave);
            SetNumber(DurationName, 1000);
            SetNumber(WaveWidthName, 200);
            SetChoice(RepeatModeName, SkUiShimmerRepeatMode.Default);
            SetChoice(EasingName, EffectsOwn);
        });
        // A skeleton view's fade: opacity 0.3 → 1 → 0.3 with a cosine over 1.5 s, passes of 750 ms there and back.
        ActionButton("Preset: pulse 0.3 ↔ 1, 1.5 s", () =>
        {
            SetChoice(EffectName, SkUiShimmerEffect.Pulse);
            SetNumber(PulseOpacityName, 0.3);
            SetNumber(DurationName, 750);
            SetChoice(RepeatModeName, SkUiShimmerRepeatMode.Default);
            SetChoice(EasingName, EffectsOwn);
        });
        // A skeleton view's color animation: start → end color linearly in 750 ms, then from the start again.
        ActionButton("Preset: color shift, restart", () =>
        {
            SetChoice(EffectName, SkUiShimmerEffect.Color);
            SetNumber(DurationName, 750);
            SetChoice(RepeatModeName, SkUiShimmerRepeatMode.Restart);
            SetChoice(EasingName, Linear);
            SetChoice(HighlightColorName, LightBlue);
        });
        Feedback("The bones are recorded once; the effect runs on the render thread");
    }

    private static Easing? EasingOf(string name) => name switch
    {
        Linear => Easing.Linear,
        SinInOut => Easing.SinInOut,
        CubicInOut => Easing.CubicInOut,
        _ => null
    };

    private static string NameOf(Easing? easing) =>
        easing is null ? EffectsOwn : ReferenceEquals(easing, Easing.Linear) ? Linear : ReferenceEquals(easing, Easing.SinInOut) ? SinInOut : CubicInOut;

    private static SkUiShimmer Create() => new()
    {
        Content = Card(),
        SkeletonContent = Skeleton()
    };

    /// <summary>The loaded card: an avatar, a name and a line of text.</summary>
    private static SkUiGrid Card()
    {
        var grid = new SkUiGrid
        {
            Padding = new Thickness(12), ColumnSpacing = 12, RowSpacing = 4, BackgroundColor = Colors.White,
            ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star)], RowDefinitions = [new(GridLength.Auto), new(GridLength.Auto)]
        };
        var avatar = new SkUiAvatarView { Text = "Ada Lovelace", Initials = SkUiAvatarInitials.FirstAndLastWord, AvatarColorMode = SkUiAvatarColorMode.LightBackground, WidthRequest = 48, HeightRequest = 48 };
        Grid.SetRowSpan(avatar, 2);
        var name = new SkUiLabel { Text = "Ada Lovelace", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = DemoColors.Ink };
        Grid.SetColumn(name, 1);
        var line = new SkUiLabel { Text = "Wrote the first program, 1843", FontSize = 13, TextColor = DemoColors.Caption };
        Grid.SetColumn(line, 1);
        Grid.SetRow(line, 1);
        grid.Children.Add(avatar);
        grid.Children.Add(name);
        grid.Children.Add(line);
        return grid;
    }

    /// <summary>The card's skeleton as plain Core bones (no bindings): a circle and two bars.</summary>
    private static SkUiCoreHost Skeleton()
    {
        var bones = new SkUiCoreHorizontalStackLayout { Padding = new Thickness(12), Spacing = 12 };
        bones.Add(new SkUiCoreBox { Color = Colors.Black, CornerRadius = 24, Width = 48, Height = 48 });
        var lines = new SkUiCoreVerticalStackLayout { Spacing = 8, VerticalAlignment = LayoutAlignment.Center };
        lines.Add(new SkUiCoreBox { Color = Colors.Black, CornerRadius = 4, Width = 140, Height = 16 });
        lines.Add(new SkUiCoreBox { Color = Colors.Black, CornerRadius = 4, Width = 200, Height = 12 });
        bones.Add(lines);
        return new SkUiCoreHost().SetContent(bones);
    }
}
