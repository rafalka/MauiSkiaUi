using System.Windows.Input;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A drawn chip: a compact element with a text, an optional leading image, a count badge and a remove button, for actions,
/// filters, entered values and suggestions (<see cref="Kind"/>). A filter chip is selected and cleared by taps (two-way
/// <see cref="IsSelected"/>) and shows a check while selected; an input chip shows its × (<see cref="ShowsRemoveButton"/>),
/// which raises <see cref="Removed"/> and runs <see cref="RemoveCommand"/>. It is a button underneath: text, font, image,
/// <c>Command</c>, <c>Clicked</c>, press feedback and keyboard focus come from <see cref="SkUiButton"/>. Drawn by
/// <see cref="SkUiLook.DrawChip"/>; <see cref="SkUiChipGroup"/> lays chips out from items. Screen readers read a filter chip as a
/// check box and offer "Remove" as an action.
/// </summary>
public class SkUiChip : SkUiButton, ISkUiKeyHandler
{
    private SkUiChipChrome? _chipChrome;
    private SkUiChipKind _kind;
    private bool _isSelected;
    private bool _showsRemoveButton;
    private string? _badgeText;
    private Color? _selectedColor;
    private Color? _strokeColor;
    private ICommand? _removeCommand;
    private Color? _badgeColor;
    private Color? _badgeTextColor;
    private SkUiChipChrome.BadgeFont _badgeFont = new(null, -1, FontAttributes.Bold);

    /// <summary>Created on first use: property callbacks can run before the constructor (see <see cref="SkUiView.StyledOr{T}"/>).</summary>
    private SkUiChipChrome ChipChrome => _chipChrome ??= new SkUiChipChrome(this);

    /// <summary>Bindable <see cref="Kind"/>.</summary>
    public static readonly BindableProperty KindProperty = BindableProperty.Create(nameof(Kind), typeof(SkUiChipKind), typeof(SkUiChip), SkUiChipKind.Action,
        validateValue: static (_, value) => value is SkUiChipKind kind && Enum.IsDefined(kind),
        propertyChanged: (view, _, value) => ((SkUiChip)view).OnKindChanged((SkUiChipKind)value));

    /// <summary>Bindable <see cref="IsSelected"/> (two-way by default: taps on a filter chip set it).</summary>
    public static readonly BindableProperty IsSelectedProperty = BindableProperty.Create(nameof(IsSelected), typeof(bool), typeof(SkUiChip), false, BindingMode.TwoWay,
        propertyChanged: (view, _, value) => ((SkUiChip)view).OnIsSelectedChanged((bool)value));

    /// <summary>Bindable <see cref="ShowsRemoveButton"/>.</summary>
    public static readonly BindableProperty ShowsRemoveButtonProperty = BindableProperty.Create(nameof(ShowsRemoveButton), typeof(bool), typeof(SkUiChip), false,
        propertyChanged: (view, _, value) => ((SkUiChip)view).OnShowsRemoveButtonChanged((bool)value),
        coerceValue: (view, value) => ((SkUiChip)view).OnShowsRemoveButtonSetOrCleared(value));

    /// <summary>Bindable <see cref="BadgeText"/>.</summary>
    public static readonly BindableProperty BadgeTextProperty = BindableProperty.Create(nameof(BadgeText), typeof(string), typeof(SkUiChip), null,
        propertyChanged: (view, _, value) => ((SkUiChip)view).OnBadgeTextChanged((string?)value));

    /// <summary>Bindable <see cref="BadgeColor"/>.</summary>
    public static readonly BindableProperty BadgeColorProperty = BindableProperty.Create(nameof(BadgeColor), typeof(Color), typeof(SkUiChip), null,
        propertyChanged: (view, _, value) => ((SkUiChip)view).OnBadgeColorChanged((Color?)value));

    /// <summary>Bindable <see cref="BadgeTextColor"/>.</summary>
    public static readonly BindableProperty BadgeTextColorProperty = BindableProperty.Create(nameof(BadgeTextColor), typeof(Color), typeof(SkUiChip), null,
        propertyChanged: (view, _, value) => ((SkUiChip)view).OnBadgeTextColorChanged((Color?)value));

