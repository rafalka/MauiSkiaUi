using SkiaSharp;

namespace MauiSkiaUi;

// Accessibility of drawn views: what screen readers read (MAUI's SemanticProperties / AutomationProperties), what they
// can do, and keyboard focus (MAUI's Focus() / Unfocus() / IsFocused, tab order).
public partial class SkUiView : ISkUiAccessibleNode
{
    private SkUiFocusManager? _focusManager;
    private bool _focusRingVisible;

    /// <summary>Creates a drawn view.</summary>
    public SkUiView()
    {
        // MAUI hands Focus() / Unfocus() of a view without a native view (and of a surface root, through its handler's
        // focus mapping) to this event; the surface's focus manager decides.
#pragma warning disable CS0618 // the event is how handler-less views take focus
        FocusChangeRequested += OnFocusChangeRequested;
#pragma warning restore CS0618
    }

    /// <summary>Bindable <see cref="IsTabStop"/>.</summary>
    public static readonly BindableProperty IsTabStopProperty = BindableProperty.Create(nameof(IsTabStop), typeof(bool), typeof(SkUiView), true,
        propertyChanged: (view, _, _) => SkUiFocusManager.ValidateAll());

    /// <summary>Bindable <see cref="IsAccessibilityEnabled"/>.</summary>
    public static readonly BindableProperty IsAccessibilityEnabledProperty = BindableProperty.Create(nameof(IsAccessibilityEnabled), typeof(bool), typeof(SkUiView), true,
        propertyChanged: (view, _, _) =>
        {
            SkUiFocusManager.ValidateAll();
            SkUiSemantics.Invalidate((SkUiView)view);
        });

    /// <summary>
    /// Whether the view and its drawn subtree take part in accessibility: what screen readers read and keyboard focus.
    /// <c>false</c> hides the subtree from screen readers (as <c>AutomationProperties.ExcludedWithChildren</c>) and from
    /// keyboard focus; on a surface root, the surface also drops its platform bridge and is read as one native view, with
    /// its own <c>SemanticProperties</c> applied to it as to any MAUI view. For decorative or self-described surfaces
    /// (charts, game canvases, backgrounds). Font scaling is not affected. App-wide: <see cref="SkUiAccessibility.IsEnabled"/>.
    /// Default <c>true</c>.
    /// </summary>
    public bool IsAccessibilityEnabled { get => (bool)GetValue(IsAccessibilityEnabledProperty); set => SetValue(IsAccessibilityEnabledProperty, value); }

    /// <summary>Sets <see cref="IsAccessibilityEnabled"/> (same as the property setter).</summary>
    public SkUiView SetIsAccessibilityEnabled(bool value)
    {
        IsAccessibilityEnabled = value;
        return this;
    }

    /// <summary>Bindable <see cref="TabIndex"/>.</summary>
    public static readonly BindableProperty TabIndexProperty = BindableProperty.Create(nameof(TabIndex), typeof(int), typeof(SkUiView), 0);

    /// <summary>
    /// Whether keyboard navigation (Tab) stops at this view (as Windows' and Xamarin.Forms' <c>IsTabStop</c>; MAUI has none).
    /// Interactive views (buttons, toggles, sliders, views with a tap handler) take keyboard focus; <c>false</c> leaves
    /// this one out of the tab order and <see cref="VisualElement.Focus"/> fails. Default <c>true</c>.
    /// </summary>
    public bool IsTabStop { get => (bool)GetValue(IsTabStopProperty); set => SetValue(IsTabStopProperty, value); }

    /// <summary>
    /// Tab order within the surface: lower values first, equal values in tree order (as Windows' and Xamarin.Forms'
    /// <c>TabIndex</c>). Default 0.
    /// </summary>
    public int TabIndex { get => (int)GetValue(TabIndexProperty); set => SetValue(TabIndexProperty, value); }

    /// <summary>Sets <see cref="IsTabStop"/> (same as the property setter).</summary>
    public SkUiView SetIsTabStop(bool value)
    {
        IsTabStop = value;
        return this;
    }

    /// <summary>Sets <see cref="TabIndex"/> (same as the property setter).</summary>
    public SkUiView SetTabIndex(int value)
    {
        TabIndex = value;
        return this;
    }

    /// <summary>
    /// Moves the screen reader's focus (TalkBack, VoiceOver, Narrator) to this view, as MAUI's <c>SetSemanticFocus</c>
    /// does for native views (that extension method needs a native view; this one takes its place for drawn views).
    /// Nothing happens when no screen reader is on or the view is not an element of its surface.
    /// </summary>
    public void SetSemanticFocus() => SkUiSemantics.SetSemanticFocus(this);

    /// <summary>
    /// Fills what this view reports to assistive technologies: its role, text, value, state and actions. The base sets the
    /// tap and long-press actions when the view has handlers for them and the enabled state; controls add theirs (call the
    /// base first). MAUI's <c>SemanticProperties</c> (description, hint, heading level) and <c>AutomationProperties</c>
    /// apply on top. Call <see cref="InvalidateSemantics"/> when something it reads changes without a redraw.
    /// </summary>
    protected virtual void OnPopulateSemantics(SkUiSemanticsInfo info) { }

