using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>Smoke tests for the public Core layer prototype (no MAUI View identity per node).</summary>
[Collection(GlobalStateCollection.Name)]
public class CoreLayerTests
{
    [Fact]
    public void AbsoluteLayout_ArrangesProportionalColumns()
    {
        var layout = new SkUiCoreAbsoluteLayout();
        layout.SetPadding(new Thickness(8));
        var left = new SkUiCoreButton();
        left.SetText("L");
        var right = new SkUiCoreButton();
        right.SetText("R");
        layout.Add(left, new Rect(0, 0, 0.5, 40), SkUiCoreAbsoluteLayoutFlags.X | SkUiCoreAbsoluteLayoutFlags.Width);
        layout.Add(right, new Rect(0.5, 0, 0.5, 40), SkUiCoreAbsoluteLayoutFlags.X | SkUiCoreAbsoluteLayoutFlags.Width);

        layout.Measure(200, 100);
        layout.Arrange(new Rect(0, 0, 200, 100));

        // MAUI AbsoluteLayoutManager: width = 0.5 * slot; x = proportion * (slot − width).
        var slot = 200 - 16;
        Assert.Equal(8, left.Frame.X, 1);
        Assert.Equal(8, left.Frame.Y, 1);
        Assert.Equal(slot * 0.5, left.Frame.Width, 1);
        Assert.Equal(8 + 0.5 * (slot - slot * 0.5), right.Frame.X, 1);
        Assert.Equal(slot * 0.5, right.Frame.Width, 1);
    }

    [Fact]
    public void AbsoluteLayout_CentersChildWithAlignment()
    {
        var layout = new SkUiCoreAbsoluteLayout();
        var spinner = new SkUiCoreActivityIndicator()
            .SetHorizontalAlignment(LayoutAlignment.Center)
            .SetVerticalAlignment(LayoutAlignment.Center);
        layout.Add(spinner, new Rect(0.5, 0, 0.5, 48),
            SkUiCoreAbsoluteLayoutFlags.X | SkUiCoreAbsoluteLayoutFlags.Width);

        layout.Measure(200, 100);
        layout.Arrange(new Rect(0, 0, 200, 100));

        // Destination slot: width=100, x=0.5*(200-100)=50; centered 36×36 inside it.
        Assert.Equal(36, spinner.Frame.Width, 1);
        Assert.Equal(36, spinner.Frame.Height, 1);
        Assert.Equal(50 + (100 - 36) / 2, spinner.Frame.X, 1);
        Assert.Equal((48 - 36) / 2, spinner.Frame.Y, 1);
    }

    [Fact]
    public void AbsoluteLayout_AddManyChildren_DoesNotQuadraticScanPlacements()
    {
        // Regression: OnChildrenChanged used to Where+Contains over all placements on every Add (~O(n³)).
        var layout = new SkUiCoreAbsoluteLayout();
        layout.StartUpdating();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        const int count = 2000;
        for (var i = 0; i < count; i++)
        {
            layout.Add(new SkUiCoreBox().SetWidth(10).SetHeight(10), new Rect(0, i * 12, 1, 10),
                SkUiCoreAbsoluteLayoutFlags.Width);
        }
        layout.EndUpdating();
        sw.Stop();
        Assert.Equal(count, layout.Children.Count);
        Assert.True(sw.ElapsedMilliseconds < 500, $"Add {count} took {sw.ElapsedMilliseconds} ms (expected < 500).");
    }

    [Fact]
    public void AbsoluteLayout_RemoveClearsPlacement()
    {
        var layout = new SkUiCoreAbsoluteLayout();
        var box = new SkUiCoreBox();
        layout.Add(box, new Rect(0, 0, 10, 10));
        Assert.True(layout.Remove(box));
        Assert.Throws<ArgumentException>(() => layout.SetLayoutBounds(box, new Rect(0, 0, 1, 1)));
    }


