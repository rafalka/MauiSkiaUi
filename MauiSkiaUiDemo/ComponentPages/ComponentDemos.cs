using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Catalog of every dedicated component demo page.</summary>
public static class ComponentDemos
{
    /// <summary>All demos in gallery display order within each category.</summary>
    public static IReadOnlyList<ComponentDemo> All { get; } =
    [
        new(typeof(SkUiView), typeof(ViewDemoPage), ComponentDemo.SkUiOnlyCounterpart, ComponentCategory.BasicControls, () => new ViewDemoPage()),
        new(typeof(SkUiLabel), typeof(LabelDemoPage), nameof(Label), ComponentCategory.BasicControls, () => new LabelDemoPage()),
        new(typeof(SkUiButton), typeof(ButtonDemoPage), nameof(Button), ComponentCategory.BasicControls, () => new ButtonDemoPage()),
        new(typeof(SkUiImage), typeof(ImageDemoPage), nameof(Image), ComponentCategory.BasicControls, () => new ImageDemoPage()),
        new(typeof(SkUiImageButton), typeof(ImageButtonDemoPage), nameof(ImageButton), ComponentCategory.BasicControls, () => new ImageButtonDemoPage()),
        new(typeof(SkUiActivityIndicator), typeof(ActivityIndicatorDemoPage), nameof(ActivityIndicator), ComponentCategory.BasicControls, () => new ActivityIndicatorDemoPage()),
        new(typeof(SkUiSwitch), typeof(SwitchDemoPage), nameof(Switch), ComponentCategory.BasicControls, () => new SwitchDemoPage()),
        new(typeof(SkUiCheckBox), typeof(CheckBoxDemoPage), nameof(CheckBox), ComponentCategory.BasicControls, () => new CheckBoxDemoPage()),
        new(typeof(SkUiRadioButton), typeof(RadioButtonDemoPage), nameof(RadioButton), ComponentCategory.BasicControls, () => new RadioButtonDemoPage()),
        new(typeof(SkUiContentView), typeof(ContentViewDemoPage), nameof(ContentView), ComponentCategory.Layouts, () => new ContentViewDemoPage()),
        new(typeof(SkUiMauiContentView), typeof(MauiContentViewDemoPage), "Editor / WebView (hosted natively)", ComponentCategory.Layouts, () => new MauiContentViewDemoPage()),
        new(typeof(SkUiBorder), typeof(BorderDemoPage), nameof(Border), ComponentCategory.Layouts, () => new BorderDemoPage()),
        new(typeof(SkUiLayout), typeof(LayoutDemoPage), ComponentDemo.SkUiOnlyCounterpart, ComponentCategory.Layouts, () => new LayoutDemoPage()),
        new(typeof(SkUiGrid), typeof(GridDemoPage), nameof(Grid), ComponentCategory.Layouts, () => new GridDemoPage()),
        new(typeof(SkUiVerticalStackLayout), typeof(VerticalStackLayoutDemoPage), nameof(VerticalStackLayout), ComponentCategory.Layouts, () => new VerticalStackLayoutDemoPage()),
        new(typeof(SkUiHorizontalStackLayout), typeof(HorizontalStackLayoutDemoPage), nameof(HorizontalStackLayout), ComponentCategory.Layouts, () => new HorizontalStackLayoutDemoPage()),
        new(typeof(SkUiAbsoluteLayout), typeof(AbsoluteLayoutDemoPage), nameof(AbsoluteLayout), ComponentCategory.Layouts, () => new AbsoluteLayoutDemoPage()),
        new(typeof(SkUiBox), typeof(BoxDemoPage), nameof(BoxView), ComponentCategory.Graphics, () => new BoxDemoPage()),
        new(typeof(SkUiEllipse), typeof(EllipseDemoPage), nameof(Microsoft.Maui.Controls.Shapes.Ellipse), ComponentCategory.Graphics, () => new EllipseDemoPage()),
        new(typeof(SkUiLine), typeof(LineDemoPage), nameof(Microsoft.Maui.Controls.Shapes.Line), ComponentCategory.Graphics, () => new LineDemoPage()),
        new(typeof(SkUiScrollView), typeof(ScrollViewDemoPage), nameof(ScrollView), ComponentCategory.ScrollingAndCollections, () => new ScrollViewDemoPage())
    ];
}
