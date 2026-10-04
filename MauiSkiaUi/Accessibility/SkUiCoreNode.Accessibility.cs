using SkiaSharp;

namespace MauiSkiaUi.Core;

// Accessibility of Core nodes: what screen readers read (the semantic properties, the Core counterpart of MAUI's
// SemanticProperties / AutomationProperties), what they can do, and keyboard focus.
public partial class SkUiCoreNode : ISkUiAccessibleNode
{
    private CoreSemantics? _semantics;
    private bool _isTabStop = true;
    private int _tabIndex;
    private bool _isFocused;
    private bool _focusRingVisible;

    /// <summary>Semantic values, created on the first set (most nodes have none).</summary>
    private sealed class CoreSemantics
    {
        public string? Description;
        public string? Hint;
        public SemanticHeadingLevel HeadingLevel;
        public bool? IsInAccessibleTree;
        public bool ExcludedWithChildren;
    }

    /// <summary>
    /// What screen readers read for the node (MAUI's <c>SemanticProperties.Description</c>): replaces its own text, makes an
    /// image or a container an element, and a container with it is read as one element (its text descendants are not read).
    /// </summary>
    public string? SemanticDescription { get => _semantics?.Description; set => SetSemanticDescription(value); }

    /// <summary>What screen readers read after the name, e.g. what activating does (MAUI's <c>SemanticProperties.Hint</c>).</summary>
    public string? SemanticHint { get => _semantics?.Hint; set => SetSemanticHint(value); }

    /// <summary>Marks the node as a heading of a level (MAUI's <c>SemanticProperties.HeadingLevel</c>); screen readers jump between headings.</summary>
    public SemanticHeadingLevel SemanticHeadingLevel { get => _semantics?.HeadingLevel ?? SemanticHeadingLevel.None; set => SetSemanticHeadingLevel(value); }

    /// <summary>
    /// Forces the node into (<c>true</c>) or out of (<c>false</c>) the elements screen readers read; its children are read
    /// either way. <c>null</c> (default): decided by its role, text and actions (MAUI's <c>AutomationProperties.IsInAccessibleTree</c>).
    /// </summary>
    public bool? IsInAccessibleTree { get => _semantics?.IsInAccessibleTree; set => SetIsInAccessibleTree(value); }

    /// <summary>Hides the node and its whole subtree from screen readers (MAUI's <c>AutomationProperties.ExcludedWithChildren</c>).</summary>
    public bool ExcludedWithChildren { get => _semantics?.ExcludedWithChildren ?? false; set => SetExcludedWithChildren(value); }

    /// <summary>Sets <see cref="SemanticDescription"/>.</summary>
    public SkUiCoreNode SetSemanticDescription(string? value) =>
        SetSemantic(_semantics?.Description, value, static (semantics, text) => semantics.Description = text, nameof(SemanticDescription));

    /// <summary>Sets <see cref="SemanticHint"/>.</summary>
    public SkUiCoreNode SetSemanticHint(string? value) =>
        SetSemantic(_semantics?.Hint, value, static (semantics, text) => semantics.Hint = text, nameof(SemanticHint));

    /// <summary>Sets <see cref="SemanticHeadingLevel"/>.</summary>
    public SkUiCoreNode SetSemanticHeadingLevel(SemanticHeadingLevel value) =>
        SetSemantic(SemanticHeadingLevel, value, static (semantics, level) => semantics.HeadingLevel = level, nameof(SemanticHeadingLevel));

    /// <summary>Sets <see cref="IsInAccessibleTree"/>.</summary>
    public SkUiCoreNode SetIsInAccessibleTree(bool? value) =>
        SetSemantic(IsInAccessibleTree, value, static (semantics, inTree) => semantics.IsInAccessibleTree = inTree, nameof(IsInAccessibleTree));

    /// <summary>Sets <see cref="ExcludedWithChildren"/>.</summary>
    public SkUiCoreNode SetExcludedWithChildren(bool value) =>
        SetSemantic(ExcludedWithChildren, value, static (semantics, excluded) => semantics.ExcludedWithChildren = excluded, nameof(ExcludedWithChildren));

