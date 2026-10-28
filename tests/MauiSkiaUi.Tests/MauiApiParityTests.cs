using System.ComponentModel;
using System.Reflection;
using MauiSkiaUi.Core;
using Xunit;
using Shapes = Microsoft.Maui.Controls.Shapes;

namespace MauiSkiaUi.Tests;

/// <summary>
/// MAUI API parity by reflection (step R1 in docs/archive/ImplementationPlan-1.2.md). Every public property, event, method
/// and bindable property field of a MAUI control exists on its drawn counterpart with types MAUI code can use: a property
/// reads as MAUI's type and, when MAUI's can be set, takes MAUI's values; an event's arguments are MAUI's (or derive from
/// them), so MAUI handlers attach; every MAUI overload of a method takes the same arguments. Core twins have MAUI's own
/// properties (the ones declared below <see cref="View"/>) by name, settable where MAUI's are. Members MAUI hides with
/// <see cref="EditorBrowsableState.Never"/> are handler plumbing and not compared; so are the visible plumbing members in
/// <see cref="PlumbingEverywhere"/>. Every other difference is listed in <see cref="Excluded"/> with the reason or in
/// <see cref="Planned"/> with the step that closes it (a step removes its entries), and an entry that no longer
/// matches a difference fails too. On a new MAUI 10.0.x release, the failure lists what MAUI added.
/// </summary>
public class MauiApiParityTests
{
    private const BindingFlags Instance = BindingFlags.Public | BindingFlags.Instance;
    private const BindingFlags Static = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;

    private static readonly (Type Maui, Type Drawn, Type? Core)[] Pairs =
    [
        (typeof(Label), typeof(SkUiLabel), typeof(SkUiCoreLabel)),
        (typeof(Button), typeof(SkUiButton), typeof(SkUiCoreButton)),
        (typeof(Image), typeof(SkUiImage), typeof(SkUiCoreImage)),
        (typeof(ImageButton), typeof(SkUiImageButton), typeof(SkUiCoreImageButton)),
        (typeof(Entry), typeof(SkUiEntry), typeof(SkUiCoreEntry)),
        (typeof(Editor), typeof(SkUiEditor), typeof(SkUiCoreEditor)),
        (typeof(SearchBar), typeof(SkUiSearchBar), typeof(SkUiCoreSearchBar)),
        (typeof(CheckBox), typeof(SkUiCheckBox), typeof(SkUiCoreCheckBox)),
        (typeof(Switch), typeof(SkUiSwitch), typeof(SkUiCoreSwitch)),
        (typeof(Slider), typeof(SkUiSlider), typeof(SkUiCoreSlider)),
        (typeof(Stepper), typeof(SkUiStepper), typeof(SkUiCoreStepper)),
        (typeof(RadioButton), typeof(SkUiRadioButton), typeof(SkUiCoreRadioButton)),
        (typeof(ProgressBar), typeof(SkUiProgressBar), typeof(SkUiCoreProgressBar)),
        (typeof(ActivityIndicator), typeof(SkUiActivityIndicator), typeof(SkUiCoreActivityIndicator)),
        (typeof(BoxView), typeof(SkUiBox), typeof(SkUiCoreBox)),
        (typeof(Border), typeof(SkUiBorder), typeof(SkUiCoreBorder)),
        (typeof(ScrollView), typeof(SkUiScrollView), typeof(SkUiCoreScrollView)),
        (typeof(Grid), typeof(SkUiGrid), typeof(SkUiCoreGrid)),
        (typeof(VerticalStackLayout), typeof(SkUiVerticalStackLayout), typeof(SkUiCoreVerticalStackLayout)),
        (typeof(HorizontalStackLayout), typeof(SkUiHorizontalStackLayout), typeof(SkUiCoreHorizontalStackLayout)),
        (typeof(AbsoluteLayout), typeof(SkUiAbsoluteLayout), typeof(SkUiCoreAbsoluteLayout)),
        (typeof(FlexLayout), typeof(SkUiFlexLayout), null),
        (typeof(ContentView), typeof(SkUiContentView), typeof(SkUiCoreContentView)),
        (typeof(ContentPresenter), typeof(SkUiContentPresenter), null),
        (typeof(CollectionView), typeof(SkUiCollectionView), null),
        (typeof(CarouselView), typeof(SkUiCarouselView), null),
        (typeof(IndicatorView), typeof(SkUiIndicatorView), typeof(SkUiCoreIndicatorView)),
        (typeof(SwipeView), typeof(SkUiSwipeView), null),
        (typeof(SwipeItemView), typeof(SkUiSwipeItemView), null),
        (typeof(RefreshView), typeof(SkUiRefreshView), null),
        (typeof(Picker), typeof(SkUiPicker), typeof(SkUiCorePicker)),
        (typeof(DatePicker), typeof(SkUiDatePicker), typeof(SkUiCoreDatePicker)),
        (typeof(TimePicker), typeof(SkUiTimePicker), typeof(SkUiCoreTimePicker)),
        (typeof(Shapes.Ellipse), typeof(SkUiEllipse), typeof(SkUiCoreEllipse)),
        (typeof(Shapes.Line), typeof(SkUiLine), typeof(SkUiCoreLine)),
        (typeof(Shapes.Rectangle), typeof(SkUiRectangle), typeof(SkUiCoreRectangle)),
        (typeof(Shapes.RoundRectangle), typeof(SkUiRoundRectangle), typeof(SkUiCoreRoundRectangle)),
        (typeof(Shapes.Path), typeof(SkUiPath), typeof(SkUiCorePath)),
        (typeof(Shapes.Polygon), typeof(SkUiPolygon), typeof(SkUiCorePolygon)),
        (typeof(Shapes.Polyline), typeof(SkUiPolyline), typeof(SkUiCorePolyline)),
    ];

