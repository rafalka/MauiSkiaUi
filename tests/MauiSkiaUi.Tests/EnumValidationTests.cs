using System.Reflection;
using System.Runtime.InteropServices;
using CommunityToolkit.Maui.Layouts;
using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Every enum property validates (decision D6 of plan 1.19): each enum <see cref="BindableProperty"/> the library and its
/// packages declare, attached ones included, has a <c>validateValue</c> that takes every defined value (for a flags enum,
/// every defined flag together) and rejects an undefined one, so MAUI ignores it with a warning on every path, and its
/// fluent <c>SetX</c> throws for an undefined one. <see cref="TwinParityTests"/> checks the Core twins throw alike.
/// </summary>
public class EnumValidationTests
{
    private static readonly Assembly[] Assemblies =
    [
        typeof(SkUiView).Assembly,
        typeof(Markdown.SkUiMarkdownView).Assembly,
        typeof(MauiSkiaUi.CommunityToolkit.SkUiDockLayout).Assembly,
        typeof(Markdown.Math.SkUiMarkdownMath).Assembly,
    ];

    /// <summary>Properties that take only some of the defined values, with the reason.</summary>
    private static readonly Dictionary<string, string> Subsets = new(StringComparer.Ordinal)
    {
        ["SkUiPicker.SelectionMode"] = "a picker selects one item or several: None and SingleDeselect are the collection view's",
    };

    /// <summary>MAUI's and the Community Toolkit's own properties SkiaUi exposes again: they validate as their owners do.</summary>
    private static readonly BindableProperty[] Reused =
    [
        FlexLayout.OrderProperty, FlexLayout.GrowProperty, FlexLayout.ShrinkProperty, FlexLayout.AlignSelfProperty, FlexLayout.BasisProperty,
        DockLayout.DockPositionProperty,
    ];

