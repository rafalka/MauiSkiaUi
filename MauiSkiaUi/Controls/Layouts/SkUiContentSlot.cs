namespace MauiSkiaUi;

/// <summary>
/// One content of a content view: an explicit view in <paramref name="contentProperty"/>, or one created from the
/// template in <paramref name="templateProperty"/> and assigned to it (a <see cref="DataTemplateSelector"/> chooses by
/// the owner's binding context, again when it changes). The owner decides when the content is needed
/// (<see cref="Ensure"/>); explicit content always wins over the template.
/// </summary>
internal sealed class SkUiContentSlot(BindableObject owner, BindableProperty contentProperty, BindableProperty templateProperty)
{
    private bool _fromTemplate;
    private DataTemplate? _selected;

    /// <summary>Whether the slot itself is assigning the content (template content created or cleared).</summary>
    public bool IsSetting { get; private set; }

    private ISkUiView? Content => (ISkUiView?)owner.GetValue(contentProperty);

    private DataTemplate? Template => (DataTemplate?)owner.GetValue(templateProperty);

    /// <summary>The content property changed.</summary>
    public void OnContentChanged()
    {
        if (!IsSetting)
            _fromTemplate = false; // set by the app: explicit content
    }

    /// <summary>The template changed: content it created goes (the owner ensures new content where needed).</summary>
    public void OnTemplateChanged() => Clear();

    /// <summary>Removes content created from the template.</summary>
    public void Clear()
    {
        if (!_fromTemplate)
            return;
        Set(null);
        _fromTemplate = false;
    }

    /// <summary>Creates the content from the template when there is none.</summary>
    public void Ensure()
    {
        if (Content is not null || Template is not { } template)
            return;
        if (template is DataTemplateSelector selector)
            template = selector.SelectTemplate(owner.BindingContext, owner);
        _selected = template;
        var content = template?.CreateContent() switch
        {
            null => null,
            ISkUiView view => view,
            var other => throw new InvalidOperationException($"{templateProperty.PropertyName} of {owner.GetType().Name} created a {other.GetType().Name}: templates must create drawn (SkUi*) views; put native views inside an SkUiMauiContentView.")
        };
        if (content is null)
            return;
        Set(content);
        _fromTemplate = true;
    }

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
