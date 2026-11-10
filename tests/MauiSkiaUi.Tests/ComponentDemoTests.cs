using MauiSkiaUi.Core;
using MauiSkiaUiDemo;
using Xunit;

namespace MauiSkiaUi.Tests;

// Demo pages set SkUiMauiContentView's static snapshot options (the overlay scrolling page) and create overlays
// the static capture hook would see: one collection with OverlayScrollTests and the other demo-page tests.
[Collection(nameof(OverlayScrollTests))]
public class ComponentDemoTests
{
    [Fact]
    public void EveryConcreteMauiCompatibleComponentHasADedicatedDemo()
    {
        // SkUiCoreHost is the Core↔MAUI bridge (Stress / Look / Core demos), not a gallery control.
        var controls = typeof(SkUiView).Assembly.GetTypes().Concat(typeof(Markdown.SkUiMarkdownView).Assembly.GetTypes())
            .Where(type => type.IsPublic && !type.IsAbstract && typeof(SkUiView).IsAssignableFrom(type))
            .Where(type => type != typeof(SkUiCoreHost))
            .OrderBy(type => type.Name);
        // A control may have several scenario pages (e.g. SkUiMauiContentView: hosting, and overlays in a scroller). A generic
        // control's demo shows one of its instantiations (SkUiStepper<decimal>).
        Assert.Equal(controls, ComponentDemos.MauiCompatible
            .Select(demo => demo.ComponentType.IsGenericType ? demo.ComponentType.GetGenericTypeDefinition() : demo.ComponentType)
            .Distinct().OrderBy(type => type.Name));
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
    public void EveryCategoryGalleryListsItsDemosUnderItsOwnRoute()
    {
        // The demo's flyout has one entry per category (DemoFlyout); the routes must not collide with the demo pages'.
        var routes = ComponentCategoryInfo.Order.Select(ComponentCategoryInfo.Route).ToList();
        Assert.Equal(routes.Count, routes.Distinct().Count());
        Assert.DoesNotContain(routes, route => ComponentDemos.All.Any(demo => demo.Route == route));
        foreach (var category in ComponentCategoryInfo.Order)
        {
            var gallery = new ComponentGalleryPage(category);
            var catalog = ((SkUiGrid)gallery.Content).Children.OfType<SkUiScrollView>().Single();
            var buttons = Descendants(catalog).OfType<SkUiButton>().ToList();
            var demos = ComponentDemos.All.Where(demo => demo.Category == category).ToList();
            Assert.Equal(demos.Select(demo => "Open" + demo.Name), buttons.Select(button => button.AutomationId));
            // Buttons name the real control type (never a route key) and what the page is for, then the counterpart.
            Assert.Equal(demos.Select(demo => demo.Title), buttons.Select(button => button.FormattedText!.Spans[0].Text));
            Assert.Equal(demos.Count, demos.Select(demo => demo.Title).Distinct().Count());
        }
    }

    [Fact]
    public void DemoTitlesSayWhatThePageIsFor()
    {
        var stepper = ComponentDemos.All.Single(demo => demo.PageType == typeof(TypedStepperDemoPage));
        Assert.Equal("SkUiStepper<decimal>", stepper.DisplayName);
        Assert.Equal("SkUiStepper<decimal>: typed values", stepper.Title);
        Assert.Equal("Replaces Stepper", stepper.Description);
        var shimmer = ComponentDemos.All.Single(demo => demo.PageType == typeof(ShimmerContentDemoPage));
        Assert.Equal(nameof(SkUiShimmer), shimmer.DisplayName);
        Assert.Equal("SkUiShimmer: skeleton built from the content", shimmer.Title);
        Assert.Equal("No MAUI counterpart", shimmer.Description);
        var label = ComponentDemos.All.Single(demo => demo.PageType == typeof(LabelDemoPage));
        Assert.Equal(nameof(SkUiLabel), label.Title);
        Assert.Equal("Replaces Label", label.Description);
        foreach (var demo in ComponentDemos.All.Where(demo => demo.Key is not null))
            Assert.False(string.IsNullOrEmpty(demo.Scenario), $"{demo.Name} is a scenario page and should describe its scenario.");
        // A control with several pages titles each one, its main page too, by what it is for.
        foreach (var demo in ComponentDemos.All.Where(demo => ComponentDemos.All.Count(other => other.ComponentType == demo.ComponentType && other.Category == demo.Category) > 1))
            Assert.False(string.IsNullOrEmpty(demo.Scenario), $"{demo.PageType.Name} shares its control with another page and should say what it is for.");
    }

    [Fact]
    public void AllPagesHaveWorkingEditorsAndResetWithoutHandlers()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher(); // MAUI's RadioButton template binds its content labels
        foreach (var demo in ComponentDemos.All)
        {
            var page = demo.Create();
            Assert.Equal(demo.PageType, page.GetType());
            Assert.Equal(demo.Title, page.Title);
            Assert.True(ContainsComponent(page, demo.ComponentType),
                $"{demo.PageType.Name}'s preview does not contain a {demo.ComponentType.Name}.");
            AssertChecksPass(page, "created");
            foreach (var slider in Descendants(page.Editors).OfType<SkUiSlider>()) slider.Value = (slider.Minimum + slider.Maximum) / 2;
            foreach (var toggle in Descendants(page.Editors).OfType<SkUiSwitch>()) toggle.IsToggled = !toggle.IsToggled;
            foreach (var picker in Descendants(page.Editors).OfType<SkUiPicker>()) picker.SelectedIndex = picker.Items.Count - 1;
            AssertChecksPass(page, "edited");
            page.ResetProperties();
            AssertChecksPass(page, "reset");
            Assert.Null(page.SkiaControl.Handler);
        }
    }