    /// <summary>
    /// Performs an assistive-technology or keyboard action: the double tap of TalkBack / VoiceOver, Narrator's invoke,
    /// Space / Enter on the focused view. The base runs the tap (<see cref="Tapped"/>, <see cref="TappedCommand"/>, a
    /// control's own action) for <see cref="SkUiSemanticsActions.Activate"/> and the long press for
    /// <see cref="SkUiSemanticsActions.LongPress"/>. Returns whether it was done.
    /// </summary>
    protected virtual bool OnSemanticsAction(SkUiSemanticsActions action)
    {
        var center = new Point(Width / 2, Height / 2);
        switch (action)
        {
            case SkUiSemanticsActions.Activate when WantsSingleTap:
                OnTapped(new SkUiTappedEventArgs(center));
                return true;
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

    /// <summary>Whether the view takes keyboard focus when visible, enabled and a tab stop (interactive views do).</summary>
    internal virtual bool TakesKeyboardFocus => WantsSingleTap;

    /// <summary>Corner radii of the focus ring (default: the press effect's).</summary>
    internal virtual CornerRadius FocusRingCornerRadii => PressEffectCornerRadii;

    /// <summary>The surface's semantics, kept while a platform bridge reads them (surface roots only).</summary>
    internal SkUiSemanticsOwner? SemanticsOwner { get; set; }

    /// <summary>Set by the platform bridge (surface roots): moves the screen reader's focus to an element id.</summary>
    internal Action<int>? SemanticFocusRequested { get; set; }

    /// <summary>The surface's keyboard focus (meaningful on surface roots).</summary>
    internal SkUiFocusManager FocusManager => _focusManager ??= new SkUiFocusManager(this);

    /// <summary>The surface's focus manager when one was created.</summary>
    internal SkUiFocusManager? FocusManagerIfCreated => _focusManager;

    /// <summary>Whether the focus ring is drawn (focused from the keyboard).</summary>
    internal bool IsFocusRingVisible => _focusRingVisible;

#pragma warning disable CS0618 // FocusRequestArgs belongs to the obsolete event
    private void OnFocusChangeRequested(object? sender, FocusRequestArgs args)
#pragma warning restore CS0618
    {
        var manager = SkUiSemantics.RootOf(this)?.FocusManager;
        if (!args.Focus)
            manager?.Unfocus(this);
        else if (manager is null)
            args.Result = false;
        // A surface root that takes no focus itself (a layout) passes it to its first focusable node, as a page does.
        else if (SkiaParent is null && !((ISkUiAccessibleNode)this).IsKeyboardFocusable)
            args.Result = manager.FocusFirst();
        else
            args.Result = manager.Focus(this);
    }

    void ISkUiAccessibleNode.GetSemantics(SkUiSemanticsInfo info)
    {
        if (!IsAccessibilityEnabled)
        {
            info.ExcludedWithChildren = true;
            return;
        }
        info.IsEnabled = IsVisualStateEnabled;
        if (WantsSingleTap)
            info.Actions |= SkUiSemanticsActions.Activate;
        if (_gestures?.WantsLongPress == true)
            info.Actions |= SkUiSemanticsActions.LongPress;
        OnPopulateSemantics(info);
#pragma warning disable CS0618 // AutomationProperties.Name / HelpText: still read, after SemanticProperties
        info.Description = SemanticProperties.GetDescription(this) ?? AutomationProperties.GetName(this);
        info.Hint = SemanticProperties.GetHint(this) ?? AutomationProperties.GetHelpText(this);
#pragma warning restore CS0618
        info.HeadingLevel = SemanticProperties.GetHeadingLevel(this);
        info.IsInAccessibleTree = AutomationProperties.GetIsInAccessibleTree(this);
        info.ExcludedWithChildren = AutomationProperties.GetExcludedWithChildren(this) == true;
        info.AutomationId = AutomationId;
    }

    bool ISkUiAccessibleNode.PerformSemanticsAction(SkUiSemanticsActions action) => IsVisualStateEnabled && OnSemanticsAction(action);

    bool ISkUiAccessibleNode.SetSemanticsValue(double value) => IsVisualStateEnabled && OnSemanticsSetValue(value);

    bool ISkUiAccessibleNode.IsKeyboardFocusable => IsVisible && IsTabStop && IsVisualStateEnabled && TakesKeyboardFocus;

    /// <summary>
    /// Something the node's focusability depends on changed (a command's <c>CanExecute</c> or parameter): a focused node that
    /// no longer takes keyboard focus loses it, as a disabled native control does.
    /// </summary>
    private protected void RevalidateFocus()
    {
        if (IsFocused)
            SkUiFocusManager.ValidateAll();
    }

    int ISkUiAccessibleNode.TabIndex => TabIndex;

    bool ISkUiAccessibleNode.IsFocused => IsFocused;

    void ISkUiAccessibleNode.SetKeyboardFocus(bool focused, bool ringVisible)
    {
        if (IsFocused != focused)
            ((IView)this).IsFocused = focused; // raises Focused / Unfocused and moves to the Focused / Unfocused visual state
        var ring = focused && ringVisible;
        if (_focusRingVisible == ring)
            return;
        _focusRingVisible = ring;
        InvalidatePaint();
    }

    /// <summary>Whether a MAUI property that semantics read changed (attached properties are reported by name).</summary>
    private static bool IsSemanticProperty(string? propertyName) => propertyName is
        "Description" or "Hint" or "HeadingLevel" or "Name" or "HelpText" or "IsInAccessibleTree" or "ExcludedWithChildren"
        or nameof(AutomationId);

    /// <summary>Draws the look's focus ring over the view (overlay layer).</summary>
    private void DrawFocusRing(SKCanvas canvas) =>
        SkUiLook.Current.DrawFocusRing(canvas, SkUiFocusRingPaint.For(new SKRect(0, 0, (float)Width, (float)Height), FocusRingCornerRadii));
}