    /// <summary>Bindable <see cref="BadgeFontSize"/>.</summary>
    public static readonly BindableProperty BadgeFontSizeProperty = BindableProperty.Create(nameof(BadgeFontSize), typeof(double), typeof(SkUiChip), -1d,
        validateValue: static (_, value) => value is double size && SkUiChipChrome.IsBadgeFontSize(size),
        propertyChanged: (view, _, value) => ((SkUiChip)view).OnBadgeFontChanged(fontSize: (double)value));

    /// <summary>Bindable <see cref="BadgeFontFamily"/>.</summary>
    public static readonly BindableProperty BadgeFontFamilyProperty = BindableProperty.Create(nameof(BadgeFontFamily), typeof(string), typeof(SkUiChip), null,
        propertyChanged: (view, _, value) => ((SkUiChip)view).OnBadgeFontChanged(fontFamily: (string?)value, familyChanged: true));

    /// <summary>Bindable <see cref="BadgeFontAttributes"/>.</summary>
    public static readonly BindableProperty BadgeFontAttributesProperty = BindableProperty.Create(nameof(BadgeFontAttributes), typeof(FontAttributes), typeof(SkUiChip),
        FontAttributes.Bold, propertyChanged: (view, _, value) => ((SkUiChip)view).OnBadgeFontChanged(attributes: (FontAttributes)value));

    /// <summary>Bindable <see cref="SelectedColor"/>.</summary>
    public static readonly BindableProperty SelectedColorProperty = BindableProperty.Create(nameof(SelectedColor), typeof(Color), typeof(SkUiChip), null,
        propertyChanged: (view, _, value) => ((SkUiChip)view).OnSelectedColorChanged((Color?)value));

    /// <summary>Bindable <see cref="StrokeColor"/>.</summary>
    public static readonly BindableProperty StrokeColorProperty = BindableProperty.Create(nameof(StrokeColor), typeof(Color), typeof(SkUiChip), null,
        propertyChanged: (view, _, value) => ((SkUiChip)view).OnStrokeColorChanged((Color?)value));

    /// <summary>Bindable <see cref="RemoveCommand"/>.</summary>
    public static readonly BindableProperty RemoveCommandProperty = BindableProperty.Create(nameof(RemoveCommand), typeof(ICommand), typeof(SkUiChip), null,
        propertyChanged: (view, _, value) => ((SkUiChip)view)._removeCommand = (ICommand?)value);

    private readonly Action<SKCanvas> _chipPainter;

    /// <summary>Creates an action chip.</summary>
    public SkUiChip()
    {
        _chipPainter = PaintChip;
        SetPaintBackground(_chipPainter);
    }

