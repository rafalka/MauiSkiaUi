using System.Collections.ObjectModel;
using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// Side-by-side property playground for <see cref="SkUiVerticalStackLayout"/>. Both stacks get their children from
/// MAUI's <see cref="BindableLayout"/> over one collection, with an empty view.
/// </summary>
public sealed class VerticalStackLayoutDemoPage : ComponentDemoPage
{
    private static readonly Color[] Palette = [Accent, DemoColors.SampleA, DemoColors.SampleB];
    private readonly ObservableCollection<Color> _items = [];

    public VerticalStackLayoutDemoPage() : base(nameof(SkUiVerticalStackLayout), new SkUiVerticalStackLayout(), new VerticalStackLayout())
    {
        var skia = (SkUiVerticalStackLayout)SkiaControl;
        var native = (VerticalStackLayout)NativeControl!;
        FillItems();
        BindableLayout.SetItemTemplate(skia, new DataTemplate(() =>
        {
            var box = new SkUiBox { HeightRequest = 24 };
            box.BindingContextChanged += (_, _) => box.Color = box.BindingContext as Color ?? Colors.Transparent;
            return box;
        }));
        BindableLayout.SetItemTemplate(native, new DataTemplate(() =>
        {
            var box = new BoxView { HeightRequest = 24 };
            box.BindingContextChanged += (_, _) => box.Color = box.BindingContext as Color ?? Colors.Transparent;
            return box;
        }));
        BindableLayout.SetEmptyView(skia, new SkUiLabel { Text = "No items" });
        BindableLayout.SetEmptyView(native, new Label { Text = "No items" });
        BindableLayout.SetItemsSource(skia, _items);
        BindableLayout.SetItemsSource(native, _items);

        Number(nameof(SkUiVerticalStackLayout.Spacing), 0, 24, 6, value => { skia.Spacing = value; native.Spacing = value; }, () => skia.Spacing, () => native.Spacing);
        ActionButton("Add item", () => _items.Add(Palette[_items.Count % Palette.Length]));
        ActionButton("Remove first item", () => { if (_items.Count > 0) _items.RemoveAt(0); });
        ActionButton("Clear items", _items.Clear);
        OnReset(FillItems);
    }

    private void FillItems()
    {
        _items.Clear();
        foreach (var color in Palette)
            _items.Add(color);
    }
}
