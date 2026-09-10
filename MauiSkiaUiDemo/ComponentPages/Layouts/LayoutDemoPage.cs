using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Property playground for <see cref="SkUiLayout"/> z-order and hit-testing.</summary>
public sealed class LayoutDemoPage : ComponentDemoPage
{
    public LayoutDemoPage() : base(nameof(SkUiLayout), new SkUiLayout())
    {
        var layout = (SkUiLayout)SkiaControl;
        var first = new SkUiBox { Color = Accent, Margin = new Thickness(0, 0, 35, 25) };
        var second = new SkUiEllipse { Color = DemoColors.SampleA, Margin = new Thickness(35, 25, 0, 0) };
        layout.Children.Add(first);
        layout.Children.Add(second);
        first.Tapped += (_, _) => Feedback("Tapped box");
        second.Tapped += (_, _) => Feedback("Tapped ellipse");
        Number(nameof(SkUiLayout.Padding), 0, 24, 8, value => layout.Padding = value, () => layout.Padding.Left);
        // Demo-specific labels: base page already exposes Opacity / InputTransparent / ZIndex for the root.
        Toggle("BoxOnTop", false, value => first.ZIndex = value ? 1 : 0, () => first.ZIndex == 1);
        Number("EllipseOpacity", 0, 1, 0.75, value => second.Opacity = value, () => second.Opacity);
        Toggle("EllipseInputTransparent", false, value => second.InputTransparent = value, () => second.InputTransparent);
    }
}
