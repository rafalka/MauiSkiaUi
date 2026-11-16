using System.ComponentModel;
using System.Runtime.CompilerServices;
using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>A duration picker as an app would write it (public: XAML creates it): three choices in its popup.</summary>
public sealed class TestDurationPicker : SkUiValuePicker<TimeSpan?>
{
    public static readonly TimeSpan[] Choices = [TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30), TimeSpan.FromHours(1)];

    public TestDurationContent? LastContent { get; private set; }

    protected override SkUiValuePopupContent<TimeSpan?> CreateValueContent(SkUiPickerPopupContext context) => LastContent = new TestDurationContent();
}

/// <summary>The duration picker's popup: a button per choice. A tap chooses and closes, or drafts while the popup waits for OK.</summary>
public sealed class TestDurationContent : SkUiValuePopupContent<TimeSpan?>
{
    public int ValueChanges { get; private set; }

    protected override SkUiView CreateBody()
    {
        var column = new SkUiVerticalStackLayout();
        foreach (var choice in TestDurationPicker.Choices)
            column.Children.Add(new SkUiButton { Text = choice.ToString() });
        return column;
    }

    public void Tap(TimeSpan choice)
    {
        if (Context.RequiresAccept)
            SetValue(choice);
        else
            Complete(choice);
    }

    public void Drag(TimeSpan choice) => SetValue(choice);

    protected override void OnValueChanged() => ValueChanges++;
}

