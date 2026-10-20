using System.Reflection;
using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// The shared property sets (<see cref="ISkUiFontElement"/>, <see cref="ISkUiFieldChromeElement"/>, <see cref="ISkUiBorderElement"/>):
/// every drawn control with the set implements it, a control and its Core twin implement the same ones, and setting through the
/// interface is setting the control's property.
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class ElementInterfaceTests
{
    private static readonly Type[] ElementInterfaces = [typeof(ISkUiFontElement), typeof(ISkUiFieldChromeElement), typeof(ISkUiBorderElement)];

    // The Core radio button is only the ring (no text or border), so it has no twin here.
    private static readonly (Type SkUi, Type Core)[] Twins =
    [
        (typeof(SkUiLabel), typeof(SkUiCoreLabel)),
        (typeof(SkUiButton), typeof(SkUiCoreButton)),
        (typeof(SkUiImageButton), typeof(SkUiCoreImageButton)),
        (typeof(SkUiAvatarView), typeof(SkUiCoreAvatar)),
        (typeof(SkUiInputView), typeof(SkUiCoreInputView)),
        (typeof(SkUiEntry), typeof(SkUiCoreEntry)),
        (typeof(SkUiPickerBase), typeof(SkUiCorePickerBase)),
        (typeof(SkUiPicker), typeof(SkUiCorePicker)),
        (typeof(SkUiStepperBase), typeof(SkUiCoreStepperBase))
    ];

    private static IEnumerable<Type> DrawnControls() => typeof(SkUiView).Assembly.GetExportedTypes()
        .Where(type => type.IsClass && (typeof(SkUiView).IsAssignableFrom(type) || typeof(SkUiCoreNode).IsAssignableFrom(type)));

    private static bool HasAllProperties(Type type, Type contract) => contract.GetProperties().All(member =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(property => property.Name == member.Name && property.PropertyType == member.PropertyType && property.CanWrite));

    [Fact]
    public void EveryDrawnControlWithAPropertySetImplementsItsInterface()
    {
        var missing = (from contract in ElementInterfaces
                       from type in DrawnControls()
                       where HasAllProperties(type, contract) && !contract.IsAssignableFrom(type)
                       select $"{type.Name}: {contract.Name}").ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public void ControlsAndTheirCoreTwinsImplementTheSameInterfaces()
    {
        foreach (var (skUi, core) in Twins)
            Assert.Equal(ElementInterfaces.Where(contract => contract.IsAssignableFrom(skUi)).Select(contract => contract.Name),
                ElementInterfaces.Where(contract => contract.IsAssignableFrom(core)).Select(contract => contract.Name));
        Assert.True(typeof(ISkUiFontElement).IsAssignableFrom(typeof(SkUiRadioButton)));
    }

    [Fact]
    public void StepperCornerRadiusAndBorderWidthHaveTheSameTypesOnBothLayers()
    {
        foreach (var name in new[] { nameof(SkUiStepperBase.CornerRadius), nameof(SkUiStepperBase.BorderWidth), nameof(SkUiStepperBase.BorderColor) })
            Assert.Equal(typeof(SkUiStepperBase).GetProperty(name)!.PropertyType, typeof(SkUiCoreStepperBase).GetProperty(name)!.PropertyType);
    }

    private static void Pill(ISkUiFieldChromeElement field)
    {
        field.FillColor = Colors.Beige;
        field.BorderColor = Colors.Teal;
        field.BorderWidth = 0;
        field.CornerRadii = new CornerRadius(22);
    }

    [Fact]
    public void FieldChromeSetThroughTheInterfaceIsTheFieldsOnBothLayers()
    {
        var entry = new SkUiEntry();
        var picker = new SkUiPicker();
        var coreEntry = new SkUiCoreEntry();
        var corePicker = new SkUiCorePicker();
        ISkUiFieldChromeElement[] fields = [entry, picker, coreEntry, corePicker];
        foreach (var field in fields)
            Pill(field);

        Assert.Equal((Colors.Beige, Colors.Teal, (double?)0, (CornerRadius?)new CornerRadius(22)),
            (entry.FillColor, entry.BorderColor, entry.BorderWidth, entry.CornerRadii));
        Assert.Equal((Colors.Beige, Colors.Teal, (double?)0, (CornerRadius?)new CornerRadius(22)),
            (coreEntry.FillColor, coreEntry.BorderColor, coreEntry.BorderWidth, coreEntry.CornerRadii));
        Assert.Equal((picker.FillColor, picker.CornerRadii), (corePicker.FillColor, corePicker.CornerRadii));

        // Invalid values: ignored on the SkUi layer (as MAUI does), thrown on the Core layer (as SetX does).
        ((ISkUiFieldChromeElement)entry).BorderWidth = -1;
        Assert.Equal(0d, entry.BorderWidth);
        Assert.Throws<ArgumentOutOfRangeException>(() => ((ISkUiFieldChromeElement)coreEntry).BorderWidth = -1);
        Assert.Equal(0d, coreEntry.BorderWidth);
    }

    [Fact]
    public void FontSetThroughTheInterfaceIsTheControlsOnBothLayers()
    {
        ISkUiFontElement[] elements = [new SkUiLabel(), new SkUiCoreButton(), new SkUiRadioButton(), new SkUiCoreAvatar(), new SkUiEntry(), new SkUiCorePicker(), new SkUiCoreStepper()];
        foreach (var element in elements)
        {
            element.FontFamily = SkUiTestHelpers.BundledFontFamily;
            element.FontSize = 21;
            element.FontAttributes = FontAttributes.Bold;
            element.FontAutoScalingEnabled = false;
        }

        Assert.All(elements, element => Assert.Equal((SkUiTestHelpers.BundledFontFamily, 21d, FontAttributes.Bold, false),
            (element.FontFamily, element.FontSize, element.FontAttributes, element.FontAutoScalingEnabled)));
        Assert.Equal(21d, ((SkUiLabel)elements[0]).FontSize);
        Assert.Equal(21d, ((SkUiCoreStepper)elements[6]).FontSize);
    }

    [Fact]
    public void BorderSetThroughTheInterfaceIsTheControlsOnBothLayers()
    {
        var button = new SkUiButton();
        var coreButton = new SkUiCoreButton();
        var imageButton = new SkUiImageButton();
        var coreImageButton = new SkUiCoreImageButton();
        ISkUiBorderElement[] elements = [button, coreButton, imageButton, coreImageButton];
        foreach (var element in elements)
        {
            element.BorderColor = Colors.Teal;
            element.BorderWidth = 2;
            element.CornerRadii = new CornerRadius(1, 2, 3, 4);
        }

        Assert.All(elements, element => Assert.Equal((Colors.Teal, 2d, new CornerRadius(1, 2, 3, 4)),
            (element.BorderColor, element.BorderWidth, element.CornerRadii)));
        // MAUI's int CornerRadius on the SkUi layer is the uniform view of the radii: it reads the top-left one.
        Assert.Equal(1, button.CornerRadius);
        Assert.Equal(1, imageButton.CornerRadius);
    }
}
