using System.Windows.Input;

namespace MauiSkiaUi;

/// <summary>
/// A swipe item of drawn views (MAUI's <c>SwipeItemView</c>, which hosts native views): add it to an
/// <see cref="SkUiSwipeView"/>'s <see cref="SwipeItems"/>. Beside the content it is as wide as it measures (100 DIPs when
/// it measures nothing) and as tall as the content; above or below it the items share the content's width. A tap invokes
/// it: <see cref="Command"/> runs, then <see cref="Invoked"/> is raised. While <see cref="Command"/> cannot execute it is
/// disabled.
/// </summary>
public class SkUiSwipeItemView : SkUiContentView, Microsoft.Maui.Controls.ISwipeItem
{
    private ICommand? _command;
    private object? _commandParameter;
    private SkUiWeakListener<SkUiSwipeItemView>? _commandListener; // a long-lived command must not keep the view alive

    /// <summary>Bindable <see cref="Command"/>.</summary>
    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(SkUiSwipeItemView), null,
        propertyChanged: (bindable, _, newValue) => ((SkUiSwipeItemView)bindable).OnCommandChanged((ICommand?)newValue));

    /// <summary>Bindable <see cref="CommandParameter"/>.</summary>
    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(
        nameof(CommandParameter), typeof(object), typeof(SkUiSwipeItemView), null,
        propertyChanged: (bindable, _, newValue) => ((SkUiSwipeItemView)bindable).OnCommandParameterChanged(newValue));

    /// <summary>Runs with <see cref="CommandParameter"/> when the item is invoked (when it can execute).</summary>
    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>The parameter of <see cref="Command"/>.</summary>
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    /// <summary>Raised when the item is invoked, after <see cref="Command"/>.</summary>
    public event EventHandler<EventArgs>? Invoked;

    /// <summary>Sets <see cref="Command"/> (same as the property setter).</summary>
    public SkUiSwipeItemView SetCommand(ICommand? value) { Command = value; return this; }

    /// <summary>Sets <see cref="CommandParameter"/> (same as the property setter).</summary>
    public SkUiSwipeItemView SetCommandParameter(object? value) { CommandParameter = value; return this; }

    /// <summary>Invokes the item: runs <see cref="Command"/> (when it can execute), then raises <see cref="Invoked"/>.</summary>
    public void OnInvoked()
    {
        if (_command?.CanExecute(_commandParameter) == true)
            _command.Execute(_commandParameter);
        Invoked?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override bool HandlesTap => true;

    /// <inheritdoc />
    protected override bool CanReceiveTap => _command?.CanExecute(_commandParameter) ?? true;

    /// <inheritdoc />
    protected override void OnTapped(SkUiTappedEventArgs args)
    {
        RaiseTapped(args);
        for (var parent = Parent; parent is not null; parent = parent.Parent)
            if (parent is SkUiSwipeView swipeView)
            {
                swipeView.InvokeItem(this);
                return;
            }
        OnInvoked();
    }

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        info.Role = SkUiSemanticsRole.Button;
    }

    private void OnCommandChanged(ICommand? value)
    {
        _command = value;
        (_commandListener ??= new SkUiWeakListener<SkUiSwipeItemView>(this, static (view, change) =>
        {
            if (change.Kind == SkUiChangeKind.CanExecute)
                view.OnCanExecuteChanged();
        })).Listen(value);
        OnCanExecuteChanged();
    }

    private void OnCommandParameterChanged(object? value)
    {
        _commandParameter = value;
        OnCanExecuteChanged();
    }

    private void OnCanExecuteChanged()
    {
        ChangeVisualState();
        InvalidateSemantics();
        RevalidateFocus();
    }
}
