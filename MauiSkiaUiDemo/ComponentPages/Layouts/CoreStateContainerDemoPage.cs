using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiCoreStateContainer"/>: a Core list replaced by loading (a skeleton of shimmering bones), error (built by
/// a factory when first shown) and empty states. The state change animations animate every change of <c>CurrentState</c>;
/// "Grow" scales the new state vertically only (independent Core <c>ScaleY</c>).
/// </summary>
public sealed class CoreStateContainerDemoPage : ComponentDemoPage
{
    private const string ContentState = "Content";
    private const string Loading = "Loading";
    private const string Error = "Error";
    private const string Empty = "Empty";
    private const string None = "None";
    private const string Fade = "Fade";
    private const string Grow = "Grow";
    private static readonly string[] Animations = [None, Fade, Grow];
    private static readonly SkUiViewAnimation GrowIn = new([new(SkUiAnimatableProperty.ScaleY, from: 0.6), new(SkUiAnimatableProperty.Opacity, from: 0)], 250, Easing.CubicOut);

    public CoreStateContainerDemoPage()
        : base(nameof(SkUiCoreStateContainer), CreateHost(out var states), native: null, widthRange: (120, 360, 260), heightRange: (80, 320, 200))
    {
        Choice("Animation", Animations, None, value => SetAnimation(states, value), () => AnimationName(states));
        Choice(nameof(SkUiCoreStateContainer.CurrentState), [ContentState, Loading, Error, Empty], ContentState,
            value => states.CurrentState = value == ContentState ? null : value, () => states.CurrentState ?? ContentState);
        states.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SkUiCoreStateContainer.CanStateChange))
                Feedback(states.CanStateChange ? $"Showing {states.ShownState ?? "the content"}" : "Animating (CanStateChange is false)");
        };
    }

    private static SkUiCoreHost CreateHost(out SkUiCoreStateContainer states)
    {
        var rows = new SkUiCoreVerticalStackLayout().SetSpacing(6);
        for (var index = 0; index < 4; index++)
            rows.Add(Row($"Message {index + 1}", index % 2 == 0 ? Accent : DemoColors.SampleA));

        var bones = new SkUiCoreVerticalStackLayout().SetSpacing(10);
        for (var index = 0; index < 4; index++)
            bones.Add(new SkUiCoreShimmerBone { Height = 28, CornerRadius = 6 });
        var skeleton = new SkUiCoreShimmer().SetSkeletonContent(bones);

        var container = new SkUiCoreStateContainer()
            .SetContent(rows)
            .AddState(Loading, skeleton)
            .AddState(Empty, Centered(new SkUiCoreLabel().SetText("Nothing here yet").SetTextColor(DemoColors.Caption)));
        container.AddState(Error, () => ErrorCard(container)); // built when first shown; its retry goes back to the content
        container.SetPadding(new Thickness(10));
        states = container;
        var host = new SkUiCoreHost { BackgroundColor = Colors.LightGray };
        host.SetContent(container);
        return host;
    }

    private static SkUiCoreNode Row(string text, Color color)
    {
        var label = new SkUiCoreLabel().SetText(text).SetTextColor(Colors.White).SetFontSize(14);
        var row = new SkUiCoreBorder().SetStrokeThickness(0).SetCornerRadius(6).SetPadding(new Thickness(10, 6)).SetContent(label);
        row.SetBackground(color);
        return row;
    }

    private static SkUiCoreNode Centered(SkUiCoreNode node)
    {
        node.SetHorizontalAlignment(LayoutAlignment.Center).SetVerticalAlignment(LayoutAlignment.Center);
        return node;
    }

    private static SkUiCoreNode ErrorCard(SkUiCoreStateContainer states)
    {
        var retry = new SkUiCoreButton();
        retry.SetText("Retry");
        retry.SetHorizontalAlignment(LayoutAlignment.Center);
        retry.SetClicked(() => states.SetCurrentState(null));
        var stack = new SkUiCoreVerticalStackLayout().SetSpacing(8);
        stack.Add(new SkUiCoreLabel().SetText("Something went wrong.").SetTextColor(Colors.Crimson).SetHorizontalTextAlignment(TextAlignment.Center));
        stack.Add(retry);
        var card = new SkUiCoreBorder().SetStroke(Colors.Crimson).SetStrokeThickness(2).SetCornerRadius(10).SetPadding(new Thickness(12)).SetContent(stack);
        card.SetBackground(Colors.White);
        return Centered(card);
    }

    private static void SetAnimation(SkUiCoreStateContainer states, string name)
    {
        var (before, after) = name switch
        {
            Fade => (SkUiViewAnimation.FadeOut(), SkUiViewAnimation.FadeIn()),
            Grow => (SkUiViewAnimation.FadeOut(120), GrowIn),
            _ => ((SkUiViewAnimation?)null, (SkUiViewAnimation?)null)
        };
        states.SetBeforeStateChangeAnimation(before).SetAfterStateChangeAnimation(after);
    }

    private static string AnimationName(SkUiCoreStateContainer states) =>
        states.AfterStateChangeAnimation is null ? None : ReferenceEquals(states.AfterStateChangeAnimation, GrowIn) ? Grow : Fade;
}
