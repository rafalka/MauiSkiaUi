using System.Reflection;
using MauiSkiaUi.Core;
using Microsoft.Maui.Controls.Shapes;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Keeps the Core layer to FR-C1 (docs/design/Requirements.md, Core layer; decision D7 of the 1.19 plan): Core nodes are
/// plain CLR objects, and the <c>Microsoft.Maui.Controls</c> types their API uses are a list a new one has to join.
/// <list type="bullet">
/// <item>No public or protected type of <c>MauiSkiaUi.Core</c> (and namespaces below it) but <see cref="SkUiCoreHost"/> (the bridge into the SkUi tree) derives
/// from <see cref="BindableObject"/> or <see cref="Element"/>.</item>
/// <item>Every <c>Microsoft.Maui.Controls</c> type, and every other type that derives from one (an <c>SkUi*</c> view), in
/// a public or protected signature of those types (base types, interfaces, fields, properties, indexers, events,
/// constructors, methods, generic constraints, also inside generic arguments, arrays and function pointers) is in
/// <see cref="AllowedControlsTypes"/>, with the reason.</item>
/// </list>
/// <c>Microsoft.Maui</c> and <c>Microsoft.Maui.Graphics</c> types (<c>Thickness</c>, <c>Color</c>, <c>ScrollOrientation</c>)
/// are allowed by FR-C1 and not checked. The check is one level deep: the members of other SkiaUi types in those signatures
/// are not followed. The look (<c>SkUiLook</c>), popup requests (<c>SkUiPopupRequest</c>, through the pickers' protected
/// <c>ShowPopupAsync</c>) and view animations are shared with the SkUi* layer by design (FR-C6, popups on the app's popup
/// system) and reach MAUI views and pages there.
/// </summary>
public class CoreLayerContractTests
{
    private const string CoreNamespace = "MauiSkiaUi.Core";
    private const string ControlsNamespace = "Microsoft.Maui.Controls";

    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                          BindingFlags.Static | BindingFlags.DeclaredOnly;

    /// <summary>The one Core type that is a MAUI view: it hosts a Core tree inside the SkUi tree.</summary>
    private static readonly Type Bridge = typeof(SkUiCoreHost);

    /// <summary>
    /// The <c>Microsoft.Maui.Controls</c> types (and types derived from them) the Core API may use, with the reason.
    /// <see cref="EveryAllowedControlsTypeIsStillUsed"/> fails once one is no longer used, so the list stays the API's.
    /// </summary>
    private static readonly Dictionary<Type, string> AllowedControlsTypes = Allow(
    [
        ("FR-C1: MAUI's own enums, so a value reads the same on both layers", [
            typeof(FontAttributes),
            typeof(StackOrientation),
            typeof(LayoutAlignment),
            typeof(EditorAutoSizeOption),
            typeof(IndicatorShape),
            typeof(ItemsLayoutOrientation),
            typeof(ItemsUpdatingScrollMode),
            typeof(ScrollToPosition),
            typeof(SnapPointsType),
            typeof(SnapPointsAlignment),
            typeof(PenLineCap),
        ]),
        ("FR-C1: MAUI's own event args, so a handler is the same on both layers", [
            typeof(CheckedChangedEventArgs),
            typeof(CurrentItemChangedEventArgs),
            typeof(DateChangedEventArgs),
            typeof(ItemsViewScrolledEventArgs),
            typeof(PickerClosedEventArgs),
            typeof(PickerOpenedEventArgs),
            typeof(PositionChangedEventArgs),
            typeof(ScrolledEventArgs),
            typeof(TextChangedEventArgs),
            typeof(TimeChangedEventArgs),
            typeof(ToggledEventArgs),
            typeof(ValueChangedEventArgs),
        ]),
        ("FR-C1: picker popups' item, header and footer templates are MAUI views, built from MAUI's DataTemplate", [
            typeof(DataTemplate),
        ]),
        ("Stretches FR-C1, allowed in 1.x: segment and tab icons (SkUiCoreSegment.Icon, SkUiCoreTabItem.Icon); SkUiImageSource in 2.0 (Roadmap.md, Held for 2.0)", [
            typeof(ImageSource),
        ]),
        ("Stretches FR-C1, allowed in 1.x: SkUiCorePicker.SelectionBackground; a SkiaUi Paint in 2.0 (Roadmap.md, Held for 2.0)", [
            typeof(Brush),
        ]),
        ("Stretches FR-C1, allowed in 1.x: CalendarStyle on the Core date pickers styles the popup's SkUiCalendar; a Core way to set up the calendar in 2.0 (Roadmap.md, Held for 2.0)", [
            typeof(Style),
        ]),
        ("Stretches FR-C1, allowed in 1.x: SkUiCoreButton.ContentLayout; a SkiaUi value in 2.0 (Roadmap.md, Held for 2.0)", [
            typeof(Button.ButtonContentLayout),
        ]),
        ("Stretches FR-C1, allowed in 1.x: SkUiCoreMultiColumnPicker's columns are the SkUi picker's bindable columns, so one column can serve both layers; a plain Core column in 2.0 (Roadmap.md, Held for 2.0)", [
            typeof(SkUiPickerColumn),
        ]),
        ("Stretches FR-C1, allowed in 1.x: an app-placed SkUiCoreScrollBar can follow an SkUiScrollView; the SkUi scroller hands out the bar in 2.0 (Roadmap.md, Held for 2.0)", [
            typeof(SkUiScrollView),
        ]),
    ]);

