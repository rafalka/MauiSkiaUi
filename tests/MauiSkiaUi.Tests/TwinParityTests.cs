using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.RegularExpressions;
using MauiSkiaUi.Core;
using Microsoft.Maui.Controls.Shapes;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Every control on both layers stays at parity (decision D3 of the November 2026 review: this test instead of a source
/// generator for the twins). Twins are found by name, <c>SkUiCoreX</c> ↔ <c>SkUiX</c>, in the library and its packages, or
/// listed in <see cref="NamedOtherwise"/>; every other SkUi type is in <see cref="Unpaired"/> with the reason. For each pair:
/// <list type="bullet">
/// <item>each public property the SkUi control declares has a property of the same name and type on the Core twin, and
/// each property the Core twin declares has one on the SkUi control;</item>
/// <item>both layers have a fluent <c>SetX</c> for every writable one;</item>
/// <item>a fresh control and a fresh node start with the same value (numbers, enums, strings, colors, time spans,
/// thicknesses, corner radii, sizes, points, rectangles);</item>
/// <item>both reject the same invalid values (NaN, infinity, -2, <c>null</c>, negative thicknesses and radii): the SkUi
/// <c>SetX</c> throws exactly when the Core <c>SetX</c> does, also for an undefined enum value or flag;</item>
/// <item>public methods and events declared on one layer exist on the other.</item>
/// </list>
/// Members SkUi views inherit from MAUI's <c>VisualElement</c> are not compared with <c>SkUiCoreNode</c>: Core has the
/// ones it draws with (opacity, transforms, background) under its own names.
/// The layers' conventions are rules here, not exceptions: MAUI types on SkUi and Skia / graphics types on Core
/// (<see cref="LayerTypes"/>), views on SkUi and nodes on Core (<c>EmptyView</c> holds an <c>SkUiCoreNode</c>,
/// <c>GetRealizedView</c> is <c>GetRealizedNode</c>), templates and commands on SkUi where Core takes factories and events,
/// MAUI's mutable collections on SkUi and read-only lists on Core. Any other difference that is meant goes in
/// <see cref="IntendedDifferences"/>, with the reason.
/// </summary>
public partial class TwinParityTests
{
    /// <summary>
    /// What one layer has, or does differently, on purpose. Keys: <c>Type.Property</c> or <c>Type.SetX</c> on the type that
    /// lacks the property or setter its twin has; <c>Type.Method(ParameterTypes)</c> or <c>Type.event Name</c> on the type
    /// that has the method or event its twin lacks; <c>Core.Property default</c> / <c>validation</c> / <c>type</c> for a
    /// value the layers treat differently. <see cref="EveryIntendedDifferenceStillHolds"/> fails once a listed difference is
    /// gone.
    /// </summary>
    private static readonly HashSet<string> IntendedDifferences = Flatten(
    [
        ("The SkUi layer gives a property back to the look with ClearValue; Core has no bindable store", [
            "SkUiCoreCalendar.ClearTodayStyle()",
            "SkUiCoreCalendar.ClearSelectionShape()",
        ]),
        ("Core's own store of attached values; SkUi views are bindable objects", [
            "SkUiCoreNode.GetValue(AttachedProperty`1)",
            "SkUiCoreNode.IsSet(AttachedProperty`1)",
            "SkUiCoreNode.ClearValue(AttachedProperty`1)",
            "SkUiCoreNode.SetValue(AttachedProperty`1, T)",
        ]),
        ("Set together on both layers: SetFont(family, size, attributes), SetBadge(color, position, borderColor)", [
            "SkUiAvatarView.SetFontFamily",
            "SkUiAvatarView.SetFontSize",
            "SkUiAvatarView.SetFontAttributes",
            "SkUiAvatarView.SetBadgeColor",
            "SkUiAvatarView.SetBadgePosition",
            "SkUiAvatarView.SetBadgeBorderColor",
            "SkUiCoreAvatar.SetFontFamily",
            "SkUiCoreAvatar.SetFontSize",
            "SkUiCoreAvatar.SetFontAttributes",
            "SkUiCoreAvatar.SetBadgeColor",
            "SkUiCoreAvatar.SetBadgePosition",
            "SkUiCoreAvatar.SetBadgeBorderColor",
        ]),
        ("The Community Toolkit's names for Stroke and StrokeThickness on the SkUi avatar; Core's avatar has its border's", [
            "SkUiCoreAvatar.BorderColor",
            "SkUiCoreAvatar.BorderWidth",
        ]),
        ("MAUI's names and Layout members on the SkUi layer: IsClippedToBounds is ClipToBounds, CascadeInputTransparent is MAUI's Layout's, and an SkUiLayout is MAUI's IList of children (a Core panel has Children)", [
            "SkUiCoreExpander.IsClippedToBounds",
            "SkUiCoreExpander.CascadeInputTransparent",
            "SkUiCoreRadioButton.IsClippedToBounds",
            "SkUiCoreRadioButton.CascadeInputTransparent",
            "SkUiCorePanel.Count",
            "SkUiCorePanel.IsReadOnly",
            "SkUiLayout.AddRange(IEnumerable`1)",
            "SkUiLayout.Contains(Node)",
            "SkUiLayout.CopyTo(IView[], Int32)",
            "SkUiLayout.GetEnumerator()",
        ]),
        ("A ControlTemplate's root and MAUI's indicator template stack exist only where templates do", [
            "SkUiCoreContentView.TemplateRoot",
            "SkUiCoreRadioButton.TemplateRoot",
            "SkUiCoreIndicatorView.IndicatorLayout",
        ]),
        ("SkUi views' input plumbing: their gesture set, the surface's hardware acceleration and IsShown from MAUI's visibility and window state; a Core node draws in its host's surface and asks its host", [
            "SkUiCoreNode.Gestures",
            "SkUiCoreNode.HwAccelerated",
            "SkUiCoreNode.IsShown",
        ]),
        ("The SkUi Markdown view hosts a Core Markdown node", [
            "SkUiCoreMarkdown.Markdown",
        ]),
        ("Core shortcuts: subscribe to Clicked, set a uniform corner radius into CornerRadii (where SkUi's CornerRadius is MAUI's int or a mirror), set an image source of each kind or a decoded image (SkUi takes MAUI's ImageSource and its kinds)", [
            "SkUiCoreButton.SetClicked(Action)",
            "SkUiCoreImageButton.SetClicked(Action)",
            "SkUiCoreButton.SetCornerRadius(Double)",
            "SkUiCoreImageButton.SetCornerRadius(Double)",
            "SkUiCoreLabel.SetCornerRadius(Double)",
            "SkUiCoreImage.SetImage(SKImage, Boolean)",
            "SkUiCoreImageButton.SetImage(SKImage, Boolean)",
            "SkUiCoreImage.SetSourceFile(String)",
            "SkUiCoreImage.SetSourceUri(Uri, Boolean, Nullable`1)",
            "SkUiCoreImage.SetSourceStream(Func`2, String)",
            "SkUiCoreImage.SetSourceFont(String, String, Double, Color, FontAttributes)",
        ]),
        ("SetValue(BindableProperty, object) is BindableObject's: the slider's fluent setter is SetSliderValue", [
            "SkUiSlider.SetValue",
            "SkUiSlider.SetSliderValue(Double)",
        ]),
        ("On the SkUi layer these are attached properties: MAUI's SemanticProperties, AutomationProperties and ToolTipProperties, and SkUiAccessibility's", [
            "SkUiView.SemanticDescription",
            "SkUiView.SemanticHint",
            "SkUiView.SemanticHeadingLevel",
            "SkUiView.IsInAccessibleTree",
            "SkUiView.ExcludedWithChildren",
            "SkUiView.LiveRegion",
            "SkUiView.UpdatesFrequently",
            "SkUiView.ToolTip",
            "SkUiSegment.SemanticDescription",
            "SkUiTabItem.SemanticDescription",
        ]),
        ("A view's switch that can also turn a surface's platform bridge off; a Core node is hidden from screen readers with ExcludedWithChildren", [
            "SkUiCoreNode.IsAccessibilityEnabled",
        ]),
        ("MAUI's layout requests on the SkUi layer: HorizontalOptions, VerticalOptions, MinimumWidthRequest, MaximumWidthRequest and the heights", [
            "SkUiView.HorizontalAlignment",
            "SkUiView.VerticalAlignment",
            "SkUiView.MinimumWidth",
            "SkUiView.MinimumHeight",
            "SkUiView.MaximumWidth",
            "SkUiView.MaximumHeight",
        ]),
        ("SkUi views take recognizers through MAUI's GestureRecognizers collection", [
            "SkUiCoreNode.AddGestureRecognizer(GestureRecognizer)",
            "SkUiCoreNode.RemoveGestureRecognizer(GestureRecognizer)",
        ]),
        ("MAUI's VisualElement.InvalidateMeasure is protected; an SkUi view is measured again through MAUI", [
            "SkUiCoreNode.InvalidateMeasure()",
        ]),
        ("Only the surface root applies the safe area (not planned on drawn views inside a surface)", [
            "SkUiCoreNode.SafeAreaInsets",
            "SkUiCoreContentView.SafeAreaEdges",
            "SkUiCorePanel.SafeAreaEdges",
        ]),
        ("A surface's render statistics: a Core tree draws in its host's surface", [
            "SkUiView.GetRenderStatistics()",
            "SkUiView.ResetRenderStatistics()",
        ]),
        ("IsShown is computed from MAUI's visibility and window state on SkUi views; a Core node asks its host", [
            "SkUiView.event IsShownChanged",
        ]),
        ("Content from ContentTemplate can wait until the view is first shown (ContentLoading) on the SkUi layer; a Core node is given its content", [
            "SkUiCoreContentView.IsContentLoaded",
            "SkUiCoreContentView.ContentLoading",
            "SkUiCoreContentView.ContentLoadingDelay",
            "SkUiCoreContentView.ContentLoadedAnimation",
            "SkUiContentView.LoadContent()",
            "SkUiContentView.event ContentLoaded",
        ]),
        ("MAUI's uniform CornerRadius, kept in step with CornerRadii; Core has CornerRadii only", [
            "SkUiCoreImageButton.CornerRadius",
            "SkUiCoreInputView.CornerRadius",
            "SkUiCoreLabel.CornerRadius",
            "SkUiCorePickerBase.CornerRadius",
        ]),
        ("MAUI's FormattedText of Spans on the SkUi label, a span list on Core; the chrome's fill is the view's Background and the paragraph direction MAUI's FlowDirection on SkUi", [
            "SkUiCoreLabel.FormattedText",
            "SkUiLabel.Spans",
            "SkUiCoreLabel.AddSpan(Span)",
            "SkUiLabel.FillColor",
            "SkUiLabel.TextDirection",
        ]),
        ("Core's radio button stays the circle alone (no content, chrome, groups or templates), composed with Core labels and borders and grouped with SkUiCoreRadioButtons", [
            "SkUiCoreRadioButton.Content",
            "SkUiCoreRadioButton.Value",
            "SkUiCoreRadioButton.GroupName",
            "SkUiCoreRadioButton.TextColor",
            "SkUiCoreRadioButton.TextTransform",
            "SkUiCoreRadioButton.CharacterSpacing",
            "SkUiCoreRadioButton.FontFamily",
            "SkUiCoreRadioButton.FontSize",
            "SkUiCoreRadioButton.FontAttributes",
            "SkUiCoreRadioButton.FontAutoScalingEnabled",
            "SkUiCoreRadioButton.Padding",
            "SkUiCoreRadioButton.BorderColor",
            "SkUiCoreRadioButton.BorderWidth",
            "SkUiCoreRadioButton.CornerRadius",
            "SkUiRadioButton.SetRadioValue(Object)",
        ]),
        ("Core takes delegates where the SkUi layer takes MAUI bindings: ItemDisplayBinding, ItemEnabledBinding, SelectedItemDisplayBinding", [
            "SkUiPicker.ItemDisplay",
            "SkUiPicker.ItemEnabled",
            "SkUiPicker.SelectedItemDisplay",
            "SkUiChipGroup.ItemDisplay",
            "SkUiSegmentedControl.ItemDisplay",
        ]),
        ("A Core node's bounds and flags set in one call; the SkUi layer sets the attached LayoutBounds and LayoutFlags", [
            "SkUiCoreAbsoluteLayout.SetLayoutBounds(Node, Rect, AbsoluteLayoutFlags)",
        ]),
        ("Core builds a carousel's items with a factory; the SkUi carousel takes MAUI's ItemTemplate", [
            "SkUiCoreCarouselView.SetItemFactory(Func`2, Action`2, Func`2)",
        ]),
        ("Core builds a tab's nodes with factories or takes them directly; the SkUi layer takes ContentTemplate and HeaderTemplate", [
            "SkUiTabItem.ContentFactory",
            "SkUiTabItem.HeaderContent",
            "SkUiTabView.HeaderFactory",
        ]),
        ("MAUI's ItemsView members on the SkUi carousel: its scroll bars are never shown, ItemsLayout is Orientation and ItemSpacing on Core, and the group overloads and ScrollToRequested are ItemsView's", [
            "SkUiCoreCarouselView.HorizontalScrollBarVisibility",
            "SkUiCoreCarouselView.VerticalScrollBarVisibility",
            "SkUiCoreCarouselView.ItemsLayout",
            "SkUiCarouselView.ScrollTo(Int32, Int32, ScrollToPosition, Boolean)",
            "SkUiCarouselView.ScrollTo(Object, Object, ScrollToPosition, Boolean)",
            "SkUiCarouselView.event ScrollToRequested",
            "SkUiScrollView.event ScrollToRequested",
        ]),
        ("SkUi's VisibleViews is MAUI's observable collection; Core raises VisibleNodesChanged", [
            "SkUiCoreCarouselView.event VisibleNodesChanged",
        ]),
        ("MAUI links a carousel to its indicator (IndicatorView) and gives the indicator an ItemsSource; Core links the indicator to its carousel (Carousel) or sets Count", [
            "SkUiCoreCarouselView.IndicatorNode",
            "SkUiCoreIndicatorView.ItemsSource",
            "SkUiIndicatorView.Carousel",
        ]),
        ("Core's grid runs its own layout and reports its tracks (the grid splitter and tables read them); SkUiGrid's layout is MAUI's GridLayoutManager", [
            "SkUiGrid.ColumnCount",
            "SkUiGrid.RowCount",
            "SkUiCoreGrid.GetCellBounds(Int32, Int32, Int32, Int32)",
            "SkUiCoreGrid.GetColumnOffset(Int32)",
            "SkUiCoreGrid.GetColumnWidth(Int32)",
            "SkUiCoreGrid.GetRowOffset(Int32)",
            "SkUiCoreGrid.GetRowHeight(Int32)",
            "SkUiCoreGrid.GetPlacement(Node)",
            "SkUiCoreGrid.SetPlacement(Node, Int32, Int32, Int32, Int32)",
        ]),
        ("Core shortcut by child index; the SkUi layer reads the attached property of the child", [
            "SkUiCoreShrinkLayout.GetShrink(Int32)",
            "SkUiCoreShrinkLayout.SetShrink(Int32, ShrinkFactor)",
        ]),
        ("MAUI's path takes its fill rule from its Geometry (PathGeometry.FillRule); Core's path data is a PathF", [
            "SkUiPath.FillRule",
        ]),
        ("MAUI's Image.IsOpaque, kept for parity; a drawn image does not use it", [
            "SkUiCoreImage.IsOpaque",
        ]),
        ("Core shortcut; an SkUi image is cleared by setting Source to null", [
            "SkUiCoreImage.Clear()",
        ]),
    ]);

    private static readonly Assembly[] Assemblies =
    [
        typeof(SkUiView).Assembly,
        typeof(Markdown.SkUiMarkdownView).Assembly,
        typeof(MauiSkiaUi.CommunityToolkit.SkUiDockLayout).Assembly,
    ];

    /// <summary>
    /// Types that mean the same on both layers: MAUI's on the SkUi control, Skia's or Microsoft.Maui.Graphics' on the Core
    /// node, and the views of a SkUi tree as nodes of a Core tree.
    /// </summary>
    private static readonly (Type SkUi, Type Core)[] LayerTypes =
    [
        (typeof(ImageSource), typeof(SkUiImageSource)),
        (typeof(Brush), typeof(Paint)),
        (typeof(PenLineCap), typeof(LineCap)),
        (typeof(PenLineJoin), typeof(LineJoin)),
        (typeof(FillRule), typeof(WindingMode)),
        (typeof(Stretch), typeof(SkUiCoreStretch)),
        (typeof(Geometry), typeof(PathF)),
        (typeof(Transform), typeof(Matrix3x2?)),
        (typeof(DataTemplate), typeof(Func<,>)),
        (typeof(View), typeof(SkUiCoreNode)),
        (typeof(ISkUiView), typeof(SkUiCoreNode)),
    ];

    /// <summary>The base types of a tree's elements on either layer: a parameter of one of them takes any view or node.</summary>
    private static readonly HashSet<Type> NodeTypes =
    [
        typeof(IView), typeof(View), typeof(VisualElement), typeof(Element), typeof(BindableObject),
        typeof(ISkUiView), typeof(SkUiView), typeof(ISkUiCoreNode), typeof(SkUiCoreNode),
    ];

    /// <summary>
    /// Members the SkUi layer has because MAUI's controls do and that Core does differently by design: templates (Core
    /// builds nodes with factories or takes them directly), commands (Core raises events) and bindings (Core takes
    /// delegates: <c>ItemDisplayBinding</c> is <c>ItemDisplay</c>).
    /// </summary>
    private static readonly Type[] MauiOnlyTypes = [typeof(DataTemplate), typeof(ControlTemplate), typeof(System.Windows.Input.ICommand), typeof(BindingBase)];

    /// <summary>Twins whose names do not follow <c>SkUiX</c> / <c>SkUiCoreX</c>.</summary>
    private static readonly (Type SkUi, Type Core)[] NamedOtherwise =
    [
        (typeof(SkUiView), typeof(SkUiCoreNode)), // what every view and node has
        (typeof(SkUiLayout), typeof(SkUiCorePanel)), // the base of the layouts
        (typeof(SkUiAvatarView), typeof(SkUiCoreAvatar)),
        (typeof(Markdown.SkUiMarkdownView), typeof(Markdown.SkUiCoreMarkdown)),
    ];

    /// <summary>SkUi types without a Core twin, and why.</summary>
    private static readonly HashSet<string> Unpaired = Flatten(
    [
        ("Collections and their virtualizing engines: Core has none (a Core list is a scroll view over a stack, or the carousel); FR-C1 keeps Core small", [
            "SkUiCollectionView", "SkUiVirtualScrollView", "SkUiVirtualVerticalStackLayout", "SkUiVirtualVerticalStackLayoutBase",
            "SkUiRefreshView", "SkUiSwipeView", "SkUiSwipeItemView",
        ]),
        ("SkUi-only by design: the sheet is SkUi* only (a Core sheet is on the backlog); hosted native views and MAUI templates have no Core meaning", [
            "SkUiSheet", "SkUiMauiContentView", "SkUiContentPresenter",
        ]),
        ("Community Toolkit and MAUI layouts kept on the SkUi layer; Core uses its wrap, shrink, tile and grid layouts (no Core flex layout, by design)", [
            "SkUiDockLayout", "SkUiUniformItemsLayout", "SkUiFlexLayout",
        ]),
        ("SkUi building blocks whose Core counterparts are not nodes: the shimmer's base, the wheel picker's columns, a stacked bar's MAUI segment (SkUiBarValue on Core), the XAML vector image source", [
            "SkUiAlternateContentView", "SkUiPickerWheels", "SkUiPickerColumn", "SkUiBarSegment", "SkUiVectorImageSource",
        ]),
    ]);

    public static TheoryData<Type, Type> Pairs()
    {
        var data = new TheoryData<Type, Type>();
        foreach (var (skUi, core) in AllPairs())
            data.Add(skUi, core);
        return data;
    }

    private static List<(Type SkUi, Type Core)> AllPairs()
    {
        var exported = Assemblies.SelectMany(a => a.GetExportedTypes()).Where(t => t.IsClass).ToList();
        var byName = exported.GroupBy(t => t.Name).ToDictionary(g => g.Key, g => g.First());
        var pairs = new List<(Type SkUi, Type Core)>();
        foreach (var core in exported.Where(t => t.Name.StartsWith("SkUiCore", StringComparison.Ordinal)).OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            if (byName.TryGetValue("SkUi" + core.Name["SkUiCore".Length..], out var skUi) && typeof(BindableObject).IsAssignableFrom(skUi))
                pairs.Add((skUi, core));
        }
        pairs.AddRange(NamedOtherwise);
        return pairs;
    }

    [Fact]
    public void TwinsAreFound()
    {
        var pairs = AllPairs();
        Assert.Contains((typeof(SkUiLabel), typeof(SkUiCoreLabel)), pairs);
        Assert.Contains((typeof(SkUiSegmentedControl), typeof(SkUiCoreSegmentedControl)), pairs);
        Assert.True(pairs.Count > 50, $"{pairs.Count} pairs");
    }

    /// <summary>A new SkUi control either has a Core twin the other tests check or is listed here with the reason.</summary>
    [Fact]
    public void EverySkUiTypeIsPairedOrListed()
    {
        var paired = AllPairs().Select(pair => pair.SkUi).ToHashSet();
        var unlisted = Assemblies.SelectMany(a => a.GetExportedTypes())
            .Where(t => t.IsClass && typeof(BindableObject).IsAssignableFrom(t) && !typeof(Page).IsAssignableFrom(t) && t.Name.StartsWith("SkUi", StringComparison.Ordinal)
                && !t.Name.StartsWith("SkUiCore", StringComparison.Ordinal) && !paired.Contains(t) && !Unpaired.Contains(t.Name))
            .Select(t => t.Name);
        var exported = Assemblies.SelectMany(a => a.GetExportedTypes()).Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        var stale = Unpaired.Where(name => paired.Any(t => t.Name == name)).Select(name => $"{name} is paired now")
            .Concat(Unpaired.Where(name => !exported.Contains(name)).Select(name => $"{name} is gone"));
        Report([.. unlisted.Concat(stale)]);
    }

    [Fact]
    public void EveryIntendedDifferenceStillHolds()
    {
        var types = Assemblies.SelectMany(a => a.GetExportedTypes()).GroupBy(t => t.Name).ToDictionary(g => g.Key, g => g.First());
        var stale = IntendedDifferences
            .Where(key => !types.TryGetValue(key[..key.IndexOf('.')], out var type) || !StillDiffers(type, key[(key.IndexOf('.') + 1)..]))
            .ToList();
        Report(stale);
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void PropertiesHaveTwinsAndSetters(Type skUi, Type core)
    {
        var closedSkUi = Closed(skUi) ?? skUi;
        var closedCore = Closed(core) ?? core;
        var missing = new List<string>();
        foreach (var bindable in DeclaredBindables(closedSkUi))
        {
            var name = bindable.PropertyName;
            var skUiProperty = Property(closedSkUi, name);
            if (skUiProperty is null)
                continue; // attached properties have no instance property
            var coreProperty = Property(closedCore, name);
            if (coreProperty is null)
            {
                if (!Intended(core, LayerName(name)) && !IsMauiOnly(skUiProperty))
                    missing.Add($"{core.Name}.{LayerName(name)}");
                continue;
            }
            if (!SameLayerType(skUiProperty.PropertyType, coreProperty.PropertyType) && !Intended(core, $"{coreProperty.Name} type"))
                missing.Add($"{core.Name}.{coreProperty.Name} type: {coreProperty.PropertyType.Name}, {skUi.Name}.{name} is {skUiProperty.PropertyType.Name}");
            if (bindable.IsReadOnly)
                continue;
            if (!HasSetter(closedSkUi, name) && !Intended(skUi, "Set" + name))
                missing.Add($"{skUi.Name}.Set{name}");
            if (!HasSetter(closedCore, coreProperty.Name) && !Intended(core, "Set" + coreProperty.Name))
                missing.Add($"{core.Name}.Set{coreProperty.Name}");
        }
        var bindableNames = DeclaredBindables(closedSkUi).Select(b => b.PropertyName).ToHashSet(StringComparer.Ordinal);
        foreach (var skUiProperty in closedSkUi.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            // Properties without a bindable property (read-only state such as IsScrolling) need a twin too.
            if (skUiProperty.GetIndexParameters().Length > 0 || bindableNames.Contains(skUiProperty.Name) || IsMauiOnly(skUiProperty)
                || Intended(core, LayerName(skUiProperty.Name)))
                continue;
            if (Property(closedCore, skUiProperty.Name) is null)
                missing.Add($"{core.Name}.{LayerName(skUiProperty.Name)}");
        }
        foreach (var coreProperty in closedCore.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (coreProperty.GetIndexParameters().Length > 0 || Intended(skUi, LayerName(coreProperty.Name)))
                continue;
            if (Property(closedSkUi, coreProperty.Name) is null)
                missing.Add($"{skUi.Name}.{LayerName(coreProperty.Name)}");
        }
        Report(missing);
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void DefaultsMatch(Type skUi, Type core)
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var (closedSkUi, closedCore) = Instantiable(skUi, core);
        var skUiNode = Create(closedSkUi);
        var coreNode = Create(closedCore);
        var differing = new List<string>();
        foreach (var (skUiProperty, coreProperty) in ComparableProperties(skUi, closedSkUi, closedCore))
        {
            if (!Intended(core, coreProperty.Name + " default") && DefaultDifference(skUiProperty, coreProperty, skUiNode, coreNode) is { } difference)
                differing.Add($"{core.Name}.{coreProperty.Name} default: {difference}");
        }
        Report(differing);
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void InvalidValuesAreRejectedAlike(Type skUi, Type core)
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var (closedSkUi, closedCore) = Instantiable(skUi, core);
        var differing = new List<string>();
        foreach (var (skUiProperty, coreProperty) in ComparableProperties(skUi, closedSkUi, closedCore))
        {
            if (!Intended(core, coreProperty.Name + " validation"))
                differing.AddRange(ValidationDifferences(closedSkUi, closedCore, skUiProperty, coreProperty).Select(d => $"{core.Name}.{coreProperty.Name} validation: {d}"));
        }
        Report(differing);
    }

    /// <summary>How a fresh control's and a fresh node's values of a property differ, or <c>null</c>.</summary>
    private static string? DefaultDifference(PropertyInfo skUiProperty, PropertyInfo coreProperty, object skUiNode, object coreNode)
    {
        var skUiValue = skUiProperty.GetValue(skUiNode);
        var coreValue = coreProperty.GetValue(coreNode);
        return Equals(skUiValue, coreValue) ? null : $"{Show(coreValue)}, {skUiNode.GetType().Name} {Show(skUiValue)}";
    }

    /// <summary>The probes the two layers' fluent setters treat differently (none when either has no one-value setter).</summary>
    private static IEnumerable<string> ValidationDifferences(Type skUi, Type core, PropertyInfo skUiProperty, PropertyInfo coreProperty)
    {
        var skUiSetter = Setter(skUi, skUiProperty.Name, skUiProperty.PropertyType);
        var coreSetter = Setter(core, coreProperty.Name, coreProperty.PropertyType);
        if (skUiSetter is null || coreSetter is null)
            yield break; // the property test reports a missing setter
        foreach (var probe in Probes(skUiProperty.PropertyType))
        {
            var skUiRejects = Rejects(skUiSetter, Create(skUi), probe);
            var coreRejects = Rejects(coreSetter, Create(core), probe);
            if (skUiRejects != coreRejects)
                yield return $"{Show(probe)} {coreRejects}, {skUi.Name} {skUiRejects}";
        }
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void PublicMethodsAndEventsMatch(Type skUi, Type core)
    {
        var skUiDeclared = MethodsAndEvents(skUi, declaredOnly: true);
        var coreDeclared = MethodsAndEvents(core, declaredOnly: true);
        var skUiAll = MethodsAndEvents(skUi, declaredOnly: false);
        var coreAll = MethodsAndEvents(core, declaredOnly: false);
        var onlySkUi = skUiDeclared.Except(coreAll).Where(m => !Intended(skUi, m)).Select(m => $"{skUi.Name}.{m}");
        skUiAll.UnionWith(AttachedGetters(skUi));
        var onlyCore = coreDeclared.Except(skUiAll).Where(m => !Intended(core, m) && !IsPlacingAdd(m)).Select(m => $"{core.Name}.{m}");
        Report([.. onlySkUi.Concat(onlyCore)]);
    }

    private static HashSet<string> Flatten((string Reason, string[] Members)[] groups) =>
        groups.SelectMany(g => g.Members).ToHashSet(StringComparer.Ordinal);

    /// <summary>Fails with every difference on its own line (xUnit's collection message cuts long ones).</summary>
    private static void Report(IReadOnlyCollection<string> differences)
    {
        if (differences.Count > 0)
            Assert.Fail(string.Join(Environment.NewLine, differences.Order(StringComparer.Ordinal)));
    }

    private static bool Intended(Type type, string member) => IntendedDifferences.Contains($"{type.Name}.{member}");

    /// <summary>Whether the difference an <see cref="IntendedDifferences"/> key names (see there) is still there.</summary>
    private static bool StillDiffers(Type type, string member)
    {
        var twin = Twin(type);
        if (twin is null)
            return false;
        var closed = Closed(type) ?? type;
        var closedTwin = Closed(twin) ?? twin;
        if (member.Contains('(') || member.StartsWith("event ", StringComparison.Ordinal))
        {
            var twinMembers = MethodsAndEvents(twin, declaredOnly: false);
            if (type.Name.StartsWith("SkUiCore", StringComparison.Ordinal))
                twinMembers.UnionWith(AttachedGetters(twin));
            return MethodsAndEvents(type, declaredOnly: false).Contains(member) && !twinMembers.Contains(member);
        }
        var name = member.Split(' ')[0];
        var kind = member.Contains(' ') ? member[(member.IndexOf(' ') + 1)..] : null;
        if (kind is not null)
        {
            // "Core.Property default / validation / type": the comparison still finds the difference.
            var (skUiType, coreType) = type.Name.StartsWith("SkUiCore", StringComparison.Ordinal) ? (twin, type) : (type, twin);
            using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
            var (createdSkUi, createdCore) = kind == "type" ? (Closed(skUiType) ?? skUiType, Closed(coreType) ?? coreType) : Instantiable(skUiType, coreType);
            if (Property(createdSkUi, name) is not { } skUiProperty || Property(createdCore, name) is not { } coreProperty)
                return false;
            return kind switch
            {
                "type" => !SameLayerType(skUiProperty.PropertyType, coreProperty.PropertyType),
                "default" => DefaultDifference(skUiProperty, coreProperty, Create(createdSkUi), Create(createdCore)) is not null,
                "validation" => ValidationDifferences(createdSkUi, createdCore, skUiProperty, coreProperty).Any(),
                _ => false,
            };
        }
        if (name.StartsWith("Set", StringComparison.Ordinal) && Property(closed, name[3..]) is not null)
            return !HasSetter(closed, name[3..]);
        return Property(closed, name) is null && Property(closedTwin, name) is not null;
    }

    private static Type? Twin(Type type)
    {
        foreach (var (skUi, core) in AllPairs())
        {
            if (skUi == type)
                return core;
            if (core == type)
                return skUi;
        }
        return null;
    }

    /// <summary>
    /// Types to create for comparing a pair's values: the pair itself, or for an abstract base (<c>SkUiShape</c>,
    /// <c>SkUiView</c>) the first pair of concrete twins derived from it.
    /// </summary>
    private static (Type SkUi, Type Core) Instantiable(Type skUi, Type core)
    {
        if (Creatable(skUi, core) is { } own)
            return own;
        foreach (var (derivedSkUi, derivedCore) in AllPairs())
        {
            if (Creatable(derivedSkUi, derivedCore) is var (a, b) && IsDerived(a, skUi) && IsDerived(b, core))
                return (a, b);
        }
        Assert.Fail($"Neither {skUi.Name} / {core.Name} nor a pair derived from them can be created to compare their values");
        return default;
    }

    private static (Type SkUi, Type Core)? Creatable(Type skUi, Type core)
    {
        var closedSkUi = Closed(skUi);
        var closedCore = Closed(core);
        if (closedSkUi is null || closedCore is null || Constructor(closedSkUi) is null || Constructor(closedCore) is null)
            return null;
        return (closedSkUi, closedCore);
    }

    /// <summary>A public constructor without arguments, or one that takes only <c>params</c> arrays (<c>SkUiCorePolygon(params Point[])</c>).</summary>
    private static ConstructorInfo? Constructor(Type type) =>
        type.IsAbstract ? null : type.GetConstructors().Where(c => c.GetParameters().All(p => p.IsDefined(typeof(ParamArrayAttribute))))
            .OrderBy(c => c.GetParameters().Length)
            .FirstOrDefault();

    private static object Create(Type type)
    {
        var constructor = Constructor(type)!;
        return constructor.Invoke([.. constructor.GetParameters().Select(p => Array.CreateInstance(p.ParameterType.GetElementType()!, 0))]);
    }

    /// <summary>Whether <paramref name="type"/> derives from <paramref name="baseType"/>, a generic definition included.</summary>
    private static bool IsDerived(Type type, Type baseType)
    {
        for (var t = type; t is not null; t = t.BaseType)
        {
            if (t == baseType || (t.IsGenericType && t.GetGenericTypeDefinition() == baseType))
                return true;
        }
        return false;
    }

    /// <summary>
    /// The bindable properties a type declares, not the aliases it gives a base's (<c>IsClippedToBoundsProperty</c>, MAUI's
    /// name for <see cref="SkUiView.ClipToBoundsProperty"/>).
    /// </summary>
    private static IEnumerable<BindableProperty> DeclaredBindables(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(f => f.FieldType == typeof(BindableProperty) && f.Name.EndsWith("Property", StringComparison.Ordinal))
            .Select(f => (BindableProperty)f.GetValue(null)!)
            .Where(p => p.DeclaringType == type || !p.DeclaringType.IsAssignableFrom(type));

    /// <summary>
    /// The most derived public instance property of that name, or of the same name in the other layer's words
    /// (<c>VisibleViews</c> finds <c>VisibleNodes</c>).
    /// </summary>
    private static PropertyInfo? Property(Type type, string name)
    {
        var layerName = LayerName(name);
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 && (p.Name == name || LayerName(p.Name) == layerName))
            .OrderByDescending(p => p.Name == name)
            .ThenByDescending(p => Depth(p.DeclaringType!))
            .FirstOrDefault();
    }

    /// <summary>A member's name with views and elements called nodes, the Core layer's word.</summary>
    private static string LayerName(string name) => ViewOrElement().Replace(name, "Node");

    [GeneratedRegex("(View|Element)(?=[A-Z]|s?$)")]
    private static partial Regex ViewOrElement();

    private static int Depth(Type type)
    {
        var depth = 0;
        for (var t = type; t is not null; t = t.BaseType)
            depth++;
        return depth;
    }

    /// <summary>A template or a command (and its parameter): what Core does with factories and events.</summary>
    private static bool IsMauiOnly(PropertyInfo property) =>
        MauiOnlyTypes.Any(t => t.IsAssignableFrom(property.PropertyType)) || property.Name.EndsWith("CommandParameter", StringComparison.Ordinal);

    /// <summary>
    /// Pairs of properties both layers have with the same simple type, from the bindable properties <paramref name="declaring"/>
    /// declares, looked up on the types created (<paramref name="skUi"/>, <paramref name="core"/>): what the default and
    /// validation tests compare.
    /// </summary>
    private static IEnumerable<(PropertyInfo SkUi, PropertyInfo Core)> ComparableProperties(Type declaring, Type skUi, Type core)
    {
        var declaringType = skUi;
        while (declaringType is not null && declaringType != declaring && !(declaringType.IsGenericType && declaringType.GetGenericTypeDefinition() == declaring))
            declaringType = declaringType.BaseType;
        foreach (var bindable in DeclaredBindables(declaringType ?? skUi))
        {
            if (bindable.IsReadOnly)
                continue;
            var skUiProperty = Property(skUi, bindable.PropertyName);
            var coreProperty = Property(core, bindable.PropertyName);
            if (skUiProperty is null || coreProperty is null || skUiProperty.PropertyType != coreProperty.PropertyType || !IsSimple(skUiProperty.PropertyType))
                continue;
            yield return (skUiProperty, coreProperty);
        }
    }

    private static bool IsSimple(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsPrimitive || underlying.IsEnum || SimpleTypes.Contains(underlying);
    }

    /// <summary>Value types the default and validation tests compare, besides primitives and enums.</summary>
    private static readonly HashSet<Type> SimpleTypes =
        [typeof(string), typeof(Color), typeof(TimeSpan), typeof(Thickness), typeof(CornerRadius), typeof(Size), typeof(Point), typeof(Rect)];

    /// <summary>Values a control should reject, or that the two layers must at least treat alike.</summary>
    private static IEnumerable<object?> Probes(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null || !type.IsValueType)
            yield return null;
        var value = underlying ?? type;
        if (value == typeof(double))
        {
            yield return double.NaN;
            yield return double.PositiveInfinity;
            yield return -2d;
        }
        else if (value == typeof(float))
        {
            yield return float.NaN;
            yield return -2f;
        }
        else if (value == typeof(int))
            yield return -2;
        else if (value == typeof(TimeSpan))
            yield return TimeSpan.FromSeconds(-2);
        else if (value == typeof(Thickness))
        {
            yield return new Thickness(-2);
            yield return new Thickness(double.NaN);
        }
        else if (value == typeof(CornerRadius))
        {
            yield return new CornerRadius(-2);
            yield return new CornerRadius(double.NaN);
        }
        else if (value.IsEnum && value.GetCustomAttribute<FlagsAttribute>() is null)
            yield return Enum.ToObject(value, 9999);
        else if (value.IsEnum)
        {
            // A flag outside every defined one.
            var defined = Enum.GetValues(value).Cast<object>().Aggregate(0L, (all, flag) => all | Convert.ToInt64(flag));
            var bit = Enumerable.Range(0, 31).Select(i => 1L << i).First(b => (defined & b) == 0);
            yield return Enum.ToObject(value, bit);
        }
    }

    /// <summary>What the fluent setter does with <paramref name="value"/>: takes it, rejects it (an argument exception), or throws.</summary>
    private static string Rejects(MethodInfo setter, object target, object? value)
    {
        try
        {
            setter.Invoke(target, [value]);
            return "taken";
        }
        catch (TargetInvocationException e) when (e.InnerException is ArgumentException)
        {
            return "rejected";
        }
        catch (TargetInvocationException e)
        {
            return $"throws {e.InnerException!.GetType().Name}";
        }
    }

    /// <summary>
    /// The same type, a pair of <see cref="LayerTypes"/>, twins (<c>SkUiTabItem</c> / <c>SkUiCoreTabItem</c>, MAUI's
    /// <c>ColumnDefinition</c> / <c>SkUiCoreColumnDefinition</c>), a template on SkUi where Core has a factory of nodes, or a
    /// MAUI collection on SkUi where Core has a read-only list of the same items.
    /// </summary>
    private static bool SameLayerType(Type skUi, Type core)
    {
        if (skUi == core || (Nullable.GetUnderlyingType(core) is { } underlying && skUi == underlying))
            return true;
        if (LayerTypes.Any(pair => pair.SkUi.IsAssignableFrom(skUi) && pair.Core == core))
            return true;
        if (core.Name.StartsWith("SkUiCore", StringComparison.Ordinal) && ParameterName(skUi) == ParameterName(core))
            return true;
        if (!core.IsGenericType)
            return false;
        var definition = core.GetGenericTypeDefinition();
        var arguments = core.GetGenericArguments();
        if (definition == typeof(Func<,>) && typeof(DataTemplate).IsAssignableFrom(skUi))
            return typeof(ISkUiCoreNode).IsAssignableFrom(arguments[1]);
        if (definition == typeof(IReadOnlyList<>) && skUi != typeof(string) && ItemType(skUi) is { } item)
            return SameLayerType(item, arguments[0]);
        return false;
    }

    /// <summary>The item type of a collection, from its <c>IEnumerable&lt;T&gt;</c>.</summary>
    private static Type? ItemType(Type type) =>
        (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>) ? type : null)?.GetGenericArguments()[0]
        ?? type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))?.GetGenericArguments()[0];

    private static bool HasSetter(Type type, string name) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Any(m =>
            m.Name == "Set" + name && m.DeclaringType != typeof(BindableObject) && m.GetParameters().Length >= 1);

    private static MethodInfo? Setter(Type type, string name, Type valueType) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(m =>
            m.Name == "Set" + name && m.DeclaringType != typeof(BindableObject) && m.GetParameters() is [var value] && value.ParameterType.IsAssignableFrom(valueType));

    /// <summary>A generic type closed with <c>double</c> (so NaN and infinity are probed), else <c>int</c>.</summary>
    private static Type? Closed(Type type)
    {
        if (!type.IsGenericTypeDefinition)
            return type;
        foreach (var argument in (Type[])[typeof(double), typeof(int)])
        {
            try
            {
                return type.MakeGenericType([.. type.GetGenericArguments().Select(_ => argument)]);
            }
            catch (ArgumentException)
            {
                // constraints the argument does not meet
            }
        }
        return null;
    }

    private static string Show(object? value) => value switch
    {
        null => "null",
        Color color => color.ToArgbHex(includeAlpha: true),
        _ => value.ToString() ?? "",
    };

    /// <summary>
    /// Public methods (not property accessors; not a property's fluent setters, which the property test covers; not
    /// overrides of a base's) and events, as "Name(ParameterTypes)" and "event Name" in the Core layer's words: the ones the
    /// type declares, or all it has. Setters of several values (<c>SetDownsample(width, height)</c>) are compared here.
    /// </summary>
    private static HashSet<string> MethodsAndEvents(Type type, bool declaredOnly)
    {
        var flags = BindingFlags.Public | BindingFlags.Instance | (declaredOnly ? BindingFlags.DeclaredOnly : 0);
        var members = new HashSet<string>(StringComparer.Ordinal);
        var closed = Closed(type) ?? type;
        foreach (var method in type.GetMethods(flags))
        {
            if (method.IsSpecialName || method.DeclaringType == typeof(BindableObject))
                continue;
            if (declaredOnly && method.Name.StartsWith("Set", StringComparison.Ordinal) && method.GetParameters().Length == 1 && Property(closed, method.Name[3..]) is not null)
                continue;
            if (declaredOnly && method.GetBaseDefinition().DeclaringType != method.DeclaringType)
                continue;
            members.Add($"{LayerName(method.Name)}({string.Join(", ", method.GetParameters().Select(p => ParameterName(p.ParameterType)))})");
        }
        foreach (var e in type.GetEvents(flags))
            members.Add($"event {LayerName(e.Name)}");
        return members;
    }

    /// <summary>
    /// The SkUi layer's attached property accessors (<c>SkUiShrinkLayout.GetShrink(BindableObject)</c>,
    /// <c>SetShrink(BindableObject, value)</c>), as the instance methods Core has for them (<c>GetShrink(node)</c>,
    /// <c>SetShrink(node, value)</c>).
    /// </summary>
    private static IEnumerable<string> AttachedGetters(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(m => m.DeclaringType != typeof(BindableObject) && (m.Name.StartsWith("Get", StringComparison.Ordinal) || m.Name.StartsWith("Set", StringComparison.Ordinal))
                && m.GetParameters() is [{ ParameterType: var p }, ..] && typeof(BindableObject).IsAssignableFrom(p))
            .Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => ParameterName(p.ParameterType)))})");

    /// <summary>
    /// A Core container's <c>Add(node, placement)</c>: the child and the values the SkUi layer sets as attached properties
    /// (a grid cell, absolute bounds, a shrink factor) in one call.
    /// </summary>
    private static bool IsPlacingAdd(string member) => member.StartsWith("Add(Node, ", StringComparison.Ordinal);

    /// <summary>
    /// A parameter's type in the Core layer's words: a view or element is a node, an image source is MAUI's, and a type with
    /// twins (<c>SkUiTabItem</c> / <c>SkUiCoreTabItem</c>, MAUI's <c>ColumnDefinition</c> / <c>SkUiCoreColumnDefinition</c>)
    /// loses its layer prefix.
    /// </summary>
    private static string ParameterName(Type type)
    {
        if (NodeTypes.Contains(type))
            return "Node";
        if (type == typeof(SkUiImageSource))
            return nameof(ImageSource);
        var name = type.Name;
        if (name.StartsWith("SkUiCore", StringComparison.Ordinal))
            return name["SkUiCore".Length..];
        return name.StartsWith("SkUi", StringComparison.Ordinal) ? name["SkUi".Length..] : name;
    }
}
