using System.Windows.Input;
using MauiSkiaUi.Rendering;

namespace MauiSkiaUi.Core;

// Input state of Core nodes, as MAUI's VisualElement: IsEnabled (cascading), InputTransparent and the tap command.
public partial class SkUiCoreNode
{
    private bool _isEnabledExplicit = true;
    private bool _isEnabled = true;
    private bool _inputTransparent;
    private SkUiCommandSlot<SkUiCoreNode>? _tappedCommand;

    /// <summary>The render children a change of the effective enabled state walks (UI thread; reentrant by ranges).</summary>
    [ThreadStatic] private static List<ISkUiRenderable>? _enabledWalk;

    /// <summary>
    /// Whether the node reacts to input, as MAUI's <c>IsEnabled</c>: the effective value, <c>false</c> while this node, a Core
    /// ancestor or the view hosting the Core tree (a <see cref="SkUiCoreHost"/>) is disabled; <see cref="SetIsEnabled"/> sets
    /// the node's own value. A disabled node takes the taps on it without acting on them (what is underneath gets none), while
    /// scrollers around it still scroll; it is not focusable, screen readers report it disabled and controls draw their disabled
    /// look. <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/> reports changes of the effective value.
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetIsEnabled(value);
    }

    /// <summary>Sets the node's own <see cref="IsEnabled"/> (the effective value also needs every ancestor enabled).</summary>
    public SkUiCoreNode SetIsEnabled(bool value)
    {
        if (_isEnabledExplicit == value) return this;
        _isEnabledExplicit = value;
        RefreshIsEnabled();
        return this;
    }

    /// <summary>
    /// Pointers pass through the node and its subtree to what is underneath (MAUI's <c>InputTransparent</c> on a layout that
    /// cascades it, the default). Keyboard focus skips the subtree; it is still drawn and read by screen readers.
    /// </summary>
    public bool InputTransparent
    {
        get => _inputTransparent;
        set => SetInputTransparent(value);
    }

    /// <summary>Sets <see cref="InputTransparent"/>.</summary>
    public SkUiCoreNode SetInputTransparent(bool value)
    {
        if (!SetProperty(ref _inputTransparent, value, nameof(InputTransparent))) return this;
        if (value)
        {
            SkUiGestureSet.CancelSubtree(this);
            SkUiFocusManager.ValidateAll();
        }
        return this;
    }

    // In the attached-value store: most nodes have no tooltip, so they pay no field for it.
    private static readonly SkUiCoreAttachedProperty<string?> ToolTipProperty =
        new(nameof(ToolTip), typeof(SkUiCoreNode), null, affectsParentMeasure: false);

    /// <summary>
    /// Text the platform shows in a tooltip while a mouse or trackpad pointer rests on the node (the twin of MAUI's
    /// <c>ToolTipProperties.Text</c> on SkUi* views): Mac Catalyst, iPad with a pointer and Windows; none on phones, as MAUI.
    /// A node without one shows its nearest ancestor's. <c>null</c> or empty for none.
    /// </summary>
    public string? ToolTip
    {
        get => GetValue(ToolTipProperty);
        set => SetToolTip(value);
    }

    /// <summary>Sets <see cref="ToolTip"/>.</summary>
    public SkUiCoreNode SetToolTip(string? value)
    {
        SetValue(ToolTipProperty, value);
        // An open tooltip under a resting pointer shows the new text.
        SkUiPointerRouter.OnToolTipChanged();
        return this;
    }

    string? ISkUiInputNode.ToolTipText => ToolTip;

    /// <summary>
    /// Command run on a tap (the twin of <see cref="SkUiView.TappedCommand"/>): while it can execute, the node takes taps (and
    /// keyboard / screen reader activation) like one with a <see cref="Tapped"/> handler, which runs first. Its
    /// <see cref="ICommand.CanExecuteChanged"/> is followed weakly. Core buttons run their own <c>Command</c> instead.
    /// </summary>
    public ICommand? TappedCommand
    {
        get => _tappedCommand?.Command;
        set => SetTappedCommand(value);
    }

    /// <summary>Parameter passed to <see cref="TappedCommand"/>.</summary>
    public object? TappedCommandParameter
    {
        get => _tappedCommand?.Parameter;
        set => SetTappedCommandParameter(value);
    }

    /// <summary>Sets <see cref="TappedCommand"/>.</summary>
    public SkUiCoreNode SetTappedCommand(ICommand? value)
    {
        if (ReferenceEquals(TappedCommand, value)) return this;
        TappedCommandSlot.SetCommand(value);
        OnPropertyChanged(nameof(TappedCommand));
        return this;
    }

    /// <summary>Sets <see cref="TappedCommandParameter"/>.</summary>
    public SkUiCoreNode SetTappedCommandParameter(object? value)
    {
        if (Equals(TappedCommandParameter, value)) return this;
        TappedCommandSlot.SetParameter(value);
        OnPropertyChanged(nameof(TappedCommandParameter));
        return this;
    }

    private SkUiCommandSlot<SkUiCoreNode> TappedCommandSlot => _tappedCommand ??= new(this, static node => node.OnTapStateChanged());

    /// <summary>Whether <see cref="TappedCommand"/> takes taps now (set and able to execute).</summary>
    private bool WantsTappedCommand => _tappedCommand is { CanExecute: true } && RunsTappedCommand;

    /// <summary>Whether a tap runs <see cref="TappedCommand"/>; buttons run their own command instead (as <see cref="SkUiButton"/>).</summary>
    internal virtual bool RunsTappedCommand => true;

    /// <summary>
    /// Whether the control's own tap action is available now (a Core button whose command can execute). While not, the node is
    /// disabled for input, semantics and focus as with <see cref="IsEnabled"/> <c>false</c>, but its subtree keeps its state.
    /// </summary>
    internal virtual bool CanReceiveTap => true;

    /// <summary>Enabled for input, semantics and focus: <see cref="IsEnabled"/> and <see cref="CanReceiveTap"/>.</summary>
    internal bool IsInteractionEnabled => _isEnabled && CanReceiveTap;

    /// <summary>
    /// Whether the node takes taps changed (a command, its parameter or its <c>CanExecute</c>): screen readers and keyboard focus
    /// follow, controls repaint their enabled look, and a press held while the tap stops being available ends without a tap
    /// (as when <see cref="IsEnabled"/> turns <c>false</c>).
    /// </summary>
    private protected void OnTapStateChanged()
    {
        if (!CanReceiveTap)
            CancelGestures();
        InvalidatePaint();
        InvalidateSemantics();
        RevalidateFocus();
    }

    /// <summary>
    /// Called after the effective <see cref="IsEnabled"/> changed (also when an ancestor's or the host's did), before
    /// <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/> is raised; the node is already marked for re-recording.
    /// </summary>
    protected virtual void OnIsEnabledChanged() { }

    /// <summary>Recomputes the effective <see cref="IsEnabled"/> of this subtree (own value, parent or host changed).</summary>
    internal void RefreshIsEnabled()
    {
        if (!UpdateIsEnabled())
            return;
        SkUiSemantics.Invalidate(this);
        if (!_isEnabled)
            SkUiFocusManager.ValidateAll();
    }

    /// <summary>Whether the parent (or, for a Core root, the hosting view) is enabled.</summary>
    private bool IsParentEnabled => _parent is SkUiCoreNode parent ? parent._isEnabled : HostOwner?.IsEnabled ?? true;

    /// <summary>Updates the effective value and pushes it down; returns whether it changed.</summary>
    private bool UpdateIsEnabled()
    {
        var value = _isEnabledExplicit && IsParentEnabled;
        if (value == _isEnabled)
            return false;
        _isEnabled = value;
        if (!value)
            CancelGestures();
        InvalidatePaint();
        OnIsEnabledChanged();
        OnPropertyChanged(nameof(IsEnabled));
        var children = _enabledWalk ??= [];
        var start = children.Count;
        AddRenderChildren(children);
        var end = children.Count;
        try
        {
            for (var index = start; index < end; index++)
                if (children[index] is SkUiCoreNode child && ReferenceEquals(child._parent, this))
                    child.UpdateIsEnabled();
        }
        finally
        {
            children.RemoveRange(start, children.Count - start);
        }
        return true;
    }
}
