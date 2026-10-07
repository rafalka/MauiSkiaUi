using MauiSkiaUi;
using MauiSkiaUi.Core;

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
        new(typeof(SkUiSlider), typeof(SliderDemoPage), nameof(Slider), ComponentCategory.BasicControls, () => new SliderDemoPage()),
        new(typeof(SkUiProgressBar), typeof(ProgressBarDemoPage), nameof(ProgressBar), ComponentCategory.BasicControls, () => new ProgressBarDemoPage()),
        new(typeof(SkUiRadioButton), typeof(RadioButtonDemoPage), nameof(RadioButton), ComponentCategory.BasicControls, () => new RadioButtonDemoPage()),
        new(typeof(SkUiContentPresenter), typeof(RadioButtonTemplateDemoPage), "RadioButton ControlTemplate (ContentPresenter)", ComponentCategory.BasicControls, () => new RadioButtonTemplateDemoPage()),
        new(typeof(SkUiContentView), typeof(ContentViewDemoPage), nameof(ContentView), ComponentCategory.Layouts, () => new ContentViewDemoPage()),
        new(typeof(SkUiAlternateContentView), typeof(AlternateContentViewDemoPage), ComponentDemo.SkUiOnlyCounterpart, ComponentCategory.Layouts, () => new AlternateContentViewDemoPage()),
        new(typeof(SkUiExpander), typeof(ExpanderDemoPage), "Community Toolkit Expander", ComponentCategory.Layouts, () => new ExpanderDemoPage()),
        new(typeof(SkUiExpander), typeof(ExpanderNestingDemoPage), "Community Toolkit Expander (nested, in a scroller)", ComponentCategory.Layouts,
            () => new ExpanderNestingDemoPage(), Key: "SkUiExpanderNested"),
        new(typeof(SkUiMauiContentView), typeof(MauiContentViewDemoPage), "Editor / WebView (hosted natively)", ComponentCategory.Layouts, () => new MauiContentViewDemoPage()),
        new(typeof(SkUiBorder), typeof(BorderDemoPage), nameof(Border), ComponentCategory.Layouts, () => new BorderDemoPage()),
        new(typeof(SkUiLayout), typeof(LayoutDemoPage), ComponentDemo.SkUiOnlyCounterpart, ComponentCategory.Layouts, () => new LayoutDemoPage()),
        new(typeof(SkUiGrid), typeof(GridDemoPage), nameof(Grid), ComponentCategory.Layouts, () => new GridDemoPage()),
        new(typeof(SkUiGrid), typeof(StateContainerDemoPage), "Community Toolkit StateContainer", ComponentCategory.Layouts,
            () => new StateContainerDemoPage(), Key: "SkUiStateContainer"),
        new(typeof(SkUiVerticalStackLayout), typeof(VerticalStackLayoutDemoPage), nameof(VerticalStackLayout), ComponentCategory.Layouts, () => new VerticalStackLayoutDemoPage()),
        new(typeof(SkUiHorizontalStackLayout), typeof(HorizontalStackLayoutDemoPage), nameof(HorizontalStackLayout), ComponentCategory.Layouts, () => new HorizontalStackLayoutDemoPage()),
        new(typeof(SkUiAbsoluteLayout), typeof(AbsoluteLayoutDemoPage), nameof(AbsoluteLayout), ComponentCategory.Layouts, () => new AbsoluteLayoutDemoPage()),
        new(typeof(SkUiFlexLayout), typeof(FlexLayoutDemoPage), nameof(FlexLayout), ComponentCategory.Layouts, () => new FlexLayoutDemoPage()),
        new(typeof(SkUiWrapLayout), typeof(WrapLayoutDemoPage), ComponentDemo.SkUiOnlyCounterpart, ComponentCategory.Layouts, () => new WrapLayoutDemoPage()),
        new(typeof(SkUiHorizontalShrinkLayout), typeof(HorizontalShrinkLayoutDemoPage), ComponentDemo.SkUiOnlyCounterpart, ComponentCategory.Layouts, () => new HorizontalShrinkLayoutDemoPage()),
        new(typeof(SkUiVerticalShrinkLayout), typeof(VerticalShrinkLayoutDemoPage), ComponentDemo.SkUiOnlyCounterpart, ComponentCategory.Layouts, () => new VerticalShrinkLayoutDemoPage()),
        new(typeof(SkUiBox), typeof(BoxDemoPage), nameof(BoxView), ComponentCategory.Graphics, () => new BoxDemoPage()),
        new(typeof(SkUiEllipse), typeof(EllipseDemoPage), nameof(Microsoft.Maui.Controls.Shapes.Ellipse), ComponentCategory.Graphics, () => new EllipseDemoPage()),
        new(typeof(SkUiLine), typeof(LineDemoPage), nameof(Microsoft.Maui.Controls.Shapes.Line), ComponentCategory.Graphics, () => new LineDemoPage()),
        new(typeof(SkUiRectangle), typeof(RectangleDemoPage), nameof(Microsoft.Maui.Controls.Shapes.Rectangle), ComponentCategory.Graphics, () => new RectangleDemoPage()),
        new(typeof(SkUiRoundRectangle), typeof(RoundRectangleDemoPage), nameof(Microsoft.Maui.Controls.Shapes.RoundRectangle), ComponentCategory.Graphics, () => new RoundRectangleDemoPage()),
        new(typeof(SkUiPolygon), typeof(PolygonDemoPage), nameof(Microsoft.Maui.Controls.Shapes.Polygon), ComponentCategory.Graphics, () => new PolygonDemoPage()),
        new(typeof(SkUiPolyline), typeof(PolylineDemoPage), nameof(Microsoft.Maui.Controls.Shapes.Polyline), ComponentCategory.Graphics, () => new PolylineDemoPage()),
        new(typeof(SkUiPath), typeof(PathDemoPage), nameof(Microsoft.Maui.Controls.Shapes.Path), ComponentCategory.Graphics, () => new PathDemoPage()),
        new(typeof(SkUiScrollView), typeof(ScrollViewDemoPage), nameof(ScrollView), ComponentCategory.ScrollingAndCollections, () => new ScrollViewDemoPage()),
        new(typeof(SkUiMauiContentView), typeof(OverlayScrollingDemoPage), "Native overlays in ScrollView", ComponentCategory.ScrollingAndCollections,
            () => new OverlayScrollingDemoPage(), Key: "OverlaysInScrollView"),
        new(typeof(SkUiVirtualScrollView), typeof(VirtualScrollViewDemoPage), nameof(CollectionView), ComponentCategory.ScrollingAndCollections, () => new VirtualScrollViewDemoPage()),
        new(typeof(SkUiVirtualVerticalStackLayout), typeof(VirtualVerticalStackLayoutDemoPage), ComponentDemo.SkUiOnlyCounterpart, ComponentCategory.ScrollingAndCollections, () => new VirtualVerticalStackLayoutDemoPage()),
        new(typeof(SkUiCoreGrid), typeof(CoreGridDemoPage), ComponentDemo.SkUiOnlyCounterpart, ComponentCategory.Core, () => new CoreGridDemoPage()),
        new(typeof(SkUiCoreTable), typeof(CoreTableDemoPage), ComponentDemo.SkUiOnlyCounterpart, ComponentCategory.Core, () => new CoreTableDemoPage()),
        new(typeof(SkUiCoreWrapLayout), typeof(CoreWrapLayoutDemoPage), ComponentDemo.SkUiOnlyCounterpart, ComponentCategory.Core, () => new CoreWrapLayoutDemoPage()),
        new(typeof(SkUiCoreHorizontalShrinkLayout), typeof(CoreHorizontalShrinkLayoutDemoPage), ComponentDemo.SkUiOnlyCounterpart, ComponentCategory.Core, () => new CoreHorizontalShrinkLayoutDemoPage()),
        new(typeof(SkUiCoreVerticalShrinkLayout), typeof(CoreVerticalShrinkLayoutDemoPage), ComponentDemo.SkUiOnlyCounterpart, ComponentCategory.Core, () => new CoreVerticalShrinkLayoutDemoPage()),
        new(typeof(SkUiCoreScrollView), typeof(CoreScrollViewDemoPage), "ScrollView + gestures", ComponentCategory.Core, () => new CoreScrollViewDemoPage()),
        new(typeof(SkUiCoreBorder), typeof(CorePressEffectDemoPage), "Composite buttons (press effect)", ComponentCategory.Core,
            () => new CorePressEffectDemoPage(), Key: "CompositeButtons")
    ];

    /// <summary>MAUI-compatible <c>SkUi*</c> demos shown on the Components flyout.</summary>
    public static IEnumerable<ComponentDemo> MauiCompatible =>
        All.Where(demo => demo.Category != ComponentCategory.Core);

    /// <summary>Core-layer demos shown on the Core flyout.</summary>
    public static IEnumerable<ComponentDemo> Core =>
        All.Where(demo => demo.Category == ComponentCategory.Core);
}
