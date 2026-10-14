using System.Reflection;
using MauiSkiaUi.Core;

namespace MauiSkiaUi.Benchmarks;

/// <summary>How a <see cref="SkeletonCompare"/> scenario builds its loading screen.</summary>
public enum SkeletonSource
{
    /// <summary>No <c>SkeletonContent</c>: the content is the skeleton (C9b).</summary>
    Content,

    /// <summary>A hand-made skeleton of shimmer bones laid out as the content (C9 / bones).</summary>
    Static
}

/// <summary>
/// Investigation: the same 60 feed cards (avatar, name, two-line message, value) loading, with the skeleton made from the
/// content or by hand with bones laid out the same way. <c>loaded</c> variants time the data arriving (texts set,
/// <c>IsLoading = false</c>); <c>wave</c> variants sample the wave while loading (device). Uses only API from before C9b
/// (the content variants draw the content as it is there).
/// </summary>
public sealed class SkeletonCompare(SkeletonSource source, bool skui, bool loaded, string mode = "Bones") : BenchScenario
{
    private const int Cards = 60;
    private const string Avatar = "Person";
    private static readonly string[] Messages =
    [
        "The quarterly numbers are in; the team beat its target by a wide margin.",
        "Shipped the release notes.",
        "Reviewing the profile page skeleton next, then the settings screen."
    ];
    private static readonly object? CoreLinesProperty = typeof(SkUiCoreShimmer).GetField("SkeletonLinesProperty")?.GetValue(null);
    private static readonly MethodInfo? CoreSetValue = CoreLinesProperty is null ? null
        : typeof(SkUiCoreNode).GetMethods().FirstOrDefault(m => m is { Name: "SetValue", IsGenericMethodDefinition: true })?.MakeGenericMethod(typeof(int));
    private static readonly BindableProperty? SkUiLinesProperty = typeof(SkUiShimmer).GetField("SkeletonLinesProperty")?.GetValue(null) as BindableProperty;

    private readonly List<(object Name, object Message, object Value)> _labels = [];
    private object? _shimmer;

    public override string Name => $"skel-{(skui ? "skui-" : "")}{(source == SkeletonSource.Content ? "content" : "static")}-{(loaded ? "loaded" : "wave")}"
        + (mode == "Bones" ? "" : "-" + mode.ToLowerInvariant());

    public override string Description => $"60 feed cards loading ({(skui ? "SkUi*" : "Core")}, {mode}), skeleton {(source == SkeletonSource.Content ? "from the content" : "hand-made bones")}; "
        + (loaded ? "update = data arrives (texts + IsLoading = false)" : "motion = the wave while loading");

    // Timed updates wait for the next frame: a running wave would supply one before the update's own.
    private void StandStill(object shimmer)
    {
        if (loaded)
            TrySet(shimmer, "Effect", "None");
    }

    private static void CoreLines(SkUiCoreNode node, int lines) => CoreSetValue?.Invoke(node, [CoreLinesProperty, lines]);

    // ---- Core ------------------------------------------------------------------------------------------------------

