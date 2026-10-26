namespace MauiSkiaUi;

/// <summary>
/// One content of a content view: an explicit view in <paramref name="contentProperty"/>, or one created from the
/// template in <paramref name="templateProperty"/> and assigned to it (a <see cref="DataTemplateSelector"/> chooses by
/// the owner's binding context, again when it changes). The owner decides when the content is needed
/// (<see cref="Ensure"/>); explicit content always wins over the template.
/// <para>
/// With <paramref name="textView"/>, the property may also hold data, as MAUI's <c>ItemsView.Header</c> / <c>Footer</c> /
/// <c>EmptyView</c> do: the view <see cref="Shown"/> is then the template's, with the data as its binding context (a
/// selector chooses by the data), or else <paramref name="textView"/> of the data's text. It is created in
/// <see cref="Ensure"/> and reported to <paramref name="shownChanged"/>, since the property itself does not change.
/// </para>
/// </summary>
internal sealed class SkUiContentSlot(BindableObject owner, BindableProperty contentProperty, BindableProperty templateProperty,
    Func<string, ISkUiView>? textView = null, Action? shownChanged = null)
{
    private bool _fromTemplate;
    private DataTemplate? _selected;
    private ISkUiView? _dataView;

    /// <summary>Whether the slot itself is assigning the content (template content created or cleared).</summary>
    public bool IsSetting { get; private set; }

    /// <summary>The view to show: the content view, or the one made for data (<c>null</c> until <see cref="Ensure"/>).</summary>
    public ISkUiView? Shown => Content ?? _dataView;

    private ISkUiView? Content => owner.GetValue(contentProperty) as ISkUiView;

    /// <summary>The data the property holds when it is not a view (only with a text view factory).</summary>
    private object? Data => textView is not null && owner.GetValue(contentProperty) is { } value and not ISkUiView ? value : null;

    private DataTemplate? Template => (DataTemplate?)owner.GetValue(templateProperty);

    /// <summary>The content property changed.</summary>
    public void OnContentChanged()
    {
        if (!IsSetting)
            _fromTemplate = false; // set by the app: explicit content
        _dataView = null; // made again for new data in Ensure
    }

    /// <summary>The template changed: content it created goes (the owner ensures new content where needed).</summary>
    public void OnTemplateChanged()
    {
        Clear();
        if (_dataView is not null)
        {
            _dataView = null;
            shownChanged?.Invoke();
        }
    }

    /// <summary>Removes content created from the template.</summary>
    public void Clear()
    {
        if (!_fromTemplate)
            return;
        Set(null);
        _fromTemplate = false;
    }

    /// <summary>Creates the content from the template when there is none, or the view for data the property holds.</summary>
    public void Ensure()
    {
        if (Data is { } data)
        {
            if (_dataView is null)
            {
                _dataView = DataView(data);
                shownChanged?.Invoke();
            }
            return;
        }
        if (Content is not null || Template is not { } template)
            return;
        if (template is DataTemplateSelector selector)
            template = selector.SelectTemplate(owner.BindingContext, owner);
        _selected = template;
        var content = Create(template);
        if (content is null)
            return;
        Set(content);
        _fromTemplate = true;
    }

    private ISkUiView DataView(object data)
    {
        var template = Template;
        if (template is DataTemplateSelector selector)
            template = selector.SelectTemplate(data, owner);
        if (Create(template) is not { } view)
            return textView!(data.ToString() ?? string.Empty);
        ((BindableObject)view).BindingContext = data;
        return view;
    }

    private ISkUiView? Create(DataTemplate? template) => template?.CreateContent() switch
    {
        null => null,
        ISkUiView view => view,
        var other => throw new InvalidOperationException($"{templateProperty.PropertyName} of {owner.GetType().Name} created a {other.GetType().Name}: templates must create drawn (SkUi*) views; put native views inside an SkUiMauiContentView.")
    };

    /// <summary>
    /// The binding context changed: when a selector now picks another template, the content it created goes and
    /// <c>true</c> is returned (the owner ensures new content where needed). The same template keeps its content.
    /// </summary>
    public bool Reselect()
    {
        if (!_fromTemplate || Template is not DataTemplateSelector selector || ReferenceEquals(selector.SelectTemplate(owner.BindingContext, owner), _selected))
            return false;
        Clear();
        return true;
    }

    private void Set(ISkUiView? content)
    {
        IsSetting = true;
        try
        {
            owner.SetValue(contentProperty, content);
        }
        finally
        {
            IsSetting = false;
        }
    }
}
