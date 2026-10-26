using System.Windows.Input;
using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// MAUI 10's new members on drawn controls (step R3 in docs/design/ImplementationPlan-1.2.md), on both layers:
/// <c>Switch.OffColor</c> and <c>CheckBox.Command</c> / <c>CommandParameter</c>.
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class MauiTenMembersTests
{
    private const int SwitchWidth = 80;
    private const int SwitchHeight = 40;

    private static SKBitmap Render(Action<SKCanvas> paint)
    {
        var bitmap = new SKBitmap(SwitchWidth, SwitchHeight);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        paint(canvas);
        return bitmap;
    }

    private static SKBitmap RenderSwitch(Color? offColor)
    {
        var toggle = new SkUiSwitch { OffColor = offColor };
        SkUiTestHelpers.Arrange(toggle, SwitchWidth, SwitchHeight);
        return Render(toggle.Paint);
    }

    private static SKBitmap RenderCoreSwitch(Color? offColor)
    {
        var toggle = new SkUiCoreSwitch().SetOffColor(offColor);
        toggle.Measure(SwitchWidth, SwitchHeight);
        toggle.Arrange(new Rect(0, 0, SwitchWidth, SwitchHeight));
        return Render(toggle.Paint);
    }

    private static int RedPixels(SKBitmap bitmap) =>
        bitmap.Pixels.Count(pixel => pixel is { Alpha: > 200, Red: > 200, Green: < 60, Blue: < 60 });

    [Fact]
    public void SwitchOffColorPaintsTheOffTrackTheSameOnBothLayers()
    {
        using var defaultTrack = RenderSwitch(null);
        using var red = RenderSwitch(Colors.Red);
        using var coreRed = RenderCoreSwitch(Colors.Red);
        using var coreDefault = RenderCoreSwitch(null);

        Assert.Equal(0, RedPixels(defaultTrack));
        Assert.True(RedPixels(red) > 0);
        Assert.Equal(red.Pixels, coreRed.Pixels);
        Assert.Equal(defaultTrack.Pixels, coreDefault.Pixels);
    }

    [Fact]
    public void CheckBoxCommandRunsAfterCheckedChangedFromTapsAndCodeOnBothLayers()
    {
        var events = new List<string>();
        var command = new TestCommand(parameter => events.Add($"command {parameter}"));
        var box = new SkUiCheckBox { Command = command, CommandParameter = "drawn" };
        box.CheckedChanged += (_, args) => events.Add($"checked {args.Value}");
        _ = new SkUiContentView { Content = box };
        SkUiTestHelpers.Arrange(SkUiDiagnostics.GetSurfaceRoot(box)!, 100, 40);

        var core = new SkUiCoreCheckBox().SetCommand(command).SetCommandParameter("core");
        core.CheckedChanged += (_, args) => events.Add($"core checked {args.Value}");
        SkUiTestHelpers.Arrange(new SkUiCoreHost().SetContent(core), 100, 40);

        Assert.NotNull(SkUiDiagnostics.SimulateTap(box));
        Assert.NotNull(SkUiDiagnostics.SimulateTap(core));
        box.IsChecked = false;
        core.IsChecked = false;

        Assert.Equal(
        [
            "checked True", "command drawn", "core checked True", "command core",
            "checked False", "command drawn", "core checked False", "command core",
        ], events);
    }

    [Fact]
    public void ACheckBoxWhoseCommandCannotExecuteActsDisabledOnBothLayers()
    {
        var executed = 0;
        var command = new TestCommand(_ => executed++) { Allowed = false };
        var box = new SkUiCheckBox { Command = command };
        _ = new SkUiContentView { Content = box };
        SkUiTestHelpers.Arrange(SkUiDiagnostics.GetSurfaceRoot(box)!, 100, 40);
        var core = new SkUiCoreCheckBox().SetCommand(command);
        SkUiTestHelpers.Arrange(new SkUiCoreHost().SetContent(core), 100, 40);

        SkUiDiagnostics.SimulateTap(box);
        SkUiDiagnostics.SimulateTap(core);
        Assert.False(box.IsChecked);
        Assert.False(core.IsChecked);

        // From code the state still changes, and the command is asked again before it runs (as MAUI's).
        box.IsChecked = true;
        core.IsChecked = true;
        Assert.Equal(0, executed);

        command.Allowed = true;
        SkUiDiagnostics.SimulateTap(box);
        SkUiDiagnostics.SimulateTap(core);
        Assert.False(box.IsChecked);
        Assert.False(core.IsChecked);
        Assert.Equal(2, executed);
    }

    [Fact]
    public void MauiMarkupForTheMauiTenMembersParses()
    {
        const string xaml = """
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <sk:SkUiGrid CascadeInputTransparent="False" IsClippedToBounds="True">
                <sk:SkUiSwitch OffColor="Red" />
                <sk:SkUiCheckBox CommandParameter="item" />
              </sk:SkUiGrid>
            </ContentView>
            """;
        var root = new ContentView();
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var grid = (SkUiGrid)root.Content;
        Assert.False(grid.CascadeInputTransparent);
        Assert.True(grid.ClipToBounds);
        Assert.Equal(Colors.Red, ((SkUiSwitch)grid.Children[0]).OffColor);
        Assert.Equal("item", ((SkUiCheckBox)grid.Children[1]).CommandParameter);
    }

    private sealed class TestCommand(Action<object?> execute) : ICommand
    {
        public bool Allowed
        {
            get;
            set
            {
                field = value;
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        } = true;

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => Allowed;

        public void Execute(object? parameter) => execute(parameter);
    }
}
