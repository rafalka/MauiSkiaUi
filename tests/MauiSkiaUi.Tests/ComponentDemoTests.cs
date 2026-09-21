using MauiSkiaUi.Core;
using MauiSkiaUiDemo;
using Xunit;

namespace MauiSkiaUi.Tests;

public class ComponentDemoTests
{
    [Fact]
    public void EveryConcreteMauiCompatibleComponentHasOneDedicatedDemo()
    {
        // SkUiCoreHost is the Core↔MAUI bridge (Stress / Look / Core demos), not a gallery control.
        var controls = typeof(SkUiView).Assembly.GetTypes()
            .Where(type => type.IsPublic && !type.IsAbstract && typeof(SkUiView).IsAssignableFrom(type))
            .Where(type => type != typeof(SkUiCoreHost))
            .OrderBy(type => type.Name);
        Assert.Equal(controls, ComponentDemos.MauiCompatible.Select(demo => demo.ComponentType).OrderBy(type => type.Name));
        Assert.Equal(ComponentDemos.All.Count, ComponentDemos.All.Select(demo => demo.PageType).Distinct().Count());
        Assert.Equal(ComponentDemos.All.Count, ComponentDemos.All.Select(demo => demo.Route).Distinct().Count());
    }

    [Fact]
    public void CoreDemosTargetCoreNodeTypes()
    {
        Assert.NotEmpty(ComponentDemos.Core);
        foreach (var demo in ComponentDemos.Core)
        {
            Assert.Equal(ComponentCategory.Core, demo.Category);
            Assert.True(typeof(ISkUiCoreNode).IsAssignableFrom(demo.ComponentType),
                $"{demo.Name} should target an ISkUiCoreNode.");
        }
    }

    [Fact]
    public void EveryDemoBelongsToADisplayedCategory()
    {
        foreach (var demo in ComponentDemos.All)
            Assert.Contains(demo.Category, ComponentCategoryInfo.Order);
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
            Assert.True(ContainsComponent(page, demo.ComponentType),
                $"{demo.PageType.Name}'s preview does not contain a {demo.ComponentType.Name}.");
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

    private static bool ContainsComponent(ComponentDemoPage page, Type type)
    {
        if (ContainsInstanceOf(page.SkiaControl, type)) return true;
        if (page.SkiaControl is SkUiCoreHost { Content: { } root })
            return ContainsCoreInstance(root, type);
        return false;
    }

    private static bool ContainsInstanceOf(ISkUiView view, Type type)
    {
        if (type.IsInstanceOfType(view)) return true;
        return view is SkUiView node && node.SkiaChildren.Any(child => ContainsInstanceOf(child, type));
    }

    private static bool ContainsCoreInstance(ISkUiCoreNode node, Type type)
    {
        if (type.IsInstanceOfType(node)) return true;
        if (node is SkUiCorePanel panel)
        {
            foreach (var child in panel.Children)
            {
                if (ContainsCoreInstance(child, type))
                    return true;
            }
        }
        else if (node is SkUiCoreContentView { Content: { } content })
        {
            return ContainsCoreInstance(content, type);
        }

        return false;
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
        var picker = Descendants(page.Editors).OfType<Picker>().Single(picker => picker.Title == nameof(SkUiLabel.FontFamily));
        picker.SelectedIndex = picker.Items.IndexOf(DemoFonts.Lobster);
        Assert.Equal(DemoFonts.Lobster, ((SkUiLabel)page.SkiaControl).FontFamily);
        Assert.Equal(DemoFonts.Lobster, ((Label)page.NativeControl!).FontFamily);
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