    [Fact]
    public void OnlyTheHostIsABindableObjectOrElement()
    {
        var offenders = CoreTypes()
            .Where(t => t != Bridge && typeof(BindableObject).IsAssignableFrom(t))
            .Select(t => $"{TypeName(t)} derives from {Describe(FirstControlsBase(t)!)}")
            .ToList();

        Assert.True(offenders.Count == 0,
            "FR-C1: Core nodes are plain CLR objects; only SkUiCoreHost (the bridge) may be a BindableObject or Element:\n  " +
            string.Join("\n  ", offenders));
    }

    [Fact]
    public void EveryControlsTypeInTheCoreApiIsAllowed()
    {
        var offenders = ControlsTypeUses()
            .Where(kv => !AllowedControlsTypes.ContainsKey(kv.Key))
            .Select(kv => $"{Describe(kv.Key)}, used by {string.Join(", ", kv.Value)}")
            .ToList();

        Assert.True(offenders.Count == 0,
            "FR-C1: a Core API uses Microsoft.Maui.Controls types that are not on the allow-list. Use a SkiaUi, Microsoft.Maui " +
            "or Microsoft.Maui.Graphics type instead, or add the type to CoreLayerContractTests.AllowedControlsTypes with the reason:\n  " +
            string.Join("\n  ", offenders));
    }

    [Fact]
    public void EveryAllowedControlsTypeIsStillUsed()
    {
        var used = ControlsTypeUses();
        var stale = AllowedControlsTypes.Keys.Where(t => !used.ContainsKey(t)).Select(Describe).ToList();

        Assert.True(stale.Count == 0,
            "The Core API no longer uses these allowed Controls types; remove them from CoreLayerContractTests.AllowedControlsTypes " +
            "(and from Roadmap.md's Held for 2.0 if they are listed there):\n  " + string.Join("\n  ", stale));
    }

    [Fact]
    public void SignatureScanFindsEveryKindOfMember()
    {
        var uses = SignatureTypes(typeof(ScanProbe<>)).ToLookup(u => u.Member, u => u.Type);
        var probe = TypeName(typeof(ScanProbe<>));

        Assert.Contains(typeof(Brush), uses[$"{probe}.Field"]);
        Assert.Contains(typeof(Style), uses[$"{probe}.Property"]);
        Assert.Contains(typeof(ImageSource), uses[$"{probe}.this[key]"]);
        Assert.Contains(typeof(EventHandler<ToggledEventArgs>), uses[$"{probe}.event Changed"]);
        Assert.Contains(typeof(DataTemplate), uses[$"new {probe}(template)"]);
        Assert.Contains(typeof(IList<Label>), uses[$"{probe}.Method() return"]);
        Assert.Contains(typeof(FontAttributes[]), uses[$"{probe}.Method(attributes)"]);
        Assert.Contains(typeof(Element), uses[$"{probe}.Generic<TElement> constraint"]);
        Assert.Contains(typeof(LayoutAlignment), uses[$"{probe}.Protected"]);
        Assert.Contains(typeof(Label), uses[$"{probe}.ProtectedInternal"]);
        Assert.Empty(uses[$"{probe}.PrivateProtected"]);
        Assert.Empty(uses[$"{probe}.Internal"]);
        Assert.Empty(uses[$"{probe}.Private"]);
        Assert.Contains(typeof(List<Label>), uses[$"{probe} base type"]);
        Assert.Contains(typeof(IComparable<Brush>), uses[$"{probe} interface"]);
        Assert.Contains(typeof(Element), uses[$"{probe}<T> constraint"]);
        Assert.Contains(typeof(Label), SignatureTypes(typeof(ScanProbe<>.Callback)).Select(u => u.Type));
        Assert.Contains(typeof(Label), SignatureTypes(typeof(ScanProbe<>)).Select(u => u.Type).SelectMany(Flatten));
        Assert.Contains(typeof(FontAttributes), Flatten(typeof(FontAttributes[])));
        Assert.True(IsReachable(typeof(ScanProbe<>)));
        Assert.True(IsReachable(typeof(ScanProbe<>).GetNestedType("ProtectedNested", BindingFlags.NonPublic)!));
        Assert.False(IsReachable(typeof(ScanProbe<>).GetNestedType("PrivateNested", BindingFlags.NonPublic)!));
        Assert.True(IsCoreNamespace("MauiSkiaUi.Core.Sub"));
        Assert.False(IsCoreNamespace("MauiSkiaUi.CoreX"));
        Assert.True(IsControlsType(typeof(SkUiPickerColumn)));
        Assert.False(IsControlsType(typeof(Thickness)));
        Assert.False(IsControlsType(typeof(SkUiCoreNode)));
    }

