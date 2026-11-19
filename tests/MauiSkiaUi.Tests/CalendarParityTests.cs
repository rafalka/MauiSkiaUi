using System.Reflection;
using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// The calendar's two layers stay at parity (1.15 A4): every bindable property of the SkUi control has a CLR property of the same
/// name and type on its Core twin, both have a fluent <c>SetX</c> for every writable one, and their public methods and events match.
/// A difference that is meant goes in the class's list of intended differences, with the reason.
/// </summary>
public class CalendarParityTests
{
    /// <summary>Members one layer has on purpose and the other does not.</summary>
    private static readonly HashSet<string> IntendedDifferences =
    [
        // The SkUi layer gives a property back to the look with ClearValue; Core has no bindable store.
        "SkUiCoreCalendar.ClearTodayStyle()",
        "SkUiCoreCalendar.ClearSelectionShape()",
    ];

    public static TheoryData<Type, Type> Pairs => new()
    {
        { typeof(SkUiCalendar), typeof(SkUiCoreCalendar) },
        { typeof(SkUiDateRangePicker), typeof(SkUiCoreDateRangePicker) },
    };

    [Theory]
    [MemberData(nameof(Pairs))]
    public void EveryBindablePropertyHasACoreTwinAndSetters(Type skUi, Type core)
    {
        var missing = new List<string>();
        foreach (var field in skUi.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            if (field.FieldType != typeof(BindableProperty) || !field.Name.EndsWith("Property", StringComparison.Ordinal))
                continue;
            var bindable = (BindableProperty)field.GetValue(null)!;
            var name = bindable.PropertyName;
            if (IntendedDifferences.Contains($"{core.Name}.{name}"))
                continue;
            var skUiProperty = skUi.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            var coreProperty = core.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (coreProperty is null)
            {
                missing.Add($"{core.Name}.{name}");
                continue;
            }
            if (skUiProperty is not null && !SameLayerType(skUiProperty.PropertyType, coreProperty.PropertyType))
                missing.Add($"{core.Name}.{name} is {coreProperty.PropertyType.Name}, {skUi.Name}.{name} is {skUiProperty.PropertyType.Name}");
            if (bindable.IsReadOnly)
                continue;
            if (!HasSetter(skUi, name, bindable.ReturnType))
                missing.Add($"{skUi.Name}.Set{name}");
            if (!HasSetter(core, name, coreProperty.PropertyType))
                missing.Add($"{core.Name}.Set{name}");
        }
        Assert.Empty(missing);
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void PublicMethodsAndEventsMatch(Type skUi, Type core)
    {
        var skUiMembers = DeclaredMethodsAndEvents(skUi);
        var coreMembers = DeclaredMethodsAndEvents(core);
        var onlySkUi = skUiMembers.Except(coreMembers).Where(m => !IntendedDifferences.Contains($"{skUi.Name}.{m}"));
        var onlyCore = coreMembers.Except(skUiMembers).Where(m => !IntendedDifferences.Contains($"{core.Name}.{m}"));
        Assert.Empty(onlySkUi.Select(m => $"{skUi.Name}.{m}").Concat(onlyCore.Select(m => $"{core.Name}.{m}")));
    }

    /// <summary>The same type, or the layers' image convention: MAUI's <c>ImageSource</c> on SkUi, <see cref="SkUiImageSource"/> on Core.</summary>
    private static bool SameLayerType(Type skUi, Type core) =>
        skUi == core || (skUi == typeof(ImageSource) && core == typeof(SkUiImageSource));

    private static bool HasSetter(Type type, string name, Type valueType) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Any(m =>
            m.Name == "Set" + name && m.GetParameters() is [var value] && value.ParameterType.IsAssignableFrom(valueType));

    /// <summary>
    /// Methods (not property accessors, not the fluent setters: the bindable test covers those) and events the type declares,
    /// as "Name(ParameterTypes)" and "event Name".
    /// </summary>
    private static HashSet<string> DeclaredMethodsAndEvents(Type type)
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var members = new HashSet<string>(StringComparer.Ordinal);
        foreach (var method in type.GetMethods(Flags))
        {
            if (method.IsSpecialName || method.Name.StartsWith("Set", StringComparison.Ordinal))
                continue;
            members.Add($"{method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name))})");
        }
        foreach (var e in type.GetEvents(Flags))
            members.Add($"event {e.Name}");
        return members;
    }
}
