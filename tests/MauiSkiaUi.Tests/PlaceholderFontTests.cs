using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// <c>PlaceholderFontFamily</c> and <c>PlaceholderFontAttributes</c> (issue #116): the hint of an empty picker or text field is laid
/// out and drawn in its own font, the value in the field's; <c>null</c> falls back to the field's font. Both layers.
/// </summary>
public sealed class PlaceholderFontTests : IDisposable
{
    private const double Tolerance = 0.5;
    private const double FontSize = 40;
    private static readonly string[] Items = ["One", "Two"];

    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly IDisposable _font = SkUiTestHelpers.UseBundledFont();
    private readonly IDisposable _gapFont = SkUiTestHelpers.UseBundledFontWithLineGap();

    public void Dispose()
    {
        _gapFont.Dispose();
        _font.Dispose();
        _ui.Dispose();
    }

    private static double Height(IView view) => view.Measure(400, double.PositiveInfinity).Height;

    [Fact]
    public void PickerLaysTheHintOutInThePlaceholderFontAndTheValueInItsOwn()
    {
        var picker = new SkUiPicker { FontFamily = SkUiTestHelpers.BundledFontFamily, FontSize = FontSize, Title = "Choose", ItemsSource = Items };
        var regular = Height(picker);
        // The line-gap font has taller lines: the hint in it makes the field taller.
        picker.SetPlaceholderFontFamily(SkUiTestHelpers.LineGapFontFamily);
        var hint = Height(picker);
        Assert.True(hint > regular + 5, $"hint {hint}, regular {regular}");
        // A value shows in the field's font.
        picker.SelectedIndex = 0;
        Assert.True(Math.Abs(Height(picker) - regular) < Tolerance);
        // Back to the field's font.
        picker.SelectedIndex = -1;
        picker.PlaceholderFontFamily = null;
        Assert.True(Math.Abs(Height(picker) - regular) < Tolerance);
    }

    [Fact]
    public void CorePickerLaysTheHintOutInThePlaceholderFont()
    {
        var picker = new SkUiCorePicker().SetItemsSource(Items).SetTitle("Choose");
        picker.SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetFontSize(FontSize);
        var regular = picker.Measure(400, double.PositiveInfinity).Height;
        Assert.Same(picker, picker.SetPlaceholderFontFamily(SkUiTestHelpers.LineGapFontFamily).SetPlaceholderFontAttributes(FontAttributes.Italic));
        Assert.Equal((SkUiTestHelpers.LineGapFontFamily, FontAttributes.Italic), (picker.PlaceholderFontFamily, picker.PlaceholderFontAttributes));
        Assert.True(picker.Measure(400, double.PositiveInfinity).Height > regular + 5);
        picker.SetSelectedIndex(1);
        Assert.True(Math.Abs(picker.Measure(400, double.PositiveInfinity).Height - regular) < Tolerance);
    }

    [Fact]
    public void PickerDrawsTheHintWithThePlaceholderAttributes()
    {
        byte[] Draw(FontAttributes field, FontAttributes? placeholder) => Render(new SkUiDatePicker
        {
            Date = null, Placeholder = "No due date", FontFamily = SkUiTestHelpers.BundledFontFamily, FontAttributes = field,
            PlaceholderFontAttributes = placeholder
        });
        AssertDrawsWithThePlaceholderAttributes(Draw);
    }

    [Fact]
    public void EntryDrawsTheHintWithThePlaceholderAttributes()
    {
        byte[] Draw(FontAttributes field, FontAttributes? placeholder) => Render(new SkUiEntry
        {
            Placeholder = "No due date", FontFamily = SkUiTestHelpers.BundledFontFamily, FontAttributes = field, PlaceholderFontAttributes = placeholder
        });
        AssertDrawsWithThePlaceholderAttributes(Draw);
    }

    [Theory]
    [InlineData(TextAlignment.Center)]
    [InlineData(TextAlignment.End)]
    public void EntryAlignsTheHintByItsOwnLines(TextAlignment alignment)
    {
        // The hint in the line-gap font (taller lines) sits where a field in that font puts it, not where the value font's
        // shorter line would: neither lower nor clipped at the bottom.
        var overridden = Render(new SkUiEntry
        {
            Placeholder = "Name", FontFamily = SkUiTestHelpers.BundledFontFamily, PlaceholderFontFamily = SkUiTestHelpers.LineGapFontFamily,
            VerticalTextAlignment = alignment
        });
        var inherited = Render(new SkUiEntry { Placeholder = "Name", FontFamily = SkUiTestHelpers.LineGapFontFamily, VerticalTextAlignment = alignment });
        Assert.Equal(inherited, overridden);
    }

    [Theory]
    [InlineData(TextAlignment.Center)]
    [InlineData(TextAlignment.End)]
    public void EditorAlignsTheHintByItsOwnLines(TextAlignment alignment)
    {
        // Multiline, shorter than the box: the hint's lines are aligned as a block by their own height.
        var overridden = Render(new SkUiEditor
        {
            Placeholder = "Notes", FontFamily = SkUiTestHelpers.BundledFontFamily, PlaceholderFontFamily = SkUiTestHelpers.LineGapFontFamily,
            VerticalTextAlignment = alignment
        }, 120);
        var inherited = Render(new SkUiEditor { Placeholder = "Notes", FontFamily = SkUiTestHelpers.LineGapFontFamily, VerticalTextAlignment = alignment }, 120);
        Assert.Equal(inherited, overridden);
    }

