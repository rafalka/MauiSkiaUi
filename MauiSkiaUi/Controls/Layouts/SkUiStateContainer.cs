using System.Diagnostics;

namespace MauiSkiaUi;

/// <summary>
/// Makes any drawn layout (<see cref="SkUiLayout"/> and the layouts built on it) state-aware, as the Community
/// Toolkit's <c>StateContainer</c>: the views in <see cref="StateViewsProperty"/>, each named by
/// <see cref="SkUiStateView.StateKeyProperty"/>, replace the layout's children while <see cref="CurrentStateProperty"/>
/// names their state (loading, empty, error…); a <c>null</c> or empty state puts the original children back.
/// Toolkit XAML ports by changing the prefixes (<c>sk:SkUiStateContainer.*</c>, <c>sk:SkUiStateView.StateKey</c>).
/// With <see cref="BeforeStateChangeAnimationProperty"/> / <see cref="AfterStateChangeAnimationProperty"/> set, every
/// change of <see cref="CurrentStateProperty"/> animates on its own.
/// </summary>
public static class SkUiStateContainer
{
    private const string CanStateChangeName = "CanStateChange";
    private const string CurrentStateName = "CurrentState";

    /// <summary>The state views of a layout: drawn views, each with a unique <see cref="SkUiStateView.StateKeyProperty"/>.</summary>
    public static readonly BindableProperty StateViewsProperty = BindableProperty.CreateAttached(
        "StateViews", typeof(IList<View>), typeof(SkUiStateContainer), null, defaultValueCreator: _ => new List<View>());

    /// <summary>
    /// The state shown: the key of a state view, or <c>null</c> / empty for the layout's own children. With state change
    /// animations set, the value changes at once and the views follow (a newer value while they animate wins); otherwise
    /// changing it while an explicit <see cref="ChangeStateWithAnimation(BindableObject, string?, CancellationToken)"/>
    /// runs throws a <see cref="SkUiStateContainerException"/>.
    /// </summary>
    public static readonly BindableProperty CurrentStateProperty = BindableProperty.CreateAttached(
        CurrentStateName, typeof(string), typeof(SkUiStateContainer), null, validateValue: ValidateCurrentState, propertyChanged: OnCurrentStateChanged);

    /// <summary>Whether the state can change now: <c>false</c> while an animated change runs. Bind it one way to source.</summary>
    public static readonly BindableProperty CanStateChangeProperty = BindableProperty.CreateAttached(
        CanStateChangeName, typeof(bool), typeof(SkUiStateContainer), true, BindingMode.OneWayToSource);

    /// <summary>
    /// Runs on every shown view before each change of <see cref="CurrentStateProperty"/> (render thread; XAML:
    /// <c>"FadeOut"</c>). With either state change animation set, changes animate without code.
    /// </summary>
    public static readonly BindableProperty BeforeStateChangeAnimationProperty = BindableProperty.CreateAttached(
        "BeforeStateChangeAnimation", typeof(SkUiViewAnimation), typeof(SkUiStateContainer), null, propertyChanged: OnAnimationChanged);

    /// <summary>Runs on every new view after each change of <see cref="CurrentStateProperty"/> (render thread; XAML: <c>"FadeIn"</c>).</summary>
    public static readonly BindableProperty AfterStateChangeAnimationProperty = BindableProperty.CreateAttached(
        "AfterStateChangeAnimation", typeof(SkUiViewAnimation), typeof(SkUiStateContainer), null, propertyChanged: OnAnimationChanged);

    private static readonly BindableProperty ControllerProperty = BindableProperty.CreateAttached(
        "Controller", typeof(SkUiStateContainerController), typeof(SkUiStateContainer), null, defaultValueCreator: CreateController);

    /// <summary>Gets the state views of <paramref name="bindable"/> (a list to add to).</summary>
    public static IList<View> GetStateViews(BindableObject bindable) => (IList<View>)bindable.GetValue(StateViewsProperty);

    /// <summary>Replaces the state views of <paramref name="bindable"/>.</summary>
    public static void SetStateViews(BindableObject bindable, IList<View> value) => bindable.SetValue(StateViewsProperty, value);

    /// <summary>Gets the state shown, or being animated to (<c>null</c>: the layout's own children).</summary>
    public static string? GetCurrentState(BindableObject bindable) => (string?)bindable.GetValue(CurrentStateProperty);

