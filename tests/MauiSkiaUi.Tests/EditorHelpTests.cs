using MauiSkiaUiDemo;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>The component pages' (?) popups: their descriptions (<see cref="EditorHelp"/>) and the bubble that shows them.</summary>
// Demo pages set SkUiMauiContentView's static snapshot options (the overlay scrolling page) and create overlays
// the static capture hook would see: one collection with OverlayScrollTests and the other demo-page tests.
[Collection(nameof(OverlayScrollTests))]
public class EditorHelpTests
{
    private static long _pointer = 95_000;

    [Fact]
    public void EveryEditorHasADescription()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher(); // MAUI's RadioButton template binds its content labels
        var missing = new List<string>();
        foreach (var demo in ComponentDemos.All)
            foreach (var (title, property, help) in demo.Create().EditorDescriptions)
                if (property && help is null)
                    missing.Add($"{demo.PageType.Name}.{title}");
        // An editor named after a property of the control (or of a type the page documents, DocumentTypes) shows its XML
        // documentation; any other needs its section in MauiSkiaUiDemo/ComponentPages/EditorHelp.md.
        Assert.Empty(missing);
    }

    [Fact]
    public void EveryMarkdownSectionDescribesAnEditor()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var editors = new HashSet<string>();
        foreach (var demo in ComponentDemos.All)
            foreach (var (title, _, _) in demo.Create().EditorDescriptions)
                for (var type = demo.PageType; type != typeof(ContentPage); type = type.BaseType!)
                    editors.Add(type.Name + "." + title);
        Assert.NotEmpty(EditorHelp.Shared.SectionKeys);
        Assert.DoesNotContain(EditorHelp.Shared.SectionKeys, key => !editors.Contains(key));
    }

    [Fact]
    public void PropertiesShowTheirXmlDocumentationAsHtml()
    {
        var help = EditorHelp.Shared;
        Type[] label = [typeof(SkUiLabel), typeof(Label)];
        var shrink = help.Find(typeof(LabelDemoPage), label, nameof(SkUiLabel.ShrinkToFit));
        Assert.NotNull(shrink);
        Assert.Equal("SkUiLabel.ShrinkToFit", shrink.Source);
        Assert.StartsWith("<p>", shrink.Html);
        // A cref reads as the member's name (with its type when it is another type's), a <c> as code.
        Assert.Contains("<code>MinimumFontScale</code>", shrink.Html);
        Assert.Contains("<code>LineBreakMode.WordWrap</code>", shrink.Html);
        Assert.DoesNotContain("<see", shrink.Html);
        // Inherited from MAUI's VisualElement; dotted and annotated titles name a property too.
        Assert.Equal("VisualElement.WidthRequest", help.Find(typeof(LabelDemoPage), label, nameof(View.WidthRequest))?.Source);
        Assert.Equal("SkUiLabel.CornerRadii", help.Find(typeof(LabelDemoPage), label, "CornerRadii.TopLeft")?.Source);
        Assert.Equal("VisualElement.HeightRequest", help.Find(typeof(LabelDemoPage), label, "Content.HeightRequest")?.Source);
        Assert.Equal("SkUiPicker.MaxTokenLines", help.Find(typeof(PickerDemoPage), [typeof(SkUiPicker)], "MaxTokenLines (0: no limit)")?.Source);
        // A title that names no property has only its Markdown section; an action has no generated description.
        Assert.Null(help.Find(typeof(LabelDemoPage), label, "Not a property"));
        Assert.Null(help.Find(typeof(LabelDemoPage), label, nameof(SkUiLabel.Text), property: false));
    }

    [Fact]
    public void MarkdownSectionsWinAndConvertToHtml()
    {
        var script = EditorHelp.Shared.Find(typeof(LabelDemoPage), [typeof(SkUiLabel)], "Script");
        Assert.NotNull(script);
        Assert.Null(script.Source);
        Assert.Contains("<code>Text</code>", script.Html);
        // The base page's sections serve every page.
        Assert.NotNull(EditorHelp.Shared.Find(typeof(BoxDemoPage), [typeof(SkUiBox)], "Background brush"));

        Assert.Equal("<p>One <b>two</b> <i>three</i> <code>a&lt;b</code> <a href=\"https://x.org\">link</a></p><ul><li>a b</li><li>c</li></ul><p>End</p>",
            EditorHelp.MarkdownToHtml(["One **two** *three* `a<b` [link](https://x.org)", "", "- a", "  b", "- c", "", "End"]));
    }

    [Fact]
    public void HelpButtonOpensTheBubbleUntilTheNextTap()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var page = new LabelDemoPage();
        var root = Root(page.Editors);
        SkUiTestHelpers.Arrange(root, 400, 900);
        var button = Descendants(page.Editors).OfType<SkUiButton>().First(candidate => candidate.AutomationId == "Help" + nameof(SkUiLabel.FontSize));
        var bounds = RootBounds(button);

        Tap(root, bounds.Center);
        Assert.True(page.Help.IsOpen);
        Assert.Contains("Font size", page.Help.Text);
        SkUiTestHelpers.Arrange(root, 400, 900); // the surface's next layout
        var bubble = Descendants(root).OfType<SkUiBorder>().Single(view => view.AutomationId == "EditorHelp");
        Assert.True(bubble.Margin.Top >= bounds.Bottom, "The bubble opens below its button.");

        // A tap elsewhere closes it (the layer over the editors takes it, not the editor there), as does a tap on the button again.
        var elsewhere = new Point(200, 850);
        Assert.True(elsewhere.Y > bubble.Margin.Top + bubble.HeightRequest);
        Assert.Equal("EditorHelpDismiss", (SkUiDiagnostics.HitTest(root, elsewhere) as SkUiView)?.AutomationId);
        Tap(root, elsewhere);
        Assert.False(page.Help.IsOpen);
        SkUiTestHelpers.Arrange(root, 400, 900);
        Tap(root, bounds.Center);
        Assert.True(page.Help.IsOpen);
        SkUiTestHelpers.Arrange(root, 400, 900);
        Tap(root, bounds.Center);
        Assert.False(page.Help.IsOpen);
    }

    [Fact]
    public void HoveringAnEditorsTitleOpensTheBubbleUntilThePointerLeaves()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var page = new LabelDemoPage();
        var root = Root(page.Editors);
        SkUiTestHelpers.Arrange(root, 400, 900);
        var title = Descendants(page.Editors).OfType<SkUiLabel>().First(label => label.Text == nameof(SkUiLabel.FontSize));
        var over = RootBounds(title).Center;

        // As a tooltip: after a moment over the title, and without the layer that closes a tapped bubble.
        Hover(root, over);
        Assert.False(page.Help.IsOpen);
        TestDispatcherProvider.RunDelayed();
        Assert.True(page.Help.IsOpen);
        Assert.Contains("Font size", page.Help.Text);
        SkUiTestHelpers.Arrange(root, 400, 900);
        Assert.False(Descendants(root).Single(view => view.AutomationId == "EditorHelpDismiss").IsVisible);

        // The pointer moving onto the bubble keeps it; leaving both closes it, after a moment.
        var bubble = Descendants(root).OfType<SkUiBorder>().Single(view => view.AutomationId == "EditorHelp");
        Hover(root, new Point(bubble.Margin.Left + 20, bubble.Margin.Top + 20));
        TestDispatcherProvider.RunDelayed();
        Assert.True(page.Help.IsOpen);
        Hover(root, new Point(390, 890));
        Assert.True(page.Help.IsOpen);
        TestDispatcherProvider.RunDelayed();
        Assert.False(page.Help.IsOpen);
    }

    private static void Hover(SkUiView root, Point at) => root.Touch(new(1, SkUiTouchAction.HoverMoved, at));

    private static Rect RootBounds(SkUiView view) => SkUiDiagnostics.GetRootBounds(view) ?? throw new InvalidOperationException("Not in a drawn tree.");

    private static SkUiView Root(SkUiView view)
    {
        while (view.Parent is SkUiView parent)
            view = parent;
        return view;
    }

    private static void Tap(SkUiView root, Point at)
    {
        var id = ++_pointer;
        root.Touch(new(id, SkUiTouchAction.Pressed, at, TimeSpan.FromSeconds(1)));
        root.Touch(new(id, SkUiTouchAction.Released, at, TimeSpan.FromSeconds(1.02)));
    }

    private static IEnumerable<SkUiView> Descendants(SkUiView view)
    {
        foreach (var child in view.SkiaChildren.OfType<SkUiView>())
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }
}