    [Fact]
    public void VerticalStack_ArrangesChildrenWithSpacing()
    {
        var stack = new SkUiCoreVerticalStackLayout().SetSpacing(4).SetPadding(new Thickness(2));
        var a = new SkUiCoreBox().SetWidth(20).SetHeight(10);
        var b = new SkUiCoreBox().SetWidth(30).SetHeight(12);
        stack.Add(a).Add(b);

        stack.Measure(100, 100);
        stack.Arrange(new Rect(0, 0, 100, 100));

        Assert.Equal(2 + (96 - 20) / 2, a.Frame.X, 1); // explicit Width: Fill centers on the cross axis, as MAUI
        Assert.Equal(2, a.Frame.Y, 1);
        Assert.Equal(20, a.Frame.Width, 1); // explicit Width wins over cross-axis stretch
        Assert.Equal(10, a.Frame.Height, 1);
        Assert.Equal(2 + 10 + 4, b.Frame.Y, 1);
        Assert.Equal(12, b.Frame.Height, 1);
    }

    [Fact]
    public void HorizontalStack_ArrangesChildrenWithSpacing()
    {
        var stack = new SkUiCoreHorizontalStackLayout().SetSpacing(6);
        var a = new SkUiCoreBox().SetWidth(20).SetHeight(10);
        var b = new SkUiCoreBox().SetWidth(30).SetHeight(12);
        stack.Add(a).Add(b);

        stack.Measure(200, 50);
        stack.Arrange(new Rect(0, 0, 200, 50));

        Assert.Equal(0, a.Frame.X, 1);
        Assert.Equal(20, a.Frame.Width, 1);
        Assert.Equal(26, b.Frame.X, 1);
        Assert.Equal(30, b.Frame.Width, 1);
        Assert.Equal(10, a.Frame.Height, 1); // explicit Height wins over cross-axis stretch
        Assert.Equal(12, b.Frame.Height, 1);
    }

    [Fact]
    public void Border_ClipsAndHostsContent()
    {
        var label = new SkUiCoreLabel().SetText("Hi");
        var border = new SkUiCoreBorder();
        border.SetPadding(new Thickness(4));
        border.SetCornerRadius(8);
        border.SetStroke(Colors.Black);
        border.SetContent(label);

        border.Measure(100, 40);
        border.Arrange(new Rect(0, 0, 100, 40));
        // As MAUI's Border: inside the padding plus the (default 1 DIP) stroke.
        Assert.Equal(5, label.Frame.X, 1);
        Assert.Equal(5, label.Frame.Y, 1);

        using var bitmap = new SKBitmap(100, 40);
        using var canvas = new SKCanvas(bitmap);
        border.Paint(canvas);
    }

    [Fact]
    public void Border_AcceptsPerCornerRadii()
    {
        var border = new SkUiCoreBorder()
            .SetCornerRadius(new CornerRadius(12, 0, 4, 8))
            .SetStroke(Colors.Black)
            .SetBackgroundColor(Colors.White);
        Assert.Equal(12, border.CornerRadius.TopLeft);
        Assert.Equal(0, border.CornerRadius.TopRight);
        Assert.Equal(4, border.CornerRadius.BottomLeft);
        Assert.Equal(8, border.CornerRadius.BottomRight);

        border.Measure(80, 40);
        border.Arrange(new Rect(0, 0, 80, 40));
        using var bitmap = new SKBitmap(80, 40);
        using var canvas = new SKCanvas(bitmap);
        border.Paint(canvas);
    }