    private SkUiCoreNode SetSemantic<T>(T current, T value, Action<CoreSemantics, T> apply, string propertyName)
    {
        if (EqualityComparer<T>.Default.Equals(current, value))
            return this;
        apply(_semantics ??= new CoreSemantics(), value);
        OnPropertyChanged(propertyName);
        SkUiSemantics.Invalidate(this);
        return this;
    }

    /// <summary>Whether keyboard navigation (Tab) stops at the node when it takes focus (interactive nodes do). Default <c>true</c>.</summary>
    public bool IsTabStop { get => _isTabStop; set => SetIsTabStop(value); }

    /// <summary>Tab order within the surface: lower values first, equal values in tree order. Default 0.</summary>
    public int TabIndex { get => _tabIndex; set => SetTabIndex(value); }

    /// <summary>Whether the node has keyboard focus (<see cref="Focus"/>, Tab).</summary>
    public bool IsFocused => _isFocused;

    /// <summary>Raised when the node gains keyboard focus.</summary>
    public event EventHandler? Focused;

    /// <summary>Raised when the node loses keyboard focus.</summary>
    public event EventHandler? Unfocused;

    /// <summary>Sets <see cref="IsTabStop"/>.</summary>
    public SkUiCoreNode SetIsTabStop(bool value)
    {
        if (SetProperty(ref _isTabStop, value, nameof(IsTabStop)) && !value)
            SkUiFocusManager.ValidateAll();
        return this;
    }

    /// <summary>Sets <see cref="TabIndex"/>.</summary>
    public SkUiCoreNode SetTabIndex(int value)
    {
        SetProperty(ref _tabIndex, value, nameof(TabIndex));
        return this;
    }

    /// <summary>
    /// Gives the node keyboard focus (its surface takes the platform's focus first); returns whether it has it. Nodes that
    /// react to taps (buttons, toggles, sliders, nodes with a <see cref="Tapped"/> handler) take focus while visible and a tab stop.
    /// </summary>
    public bool Focus() => SkUiSemantics.RootOf(this)?.FocusManager.Focus(this) == true;

    /// <summary>Removes keyboard focus from the node (its surface gives the platform's focus up).</summary>
    public void Unfocus()
    {
        if (_isFocused)
            SkUiSemantics.RootOf(this)?.FocusManager.Unfocus(this);
    }

    /// <summary>
    /// Moves the screen reader's focus (TalkBack, VoiceOver, Narrator) to this node (MAUI's <c>SetSemanticFocus</c>); nothing
    /// happens when no screen reader is on or the node is not an element of its surface.
    /// </summary>
    public void SetSemanticFocus() => SkUiSemantics.SetSemanticFocus(this);

    /// <summary>
    /// Fills what this node reports to assistive technologies: its role, text, value, state and actions. The base sets the
    /// tap and long-press actions when the node has handlers for them; controls add theirs (call the base first). The
    /// semantic properties (<see cref="SemanticDescription"/>, …) apply on top. Call <see cref="InvalidateSemantics"/> when
    /// something it reads changes without a redraw.
    /// </summary>
    protected virtual void OnPopulateSemantics(SkUiSemanticsInfo info) { }

    /// <summary>
    /// Performs an assistive-technology or keyboard action (double tap in TalkBack / VoiceOver, Narrator's invoke, Space /
    /// Enter on the focused node). The base runs the tap (<see cref="Tapped"/> and a control's own action) for
    /// <see cref="SkUiSemanticsActions.Activate"/> and the long press for <see cref="SkUiSemanticsActions.LongPress"/>.
    /// </summary>
    protected virtual bool OnSemanticsAction(SkUiSemanticsActions action)
    {
        var center = new Point(Frame.Width / 2, Frame.Height / 2);
        switch (action)
        {
            case SkUiSemanticsActions.Activate when HasTapAction:
            {
                var args = new SkUiTappedEventArgs(center);
                _gestures?.Tapped?.Invoke(this, args);
                if (HasIntrinsicTap)
                    OnIntrinsicTap(args);
                return true;
            }
            case SkUiSemanticsActions.LongPress when _gestures?.WantsLongPress == true:
                _gestures.RaiseLongPressed(new SkUiLongPressedEventArgs(center));
                return true;
        }
        return false;
    }

