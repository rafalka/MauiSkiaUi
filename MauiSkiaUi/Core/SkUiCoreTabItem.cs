using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MauiSkiaUi.Core;

/// <summary>
/// One tab of a <see cref="SkUiCoreTabView"/> (the Core form of <see cref="SkUiTabItem"/>): its header (<see cref="Header"/>,
/// <see cref="Icon"/>, <see cref="BadgeText"/>, drawn by the look, or a node of the app's: <see cref="HeaderContent"/>) and its pane (<see cref="Content"/>, or <see cref="ContentFactory"/>, called once
/// when the pane first comes into view). A plain object with change notification.
/// </summary>
public sealed class SkUiCoreTabItem : INotifyPropertyChanged
{
    private string? _header;
    private ImageSource? _icon;
    private string? _badgeText;
    private bool _isEnabled = true;
    private SkUiCoreNode? _content;
    private Func<SkUiCoreNode>? _contentFactory;
    private string? _automationId;
    private string? _semanticDescription;
    private SkUiCoreNode? _headerContent;
    private bool _isSelected;
    private Color? _currentTextColor;

    private static readonly PropertyChangedEventArgs IsSelectedChangedArgs = new(nameof(IsSelected));
    private static readonly PropertyChangedEventArgs CurrentTextColorChangedArgs = new(nameof(CurrentTextColor));

    /// <summary>Creates an empty tab.</summary>
    public SkUiCoreTabItem() { }

    /// <summary>Creates a tab with <paramref name="header"/> and <paramref name="content"/>.</summary>
    public SkUiCoreTabItem(string? header, SkUiCoreNode? content = null) => (_header, _content) = (header, content);

    /// <summary>Creates a tab with <paramref name="header"/> whose pane <paramref name="contentFactory"/> makes when it first comes into view.</summary>
    public SkUiCoreTabItem(string? header, Func<SkUiCoreNode> contentFactory) => (_header, _contentFactory) = (header, contentFactory);

    /// <summary>The header's text (default <c>null</c>).</summary>
    public string? Header { get => _header; set => SetHeader(value); }

    /// <summary>An icon at the start of the header's text (above it in some looks), or alone.</summary>
    public ImageSource? Icon { get => _icon; set => SetIcon(value); }

    /// <summary>A badge at the header's top end: a short text in a pill; an empty string a dot; <c>null</c> (default) none.</summary>
    public string? BadgeText { get => _badgeText; set => SetBadgeText(value); }

    /// <summary>Whether the user can select the tab (default <c>true</c>); a disabled one is dimmed and skipped, and code can still select it.</summary>
    public bool IsEnabled { get => _isEnabled; set => SetIsEnabled(value); }

    /// <summary>The pane; a node without a parent.</summary>
    public SkUiCoreNode? Content { get => _content; set => SetContent(value); }

    /// <summary>Makes the pane when it first comes into view, when <see cref="Content"/> is not set; called once.</summary>
    public Func<SkUiCoreNode>? ContentFactory { get => _contentFactory; set => SetContentFactory(value); }

    /// <summary>An identifier for UI automation: the header's element id is the tab view's with <c>_</c> and this (else its index).</summary>
    public string? AutomationId { get => _automationId; set => SetAutomationId(value); }

    /// <summary>What screen readers call the tab when it has no <see cref="Header"/> (an icon alone).</summary>
    public string? SemanticDescription { get => _semanticDescription; set => SetSemanticDescription(value); }

    /// <summary>
    /// A header node of the app's for this tab, in place of the look's text, icon and badge (a node without a parent);
    /// <c>null</c> (default): the tab view's <see cref="SkUiCoreTabView.HeaderFactory"/>, else the look's header. It takes no
    /// input, focus or screen reader of its own: a tap selects the tab, and screen readers read <see cref="Header"/>. It follows
    /// <see cref="IsSelected"/> and <see cref="CurrentTextColor"/> through <see cref="PropertyChanged"/>.
    /// </summary>
    public SkUiCoreNode? HeaderContent { get => _headerContent; set => SetHeaderContent(value); }

    /// <summary>Whether the tab is the selected one (set by its tab view; raises <see cref="PropertyChanged"/>).</summary>
    public bool IsSelected => _isSelected;

    /// <summary>
    /// The color the look gives this tab's header text now (selected, unselected or disabled, with the tab view's colors over
    /// the look's); <c>null</c> outside a tab view. See <see cref="SkUiTabItem.CurrentTextColor"/>.
    /// </summary>
    public Color? CurrentTextColor => _currentTextColor;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Sets <see cref="Header"/>.</summary>
    public SkUiCoreTabItem SetHeader(string? value) => Set(ref _header, value);

    /// <summary>Sets <see cref="Icon"/>.</summary>
    public SkUiCoreTabItem SetIcon(ImageSource? value) => Set(ref _icon, value);

    /// <summary>Sets <see cref="BadgeText"/>.</summary>
    public SkUiCoreTabItem SetBadgeText(string? value) => Set(ref _badgeText, value);

    /// <summary>Sets <see cref="IsEnabled"/>.</summary>
    public SkUiCoreTabItem SetIsEnabled(bool value) => Set(ref _isEnabled, value);

    /// <summary>Sets <see cref="Content"/>.</summary>
    public SkUiCoreTabItem SetContent(SkUiCoreNode? value) => Set(ref _content, value);

    /// <summary>Sets <see cref="ContentFactory"/>.</summary>
    public SkUiCoreTabItem SetContentFactory(Func<SkUiCoreNode>? value) => Set(ref _contentFactory, value);

    /// <summary>Sets <see cref="AutomationId"/>.</summary>
    public SkUiCoreTabItem SetAutomationId(string? value) => Set(ref _automationId, value);

    /// <summary>Sets <see cref="SemanticDescription"/>.</summary>
    public SkUiCoreTabItem SetSemanticDescription(string? value) => Set(ref _semanticDescription, value);

    /// <summary>Sets <see cref="HeaderContent"/>.</summary>
    public SkUiCoreTabItem SetHeaderContent(SkUiCoreNode? value) => Set(ref _headerContent, value);

    /// <summary>The tab view tells the tab whether it is selected and its header's text color (<c>null</c>: it left).</summary>
    internal void SetHeaderState(bool selected, Color? textColor)
    {
        if (_isSelected != selected)
        {
            _isSelected = selected;
            PropertyChanged?.Invoke(this, IsSelectedChangedArgs);
        }
        if (!Equals(_currentTextColor, textColor))
        {
            _currentTextColor = textColor;
            PropertyChanged?.Invoke(this, CurrentTextColorChangedArgs);
        }
    }

    private SkUiCoreTabItem Set<T>(ref T field, T value, [CallerMemberName] string? setter = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return this;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(setter!["Set".Length..]));
        return this;
    }
}
