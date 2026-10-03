namespace MauiSkiaUi.Core;

/// <summary>
/// Helpers for grouping <see cref="SkUiCoreRadioButton"/>s, which do not group themselves: uncheck a list of radio
/// buttons, the radio buttons under a node, or make a list exclude each other (<see cref="Group"/>).
/// </summary>
public static class SkUiCoreRadioButtons
{
    /// <summary>Unchecks every radio button in <paramref name="buttons"/>.</summary>
    public static void Uncheck(params IEnumerable<SkUiCoreRadioButton> buttons)
    {
        ArgumentNullException.ThrowIfNull(buttons);
        foreach (var button in buttons)
            button?.SetIsChecked(false);
    }

    /// <summary>
    /// Unchecks the radio buttons among <paramref name="host"/>'s children except <paramref name="excluded"/>; with
    /// <paramref name="recursive"/>, every one in its subtree (radio buttons composed into rows or cards).
    /// </summary>
    public static void UncheckRadioButtons(this ISkUiCoreNode host, SkUiCoreRadioButton? excluded = null, bool recursive = false)
    {
        ArgumentNullException.ThrowIfNull(host);
        foreach (var child in ((IVisualTreeElement)host).GetVisualChildren())
        {
            if (ReferenceEquals(child, excluded))
                continue;
            if (child is SkUiCoreRadioButton radio)
                radio.SetIsChecked(false);
            else if (recursive && child is ISkUiCoreNode node)
                node.UncheckRadioButtons(excluded, recursive);
        }
    }

    /// <summary>
    /// Makes <paramref name="buttons"/> exclude each other, wherever they are in the tree: when one is checked (by a tap,
    /// code or a binding), the others are unchecked, then <paramref name="onChecked"/> runs with it. If several are checked
    /// already, the last of them in <paramref name="buttons"/> stays checked (without calling <paramref name="onChecked"/>).
    /// Dispose the result to stop (it removes the handlers; disposing twice does nothing).
    /// </summary>
    /// <remarks>
    /// The handlers live on the radio buttons and hold the list and <paramref name="onChecked"/>, so the group keeps
    /// nothing alive beyond the radio buttons themselves: a screen that is closed is collected with its group, also when
    /// <paramref name="onChecked"/> belongs to a longer-lived object. Dispose it to change or end the grouping while the
    /// radio buttons stay (e.g. before grouping them again with other buttons).
    /// </remarks>
    public static IDisposable Group(IEnumerable<SkUiCoreRadioButton> buttons, Action<SkUiCoreRadioButton>? onChecked = null)
    {
        ArgumentNullException.ThrowIfNull(buttons);
        var members = buttons.Distinct().ToArray();
        if (Array.IndexOf(members, null) >= 0)
            throw new ArgumentException("A group cannot contain null.", nameof(buttons));
        return new Subscription(members, onChecked);
    }

    private sealed class Subscription : IDisposable
    {
        private readonly SkUiCoreRadioButton[] _members;
        private readonly Action<SkUiCoreRadioButton>? _onChecked;
        private bool _disposed;

        public Subscription(SkUiCoreRadioButton[] members, Action<SkUiCoreRadioButton>? onChecked)
        {
            _members = members;
            _onChecked = onChecked;
            // Already several checked (restored or bound state): one selection from the start.
            if (Array.FindLast(members, member => member.IsChecked) is { } selected)
                UncheckOthers(selected);
            foreach (var member in members)
                member.CheckedChanged += OnCheckedChanged;
        }

        private void OnCheckedChanged(object? sender, CheckedChangedEventArgs args)
        {
            if (!args.Value || sender is not SkUiCoreRadioButton radio)
                return;
            UncheckOthers(radio);
            _onChecked?.Invoke(radio);
        }

        private void UncheckOthers(SkUiCoreRadioButton selected)
        {
            foreach (var member in _members)
                if (!ReferenceEquals(member, selected))
                    member.SetIsChecked(false);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            foreach (var member in _members)
                member.CheckedChanged -= OnCheckedChanged;
        }
    }
}