    /// <summary>Sets the value of a range element (TalkBack's and Narrator's set-value); returns whether it was done.</summary>
    protected virtual bool OnSemanticsSetValue(double value) => false;

    /// <summary>Rebuilds the surface's semantics tree, for changes of what <see cref="OnPopulateSemantics"/> reads that do not redraw.</summary>
    protected void InvalidateSemantics() => SkUiSemantics.Invalidate(this);

    /// <summary>Whether the node reacts to a tap (an intrinsic action or a <see cref="Tapped"/> handler).</summary>
    private bool HasTapAction => HasIntrinsicTap || _gestures?.Tapped is not null;

    /// <summary>Whether the node takes keyboard focus when visible and a tab stop (interactive nodes do).</summary>
    internal virtual bool TakesKeyboardFocus => HasTapAction;

    /// <summary>Whether the node's actions are enabled (Core buttons: their command can execute).</summary>
    internal virtual bool IsSemanticsEnabled => true;

    /// <summary>Corner radii of the focus ring (default: the press effect's).</summary>
    internal virtual CornerRadius FocusRingCornerRadii => PressEffectCornerRadii;

    void ISkUiAccessibleNode.GetSemantics(SkUiSemanticsInfo info)
    {
        info.IsEnabled = IsSemanticsEnabled;
        if (HasTapAction)
            info.Actions |= SkUiSemanticsActions.Activate;
        if (_gestures?.WantsLongPress == true)
            info.Actions |= SkUiSemanticsActions.LongPress;
        OnPopulateSemantics(info);
        if (_semantics is { } semantics)
        {
            info.Description = semantics.Description;
            info.Hint = semantics.Hint;
            info.HeadingLevel = semantics.HeadingLevel;
            info.IsInAccessibleTree = semantics.IsInAccessibleTree;
            info.ExcludedWithChildren = semantics.ExcludedWithChildren;
        }
        info.AutomationId = AutomationId;
    }

    bool ISkUiAccessibleNode.PerformSemanticsAction(SkUiSemanticsActions action) => IsSemanticsEnabled && OnSemanticsAction(action);

    bool ISkUiAccessibleNode.SetSemanticsValue(double value) => IsSemanticsEnabled && OnSemanticsSetValue(value);

    bool ISkUiAccessibleNode.IsKeyboardFocusable => _isVisible && _isTabStop && IsSemanticsEnabled && TakesKeyboardFocus;

    /// <summary>
    /// Something the node's focusability depends on changed (a command's <c>CanExecute</c> or parameter): a focused node that
    /// no longer takes keyboard focus loses it, as a disabled native control does.
    /// </summary>
    private protected void RevalidateFocus()
    {
        if (_isFocused)
            SkUiFocusManager.ValidateAll();
    }

    void ISkUiAccessibleNode.SetKeyboardFocus(bool focused, bool ringVisible)
    {
        if (_isFocused != focused)
        {
            _isFocused = focused;
            OnPropertyChanged(nameof(IsFocused));
            (focused ? Focused : Unfocused)?.Invoke(this, EventArgs.Empty);
        }
        var ring = focused && ringVisible;
        if (_focusRingVisible == ring)
            return;
        _focusRingVisible = ring;
        InvalidatePaint();
    }

    /// <summary>Whether the focus ring is drawn (focused from the keyboard).</summary>
    internal bool IsFocusRingVisible => _focusRingVisible;

    /// <summary>Draws the look's focus ring over the node (overlay layer).</summary>
    private void DrawFocusRing(SKCanvas canvas) =>
        SkUiLook.Current.DrawFocusRing(canvas, SkUiFocusRingPaint.For(new SKRect(0, 0, (float)Frame.Width, (float)Frame.Height), FocusRingCornerRadii));
}
