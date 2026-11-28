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
/// generator for the twins). Twins are found by name, <c>SkUiCoreX</c> ↔ <c>SkUiX</c>, in the library and its packages. For
/// each pair:
/// <list type="bullet">
/// <item>each bindable property of the SkUi control has a property of the same name and type on the Core twin, and each
/// property the Core twin declares has one on the SkUi control;</item>
/// <item>both layers have a fluent <c>SetX</c> for every writable one;</item>
/// <item>a fresh control and a fresh node start with the same value (numbers, enums, strings, colors, time spans);</item>
/// <item>both reject the same invalid values (NaN, infinity, -2, an undefined enum value, <c>null</c>): the SkUi <c>SetX</c>
/// throws exactly when the Core <c>SetX</c> does;</item>
/// <item>public methods and events declared on one layer exist on the other.</item>
/// </list>
/// The layers' conventions are rules here, not exceptions: MAUI types on SkUi and Skia / graphics types on Core
/// (<see cref="LayerTypes"/>), views on SkUi and nodes on Core (<c>EmptyView</c> holds an <c>SkUiCoreNode</c>,
/// <c>GetRealizedView</c> is <c>GetRealizedNode</c>), templates and commands on SkUi where Core takes factories and events,
/// MAUI's mutable collections on SkUi and read-only lists on Core. Any other difference that is meant goes in
/// <see cref="IntendedDifferences"/>, with the reason.
/// </summary>
public partial class TwinParityTests
{
    /// <summary>
    /// What one layer has, or does differently, on purpose. Keys: <c>Type.Member</c> for a member only that type has (a
    /// property, <c>SetX</c>, a method as "Name(ParameterTypes)" or "event Name"); <c>Type.Member default</c> /
    /// <c>Type.Member validation</c> (on the Core type) for a value the layers treat differently.
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
        ]),
        ("Set together on both layers: SetDownsample(width, height), SetFadeAnimation(enabled, duration), SetPlaceholders(loading, error)", [
            "SkUiImage.SetDownsampleWidth",
            "SkUiImage.SetDownsampleHeight",
            "SkUiImage.SetFadeAnimationEnabled",
            "SkUiImage.SetFadeAnimationDuration",
            "SkUiImage.SetLoadingPlaceholder",
            "SkUiImage.SetErrorPlaceholder",
            "SkUiCoreImage.SetDownsampleWidth",
            "SkUiCoreImage.SetDownsampleHeight",
            "SkUiCoreImage.SetFadeAnimationEnabled",
        ]),
        ("SetValue(BindableProperty, object) is BindableObject's: the slider's fluent setter is SetSliderValue", [
            "SkUiSlider.SetValue",
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
        ]),
        ("Core takes delegates where the SkUi layer takes MAUI bindings: ItemDisplayBinding, ItemEnabledBinding, SelectedItemDisplayBinding", [
            "SkUiPicker.ItemDisplay",
            "SkUiPicker.ItemEnabled",
            "SkUiPicker.SelectedItemDisplay",
            "SkUiChipGroup.ItemDisplay",
            "SkUiSegmentedControl.ItemDisplay",
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
        ]),
        ("Core shortcut by child index; the SkUi layer reads the attached property of the child", [
            "SkUiCoreShrinkLayout.GetShrink(Int32)",
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
    /// builds nodes with factories or takes them directly) and commands (Core raises events).
    /// </summary>
    private static readonly Type[] MauiOnlyTypes = [typeof(DataTemplate), typeof(ControlTemplate), typeof(System.Windows.Input.ICommand)];

    public static TheoryData<Type, Type> Pairs()
    {
        var exported = Assemblies.SelectMany(a => a.GetExportedTypes()).Where(t => t.IsClass).ToList();
        var byName = exported.GroupBy(t => t.Name).ToDictionary(g => g.Key, g => g.First());
        var data = new TheoryData<Type, Type>();
        foreach (var core in exported.Where(t => t.Name.StartsWith("SkUiCore", StringComparison.Ordinal)).OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            if (byName.TryGetValue("SkUi" + core.Name["SkUiCore".Length..], out var skUi) && typeof(BindableObject).IsAssignableFrom(skUi))
                data.Add(skUi, core);
        }
        data.Add(typeof(SkUiView), typeof(SkUiCoreNode)); // what every view and node has
        return data;
    }

    [Fact]
    public void TwinsAreFound()
    {
        var pairs = Pairs().Select(row => ((Type)row[0], (Type)row[1])).ToList();
        Assert.Contains((typeof(SkUiLabel), typeof(SkUiCoreLabel)), pairs);
        Assert.Contains((typeof(SkUiSegmentedControl), typeof(SkUiCoreSegmentedControl)), pairs);
        Assert.True(pairs.Count > 50, $"{pairs.Count} pairs");
    }

    [Fact]
    public void EveryIntendedDifferenceStillExists()
    {
        var types = Assemblies.SelectMany(a => a.GetExportedTypes()).GroupBy(t => t.Name).ToDictionary(g => g.Key, g => g.First());
        var stale = IntendedDifferences
            .Where(key => !types.TryGetValue(key[..key.IndexOf('.')], out var type) || !HasMember(type, key[(key.IndexOf('.') + 1)..]))
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
        if (Instantiable(skUi, core) is not var (closedSkUi, closedCore))
            return;
        var skUiNode = Activator.CreateInstance(closedSkUi)!;
        var coreNode = Activator.CreateInstance(closedCore)!;
        var differing = new List<string>();
        foreach (var (skUiProperty, coreProperty) in ComparableProperties(skUi, closedSkUi, closedCore))
        {
            if (Intended(core, coreProperty.Name + " default"))
                continue;
            var skUiValue = skUiProperty.GetValue(skUiNode);
            var coreValue = coreProperty.GetValue(coreNode);
            if (!Equals(skUiValue, coreValue))
                differing.Add($"{core.Name}.{coreProperty.Name} default: {Show(coreValue)}, {skUi.Name} {Show(skUiValue)}");
        }
        Report(differing);
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void InvalidValuesAreRejectedAlike(Type skUi, Type core)
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        if (Instantiable(skUi, core) is not var (closedSkUi, closedCore))
            return;
        var differing = new List<string>();
        foreach (var (skUiProperty, coreProperty) in ComparableProperties(skUi, closedSkUi, closedCore))
        {
            if (Intended(core, coreProperty.Name + " validation"))
                continue;
            var skUiSetter = Setter(closedSkUi, skUiProperty.Name, skUiProperty.PropertyType);
            var coreSetter = Setter(closedCore, coreProperty.Name, coreProperty.PropertyType);
            if (skUiSetter is null || coreSetter is null)
                continue; // the property test reports a missing setter
            foreach (var probe in Probes(skUiProperty.PropertyType))
            {
                var skUiRejects = Rejects(skUiSetter, Activator.CreateInstance(closedSkUi)!, probe);
                var coreRejects = Rejects(coreSetter, Activator.CreateInstance(closedCore)!, probe);
                if (skUiRejects != coreRejects)
                    differing.Add($"{core.Name}.{coreProperty.Name} validation: {Show(probe)} {coreRejects}, {skUi.Name} {skUiRejects}");
            }
        }
        Report(differing);
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

    /// <summary>Whether an <see cref="IntendedDifferences"/> key still names a member of <paramref name="type"/>.</summary>
    private static bool HasMember(Type type, string member)
    {
        var name = member.Split(' ', '(')[0];
        if (member.StartsWith("event ", StringComparison.Ordinal))
            return MethodsAndEvents(type, declaredOnly: false).Contains(member);
        if (member.Contains('('))
            return MethodsAndEvents(type, declaredOnly: false).Contains(member);
        var closed = Closed(type) ?? type;
        if (member.EndsWith(" default", StringComparison.Ordinal) || member.EndsWith(" validation", StringComparison.Ordinal) || member.EndsWith(" type", StringComparison.Ordinal))
            return Property(closed, name) is not null;
        // A member only one type has: the key names the missing one, on the type that lacks it, so its twin has it.
        var twin = Twin(type);
        return twin is not null && (Property(Closed(twin) ?? twin, name) is not null || (name.StartsWith("Set", StringComparison.Ordinal) && Property(closed, name[3..]) is not null));
    }

    private static Type? Twin(Type type)
    {
        if (type == typeof(SkUiView))
            return typeof(SkUiCoreNode);
        if (type == typeof(SkUiCoreNode))
            return typeof(SkUiView);
        var twinName = type.Name.StartsWith("SkUiCore", StringComparison.Ordinal) ? "SkUi" + type.Name["SkUiCore".Length..] : "SkUiCore" + type.Name["SkUi".Length..];
        return Assemblies.SelectMany(a => a.GetExportedTypes()).FirstOrDefault(t => t.Name == twinName);
    }

    /// <summary>
    /// Types to create for comparing a pair's values: the pair itself, or for an abstract base (<c>SkUiShape</c>,
    /// <c>SkUiView</c>) the first pair of concrete twins derived from it.
    /// </summary>
    private static (Type SkUi, Type Core)? Instantiable(Type skUi, Type core)
    {
        if (Creatable(skUi, core) is { } own)
            return own;
        foreach (var row in Pairs())
        {
            var (derivedSkUi, derivedCore) = ((Type)row[0], (Type)row[1]);
            if (Creatable(derivedSkUi, derivedCore) is var (a, b) && IsDerived(a, skUi) && IsDerived(b, core))
                return (a, b);
        }
        return null;
    }

    private static (Type SkUi, Type Core)? Creatable(Type skUi, Type core)
    {
        var closedSkUi = Closed(skUi);
        var closedCore = Closed(core);
        if (closedSkUi is null || closedCore is null || closedSkUi.IsAbstract || closedCore.IsAbstract
            || closedSkUi.GetConstructor(Type.EmptyTypes) is null || closedCore.GetConstructor(Type.EmptyTypes) is null)
            return null;
        return (closedSkUi, closedCore);
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
        return underlying.IsPrimitive || underlying.IsEnum || underlying == typeof(string) || underlying == typeof(Color) || underlying == typeof(TimeSpan);
    }

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
        else if (value.IsEnum && value.GetCustomAttribute<FlagsAttribute>() is null)
            yield return Enum.ToObject(value, 9999);
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
    /// The same type, a pair of <see cref="LayerTypes"/>, or a MAUI collection on SkUi where Core has a read-only list of the
    /// same items.
    /// </summary>
    private static bool SameLayerType(Type skUi, Type core)
    {
        if (skUi == core)
            return true;
        var coreDefinition = core.IsGenericType ? core.GetGenericTypeDefinition() : core;
        if (LayerTypes.Any(pair => pair.SkUi.IsAssignableFrom(skUi) && (pair.Core == core || pair.Core == coreDefinition)))
            return true;
        if (typeof(ISkUiCoreNode).IsAssignableFrom(core) && LayerTypes.Any(pair => pair.Core == typeof(SkUiCoreNode) && pair.SkUi.IsAssignableFrom(skUi)))
            return true;
        return coreDefinition == typeof(IReadOnlyList<>) && skUi != typeof(string) && typeof(IEnumerable).IsAssignableFrom(skUi);
    }

    private static bool HasSetter(Type type, string name) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Any(m =>
            m.Name == "Set" + name && m.DeclaringType != typeof(BindableObject) && m.GetParameters().Length >= 1);

    private static MethodInfo? Setter(Type type, string name, Type valueType) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(m =>
            m.Name == "Set" + name && m.DeclaringType != typeof(BindableObject) && m.GetParameters() is [var value] && value.ParameterType.IsAssignableFrom(valueType));

    private static Type? Closed(Type type)
    {
        if (!type.IsGenericTypeDefinition)
            return type;
        try
        {
            return type.MakeGenericType([.. type.GetGenericArguments().Select(_ => typeof(int))]);
        }
        catch (ArgumentException)
        {
            return null; // constraints int does not meet
        }
    }

    private static string Show(object? value) => value switch
    {
        null => "null",
        Color color => color.ToArgbHex(includeAlpha: true),
        _ => value.ToString() ?? "",
    };

    /// <summary>
    /// Public methods (not property accessors; not the fluent setters, which the property test covers; not overrides of a
    /// base's) and events, as "Name(ParameterTypes)" and "event Name" in the Core layer's words: the ones the type declares,
    /// or all it has.
    /// </summary>
    private static HashSet<string> MethodsAndEvents(Type type, bool declaredOnly)
    {
        var flags = BindingFlags.Public | BindingFlags.Instance | (declaredOnly ? BindingFlags.DeclaredOnly : 0);
        var members = new HashSet<string>(StringComparer.Ordinal);
        foreach (var method in type.GetMethods(flags))
        {
            if (method.IsSpecialName || method.Name.StartsWith("Set", StringComparison.Ordinal))
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
    /// The SkUi layer's attached property getters (<c>SkUiShrinkLayout.GetShrink(BindableObject)</c>), as the instance
    /// methods Core has for them (<c>SkUiCoreShrinkLayout.GetShrink(node)</c>).
    /// </summary>
    private static IEnumerable<string> AttachedGetters(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(m => m.Name.StartsWith("Get", StringComparison.Ordinal) && m.GetParameters() is [{ ParameterType: var p }] && typeof(BindableObject).IsAssignableFrom(p))
            .Select(m => $"{m.Name}(Node)");

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
