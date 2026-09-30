using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// Property playground for <see cref="SkUiVerticalShrinkLayout"/> (white) inside the available space (gray): a header,
/// a shrinkable scroll view and a footer. While they fit the layout is as tall as its content, like a vertical stack;
/// lower the height below that and the scroll view shortens while the header and footer keep their height.
/// </summary>
public sealed class VerticalShrinkLayoutDemoPage : ComponentDemoPage
{
    public VerticalShrinkLayoutDemoPage()
        : base(nameof(SkUiVerticalShrinkLayout), new SkUiContentView(), widthRange: (80, 360, 240), heightRange: (60, 480, 320))
    {
        var area = (SkUiContentView)SkiaControl;
        area.BackgroundColor = Colors.LightGray;
        var column = new SkUiVerticalShrinkLayout
        {
            Spacing = 6, BackgroundColor = Colors.White, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Start
        };
        var content = new SkUiLabel
        {
            Text = "Content in a shrinkable scroll view. When header, content and footer do not fit, the scroll view gets the height that is left.",
            TextColor = Ink, FontSize = 14, Padding = new Thickness(8), Background = Color.FromArgb("#BFDBFE"), HeightRequest = 160
        };
        var scroll = new SkUiScrollView { Content = content };
        SkUiShrinkLayout.SetShrink(scroll, SkUiShrinkFactor.Auto);
        column.Children.Add(Bar("Header"));
        column.Children.Add(scroll);
        column.Children.Add(Bar("Footer"));
        area.Content = column;

        var state = new ShrinkLayoutDemoState(vertical: true);
        void Update() => state.Update(new Size(column.Width, column.Height), new Size(area.Width, area.Height));
        column.SizeChanged += (_, _) => Update();
        area.SizeChanged += (_, _) => Update();
        AddEditor("Layout", state.View);

        Number(nameof(SkUiShrinkLayout.Spacing), 0, 24, 6, value => column.Spacing = value, () => column.Spacing);
        Number("Content.HeightRequest", 20, 400, 160, value => content.HeightRequest = value, () => content.HeightRequest);
        Choice<SkUiShrinkFactor>("ScrollView.Shrink", [SkUiShrinkFactor.None, SkUiShrinkFactor.Auto, 1, 2], SkUiShrinkFactor.Auto,
            value => SkUiShrinkLayout.SetShrink(scroll, value), () => SkUiShrinkLayout.GetShrink(scroll));
    }

    private static SkUiLabel Bar(string text) => new()
    {
        Text = text, TextColor = Colors.White, FontSize = 14, Padding = new Thickness(8), Background = Accent,
        HorizontalTextAlignment = TextAlignment.Center
    };
}
