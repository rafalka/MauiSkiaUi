using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Pickers of the app's own that show their own UI for the items: <c>SelectItemsAsync</c> on <see cref="SkUiPicker"/> and
/// <see cref="SkUiCorePicker"/> (items in, chosen items out), and underneath it <c>ShowPopupAsync</c> on <see cref="SkUiPickerBase"/>
/// and <see cref="SkUiCorePickerBase"/> (the typed request instead of the presenter); both close when the picker does and hand what
/// they do not handle to the base (the presenter). In the global collection: the app-wide presenter is process-wide.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class PickerCustomPopupTests : IDisposable
{
    private static readonly string[] Monkeys = ["Baboon", "Capuchin Monkey", "Blue Monkey", "Squirrel Monkey"];

    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly RecordingPopupPresenter _presenter = new();
    private readonly ISkUiPopupPresenter? _previous = SkUiPopups.Presenter;

    public PickerCustomPopupTests() => SkUiPopups.Presenter = _presenter;

    public void Dispose()
    {
        SkUiPopups.Presenter = _previous;
        _ui.Dispose();
    }

    /// <summary>A picker whose own UI shows its lists to choose one item from: the test answers for the user.</summary>
    private sealed class OwnUiPicker : SkUiPicker
    {
        private TaskCompletionSource<int?>? _answer;

        public SkUiListPopupRequest? Shown { get; private set; }

        public bool ClosedByPicker { get; private set; }

        /// <summary>The user picks <paramref name="index"/> in the UI (<c>null</c>: dismisses it).</summary>
        public void Answer(int? index) => _answer?.TrySetResult(index);

        protected override async Task ShowPopupAsync(SkUiPopupRequest request, CancellationToken cancellationToken)
        {
            if (request is not SkUiListPopupRequest list)
            {
                await base.ShowPopupAsync(request, cancellationToken);
                return;
            }
            Shown = list;
            var answer = new TaskCompletionSource<int?>();
            _answer = answer;
            await using var registration = cancellationToken.Register(() =>
            {
                ClosedByPicker = true;
                answer.TrySetResult(null);
            });
            if (await answer.Task is { } index)
                list.Complete(index);
        }
    }

    /// <summary>A Core picker whose own UI chooses the last item at once.</summary>
    private sealed class LastItemCorePicker : SkUiCorePicker
    {
        public int Shows { get; private set; }

        protected override Task ShowPopupAsync(SkUiPopupRequest request, CancellationToken cancellationToken)
        {
            Shows++;
            if (request is SkUiListPopupRequest list)
                list.Complete(list.Items.Count - 1);
            return Task.CompletedTask;
        }
    }

    /// <summary>A picker whose own UI fails.</summary>
    private sealed class FailingPicker : SkUiPicker
    {
        protected override Task ShowPopupAsync(SkUiPopupRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No UI here.");
    }

    public sealed record Paint(string Name, bool InStock = true);

    private static readonly Paint[] Paints = [new("Teal"), new("Coral"), new("Sun", InStock: false), new("Leaf")];

    /// <summary>A picker whose own selector answers with what the test gives it; it records what it was asked.</summary>
    private sealed class SelectorPicker : SkUiPicker
    {
        private TaskCompletionSource<IReadOnlyList<object>?>? _answer;

        public SkUiPickerSelection? Asked { get; private set; }

        public bool UsesDrawnList { get; init; }

        public IReadOnlyList<object>? DrawnAnswer { get; private set; }

        public void Answer(IReadOnlyList<object>? items) => _answer?.TrySetResult(items);

        protected override async Task<IReadOnlyList<object>?> SelectItemsAsync(SkUiPickerSelection selection, CancellationToken cancellationToken)
        {
            Asked = selection;
            if (UsesDrawnList)
                return DrawnAnswer = await base.SelectItemsAsync(selection, cancellationToken);
            var answer = new TaskCompletionSource<IReadOnlyList<object>?>();
            _answer = answer;
            await using var registration = cancellationToken.Register(() => answer.TrySetResult([Paints[0]]));
            return await answer.Task;
        }
    }

    /// <summary>A Core picker whose selector chooses the first and the last item.</summary>
    private sealed class EndsCorePicker : SkUiCorePicker
    {
        protected override Task<IReadOnlyList<object>?> SelectItemsAsync(SkUiPickerSelection selection, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<object>?>([selection.Items[0], selection.Items[^1]]);
    }

    private static SelectorPicker PaintPicker(SkUiSelectionMode mode = SkUiSelectionMode.Single, bool drawn = false)
    {
        var picker = new SelectorPicker
        {
            Title = "Paint",
            SelectionMode = mode,
            UsesDrawnList = drawn,
            ItemsSource = Paints,
            ItemDisplayBinding = Binding.Create(static (Paint paint) => paint.Name),
            ItemEnabledBinding = Binding.Create(static (Paint paint) => paint.InStock)
        };
        var stack = new SkUiVerticalStackLayout { Padding = new Thickness(10, 20) };
        stack.Children.Add(picker);
        SkUiTestHelpers.Arrange(stack, 360, 700);
        return picker;
    }

    [Fact]
    public void ASelectorGetsTheItemsAndItsAnswerIsTheChoice()
    {
        var picker = PaintPicker();
        picker.SelectedItem = Paints[1];
        var events = new List<string>();
        picker.SelectedIndexChanged += (_, _) => events.Add($"SelectedIndexChanged {picker.SelectedIndex}");
        picker.Closed += (_, _) => events.Add("Closed");

        picker.IsOpen = true;
        Assert.Empty(_presenter.Requests);
        var selection = Assert.IsType<SkUiPickerSelection>(picker.Asked);
        Assert.Equal(Paints, selection.Items);
        Assert.Equal(["Teal", "Coral", "Sun", "Leaf"], selection.Texts);
        Assert.Equal([Paints[1]], selection.SelectedItems);
        Assert.False(selection.AllowsMultiple);
        Assert.False(selection.IsItemEnabled(2));
        Assert.Equal("Paint", selection.Title);

        picker.Answer([Paints[3]]);
        _ui.RunPending();
        Assert.Same(Paints[3], picker.SelectedItem);
        Assert.False(picker.IsOpen);
        Assert.Equal(["SelectedIndexChanged 3", "Closed"], events);
    }

    [Fact]
    public void NoItemsClearTheChoiceAndNullChangesNothing()
    {
        var picker = PaintPicker();
        picker.SelectedIndex = 1;
        picker.IsOpen = true;
        picker.Answer(null);
        _ui.RunPending();
        Assert.Equal(1, picker.SelectedIndex);

        picker.IsOpen = true;
        picker.Answer([]);
        _ui.RunPending();
        Assert.Equal(-1, picker.SelectedIndex);
        Assert.False(picker.IsOpen);

        // Items that are not the picker's, and disabled ones, are not chosen.
        picker.IsOpen = true;
        picker.Answer([new Paint("Elsewhere")]);
        _ui.RunPending();
        Assert.Equal(-1, picker.SelectedIndex);
        picker.IsOpen = true;
        picker.Answer([Paints[2]]);
        _ui.RunPending();
        Assert.Equal(-1, picker.SelectedIndex);
    }

    [Fact]
    public void ASelectorClosedByThePickerChoosesNothing()
    {
        var picker = PaintPicker();
        picker.IsOpen = true;
        // The selector answers Teal as it closes: ignored.
        picker.IsOpen = false;
        _ui.RunPending();
        Assert.Equal(-1, picker.SelectedIndex);
        Assert.True(picker.Asked!.PopupRequest.IsCompleted);
    }

    [Fact]
    public void ASelectorChoosesSeveralItemsAndDisabledOnesKeepTheirState()
    {
        var picker = PaintPicker(SkUiSelectionMode.Multiple);
        picker.IsOpen = true;
        Assert.True(picker.Asked!.AllowsMultiple);
        picker.Answer([Paints[3], Paints[2], Paints[0]]);
        _ui.RunPending();
        Assert.Equal([Paints[0], Paints[3]], picker.SelectedItems);
        Assert.False(picker.IsOpen);
    }

    [Fact]
    public void TheDefaultSelectorIsTheDrawnListAndAnswersWithItsChoice()
    {
        var picker = PaintPicker(drawn: true);
        picker.IsOpen = true;
        var request = Assert.IsType<SkUiListPopupRequest>(Assert.Single(_presenter.Requests));
        Assert.Same(request, picker.Asked!.PopupRequest);
        Assert.True(request.Complete(1));
        _ui.RunPending();
        Assert.Equal([Paints[1]], picker.DrawnAnswer);
        Assert.Equal(1, picker.SelectedIndex);
        Assert.False(picker.IsOpen);

        // A dismissal of the drawn list answers nothing.
        picker.IsOpen = true;
        _presenter.Dismiss();
        _ui.RunPending();
        Assert.Null(picker.DrawnAnswer);
        Assert.Equal(1, picker.SelectedIndex);
    }

    [Fact]
    public void ACoreSelectorChoosesToo()
    {
        var core = new EndsCorePicker().SetSelectionMode(SkUiSelectionMode.Multiple);
        foreach (var monkey in Monkeys)
            core.Items.Add(monkey);
        var stack = new SkUiVerticalStackLayout();
        stack.Children.Add(new SkUiCoreHost().SetContent(core));
        SkUiTestHelpers.Arrange(stack, 360, 700);

        core.IsOpen = true;
        _ui.RunPending();
        Assert.Empty(_presenter.Requests);
        Assert.Equal([Monkeys[0], Monkeys[^1]], core.SelectedItems);
        Assert.False(core.IsOpen);
    }

    private static T Arranged<T>(T picker) where T : SkUiPicker
    {
        foreach (var monkey in Monkeys)
            picker.Items.Add(monkey);
        var stack = new SkUiVerticalStackLayout { Padding = new Thickness(10, 20) };
        stack.Children.Add(picker);
        SkUiTestHelpers.Arrange(stack, 360, 700);
        return picker;
    }

    [Fact]
    public void AnOverridingPickerShowsItsOwnUiInsteadOfThePresenter()
    {
        var picker = Arranged(new OwnUiPicker { Title = "Monkey" });
        var events = new List<string>();
        picker.SelectedIndexChanged += (_, _) => events.Add($"SelectedIndexChanged {picker.SelectedIndex}");
        picker.Closed += (_, _) => events.Add("Closed");

        picker.IsOpen = true;
        Assert.Empty(_presenter.Requests);
        var request = Assert.IsType<SkUiListPopupRequest>(picker.Shown);
        Assert.Equal(Monkeys, request.Items);
        Assert.Equal(-1, request.SelectedIndex);
        Assert.Equal("Monkey", request.Title);

        picker.Answer(2);
        _ui.RunPending();
        Assert.Equal(2, picker.SelectedIndex);
        Assert.False(picker.IsOpen);
        Assert.True(request.IsCompleted);
        Assert.Equal(["SelectedIndexChanged 2", "Closed"], events);
    }

    [Fact]
    public void TheOwnUiClosesWhenThePickerDoesAndADismissalChoosesNothing()
    {
        var picker = Arranged(new OwnUiPicker());
        picker.SelectedIndex = 1;

        picker.IsOpen = true;
        picker.IsOpen = false;
        _ui.RunPending();
        Assert.True(picker.ClosedByPicker);
        Assert.True(picker.Shown!.IsCompleted);
        Assert.Equal(1, picker.SelectedIndex);

        picker.IsOpen = true;
        picker.Answer(null);
        _ui.RunPending();
        Assert.False(picker.IsOpen);
        Assert.Equal(1, picker.SelectedIndex);
    }

    [Fact]
    public void RequestsTheOwnUiDoesNotHandleGoToThePresenter()
    {
        var picker = Arranged(new OwnUiPicker { SelectionMode = SkUiSelectionMode.Multiple });
        picker.IsOpen = true;
        Assert.Null(picker.Shown);
        Assert.IsType<SkUiMultiListPopupRequest>(Assert.Single(_presenter.Requests));
        _presenter.Dismiss();
        _ui.RunPending();
        Assert.False(picker.IsOpen);
    }

    [Fact]
    public void ACorePickerCanShowItsOwnUiToo()
    {
        var core = new LastItemCorePicker();
        foreach (var monkey in Monkeys)
            core.Items.Add(monkey);
        var stack = new SkUiVerticalStackLayout();
        stack.Children.Add(new SkUiCoreHost().SetContent(core));
        SkUiTestHelpers.Arrange(stack, 360, 700);

        core.IsOpen = true;
        _ui.RunPending();
        Assert.Equal(1, core.Shows);
        Assert.Empty(_presenter.Requests);
        Assert.Equal(3, core.SelectedIndex);
        Assert.False(core.IsOpen);
    }

    [Fact]
    public void AnOwnUiThatFailsLeavesThePickerClosed()
    {
        var picker = Arranged(new FailingPicker());
        picker.IsOpen = true;
        _ui.RunPending();
        Assert.False(picker.IsOpen);
        Assert.Equal(-1, picker.SelectedIndex);
    }
}