    private const string DrawnContent = "Drawn containers hold drawn views (ISkUiView); MAUI views go in an SkUiMauiContentView";
    private const string MauiAttached = "Attached properties stay MAUI's: children of drawn layouts use Grid.Row, AbsoluteLayout.LayoutBounds, FlexLayout.Grow";
    private const string CoreChildren = "MAUI's layouts are an IList<IView>; Core containers expose Children and Add / Insert / Remove";
    private const string CoreNotTemplated = "Core nodes are not templated";

    private static readonly string[] CoreLayouts = ["SkUiCoreAbsoluteLayout", "SkUiCoreGrid", "SkUiCoreHorizontalStackLayout", "SkUiCoreVerticalStackLayout"];

    /// <summary>Handler and bindable-property plumbing that MAUI does not hide, compared by member name on every type.</summary>
    private static readonly Dictionary<string, string> PlumbingEverywhere = new(StringComparer.Ordinal)
    {
        ["On"] = "Platform-specifics: MAUI's On<T>() is typed to its own control classes (the migration checker converts the XAML attached properties with a drawn meaning, S2)",
        ["UpdateFormsText"] = "Handler plumbing for TextTransform; drawn text transforms itself",
        ["OnTextTransformChanged"] = "Handler plumbing for TextTransform; drawn text transforms itself",
        ["OnHorizontalTextAlignmentPropertyChanged"] = "Handler plumbing",
        ["CrossPlatformMeasure"] = "ICrossPlatformLayout plumbing for MAUI's handlers; drawn views measure in MeasureOverride",
        ["CrossPlatformArrange"] = "ICrossPlatformLayout plumbing for MAUI's handlers; drawn views arrange in ArrangeOverride",
        ["OnPaddingPropertyChanged"] = "Bindable property plumbing of MAUI's Border",
        ["PaddingDefaultValueCreator"] = "Bindable property plumbing of MAUI's Border",
        ["ResolveControlTemplate"] = "Template plumbing of MAUI's templated views",
        ["GetPath"] = "Platform geometry for MAUI's shape handlers; drawn shapes build an SKPath",
        ["SendScrolled"] = "Handler plumbing: the drawn engine raises Scrolled itself",
        ["SendRemainingItemsThresholdReached"] = "Handler plumbing: the drawn engine raises RemainingItemsThresholdReached itself",
    };

