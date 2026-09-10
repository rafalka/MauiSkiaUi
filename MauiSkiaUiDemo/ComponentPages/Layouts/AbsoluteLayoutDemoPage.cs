using MauiSkiaUi;
using Microsoft.Maui.Layouts;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiAbsoluteLayout"/>.</summary>
public sealed class AbsoluteLayoutDemoPage : ComponentDemoPage
{
    public AbsoluteLayoutDemoPage() : base(nameof(SkUiAbsoluteLayout), new SkUiAbsoluteLayout(), new AbsoluteLayout())
    {
        var skia = (SkUiAbsoluteLayout)SkiaControl;
        var native = (AbsoluteLayout)NativeControl!;
        var drawn = new SkUiBox { Color = Accent };
        var standard = new BoxView { Color = Accent };
        SkUiAbsoluteLayout.SetLayoutBounds(drawn, new Rect(0.1, 0.1, 60, 40));
        AbsoluteLayout.SetLayoutBounds(standard, new Rect(0.1, 0.1, 60, 40));
        AbsoluteLayout.SetLayoutFlags(standard, AbsoluteLayoutFlags.PositionProportional);
        SkUiAbsoluteLayout.SetLayoutFlags(drawn, AbsoluteLayoutFlags.PositionProportional);
        skia.Children.Add(drawn);
        native.Add(standard);
        Number(nameof(Rect.X), 0, 1, 0.1, value => { SkUiAbsoluteLayout.SetLayoutBounds(drawn, new Rect(value, SkUiAbsoluteLayout.GetLayoutBounds(drawn).Y, 60, 40)); AbsoluteLayout.SetLayoutBounds(standard, new Rect(value, AbsoluteLayout.GetLayoutBounds(standard).Y, 60, 40)); },
            () => SkUiAbsoluteLayout.GetLayoutBounds(drawn).X, () => AbsoluteLayout.GetLayoutBounds(standard).X);
        Number(nameof(Rect.Y), 0, 1, 0.1, value => { SkUiAbsoluteLayout.SetLayoutBounds(drawn, new Rect(SkUiAbsoluteLayout.GetLayoutBounds(drawn).X, value, 60, 40)); AbsoluteLayout.SetLayoutBounds(standard, new Rect(AbsoluteLayout.GetLayoutBounds(standard).X, value, 60, 40)); },
            () => SkUiAbsoluteLayout.GetLayoutBounds(drawn).Y, () => AbsoluteLayout.GetLayoutBounds(standard).Y);
    }
}