    /// <summary>Shows the state view with key <paramref name="value"/>, or the layout's own children for <c>null</c> / empty.</summary>
    public static void SetCurrentState(BindableObject bindable, string? value) => bindable.SetValue(CurrentStateProperty, value);

    /// <summary>Gets whether the state can change now (<c>false</c> while an animated change runs).</summary>
    public static bool GetCanStateChange(BindableObject bindable) => (bool)bindable.GetValue(CanStateChangeProperty);

    private static void SetCanStateChange(BindableObject bindable, bool value) => bindable.SetValue(CanStateChangeProperty, value);

    /// <summary>Gets the animation run on the shown views before each state change.</summary>
    public static SkUiViewAnimation? GetBeforeStateChangeAnimation(BindableObject bindable) => (SkUiViewAnimation?)bindable.GetValue(BeforeStateChangeAnimationProperty);

    /// <summary>Sets the animation run on the shown views before each state change (<c>null</c>: none).</summary>
    public static void SetBeforeStateChangeAnimation(BindableObject bindable, SkUiViewAnimation? value) => bindable.SetValue(BeforeStateChangeAnimationProperty, value);

    /// <summary>Gets the animation run on the new views after each state change.</summary>
    public static SkUiViewAnimation? GetAfterStateChangeAnimation(BindableObject bindable) => (SkUiViewAnimation?)bindable.GetValue(AfterStateChangeAnimationProperty);

    /// <summary>Sets the animation run on the new views after each state change (<c>null</c>: none).</summary>
    public static void SetAfterStateChangeAnimation(BindableObject bindable, SkUiViewAnimation? value) => bindable.SetValue(AfterStateChangeAnimationProperty, value);

    /// <summary>
    /// Changes the state with a fade: the shown views fade out, the state changes, the new views fade in to their own
    /// <see cref="VisualElement.Opacity"/> (<see cref="SkUiViewAnimation.FadeOut"/> / <see cref="SkUiViewAnimation.FadeIn"/>
    /// on the render thread).
    /// </summary>
    public static Task ChangeStateWithAnimation(BindableObject bindable, string? state, CancellationToken token = default) =>
        ChangeStateWithAnimation(bindable, state, SkUiViewAnimation.FadeOut(), SkUiViewAnimation.FadeIn(), token);

    /// <summary>
    /// Changes the state with render-thread animations: <paramref name="beforeStateChange"/> runs on every shown view
    /// before the change, <paramref name="afterStateChange"/> on every new view after it (at least one is required).
    /// Views that leave get their own opacity, translation, rotation and scale back; the new ones end at the animation's
    /// end values. While the layout is not on screen yet, the state changes without animating. <see cref="CurrentStateProperty"/>
    /// takes the new value at the end. Replaces the toolkit's overload with MAUI <see cref="Animation"/>s, which run on
    /// the UI thread: convert each one to a <see cref="SkUiViewAnimation"/> (see its remarks).
    /// </summary>
    public static async Task ChangeStateWithAnimation(BindableObject bindable, string? state, SkUiViewAnimation? beforeStateChange, SkUiViewAnimation? afterStateChange,
        CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (beforeStateChange is null && afterStateChange is null)
            throw new ArgumentException($"Animation required. Parameters {nameof(beforeStateChange)} and {nameof(afterStateChange)} cannot both be null");
        var controller = BeginChange(bindable, state);
        try
        {
            await Transition(controller.Layout, () => ChangeState(bindable, state), beforeStateChange, afterStateChange, token);
        }
        finally
        {
            EndChange(bindable, controller, state);
        }
    }

    /// <summary>
    /// Changes the state with animations of the layout itself: <paramref name="beforeStateChange"/> before the change,
    /// <paramref name="afterStateChange"/> after it (at least one is required); each receives the layout.
    /// </summary>
    public static async Task ChangeStateWithAnimation(BindableObject bindable, string? state, Func<VisualElement, CancellationToken, Task>? beforeStateChange,
        Func<VisualElement, CancellationToken, Task>? afterStateChange, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (beforeStateChange is null && afterStateChange is null)
            throw new ArgumentException($"Animation required. Parameters {nameof(beforeStateChange)} and {nameof(afterStateChange)} cannot both be null");
        var controller = BeginChange(bindable, state);
        var layout = controller.Layout;
        try
        {
            if (layout.Children.Count > 0 && beforeStateChange is not null)
                await beforeStateChange(layout, cancellationToken).WaitAsync(cancellationToken);
            ChangeState(bindable, state);
            if (layout.Children.Count > 0 && afterStateChange is not null)
                await afterStateChange(layout, cancellationToken).WaitAsync(cancellationToken);
        }
        finally
        {
            EndChange(bindable, controller, state);
        }
    }

