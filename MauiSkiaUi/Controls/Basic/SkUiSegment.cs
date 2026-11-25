namespace MauiSkiaUi;

/// <summary>
/// One option of a <see cref="SkUiSegmentedControl"/>: its <see cref="Text"/> and / or <see cref="Icon"/>, and whether it can be
/// chosen (<see cref="IsEnabled"/>). It takes the control's binding context, so its properties can bind; set
/// <c>SemanticProperties.Description</c> on an icon-only segment for screen readers.
/// </summary>
public class SkUiSegment : Element
{
    /// <summary>Bindable property for <see cref="Text"/>.</summary>
    public static readonly BindableProperty TextProperty = BindableProperty.Create(nameof(Text), typeof(string), typeof(SkUiSegment), null);

    /// <summary>Bindable property for <see cref="Icon"/>.</summary>
    public static readonly BindableProperty IconProperty = BindableProperty.Create(nameof(Icon), typeof(ImageSource), typeof(SkUiSegment), null,
        propertyChanged: static (item, previous, _) => ((SkUiSegment)item).OnIconChanged((ImageSource?)previous));

    /// <summary>Bindable property for <see cref="IsEnabled"/>.</summary>
    public static readonly BindableProperty IsEnabledProperty = BindableProperty.Create(nameof(IsEnabled), typeof(bool), typeof(SkUiSegment), true);

    /// <summary>Creates an empty segment.</summary>
    public SkUiSegment() { }

    /// <summary>Creates a segment showing <paramref name="text"/>.</summary>
    public SkUiSegment(string? text) => Text = text;

    /// <summary>The segment's text (default <c>null</c>).</summary>
    public string? Text { get => (string?)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>An icon drawn at the start of the text, or alone (default <c>null</c>).</summary>
    public ImageSource? Icon { get => (ImageSource?)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>Whether the user can choose the segment (default <c>true</c>); a disabled one is dimmed, and the app can still select it.</summary>
    public bool IsEnabled { get => (bool)GetValue(IsEnabledProperty); set => SetValue(IsEnabledProperty, value); }

    /// <summary>Sets <see cref="Text"/> (same as the property setter).</summary>
    public SkUiSegment SetText(string? value) { Text = value; return this; }

    /// <summary>Sets <see cref="Icon"/> (same as the property setter).</summary>
    public SkUiSegment SetIcon(ImageSource? value) { Icon = value; return this; }

    /// <summary>Sets <see cref="IsEnabled"/> (same as the property setter).</summary>
    public SkUiSegment SetIsEnabled(bool value) { IsEnabled = value; return this; }

    private void OnIconChanged(ImageSource? previous)
    {
        if (previous is not null && ReferenceEquals(previous.Parent, this))
        {
            previous.Parent = null;
            (Parent as SkUiSegmentedControl)?.ReadoptIcon(previous);
        }
        AdoptIcon();
    }

    /// <summary>
    /// The icon becomes the item's child, as in MAUI: bindings and dynamic resources inside it (a color, a glyph) resolve against
    /// the item. One that several items share belongs to the last of them, and to another when that one lets it go.
    /// </summary>
    internal void AdoptIcon()
    {
        if (Icon is not { } icon)
            return;
        icon.Parent = this;
        SetInheritedBindingContext(icon, BindingContext);
    }

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (Icon is { } icon && ReferenceEquals(icon.Parent, this))
            SetInheritedBindingContext(icon, BindingContext);
    }
}
