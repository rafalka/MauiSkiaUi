using System.Windows.Input;

namespace MauiSkiaUi.Core;

/// <summary>
/// Minimal always-executable <see cref="ICommand"/> used by Core button helpers such as
/// <see cref="SkUiCoreButton.SetClicked"/>. Prefer supplying your own <see cref="ICommand"/> when
/// CanExecute or reuse matters.
/// </summary>
public sealed class SkUiCoreCommand : ICommand
{
    private readonly Action<object?> _execute;

    /// <summary>Creates a command that ignores the parameter.</summary>
    public SkUiCoreCommand(Action execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        _execute = _ => execute();
    }

    /// <summary>Creates a command that receives the command parameter.</summary>
    public SkUiCoreCommand(Action<object?> execute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        _execute = execute;
    }

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => true;

    /// <inheritdoc />
    public void Execute(object? parameter) => _execute(parameter);

    /// <inheritdoc />
    /// <remarks>Always executable; add/remove are no-ops.</remarks>
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }
}