    /// <summary>What the chip is for (default <see cref="SkUiChipKind.Action"/>).</summary>
    public SkUiChipKind Kind { get => (SkUiChipKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }

    /// <summary>
    /// Whether the chip is selected; a tap on a filter chip toggles it (default <c>false</c>). While selected and enabled, the chip
    /// is in the <c>Selected</c> state of <c>CommonStates</c> (after <c>Disabled</c> and <c>Pressed</c>), as MAUI's selected
    /// collection items: a style's <c>Selected</c> state can set <c>TextColor</c>, <c>FontAttributes</c>, <see cref="SelectedColor"/>,
    /// <see cref="StrokeColor"/> or anything else. A style without a <c>Pressed</c> state keeps the selected look while pressed.
    /// </summary>
    public bool IsSelected { get => (bool)GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    /// <summary>
    /// Whether the × at the end shows (default: shown for <see cref="SkUiChipKind.Input"/> chips, hidden for the others, until set). A
    /// tap on it, a screen reader's "Remove" action or Delete / Backspace on the focused chip removes it: <see cref="Removed"/> and
    /// <see cref="RemoveCommand"/>; the app takes the chip (or its item) away.
    /// </summary>
    public bool ShowsRemoveButton { get => (bool)GetValue(ShowsRemoveButtonProperty); set => SetValue(ShowsRemoveButtonProperty, value); }

    /// <summary>A count or short text in a pill at the end, before the × (default <c>null</c>: none); screen readers read it as the value.</summary>
    public string? BadgeText { get => (string?)GetValue(BadgeTextProperty); set => SetValue(BadgeTextProperty, value); }

    /// <summary>The badge's pill (default <c>null</c>: the color scheme's button background).</summary>
    public Color? BadgeColor { get => (Color?)GetValue(BadgeColorProperty); set => SetValue(BadgeColorProperty, value); }

    /// <summary>The badge's text color (default <c>null</c>: the color scheme's button foreground).</summary>
    public Color? BadgeTextColor { get => (Color?)GetValue(BadgeTextColorProperty); set => SetValue(BadgeTextColorProperty, value); }

    /// <summary>The badge's font size in DIPs (default -1: the look's <see cref="SkUiLook.ChipBadgeFontSize"/>, 11); scaled with the system text size as the chip's text is.</summary>
    public double BadgeFontSize { get => (double)GetValue(BadgeFontSizeProperty); set => SetValue(BadgeFontSizeProperty, value); }

    /// <summary>The badge's font family (default <c>null</c>: the default font).</summary>
    public string? BadgeFontFamily { get => (string?)GetValue(BadgeFontFamilyProperty); set => SetValue(BadgeFontFamilyProperty, value); }

    /// <summary>The badge's font attributes (default <see cref="FontAttributes.Bold"/>).</summary>
    public FontAttributes BadgeFontAttributes { get => (FontAttributes)GetValue(BadgeFontAttributesProperty); set => SetValue(BadgeFontAttributesProperty, value); }

    /// <summary>The background while selected (default <c>null</c>: the accent, light).</summary>
    public Color? SelectedColor { get => (Color?)GetValue(SelectedColorProperty); set => SetValue(SelectedColorProperty, value); }

    /// <summary>The outline (default <c>null</c>: a muted line while not selected, none while selected).</summary>
    public Color? StrokeColor { get => (Color?)GetValue(StrokeColorProperty); set => SetValue(StrokeColorProperty, value); }

    /// <summary>Run with <see cref="SkUiButton.CommandParameter"/> when the chip is removed.</summary>
    public ICommand? RemoveCommand { get => (ICommand?)GetValue(RemoveCommandProperty); set => SetValue(RemoveCommandProperty, value); }

    /// <summary>Raised when the chip is removed (its ×, a screen reader's action, Delete / Backspace).</summary>
    public event EventHandler? Removed;

    /// <summary>Sets <see cref="Kind"/>.</summary>
    public SkUiChip SetKind(SkUiChipKind value)
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, "Not a defined chip kind.");
        Kind = value;
        return this;
    }

    /// <summary>Sets <see cref="IsSelected"/> (same as the property setter).</summary>
    public SkUiChip SetIsSelected(bool value) { IsSelected = value; return this; }

    /// <summary>Sets <see cref="ShowsRemoveButton"/> (same as the property setter).</summary>
    public SkUiChip SetShowsRemoveButton(bool value) { ShowsRemoveButton = value; return this; }

    /// <summary>Sets <see cref="BadgeText"/> (same as the property setter).</summary>
    public SkUiChip SetBadgeText(string? value) { BadgeText = value; return this; }

    /// <summary>Sets <see cref="BadgeColor"/> (same as the property setter).</summary>
    public SkUiChip SetBadgeColor(Color? value) { BadgeColor = value; return this; }

    /// <summary>Sets <see cref="BadgeTextColor"/> (same as the property setter).</summary>
    public SkUiChip SetBadgeTextColor(Color? value) { BadgeTextColor = value; return this; }

    /// <summary>Sets <see cref="BadgeFontSize"/>: DIPs, or -1 for the look's.</summary>
    public SkUiChip SetBadgeFontSize(double value)
    {
        SkUiChipChrome.ThrowIfNotBadgeFontSize(value, nameof(value));
        BadgeFontSize = value;
        return this;
    }

    /// <summary>Sets <see cref="BadgeFontFamily"/> (same as the property setter).</summary>
    public SkUiChip SetBadgeFontFamily(string? value) { BadgeFontFamily = value; return this; }

