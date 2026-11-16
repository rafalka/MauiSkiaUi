using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Pickers of your own (1.12 P1, P2): a picker derived from <see cref="SkUiPickerBase"/> / <see cref="SkUiCorePickerBase"/> that says
/// what its field shows (<c>SetFieldText</c>), what its popup holds (<c>CreatePopupContent</c>, an <see cref="SkUiPickerPopupContent"/>
/// in SkiaUi's chrome) and what clearing does (<c>OnClearSelection</c>), on the same popup system as the built-in pickers.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class CustomPickerTests : IDisposable
{
    private static readonly Color[] Swatches = [Colors.Red, Colors.Green, Colors.Blue];

    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly RecordingPopupPresenter _presenter = new();
    private readonly ISkUiPopupPresenter? _previous = SkUiPopups.Presenter;

    public CustomPickerTests() => SkUiPopups.Presenter = _presenter;

    public void Dispose()
    {
        SkUiPopups.Presenter = _previous;
        _ui.Dispose();
    }

    /// <summary>A color picker as an app would write it: only the picker API's public and protected members.</summary>
    private sealed class ColorPicker : SkUiPickerBase
    {
        public Color? Value { get; private set; }

        public int Clears { get; private set; }

        public SwatchContent? LastContent { get; private set; }

        public void Choose(Color? value)
        {
            Value = value;
            SetFieldText(value is null ? null : Name(value));
        }

        public static string Name(Color color) => color.ToHex();

        protected override void OnClearSelection()
        {
            Clears++;
            Choose(null);
        }

        protected override SkUiPickerPopupContent CreatePopupContent(SkUiPickerPopupContext context) => LastContent = new SwatchContent(this);
    }

    /// <summary>A row of swatches; a tap chooses (at once, closing) or drafts (with OK).</summary>
    private sealed class SwatchContent(ColorPicker picker) : SkUiPickerPopupContent
    {
        private Color? _draft;

        public int Accepts { get; private set; }
        public int ClearedCount { get; private set; }
        public int PickerChanges { get; private set; }
        public int Closes { get; private set; }

        protected override SkUiView CreateBody()
        {
            var row = new SkUiHorizontalStackLayout { Spacing = 8, Padding = new Thickness(12) };
            foreach (var swatch in Swatches)
                row.Children.Add(new SkUiBox { Color = swatch, WidthRequest = 40, HeightRequest = 40 });
            return row;
        }

        public void Tap(Color color)
        {
            if (Context.RequiresAccept)
            {
                _draft = color;
                return;
            }
            picker.Choose(color);
            Close();
        }

        protected override void OnAccepted()
        {
            Accepts++;
            if (_draft is { } draft)
                picker.Choose(draft);
        }

        protected override void OnCleared() => ClearedCount++;

        protected override void OnPickerChanged() => PickerChanges++;

        protected override void OnClosed() => Closes++;
    }

    private static SkUiVerticalStackLayout Root(params ISkUiView[] children)
    {
        var stack = new SkUiVerticalStackLayout { Padding = new Thickness(10, 20) };
        foreach (var child in children)
            stack.Children.Add(child);
        SkUiTestHelpers.Arrange(stack, 300, 600);
        return stack;
    }

    private SkUiCustomPopupRequest Shown() => Assert.IsType<SkUiCustomPopupRequest>(_presenter.Current);

    private static string? FieldValue(SkUiView root, ISkUiAccessibleNode picker) =>
        SkUiSemanticsTree.Build(root).Flatten().First(node => ReferenceEquals(node.Source, picker)).Value;

    [Fact]
    public void ItsContentOpensInSkiaUisChromeAndChoosesAtOnce()
    {
        var picker = new ColorPicker { Title = "Color", PopupPlacement = SkUiPopupPlacement.Window };
        var root = Root(picker);
        var events = new List<string>();
        picker.Opened += (_, _) => events.Add("Opened");
        picker.Closed += (_, _) => events.Add("Closed");

        picker.IsOpen = true;

        var request = Shown();
        Assert.Same(picker, request.Anchor);
        var surface = Assert.IsType<SkUiCustomPopup>(request.Content);
        var content = picker.LastContent!;
        Assert.Same(content, surface.AppContent);
        Assert.True(content.IsOpen);
        Assert.Same(request, content.Request);
        Assert.Equal((SkUiPopupPlacement.Window, "Color", false), (content.Context.Placement, content.Context.Title, content.Context.RequiresAccept));
        Assert.IsType<SkUiHorizontalStackLayout>(content.Body);
        // The popup is as wide as the swatches (within the window popup's widths) and tall enough for the title and the swatches.
        Assert.True(request.PreferredSize.Height >= 64);

        content.Tap(Colors.Green);
        _ui.RunPending();

        Assert.Equal(Colors.Green, picker.Value);
        Assert.False(picker.IsOpen);
        Assert.Equal(["Opened", "Closed"], events);
        Assert.Equal(1, content.Closes);
        Assert.False(content.IsOpen);
        Assert.Null(surface.AppContent);
        Assert.Equal(ColorPicker.Name(Colors.Green), FieldValue(root, picker));
    }

    [Fact]
    public void WithUpdateModeWhenFinishedOkAppliesTheDraftAndCancelDiscardsIt()
    {
        var picker = new ColorPicker { Title = "Color", UpdateMode = SkUiPickerUpdateMode.WhenFinished };
        Root(picker);

        picker.IsOpen = true;
        var content = picker.LastContent!;
        Assert.True(content.Context.RequiresAccept);
        Assert.True(Shown().RequiresAccept);
        content.Tap(Colors.Blue);
        Shown().Cancel();
        _ui.RunPending();
        Assert.Null(picker.Value);
        Assert.Equal(0, content.Accepts);

        picker.IsOpen = true;
        var second = picker.LastContent!;
        Assert.NotSame(content, second);
        second.Tap(Colors.Red);
        Assert.Null(picker.Value);
        Assert.True(Shown().Accept());
        _ui.RunPending();
        Assert.Equal(Colors.Red, picker.Value);
        Assert.Equal(1, second.Accepts);
        Assert.False(picker.IsOpen);
    }

    [Fact]
    public void ClearClearsThePickerAndCloses()
    {
        var picker = new ColorPicker { IsPopupClearButtonVisible = true, IsClearButtonVisible = true, Placeholder = "None" };
        var root = Root(picker);
        picker.Choose(Colors.Red);

        picker.IsOpen = true;
        var content = picker.LastContent!;
        Assert.True(content.Context.IsClearButtonVisible);
        Assert.True(Shown().Clear());
        _ui.RunPending();

        Assert.Null(picker.Value);
        Assert.Equal(1, picker.Clears);
        Assert.Equal(1, content.ClearedCount);
        Assert.False(picker.IsOpen);
        // The field shows its placeholder again; ClearSelection() goes to the same place.
        Assert.NotEqual(ColorPicker.Name(Colors.Red), FieldValue(root, picker));
        picker.Choose(Colors.Blue);
        picker.ClearSelection();
        Assert.Equal(2, picker.Clears);
    }

    [Fact]
    public void TheContentHearsOfChangesWhileOpenAndEnterAccepts()
    {
        var picker = new ColorPicker();
        Root(picker);
        picker.IsOpen = true;
        var content = picker.LastContent!;
        var surface = (ISkUiKeyHandler)Shown().Content!;

        picker.Choose(Colors.Blue);
        Assert.Equal(1, content.PickerChanges);

        Assert.True(surface.OnKeyDown(new SkUiKeyEvent(SkUiKey.Enter, SkUiKeyModifiers.None, false)));
        _ui.RunPending();
        Assert.Equal(1, content.Accepts);
        Assert.False(picker.IsOpen);

        // Escape closes without accepting.
        picker.IsOpen = true;
        var second = picker.LastContent!;
        Assert.True(((ISkUiKeyHandler)Shown().Content!).OnKeyDown(new SkUiKeyEvent(SkUiKey.Escape, SkUiKeyModifiers.None, false)));
        _ui.RunPending();
        Assert.Equal(0, second.Accepts);
        Assert.False(picker.IsOpen);
    }

    [Fact]
    public void ThePickerClosingItEndsTheContent()
    {
        var picker = new ColorPicker();
        Root(picker);
        picker.IsOpen = true;
        var content = picker.LastContent!;

        picker.IsOpen = false;
        _ui.RunPending();

        Assert.Equal(1, content.Closes);
        Assert.Null(content.Request);
        Assert.Null(picker.Value);
    }

    [Fact]
    public void BuiltInPickersKeepTheirOwnContent()
    {
        var picker = new SkUiPicker { Items = { "One", "Two" } };
        Root(picker);

        picker.IsOpen = true;

        Assert.IsType<SkUiListPopupRequest>(_presenter.Current);
    }

    /// <summary>The Core twin: the same contract on <see cref="SkUiCorePickerBase"/>.</summary>
    private sealed class CoreColorPicker : SkUiCorePickerBase
    {
        public Color? Value { get; private set; }

        public void Choose(Color? value)
        {
            Value = value;
            SetFieldText(value?.ToHex());
        }

        protected override void OnClearSelection() => Choose(null);

        protected override SkUiPickerPopupContent CreatePopupContent(SkUiPickerPopupContext context) => new CoreSwatches(this);

        private sealed class CoreSwatches(CoreColorPicker picker) : SkUiPickerPopupContent
        {
            protected override SkUiView CreateBody()
            {
                var button = new SkUiButton { Text = "Blue" };
                button.Clicked += (_, _) =>
                {
                    picker.Choose(Colors.Blue);
                    Close();
                };
                return button;
            }
        }
    }

    [Fact]
    public void TheCoreTwinOpensFromItsHost()
    {
        var core = new CoreColorPicker { Title = "Color" };
        var host = new SkUiCoreHost().SetContent(core);
        Root(host);

        core.IsOpen = true;

        var request = Shown();
        Assert.Same(host, request.Anchor);
        var button = Assert.IsType<SkUiButton>(((SkUiCustomPopup)request.Content!).AppContent!.Body);
        ((ISkUiAccessibleNode)button).PerformSemanticsAction(SkUiSemanticsActions.Activate);
        _ui.RunPending();

        Assert.Equal(Colors.Blue, core.Value);
        Assert.False(core.IsOpen);
        core.ClearSelection();
        Assert.Null(core.Value);
    }
}
