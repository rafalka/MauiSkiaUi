using System.Windows.Input;
using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// A drawn chip (Core analogue of <c>SkUiChip</c>): a text, an optional image, a count badge and a remove button, for actions,
/// filters, entered values and suggestions (<see cref="Kind"/>). A filter chip toggles <see cref="IsSelected"/> on taps and shows a
/// check; an input chip shows its ×, which raises <see cref="Removed"/> and runs <see cref="RemoveCommand"/>. A button underneath
/// (text, font, image, <c>Command</c>, <c>Clicked</c>); drawn by <see cref="SkUiLook.DrawChip"/>, as the SkUi chip.
/// </summary>
public class SkUiCoreChip : SkUiCoreButton, ISkUiKeyHandler
{
    private SkUiChipChrome? _chipChrome;
    private SkUiChipKind _kind;
    private bool _isSelected;
    private bool? _showsRemoveButton;
    private string? _badgeText;
    private Color? _selectedColor;
    private Color? _strokeColor;
    private ICommand? _removeCommand;
    private Color? _badgeColor;
    private Color? _badgeTextColor;
    private SkUiChipChrome.BadgeFont _badgeFont = new(null, -1, FontAttributes.Bold);
    private readonly Action<SKCanvas> _chipPainter;

    private SkUiChipChrome ChipChrome => _chipChrome ??= new SkUiChipChrome(this);

    /// <summary>Creates an action chip in the color scheme's text color, outlined.</summary>
    public SkUiCoreChip()
    {
        SetTextColor(SkUiColors.DefaultForeground);
        _chipPainter = PaintChip;
        SetPaintBackground(_chipPainter);
    }

    /// <summary>What the chip is for (default <see cref="SkUiChipKind.Action"/>).</summary>
    public SkUiChipKind Kind { get => _kind; set => SetKind(value); }

    /// <summary>Whether the chip is selected; a tap on a filter chip toggles it.</summary>
    public bool IsSelected { get => _isSelected; set => SetIsSelected(value); }

    /// <summary>Whether the × shows (default: for <see cref="SkUiChipKind.Input"/> chips, until set).</summary>
    public bool ShowsRemoveButton { get => EffectiveShowsRemoveButton; set => SetShowsRemoveButton(value); }

    /// <summary>A count or short text in a pill at the end (default <c>null</c>: none).</summary>
    public string? BadgeText { get => _badgeText; set => SetBadgeText(value); }

    /// <summary>The badge's pill (default <c>null</c>: the color scheme's button background).</summary>
    public Color? BadgeColor { get => _badgeColor; set => SetBadgeColor(value); }

    /// <summary>The badge's text color (default <c>null</c>: the color scheme's button foreground).</summary>
    public Color? BadgeTextColor { get => _badgeTextColor; set => SetBadgeTextColor(value); }

    /// <summary>The badge's font size in DIPs (default -1: the look's <see cref="SkUiLook.ChipBadgeFontSize"/>).</summary>
    public double BadgeFontSize { get => _badgeFont.FontSize; set => SetBadgeFontSize(value); }

    /// <summary>The badge's font family (default <c>null</c>: the default font).</summary>
    public string? BadgeFontFamily { get => _badgeFont.FontFamily; set => SetBadgeFontFamily(value); }

    /// <summary>The badge's font attributes (default <see cref="FontAttributes.Bold"/>).</summary>
    public FontAttributes BadgeFontAttributes { get => _badgeFont.Attributes; set => SetBadgeFontAttributes(value); }

    /// <summary>The background while selected (default <c>null</c>: the accent, light).</summary>
    public Color? SelectedColor { get => _selectedColor; set => SetSelectedColor(value); }

    /// <summary>The outline (default <c>null</c>: a muted line while not selected, none while selected).</summary>
    public Color? StrokeColor { get => _strokeColor; set => SetStrokeColor(value); }

    /// <summary>Run with <see cref="SkUiCoreButton.CommandParameter"/> when the chip is removed.</summary>
    public ICommand? RemoveCommand { get => _removeCommand; set => SetRemoveCommand(value); }

    /// <summary>Raised when the chip is removed (its ×, a screen reader's action, Delete / Backspace).</summary>
    public event EventHandler? Removed;