    private static void AssertChecksPass(ComponentDemoPage page, string when)
    {
        var failures = page.CheckProperties();
        Assert.True(failures.Length == 0, $"{page.GetType().Name} ({when}): {string.Join(", ", failures)}");
    }

    [Fact]
    public void EditorsFollowWhatTheUserChangesOnThePreviews()
    {
        var page = new EntryDemoPage();
        var skia = (SkUiEntry)page.SkiaControl;
        var native = (Entry)page.NativeControl!;
        var reveal = Editor<SkUiSwitch>(page, nameof(SkUiEntry.IsPasswordRevealed));
        var text = Editor<SkUiEntry>(page, nameof(SkUiEntry.Text));

        // The reveal button sets the property as the editor would have.
        skia.IsPasswordRevealed = true;
        Assert.True(reveal.IsToggled);
        // Typed on either preview: the editor shows it and applies it to the other preview.
        skia.Text = "typed in SkUi";
        Assert.Equal("typed in SkUi", text.Text);
        Assert.Equal("typed in SkUi", native.Text);
        native.Text = "typed in MAUI";
        Assert.Equal("typed in MAUI", text.Text);
        Assert.Equal("typed in MAUI", skia.Text);
        AssertChecksPass(page, "edited on the previews");
    }

    [Fact]
    public void AClearedNumericValueStaysCleared()
    {
        var page = new NumericEntryDemoPage();
        var skia = (SkUiNumericEntry)page.SkiaControl;
        var value = Editor<SkUiSlider>(page, nameof(SkUiNumericEntry.Value));
        skia.Value = null; // the user cleared the field (AllowNull)
        Assert.Null(skia.Value);
        Assert.Equal(25, value.Value);
        skia.Value = 40;
        Assert.Equal(40, value.Value);
    }

    [Fact]
    public void TilePhotosRemovedThenResetKeepTheSecondPhotoEditor()
    {
        var page = new TileLayoutDemoPage();
        var remove = Descendants(page.Editors).OfType<SkUiButton>().Single(button => button.AutomationId == "Removephoto");
        SkUiTestHelpers.Arrange(remove, 200, 40);
        for (var index = 0; index < 20; index++)
        {
            remove.Touch(new SkUiTouchEvent(2 * index + 1, SkUiTouchAction.Pressed, new Point(100, 20), TimeSpan.FromSeconds(index)));
            remove.Touch(new SkUiTouchEvent(2 * index + 1, SkUiTouchAction.Released, new Point(100, 20), TimeSpan.FromSeconds(index + 0.05)));
        }
        Assert.Equal(2, ((SkUiTileLayout)page.SkiaControl).Children.Count);
        page.ResetProperties();
        AssertChecksPass(page, "reset after removing photos");
    }

    [Fact]
    public void EditorsDoNotApplyAnEffectiveIsEnabled()
    {
        var page = new ButtonDemoPage();
        var canExecute = Editor<SkUiSwitch>(page, nameof(Command.CanExecute));
        var isEnabled = Editor<SkUiSwitch>(page, nameof(VisualElement.IsEnabled));

        // A command that cannot execute disables MAUI's button without its IsEnabled being set (MAUI coerces it); the editor
        // stays as set, and both buttons are enabled again with the command.
        canExecute.IsToggled = false;
        Assert.False(page.NativeControl!.IsEnabled);
        Assert.True(isEnabled.IsToggled);
        canExecute.IsToggled = true;
        Assert.True(page.SkiaControl.IsEnabled);
        Assert.True(page.NativeControl.IsEnabled);
    }