    private SkUiCoreBorder CoreCard(SkUiCoreNode avatar, SkUiCoreNode name, SkUiCoreNode message, SkUiCoreNode value)
    {
        var text = new SkUiCoreVerticalStackLayout { Spacing = 4 }.Add(name).Add(message);
        var grid = new SkUiCoreGrid()
            .SetColumnDefinitions([new SkUiCoreColumnDefinition(SkUiCoreGridLength.Auto), new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star),
                new SkUiCoreColumnDefinition(SkUiCoreGridLength.Auto)])
            .SetColumnSpacing(12);
        grid.Add(avatar);
        grid.Add(text);
        grid.SetColumn(text, 1);
        grid.Add(value);
        grid.SetColumn(value, 2);
        return new SkUiCoreBorder().SetBackgroundColor(Colors.White).SetStroke(Colors.LightGray).SetStrokeThickness(1).SetCornerRadius(8)
            .SetPadding(new Thickness(16)).SetContent(grid);
    }

    private SkUiCoreBorder CoreContentCard()
    {
        var name = new SkUiCoreLabel { FontSize = 15, FontAttributes = FontAttributes.Bold };
        var message = new SkUiCoreLabel { FontSize = 13, MaxLines = 2 };
        CoreLines(message, 2);
        var value = new SkUiCoreLabel { FontSize = 15, Width = 48 };
        if (loaded)
            _labels.Add((name, message, value));
        var avatar = new SkUiCoreBox { Color = Colors.SteelBlue, CornerRadius = 16, Width = 32, Height = 32, VerticalAlignment = LayoutAlignment.Start };
        return CoreCard(avatar, name, message, value);
    }

    private static SkUiCoreShimmerBone CoreBone(double width, double height, double radius, LayoutAlignment alignment = LayoutAlignment.Start) =>
        new() { CornerRadius = new CornerRadius(radius), Width = width, Height = height, HorizontalAlignment = alignment, VerticalAlignment = LayoutAlignment.Start };

    private static SkUiCoreBorder CoreSkeletonCard(SkeletonCompare self)
    {
        var message = new SkUiCoreVerticalStackLayout { Spacing = 7 }
            .Add(CoreBone(double.NaN, 10, 4, LayoutAlignment.Fill))
            .Add(CoreBone(160, 10, 4));
        return self.CoreCard(CoreBone(32, 32, 16), CoreBone(110, 12, 4), message, CoreBone(48, 12, 4));
    }

    private View BuildCore()
    {
        var cards = new SkUiCoreVerticalStackLayout { Spacing = 12, Padding = new Thickness(12) };
        for (var i = 0; i < Cards; i++)
            cards.Add(CoreContentCard());
        var shimmer = new SkUiCoreShimmer().SetContent(cards);
        TrySet(shimmer, "Mode", mode);
        StandStill(shimmer);
        if (source == SkeletonSource.Static)
        {
            var skeleton = new SkUiCoreVerticalStackLayout { Spacing = 12, Padding = new Thickness(12) };
            for (var i = 0; i < Cards; i++)
                skeleton.Add(CoreSkeletonCard(this));
            shimmer.SetSkeletonContent(skeleton);
        }
        _shimmer = loaded ? shimmer : null;
        return Scenarios.Scroll(new SkUiCoreHost().SetContent(shimmer));
    }

    // ---- SkUi* -----------------------------------------------------------------------------------------------------

    private static SkUiBorder SkUiCard(SkUiView avatar, SkUiView name, SkUiView message, SkUiView value)
    {
        var text = new SkUiVerticalStackLayout { Spacing = 4 };
        text.Children.Add(name);
        text.Children.Add(message);
        var grid = new SkUiGrid { ColumnSpacing = 12 };
        grid.ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star),
            new ColumnDefinition(GridLength.Auto));
        grid.Children.Add(avatar);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        Grid.SetColumn(value, 2);
        grid.Children.Add(value);
        return new SkUiBorder
        {
            BackgroundColor = Colors.White, Stroke = Colors.LightGray, StrokeThickness = 1, CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16), Content = grid
        };
    }

    private SkUiBorder SkUiContentCard()
    {
        var name = new SkUiLabel { FontSize = 15, FontAttributes = FontAttributes.Bold };
        var message = new SkUiLabel { FontSize = 13, MaxLines = 2 };
        if (SkUiLinesProperty is not null)
            message.SetValue(SkUiLinesProperty, 2);
        var value = new SkUiLabel { FontSize = 15, WidthRequest = 48 };
        if (loaded)
            _labels.Add((name, message, value));
        var avatar = new SkUiBox { Color = Colors.SteelBlue, CornerRadius = new CornerRadius(16), WidthRequest = 32, HeightRequest = 32, VerticalOptions = LayoutOptions.Start };
        return SkUiCard(avatar, name, message, value);
    }

    private static SkUiShimmerBone SkUiBone(double width, double height, double radius, LayoutOptions? horizontal = null) =>
        new() { CornerRadius = new CornerRadius(radius), WidthRequest = width, HeightRequest = height, HorizontalOptions = horizontal ?? LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };

    private static SkUiBorder SkUiSkeletonCard()
    {
        var message = new SkUiVerticalStackLayout { Spacing = 7 };
        message.Children.Add(SkUiBone(-1, 10, 4, LayoutOptions.Fill));
        message.Children.Add(SkUiBone(160, 10, 4));
        return SkUiCard(SkUiBone(32, 32, 16), SkUiBone(110, 12, 4), message, SkUiBone(48, 12, 4));
    }

    private View BuildSkUi()
    {
        var cards = new SkUiVerticalStackLayout { Spacing = 12, Padding = new Thickness(12) };
        for (var i = 0; i < Cards; i++)
            cards.Children.Add(SkUiContentCard());
        var shimmer = new SkUiShimmer { Content = cards };
        TrySet(shimmer, "Mode", mode);
        StandStill(shimmer);
        if (source == SkeletonSource.Static)
        {
            var skeleton = new SkUiVerticalStackLayout { Spacing = 12, Padding = new Thickness(12) };
            for (var i = 0; i < Cards; i++)
                skeleton.Children.Add(SkUiSkeletonCard());
            shimmer.SkeletonContent = skeleton;
        }
        _shimmer = loaded ? shimmer : null;
        return Scenarios.Scroll(shimmer);
    }

    [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicFields
        | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties, typeof(SkUiCoreShimmer))]
    [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicFields
        | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties, typeof(SkUiShimmer))]
    [System.Diagnostics.CodeAnalysis.DynamicDependency(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods, typeof(SkUiCoreNode))]
    public override View Build()
    {
        _labels.Clear();
        return skui ? BuildSkUi() : BuildCore();
    }

    public override Action<View>? Update => !loaded ? null : _ =>
    {
        for (var index = 0; index < _labels.Count; index++)
        {
            var (name, message, value) = _labels[index];
            var texts = ($"{Avatar} {index}", Messages[index % Messages.Length], $"{index * 7 % 100} pts");
            if (name is SkUiCoreLabel coreName)
            {
                coreName.Text = texts.Item1;
                ((SkUiCoreLabel)message).Text = texts.Item2;
                ((SkUiCoreLabel)value).Text = texts.Item3;
            }
            else
            {
                ((SkUiLabel)name).Text = texts.Item1;
                ((SkUiLabel)message).Text = texts.Item2;
                ((SkUiLabel)value).Text = texts.Item3;
            }
        }
        switch (_shimmer)
        {
            case SkUiCoreShimmer core: core.IsLoading = false; break;
            case SkUiShimmer view: view.IsLoading = false; break;
        }
        _labels.Clear();
        _shimmer = null;
    };

    public override Func<View, IDisposable?>? Motion => loaded ? null : _ => null;
}