    /// <summary>Sets <see cref="Kind"/>.</summary>
    public SkUiCoreChip SetKind(SkUiChipKind value)
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, "Not a defined chip kind.");
        if (SetProperty(ref _kind, value, nameof(Kind)))
            ChromeChanged();
        return this;
    }

    /// <summary>Sets <see cref="IsSelected"/>.</summary>
    public SkUiCoreChip SetIsSelected(bool value)
    {
        if (!SetProperty(ref _isSelected, value, nameof(IsSelected)))
            return this;
        ChromeChanged();
        Group?.OnChipSelectedChanged(this);
        return this;
    }

    /// <summary>Sets <see cref="ShowsRemoveButton"/>.</summary>
    public SkUiCoreChip SetShowsRemoveButton(bool value)
    {
        if (_showsRemoveButton == value)
            return this;
        _showsRemoveButton = value;
        OnPropertyChanged(nameof(ShowsRemoveButton));
        ChromeChanged();
        return this;
    }

    /// <summary>Sets <see cref="BadgeText"/>.</summary>
    public SkUiCoreChip SetBadgeText(string? value)
    {
        if (SetProperty(ref _badgeText, value, nameof(BadgeText)))
            ChromeChanged();
        return this;
    }

    /// <summary>Sets <see cref="BadgeColor"/>.</summary>
    public SkUiCoreChip SetBadgeColor(Color? value)
    {
        if (SetProperty(ref _badgeColor, value, nameof(BadgeColor)))
            InvalidatePaint();
        return this;
    }

    /// <summary>Sets <see cref="BadgeTextColor"/>.</summary>
    public SkUiCoreChip SetBadgeTextColor(Color? value)
    {
        if (SetProperty(ref _badgeTextColor, value, nameof(BadgeTextColor)))
            InvalidatePaint();
        return this;
    }

    /// <summary>Sets <see cref="BadgeFontSize"/>: DIPs, or -1 for the look's.</summary>
    public SkUiCoreChip SetBadgeFontSize(double value)
    {
        SkUiChipChrome.ThrowIfNotBadgeFontSize(value, nameof(value));
        return SetBadgeFont(_badgeFont with { FontSize = value }, nameof(BadgeFontSize));
    }

    /// <summary>Sets <see cref="BadgeFontFamily"/>.</summary>
    public SkUiCoreChip SetBadgeFontFamily(string? value) => SetBadgeFont(_badgeFont with { FontFamily = value }, nameof(BadgeFontFamily));

    /// <summary>Sets <see cref="BadgeFontAttributes"/>.</summary>
    public SkUiCoreChip SetBadgeFontAttributes(FontAttributes value) => SetBadgeFont(_badgeFont with { Attributes = value }, nameof(BadgeFontAttributes));

    private SkUiCoreChip SetBadgeFont(SkUiChipChrome.BadgeFont value, string propertyName)
    {
        if (SetProperty(ref _badgeFont, value, propertyName))
            ChromeChanged();
        return this;
    }

    /// <summary>Sets <see cref="SelectedColor"/>.</summary>
    public SkUiCoreChip SetSelectedColor(Color? value)
    {
        if (SetProperty(ref _selectedColor, value, nameof(SelectedColor)))
            InvalidatePaint();
        return this;
    }

    /// <summary>Sets <see cref="StrokeColor"/>.</summary>
    public SkUiCoreChip SetStrokeColor(Color? value)
    {
        if (SetProperty(ref _strokeColor, value, nameof(StrokeColor)))
            InvalidatePaint();
        return this;
    }

    /// <summary>Sets <see cref="RemoveCommand"/>.</summary>
    public SkUiCoreChip SetRemoveCommand(ICommand? value)
    {
        SetProperty(ref _removeCommand, value, nameof(RemoveCommand));
        return this;
    }

    /// <summary>The group the chip belongs to, when a <see cref="SkUiCoreChipGroup"/> made it.</summary>
    internal SkUiCoreChipGroup? Group { get; set; }

    internal bool EffectiveShowsRemoveButton => _showsRemoveButton ?? _kind == SkUiChipKind.Input;

    internal bool IsToggle => Group is { } group ? group.SelectionMode != SkUiChipSelectionMode.None : _kind == SkUiChipKind.Filter;

    private void ChromeChanged()
    {
        InvalidateMeasure();
        InvalidatePaint();
    }

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
    protected override double DefaultBorderWidth => 0;

    private void PaintChip(SKCanvas canvas)
    {
        // A solid Background (a gradient's averaged color) fills an unselected chip.
        var background = Background is { } paint && SkUiFill.From(paint) is { IsVisible: true } fill
            ? new Color(fill.Color.Red / 255f, fill.Color.Green / 255f, fill.Color.Blue / 255f, fill.Color.Alpha / 255f)
            : null;
        ChipChrome.Draw(canvas, ChromeState, (float)Frame.Width, (float)Frame.Height, Padding, CornerRadii, background, _selectedColor, _strokeColor,
            TextColor, _badgeColor, _badgeTextColor, PressVisual, PressPosition, IsPressed, IsInteractionEnabled);
    }

    /// <inheritdoc />
    private protected override double ContentOpacity => IsInteractionEnabled ? 1 : 0.5;

    /// <inheritdoc />
    internal override void OnIntrinsicTap(SkUiTappedEventArgs args)
    {
        if (!CanReceiveTap)
            return;
        if (EffectiveShowsRemoveButton && ChipChrome.Layout(ChromeState, (float)Frame.Width, (float)Frame.Height, Padding).RemoveHit
                .Contains((float)args.Position.X, (float)args.Position.Y))
        {
            Remove();
            return;
        }
        if (Group is { } group)
            group.OnChipTapped(this);
        else if (_kind == SkUiChipKind.Filter)
            SetIsSelected(!_isSelected);
        base.OnIntrinsicTap(args);
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
        if (key.Key is not (SkUiKey.Delete or SkUiKey.Backspace) || key.HasCommandModifier || !EffectiveShowsRemoveButton || !IsInteractionEnabled)
            return false;
        Remove();
        return true;
    }

    /// <inheritdoc />
    internal override void OnEffectiveFlowDirectionChanged()
    {
        base.OnEffectiveFlowDirectionChanged();
        ChromeChanged();
    }

    /// <inheritdoc />
    internal override void ReleaseDrawingResources()
    {
        base.ReleaseDrawingResources();
        _chipChrome?.Release();
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
