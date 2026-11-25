namespace MauiSkiaUi;

/// <summary>
/// One tab of a <see cref="SkUiTabView"/>: its header (<see cref="Header"/>, <see cref="Icon"/>, <see cref="BadgeText"/>) and its
/// pane (<see cref="Content"/>, a drawn view, or <see cref="ContentTemplate"/>, made once). The pane is created when it first
/// comes into view and kept after that. A tab item takes the tab view's binding context.
/// </summary>
[ContentProperty(nameof(Content))]
public class SkUiTabItem : Element
{
    /// <summary>Bindable property for <see cref="Header"/>.</summary>
    public static readonly BindableProperty HeaderProperty = BindableProperty.Create(nameof(Header), typeof(string), typeof(SkUiTabItem), null);

    /// <summary>Bindable property for <see cref="Icon"/>.</summary>
    public static readonly BindableProperty IconProperty = BindableProperty.Create(nameof(Icon), typeof(ImageSource), typeof(SkUiTabItem), null,
        propertyChanged: static (item, previous, value) => ((SkUiTabItem)item).OnIconChanged((ImageSource?)previous, (ImageSource?)value));

    /// <summary>Bindable property for <see cref="BadgeText"/>.</summary>
    public static readonly BindableProperty BadgeTextProperty = BindableProperty.Create(nameof(BadgeText), typeof(string), typeof(SkUiTabItem), null);

    /// <summary>Bindable property for <see cref="IsEnabled"/>.</summary>
    public static readonly BindableProperty IsEnabledProperty = BindableProperty.Create(nameof(IsEnabled), typeof(bool), typeof(SkUiTabItem), true);

    /// <summary>Bindable property for <see cref="Content"/>.</summary>
    public static readonly BindableProperty ContentProperty = BindableProperty.Create(nameof(Content), typeof(View), typeof(SkUiTabItem), null);

    /// <summary>Bindable property for <see cref="ContentTemplate"/>.</summary>
    public static readonly BindableProperty ContentTemplateProperty = BindableProperty.Create(nameof(ContentTemplate), typeof(DataTemplate), typeof(SkUiTabItem), null);

    /// <summary>Creates an empty tab.</summary>
    public SkUiTabItem() { }

    /// <summary>Creates a tab with <paramref name="header"/> and <paramref name="content"/>.</summary>
    public SkUiTabItem(string? header, View? content = null)
    {
        Header = header;
        Content = content;
    }

    /// <summary>The header's text (default <c>null</c>).</summary>
    public string? Header { get => (string?)GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

    /// <summary>An icon at the start of the header's text (above it in some looks), or alone.</summary>
    public ImageSource? Icon { get => (ImageSource?)GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>A badge at the header's top end: a number or a short text in a small pill; an empty string a dot; <c>null</c> (default) none.</summary>
    public string? BadgeText { get => (string?)GetValue(BadgeTextProperty); set => SetValue(BadgeTextProperty, value); }

    /// <summary>Whether the user can select the tab (default <c>true</c>); a disabled tab is dimmed and skipped by swipes and keys, and code can still select it.</summary>
    public bool IsEnabled { get => (bool)GetValue(IsEnabledProperty); set => SetValue(IsEnabledProperty, value); }

    /// <summary>The pane: a drawn view (native views go inside an <see cref="SkUiMauiContentView"/>).</summary>
    public View? Content { get => (View?)GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>Makes the pane when it is first shown, when <see cref="Content"/> is not set.</summary>
    public DataTemplate? ContentTemplate { get => (DataTemplate?)GetValue(ContentTemplateProperty); set => SetValue(ContentTemplateProperty, value); }

    /// <summary>Sets <see cref="Header"/> (same as the property setter).</summary>
    public SkUiTabItem SetHeader(string? value) { Header = value; return this; }

    /// <summary>Sets <see cref="Icon"/> (same as the property setter).</summary>
    public SkUiTabItem SetIcon(ImageSource? value) { Icon = value; return this; }

    /// <summary>Sets <see cref="BadgeText"/> (same as the property setter).</summary>
    public SkUiTabItem SetBadgeText(string? value) { BadgeText = value; return this; }

    /// <summary>Sets <see cref="IsEnabled"/> (same as the property setter).</summary>
    public SkUiTabItem SetIsEnabled(bool value) { IsEnabled = value; return this; }

    /// <summary>Sets <see cref="Content"/> (same as the property setter).</summary>
    public SkUiTabItem SetContent(View? value) { Content = value; return this; }

    /// <summary>Sets <see cref="ContentTemplate"/> (same as the property setter).</summary>
    public SkUiTabItem SetContentTemplate(DataTemplate? value) { ContentTemplate = value; return this; }

    // Bindings and dynamic resources inside the icon (a color, a glyph) resolve against the item, as in MAUI.
    private void OnIconChanged(ImageSource? previous, ImageSource? value)
    {
        if (previous is not null && ReferenceEquals(previous.Parent, this))
            previous.Parent = null;
        if (value is not null)
            value.Parent = this;
    }

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (Icon is { } icon)
            SetInheritedBindingContext(icon, BindingContext);
    }
}
