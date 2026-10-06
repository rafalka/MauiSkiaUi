using MauiSkiaUi;
using Microsoft.Maui.Controls.Shapes;

namespace MauiSkiaUiDemo;

/// <summary>Shared pieces of the <see cref="SkUiExpander"/> demos: a header with a chevron that turns with the expander, and the easings to pick from.</summary>
internal static class DemoExpanders
{
    /// <summary>Easings offered by the demos, by their MAUI name (also their XAML value).</summary>
    public static readonly (string Name, Easing Easing)[] Easings =
    [
        ("CubicInOut", Easing.CubicInOut), ("Linear", Easing.Linear), ("SinInOut", Easing.SinInOut), ("CubicOut", Easing.CubicOut),
        ("SinOut", Easing.SinOut), ("SpringOut", Easing.SpringOut), ("BounceOut", Easing.BounceOut)
    ];

    public static string[] EasingNames => Easings.Select(easing => easing.Name).ToArray();

    public static Easing EasingNamed(string name) => Easings.First(easing => easing.Name == name).Easing;

    public static string NameOf(Easing? easing) => Easings.FirstOrDefault(entry => ReferenceEquals(entry.Easing, easing)).Name ?? "Linear";

    /// <summary>
    /// A header row: <paramref name="title"/> and a chevron that points to where the content opens while collapsed and
    /// turns over while expanded, animated on the render thread with the expander's own length and easing.
    /// </summary>
    public static SkUiView Header(SkUiExpander expander, string title, Color text, Color background, double fontSize = 16)
    {
        var chevron = new SkUiPath
        {
            Stroke = text, StrokeThickness = 2, StrokeLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round,
            WidthRequest = 14, HeightRequest = 8, VerticalOptions = LayoutOptions.Center
        }.SetData("M 0 0 L 6 6 L 12 0");
        var header = new SkUiGrid
        {
            ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 8,
            Padding = new Thickness(12, 10), Background = background,
            Children = { new SkUiLabel { Text = title, TextColor = text, FontSize = fontSize, FontAttributes = FontAttributes.Bold } }
        };
        Grid.SetColumn(chevron, 1);
        header.Children.Add(chevron);
        chevron.Rotation = ChevronAngle(expander);
        expander.ExpandedChanged += (_, _) => TurnChevron(expander, chevron);
        expander.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SkUiExpander.Direction))
                chevron.Rotation = ChevronAngle(expander);
        };
        return header;
    }

    // Down: points down while collapsed, up while expanded; Up: the other way round.
    private static double ChevronAngle(SkUiExpander expander) =>
        expander.IsExpanded != (expander.Direction == SkUiExpandDirection.Up) ? 180 : 0;

    private static void TurnChevron(SkUiExpander expander, SkUiView chevron)
    {
        var angle = ChevronAngle(expander);
        if (expander.AnimationLength == 0 || SkUiMotion.IsMotionReduced)
        {
            chevron.Rotation = angle;
            return;
        }
        // A newer turn supersedes a running one, which then keeps the newer angle.
        _ = new SkUiViewAnimation([new(SkUiAnimatableProperty.Rotation, to: angle)], expander.AnimationLength, expander.AnimationEasing)
            .RunAsync(chevron);
    }
}
