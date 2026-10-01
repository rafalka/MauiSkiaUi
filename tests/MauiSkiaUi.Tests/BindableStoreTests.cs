using System.Reflection;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// CLR getters of drawn controls read the bindable store, as MAUI's do, so a drawn control works as a binding source:
/// MAUI raises <c>PropertyChanged</c> before a property's change callback, so a getter backed by a field that the
/// callback updates would read the old value. Direct <c>Set*</c> setters write the store too.
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class BindableStoreTests
{
    private static string Bound(BindableObject source, BindableProperty property, object value)
    {
        var target = new Label();
        target.SetBinding(Label.TextProperty, new Binding(property.PropertyName, source: source, stringFormat: "{0}"));
        source.SetValue(property, value);
        return target.Text;
    }

    [Fact]
    public void DrawnControlsAreBindingSourcesThatSeeNewValues()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        Assert.Equal("b", Bound(new SkUiLabel { Text = "a" }, SkUiLabel.TextProperty, "b"));
        Assert.Equal(Colors.Red.ToString(), Bound(new SkUiSwitch(), SkUiSwitch.OnColorProperty, Colors.Red));
        Assert.Equal("0.5", Bound(new SkUiSlider(), SkUiSlider.ValueProperty, 0.5).Replace(',', '.'));
        Assert.Equal("1", Bound(new SkUiProgressBar(), SkUiProgressBar.ProgressProperty, 3d)); // coerced, as MAUI
        Assert.Equal("True", Bound(new SkUiCheckBox(), SkUiToggleControl.IsCheckedProperty, true));
        Assert.Equal("Horizontal", Bound(new SkUiScrollView(), SkUiScrollView.OrientationProperty, ScrollOrientation.Horizontal));
    }

    [Fact]
    public void DirectSettersAreTheMauiProperty()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var label = new SkUiLabel();
        var target = new Label();
        target.SetBinding(Label.TextProperty, new Binding(nameof(SkUiLabel.Text), source: label));
        label.SetText("fluent");
        Assert.Equal("fluent", target.Text);
        Assert.Equal("fluent", label.GetValue(SkUiLabel.TextProperty));

        var box = new SkUiCheckBox();
        box.SetCheckState(SkUiCheckState.Checked);
        Assert.True((bool)box.GetValue(SkUiToggleControl.IsCheckedProperty));
        var slider = new SkUiSlider().SetMaximum(10).SetSliderValue(20);
        Assert.Equal((10d, 10d), (slider.Maximum, slider.Value));
        slider.SetMaximum(30);
        Assert.Equal(20d, slider.Value); // the requested value comes back when the range widens
        Assert.Throws<ArgumentOutOfRangeException>(() => label.SetFontSize(-1));
        Assert.Equal(label.FontSize, (double)label.GetValue(SkUiLabel.FontSizeProperty)); // an invalid value never reaches the store
    }

    [Fact]
    public void XReferenceBindingsAndTriggersFollowDrawnControls()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        const string xaml = """
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <sk:SkUiVerticalStackLayout>
                <sk:SkUiCheckBox x:Name="Agree" />
                <sk:SkUiButton x:Name="Submit" Text="Submit" IsEnabled="{Binding IsChecked, Source={x:Reference Agree}}" />
                <sk:SkUiLabel x:Name="Status" Text="{Binding Text, Source={x:Reference Submit}, StringFormat='Button: {0}'}" />
              </sk:SkUiVerticalStackLayout>
            </ContentView>
            """;
        var root = new ContentView();
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var stack = (SkUiVerticalStackLayout)root.Content;
        var (agree, submit, status) = ((SkUiCheckBox)stack.Children[0], (SkUiButton)stack.Children[1], (SkUiLabel)stack.Children[2]);
        Assert.False(submit.IsEnabled);
        agree.IsChecked = true;
        Assert.True(submit.IsEnabled);
        submit.SetText("Send");
        Assert.Equal("Button: Send", status.Text);

        var trigger = new DataTrigger(typeof(SkUiLabel)) { Binding = new Binding(nameof(SkUiCheckBox.IsChecked), source: agree), Value = false };
        trigger.Setters.Add(new Setter { Property = SkUiLabel.TextColorProperty, Value = Colors.Gray });
        status.Triggers.Add(trigger);
        agree.SetIsChecked(false);
        Assert.Equal(Colors.Gray, status.TextColor);
    }

    [Fact]
    public void StyleValuesStillApplyWhenADirectSetterPassesTheSameValue()
    {
        var style = new Style(typeof(SkUiLabel)) { Setters = { new Setter { Property = SkUiLabel.TextColorProperty, Value = Colors.Red } } };
        var label = new SkUiLabel { Style = style };
        label.SetTextColor(Colors.Red); // same as the style: no local value over it
        label.Style = new Style(typeof(SkUiLabel)) { Setters = { new Setter { Property = SkUiLabel.TextColorProperty, Value = Colors.Blue } } };
        Assert.Equal(Colors.Blue, label.TextColor);
    }

    /// <summary>Painting and layout read private fields; after construction they must agree with the store on every control.</summary>
    [Fact]
    public void FieldsAgreeWithTheStoreOnEveryControl()
    {
        var mismatches = new List<string>();
        foreach (var type in typeof(SkUiView).Assembly.GetTypes().Where(type => type.IsPublic && !type.IsAbstract && typeof(SkUiView).IsAssignableFrom(type)
                     && type.GetConstructor(Type.EmptyTypes) is not null))
        {
            var view = (SkUiView)Activator.CreateInstance(type)!;
            for (var owner = type; owner is not null && owner.Assembly == type.Assembly; owner = owner.BaseType)
            {
                foreach (var declared in owner.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Where(f => f.FieldType == typeof(BindableProperty)))
                {
                    var property = (BindableProperty)declared.GetValue(null)!;
                    var name = "_" + char.ToLowerInvariant(property.PropertyName[0]) + property.PropertyName[1..];
                    FieldInfo? field = null;
                    for (var t = type; t is not null && field is null; t = t.BaseType)
                        field = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (field is null)
                        continue;
                    var (store, applied) = (view.GetValue(property), field.GetValue(view));
                    if (!Equals(store, applied) && !(store is null && applied is ""))
                        mismatches.Add($"{type.Name}.{property.PropertyName}: store {store ?? "null"}, field {applied ?? "null"}");
                }
            }
            (view as IDisposable)?.Dispose();
        }
        Assert.Empty(mismatches.Distinct());
    }
}
