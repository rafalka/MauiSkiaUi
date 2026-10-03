using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A drawn radio button, similar to MAUI's RadioButton. Checking one unchecks the others of its group, with MAUI's
/// rules: radio buttons without a <see cref="GroupName"/> are grouped with their siblings in the same parent; radio
/// buttons with a <see cref="GroupName"/> are grouped with every radio button of that name on the page.
/// MAUI's <see cref="RadioButtonGroup"/> <c>GroupName</c> / <c>SelectedValue</c> on an <see cref="SkUiLayout"/> name its radio
/// buttons and bind the group's selected <see cref="Value"/>.
/// <para>
/// <see cref="Content"/> is shown beside the circle: a string (or any object, as its <c>ToString()</c>) drawn with the
/// text properties, or a drawn view. <see cref="BorderColor"/>, <see cref="BorderWidth"/> and <see cref="CornerRadius"/>
/// outline the whole control, around <see cref="Padding"/>. A <see cref="ControlTemplate"/> of drawn views replaces all
/// of it; an <see cref="SkUiContentPresenter"/> in the template shows the content.
/// </para>
/// </summary>
[ContentProperty(nameof(Content))]
public class SkUiRadioButton : SkUiToggleControl, ISkUiTemplatedContent
{
    private Color _color = SkUiColors.Accent;
    private string? _groupName;
    private object? _value;
    private object? _content;
    private ISkUiView? _hostedContent; // a view Content drawn beside the circle (not while templated)
    private string _text = string.Empty; // string Content after the text transform
    private Color _textColor = SkUiColors.DefaultForeground;
    private double _fontSize = 16;
    private string? _fontFamily;
    private FontAttributes _fontAttributes;
    private double _characterSpacing;
    private TextTransform _textTransform = TextTransform.Default;
    private Thickness _padding;
    private SkUiChromeState _chrome;
    private SkUiRadioButtonContent? _textContent;
    private ControlTemplate? _controlTemplate;
    private ISkUiView? _templateRoot;
    private List<SkUiContentPresenter>? _presenters;