    private static SkUiStateContainerController BeginChange(BindableObject bindable, string? state)
    {
        ValidateCanStateChange(bindable);
        var controller = GetController(bindable);
        if (!string.IsNullOrEmpty(state))
            SkUiStateContainerController.ViewForState(state, GetStateViews(bindable));
        controller.ExplicitChange = true;
        SetCanStateChange(bindable, false);
        return controller;
    }

    private static void EndChange(BindableObject bindable, SkUiStateContainerController controller, string? state)
    {
        try
        {
            SetCanStateChange(bindable, true);
            // Applies the state when the change was cancelled before it; otherwise only records it (the switch is idempotent).
            if (GetCurrentState(bindable) != state)
                SetCurrentState(bindable, state);
        }
        finally
        {
            controller.ExplicitChange = false;
        }
    }

    private static Task Transition(SkUiLayout layout, Action change, SkUiViewAnimation? before, SkUiViewAnimation? after, CancellationToken token) =>
        SkUiStateTransition.RunAsync(layout, () => layout.Children.OfType<SkUiView>(), change, before, after, token);

    // Changes from CurrentState with state change animations: the value is already the target; the views catch up one
    // transition at a time, and each switch goes to the newest target (a target set during the "before" animation
    // replaces the pending one; one set during the "after" animation follows it).
    private static async Task RunAutomaticChanges(BindableObject bindable, SkUiStateContainerController controller)
    {
        controller.Animating = true;
        SetCanStateChange(bindable, false);
        try
        {
            while (controller.Target != controller.Shown)
            {
                await Transition(controller.Layout, () => controller.Switch(controller.Target, GetStateViews(bindable)),
                    GetBeforeStateChangeAnimation(bindable), GetAfterStateChangeAnimation(bindable), CancellationToken.None);
            }
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"SkiaUi: {nameof(SkUiStateContainer)} could not show state '{controller.Target}' on {controller.Layout.GetType().Name}: {exception.Message}");
        }
        finally
        {
            controller.Animating = false;
            SetCanStateChange(bindable, true);
        }
    }

    // Invalid changes throw here, before MAUI starts setting the value: an exception thrown from a property-changing or
    // -changed callback would leave MAUI's per-property "being set" flag on, and every later value would be dropped.
    private static bool ValidateCurrentState(BindableObject bindable, object? value)
    {
        var state = (string?)value;
        if (state == GetCurrentState(bindable))
            return true;
        var controller = GetController(bindable); // only drawn layouts
        if (controller.ExplicitChange)
            ValidateCanStateChange(bindable);
        if (!string.IsNullOrEmpty(state))
            SkUiStateContainerController.ViewForState(state, GetStateViews(bindable));
        return true;
    }

    private static void OnCurrentStateChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        var state = (string?)newValue;
        var controller = GetController(bindable);
        if (controller.ExplicitChange)
        {
            ChangeState(bindable, state);
            return;
        }
        controller.Target = string.IsNullOrEmpty(state) ? null : state;
        if (controller.Animating)
            return; // the running loop picks the new target up
        var animated = GetBeforeStateChangeAnimation(bindable) is not null || GetAfterStateChangeAnimation(bindable) is not null;
        if (animated && controller.Layout.RenderState.HasCommitted)
            _ = RunAutomaticChanges(bindable, controller);
        else
            ChangeState(bindable, state);
    }

    private static void OnAnimationChanged(BindableObject bindable, object? oldValue, object? newValue) => (newValue as SkUiViewAnimation)?.Freeze();

    private static void ChangeState(BindableObject bindable, string? state) =>
        GetController(bindable).Switch(string.IsNullOrEmpty(state) ? null : state, GetStateViews(bindable));

    private static SkUiStateContainerController GetController(BindableObject bindable) => (SkUiStateContainerController)bindable.GetValue(ControllerProperty);

    private static object CreateController(BindableObject bindable) => bindable is SkUiLayout layout
        ? new SkUiStateContainerController(layout)
        : throw new SkUiStateContainerException($"{nameof(SkUiStateContainer)} needs a drawn layout ({nameof(SkUiLayout)} or a layout built on it), not {bindable.GetType().FullName}.");

    private static void ValidateCanStateChange(BindableObject bindable)
    {
        if (!GetCanStateChange(bindable))
            throw new SkUiStateContainerException($"{CanStateChangeName} is false. {CurrentStateName} cannot be changed while a state change is in progress. To avoid this exception, first verify {CanStateChangeName} is {true} before changing {CurrentStateName}.");
    }
}