    [Fact]
    public void EmptyEditorPutsTheCaretAtTheTopOfAWrappedCenteredHint()
    {
        float CaretTop(string? placeholder)
        {
            var editor = new SkUiEditor { Placeholder = placeholder, FontFamily = SkUiTestHelpers.BundledFontFamily, VerticalTextAlignment = TextAlignment.Center };
            using var surface = new SkUiTestSurface(new SkUiContentView { Content = editor }, 160, 200);
            editor.Focus();
            surface.Frame();
            return Assert.IsType<SkUiCaretDrawable>(editor.RenderState.Committed.Drawable).Rect.Top;
        }
        var alone = CaretTop(null);
        var oneLine = CaretTop("Notes");
        var wrapped = CaretTop("A hint long enough to wrap onto three lines");
        Assert.True(Math.Abs(oneLine - alone) < Tolerance, $"one line {oneLine}, alone {alone}");
        // Three lines centered: the first starts about a line above the middle one.
        Assert.True(wrapped < alone - 10, $"wrapped {wrapped}, alone {alone}");
    }

    /// <summary>
    /// A bold hint in a regular field draws as a bold field's hint, a regular hint in a bold field as a regular field's, and
    /// the two differ (Roboto Mono has no bold face: bold is synthesized).
    /// </summary>
    private static void AssertDrawsWithThePlaceholderAttributes(Func<FontAttributes, FontAttributes?, byte[]> draw)
    {
        var bold = draw(FontAttributes.Bold, null);
        var regular = draw(FontAttributes.None, null);
        Assert.NotEqual(bold, regular);
        Assert.Equal(bold, draw(FontAttributes.None, FontAttributes.Bold));
        Assert.Equal(regular, draw(FontAttributes.Bold, FontAttributes.None));
    }

    [Fact]
    public void EntryLaysTheHintOutInThePlaceholderFont()
    {
        var entry = new SkUiEntry { FontFamily = SkUiTestHelpers.BundledFontFamily, FontSize = FontSize, Placeholder = "Name" };
        var regular = Height(entry);
        Assert.Same(entry, entry.SetPlaceholderFontFamily(SkUiTestHelpers.LineGapFontFamily));
        Assert.True(Height(entry) > regular + 5);
        entry.Text = "Ann";
        Assert.True(Math.Abs(Height(entry) - regular) < Tolerance);
    }

    [Fact]
    public void CoreEditorLaysTheHintOutInThePlaceholderFont()
    {
        var editor = new SkUiCoreEditor().SetPlaceholder("Notes");
        editor.SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetFontSize(FontSize);
        var regular = editor.Measure(400, double.PositiveInfinity).Height;
        Assert.Same(editor, editor.SetPlaceholderFontFamily(SkUiTestHelpers.LineGapFontFamily).SetPlaceholderFontAttributes(FontAttributes.Bold));
        Assert.Equal(FontAttributes.Bold, editor.PlaceholderFontAttributes);
        Assert.True(editor.Measure(400, double.PositiveInfinity).Height > regular + 5);
    }

    /// <summary>Draws <paramref name="view"/> on white, 300 wide and <paramref name="height"/> tall, and returns its pixels.</summary>
    private static byte[] Render(SkUiView view, int height = 60)
    {
        using var surface = new SkUiTestSurface(new SkUiContentView { Content = view, Background = Colors.White }, 300, height);
        return surface.Frame().Bytes;
    }
}

/// <summary>The placeholder font from XAML (<c>PlaceholderFontAttributes</c> is a nullable enum).</summary>
[Collection(RuntimeXamlCollection.Name)]
public sealed class PlaceholderFontXamlTests
{
    [Fact]
    public void XamlSetsThePlaceholderFont()
    {
        const string xaml = """
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <sk:SkUiVerticalStackLayout>
                <sk:SkUiDatePicker PlaceholderFontFamily="OpenSansRegular" PlaceholderFontAttributes="Italic" />
                <sk:SkUiEntry PlaceholderFontFamily="OpenSansRegular" PlaceholderFontAttributes="Bold,Italic" />
                <sk:SkUiPicker />
              </sk:SkUiVerticalStackLayout>
            </ContentView>
            """;
        var root = new ContentView();
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var children = ((SkUiVerticalStackLayout)root.Content).Children;
        var datePicker = (SkUiDatePicker)children[0];
        var entry = (SkUiEntry)children[1];
        var picker = (SkUiPicker)children[2];
        Assert.Equal(("OpenSansRegular", FontAttributes.Italic), (datePicker.PlaceholderFontFamily, datePicker.PlaceholderFontAttributes));
        Assert.Equal(("OpenSansRegular", FontAttributes.Bold | FontAttributes.Italic), (entry.PlaceholderFontFamily, entry.PlaceholderFontAttributes));
        Assert.Equal((null, null), (picker.PlaceholderFontFamily, picker.PlaceholderFontAttributes));
    }
}