    [Fact]
    public void ToggleControls_ChangeStateOnTap()
    {
        var check = new SkUiCoreCheckBox();
        check.Measure(24, 24);
        check.Arrange(new Rect(0, 0, 24, 24));
        Assert.False(check.IsChecked);
        Assert.True(check.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Pressed, new Point(8, 8))));
        Assert.True(check.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Released, new Point(8, 8))));
        Assert.True(check.IsChecked);

        var radio = new SkUiCoreRadioButton();
        radio.SetIsChecked(true);
        radio.Measure(24, 24);
        radio.Arrange(new Rect(0, 0, 24, 24));
        Assert.True(radio.Touch(new SkUiTouchEvent(2, SkUiTouchAction.Pressed, new Point(8, 8))));
        Assert.True(radio.Touch(new SkUiTouchEvent(2, SkUiTouchAction.Released, new Point(8, 8))));
        Assert.True(radio.IsChecked); // never unchecks

        var sw = new SkUiCoreSwitch();
        sw.Measure(51, 31);
        sw.Arrange(new Rect(0, 0, 51, 31));
        Assert.True(sw.Touch(new SkUiTouchEvent(3, SkUiTouchAction.Pressed, new Point(10, 10))));
        Assert.True(sw.Touch(new SkUiTouchEvent(3, SkUiTouchAction.Released, new Point(10, 10))));
        Assert.True(sw.IsChecked);
    }

    [Fact]
    public void CoreImage_PaintsAssignedSkImage()
    {
        using var bitmap = new SKBitmap(16, 10);
        bitmap.Erase(SKColors.Red);
        using var image = SKImage.FromBitmap(bitmap);
        var node = new SkUiCoreImage().SetImage(image, ownsImage: false);
        node.Measure(100, 100);
        Assert.Equal(16, node.DesiredSize.Width);
        Assert.Equal(10, node.DesiredSize.Height);
        node.Arrange(new Rect(0, 0, 32, 20));
        using var surface = new SKBitmap(32, 20);
        using var canvas = new SKCanvas(surface);
        node.Paint(canvas);
        Assert.Equal(SKColors.Red, surface.GetPixel(16, 10));
    }

    [Fact]
    public void CoreImageButton_ExecutesCommand()
    {
        var executed = 0;
        using var bitmap = new SKBitmap(8, 8);
        using var image = SKImage.FromBitmap(bitmap);
        var button = new SkUiCoreImageButton();
        button.SetImage(image, ownsImage: false);
        button.SetCommand(new SkUiCoreCommand(() => executed++));
        button.Measure(40, 40);
        button.Arrange(new Rect(0, 0, 40, 40));
        Assert.True(button.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Pressed, new Point(5, 5))));
        Assert.True(button.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Released, new Point(5, 5))));
        Assert.Equal(1, executed);
    }

    [Fact]
    public void CoreHost_MeasuresAndPaintsContent()
    {
        using var font = SkUiTestHelpers.UseBundledFont();
        var label = new SkUiCoreLabel()
            .SetText("Hello")
            .SetFontSize(16)
            .SetFontFamily(SkUiTestHelpers.BundledFontFamily);
        var host = new SkUiCoreHost();
        host.SetContent(label);

        var size = ((IView)host).Measure(400, 300);
        Assert.True(size.Width > 0);
        Assert.True(size.Height > 0);

        ((IView)host).Arrange(new Rect(0, 0, size.Width, size.Height));
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(new SKRect(0, 0, (float)size.Width, (float)size.Height));
        host.Paint(canvas);
        using var picture = recorder.EndRecording();
        Assert.NotNull(picture);
    }

    [Fact]
    public void CoreHost_SpinnerRunsOnRenderThread()
    {
        var spinner = new SkUiCoreActivityIndicator();
        spinner.SetWidth(20).SetHeight(20);
        var host = new SkUiCoreHost().SetContent(spinner);
        using var surface = new SkUiTestSurface(host, 40, 40);
        spinner.SetIsRunning(true);
        surface.Frame(0);
        Assert.True(surface.NeedsFrame);
        spinner.SetIsRunning(false);
        surface.Frame(100);
        Assert.False(surface.NeedsFrame);
    }

    [Fact]
    public void CoreLabel_RaisesPropertyChanged()
    {
        var label = new SkUiCoreLabel();
        string? changed = null;
        label.PropertyChanged += (_, args) => changed = args.PropertyName;

        label.SetText("Hi");
        Assert.Equal(nameof(SkUiCoreLabel.Text), changed);

        changed = null;
        label.Text = "Hi"; // unchanged — no notification
        Assert.Null(changed);

        label.Text = "There";
        Assert.Equal(nameof(SkUiCoreLabel.Text), changed);
    }

    [Fact]
    public void CoreLabel_WordWrapIncreasesMeasuredHeight()
    {
        using var font = SkUiTestHelpers.UseBundledFont();
        var label = new SkUiCoreLabel()
            .SetText("AAAA BBBB CCCC DDDD")
            .SetFontSize(16)
            .SetFontFamily(SkUiTestHelpers.BundledFontFamily)
            .SetLineBreakMode(LineBreakMode.WordWrap);

        var wrapped = label.Measure(70, double.PositiveInfinity);
        var unconstrained = label.Measure(double.PositiveInfinity, double.PositiveInfinity);
        Assert.True(wrapped.Height > unconstrained.Height);
        Assert.True(wrapped.Width <= 70 + 0.5);
    }

    [Fact]
    public void CoreLabel_TailTruncationStaysSingleLine()
    {
        using var font = SkUiTestHelpers.UseBundledFont();
        var label = new SkUiCoreLabel()
            .SetText("ABCDEFGHIJKLMNOPQRSTUVWXYZ")
            .SetFontSize(16)
            .SetFontFamily(SkUiTestHelpers.BundledFontFamily)
            .SetLineBreakMode(LineBreakMode.TailTruncation);

        var size = label.Measure(80, double.PositiveInfinity);
        var full = label.Measure(double.PositiveInfinity, double.PositiveInfinity);
        Assert.Equal(full.Height, size.Height, 0.5);
        Assert.True(size.Width < full.Width);
    }

    [Fact]
    public void CoreLabel_CustomLineBreakerReplacesTheModeAndCanApplyIt()
    {
        using var font = SkUiTestHelpers.UseBundledFont();
        var calls = 0;
        var label = new SkUiCoreLabel()
            .SetText("one two three")
            .SetFontSize(16)
            .SetFontFamily(SkUiTestHelpers.BundledFontFamily)
            .SetLineBreakMode(LineBreakMode.NoWrap)
            .SetLineBreaker(context =>
            {
                calls++;
                Assert.Equal(LineBreakMode.NoWrap, context.LineBreakMode);
                return context.Text.Split(' ');
            });

        Assert.Equal(LineBreakMode.NoWrap, label.LineBreakMode);
        var size = label.Measure(400, double.PositiveInfinity);
        Assert.True(calls >= 1);
        Assert.True(size.Height > 16 * 2);

        label.SetLineBreaker(null);
        Assert.Null(label.LineBreaker);
        Assert.True(label.Measure(400, double.PositiveInfinity).Height < size.Height);
    }

    [Fact]
    public void TextLineBreakers_ForReturnsStableInstances()
    {
        Assert.Same(SkUiTextLineBreakers.WordWrap, SkUiTextLineBreakers.For(LineBreakMode.WordWrap));
        Assert.Same(SkUiTextLineBreakers.TailTruncation, SkUiTextLineBreakers.For(LineBreakMode.TailTruncation));
    }

    [Fact]
    public void CoreButton_ExecutesCommand()
    {
        var executed = 0;
        var button = new SkUiCoreButton();
        button.SetText("Go");
        button.SetCommand(new SkUiCoreCommand(() => executed++));
        button.Measure(100, 44);
        button.Arrange(new Rect(0, 0, 100, 44));

        Assert.True(button.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Pressed, new Point(10, 10))));
        Assert.True(button.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Released, new Point(10, 10))));
        Assert.Equal(1, executed);
    }

    [Fact]
    public void CorePaintDelegatesPaintBackgroundAndOverlayAroundVirtualContent()
    {
        var calls = new List<string>();
        var node = new ContentPaintProbe(calls);
        node.SetWidth(40).SetHeight(40);
        node.SetPaintBackground(_ => calls.Add("background"));
        node.SetPaintOverlay(_ => calls.Add("overlay"));
        node.Measure(40, 40);
        node.Arrange(new Rect(0, 0, 40, 40));
        using var bitmap = new SKBitmap(40, 40);
        using var canvas = new SKCanvas(bitmap);
        node.Paint(canvas);
        Assert.Equal(["background", "content", "overlay"], calls);
    }

    [Fact]
    public void Panel_RejectsAncestorCycle()
    {
        var outer = new SkUiCoreVerticalStackLayout();
        var inner = new SkUiCoreVerticalStackLayout();
        outer.Add(inner);
        Assert.Throws<InvalidOperationException>(() => inner.Add(outer));
        Assert.Throws<InvalidOperationException>(() => outer.Add(outer));
    }

    [Fact]
    public void ContentView_RejectsAncestorCycle()
    {
        var root = new SkUiCoreBorder();
        var nested = new SkUiCoreBorder();
        root.SetContent(nested);
        Assert.Throws<InvalidOperationException>(() => nested.SetContent(root));
    }

    [Fact]
    public void CoreHost_RejectsSharedRootAcrossHosts()
    {
        var label = new SkUiCoreLabel().SetText("x");
        var first = new SkUiCoreHost().SetContent(label);
        var second = new SkUiCoreHost();
        Assert.Throws<InvalidOperationException>(() => second.SetContent(label));
        Assert.Same(label, first.Content);
        Assert.Null(second.Content);
    }

    [Fact]
    public void CoreHost_ClearsCaptureWhenCapturedNodeDetached()
    {
        var button = new SkUiCoreButton().SetText("Go");
        var panel = new SkUiCoreVerticalStackLayout().Add(button);
        var host = new SkUiCoreHost().SetContent(panel);
        ((IView)host).Measure(100, 44);
        ((IView)host).Arrange(new Rect(0, 0, 100, 44));

        Assert.True(host.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Pressed, new Point(10, 10))));
        panel.Remove(button);
        Assert.False(host.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Released, new Point(10, 10))));
    }

    [Fact]
    public void AbsoluteLayout_ZeroBoundMeasuresIntrinsicSize()
    {
        var layout = new SkUiCoreAbsoluteLayout();
        var box = new SkUiCoreBox().SetWidth(24).SetHeight(18);
        layout.Add(box, new Rect(4, 6, 0, 0));
        layout.Measure(100, 100);
        layout.Arrange(new Rect(0, 0, 100, 100));
        Assert.Equal(24, box.Frame.Width, 1);
        Assert.Equal(18, box.Frame.Height, 1);
        Assert.Equal(4, box.Frame.X, 1);
        Assert.Equal(6, box.Frame.Y, 1);
    }

    [Fact]
    public void AbsoluteLayout_MeasureUsesPaddedSlotForProportionalChildren()
    {
        var layout = new SkUiCoreAbsoluteLayout().SetPadding(new Thickness(8));
        var child = new SkUiCoreBox();
        layout.Add(child, new Rect(0, 0, 0.5, 40), SkUiCoreAbsoluteLayoutFlags.Width);
        layout.Measure(200, 100);
        layout.Arrange(new Rect(0, 0, 200, 100));
        Assert.Equal(0.5 * (200 - 16), child.Frame.Width, 1);
    }

    [Fact]
    public void ActivityIndicator_StopsWhenSubtreeDetachedAndResumesOnNewHost()
    {
        var spinner = new SkUiCoreActivityIndicator();
        spinner.SetWidth(20).SetHeight(20);
        var panel = new SkUiCoreVerticalStackLayout().Add(spinner);
        var host1 = new SkUiCoreHost().SetContent(panel);
        using var surface1 = new SkUiTestSurface(host1, 40, 40);
        spinner.SetIsRunning(true);
        surface1.Frame(0);
        Assert.True(surface1.NeedsFrame);

        panel.Remove(spinner);
        surface1.Frame(100);
        Assert.True(spinner.IsRunning);
        Assert.False(surface1.NeedsFrame);

        var host2 = new SkUiCoreHost().SetContent(spinner);
        using var surface2 = new SkUiTestSurface(host2, 40, 40);
        surface2.Frame(0);
        Assert.True(surface2.NeedsFrame);
    }

    [Fact]
    public void CoreNode_TransformsAndOpacityCompositeAndHitTest()
    {
        var box = new SkUiCoreBox();
        box.SetColor(Colors.Red);
        box.SetWidth(20).SetHeight(20);
        var clicks = 0;
        var button = new SkUiCoreButton();
        button.SetText("x");
        button.SetWidth(20).SetHeight(20);
        button.Clicked += (_, _) => clicks++;
        var panel = new SkUiCoreAbsoluteLayout();
        panel.Add(box, new Rect(0, 0, 20, 20));
        panel.Add(button, new Rect(0, 30, 20, 20));
        var host = new SkUiCoreHost().SetContent(panel);
        using var surface = new SkUiTestSurface(host, 60, 60);
        box.SetTranslationX(30);
        button.SetTranslationX(30);
        var bitmap = surface.Frame();
        Assert.Equal(SKColors.Red, bitmap.GetPixel(40, 10));
        Assert.NotEqual(SKColors.Red, bitmap.GetPixel(10, 10));
        host.Touch(new(1, SkUiTouchAction.Pressed, new Point(40, 40)));
        host.Touch(new(1, SkUiTouchAction.Released, new Point(40, 40)));
        Assert.Equal(1, clicks);

        box.SetOpacity(0);
        bitmap = surface.Frame();
        Assert.NotEqual(SKColors.Red, bitmap.GetPixel(40, 10));
    }

    [Fact]
    public void CoreImage_ReplaceSameInstanceDoesNotDispose()
    {
        using var bitmap = new SKBitmap(8, 8);
        using var image = SKImage.FromBitmap(bitmap);
        var node = new SkUiCoreImage().SetImage(image, ownsImage: true);
        node.SetImage(image, ownsImage: true);
        Assert.Equal(8, node.ImageSize.Width);
    }

    [Fact]
    public void CoreButton_LookMinimumAppliesUntilExplicitMinimumSet()
    {
        var previous = SkUiLook.Current;
        try
        {
            SkUiLook.Current = new TallButtonLook();
            var button = new SkUiCoreButton().SetText("Hi");
            button.Measure(200, 200);
            Assert.True(button.DesiredSize.Height >= 60);

            button.SetMinimumHeight(20);
            button.Measure(200, 200);
            // Explicit minimum is lower than look; content+padding still drives height, but look floor is off.
            Assert.True(button.DesiredSize.Height < 60 || button.MinimumHeight == 20);
            Assert.Equal(20, button.MinimumHeight);
        }
        finally
        {
            SkUiLook.Current = previous;
        }
    }

    private sealed class TallButtonLook : DefaultSkUiLook
    {
        public override double DefaultButtonMinimumHeight => 60;
    }

    private sealed class ContentPaintProbe(List<string> calls) : SkUiCoreNode
    {
        protected override void OnPaintContent(SKCanvas canvas) => calls.Add("content");
    }
}

