using MauiSkiaUi;
using MauiSkiaUiDemo;
using Microsoft.Maui.Controls;
using Xunit;

namespace MauiSkiaUi.Tests;

public class ComponentDemoTests
{
    [Fact]
    public void EveryConcreteComponentHasOneDedicatedDemo()
    {
        var controls = typeof(SkUiView).Assembly.GetTypes().Where(type => type.IsPublic && !type.IsAbstract && typeof(SkUiView).IsAssignableFrom(type)).OrderBy(type => type.Name);
        Assert.Equal(controls, ComponentDemos.All.Select(demo => demo.ComponentType).OrderBy(type => type.Name));
        Assert.Equal(ComponentDemos.All.Count, ComponentDemos.All.Select(demo => demo.PageType).Distinct().Count());
        Assert.Equal(ComponentDemos.All.Count, ComponentDemos.All.Select(demo => demo.Route).Distinct().Count());
    }

    [Fact]
    public void EveryDemoBelongsToADisplayedCategory()
    {
        foreach (var demo in ComponentDemos.All)
            Assert.Contains(demo.Category, ComponentCategoryInfo.Order);
        // Every declared category must have at least one demo, or its gallery section would be dead weight.
        foreach (var category in ComponentCategoryInfo.Order)
            Assert.Contains(ComponentDemos.All, demo => demo.Category == category);
    }

    [Fact]
    public void AllPagesHaveWorkingEditorsAndResetWithoutHandlers()
    {
        foreach (var demo in ComponentDemos.All)
        {
            var page = demo.Create();
            Assert.Equal(demo.PageType, page.GetType());
            Assert.Equal(demo.ComponentType, page.SkiaControl.GetType());
            Assert.Empty(page.CheckProperties());
            foreach (var slider in Descendants(page.Editors).OfType<Slider>()) slider.Value = (slider.Minimum + slider.Maximum) / 2;
            foreach (var toggle in Descendants(page.Editors).OfType<Switch>()) toggle.IsToggled = !toggle.IsToggled;
            foreach (var picker in Descendants(page.Editors).OfType<Picker>()) picker.SelectedIndex = picker.Items.Count - 1;
            Assert.Empty(page.CheckProperties());
            page.ResetProperties();
            Assert.Empty(page.CheckProperties());
            Assert.Null(page.SkiaControl.Handler);
            if (page.SkiaControl is SkUiImage image) image.Dispose();
        }
    }

    [Fact]
    public void LabelEditorsUpdateBothPreviewsAndReset()
    {
        var page = new LabelDemoPage();
        Assert.Empty(page.CheckProperties());
        var editor = Descendants(page.Editors).OfType<Editor>().Single();
        editor.Text = "Changed\nMultiline";
        Assert.Equal("Changed\nMultiline", ((SkUiLabel)page.SkiaControl).Text);
        Assert.Equal("Changed\nMultiline", ((Label)page.NativeControl!).Text);
        Assert.Empty(page.CheckProperties());
        page.ResetProperties();
        Assert.NotEqual("Changed\nMultiline", editor.Text);
        Assert.Empty(page.CheckProperties());
        page.UpdateComparisonLayout(900);
        Assert.True(page.IsWide);
        page.UpdateComparisonLayout(390);
        Assert.False(page.IsWide);
    }

    [Fact]
    public void LabelFontFamilyChoiceUsesRegisteredAndSystemNames()
    {
        var page = new LabelDemoPage();
        var picker = Descendants(page.Editors).OfType<Picker>().Single(picker => picker.Title == "FontFamily");
        picker.SelectedIndex = picker.Items.IndexOf("Lobster");
        Assert.Equal("Lobster", ((SkUiLabel)page.SkiaControl).FontFamily);
        Assert.Equal("Lobster", ((Label)page.NativeControl!).FontFamily);
        Assert.Empty(page.CheckProperties());
        page.ResetProperties();
        Assert.Null(((SkUiLabel)page.SkiaControl).FontFamily);
    }

    private static IEnumerable<View> Descendants(Layout root)
    {
        foreach (var child in root.Children.OfType<View>())
        {
            yield return child;
            if (child is Layout layout)
                foreach (var nested in Descendants(layout)) yield return nested;
        }
    }
}