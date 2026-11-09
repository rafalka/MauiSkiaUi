using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiCoreExpander"/>: FAQ sections of Core nodes whose answers grow from the header (scaled vertically, the
/// height following on the UI clock); the chevron turns with the expander's length and easing.
/// </summary>
public sealed class CoreExpanderDemoPage : ComponentDemoPage
{
    private static readonly string[] Questions = ["What is the Core layer?", "Does it animate?", "Can sections nest?"];
    private static readonly string[] Answers =
    [
        "Plain CLR nodes with fluent setters: no bindable store, the fastest way to build large drawn trees.",
        "The content grows from the header while the expander's height follows; only resized nodes are recorded again.",
        "Yes: an expander's content can hold more expanders, each laid out through the others."
    ];

    private static readonly Easing[] Easings = [Easing.CubicInOut, Easing.CubicOut, Easing.SpringOut, Easing.Linear];
    private static readonly string[] EasingNames = [nameof(Easing.CubicInOut), nameof(Easing.CubicOut), nameof(Easing.SpringOut), nameof(Easing.Linear)];

    public CoreExpanderDemoPage()
        : base(nameof(SkUiCoreExpander), CreateHost(out var sections), native: null, widthRange: (160, 400, 300), heightRange: (120, 480, 320))
    {
        Number(nameof(SkUiCoreExpander.AnimationLength), 0, 1000, 250, value => Each(sections, section => section.SetAnimationLength((uint)value)),
            () => sections[0].AnimationLength, whole: true);
        Choice(nameof(SkUiCoreExpander.AnimationEasing), EasingNames, EasingNames[0],
            value => Each(sections, section => section.SetAnimationEasing(Easings[Array.IndexOf(EasingNames, value)])),
            () => EasingNames[Math.Max(0, Array.IndexOf(Easings, sections[0].AnimationEasing))]);
        Choice(nameof(SkUiCoreExpander.Direction), [SkUiExpandDirection.Down, SkUiExpandDirection.Up], SkUiExpandDirection.Down,
            value => Each(sections, section => section.SetDirection(value)), () => sections[0].Direction);
        Toggle(nameof(SkUiCoreExpander.LazyContentExpansion), false, value => Each(sections, section => section.SetLazyContentExpansion(value)),
            () => sections[0].LazyContentExpansion);
        ActionButton("Collapse all", () => Each(sections, section => section.SetIsExpanded(false)));
        foreach (var section in sections)
            section.ExpandedChanged += (_, args) => Feedback($"{(args.IsExpanded ? "Expanded" : "Collapsed")}: {((SkUiCoreLabel)((SkUiCoreHorizontalStackLayout)section.Header!).Children[0]).Text}");
    }

    private static void Each(IEnumerable<SkUiCoreExpander> sections, Action<SkUiCoreExpander> action)
    {
        foreach (var section in sections)
            action(section);
    }

    private static SkUiCoreHost CreateHost(out SkUiCoreExpander[] sections)
    {
        var stack = new SkUiCoreVerticalStackLayout().SetSpacing(8).SetPadding(new Thickness(10));
        sections = new SkUiCoreExpander[Questions.Length];
        for (var index = 0; index < Questions.Length; index++)
        {
            var section = Section(Questions[index], Answers[index]);
            sections[index] = section;
            var card = new SkUiCoreBorder().SetStroke(DemoColors.Caption).SetStrokeThickness(1).SetCornerRadius(8).SetContent(section);
            card.SetBackground(Colors.White);
            stack.Add(card);
        }
        var scroll = new SkUiCoreScrollView();
        scroll.SetContent(stack);
        var host = new SkUiCoreHost { BackgroundColor = Colors.LightGray };
        host.SetContent(scroll);
        return host;
    }

    private static SkUiCoreExpander Section(string question, string answer)
    {
        var title = new SkUiCoreLabel().SetText(question).SetTextColor(Ink).SetFontSize(15).SetFontAttributes(FontAttributes.Bold);
        title.SetVerticalAlignment(LayoutAlignment.Center);
        var chevron = new SkUiCoreLabel().SetText("›").SetTextColor(Accent).SetFontSize(20);
        chevron.SetHorizontalAlignment(LayoutAlignment.End).SetVerticalAlignment(LayoutAlignment.Center);
        var header = new SkUiCoreHorizontalStackLayout().SetSpacing(8).SetPadding(new Thickness(12, 10));
        header.Add(title);
        header.Add(chevron);
        var body = new SkUiCoreLabel().SetText(answer).SetTextColor(Ink).SetFontSize(14);
        body.SetMargin(new Thickness(12, 0, 12, 12));
        var section = new SkUiCoreExpander().SetHeader(header).SetContent(body).SetAnimationLength(250);
        // The chevron turns with the expander, on the render thread.
        section.ExpandedChanged += (_, args) => _ = chevron.AnimateAsync(SkUiAnimatableProperty.Rotation, args.IsExpanded ? 90 : 0,
            section.AnimationLength, section.AnimationEasing);
        return section;
    }
}