    /// <summary>Sets <see cref="BadgeFontAttributes"/> (same as the property setter).</summary>
    public SkUiChip SetBadgeFontAttributes(FontAttributes value) { BadgeFontAttributes = value; return this; }

    /// <summary>Sets <see cref="SelectedColor"/> (same as the property setter).</summary>
    public SkUiChip SetSelectedColor(Color? value) { SelectedColor = value; return this; }

    /// <summary>Sets <see cref="StrokeColor"/> (same as the property setter).</summary>
    public SkUiChip SetStrokeColor(Color? value) { StrokeColor = value; return this; }

    /// <summary>Sets <see cref="RemoveCommand"/> (same as the property setter).</summary>
    public SkUiChip SetRemoveCommand(ICommand? value) { RemoveCommand = value; return this; }

    /// <summary>The group the chip belongs to, when a <see cref="SkUiChipGroup"/> made it.</summary>
    internal SkUiChipGroup? Group { get; set; }

    /// <summary>Whether the × shows: the property once set, else for input chips.</summary>
    internal bool EffectiveShowsRemoveButton => IsSet(ShowsRemoveButtonProperty) ? _showsRemoveButton : _kind == SkUiChipKind.Input;

    /// <summary>Whether taps select the chip: a filter chip, or any chip of a group that selects.</summary>
    internal bool IsToggle => Group is { } group ? group.SelectionMode != SkUiChipSelectionMode.None : _kind == SkUiChipKind.Filter;

    private void OnKindChanged(SkUiChipKind value) { _kind = value; ChromeChanged(); }

    private void OnIsSelectedChanged(bool value)
    {
        if (_isSelected == value) return;
        _isSelected = value;
        ChromeChanged();
        ChangeVisualState();
        Group?.OnChipSelectedChanged(this);
    }

    private void OnShowsRemoveButtonChanged(bool value) { _showsRemoveButton = value; ChromeChanged(); }

    private object OnShowsRemoveButtonSetOrCleared(object value)
    {
        // Coerced on every set and on a clear (MAUI coerces both, before the store changes, and raises no change for an equal
        // value): whether the app's value or the kind's default applies is read when measured and drawn (IsSet).
        ChromeChanged();
        return value;
    }

    private void OnBadgeTextChanged(string? value) { _badgeText = value; ChromeChanged(); }
    private void OnSelectedColorChanged(Color? value) { _selectedColor = value; InvalidatePaint(); }
    private void OnBadgeColorChanged(Color? value) { _badgeColor = value; InvalidatePaint(); }
    private void OnBadgeTextColorChanged(Color? value) { _badgeTextColor = value; InvalidatePaint(); }

    private void OnBadgeFontChanged(double? fontSize = null, string? fontFamily = null, bool familyChanged = false, FontAttributes? attributes = null)
    {
        _badgeFont = new SkUiChipChrome.BadgeFont(familyChanged ? fontFamily : _badgeFont.FontFamily, fontSize ?? _badgeFont.FontSize,
            attributes ?? _badgeFont.Attributes);
        ChromeChanged();
    }
    private void OnStrokeColorChanged(Color? value) { _strokeColor = value; InvalidatePaint(); }

    /// <summary>Something the chrome's room depends on changed: measure and draw again.</summary>
    private protected void ChromeChanged()
    {
        InvalidateMeasureOverride();
        InvalidatePaint();
    }

    /// <inheritdoc />
    private protected override bool IsInSelectedVisualState => _isSelected || base.IsInSelectedVisualState;

    private SkUiChipChrome.State ChromeState => new(_kind, IsToggle, _isSelected, ImageSize.Width > 0, EffectiveShowsRemoveButton, _badgeText,
        IsRightToLeft, FontAutoScalingEnabled, _badgeFont);

    /// <inheritdoc />
    private protected override Thickness ChromeInset => ChipChrome.Inset(ChromeState);