    /// <summary>Differences kept on purpose, keyed by <c>DrawnType.Member</c>, with the reason.</summary>
    private static readonly Dictionary<string, string> Excluded = Entries(
        (DrawnContent, ["SkUiBorder", "SkUiContentPresenter", "SkUiContentView", "SkUiRefreshView", "SkUiScrollView", "SkUiSwipeItemView", "SkUiSwipeView"], ["Content"]),
        (DrawnContent, ["SkUiAbsoluteLayout", "SkUiFlexLayout", "SkUiGrid", "SkUiHorizontalStackLayout", "SkUiVerticalStackLayout"], ["Children"]),
        (MauiAttached, ["SkUiGrid"], ["RowProperty", "RowSpanProperty", "ColumnProperty", "ColumnSpanProperty"]),
        (MauiAttached, ["SkUiAbsoluteLayout"], ["LayoutBoundsProperty", "LayoutFlagsProperty", "GetLayoutBounds", "GetLayoutFlags", "SetLayoutBounds", "SetLayoutFlags"]),
        (MauiAttached, ["SkUiFlexLayout"], ["GetAlignSelf", "GetBasis", "GetGrow", "GetOrder", "GetShrink", "SetAlignSelf", "SetBasis", "SetGrow", "SetOrder", "SetShrink"]),
        ("Read-only CLR properties: a bindable property would cost a SetValue per scroll frame; Scrolled reports the offset",
            ["SkUiScrollView"], ["ScrollXProperty", "ScrollYProperty", "ContentSizeProperty"]),
        ("MAUI inherits it from TemplatedView; the drawn swipe, refresh and indicator views are not templated (template their content)",
            ["SkUiIndicatorView", "SkUiRefreshView", "SkUiSwipeView"], ["ControlTemplate", "ControlTemplateProperty"]),
        ("Links the drawn indicator, SkUiIndicatorView", ["SkUiCarouselView"], ["IndicatorView"]),
        ("ICheckBox's paint for MAUI's handlers, derived from Color", ["SkUiCheckBox", "SkUiCoreCheckBox"], ["Foreground"]),
        ("Handler plumbing of the platform radio button", ["SkUiRadioButton"], ["ContentAsString"]),
        ("Held for 2.0 (D1 in ImplementationPlan-1.2.md); the conversion is in Migration.md", ["SkUiCollectionView"], ["Scrolled", "SelectionChanged", "SelectionMode"]),
        ("Drawn views (D5 in ImplementationPlan-1.2.md): text in XAML converts to a label (SkUiTextViewTypeConverter); data converts to a template bound through the list's context (Migration.md)",
            ["SkUiCollectionView"], ["Header", "Footer", "EmptyView"]),
        ("A drawn view (D5): text in XAML converts to a centered label; data converts to a template bound through the carousel's context",
            ["SkUiCarouselView"], ["EmptyView"]),
        ("Core nodes are never a surface root, the only place SkiaUi applies the safe area (S1)",
            ["SkUiCoreAbsoluteLayout", "SkUiCoreBorder", "SkUiCoreContentView", "SkUiCoreGrid", "SkUiCoreHorizontalStackLayout", "SkUiCoreScrollView", "SkUiCoreVerticalStackLayout"],
            ["SafeAreaEdges"]),
        ("Rarely used: GetRealizedView(index) or Scrolled instead; the roadmap's backlog keeps it", ["SkUiCarouselView"], ["VisibleViews", "VisibleViewsProperty"]),
        (CoreChildren, CoreLayouts, ["Count", "IsReadOnly", "Item"]),
        ("Filled in place: SetRowDefinitions / AddRowDefinition, SetColumnDefinitions / AddColumnDefinition", ["SkUiCoreGrid"], ["RowDefinitions", "ColumnDefinitions"]),
        (CoreNotTemplated, ["SkUiCoreContentView", "SkUiCoreIndicatorView"], ["ControlTemplate"]),
        (CoreNotTemplated, ["SkUiCoreIndicatorView"], ["IndicatorTemplate", "IndicatorLayout"]),
        ("The Core indicator takes Count instead of items", ["SkUiCoreIndicatorView"], ["ItemsSource"]),
        ("Set through SetSource and its overloads (file, URI, stream, MAUI ImageSource)", ["SkUiCoreImage", "SkUiCoreImageButton"], ["Source"]),
        ("A hint MAUI's handlers pass to the platform image view; Core images have no use for it", ["SkUiCoreImage", "SkUiCoreImageButton"], ["IsOpaque"]),
        ("CornerRadii (a CornerRadius per corner) and SetCornerRadius(double)", ["SkUiCoreButton", "SkUiCoreImageButton"], ["CornerRadius"]),
        ("Spans and SetSpans / AddSpan; MAUI's FormattedString is a tree of bindable objects", ["SkUiCoreLabel"], ["FormattedText"]),
        ("Core nodes have no bindings: ItemDisplay takes a Func<object?, string>", ["SkUiCorePicker"], ["ItemDisplayBinding"]),
        ("The Core slider raises DragStarted / DragCompleted", ["SkUiCoreSlider"], ["DragStartedCommand", "DragCompletedCommand"]),
        ("The Core radio button draws the circle only: its text is a separate Core label, its group SkUiCoreRadioButtons.Group",
            ["SkUiCoreRadioButton"],
            [
                "BorderColor", "BorderWidth", "CascadeInputTransparent", "CharacterSpacing", "Content", "ControlTemplate", "CornerRadius", "FontAttributes",
                "FontAutoScalingEnabled", "FontFamily", "FontSize", "GroupName", "IsClippedToBounds", "Padding", "TextColor", "TextTransform", "Value",
            ]));

