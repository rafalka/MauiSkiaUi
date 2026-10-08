using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiExpander"/> in a complex tree: a scroll view of bordered sections, each an expander whose content
/// holds text, controls and a bordered expander of the next level (three levels). Expanding moves everything below
/// it, re-measures the scroller's extent and keeps the controls inside working; the editors apply to every expander.
/// </summary>
public sealed class ExpanderNestingDemoPage : ComponentDemoPage
{
    private const int Sections = 12;
    private const int Levels = 3;
    private static readonly Color[] LevelBackgrounds = [Colors.White, DemoColors.SoftSurface, DemoColors.StressAlt];
    private readonly List<SkUiExpander> _expanders = [];
    private int _clicks;

    public ExpanderNestingDemoPage()
        : base("SkUiExpander (nested, in a scroller)", new SkUiScrollView(), widthRange: (220, 420, 340), heightRange: (200, 900, 360))
    {
        // Phones: the preview leaves room for the editors below it.
        SinglePanelHeight = 420;
        var stack = new SkUiVerticalStackLayout { Spacing = 10, Padding = new Thickness(10) };
        for (var index = 0; index < Sections; index++)
            stack.Children.Add(Section($"Section {index + 1}", level: 1));
        ((SkUiScrollView)SkiaControl).Content = stack;
        foreach (var expander in _expanders)
            expander.ExpandedChanged += (_, _) => Report();

        // First, so they are on screen without scrolling the editors.
        AddEditor("All expanders", new HorizontalStackLayout
        {
            Spacing = 8,
            Children = { HeaderButton("Expand all", () => ForAll(e => e.IsExpanded = true)), HeaderButton("Collapse all", () => ForAll(e => e.IsExpanded = false)) }
        });
        Number(nameof(SkUiExpander.AnimationLength), 0, 800, 300, value => ForAll(e => e.AnimationLength = (uint)value), () => _expanders[0].AnimationLength, whole: true);
        Choice(nameof(SkUiExpander.AnimationEasing), DemoExpanders.EasingNames, "CubicInOut",
            value => ForAll(e => e.AnimationEasing = DemoExpanders.EasingNamed(value)), () => DemoExpanders.NameOf(_expanders[0].AnimationEasing));
        Toggle(nameof(SkUiExpander.LazyContentExpansion), false, value => ForAll(e => e.LazyContentExpansion = value), () => _expanders[0].LazyContentExpansion);
    }

    /// <summary>A button above the preview (not an editor row, as the base page's <c>ActionButton</c>).</summary>
    private static Button HeaderButton(string title, Action action)
    {
        var button = new Button { Text = title, Background = Accent, TextColor = Colors.White, AutomationId = title.Replace(" ", "") };
        button.Clicked += (_, _) => action();
        return button;
    }

    private void ForAll(Action<SkUiExpander> apply)
    {
        foreach (var expander in _expanders)
            apply(expander);
    }

    // A bordered expander; its content has text, controls and (above the last level) a bordered expander of the next level.
    private SkUiBorder Section(string title, int level)
    {
        var expander = new SkUiExpander { AnimationLength = 300 };
        _expanders.Add(expander);
        var accent = level == 1 ? Accent : level == 2 ? DemoColors.SampleB : DemoColors.SampleA;
        expander.Header = DemoExpanders.Header(expander, title, level == 1 ? Colors.White : accent,
            level == 1 ? Accent : Colors.Transparent, fontSize: level == 1 ? 16 : 14);
        var content = new SkUiVerticalStackLayout { Spacing = 8, Padding = new Thickness(12, 4, 12, 12) };
        content.Children.Add(new SkUiLabel
        {
            Text = $"{title}: level {level} of {Levels}. Everything below moves while this opens; the controls keep working mid-animation.",
            TextColor = Ink, FontSize = 13, LineBreakMode = LineBreakMode.WordWrap
        });
        var progress = new SkUiProgressBar { Progress = 0.4, ProgressColor = accent };
        var slider = new SkUiSlider { Maximum = 1, Value = 0.4 };
        slider.ValueChanged += (_, e) => progress.Progress = e.NewValue;
        content.Children.Add(slider);
        content.Children.Add(progress);
        content.Children.Add(new SkUiHorizontalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new SkUiSwitch { IsToggled = level % 2 == 1 },
                new SkUiLabel { Text = "Notifications", TextColor = Ink, VerticalOptions = LayoutOptions.Center },
                new SkUiCheckBox { IsChecked = true },
                new SkUiLabel { Text = "Sync", TextColor = Ink, VerticalOptions = LayoutOptions.Center }
            }
        });
        var button = new SkUiButton { Text = $"Action ({title})", FontSize = 13, HorizontalOptions = LayoutOptions.Start };
        button.Clicked += (_, _) => { _clicks++; Report(); };
        content.Children.Add(button);
        if (level < Levels)
            content.Children.Add(Section($"{title}.{level + 1}", level + 1));
        expander.Content = content;
        var border = new SkUiBorder
        {
            Stroke = accent, StrokeThickness = 1, CornerRadius = 10, Background = LevelBackgrounds[level - 1],
            Content = expander
        };
        if (level == 1)
            border.Shadow = new Shadow { Brush = Colors.Black, Opacity = 0.15f, Radius = 6, Offset = new Point(0, 2) };
        return border;
    }

    private void Report() =>
        Feedback($"{_expanders.Count(e => e.IsExpanded)} of {_expanders.Count} expanded · button clicks: {_clicks}");
}