    private static readonly PropertyInfo ValidateValue =
        typeof(BindableProperty).GetProperty("ValidateValue", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("MAUI's BindableProperty.ValidateValue is gone; find where it keeps validateValue now.");

    [Fact]
    public void EveryEnumBindablePropertyRejectsUndefinedValues()
    {
        var properties = EnumProperties().ToList();
        Assert.NotEmpty(properties);
        var failures = new List<string>();
        foreach (var (name, property) in properties)
        {
            var enumType = Nullable.GetUnderlyingType(property.ReturnType) ?? property.ReturnType;
            if (ValidateValue.GetValue(property) is not BindableProperty.ValidateValueDelegate validate)
            {
                failures.Add($"{name} ({enumType.Name}) has no validateValue");
                continue;
            }
            if (!Subsets.ContainsKey(name))
                foreach (var value in Defined(enumType))
                    if (!validate(null!, value))
                        failures.Add($"{name} rejects the defined value {value}");
            if (Nullable.GetUnderlyingType(property.ReturnType) is not null && !validate(null!, null))
                failures.Add($"{name} rejects null");
            var undefined = Undefined(enumType);
            if (validate(null!, undefined))
                failures.Add($"{name} takes the undefined value {Convert.ToInt64(undefined)}");
        }
        if (failures.Count > 0)
            Assert.Fail(string.Join(Environment.NewLine, failures.Order(StringComparer.Ordinal)));
    }

    /// <summary>Enum properties without a one-value fluent setter of their own, with the reason.</summary>
    private static readonly Dictionary<string, string> WithoutSetter = new(StringComparer.Ordinal)
    {
        ["SkUiAccessibility.LiveRegion"] = "attached: SkUiAccessibility.SetLiveRegion(view, value)",
        ["SkUiShimmer.Skeleton"] = "attached: SkUiShimmer.SetSkeleton(view, value)",
        ["SkUiAvatarView.FontAttributes"] = "set with SetFont(family, size, attributes), as on Core",
        ["SkUiAvatarView.BadgePosition"] = "set with SetBadge(color, position, borderColor), as on Core",
    };

    [Fact]
    public void EveryFluentEnumSetterThrowsOnUndefinedValues()
    {
        var failures = new List<string>();
        var withoutSetter = new List<string>();
        var types = Assemblies.SelectMany(a => a.GetTypes()).Select(t => Closed(t)).OfType<Type>().ToList();
        foreach (var (name, property) in EnumProperties())
        {
            var undefined = Undefined(Nullable.GetUnderlyingType(property.ReturnType) ?? property.ReturnType);
            var probed = false;
            // The declaring type's setter, and each derived type's that hides it ("public new SkUiEntry SetFontAttributes").
            foreach (var type in types.Where(t => t == property.DeclaringType || t.IsSubclassOf(property.DeclaringType)))
            {
                var setter = type.GetMethod($"Set{property.PropertyName}", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, [property.ReturnType]);
                if (setter is null)
                    continue;
                if (Concrete(type, types) is not { } concrete)
                {
                    failures.Add($"{name}: no type to probe {type.Name}.{setter.Name} on");
                    continue;
                }
                probed = true;
                try
                {
                    setter.Invoke(Activator.CreateInstance(concrete), [undefined]);
                    failures.Add($"{name}: {type.Name}.{setter.Name} takes the undefined value {Convert.ToInt64(undefined)}");
                }
                catch (TargetInvocationException e) when (e.InnerException is ArgumentOutOfRangeException)
                {
                }
            }
            if (!probed)
                withoutSetter.Add(name);
        }
        failures.AddRange(withoutSetter.Except(WithoutSetter.Keys).Select(n => $"{n} has no fluent setter: add one, or list it in WithoutSetter"));
        failures.AddRange(WithoutSetter.Keys.Except(withoutSetter).Select(n => $"{n} is listed in WithoutSetter but has a fluent setter now"));
        if (failures.Count > 0)
            Assert.Fail(string.Join(Environment.NewLine, failures.Order(StringComparer.Ordinal)));
    }

    [Flags]
    private enum ByteFlags : byte { A = 1, B = 2 }

    [Flags]
    private enum SignedByteFlags : sbyte { A = 1, Top = -128 }

    [Flags]
    private enum LongFlags : long { A = 1, Top = long.MinValue }

    [Fact]
    public void FlagsHelpersReadEveryUnderlyingType()
    {
        Assert.True(SkUiValidate.IsFlags(ByteFlags.A | ByteFlags.B));
        Assert.False(SkUiValidate.IsFlags((ByteFlags)4));
        Assert.False(SkUiValidate.IsFlags((ByteFlags)128));
        Assert.True(SkUiValidate.IsFlags(SignedByteFlags.A | SignedByteFlags.Top));
        Assert.False(SkUiValidate.IsFlags((SignedByteFlags)2));
        Assert.False(SkUiValidate.IsFlags((SignedByteFlags)(-128 | 4)));
        Assert.False(SkUiValidate.IsFlags((SignedByteFlags)(-1)));
        Assert.True(SkUiValidate.IsFlags(LongFlags.A | LongFlags.Top));
        Assert.False(SkUiValidate.IsFlags((LongFlags)(1L << 40)));
        Assert.False(SkUiValidate.IsFlags((LongFlags)2));
        Assert.True(SkUiValidate.OptionalFlags<ByteFlags>(null!, null));
        Assert.False(SkUiValidate.OptionalFlags<ByteFlags>(null!, (ByteFlags)8));
        SkUiValidate.ThrowIfNotFlags((ByteFlags?)null, "value");
        Assert.Throws<ArgumentOutOfRangeException>(() => SkUiValidate.ThrowIfNotFlags((ByteFlags?)8, "value"));
    }

    [Fact]
    public void DefinedAndFlagsHelpersFollowTheEnum()
    {
        Assert.True(SkUiValidate.Defined<SkUiPanAxis>(null!, SkUiPanAxis.Vertical));
        Assert.False(SkUiValidate.Defined<SkUiPanAxis>(null!, (SkUiPanAxis)9));
        Assert.False(SkUiValidate.Defined<SkUiPanAxis>(null!, 1));
        Assert.False(SkUiValidate.Defined<SkUiPanAxis>(null!, null));
        Assert.True(SkUiValidate.Defined<FontAttributes>(null!, FontAttributes.Bold));
        Assert.False(SkUiValidate.Defined<FontAttributes>(null!, FontAttributes.Bold | FontAttributes.Italic));

        Assert.True(SkUiValidate.Flags<FontAttributes>(null!, FontAttributes.Bold | FontAttributes.Italic));
        Assert.True(SkUiValidate.Flags<FontAttributes>(null!, FontAttributes.None));
        Assert.False(SkUiValidate.Flags<FontAttributes>(null!, (FontAttributes)8));
        Assert.True(SkUiValidate.IsFlags(SwipeDirection.Left | SwipeDirection.Down));
        Assert.True(SkUiValidate.IsFlags((SwipeDirection)0));
        Assert.False(SkUiValidate.IsFlags((SwipeDirection)16));

        Assert.Throws<ArgumentOutOfRangeException>(() => SkUiValidate.ThrowIfNotDefined((SkUiPanAxis)9, "axis"));
        Assert.Throws<ArgumentOutOfRangeException>(() => SkUiValidate.ThrowIfNotFlags((FontAttributes)8, "attributes"));
        SkUiValidate.ThrowIfNotDefined(SkUiPanAxis.Both, "axis");
        SkUiValidate.ThrowIfNotFlags(FontAttributes.Bold | FontAttributes.Italic, "attributes");
    }

    [Fact]
    public void UndefinedValuesAreIgnoredOnTheBindablePropertyAndThrowInBothSetters()
    {
        var label = new SkUiLabel();
        label.HorizontalTextAlignment = TextAlignment.Center;
        label.HorizontalTextAlignment = (TextAlignment)99;
        label.FontAttributes = (FontAttributes)8;
        Assert.Equal(TextAlignment.Center, label.HorizontalTextAlignment);
        Assert.Equal(FontAttributes.None, label.FontAttributes);
        Assert.Throws<ArgumentOutOfRangeException>(() => label.SetHorizontalTextAlignment((TextAlignment)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => label.SetFontAttributes((FontAttributes)8));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiCoreLabel().SetHorizontalTextAlignment((TextAlignment)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiCoreLabel().SetFontAttributes((FontAttributes)8));

        // Defined flags together still go through.
        label.SetFontAttributes(FontAttributes.Bold | FontAttributes.Italic);
        Assert.Equal(FontAttributes.Bold | FontAttributes.Italic, label.FontAttributes);
    }

    private static IEnumerable<(string Name, BindableProperty Property)> EnumProperties()
    {
        foreach (var type in Assemblies.SelectMany(a => a.GetTypes()).Where(DeclaresBindableProperties)
                     .Select(t => Closed(t) ?? throw new InvalidOperationException($"Add an argument {t} takes to Closed.")))
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            if (field.FieldType != typeof(BindableProperty) && field.FieldType != typeof(BindablePropertyKey))
                continue;
            var property = field.GetValue(null) switch
            {
                BindableProperty bindable => bindable,
                BindablePropertyKey key => key.BindableProperty,
                _ => null
            };
            if (property is null || field.FieldType == typeof(BindableProperty) && property.IsReadOnly)
                continue; // a read-only property is listed once, by its key, which only the control writes
            if (Reused.Contains(property))
                continue;
            if ((Nullable.GetUnderlyingType(property.ReturnType) ?? property.ReturnType).IsEnum)
                yield return ($"{type.Name}.{property.PropertyName}", property);
        }
    }

    /// <summary><paramref name="type"/>, or the first type deriving from it, that can be made with no arguments.</summary>
    private static Type? Concrete(Type type, IEnumerable<Type> types) =>
        new[] { type }.Concat(types.Where(t => t.IsSubclassOf(type)).OrderBy(t => t.FullName, StringComparer.Ordinal))
            .FirstOrDefault(t => !t.IsAbstract && !t.ContainsGenericParameters && t.GetConstructor(Type.EmptyTypes) is not null);

    private static bool DeclaresBindableProperties(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Any(f => f.FieldType == typeof(BindableProperty) || f.FieldType == typeof(BindablePropertyKey));

    /// <summary>
    /// A generic type closed over the first of a few arguments its constraints take (its static fields can only be read and
    /// it can only be made then), the type if it is not generic, or <c>null</c> if no argument fits.
    /// </summary>
    private static Type? Closed(Type type)
    {
        if (!type.IsGenericTypeDefinition)
            return type;
        foreach (var argument in new[] { typeof(object), typeof(double), typeof(int), typeof(Action) })
        {
            try
            {
                return type.MakeGenericType([.. type.GetGenericArguments().Select(_ => argument)]);
            }
            catch (ArgumentException)
            {
                // Violates a constraint: try the next.
            }
        }
        return null;
    }

    private static IEnumerable<object> Defined(Type enumType)
    {
        var values = Enum.GetValues(enumType).Cast<object>().ToList();
        if (enumType.GetCustomAttribute<FlagsAttribute>() is null)
            return values;
        var all = values.Aggregate(0L, (mask, flag) => mask | Convert.ToInt64(flag));
        return [.. values, Enum.ToObject(enumType, all)];
    }

    private static object Undefined(Type enumType)
    {
        var defined = Enum.GetValues(enumType).Cast<object>().Select(Convert.ToInt64).ToHashSet();
        if (enumType.GetCustomAttribute<FlagsAttribute>() is null)
            return Enum.ToObject(enumType, Enumerable.Range(2, 1000).First(v => !defined.Contains(v)));
        var mask = defined.Aggregate(0L, (all, flag) => all | flag);
        var bits = Marshal.SizeOf(Enum.GetUnderlyingType(enumType)) * 8;
        var free = Enumerable.Range(0, bits).Select(i => 1L << i).Where(b => (mask & b) == 0).ToList();
        if (free.Count == 0)
            throw new InvalidOperationException($"Every bit of {enumType} is a defined flag: no undefined value to probe with.");
        return Enum.ToObject(enumType, free[0]);
    }
}