    /// <summary>Each Controls type the Core API uses, with the members that use it.</summary>
    private static Dictionary<Type, SortedSet<string>> ControlsTypeUses()
    {
        var uses = new Dictionary<Type, SortedSet<string>>();
        foreach (var type in CoreTypes().Where(t => t != Bridge))
        foreach (var (member, signatureType) in SignatureTypes(type))
        foreach (var used in Flatten(signatureType).Where(t => t != Bridge && IsControlsType(t)))
        {
            if (!uses.TryGetValue(used, out var members))
                uses[used] = members = new SortedSet<string>(StringComparer.Ordinal);
            members.Add(member);
        }
        return uses;
    }

    /// <summary>The public types of the Core namespaces and their public and protected nested types.</summary>
    private static IEnumerable<Type> CoreTypes() =>
        typeof(SkUiCoreNode).Assembly.GetTypes().Where(t => IsCoreNamespace(t.Namespace) && IsReachable(t));

    private static bool IsCoreNamespace(string? ns) =>
        ns == CoreNamespace || ns?.StartsWith(CoreNamespace + ".", StringComparison.Ordinal) == true;

    /// <summary>Whether code outside the assembly can name <paramref name="type"/> (or, nested, a subclass of its declaring type can).</summary>
    private static bool IsReachable(Type type) =>
        type.IsNested
            ? (type.IsNestedPublic || type.IsNestedFamily || type.IsNestedFamORAssem) && IsReachable(type.DeclaringType!)
            : type.IsPublic;

    /// <summary>The types in the public and protected signature of <paramref name="type"/>'s own members.</summary>
    private static IEnumerable<(string Member, Type Type)> SignatureTypes(Type type)
    {
        var name = TypeName(type);
        if (type.BaseType is { } baseType)
            yield return ($"{name} base type", baseType);
        foreach (var i in type.GetInterfaces())
            yield return ($"{name} interface", i);
        foreach (var g in type.IsGenericTypeDefinition ? type.GetGenericArguments() : [])
        foreach (var c in g.GetGenericParameterConstraints())
            yield return ($"{name}<{g.Name}> constraint", c);
        foreach (var f in type.GetFields(Declared).Where(f => f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly))
            yield return ($"{name}.{f.Name}", f.FieldType);
        foreach (var p in type.GetProperties(Declared).Where(p => IsVisible(p.GetMethod) || IsVisible(p.SetMethod)))
        {
            var indexParameters = p.GetIndexParameters();
            var member = indexParameters.Length == 0
                ? $"{name}.{p.Name}"
                : $"{name}.this[{string.Join(", ", indexParameters.Select(ip => ip.Name))}]";
            yield return (member, p.PropertyType);
            foreach (var ip in indexParameters)
                yield return (member, ip.ParameterType);
        }
        foreach (var e in type.GetEvents(Declared).Where(e => IsVisible(e.AddMethod) || IsVisible(e.RemoveMethod)))
            yield return ($"{name}.event {e.Name}", e.EventHandlerType!);
        foreach (var c in type.GetConstructors(Declared).Where(IsVisible))
        foreach (var p in c.GetParameters())
            yield return ($"new {name}({p.Name})", p.ParameterType);
        foreach (var m in type.GetMethods(Declared).Where(m => IsVisible(m) && !IsAccessor(m)))
        {
            yield return ($"{name}.{m.Name}() return", m.ReturnType);
            foreach (var p in m.GetParameters())
                yield return ($"{name}.{m.Name}({p.Name})", p.ParameterType);
            foreach (var g in m.GetGenericArguments())
            foreach (var c in g.GetGenericParameterConstraints())
                yield return ($"{name}.{m.Name}<{g.Name}> constraint", c);
        }
    }