/// <summary>
/// Value pickers (1.12 P3): <see cref="SkUiValuePicker{T}"/> and <see cref="SkUiCoreValuePicker{T}"/> over the pickers of your own,
/// with the typed request <see cref="SkUiValuePopupRequest{T}"/> shared by the content and presenters with their own UI.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class ValuePickerTests : IDisposable
{
    private readonly TestUiContext _ui = TestUiContext.Install();
    private readonly RecordingPopupPresenter _presenter = new();
    private readonly ISkUiPopupPresenter? _previous = SkUiPopups.Presenter;

    public ValuePickerTests() => SkUiPopups.Presenter = _presenter;

    public void Dispose()
    {
        SkUiPopups.Presenter = _previous;
        _ui.Dispose();
    }

    private sealed class NamedColorPicker : SkUiValuePicker<Color?>
    {
        protected override SkUiValuePopupContent<Color?> CreateValueContent(SkUiPickerPopupContext context) => new Swatches();

        protected override string? FormatValue(Color? value) => value?.ToHex();

        protected override string? DescribeValue(Color? value) => value == Colors.Red ? "Red" : null;

        private sealed class Swatches : SkUiValuePopupContent<Color?>
        {
            protected override SkUiView CreateBody() => new SkUiBox { WidthRequest = 40, HeightRequest = 40 };
        }
    }

    private sealed class Plan : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public TimeSpan? Reminder { get; set { field = value; Raise(); } }

        private void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private static SkUiVerticalStackLayout Root(params ISkUiView[] children)
    {
        var stack = new SkUiVerticalStackLayout { Padding = new Thickness(10, 20) };
        foreach (var child in children)
            stack.Children.Add(child);
        SkUiTestHelpers.Arrange(stack, 300, 600);
        return stack;
    }

    private SkUiValuePopupRequest<T> Shown<T>() => Assert.IsType<SkUiValuePopupRequest<T>>(_presenter.Current);

    private static string? FieldValue(SkUiView root, ISkUiAccessibleNode picker) =>
        SkUiSemanticsTree.Build(root).Flatten().First(node => ReferenceEquals(node.Source, picker)).Value;

    [Fact]
    public void EachClosedTypeHasItsOwnValueProperty()
    {
        Assert.NotSame(SkUiValuePicker<TimeSpan?>.ValueProperty, SkUiValuePicker<Color?>.ValueProperty);
        Assert.Equal(typeof(TimeSpan?), SkUiValuePicker<TimeSpan?>.ValueProperty.ReturnType);
        Assert.Equal(typeof(Color), SkUiValuePicker<Color?>.ValueProperty.ReturnType);
        Assert.Equal(BindingMode.TwoWay, SkUiValuePicker<TimeSpan?>.ValueProperty.DefaultBindingMode);

        var duration = new TestDurationPicker { Value = TimeSpan.FromMinutes(30) };
        var color = new NamedColorPicker { Value = Colors.Blue };
        Assert.Equal(TimeSpan.FromMinutes(30), duration.Value);
        Assert.Equal(Colors.Blue, color.Value);
    }

    [Fact]
    public void AChoiceInThePopupIsTheValueAndRunsCommandThenEvent()
    {
        var order = new List<string>();
        var picker = new TestDurationPicker { Title = "Reminder" };
        picker.SelectionChangedCommand = new Command(() => order.Add("command"));
        picker.ValueChanged += (_, args) => order.Add($"event {args.OldValue} -> {args.NewValue}");
        var root = Root(picker);

        picker.IsOpen = true;
        var request = Shown<TimeSpan?>();
        Assert.Null(request.Value);
        picker.LastContent!.Tap(TimeSpan.FromHours(1));
        _ui.RunPending();

        Assert.Equal(TimeSpan.FromHours(1), picker.Value);
        Assert.False(picker.IsOpen);
        Assert.Equal(["command", "event  -> 01:00:00"], order);
        Assert.Equal("01:00:00", FieldValue(root, picker));
        Assert.True(request.HasResult);
        Assert.Equal(TimeSpan.FromHours(1), request.Result);
    }

    [Fact]
    public void BindsTwoWayAndKeepsOneWayBindings()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var plan = new Plan { Reminder = TimeSpan.FromMinutes(15) };
        var picker = new TestDurationPicker { BindingContext = plan };
        picker.SetBinding(SkUiValuePicker<TimeSpan?>.ValueProperty, nameof(Plan.Reminder));
        Root(picker);
        Assert.Equal(TimeSpan.FromMinutes(15), picker.Value);

        picker.IsOpen = true;
        Assert.Equal(TimeSpan.FromMinutes(15), picker.LastContent!.Value);
        picker.LastContent.Tap(TimeSpan.FromMinutes(30));
        _ui.RunPending();
        Assert.Equal(TimeSpan.FromMinutes(30), plan.Reminder);

        // One way: the user's choice is a handler value, so the app's later change still arrives.
        var oneWay = new TestDurationPicker { BindingContext = plan };
        oneWay.SetBinding(SkUiValuePicker<TimeSpan?>.ValueProperty, new Binding(nameof(Plan.Reminder), BindingMode.OneWay));
        Root(oneWay);
        oneWay.IsOpen = true;
        oneWay.LastContent!.Tap(TimeSpan.FromHours(1));
        _ui.RunPending();
        Assert.Equal(TimeSpan.FromMinutes(30), plan.Reminder);
        plan.Reminder = TimeSpan.FromMinutes(15);
        Assert.Equal(TimeSpan.FromMinutes(15), oneWay.Value);
    }

    [Fact]
    public void WithUpdateModeWhenFinishedChangesAreADraftUntilOk()
    {
        var picker = new TestDurationPicker { UpdateMode = SkUiPickerUpdateMode.WhenFinished, Value = TimeSpan.FromMinutes(15) };
        Root(picker);

        picker.IsOpen = true;
        var content = picker.LastContent!;
        content.Tap(TimeSpan.FromMinutes(30));
        Assert.Equal(TimeSpan.FromMinutes(30), content.Value);
        Assert.Equal(TimeSpan.FromMinutes(15), picker.Value);
        Assert.Equal(1, content.ValueChanges);
        Shown<TimeSpan?>().Cancel();
        _ui.RunPending();
        Assert.Equal(TimeSpan.FromMinutes(15), picker.Value);

        picker.IsOpen = true;
        picker.LastContent!.Tap(TimeSpan.FromHours(1));
        Assert.True(Shown<TimeSpan?>().Accept());
        _ui.RunPending();
        Assert.Equal(TimeSpan.FromHours(1), picker.Value);
        Assert.False(picker.IsOpen);
    }

    [Fact]
    public void WithoutWaitingEachChangeAppliesAndThePopupStaysOpen()
    {
        var picker = new TestDurationPicker();
        Root(picker);
        picker.IsOpen = true;

        picker.LastContent!.Drag(TimeSpan.FromMinutes(15));
        Assert.Equal(TimeSpan.FromMinutes(15), picker.Value);
        picker.LastContent.Drag(TimeSpan.FromMinutes(30));

        Assert.Equal(TimeSpan.FromMinutes(30), picker.Value);
        Assert.True(picker.IsOpen);
    }

    [Fact]
    public void TheAppsChangeAndClearAreFollowedByTheOpenPopup()
    {
        var picker = new TestDurationPicker { Value = TimeSpan.FromMinutes(15), IsPopupClearButtonVisible = true };
        Root(picker);
        picker.IsOpen = true;
        var content = picker.LastContent!;
        var request = Shown<TimeSpan?>();

        picker.Value = TimeSpan.FromHours(1);
        Assert.Equal(TimeSpan.FromHours(1), content.Value);
        Assert.Equal(TimeSpan.FromHours(1), request.Value);

        Assert.True(request.Clear());
        _ui.RunPending();
        Assert.Null(picker.Value);
        Assert.False(picker.IsOpen);
        Assert.Equal(2, content.ValueChanges);
    }

    [Fact]
    public void APresenterWithItsOwnUiUsesTheTypedRequest()
    {
        var picker = new TestDurationPicker();
        Root(picker);

        picker.IsOpen = true;
        var request = Shown<TimeSpan?>();
        Assert.True(request.SetValue(TimeSpan.FromMinutes(30)));
        Assert.Equal(TimeSpan.FromMinutes(30), picker.Value);
        Assert.True(request.Complete(TimeSpan.FromMinutes(15)));
        _ui.RunPending();

        Assert.Equal(TimeSpan.FromMinutes(15), picker.Value);
        Assert.False(picker.IsOpen);
        Assert.False(request.SetValue(TimeSpan.FromHours(1)));
    }

    [Fact]
    public void TheFieldShowsTheFormatAndScreenReadersTheDescription()
    {
        var duration = new TestDurationPicker { ValueFormat = "{0:hh\\:mm}", Placeholder = "None" };
        var color = new NamedColorPicker();
        var root = Root(duration, color);

        Assert.NotEqual("00:30", FieldValue(root, duration));
        duration.Value = TimeSpan.FromMinutes(30);
        Assert.Equal("00:30", FieldValue(root, duration));
        duration.ValueFormat = "{0:mm} min";
        Assert.Equal("30 min", FieldValue(root, duration));
        duration.ClearSelection();
        Assert.Null(duration.Value);

        color.Value = Colors.Red;
        Assert.Equal("Red", FieldValue(root, color));
        color.Value = Colors.Blue;
        Assert.Equal(Colors.Blue.ToHex(), FieldValue(root, color));
    }

    /// <summary>Overrides that do not call the base: the content still follows the picker and lets go of it.</summary>
    private sealed class NoBasePicker : SkUiValuePicker<TimeSpan?>
    {
        public NoBaseContent? LastContent { get; private set; }

        protected override SkUiValuePopupContent<TimeSpan?> CreateValueContent(SkUiPickerPopupContext context) => LastContent = new NoBaseContent();
    }

    private sealed class NoBaseContent : SkUiValuePopupContent<TimeSpan?>
    {
        public int Closes { get; private set; }

        protected override SkUiView CreateBody() => new SkUiBox { HeightRequest = 40 };

        public void Drag(TimeSpan value) => SetValue(value);

        protected override void OnPickerChanged() { }

        protected override void OnCleared() { }

        protected override void OnClosed() => Closes++;
    }

    [Fact]
    public void OverridesNeedNotCallTheBase()
    {
        var picker = new NoBasePicker { IsPopupClearButtonVisible = true };
        Root(picker);
        picker.IsOpen = true;
        var content = picker.LastContent!;

        picker.Value = TimeSpan.FromHours(2);
        Assert.Equal(TimeSpan.FromHours(2), content.Value);
        Shown<TimeSpan?>().Clear();
        _ui.RunPending();

        Assert.Null(content.Value);
        Assert.Equal(1, content.Closes);
        Assert.Null(content.Request);
    }

    [Fact]
    public void ADraftSurvivesARefreshOfTheField()
    {
        var picker = new TestDurationPicker { UpdateMode = SkUiPickerUpdateMode.WhenFinished, Value = TimeSpan.FromMinutes(15) };
        Root(picker);
        picker.IsOpen = true;
        var content = picker.LastContent!;
        content.Tap(TimeSpan.FromHours(1));

        // The field's text changes (a format, a culture switch), the value does not: the draft stays.
        picker.ValueFormat = "{0:hh\\:mm}";
        Assert.Equal(TimeSpan.FromHours(1), content.Value);

        // The app changes the value: the popup shows it.
        picker.Value = TimeSpan.FromMinutes(30);
        Assert.Equal(TimeSpan.FromMinutes(30), content.Value);
        Assert.True(Shown<TimeSpan?>().Accept());
        _ui.RunPending();
        Assert.Equal(TimeSpan.FromMinutes(30), picker.Value);
    }

    /// <summary>A picker whose default is a choice (0) and whose text depends on its own constructor.</summary>
    private sealed class CountPicker : SkUiValuePicker<int>
    {
        private readonly string _unit;

        public CountPicker(string unit) => _unit = unit;

        protected override string? FormatValue(int value) => $"{value} {_unit.ToUpperInvariant()}";

        protected override SkUiValuePopupContent<int> CreateValueContent(SkUiPickerPopupContext context) => throw new NotSupportedException();
    }

    [Fact]
    public void TheFieldTextWaitsForTheDerivedConstructor()
    {
        var picker = new CountPicker("items");
        var root = Root(picker);

        Assert.Equal("0 ITEMS", FieldValue(root, picker));
        picker.Value = 3;
        Assert.Equal("3 ITEMS", FieldValue(root, picker));
    }

    /// <summary>The Core twin.</summary>
    private sealed class CoreDurationPicker : SkUiCoreValuePicker<TimeSpan?>
    {
        public TestDurationContent? LastContent { get; private set; }

        protected override SkUiValuePopupContent<TimeSpan?> CreateValueContent(SkUiPickerPopupContext context) => LastContent = new TestDurationContent();
    }

    [Fact]
    public void TheCoreTwinPicksAndNotifies()
    {
        var core = new CoreDurationPicker();
        var host = new SkUiCoreHost().SetContent(core);
        Root(host);
        var changed = new List<string?>();
        core.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        var values = new List<TimeSpan?>();
        core.ValueChanged += (_, args) => values.Add(args.NewValue);

        core.IsOpen = true;
        Assert.Same(host, Shown<TimeSpan?>().Anchor);
        core.LastContent!.Tap(TimeSpan.FromMinutes(30));
        _ui.RunPending();

        Assert.Equal(TimeSpan.FromMinutes(30), core.Value);
        Assert.False(core.IsOpen);
        Assert.Contains(nameof(core.Value), changed);
        Assert.Equal([TimeSpan.FromMinutes(30)], values);
        core.ClearSelection();
        Assert.Null(core.Value);
        Assert.Same(core, core.SetValueFormat("{0:mm}").SetValue(TimeSpan.FromMinutes(15)));
    }
}

/// <summary>A closed value picker of the app's in XAML, with no type arguments.</summary>
[Collection(RuntimeXamlCollection.Name)]
public class ValuePickerXamlTests
{
    public sealed class Model
    {
        public TimeSpan? Reminder { get; set; } = TimeSpan.FromMinutes(30);
    }

    [Fact]
    public void AClosedTypeNeedsNoTypeArguments()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        const string xaml = """
            <VerticalStackLayout xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                                 xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                                 xmlns:t="clr-namespace:MauiSkiaUi.Tests;assembly=MauiSkiaUi.Tests">
              <t:TestDurationPicker Value="{Binding Reminder}" ValueFormat="{}{0:hh\:mm}" Title="Reminder" UpdateMode="WhenFinished" />
            </VerticalStackLayout>
            """;
        var root = new VerticalStackLayout { BindingContext = new Model() };
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var picker = Assert.IsType<TestDurationPicker>(root.Children[0]);

        Assert.Equal(TimeSpan.FromMinutes(30), picker.Value);
        Assert.Equal("{0:hh\\:mm}", picker.ValueFormat);
        Assert.Equal(SkUiPickerUpdateMode.WhenFinished, picker.UpdateMode);
    }
}