/// <summary>Names a view in <see cref="SkUiStateContainer.StateViewsProperty"/> by its state, as the Community Toolkit's <c>StateView</c>.</summary>
public static class SkUiStateView
{
    /// <summary>The state the view represents (unique within its container).</summary>
    public static readonly BindableProperty StateKeyProperty = BindableProperty.CreateAttached(
        "StateKey", typeof(string), typeof(SkUiStateView), string.Empty);

    /// <summary>Gets the state <paramref name="bindable"/> represents.</summary>
    public static string GetStateKey(BindableObject bindable) => (string)bindable.GetValue(StateKeyProperty);

    /// <summary>Sets the state <paramref name="bindable"/> represents.</summary>
    public static void SetStateKey(BindableObject bindable, string value) => bindable.SetValue(StateKeyProperty, value);
}

/// <summary>Thrown when <see cref="SkUiStateContainer"/> cannot change state (the Community Toolkit's <c>StateContainerException</c>).</summary>
public sealed class SkUiStateContainerException(string message, Exception? innerException = null) : InvalidOperationException(message, innerException);

/// <summary>Swaps a layout's children for a state view and back.</summary>
internal sealed class SkUiStateContainerController(SkUiLayout layout)
{
    private string? _state;
    private ISkUiView[] _content = [];

    public SkUiLayout Layout { get; } = layout;

    /// <summary>The state on screen (<c>null</c>: the layout's own children).</summary>
    public string? Shown => _state;

    /// <summary>The state automatic changes animate to.</summary>
    public string? Target { get; set; }

    /// <summary>Whether automatic changes are animating.</summary>
    public bool Animating { get; set; }

    /// <summary>Whether an explicit <c>ChangeStateWithAnimation</c> runs.</summary>
    public bool ExplicitChange { get; set; }

    public void Switch(string? state, IList<View> stateViews)
    {
        Target = state;
        if (state is null)
            SwitchToContent();
        else
            SwitchToState(state, stateViews);
    }

    public void SwitchToContent()
    {
        if (_state is null)
            return;
        _state = null;
        Layout.Children.Clear();
        foreach (var child in _content)
            Layout.Children.Add(child);
        _content = [];
    }

    public void SwitchToState(string state, IList<View> stateViews)
    {
        var view = ViewForState(state, stateViews);
        if (_state == state && Layout.Children.Count == 1 && ReferenceEquals(Layout.Children[0], view))
            return;
        // The layout's own children wait here until the state is cleared.
        if (_state is null)
            _content = [.. Layout.Children];
        _state = state;
        Layout.Children.Clear();
        // On a grid, the state view covers every cell instead of the first one.
        if (Layout is SkUiGrid grid)
        {
            if (grid.RowDefinitions.Count > 0)
                Grid.SetRowSpan((BindableObject)view, grid.RowDefinitions.Count);
            if (grid.ColumnDefinitions.Count > 0)
                Grid.SetColumnSpan((BindableObject)view, grid.ColumnDefinitions.Count);
        }
        Layout.Children.Add(view);
    }

    public static ISkUiView ViewForState(string state, IList<View> stateViews)
    {
        View? view;
        try
        {
            view = stateViews.SingleOrDefault(candidate => SkUiStateView.GetStateKey(candidate) == state);
        }
        catch (InvalidOperationException exception)
        {
            throw new SkUiStateContainerException($"Unable to determine {nameof(SkUiStateView)} for State: {state}. This State has been assigned to multiple {nameof(SkUiStateView)}s. Ensure each {nameof(SkUiStateView)} has a unique StateKey", exception);
        }
        return view switch
        {
            null => throw new SkUiStateContainerException($"{nameof(SkUiStateView)} for {state} not defined."),
            ISkUiView drawn => drawn,
            _ => throw new SkUiStateContainerException($"The {nameof(SkUiStateView)} for {state} is a {view.GetType().Name}: state views must be drawn (SkUi*) views; put native views inside an SkUiMauiContentView.")
        };
    }
}