    private static T Editor<T>(ComponentDemoPage page, string name) where T : SkUiView =>
        Descendants(page.Editors).OfType<T>().Single(editor => editor.AutomationId == "Edit" + name);

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
        else if (node is SkUiCoreShimmer shimmer)
        {
            return (shimmer.SkeletonContent is { } skeleton && ContainsCoreInstance(skeleton, type))
                || (shimmer.Content is { } loaded && ContainsCoreInstance(loaded, type));
        }

        return false;
    }

    [Fact]
    public void LabelEditorsUpdateBothPreviewsAndReset()
    {
        var page = new LabelDemoPage();
        Assert.Empty(page.CheckProperties());
        var editor = Descendants(page.Editors).OfType<SkUiEditor>().Single();
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
    public void GalleryShowsTheNarrowScreenNoteWhileThePageShorterSideIsBelowSideBySide()
    {
        var gallery = new ComponentGalleryPage(ComponentCategory.BasicControls);
        Assert.False(gallery.ShowsNarrowNote);
        gallery.UpdateNarrowNote(390, 760); // phone, portrait
        Assert.True(gallery.ShowsNarrowNote);
        gallery.UpdateNarrowNote(844, 340); // phone, landscape: wide enough for side by side, but no room for the editors
        Assert.True(gallery.ShowsNarrowNote);
        gallery.UpdateNarrowNote(820, 1100); // tablet
        Assert.False(gallery.ShowsNarrowNote);
        gallery.UpdateNarrowNote(1280, ComponentDemoPage.SideBySideMinWidth); // desktop window
        Assert.False(gallery.ShowsNarrowNote);
    }

    [Fact]
    public void SearchFiltersDemosAndPagesByEveryWordOfTheQuery()
    {
        DemoSearchItem[] pages = [new("Stress", "Performance · Page", "//stress")];
        var search = new DemoSearchPage(pages);
        Assert.Equal(ComponentDemos.All.Count + pages.Length, search.Results.Count());
        Assert.Equal($"{ComponentDemos.All.Count + pages.Length} demos", search.Summary);
        // The MAUI counterpart and the category match as well as the control's type.
        search.SearchBar.Text = "searchbar";
        Assert.Equal(["demo-SkUiSearchBar"], search.Results.Select(item => item.Route));
        Assert.Equal("1 demo", search.Summary);
        search.SearchBar.Text = "  core   SHIMMER ";
        Assert.Equal(["demo-SkUiCoreShimmer", "demo-SkUiCoreShimmerBone"], search.Results.Select(item => item.Route));
        search.SearchBar.Text = "stress";
        Assert.Equal(["//stress"], search.Results.Select(item => item.Route));
        search.SearchBar.Text = "nothing like this";
        Assert.Empty(search.Results);
        Assert.Equal("No demo matches \"nothing like this\".", search.Summary);
    }

    [Fact]
    public void ControlSpecificEditorsComeBeforeTheCommonOnes()
    {
        var page = new GridDemoPage();
        var captions = Descendants(page.Editors).OfType<SkUiLabel>().Select(label => label.Text).ToList();
        Assert.Equal(nameof(SkUiGrid.Padding), captions[0]);
        var common = captions.IndexOf("Common properties");
        Assert.True(common > captions.IndexOf("SpanLastCell"));
        Assert.Equal("HwAccelerated", captions[common + 1]);
    }

    [Fact]
    public void LabelFontFamilyChoiceUsesRegisteredAndSystemNames()
    {
        var page = new LabelDemoPage();
        var picker = Descendants(page.Editors).OfType<SkUiPicker>().Single(picker => picker.Title == nameof(SkUiLabel.FontFamily));
        picker.SelectedIndex = picker.Items.IndexOf(DemoFonts.Lobster);
        Assert.Equal(DemoFonts.Lobster, ((SkUiLabel)page.SkiaControl).FontFamily);
        Assert.Equal(DemoFonts.Lobster, ((Label)page.NativeControl!).FontFamily);
        Assert.Empty(page.CheckProperties());
        page.ResetProperties();
        Assert.Null(((SkUiLabel)page.SkiaControl).FontFamily);
    }

    private static IEnumerable<SkUiView> Descendants(SkUiView root)
    {
        foreach (var child in root.SkiaChildren.OfType<SkUiView>())
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