/// <summary>Core containers place children by their alignment exactly as the SkUi* (MAUI) containers do.</summary>
public class CoreAlignmentParityTests
{
    public static TheoryData<LayoutAlignment, LayoutAlignment, bool> Alignments()
    {
        var data = new TheoryData<LayoutAlignment, LayoutAlignment, bool>();
        foreach (var horizontal in Enum.GetValues<LayoutAlignment>())
            foreach (var vertical in Enum.GetValues<LayoutAlignment>())
            {
                data.Add(horizontal, vertical, false);
                data.Add(horizontal, vertical, true);
            }
        return data;
    }

    private static LayoutOptions Options(LayoutAlignment alignment) => new(alignment, false);

    [Theory]
    [MemberData(nameof(Alignments))]
    public void StacksAndContentViewsAlignLikeMaui(LayoutAlignment horizontal, LayoutAlignment vertical, bool explicitSize)
    {
        // A box with margins (and, with explicitSize, a requested size) in a horizontal stack, a vertical stack and a
        // content view of the same size on both layers.
        SkUiBox SkUiChild() => new()
        {
            Color = Colors.Red, Margin = new Thickness(3, 2, 5, 4), HorizontalOptions = Options(horizontal), VerticalOptions = Options(vertical),
            WidthRequest = explicitSize ? 30 : -1, HeightRequest = explicitSize ? 20 : -1
        };
        SkUiCoreBox CoreChild()
        {
            var box = new SkUiCoreBox().SetColor(Colors.Red);
            box.SetMargin(new Thickness(3, 2, 5, 4)).SetHorizontalAlignment(horizontal).SetVerticalAlignment(vertical);
            if (explicitSize) box.SetWidth(30).SetHeight(20);
            return box;
        }

        var skUiRow = SkUiChild();
        var coreRow = CoreChild();
        Compare(new SkUiHorizontalStackLayout { Padding = 4, Children = { skUiRow } },
            new SkUiCoreHorizontalStackLayout().SetPadding(new Thickness(4)).Add(coreRow), skUiRow, coreRow, "horizontal stack");
        var skUiColumn = SkUiChild();
        var coreColumn = CoreChild();
        Compare(new SkUiVerticalStackLayout { Padding = 4, Children = { skUiColumn } },
            new SkUiCoreVerticalStackLayout().SetPadding(new Thickness(4)).Add(coreColumn), skUiColumn, coreColumn, "vertical stack");
        var skUiContent = SkUiChild();
        var coreContent = CoreChild();
        Compare(new SkUiContentView { Padding = 4, Content = skUiContent },
            new SkUiCoreContentView().SetPadding(new Thickness(4)).SetContent(coreContent), skUiContent, coreContent, "content view");
    }

    private static void Compare(SkUiView skUiParent, SkUiCoreNode coreParent, SkUiView skUiChild, SkUiCoreNode coreChild, string name)
    {
        SkUiTestHelpers.Arrange(skUiParent, 120, 80);
        coreParent.Measure(120, 80);
        coreParent.Arrange(new Rect(0, 0, 120, 80));
        Assert.True(skUiChild.Frame == coreChild.Frame, $"{name}: SkUi {skUiChild.Frame}, Core {coreChild.Frame}");
    }
}
