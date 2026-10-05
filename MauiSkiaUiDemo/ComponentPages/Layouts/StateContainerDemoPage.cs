using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiStateContainer"/> (the Community Toolkit's <c>StateContainer</c>) on a <see cref="SkUiGrid"/>: loading,
/// error and empty states replace the cells and span the whole grid. The state change animations
/// (<see cref="SkUiStateContainer.BeforeStateChangeAnimationProperty"/> / <see cref="SkUiStateContainer.AfterStateChangeAnimationProperty"/>)
/// animate every change of <c>CurrentState</c>; picking states quickly shows the newest one winning.
/// </summary>
public sealed class StateContainerDemoPage : ComponentDemoPage
{
    private const string ContentState = "Content";
    private static readonly string[] Animations = ["None", "Fade", "Slide"];

    public StateContainerDemoPage()
        : base("SkUiStateContainer", new SkUiGrid(), widthRange: (120, 360, 260), heightRange: (80, 320, 160))
    {
        var grid = (SkUiGrid)SkiaControl;
        grid.RowDefinitions = [new(GridLength.Star), new(GridLength.Star)];
        grid.ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)];
        grid.RowSpacing = 6;
        grid.ColumnSpacing = 6;
        grid.BackgroundColor = Colors.LightGray;
        for (var index = 0; index < 4; index++)
        {
            var color = index % 3 == 0 ? Accent : index % 3 == 1 ? DemoColors.SampleA : DemoColors.SampleB;
            var cell = new SkUiLabel { Text = $"Cell {index + 1}", Background = color, TextColor = Colors.White, FontSize = 14, Padding = new Thickness(8) };
            Grid.SetRow(cell, index / 2);
            Grid.SetColumn(cell, index % 2);
            grid.Children.Add(cell);
        }

        var states = SkUiStateContainer.GetStateViews(grid);
        states.Add(State("Loading", new SkUiVerticalStackLayout
        {
            Spacing = 8, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                new SkUiActivityIndicator { IsRunning = true, Color = Accent, WidthRequest = 28, HeightRequest = 28 },
                new SkUiLabel { Text = "Loading…", TextColor = Ink, HorizontalTextAlignment = TextAlignment.Center }
            }
        }));
        var retry = new SkUiButton { Text = "Retry", HorizontalOptions = LayoutOptions.Center };
        retry.Clicked += (_, _) => SkUiStateContainer.SetCurrentState(grid, null);
        states.Add(State("Error", new SkUiBorder
        {
            Stroke = Colors.Crimson, StrokeThickness = 2, CornerRadius = 10, Padding = new Thickness(12), Background = Colors.White,
            Content = new SkUiVerticalStackLayout
            {
                Spacing = 8,
                Children = { new SkUiLabel { Text = "Something went wrong.", TextColor = Colors.Crimson, HorizontalTextAlignment = TextAlignment.Center }, retry }
            }
        }));
        states.Add(State("Empty", new SkUiLabel
        {
            Text = "Nothing here yet", TextColor = DemoColors.Caption, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center
        }));

        Choice("Animation", Animations, "None", value => SetAnimation(grid, value), () => AnimationName(grid));
        Choice("CurrentState", [ContentState, "Loading", "Error", "Empty"], ContentState,
            value => SkUiStateContainer.SetCurrentState(grid, value == ContentState ? null : value),
            () => SkUiStateContainer.GetCurrentState(grid) ?? ContentState);
        grid.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == SkUiStateContainer.CanStateChangeProperty.PropertyName)
                Feedback(SkUiStateContainer.GetCanStateChange(grid) ? $"Showing {SkUiStateContainer.GetCurrentState(grid) ?? "the content"}" : "Animating (CanStateChange is false)");
        };
    }

    private static readonly SkUiViewAnimation SlideOut = new([new(SkUiAnimatableProperty.TranslationY, to: -16), new(SkUiAnimatableProperty.Opacity, to: 0)], 150, Easing.CubicIn);
    private static readonly SkUiViewAnimation SlideIn = new([new(SkUiAnimatableProperty.TranslationY, from: 24), new(SkUiAnimatableProperty.Scale, from: 0.96), new(SkUiAnimatableProperty.Opacity, from: 0)], 250, Easing.CubicOut);

    private static void SetAnimation(BindableObject grid, string name)
    {
        var (before, after) = name switch
        {
            "Fade" => (SkUiViewAnimation.FadeOut(), SkUiViewAnimation.FadeIn()),
            "Slide" => (SlideOut, SlideIn),
            _ => ((SkUiViewAnimation?)null, (SkUiViewAnimation?)null)
        };
        SkUiStateContainer.SetBeforeStateChangeAnimation(grid, before);
        SkUiStateContainer.SetAfterStateChangeAnimation(grid, after);
    }

    private static string AnimationName(BindableObject grid) => SkUiStateContainer.GetAfterStateChangeAnimation(grid) switch
    {
        null => "None",
        var after when ReferenceEquals(after, SlideIn) => "Slide",
        _ => "Fade"
    };

    private static SkUiView State(string key, SkUiView view)
    {
        SkUiStateView.SetStateKey(view, key);
        return view;
    }
}
