using System.Windows.Input;

namespace MauiSkiaUi;

/// <summary>
/// A command a Core node runs (<see cref="Core.SkUiCoreNode.TappedCommand"/>, a Core button's <c>Command</c>) with its
/// parameter. <see cref="ICommand.CanExecuteChanged"/> is followed weakly (a long-lived command does not keep the node alive),
/// and <see cref="ICommand.CanExecute"/> is read when the command, its parameter or its state changes, not per press or frame.
/// </summary>
/// <param name="owner">The node the slot belongs to, passed to <paramref name="changed"/>.</param>
/// <param name="changed">
/// Called when the command or <see cref="CanExecute"/> changed, also through a new parameter (so the callback can be <c>static</c>).
/// </param>
internal sealed class SkUiCommandSlot<TOwner>(TOwner owner, Action<TOwner> changed)
    where TOwner : class
{
    private SkUiWeakListener<SkUiCommandSlot<TOwner>>? _listener;

    /// <summary>The command; <c>null</c>: none.</summary>
    public ICommand? Command { get; private set; }

    /// <summary>The parameter passed to <see cref="Command"/>.</summary>
    public object? Parameter { get; private set; }

    /// <summary>Whether <see cref="Command"/> is set and could execute with <see cref="Parameter"/> when last asked.</summary>
    public bool CanExecute { get; private set; }

    /// <summary>Sets the command; returns whether it changed.</summary>
    public bool SetCommand(ICommand? value)
    {
        if (ReferenceEquals(Command, value))
            return false;
        Command = value;
        (_listener ??= new(this, static (slot, change) =>
        {
            if (change.Kind == SkUiChangeKind.CanExecute && slot.Refresh())
                slot.Changed();
        })).Listen(value);
        Refresh();
        Changed();
        return true;
    }

    /// <summary>Sets the parameter; returns whether it changed.</summary>
    public bool SetParameter(object? value)
    {
        if (Equals(Parameter, value))
            return false;
        Parameter = value;
        // Only CanExecute depends on the parameter: rows that re-set it on every bind repaint nothing while it stays the same.
        if (Refresh())
            Changed();
        return true;
    }

    /// <summary>Runs the command when it can execute now (asked again: the app may not raise <see cref="ICommand.CanExecuteChanged"/>).</summary>
    public void Execute()
    {
        if (Command is { } command && command.CanExecute(Parameter))
            command.Execute(Parameter);
    }

    /// <summary>Asks the command again; returns whether <see cref="CanExecute"/> changed.</summary>
    private bool Refresh()
    {
        var value = Command?.CanExecute(Parameter) == true;
        if (value == CanExecute)
            return false;
        CanExecute = value;
        return true;
    }

    private void Changed() => changed(owner);
}
