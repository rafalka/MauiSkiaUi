using System.Collections;
using System.Reflection;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// App-level implicit styles on drawn controls. MAUI applies them while its base <c>View</c> constructor runs
/// (<c>StyleableElement</c> → <c>MergedStyle</c> → <c>SetDynamicResource</c> finds the style in
/// <see cref="Application.Current"/>'s resources at once), so property change callbacks run before the control's own
/// constructor body: a control must neither crash there nor reset the styled state afterwards. Each control is styled
/// with every simple property set to a non-default value and must measure and draw exactly as the same control with the
/// values set after construction. Replaces <see cref="Application.Current"/>, so the class runs alone.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public class ImplicitStyleTests
{
    private const int Width = 240;
    private const int Height = 120;

    public static TheoryData<Type> DrawnViewTypes()
    {
        var data = new TheoryData<Type>();
        foreach (var type in typeof(SkUiView).Assembly.GetExportedTypes()
                     .Where(type => type.IsClass && !type.IsAbstract && typeof(SkUiView).IsAssignableFrom(type))
                     .OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            var closed = type.IsGenericTypeDefinition ? CloseGeneric(type) : type;
            if (closed?.GetConstructor(Type.EmptyTypes) is not null)
                data.Add(closed);
        }
        return data;
    }

    private static Type? CloseGeneric(Type definition)
    {
        try
        {
            return definition.MakeGenericType([.. definition.GetGenericArguments().Select(_ => typeof(int))]);
        }
        catch (ArgumentException)
        {
            return null; // constraints int does not meet
        }
    }

    [Theory]
    [MemberData(nameof(DrawnViewTypes))]
    public void AppImplicitStyleIsWhatTheControlDraws(Type type)
    {
        using var font = SkUiTestHelpers.UseBundledFont();
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var values = StyledValues(type);
        Assert.NotEmpty(values);

        using var app = new AppScope();
        var differing = Differences(app, type, values);
        if (differing is null)
            return;
        // Name the properties a style loses or breaks on their own, for the failure message.
        var culprits = values.Where(value => Differences(app, type, [value]) is not null)
            .Select(value => value.Property.PropertyName).ToList();
        Assert.Fail($"{type.Name}: {differing}; properties: {string.Join(", ", culprits)}");
    }

    [Fact]
    public void StyleClassAndDerivedTypeStylesApplyToDrawnLabels()
    {
        using var font = SkUiTestHelpers.UseBundledFont();
        using var app = new AppScope();
        app.Resources.Add(new Style(typeof(SkUiLabel))
        {
            ApplyToDerivedTypes = true,
            Setters =
            {
                new Setter { Property = SkUiLabel.FontFamilyProperty, Value = SkUiTestHelpers.BundledFontFamily },
                new Setter { Property = SkUiLabel.TextColorProperty, Value = Colors.Red },
                new Setter { Property = SkUiLabel.FontSizeProperty, Value = 30d }
            }
        });
        app.Resources.Add(new Style(typeof(SkUiLabel))
        {
            Class = "padded",
            Setters = { new Setter { Property = SkUiLabel.PaddingProperty, Value = new Thickness(10) } }
        });

        var label = new SkUiLabel { Text = "Hi", StyleClass = ["padded"] };
        // A button is a label: the label's implicit style applies to it while it is built too (ApplyToDerivedTypes).
        var button = new SkUiButton { Text = "Hi" };

        var plain = new SkUiLabel { Text = "Hi", FontFamily = SkUiTestHelpers.BundledFontFamily, FontSize = 30, TextColor = Colors.Red, Padding = new Thickness(10) };
        Assert.Equal(plain.Measure(Width, double.PositiveInfinity), label.Measure(Width, double.PositiveInfinity));
        Assert.Equal(Pixels(plain), Pixels(label));
        Assert.Equal(Colors.Red, button.TextColor);
        Assert.Contains(Pixels(button), pixel => pixel.Red > 200 && pixel.Green < 60 && pixel.Blue < 60);
    }

    /// <summary>How a control styled with <paramref name="values"/> differs from one given them after construction; <c>null</c>: it does not.</summary>
    private static string? Differences(AppScope app, Type type, IReadOnlyList<(BindableProperty Property, object? Value)> values)
    {
        var style = new Style(type);
        foreach (var (property, value) in values)
            style.Setters.Add(new Setter { Property = property, Value = value });
        app.Resources.Add(style);
        SkUiView styled;
        try
        {
            styled = Create(type);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is { } inner)
        {
            return $"construction threw {inner.GetType().Name} at {inner.StackTrace?.Split('\n')[0].Trim()}";
        }
        finally
        {
            app.Resources.Remove(type.FullName!);
        }
        var direct = Create(type);
        foreach (var (property, value) in values)
            direct.SetValue(property, value);
        foreach (var (property, value) in values)
            if (!Equals(styled.GetValue(property), value))
                return $"{property.PropertyName} is {styled.GetValue(property)}, expected {value}";

        var expectedSize = ((IView)direct).Measure(Width, double.PositiveInfinity);
        var actualSize = ((IView)styled).Measure(Width, double.PositiveInfinity);
        if (expectedSize != actualSize)
            return $"measures {actualSize}, expected {expectedSize}";
        return Pixels(direct).SequenceEqual(Pixels(styled)) ? null : "draws differently";
    }

    private static SkUiView Create(Type type) => (SkUiView)Activator.CreateInstance(type)!;

    private static SKColor[] Pixels(SkUiView view)
    {
        SkUiTestHelpers.Arrange(view, Width, Height);
        using var bitmap = new SKBitmap(Width, Height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            view.Paint(canvas);
        }
        return bitmap.Pixels;
    }

    /// <summary>
    /// A non-default value for each settable property of a simple type declared by SkiaUi (and the view basics MAUI
    /// declares), checked on a probe instance given the values chosen so far: the control takes it (validation,
    /// coercion), reports it back and keeps the earlier ones (no mirrored pair such as <c>CornerRadius</c> /
    /// <c>CornerRadii</c>, whose later setter a style cannot apply over the mirror's local value).
    /// </summary>
    private static List<(BindableProperty Property, object? Value)> StyledValues(Type type)
    {
        var defaults = Create(type);
        var values = new List<(BindableProperty Property, object? Value)>();
        var seen = new HashSet<BindableProperty>();
        for (var declaring = type; declaring is not null && declaring != typeof(object); declaring = declaring.BaseType)
        {
            var fromSkiaUi = declaring.Assembly == typeof(SkUiView).Assembly;
            foreach (var field in declaring.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (field.GetValue(null) is not BindableProperty property || property.IsReadOnly || !seen.Add(property))
                    continue;
                if ((!fromSkiaUi && !MauiViewProperties.Contains(property)) || NotStyled.Contains(property.PropertyName))
                    continue;
                var current = defaults.GetValue(property);
                foreach (var candidate in Candidates(property, current))
                {
                    if (Equals(candidate, current) || !Takes(type, [.. values, (property, candidate)]))
                        continue;
                    values.Add((property, candidate));
                    break;
                }
            }
        }
        return values;
    }

    /// <summary>
    /// Interaction state a control writes itself (a text field moves its caret when the text is set): its local value
    /// hides a style's, on MAUI's controls too.
    /// </summary>
    private static readonly HashSet<string> NotStyled = ["CursorPosition", "SelectionLength"];

    /// <summary>MAUI view properties drawn controls read (SkUiView's <c>OnPropertyChanged</c> overrides).</summary>
    private static readonly HashSet<BindableProperty> MauiViewProperties =
    [
        VisualElement.BackgroundColorProperty, VisualElement.BackgroundProperty, VisualElement.OpacityProperty,
        VisualElement.IsEnabledProperty, VisualElement.WidthRequestProperty, VisualElement.HeightRequestProperty,
        VisualElement.MinimumHeightRequestProperty, View.MarginProperty, VisualElement.FlowDirectionProperty
    ];

    private static bool Takes(Type type, IReadOnlyList<(BindableProperty Property, object? Value)> values)
    {
        try
        {
            var probe = Create(type);
            foreach (var (property, value) in values)
                probe.SetValue(property, value);
            return values.All(value => Equals(probe.GetValue(value.Property), value.Value));
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static IEnumerable<object?> Candidates(BindableProperty property, object? current)
    {
        var type = Nullable.GetUnderlyingType(property.ReturnType) ?? property.ReturnType;
        if (property.PropertyName.Contains("FontFamily", StringComparison.Ordinal))
            yield return SkUiTestHelpers.BundledFontFamily;
        else if (property.PropertyName == "MaxLength")
            yield return 100; // longer than any text here: a numeric entry's formatted value never has to be cut
        else if (type == typeof(string))
            yield return "Styled text";
        else if (type == typeof(bool))
            yield return current is not true;
        else if (type == typeof(double) || type == typeof(float) || type == typeof(int))
            foreach (var number in Numbers(current))
                yield return Convert.ChangeType(number, type, System.Globalization.CultureInfo.InvariantCulture);
        else if (type.IsEnum)
            foreach (var value in Enum.GetValues(type))
                yield return value;
        else if (type == typeof(Color))
        {
            yield return Colors.Red;
            yield return Colors.Blue;
        }
        else if (type == typeof(Brush))
            yield return new SolidColorBrush(Colors.Green);
        else if (type == typeof(Thickness))
            yield return new Thickness(3, 4, 5, 6);
        else if (type == typeof(CornerRadius))
            yield return new CornerRadius(5, 6, 7, 8);
        else if (type == typeof(TimeSpan))
            yield return TimeSpan.FromHours(13.5);
        else if (type == typeof(DateTime))
            yield return new DateTime(2020, 5, 6);
        else if (type == typeof(DataTemplate))
            yield return ItemTemplate;
        else if (type == typeof(IEnumerable) || type == typeof(IList))
            yield return Items;
    }

    // Shared by every control a style sets them on, as a style's values are (views are not: one view has one parent).
    private static readonly string[] Items = ["One", "Two", "Three"];
    private static readonly DataTemplate ItemTemplate = new(() => new SkUiLabel { FontFamily = SkUiTestHelpers.BundledFontFamily, Text = "Item" });

    private static IEnumerable<double> Numbers(object? current)
    {
        var value = Convert.ToDouble(current ?? 0d, System.Globalization.CultureInfo.InvariantCulture);
        if (value >= 0 && value < 1000)
            yield return value + 3;
        yield return 7;
        yield return 0.5;
        yield return 2;
        yield return 1;
        yield return 40;
    }

    /// <summary>An <see cref="Application.Current"/> with resources for the test, restoring the previous one when disposed.</summary>
    private sealed class AppScope : IDisposable
    {
        private readonly Application? _previous = Application.Current;
        private readonly Application _app = new();

        public ResourceDictionary Resources => _app.Resources;

        public void Dispose() => Application.Current = _previous;
    }
}
