using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MauiSkiaUi.Core;

/// <summary>
/// One option of a <see cref="SkUiCoreSegmentedControl"/> (the Core form of <see cref="SkUiSegment"/>): its <see cref="Text"/>
/// and / or <see cref="Icon"/>, and whether it can be chosen. A plain object with change notification.
/// </summary>
public sealed class SkUiCoreSegment : INotifyPropertyChanged
{
    private string? _text;
    private ImageSource? _icon;
    private bool _isEnabled = true;
    private string? _automationId;
    private string? _semanticDescription;

    /// <summary>Creates an empty segment.</summary>
    public SkUiCoreSegment() { }

    /// <summary>Creates a segment showing <paramref name="text"/>.</summary>
    public SkUiCoreSegment(string? text) => _text = text;

    /// <summary>The segment's text (default <c>null</c>).</summary>
    public string? Text { get => _text; set => SetText(value); }

    /// <summary>An icon drawn at the start of the text, or alone (default <c>null</c>).</summary>
    public ImageSource? Icon { get => _icon; set => SetIcon(value); }

    /// <summary>Whether the user can choose the segment (default <c>true</c>); a disabled one is dimmed, and code can still select it.</summary>
    public bool IsEnabled { get => _isEnabled; set => SetIsEnabled(value); }

    /// <summary>An identifier for UI automation (as <c>SkUiCoreNode.AutomationId</c>).</summary>
    public string? AutomationId { get => _automationId; set => SetAutomationId(value); }

    /// <summary>What screen readers read for the segment when it has no text (an icon-only segment).</summary>
    public string? SemanticDescription { get => _semanticDescription; set => SetSemanticDescription(value); }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Sets <see cref="Text"/>.</summary>
    public SkUiCoreSegment SetText(string? value) => Set(ref _text, value);

    /// <summary>Sets <see cref="Icon"/>.</summary>
    public SkUiCoreSegment SetIcon(ImageSource? value) => Set(ref _icon, value);

    /// <summary>Sets <see cref="IsEnabled"/>.</summary>
    public SkUiCoreSegment SetIsEnabled(bool value) => Set(ref _isEnabled, value);

    /// <summary>Sets <see cref="AutomationId"/>.</summary>
    public SkUiCoreSegment SetAutomationId(string? value) => Set(ref _automationId, value);

    /// <summary>Sets <see cref="SemanticDescription"/>.</summary>
    public SkUiCoreSegment SetSemanticDescription(string? value) => Set(ref _semanticDescription, value);

    private SkUiCoreSegment Set<T>(ref T field, T value, [CallerMemberName] string? setter = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return this;
        field = value;
        // Set<Name> → Name.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(setter![3..]));
        return this;
    }
}