    /// <inheritdoc />
    protected override double DefaultMinimumHeight => SkUiLook.Current.DefaultChipHeight;
    /// <inheritdoc />
    protected override Thickness DefaultPadding => SkUiLook.Current.DefaultChipPadding;
    /// <inheritdoc />
    protected override double DefaultCornerRadius => SkUiLook.Current.DefaultChipCornerRadius;
    /// <inheritdoc />
    protected override Color DefaultTextColor => SkUiColors.DefaultForeground;
    /// <inheritdoc />
    protected override double DefaultBorderWidth => 0;

    private void PaintChip(SKCanvas canvas)
    {
        // A solid Background or BackgroundColor (a gradient's averaged color) fills an unselected chip.
        var background = ResolveBackgroundFill() is { IsVisible: true } fill
            ? new Color(fill.Color.Red / 255f, fill.Color.Green / 255f, fill.Color.Blue / 255f, fill.Color.Alpha / 255f)
            : null;
        ChipChrome.Draw(canvas, ChromeState, (float)Width, (float)Height, Padding, CornerRadii, background, _selectedColor, _strokeColor,
            TextColor, _badgeColor, _badgeTextColor, PressVisual, PressPosition, IsPressed, IsVisualStateEnabled);
    }

    /// <inheritdoc />
    private protected override double ContentOpacity => IsVisualStateEnabled ? 1 : 0.5;

    /// <summary>
    /// A tap on the × removes the chip; elsewhere, a toggle chip is selected or cleared (by its group's rule in a group) before
    /// <c>Clicked</c> and <c>Command</c> run.
    /// </summary>
    protected override void OnTapped(SkUiTappedEventArgs args)
    {
        if (!CanReceiveTap) return;
        if (EffectiveShowsRemoveButton && ChipChrome.Layout(ChromeState, (float)Width, (float)Height, Padding).RemoveHit
                .Contains((float)args.Position.X, (float)args.Position.Y))
        {
            Remove();
            return;
        }
        ToggleByUser();
        base.OnTapped(args);
    }

    private void ToggleByUser()
    {
        if (Group is { } group)
            group.OnChipTapped(this);
        else if (_kind == SkUiChipKind.Filter)
            WriteBack(IsSelectedProperty, !_isSelected);
    }

    /// <summary>Sets the selection as the control's own change (a group's rule, its sync): a one-way binding of <see cref="IsSelected"/> stays.</summary>
    internal void SelectByUser(bool selected)
    {
        if (_isSelected != selected)
            WriteBack(IsSelectedProperty, selected);
    }

    /// <summary>Removes the chip as its × does: <see cref="Removed"/>, <see cref="RemoveCommand"/>, and its group's <c>ChipRemoved</c>.</summary>
    public void Remove()
    {
        Removed?.Invoke(this, EventArgs.Empty);
        if (_removeCommand is { } command && CommandParameter is var parameter && command.CanExecute(parameter))
            command.Execute(parameter);
        Group?.OnChipRemoved(this);
    }

    bool ISkUiKeyHandler.OnKeyDown(in SkUiKeyEvent key)
    {
        if (key.Key is not (SkUiKey.Delete or SkUiKey.Backspace) || key.HasCommandModifier || !EffectiveShowsRemoveButton || !IsVisualStateEnabled)
            return false;
        Remove();
        return true;
    }

    /// <inheritdoc />
    internal override void ReleaseDrawingResources()
    {
        base.ReleaseDrawingResources();
        _chipChrome?.Release();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName is nameof(FlowDirection) or nameof(FontAutoScalingEnabled))
        {
            _chipChrome?.Invalidate();
            ChromeChanged();
        }
    }

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        if (IsToggle)
        {
            info.Role = SkUiSemanticsRole.CheckBox;
            info.CheckState = _isSelected ? SkUiCheckState.Checked : SkUiCheckState.Unchecked;
        }
        if (!string.IsNullOrWhiteSpace(_badgeText))
            info.Value = _badgeText;
        if (EffectiveShowsRemoveButton)
            info.AddCustomAction(SkUiStrings.Current.RemoveText);
    }

    /// <inheritdoc />
    internal override bool OnSemanticsCustomAction(int index)
    {
        if (index != 0 || !EffectiveShowsRemoveButton)
            return false;
        Remove();
        return true;
    }
}
