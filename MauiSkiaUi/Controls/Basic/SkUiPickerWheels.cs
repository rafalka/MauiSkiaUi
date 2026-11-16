using MauiSkiaUi.Rendering;
using System.Windows.Input;

namespace MauiSkiaUi;

/// <summary>
/// The wheels of <see cref="SkUiMultiColumnPicker"/>'s popup as a view of its own, always open in the page: one snapping wheel per
/// column (looping or stopping at its ends), the chosen row on a band in the middle, the outer rows fading and turning away. Fill
/// <see cref="Columns"/> with <see cref="SkUiPickerColumn"/>s (in XAML, the element's content); each column's
/// <see cref="SkUiPickerColumn.SelectedIndex"/> / <see cref="SkUiPickerColumn.SelectedItem"/> binds two-way. SkiaUi control (MAUI has
/// no inline wheel).
/// </summary>
/// <remarks>
/// <para>
/// <b>Choosing.</b> Scrolling a wheel (a drag, a fling, the pointer wheel), tapping an item, the keyboard or a screen reader moves
/// the wheels; what they show is written to the columns once every wheel has stopped and <see cref="CommitDelay"/> has passed, not
/// for each row passed on the way. A column with nothing chosen shows its first item until the wheels choose. Items a column says
/// cannot be chosen (<see cref="SkUiPickerColumn.ItemEnabledBinding"/>) are dimmed and passed.
/// </para>
/// <para>
/// <b>Keyboard</b> (one tab stop): Left / Right change the column, Up / Down, Page Up / Page Down, Home / End move its wheel, typing
/// jumps to the next item starting with what was typed. <b>Screen readers</b> read each wheel as one adjustable element named by
/// its column (<see cref="SkUiPickerColumn.Header"/>, or MAUI's <c>SemanticProperties.Description</c> set on the column).
/// </para>
/// </remarks>
[ContentProperty(nameof(Columns))]
public class SkUiPickerWheels : SkUiView, ISkUiFontElement, ISkUiKeyHandler, ISkUiColumnsHost
{
    /// <summary>The default for <see cref="CommitDelay"/>: 100 ms.</summary>
    public static readonly TimeSpan DefaultCommitDelay = TimeSpan.FromMilliseconds(100);

    private SkUiColumnsTracker? _tracker;
    private SkUiWheelsView? _wheels;
    private bool _dirty = true;
    private int[]? _pending;
    private IDisposable? _commitTimer;

    /// <summary>Creates wheels without columns.</summary>
    public SkUiPickerWheels()
    {
    }

    /// <summary>The columns' bookkeeping, created on first use (property callbacks can run before the constructor).</summary>
    private SkUiColumnsTracker Tracker
    {
        get
        {
            if (_tracker is null)
            {
                _tracker = new SkUiColumnsTracker(this);
                _tracker.ColumnsChanged += (_, _) => InheritBindingContext();
            }
            return _tracker;
        }
    }

    #region Properties

    /// <summary>Bindable property for <see cref="VisibleRows"/>.</summary>
    public static readonly BindableProperty VisibleRowsProperty = BindableProperty.Create(nameof(VisibleRows), typeof(int), typeof(SkUiPickerWheels),
        SkUiColumnsModel.DefaultVisibleRows, validateValue: (_, value) => (int)value is >= 1 and <= SkUiColumnsModel.MaximumVisibleRows,
        propertyChanged: (view, _, _) => ((SkUiPickerWheels)view).InvalidateWheels());

    /// <summary>Bindable property for <see cref="RowHeight"/>.</summary>
    public static readonly BindableProperty RowHeightProperty = BindableProperty.Create(nameof(RowHeight), typeof(double), typeof(SkUiPickerWheels), -1d,
        validateValue: (_, value) => IsRowHeight((double)value),
        propertyChanged: (view, _, _) => ((SkUiPickerWheels)view).InvalidateWheels());

    /// <summary>Bindable property for <see cref="CommitDelay"/>.</summary>
    public static readonly BindableProperty CommitDelayProperty = BindableProperty.Create(nameof(CommitDelay), typeof(TimeSpan), typeof(SkUiPickerWheels),
        DefaultCommitDelay, validateValue: (_, value) => (TimeSpan)value >= TimeSpan.Zero);

