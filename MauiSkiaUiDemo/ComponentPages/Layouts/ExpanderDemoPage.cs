using System.Diagnostics;
using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// Property playground for <see cref="SkUiExpander"/> (Community Toolkit <c>Expander</c>): tap the header to expand.
/// The content is a card, a card created from <c>ContentTemplate</c> (creations are counted, so
/// <c>LazyContentExpansion</c> shows when it runs), or a native Editor hosted in the drawn tree. A line below the
/// expander shows the views around it moving with the animation.
/// </summary>
public sealed class ExpanderDemoPage : ComponentDemoPage
{
    private static readonly string[] Contents = ["Card", "Template", "Native editor"];
    private readonly SkUiExpander _expander;
    private string _content = "Card";
    private int _templateCards;

    public ExpanderDemoPage()
        : base(nameof(SkUiExpander), CreatePreview(out var expander), widthRange: (160, 360, 280), heightRange: (120, 400, 280))
    {
        _expander = expander;
        SinglePanelHeight = 320;
        expander.Header = Header();
        expander.ExpandedChanged += (_, e) => Report(e.IsExpanded);
        SetContent("Card");

        Toggle(nameof(SkUiExpander.IsExpanded), false, value => expander.IsExpanded = value, () => expander.IsExpanded);
        Choice(nameof(SkUiExpander.Direction), [SkUiExpandDirection.Down, SkUiExpandDirection.Up], SkUiExpandDirection.Down,
            value => expander.Direction = value, () => expander.Direction);
        Number(nameof(SkUiExpander.AnimationLength), 0, 600, 250, value => expander.AnimationLength = (uint)value, () => expander.AnimationLength, whole: true);
        Toggle(nameof(SkUiExpander.LazyContentExpansion), false, value => expander.LazyContentExpansion = value, () => expander.LazyContentExpansion);
        Choice("Content", Contents, "Card", SetContent, () => _content);
    }

    private static SkUiVerticalStackLayout CreatePreview(out SkUiExpander expander)
    {
        expander = new SkUiExpander { AnimationLength = 250 };
        return new SkUiVerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                expander,
                new SkUiLabel { Text = "Below the expander: moves along with it", TextColor = DemoColors.Caption, FontSize = 12 }
            }
        };
    }

    private static SkUiBorder Header() => new()
    {
        Background = Accent, CornerRadius = 8, Padding = new Thickness(12, 10), StrokeThickness = 0,
        Content = new SkUiLabel { Text = "Details (tap me)", TextColor = Colors.White, FontSize = 16, FontAttributes = FontAttributes.Bold }
    };

    private static SkUiBorder Card(string text) => new()
    {
        Stroke = DemoColors.SampleA, StrokeThickness = 1, CornerRadius = 8, Padding = new Thickness(12, 8), Margin = new Thickness(0, 4, 0, 0),
        Background = Colors.White,
        Content = new SkUiVerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new SkUiLabel { Text = text, TextColor = Ink, FontSize = 14 },
                new SkUiLabel { Text = "Item 1", TextColor = Ink },
                new SkUiLabel { Text = "Item 2", TextColor = Ink },
                new SkUiLabel { Text = "Item 3", TextColor = Ink }
            }
        }
    };

    private void SetContent(string value)
    {
        _content = value;
        _expander.Content = null;
        _expander.ContentTemplate = null;
        switch (value)
        {
            case "Template":
                _expander.ContentTemplate = new DataTemplate(() =>
                {
                    _templateCards++;
                    Trace.WriteLine($"ExpanderDemo: template card created ({_templateCards})");
                    return Card($"From ContentTemplate (#{_templateCards})");
                });
                break;
            case "Native editor":
                _expander.Content = new SkUiMauiContentView
                {
                    HeightRequest = 100, Margin = new Thickness(0, 4, 0, 0),
                    Content = new Editor { Placeholder = "A native Editor: hidden while collapsed", Background = Colors.White, TextColor = Ink }
                };
                break;
            default:
                _expander.Content = Card("Explicit Content");
                break;
        }
    }

    private void Report(bool expanded) =>
        Feedback($"IsExpanded {expanded} · template cards created: {_templateCards}");
}