    /// <summary>Gaps a planned step closes, keyed like <see cref="Excluded"/>, with the step (none planned now: reordering, the last, shipped in 1.3).</summary>
    private static readonly Dictionary<string, string> Planned = Entries();

    private static readonly Lazy<List<(string Key, string Problem)>> DrawnDifferences =
        new(() => [.. Pairs.SelectMany(pair => Differences(pair.Maui, pair.Drawn))]);

    private static readonly Lazy<List<(string Key, string Problem)>> CoreTwinDifferences =
        new(() => [.. Pairs.Where(pair => pair.Core is not null).SelectMany(pair => CoreDifferences(pair.Maui, pair.Core!))]);

    [Fact]
    public void DrawnControlsHaveMauisPublicApi() => AssertExplained(DrawnDifferences.Value);

    [Fact]
    public void CoreTwinsHaveMauisOwnProperties() => AssertExplained(CoreTwinDifferences.Value);

    [Fact]
    public void EveryEntryIsStillNeeded()
    {
        var all = DrawnDifferences.Value.Concat(CoreTwinDifferences.Value).ToList();
        var keys = all.Select(difference => difference.Key).ToHashSet();
        var members = all.Select(difference => Member(difference.Key)).ToHashSet();
        var stale = Excluded.Keys.Concat(Planned.Keys).Where(key => !keys.Contains(key))
            .Concat(PlumbingEverywhere.Keys.Where(member => !members.Contains(member)))
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.True(stale.Count == 0, "Match MAUI now, remove their entries: " + string.Join(", ", stale));
        Assert.Empty(Excluded.Keys.Intersect(Planned.Keys));
    }