    private static bool IsVisible(MethodBase? method) =>
        method is not null && (method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly);

    /// <summary>Property and event accessors: their types are reported under the property or event.</summary>
    private static bool IsAccessor(MethodInfo method) =>
        method.IsSpecialName && (method.Name.StartsWith("get_", StringComparison.Ordinal) ||
                                 method.Name.StartsWith("set_", StringComparison.Ordinal) ||
                                 method.Name.StartsWith("add_", StringComparison.Ordinal) ||
                                 method.Name.StartsWith("remove_", StringComparison.Ordinal) ||
                                 method.Name.StartsWith("raise_", StringComparison.Ordinal));

    /// <summary>
    /// A type and the types inside it: element types of arrays, pointers and by-refs, generic definitions and arguments,
    /// function pointers' parameters and return.
    /// </summary>
    private static IEnumerable<Type> Flatten(Type type)
    {
        if (type.HasElementType)
            return Flatten(type.GetElementType()!);
        if (type.IsFunctionPointer)
            return type.GetFunctionPointerParameterTypes().Append(type.GetFunctionPointerReturnType()).SelectMany(Flatten);
        if (type.IsGenericParameter)
            return [];
        if (!type.IsGenericType)
            return [type];
        return type.GetGenericArguments().SelectMany(Flatten).Prepend(type.GetGenericTypeDefinition());
    }

    private static bool IsControlsType(Type type) => FirstControlsBase(type) is not null;

    /// <summary><paramref name="type"/> itself or its nearest base type in a <c>Microsoft.Maui.Controls</c> namespace.</summary>
    private static Type? FirstControlsBase(Type type)
    {
        for (var t = type; t is not null; t = t.BaseType)
            if (t.Namespace == ControlsNamespace || t.Namespace?.StartsWith(ControlsNamespace + ".", StringComparison.Ordinal) == true)
                return t;
        return null;
    }

    private static string Describe(Type type)
    {
        var controlsBase = FirstControlsBase(type);
        var name = $"{type.Namespace}.{TypeName(type)}";
        return controlsBase is null || controlsBase == type ? name : $"{name} (derives from {controlsBase.FullName})";
    }

    private static string TypeName(Type type) => type.DeclaringType is { } declaring ? $"{TypeName(declaring)}.{type.Name}" : type.Name;

    private static Dictionary<Type, string> Allow((string Reason, Type[] Types)[] groups) =>
        groups.SelectMany(g => g.Types.Select(t => (Type: t, g.Reason))).ToDictionary(e => e.Type, e => e.Reason);

    /// <summary>One member of each kind the scan reads, for <see cref="SignatureScanFindsEveryKindOfMember"/>.</summary>
    // ReSharper disable UnusedMember.Global, UnusedParameter.Local, EventNeverSubscribedTo.Global, UnusedType.Global, ClassNeverInstantiated.Global, MemberCanBePrivate.Global, UnusedType.Local
#pragma warning disable CS0067, CS0649, CS0169
    public class ScanProbe<T> : List<Label>, IComparable<Brush> where T : Element
    {
        public delegate Label Callback();

        public Brush? Field;
        protected LayoutAlignment Protected;
        protected internal Label? ProtectedInternal;
        private protected Label? PrivateProtected;
        internal Label? Internal;
        private Label? Private;

        public ScanProbe(DataTemplate template) { }

        public Style? Property { get; set; }

        public int this[ImageSource key] => 0;

        public event EventHandler<ToggledEventArgs>? Changed;

        public IList<Label> Method(FontAttributes[] attributes) => [];

        public void Generic<TElement>() where TElement : Element { }

        public int CompareTo(Brush? other) => 0;

        protected class ProtectedNested;

        private class PrivateNested;
    }
#pragma warning restore CS0067, CS0649, CS0169
    // ReSharper restore UnusedMember.Global, UnusedParameter.Local, EventNeverSubscribedTo.Global, UnusedType.Global, ClassNeverInstantiated.Global, MemberCanBePrivate.Global, UnusedType.Local
}