    /// <summary>Bindable property for <see cref="IsHapticFeedbackEnabled"/>.</summary>
    public static readonly BindableProperty IsHapticFeedbackEnabledProperty = BindableProperty.Create(nameof(IsHapticFeedbackEnabled), typeof(bool?),
        typeof(SkUiPickerWheels), null, propertyChanged: (view, _, value) =>
        {
            if (((SkUiPickerWheels)view)._wheels is { } wheels)
                wheels.IsHapticFeedbackEnabled = (bool?)value;
        });

    /// <summary>Bindable property for <see cref="TextColor"/>.</summary>
    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(SkUiPickerWheels), null,
        propertyChanged: (view, _, _) => ((SkUiPickerWheels)view).InvalidateWheels());

    /// <summary>Bindable property for <see cref="FontFamily"/>.</summary>
    public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(nameof(FontFamily), typeof(string), typeof(SkUiPickerWheels), null,
        propertyChanged: (view, _, _) => ((SkUiPickerWheels)view).InvalidateWheels());

    /// <summary>Bindable property for <see cref="FontSize"/>.</summary>
    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(nameof(FontSize), typeof(double), typeof(SkUiPickerWheels), 0d,
        defaultValueCreator: _ => SkUiPickerModel.DefaultFontSize, validateValue: SkUiValidate.FinitePositive,
        propertyChanged: (view, _, _) => ((SkUiPickerWheels)view).InvalidateWheels());

    /// <summary>Bindable property for <see cref="FontAttributes"/>.</summary>
    public static readonly BindableProperty FontAttributesProperty = BindableProperty.Create(nameof(FontAttributes), typeof(FontAttributes), typeof(SkUiPickerWheels),
        FontAttributes.None, propertyChanged: (view, _, _) => ((SkUiPickerWheels)view).InvalidateWheels());

    /// <summary>Bindable property for <see cref="FontAutoScalingEnabled"/>.</summary>
    public static readonly BindableProperty FontAutoScalingEnabledProperty = BindableProperty.Create(nameof(FontAutoScalingEnabled), typeof(bool), typeof(SkUiPickerWheels),
        true, propertyChanged: (view, _, _) => ((SkUiPickerWheels)view).InvalidateWheels());

    /// <summary>Bindable property for <see cref="CharacterSpacing"/>.</summary>
    public static readonly BindableProperty CharacterSpacingProperty = BindableProperty.Create(nameof(CharacterSpacing), typeof(double), typeof(SkUiPickerWheels), 0d,
        validateValue: SkUiValidate.Finite, propertyChanged: (view, _, _) => ((SkUiPickerWheels)view).InvalidateWheels());

    /// <summary>Bindable property for <see cref="SelectionChangedCommand"/>.</summary>
    public static readonly BindableProperty SelectionChangedCommandProperty = BindableProperty.Create(nameof(SelectionChangedCommand), typeof(ICommand),
        typeof(SkUiPickerWheels), null);

    /// <summary>Bindable property for <see cref="SelectionChangedCommandParameter"/>.</summary>
    public static readonly BindableProperty SelectionChangedCommandParameterProperty = BindableProperty.Create(nameof(SelectionChangedCommandParameter),
        typeof(object), typeof(SkUiPickerWheels), null);

    /// <summary>
    /// The columns, left to right (right to left in right-to-left layouts). Changing them, or a column's items, loop, width, header or
    /// item template, builds the wheels again. In XAML, the element's content.
    /// </summary>
    public IList<SkUiPickerColumn> Columns => Tracker.Columns;

    /// <summary>How many rows of each wheel show (default 5; 1–9). Odd numbers put the chosen row in the middle.</summary>
    public int VisibleRows { get => (int)GetValue(VisibleRowsProperty); set => SetValue(VisibleRowsProperty, value); }

    /// <summary>The height of a row in DIPs; -1 (default): the look's <see cref="SkUiLook.PickerWheelRowHeight"/>.</summary>
    public double RowHeight { get => (double)GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }

    /// <summary>
    /// How long the wheels stay still before what they show is written to the columns (default 100 ms; zero: as soon as they
    /// stop). Rows passed while a wheel turns are never written.
    /// </summary>
    public TimeSpan CommitDelay { get => (TimeSpan)GetValue(CommitDelayProperty); set => SetValue(CommitDelayProperty, value); }

    /// <summary>
    /// Whether a row passing the middle of a turning wheel gives a light tick, as native wheel pickers do; <c>null</c> (default): the
    /// app-wide <see cref="SkUiGestureSettings.IsWheelHapticFeedbackEnabled"/> (off unless the app turns it on). The platform's
    /// feedback (Android, iOS, Mac Catalyst), so the system's haptics setting still applies.
    /// </summary>
    public bool? IsHapticFeedbackEnabled { get => (bool?)GetValue(IsHapticFeedbackEnabledProperty); set => SetValue(IsHapticFeedbackEnabledProperty, value); }

    /// <summary>The items' text color; <c>null</c>: the color scheme's foreground.</summary>
    public Color? TextColor { get => (Color?)GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }

    /// <inheritdoc />
    public string? FontFamily { get => (string?)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    /// <inheritdoc />
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <inheritdoc />
    public FontAttributes FontAttributes { get => (FontAttributes)GetValue(FontAttributesProperty); set => SetValue(FontAttributesProperty, value); }

    /// <inheritdoc />
    public bool FontAutoScalingEnabled { get => (bool)GetValue(FontAutoScalingEnabledProperty); set => SetValue(FontAutoScalingEnabledProperty, value); }

    /// <summary>Space added between the items' characters, in DIPs.</summary>
    public double CharacterSpacing { get => (double)GetValue(CharacterSpacingProperty); set => SetValue(CharacterSpacingProperty, value); }

    /// <summary>Runs with <see cref="SelectionChangedCommandParameter"/> after a column's choice changes (when it can execute), before <see cref="SelectionChanged"/>.</summary>
    public ICommand? SelectionChangedCommand { get => (ICommand?)GetValue(SelectionChangedCommandProperty); set => SetValue(SelectionChangedCommandProperty, value); }

    /// <summary>The parameter of <see cref="SelectionChangedCommand"/>.</summary>
    public object? SelectionChangedCommandParameter { get => GetValue(SelectionChangedCommandParameterProperty); set => SetValue(SelectionChangedCommandParameterProperty, value); }

    /// <summary>The chosen index of each column (-1: none), in the order of <see cref="Columns"/>.</summary>
    public IReadOnlyList<int> SelectedIndices => Tracker.SelectedIndices;

    /// <summary>The chosen item of each column (<c>null</c>: none), in the order of <see cref="Columns"/>.</summary>
    public IReadOnlyList<object?> SelectedItems => [.. Columns.Select(column => column.SelectedItem)];

    /// <summary>Raised for each column that chose another item (the user on the wheels, or the app).</summary>
    public event EventHandler<SkUiColumnSelectionChangedEventArgs>? SelectionChanged;

    private static bool IsRowHeight(double value) => value == -1 || (double.IsFinite(value) && value > 0); // -1: the sentinel for the look's

    #endregion

    #region Fluent setters

    /// <summary>Adds <paramref name="column"/> after the others.</summary>
    public SkUiPickerWheels AddColumn(SkUiPickerColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);
        Columns.Add(column);
        return this;
    }

    /// <summary>Sets <see cref="VisibleRows"/> (same as the property setter).</summary>
    public SkUiPickerWheels SetVisibleRows(int value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, SkUiColumnsModel.MaximumVisibleRows);
        VisibleRows = value;
        return this;
    }

    /// <summary>Sets <see cref="RowHeight"/> (same as the property setter; -1: the look's).</summary>
    public SkUiPickerWheels SetRowHeight(double value)
    {
        if (!IsRowHeight(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, "A row height is positive and finite, or -1 for the look's.");
        RowHeight = value;
        return this;
    }

    /// <summary>Sets <see cref="CommitDelay"/> (same as the property setter).</summary>
    public SkUiPickerWheels SetCommitDelay(TimeSpan value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);
        CommitDelay = value;
        return this;
    }

    /// <summary>Sets <see cref="IsHapticFeedbackEnabled"/> (same as the property setter; <c>null</c>: the app-wide setting).</summary>
    public SkUiPickerWheels SetIsHapticFeedbackEnabled(bool? value) { IsHapticFeedbackEnabled = value; return this; }

    /// <summary>Sets <see cref="TextColor"/> (same as the property setter).</summary>
    public SkUiPickerWheels SetTextColor(Color? value) { TextColor = value; return this; }

    /// <summary>Sets <see cref="FontFamily"/> (same as the property setter).</summary>
    public SkUiPickerWheels SetFontFamily(string? value) { FontFamily = value; return this; }

    /// <summary>Sets <see cref="FontSize"/> (same as the property setter).</summary>
    public SkUiPickerWheels SetFontSize(double value)
    {
        if (!double.IsFinite(value) || value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), value, "A font size is positive and finite.");
        FontSize = value;
        return this;
    }

    /// <summary>Sets <see cref="FontAttributes"/> (same as the property setter).</summary>
    public SkUiPickerWheels SetFontAttributes(FontAttributes value) { FontAttributes = value; return this; }

    /// <summary>Sets <see cref="FontAutoScalingEnabled"/> (same as the property setter).</summary>
    public SkUiPickerWheels SetFontAutoScalingEnabled(bool value) { FontAutoScalingEnabled = value; return this; }

    /// <summary>Sets <see cref="CharacterSpacing"/> (same as the property setter).</summary>
    public SkUiPickerWheels SetCharacterSpacing(double value)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, "Character spacing is finite.");
        CharacterSpacing = value;
        return this;
    }

    /// <summary>Sets <see cref="SelectionChangedCommand"/> (same as the property setter).</summary>
    public SkUiPickerWheels SetSelectionChangedCommand(ICommand? value) { SelectionChangedCommand = value; return this; }

    /// <summary>Sets <see cref="SelectionChangedCommandParameter"/> (same as the property setter).</summary>
    public SkUiPickerWheels SetSelectionChangedCommandParameter(object? value) { SelectionChangedCommandParameter = value; return this; }

    #endregion

    #region The wheels

    /// <summary>The wheels (tests); built on first use.</summary>
    internal SkUiWheelsView Wheels => EnsureWheels();

    /// <summary>
    /// What the wheels show must be built again (a column, the rows, the font). Done at once once the view is in a tree, else when
    /// it gets there or is measured, so the XAML attributes and columns set before apply together.
    /// </summary>
    private void InvalidateWheels()
    {
        _dirty = true;
        if (Parent is not null)
            EnsureWheels();
        else
            InvalidateMeasureOverride();
    }

    private SkUiWheelsView EnsureWheels()
    {
        if (!_dirty && _wheels is not null)
            return _wheels;
        _dirty = false;
        CancelCommit();
        _pending = null;
        if (_wheels is { } old)
        {
            old.ScrollingChanged -= OnScrollingChanged;
            old.Release();
            DetachChild(old);
        }
        var (texts, loops, widths, states, parts) = Tracker.Snapshot();
        var style = new SkUiPickerRowStyle(FontFamily, IsSet(FontSizeProperty) ? FontSize : SkUiPickerModel.DefaultFontSize, FontAttributes,
            FontAutoScalingEnabled, CharacterSpacing, TextColor);
        var wheels = new SkUiWheelsView(texts, loops, widths, Tracker.SelectedIndices, VisibleRows, style, IsRightToLeft,
            Chosen, Select, states, parts, rowHeight: RowHeight) { IsHapticFeedbackEnabled = IsHapticFeedbackEnabled };
        wheels.ScrollingChanged += OnScrollingChanged;
        _wheels = wheels;
        AttachChild(wheels);
        return wheels;
    }

    /// <summary>What the wheels have chosen: what waits to be written, else the columns' choice.</summary>
    private IReadOnlyList<int> Chosen() => _pending ?? Tracker.SelectedIndices;

    /// <summary>The wheels chose <paramref name="indices"/>: written once they have settled.</summary>
    private void Select(int[] indices)
    {
        _pending = indices;
        ScheduleCommit();
    }

    private void OnScrollingChanged(object? sender, EventArgs args) => ScheduleCommit();

    /// <summary>A choice waits and no wheel turns: written after <see cref="CommitDelay"/> (a wheel turning again waits for its stop).</summary>
    private void ScheduleCommit()
    {
        CancelCommit();
        if (_pending is null || _wheels is not { } wheels || wheels.IsScrolling)
            return;
        var delay = CommitDelay;
        if (delay > TimeSpan.Zero)
            _commitTimer = SkUiGestureSettings.Schedule(delay, Commit);
        // No delay, or no timer source (headless without a dispatcher): at once.
        if (_commitTimer is null)
            Commit();
    }

    private void CancelCommit()
    {
        _commitTimer?.Dispose();
        _commitTimer = null;
    }

    /// <summary>Writes what the wheels chose to the columns (as handler values, so the app's one-way bindings stay).</summary>
    private void Commit()
    {
        _commitTimer = null;
        if (_pending is not { } indices || _wheels?.IsScrolling == true)
            return;
        _pending = null;
        Tracker.Apply(indices);
    }

    /// <summary>Writes a choice that waits now (tests: as if <see cref="CommitDelay"/> passed).</summary>
    internal void CommitNow()
    {
        CancelCommit();
        Commit();
    }

    /// <summary>Whether a choice of the wheels waits to be written (tests).</summary>
    internal bool HasPendingChoice => _pending is not null;

    #endregion

    #region ISkUiColumnsHost

    /// <inheritdoc />
    void ISkUiColumnsHost.ColumnsRebuilt() => InvalidateWheels();

    /// <inheritdoc />
    /// <remarks>The app's choice wins over one that waits; the wheels show it.</remarks>
    void ISkUiColumnsHost.SelectionRefreshed()
    {
        if (_dirty || _wheels is not { } wheels)
            return;
        var chosen = Tracker.SelectedIndices;
        if (_pending is { } pending && !pending.SequenceEqual(chosen))
        {
            CancelCommit();
            _pending = null;
        }
        wheels.ShowChoice(chosen);
    }

    /// <inheritdoc />
    void ISkUiColumnsHost.ItemStatesChanged(int column, IReadOnlyList<bool>? states)
    {
        if (!_dirty)
            _wheels?.ItemStatesChanged(column, states);
    }

    /// <inheritdoc />
    void ISkUiColumnsHost.SelectionChanged(int column, int previous, int current)
    {
        if (SelectionChangedCommand is { } command && command.CanExecute(SelectionChangedCommandParameter))
            command.Execute(SelectionChangedCommandParameter);
        SelectionChanged?.Invoke(this, new SkUiColumnSelectionChangedEventArgs(column, previous, current));
    }

    #endregion

    #region Layout, children, input

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var wheels = EnsureWheels();
        var width = Math.Min(Math.Ceiling(wheels.PreferredWidth()), widthConstraint);
        var height = wheels.PreferredHeight(width);
        // The child is measured too (its rows, headers and labels), at the size it gets.
        wheels.Measure(width, height);
        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size) => _wheels?.Arrange(new Rect(0, 0, size.Width, size.Height));

    /// <inheritdoc />
    internal override IEnumerable<ISkUiView> SkiaChildren { get { if (_wheels is { } wheels) yield return wheels; } }

    /// <inheritdoc />
    internal override void AddRenderChildren(List<ISkUiRenderable> children)
    {
        if (_wheels is { } wheels)
            children.Add(wheels);
    }

    /// <inheritdoc />
    protected override void OnParentSet()
    {
        base.OnParentSet();
        if (Parent is not null && _dirty)
            EnsureWheels();
    }

    /// <inheritdoc />
    internal override void OnEffectiveFlowDirectionChanged()
    {
        base.OnEffectiveFlowDirectionChanged();
        InvalidateWheels();
    }

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        InheritBindingContext();
    }

    /// <summary>The columns bind to the wheels' binding context (XAML columns bind to the view model).</summary>
    private void InheritBindingContext()
    {
        foreach (var column in Columns)
            SetInheritedBindingContext(column, BindingContext);
    }

    /// <inheritdoc />
    internal override bool TakesKeyboardFocus => true;

    /// <inheritdoc />
    bool ISkUiKeyHandler.OnKeyDown(in SkUiKeyEvent key) => IsEnabled && EnsureWheels().OnKeyDown(key);

    #endregion
}