    /// <summary>Bindable dot/ring color while checked.</summary>
    public static readonly BindableProperty ColorProperty = BindableProperty.Create(nameof(Color), typeof(Color), typeof(SkUiRadioButton), null,
        defaultValueCreator: _ => SkUiColors.Accent,
        validateValue: SkUiValidate.NotNull,
        propertyChanged: (view, _, value) => ((SkUiRadioButton)view).OnColorChanged((Color)value));
    /// <summary>Bindable <see cref="GroupName"/>.</summary>
    public static readonly BindableProperty GroupNameProperty = BindableProperty.Create(nameof(GroupName), typeof(string), typeof(SkUiRadioButton), null,
        propertyChanged: (view, _, value) => ((SkUiRadioButton)view).OnGroupNameChanged((string?)value));
    /// <summary>Bindable <see cref="Value"/>.</summary>
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(nameof(Value), typeof(object), typeof(SkUiRadioButton), null,
        propertyChanged: (view, _, value) => ((SkUiRadioButton)view).OnValueChanged(value));
    /// <summary>Bindable <see cref="Content"/>.</summary>
    public static readonly BindableProperty ContentProperty = BindableProperty.Create(nameof(Content), typeof(object), typeof(SkUiRadioButton), null,
        validateValue: (bindable, value) =>
        {
            var radio = (SkUiRadioButton)bindable;
            if (value is ISkUiView view && !ReferenceEquals(radio._content, view) && view.Parent is not null)
                radio.ValidateChild(view); // throws: a view shown elsewhere
            return true;
        },
        propertyChanged: (view, _, value) => ((SkUiRadioButton)view).OnContentChanged(value));
    /// <summary>Bindable <see cref="TextColor"/>.</summary>
    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(SkUiRadioButton), null,
        defaultValueCreator: _ => SkUiColors.DefaultForeground,
        validateValue: SkUiValidate.NotNull,
        propertyChanged: (view, _, value) => ((SkUiRadioButton)view).OnTextColorChanged((Color)value));
    /// <summary>Bindable <see cref="FontSize"/>.</summary>
    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(nameof(FontSize), typeof(double), typeof(SkUiRadioButton), 16d,
        validateValue: SkUiValidate.FinitePositive,
        propertyChanged: (view, _, value) => ((SkUiRadioButton)view).OnTextStyleChanged(radio => radio._fontSize = (double)value));
    /// <summary>Bindable <see cref="FontFamily"/>.</summary>
    public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(nameof(FontFamily), typeof(string), typeof(SkUiRadioButton), null,
        propertyChanged: (view, _, value) => ((SkUiRadioButton)view).OnTextStyleChanged(radio => radio._fontFamily = (string?)value));
    /// <summary>Bindable <see cref="FontAttributes"/>.</summary>
    public static readonly BindableProperty FontAttributesProperty = BindableProperty.Create(nameof(FontAttributes), typeof(FontAttributes), typeof(SkUiRadioButton), FontAttributes.None,
        propertyChanged: (view, _, value) => ((SkUiRadioButton)view).OnTextStyleChanged(radio => radio._fontAttributes = (FontAttributes)value));
    /// <summary>Bindable <see cref="CharacterSpacing"/>.</summary>
    public static readonly BindableProperty CharacterSpacingProperty = BindableProperty.Create(nameof(CharacterSpacing), typeof(double), typeof(SkUiRadioButton), 0d,
        validateValue: SkUiValidate.Finite,
        propertyChanged: (view, _, value) => ((SkUiRadioButton)view).OnTextStyleChanged(radio => radio._characterSpacing = (double)value));
    /// <summary>Bindable <see cref="TextTransform"/>.</summary>
    public static readonly BindableProperty TextTransformProperty = BindableProperty.Create(nameof(TextTransform), typeof(TextTransform), typeof(SkUiRadioButton), TextTransform.Default,
        propertyChanged: (view, _, value) => ((SkUiRadioButton)view).OnTextStyleChanged(radio => radio._textTransform = (TextTransform)value));
    /// <summary>Bindable <see cref="BorderColor"/>.</summary>
    public static readonly BindableProperty BorderColorProperty = BindableProperty.Create(nameof(BorderColor), typeof(Color), typeof(SkUiRadioButton), Colors.Transparent,
        validateValue: SkUiValidate.NotNull,
        propertyChanged: (view, _, value) => ((SkUiRadioButton)view).OnBorderColorChanged((Color)value));
    /// <summary>Bindable <see cref="BorderWidth"/>.</summary>
    public static readonly BindableProperty BorderWidthProperty = BindableProperty.Create(nameof(BorderWidth), typeof(double), typeof(SkUiRadioButton), 0d,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiRadioButton)view).OnBorderWidthChanged((double)value));
    /// <summary>Bindable <see cref="CornerRadius"/>.</summary>
    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(nameof(CornerRadius), typeof(int), typeof(SkUiRadioButton), 0,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiRadioButton)view).OnCornerRadiusChanged((int)value));
    /// <summary>Bindable <see cref="Padding"/>.</summary>
    public static readonly BindableProperty PaddingProperty = BindableProperty.Create(nameof(Padding), typeof(Thickness), typeof(SkUiRadioButton), default(Thickness),
        propertyChanged: (view, _, value) => ((SkUiRadioButton)view).OnPaddingChanged((Thickness)value));
    /// <summary>Bindable <see cref="ControlTemplate"/>.</summary>
    public static readonly BindableProperty ControlTemplateProperty = BindableProperty.Create(nameof(ControlTemplate), typeof(ControlTemplate), typeof(SkUiRadioButton), null,
        propertyChanged: (view, _, value) => ((SkUiRadioButton)view).OnControlTemplateChanged((ControlTemplate?)value));

    /// <summary>Ring/dot color while checked.</summary>
    public Color Color { get => (Color)GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
    /// <summary>
    /// The group whose radio buttons exclude each other across the page; <c>null</c> or empty groups the radio button
    /// with its siblings in the same parent.
    /// </summary>
    public string? GroupName { get => (string?)GetValue(GroupNameProperty); set => SetValue(GroupNameProperty, value); }
    /// <summary>The value this radio button stands for: the group layout's <see cref="RadioButtonGroup.SelectedValueProperty"/> while it is checked.</summary>
    public object? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    /// <summary>
    /// What is shown beside the circle (MAUI's <c>Content</c>): a drawn view (<see cref="ISkUiView"/>), or text: a string,
    /// or any other object as its <c>ToString()</c>, drawn with <see cref="TextColor"/>, the font properties,
    /// <see cref="CharacterSpacing"/> and <see cref="TextTransform"/>, wrapped to the width left. The whole control is
    /// the tap target. With a <see cref="ControlTemplate"/>, an <see cref="SkUiContentPresenter"/> shows it instead.
    /// </summary>
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }
    /// <summary>Color of text content (dimmed while disabled).</summary>
    public Color TextColor { get => (Color)GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }
    /// <summary>Font size of text content in DIPs.</summary>
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    /// <summary>Font family of text content (system font, or a name registered via <see cref="SkUiFonts.Register"/>).</summary>
    public string? FontFamily { get => (string?)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    /// <summary>Bold and italic flags of text content.</summary>
    public FontAttributes FontAttributes { get => (FontAttributes)GetValue(FontAttributesProperty); set => SetValue(FontAttributesProperty, value); }
    /// <summary>DIPs added after each character of text content.</summary>
    public double CharacterSpacing { get => (double)GetValue(CharacterSpacingProperty); set => SetValue(CharacterSpacingProperty, value); }
    /// <summary>Displays text content in upper or lower case (invariant culture, as MAUI).</summary>
    public TextTransform TextTransform { get => (TextTransform)GetValue(TextTransformProperty); set => SetValue(TextTransformProperty, value); }
    /// <summary>Color of the border around the whole control (circle and content), drawn inside the bounds.</summary>
    public Color BorderColor { get => (Color)GetValue(BorderColorProperty); set => SetValue(BorderColorProperty, value); }
    /// <summary>Border width in DIPs (0: none); circle and content sit inside it.</summary>
    public double BorderWidth { get => (double)GetValue(BorderWidthProperty); set => SetValue(BorderWidthProperty, value); }
    /// <summary>Corner radius in DIPs of the border and the <see cref="VisualElement.Background"/> (MAUI's <c>int</c>).</summary>
    public int CornerRadius { get => (int)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    /// <summary>Space between the border and the circle and content (with a template: around the template's root).</summary>
    public Thickness Padding { get => (Thickness)GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }
    /// <summary>
    /// Replaces the drawn circle, content and chrome with a tree of drawn views (MAUI's <c>ControlTemplate</c>; its root
    /// must be an <see cref="ISkUiView"/>). The root gets the <c>Checked</c> / <c>Unchecked</c> visual states, as in MAUI,
    /// and an <see cref="SkUiContentPresenter"/> inside it shows <see cref="Content"/>. Unlike MAUI, the root inherits
    /// the radio button's binding context, and <c>TemplateBinding</c> / <c>RelativeSource TemplatedParent</c> do not
    /// reach drawn controls: bind with <c>RelativeSource AncestorType</c> instead.
    /// </summary>
    public ControlTemplate? ControlTemplate { get => (ControlTemplate?)GetValue(ControlTemplateProperty); set => SetValue(ControlTemplateProperty, value); }

    /// <summary>The root view created from <see cref="ControlTemplate"/>, or <c>null</c>.</summary>
    public ISkUiView? TemplateRoot => _templateRoot;

    /// <summary>Sets the color (same as the property setter).</summary>
    public SkUiRadioButton SetColor(Color value) { ArgumentNullException.ThrowIfNull(value); Color = value; return this; }
    private void OnColorChanged(Color value) { _color = value; InvalidatePaint(); }

    /// <summary>Sets the group name (same as the property setter).</summary>
    public SkUiRadioButton SetGroupName(string? value)
    {
        GroupName = value;
        return this;
    }

    private void OnGroupNameChanged(string? value)
    {
        var old = _groupName;
        if (old == value) return;
        _groupName = value;
        SkUiRadioGroups.OnGroupNameChanged(this, old);
    }

    /// <summary>Sets <see cref="Value"/> (same as the property setter).</summary>
    public SkUiRadioButton SetRadioValue(object? value)
    {
        Value = value;
        return this;
    }

    private void OnValueChanged(object? value)
    {
        if (Equals(_value, value)) return;
        _value = value;
        if (IsChecked)
            SkUiRadioGroups.OnSelectionChanged(this);
    }

    /// <summary>Sets <see cref="Content"/> (same as the property setter).</summary>
    public SkUiRadioButton SetContent(object? value) { Content = value; return this; }

    private void OnContentChanged(object? value)
    {
        if (_hostedContent is { } hosted)
        {
            _hostedContent = null;
            DetachChild(hosted);
        }
        ReleasePresentedContent();
        _content = value;
        UpdateText();
        if (_controlTemplate is null)
        {
            if (value is ISkUiView view)
            {
                _hostedContent = view;
                AttachChild(view);
            }
        }
        else
            PresentContent();
        InvalidateMeasureOverride();
    }

    /// <summary>The text of non-view content after the text transform.</summary>
    private void UpdateText()
    {
        var text = _content is null or ISkUiView ? string.Empty : SkUiTextTransform.Apply(_content.ToString() ?? string.Empty, _textTransform);
        if (text == _text) return;
        _text = text;
        _textContent?.Invalidate();
    }

    /// <summary>Sets the text color (same as the property setter).</summary>
    public SkUiRadioButton SetTextColor(Color value) { ArgumentNullException.ThrowIfNull(value); TextColor = value; return this; }
    private void OnTextColorChanged(Color value)
    {
        _textColor = value;
        StylePresentedText();
        InvalidatePaint();
    }

    /// <summary>Sets the font size (same as the property setter).</summary>
    public SkUiRadioButton SetFontSize(double value) { SkUiValidate.ThrowIfNotFinitePositive(value, nameof(value)); FontSize = value; return this; }
    /// <summary>Sets the font family (same as the property setter).</summary>
    public SkUiRadioButton SetFontFamily(string? value) { FontFamily = value; return this; }
    /// <summary>Sets the font attributes (same as the property setter).</summary>
    public SkUiRadioButton SetFontAttributes(FontAttributes value) { FontAttributes = value; return this; }
    /// <summary>Sets the character spacing (same as the property setter).</summary>
    public SkUiRadioButton SetCharacterSpacing(double value) { SkUiValidate.ThrowIfNotFinite(value, nameof(value)); CharacterSpacing = value; return this; }
    /// <summary>Sets the text transform (same as the property setter).</summary>
    public SkUiRadioButton SetTextTransform(TextTransform value) { TextTransform = value; return this; }

    private void OnTextStyleChanged(Action<SkUiRadioButton> apply)
    {
        apply(this);
        UpdateText();
        _textContent?.Invalidate();
        StylePresentedText();
        InvalidateMeasureOverride();
    }

    /// <summary>Sets the border color (same as the property setter).</summary>
    public SkUiRadioButton SetBorderColor(Color value) { ArgumentNullException.ThrowIfNull(value); BorderColor = value; return this; }
    private void OnBorderColorChanged(Color value) { if (_chrome.SetBorderColor(value)) InvalidatePaint(); }
    /// <summary>Sets the border width (same as the property setter).</summary>
    public SkUiRadioButton SetBorderWidth(double value) { SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value)); BorderWidth = value; return this; }
    private void OnBorderWidthChanged(double value) { if (_chrome.SetBorderWidth(value)) InvalidateMeasureOverride(); }
    /// <summary>Sets the corner radius (same as the property setter).</summary>
    public SkUiRadioButton SetCornerRadius(int value) { ArgumentOutOfRangeException.ThrowIfNegative(value); CornerRadius = value; return this; }
    private void OnCornerRadiusChanged(int value) { if (_chrome.SetRadii(new Microsoft.Maui.CornerRadius(value))) InvalidatePaint(); }
    /// <summary>Sets the padding (same as the property setter).</summary>
    public SkUiRadioButton SetPadding(Thickness value) { Padding = value; return this; }
    private void OnPaddingChanged(Thickness value) { if (_padding == value) return; _padding = value; InvalidateMeasureOverride(); }
    /// <summary>Sets the control template (same as the property setter).</summary>
    public SkUiRadioButton SetControlTemplate(ControlTemplate? value) { ControlTemplate = value; return this; }

    private void OnControlTemplateChanged(ControlTemplate? value)
    {
        if (ReferenceEquals(_controlTemplate, value)) return;
        ReleasePresentedContent();
        _presenters?.Clear();
        if (_templateRoot is { } oldRoot)
        {
            _templateRoot = null;
            DetachChild(oldRoot);
        }
        if (_hostedContent is { } hosted)
        {
            _hostedContent = null;
            DetachChild(hosted);
        }
        _controlTemplate = value;
        if (value is null)
        {
            if (_content is ISkUiView view)
            {
                _hostedContent = view;
                AttachChild(view);
            }
            InvalidateMeasureOverride();
            InvalidatePaint();
            return;
        }
        var root = value.CreateContent() as ISkUiView
            ?? throw new InvalidOperationException($"The ControlTemplate of a {nameof(SkUiRadioButton)} must create a drawn view (ISkUiView, e.g. an SkUiBorder or SkUiGrid).");
        _templateRoot = root;
        AttachChild(root);
        SkUiContentPresenter.UpdateTemplatedParents(root);
        ChangeVisualState();
        OnApplyTemplate();
        InvalidateMeasureOverride();
    }

    /// <summary>Called after a <see cref="ControlTemplate"/> was applied (<see cref="TemplateRoot"/> is set).</summary>
    protected virtual void OnApplyTemplate() { }

    /// <summary>The element named <paramref name="name"/> in the applied template, or <c>null</c> (MAUI's <c>GetTemplateChild</c>).</summary>
    protected object? GetTemplateChild(string name) =>
        _templateRoot is Element root ? Microsoft.Maui.Controls.Internals.NameScope.GetNameScope(root)?.FindByName(name) : null;

    bool ISkUiTemplatedContent.IsTemplated => _controlTemplate is not null;

    void ISkUiTemplatedContent.AddPresenter(SkUiContentPresenter presenter)
    {
        (_presenters ??= []).Add(presenter);
        PresentContent();
    }

    void ISkUiTemplatedContent.RemovePresenter(SkUiContentPresenter presenter)
    {
        if (_presenters is null || !_presenters.Remove(presenter)) return;
        presenter.Present(null);
        PresentContent(); // a view it showed may move to another presenter
    }

    /// <summary>
    /// Shows <see cref="Content"/> in the template's presenters: a view in the first one (a view has one parent), text in
    /// each one as a label styled by the text properties.
    /// </summary>
    private void PresentContent()
    {
        if (_presenters is not { Count: > 0 } presenters) return;
        if (_content is ISkUiView view)
        {
            if (view.Parent is null)
                presenters[0].Present(view);
            return;
        }
        foreach (var presenter in presenters)
            presenter.PresentText(_content is null ? null : _content.ToString());
        StylePresentedText();
    }

    private void ReleasePresentedContent()
    {
        if (_presenters is null) return;
        foreach (var presenter in _presenters)
            presenter.Present(null);
    }

    /// <summary>Gives the presenters' labels the text properties (as MAUI's content labels follow the radio button's).</summary>
    private void StylePresentedText()
    {
        if (_presenters is null) return;
        foreach (var presenter in _presenters)
        {
            if (presenter.TextLabel is not { } label) continue;
            label.StartUpdating();
            try
            {
                label.TextColor = _textColor;
                label.FontSize = _fontSize;
                label.FontFamily = _fontFamily;
                label.FontAttributes = _fontAttributes;
                label.CharacterSpacing = _characterSpacing;
                label.TextTransform = _textTransform;
            }
            finally { label.EndUpdating(); }
        }
    }

    /// <summary>Unlike the shared toggle base, a tap only selects (matching MAUI's RadioButton); it never unchecks.</summary>
    protected override SkUiCheckState NextCheckState() => SkUiCheckState.Checked;

    private protected override void OnCheckStateApplied(SkUiCheckState oldState, SkUiCheckState newState)
    {
        if (newState == SkUiCheckState.Checked)
            SkUiRadioGroups.OnChecked(this);
    }

    /// <summary>The visual state while checked (MAUI's <c>RadioButton.CheckedVisualState</c>).</summary>
    public const string CheckedVisualState = "Checked";

    /// <summary>The visual state while not checked (MAUI's <c>RadioButton.UncheckedVisualState</c>; also Indeterminate).</summary>
    public const string UncheckedVisualState = "Unchecked";

    /// <summary>
    /// As MAUI's RadioButton: <c>Checked</c> / <c>Unchecked</c> (in any group that defines them), on the radio button and
    /// on the template's root, then the common states.
    /// </summary>
    protected override void ChangeVisualState()
    {
        var state = IsChecked ? CheckedVisualState : UncheckedVisualState;
        VisualStateManager.GoToState(this, state);
        if (_templateRoot is VisualElement root)
            VisualStateManager.GoToState(root, state);
        base.ChangeVisualState();
    }

    /// <summary>A change made by the group (exclusion, selected value): written back like a user change.</summary>
    internal void SetCheckedByGroup(bool value) => CommitState(SkUiCheckStates.FromIsChecked(value));

    /// <inheritdoc />
    internal override IEnumerable<ISkUiView> SkiaChildren
    {
        get
        {
            if (_templateRoot is not null) yield return _templateRoot;
            else if (_hostedContent is not null) yield return _hostedContent;
        }
    }

    /// <inheritdoc />
    internal override Microsoft.Maui.CornerRadius PressEffectCornerRadii => _chrome.Radii;

    private Thickness ContentInset => SkUiRadioButtonContent.Inset(_padding, _chrome.BorderWidth);

    private SkUiRadioButtonContent TextContent => _textContent ??= new SkUiRadioButtonContent(this);

    private SkUiTextStyle TextStyle => SkUiRadioButtonContent.TextStyle(_fontFamily, _fontSize, _fontAttributes, _characterSpacing, TextDirection);

    /// <summary>Paragraph direction from MAUI's flow direction, as <see cref="SkUiLabel"/>'s.</summary>
    private SkUiTextDirection TextDirection
    {
        get
        {
            var effective = ((IVisualElementController)this).EffectiveFlowDirection;
            if (effective.HasFlag(EffectiveFlowDirection.RightToLeft)) return SkUiTextDirection.RightToLeft;
            return effective.HasFlag(EffectiveFlowDirection.Explicit) ? SkUiTextDirection.LeftToRight : SkUiTextDirection.Auto;
        }
    }

    /// <inheritdoc />
    internal override void OnEffectiveFlowDirectionChanged()
    {
        _textContent?.Invalidate();
        InvalidateMeasureOverride();
    }

    private bool HasContent => _hostedContent is not null || _text.Length > 0;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        if (_templateRoot is { } root)
        {
            var size = root.Measure(Math.Max(0, widthConstraint - _padding.HorizontalThickness), Math.Max(0, heightConstraint - _padding.VerticalThickness));
            return new Size(size.Width + _padding.HorizontalThickness, size.Height + _padding.VerticalThickness);
        }
        var inset = ContentInset;
        var circle = SkUiRadioButtonContent.CircleSize(widthConstraint, heightConstraint);
        if (!HasContent)
            return SkUiRadioButtonContent.Measure(circle, inset, null);
        var width = SkUiRadioButtonContent.ContentWidthConstraint(widthConstraint, circle, inset);
        var height = SkUiRadioButtonContent.ContentHeightConstraint(heightConstraint, inset);
        var content = _hostedContent is { } view ? view.Measure(width, height) : TextContent.MeasureText(_text, TextStyle, width);
        return SkUiRadioButtonContent.Measure(circle, inset, content);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        if (_templateRoot is { } root)
        {
            root.Arrange(new Rect(_padding.Left, _padding.Top,
                Math.Max(0, size.Width - _padding.HorizontalThickness), Math.Max(0, size.Height - _padding.VerticalThickness)));
            return;
        }
        if (_hostedContent is { } view)
            view.Arrange(SkUiRadioButtonContent.Arrange(size.Width, size.Height, ContentInset, hasContent: true).Content);
    }

    /// <inheritdoc />
    protected override SkUiTransitionKind TransitionKind => SkUiTransitionKind.RadioButton;

    /// <summary>Whether the background and border are drawn as rounded chrome.</summary>
    private bool HasChrome => _templateRoot is null && (SkUiCornerRadii.HasAny(_chrome.Radii) || _chrome.HasBorder);

    /// <inheritdoc />
    protected override void OnPaintBackground(SKCanvas canvas)
    {
        if (HasChrome)
            _chrome.Draw(canvas, (float)Width, (float)Height, _chrome.Radii, ResolveBackgroundFill() ?? default);
        else
            base.OnPaintBackground(canvas);
    }

    /// <inheritdoc />
    internal override SKPath? CreateShadowOutline(float width, float height) =>
        PaintBackground is null && HasChrome ? _chrome.ShadowOutline(width, height, _chrome.Radii, ResolveBackgroundPaint()) : base.CreateShadowOutline(width, height);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        if (_templateRoot is not null) return;
        var rightToLeft = IsRightToLeft;
        var placement = SkUiRadioButtonContent.Arrange(Width, Height, ContentInset, HasContent);
        var circle = rightToLeft ? SkUiRadioButtonContent.Mirror(placement.Circle, Width) : placement.Circle;
        SkUiRadioButtonContent.DrawCircle(canvas, circle, ToggleVisual, _color, IsEnabled);
        if (_text.Length == 0 || _hostedContent is not null) return;
        var area = rightToLeft ? SkUiRadioButtonContent.Mirror(placement.Content, Width) : placement.Content;
        TextContent.DrawText(canvas, _text, TextStyle, area, ToSkColor(IsEnabled ? _textColor : _textColor.MultiplyAlpha(0.5f)));
    }
}