/// <summary>
/// Investigation: what an active content skeleton costs controls outside it. 1,000 labels eight layouts deep change their
/// texts (re-measure and re-record) next to a one-card shimmer that shows its content as a skeleton (<c>on</c>) or has loaded
/// (<c>off</c>). Before C9b both draw the card as it is.
/// </summary>
public sealed class SkeletonTax(bool active, bool skui) : BenchScenario
{
    private const int Depth = 8;
    private readonly List<object> _labels = [];
    private int _round;

    public override string Name => $"skel-tax-{(skui ? "skui-" : "")}{(active ? "on" : "off")}";

    public override string Description => $"1,000 {(skui ? "SkUiLabel" : "SkUiCoreLabel")} {Depth} layouts deep change their texts beside a one-card content skeleton {(active ? "loading" : "loaded")}";

    public override View Build()
    {
        _labels.Clear();
        return skui ? BuildSkUi() : BuildCore();
    }

    private View BuildCore()
    {
        var grid = Scenarios.CoreGrid(Scenarios.Count);
        grid.StartUpdating();
        for (var i = 0; i < Scenarios.Count; i++)
        {
            var label = new SkUiCoreLabel { Text = $"Label {i:0000}", FontSize = 14 };
            _labels.Add(label);
            grid.Add(label, i / 2, i % 2);
        }
        grid.EndUpdating();
        SkUiCoreNode nested = grid;
        for (var level = 0; level < Depth; level++)
            nested = new SkUiCoreVerticalStackLayout().Add(nested);
        var card = new SkUiCoreBorder().SetPadding(new Thickness(16)).SetContent(new SkUiCoreVerticalStackLayout()
            .Add(new SkUiCoreLabel { Text = "Loading", FontSize = 15 }).Add(new SkUiCoreLabel { Text = "A message", FontSize = 13 }));
        var shimmer = new SkUiCoreShimmer { IsLoading = active }.SetContent(card);
        TrySet(shimmer, "Mode", "Bones");
        TrySet(shimmer, "Effect", "None"); // no frames of its own: the update's frame is the one timed
        return Scenarios.Scroll(new SkUiCoreHost().SetContent(new SkUiCoreVerticalStackLayout().Add(shimmer).Add(nested)));
    }

    private View BuildSkUi()
    {
        var grid = Scenarios.SkUiGrid(Scenarios.Count);
        grid.StartUpdating();
        for (var i = 0; i < Scenarios.Count; i++)
        {
            var label = new SkUiLabel { Text = $"Label {i:0000}", FontSize = 14 };
            _labels.Add(label);
            Grid.SetRow(label, i / 2);
            Grid.SetColumn(label, i % 2);
            grid.Children.Add(label);
        }
        grid.EndUpdating();
        SkUiView nested = grid;
        for (var level = 0; level < Depth; level++)
        {
            var stack = new SkUiVerticalStackLayout();
            stack.Children.Add(nested);
            nested = stack;
        }
        var text = new SkUiVerticalStackLayout();
        text.Children.Add(new SkUiLabel { Text = "Loading", FontSize = 15 });
        text.Children.Add(new SkUiLabel { Text = "A message", FontSize = 13 });
        var shimmer = new SkUiShimmer { IsLoading = active, Content = new SkUiBorder { Padding = new Thickness(16), Content = text } };
        TrySet(shimmer, "Mode", "Bones");
        TrySet(shimmer, "Effect", "None"); // no frames of its own: the update's frame is the one timed
        var root = new SkUiVerticalStackLayout();
        root.Children.Add(shimmer);
        root.Children.Add(nested);
        return Scenarios.Scroll(root);
    }

    public override Action<View>? Update => _ =>
    {
        _round++;
        for (var index = 0; index < _labels.Count; index++)
        {
            var text = $"Item {(index + _round) % 10000:0000}";
            if (_labels[index] is SkUiCoreLabel core)
                core.Text = text;
            else
                ((SkUiLabel)_labels[index]).Text = text;
        }
        _labels.Clear();
    };
}