    private static Dictionary<string, string> Entries(params (string Reason, string[] Types, string[] Members)[] groups)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (reason, types, members) in groups)
        {
            foreach (var type in types)
            {
                foreach (var member in members)
                    entries.Add($"{type}.{member}", reason);
            }
        }
        return entries;
    }

    private static string Member(string key) => key[(key.IndexOf('.') + 1)..];

    private static void AssertExplained(IEnumerable<(string Key, string Problem)> differences)
    {
        var unexplained = differences
            .Where(difference => !Excluded.ContainsKey(difference.Key) && !Planned.ContainsKey(difference.Key) && !PlumbingEverywhere.ContainsKey(Member(difference.Key)))
            .Select(difference => $"{difference.Key}: {difference.Problem}")
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.True(unexplained.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, unexplained));
    }

    private static IEnumerable<(string Key, string Problem)> Differences(Type maui, Type drawn)
    {
        foreach (var property in maui.GetProperties(Instance).Where(IsCompared).DistinctBy(property => property.Name))
        {
            var key = Key(drawn, property.Name);
            var match = FindProperty(drawn, property.Name);
            if (match is null)
            {
                yield return (key, "missing");
                continue;
            }
            if (property.GetMethod?.IsPublic == true && !property.PropertyType.IsAssignableFrom(match.PropertyType))
                yield return (key, $"reads as {Name(match.PropertyType)}, MAUI's as {Name(property.PropertyType)}");
            if (property.SetMethod?.IsPublic != true)
                continue;
            if (match.SetMethod?.IsPublic != true)
                yield return (key, "cannot be set");
            else if (!match.PropertyType.IsAssignableFrom(property.PropertyType))
                yield return (key, $"takes {Name(match.PropertyType)}, MAUI's takes {Name(property.PropertyType)}");
        }

        foreach (var mauiEvent in maui.GetEvents(Instance).Where(IsCompared).DistinctBy(mauiEvent => mauiEvent.Name))
        {
            var key = Key(drawn, mauiEvent.Name);
            var match = FindEvent(drawn, mauiEvent.Name);
            if (match is null)
                yield return (key, "missing");
            else if (!EventArgs(mauiEvent).IsAssignableFrom(EventArgs(match)))
                yield return (key, $"handlers get {Name(EventArgs(match))}, MAUI's get {Name(EventArgs(mauiEvent))}");
        }

        foreach (var overloads in maui.GetMethods(Instance).Where(method => !method.IsSpecialName && method.DeclaringType != typeof(object) && IsCompared(method))
                     .GroupBy(method => method.Name))
        {
            var key = Key(drawn, overloads.Key);
            var candidates = drawn.GetMethods(Instance).Where(method => method.Name == overloads.Key).ToList();
            if (candidates.Count == 0)
            {
                yield return (key, "missing");
                continue;
            }
            foreach (var overload in overloads.Where(method => !method.IsGenericMethodDefinition && !candidates.Any(candidate => Accepts(candidate, method))))
                yield return (key, $"no overload takes ({string.Join(", ", overload.GetParameters().Select(parameter => Name(parameter.ParameterType)))})");
        }

        var drawnFields = drawn.GetFields(Static).Where(field => field.FieldType == typeof(BindableProperty)).Select(field => field.Name).ToHashSet();
        foreach (var field in maui.GetFields(Static).Where(field => field.FieldType == typeof(BindableProperty) && IsCompared(field)).DistinctBy(field => field.Name))
        {
            if (!drawnFields.Contains(field.Name))
                yield return (Key(drawn, field.Name), "missing");
        }
    }

    private static IEnumerable<(string Key, string Problem)> CoreDifferences(Type maui, Type core)
    {
        foreach (var property in maui.GetProperties(Instance)
                     .Where(property => IsCompared(property) && !property.DeclaringType!.IsAssignableFrom(typeof(View)))
                     .DistinctBy(property => property.Name))
        {
            var match = FindProperty(core, property.Name);
            if (match is null)
                yield return (Key(core, property.Name), "missing");
            else if (property.SetMethod?.IsPublic == true && match.SetMethod?.IsPublic != true)
                yield return (Key(core, property.Name), "cannot be set");
        }
    }

    private static string Key(Type type, string member) => $"{type.Name}.{member}";

    /// <summary>Public, not obsolete, not hidden from IntelliSense, not an explicit interface implementation.</summary>
    private static bool IsCompared(MemberInfo member) =>
        !member.Name.Contains('.')
        && member.GetCustomAttribute<ObsoleteAttribute>() is null
        && member.GetCustomAttribute<EditorBrowsableAttribute>()?.State != EditorBrowsableState.Never;

    /// <summary>The most derived property of that name (a <c>new</c> member hides its base's).</summary>
    private static PropertyInfo? FindProperty(Type type, string name)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var property = current.GetProperties(Instance | BindingFlags.DeclaredOnly).FirstOrDefault(property => property.Name == name);
            if (property is not null)
                return property;
        }
        return null;
    }

    private static EventInfo? FindEvent(Type type, string name)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.GetEvent(name, Instance | BindingFlags.DeclaredOnly) is { } found)
                return found;
        }
        return null;
    }

    private static Type EventArgs(EventInfo info) => info.EventHandlerType!.GetMethod("Invoke")!.GetParameters()[^1].ParameterType;

    /// <summary>Whether a call that compiles against MAUI's <paramref name="maui"/> overload compiles against <paramref name="candidate"/>.</summary>
    private static bool Accepts(MethodInfo candidate, MethodInfo maui)
    {
        var expected = maui.GetParameters();
        var actual = candidate.GetParameters();
        if (candidate.IsGenericMethodDefinition || actual.Length < expected.Length || actual.Skip(expected.Length).Any(parameter => !parameter.IsOptional))
            return false;
        return expected.Zip(actual).All(pair => pair.Second.ParameterType.IsAssignableFrom(pair.First.ParameterType));
    }

    private static string Name(Type type) =>
        type.IsGenericType ? $"{type.Name[..type.Name.IndexOf('`')]}<{string.Join(", ", type.GetGenericArguments().Select(Name))}>" : type.Name;
}
