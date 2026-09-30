using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// Property playground for <see cref="SkUiHorizontalShrinkLayout"/> (white) inside the available space (gray): an
/// icon, two shrinkable labels (the longer keeps more space) and a fixed label. While the row fits it is as wide as
/// its content, like a horizontal stack; narrow the width below that and the shrinkable labels shrink.
/// </summary>
public sealed class HorizontalShrinkLayoutDemoPage : ComponentDemoPage
{
    public HorizontalShrinkLayoutDemoPage()
        : base(nameof(SkUiHorizontalShrinkLayout), new SkUiContentView(), widthRange: (100, 480, 420), heightRange: (40, 200, 80))
    {
        var area = (SkUiContentView)SkiaControl;
        area.BackgroundColor = Colors.LightGray;
        var row = new SkUiHorizontalShrinkLayout
        {
            BackgroundColor = Colors.White, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Center
        };
        var first = Label("A first label", "#BFDBFE");
        var second = Label("A longer second label", "#BBF7D0");
        SkUiShrinkLayout.SetShrink(first, SkUiShrinkFactor.Auto);
        SkUiShrinkLayout.SetShrink(second, SkUiShrinkFactor.Auto);
        row.Children.Add(new SkUiBox { Color = Accent, WidthRequest = 32, HeightRequest = 32, VerticalOptions = LayoutOptions.Center });
        row.Children.Add(first);
        row.Children.Add(second);
        row.Children.Add(Label("Fixed", "#FBCFE8"));
        area.Content = row;

        var state = new ShrinkLayoutDemoState(vertical: false);
        void Update() => state.Update(new Size(row.Width, row.Height), new Size(area.Width, area.Height));
        row.SizeChanged += (_, _) => Update();
        area.SizeChanged += (_, _) => Update();
        AddEditor("Layout", state.View);

        Number(nameof(SkUiShrinkLayout.Spacing), 0, 24, 8, value => row.Spacing = value, () => row.Spacing);
        Number(nameof(SkUiShrinkLayout.Padding), 0, 32, 4, value => row.Padding = value, () => row.Padding.Left);
        // Edit the text to watch the row switch between a plain stack (fits) and shrinking (too long).
        Text("Label2.Text", second.Text, value => second.Text = value, () => second.Text);
        Choice<SkUiShrinkFactor>("Label1.Shrink", [SkUiShrinkFactor.None, SkUiShrinkFactor.Auto, 1, 2], SkUiShrinkFactor.Auto,
            value => SkUiShrinkLayout.SetShrink(first, value), () => SkUiShrinkLayout.GetShrink(first));
        Choice<SkUiShrinkFactor>("Label2.Shrink", [SkUiShrinkFactor.None, SkUiShrinkFactor.Auto, 1, 2], SkUiShrinkFactor.Auto,
            value => SkUiShrinkLayout.SetShrink(second, value), () => SkUiShrinkLayout.GetShrink(second));
        Choice(nameof(SkUiLabel.LineBreakMode), [LineBreakMode.TailTruncation, LineBreakMode.WordWrap], LineBreakMode.TailTruncation,
            value => { first.LineBreakMode = value; second.LineBreakMode = value; }, () => first.LineBreakMode);
    }

    private static SkUiLabel Label(string text, string background) => new()
    {
        Text = text, TextColor = Ink, FontSize = 14, Padding = new Thickness(4), Background = Color.FromArgb(background),
        VerticalTextAlignment = TextAlignment.Center
    };
}
