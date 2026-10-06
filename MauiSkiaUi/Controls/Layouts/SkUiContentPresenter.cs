namespace MauiSkiaUi;

/// <summary>A drawn control whose <c>ControlTemplate</c> shows its content through <see cref="SkUiContentPresenter"/>s.</summary>
internal interface ISkUiTemplatedContent
{
    /// <summary>Whether a template is applied (only then do presenters below it show its content).</summary>
    bool IsTemplated { get; }

    /// <summary>A presenter in the applied template found this control: it shows the content from now on.</summary>
    void AddPresenter(SkUiContentPresenter presenter);

    /// <summary>The presenter left the template or found another control.</summary>
    void RemovePresenter(SkUiContentPresenter presenter);
}

/// <summary>
/// Shows the <c>Content</c> of the drawn control whose <c>ControlTemplate</c> contains it (MAUI's <c>ContentPresenter</c>
/// for templates of drawn views, e.g. <see cref="SkUiContentView.ControlTemplate"/>, <see cref="SkUiRadioButton.ControlTemplate"/>): a drawn view as is, text as a label
/// styled by the control's text properties. As in MAUI, the control is the nearest templated ancestor, skipping one per
/// presenter crossed on the way (content shown by an outer template belongs to that one). <see cref="SkUiContentView.Content"/>
/// is set by the control; do not set it.
/// </summary>
public class SkUiContentPresenter : SkUiContentView
{
    private ISkUiTemplatedContent? _templatedParent;

    /// <summary>The label showing text content, or <c>null</c>.</summary>
    internal SkUiLabel? TextLabel { get; private set; }

    /// <summary>Creates an empty presenter; the templated control fills it.</summary>
    public SkUiContentPresenter() => HwAccelerated = false;

    /// <inheritdoc />
    protected override void OnParentSet()
    {
        base.OnParentSet();
        UpdateTemplatedParent();
    }

    /// <summary>Lets every presenter in <paramref name="root"/>'s subtree find its templated control again (a template was applied).</summary>
    internal static void UpdateTemplatedParents(ISkUiView root)
    {
        if (root is SkUiContentPresenter presenter)
            presenter.UpdateTemplatedParent();
        if (root is SkUiView view)
            foreach (var child in view.SkiaChildren.ToArray())
                UpdateTemplatedParents(child);
    }

    private void UpdateTemplatedParent()
    {
        var parent = FindTemplatedParent();
        if (ReferenceEquals(parent, _templatedParent)) return;
        var previous = _templatedParent;
        _templatedParent = parent;
        previous?.RemovePresenter(this);
        FollowBindingContext(previous as BindableObject, parent as BindableObject);
        if (parent is null)
            Present(null);
        else
            parent.AddPresenter(this);
    }

    // The control's content belongs to the control: it binds against the control's context, as in MAUI, even when the
    // template gives an element above this presenter a context of its own.
    private void FollowBindingContext(BindableObject? previous, BindableObject? parent)
    {
        if (previous is not null)
        {
            previous.BindingContextChanged -= OnTemplatedParentBindingContextChanged;
            ClearValue(BindingContextProperty);
        }
        if (parent is null)
            return;
        parent.BindingContextChanged += OnTemplatedParentBindingContextChanged;
        BindingContext = parent.BindingContext;
    }

    private void OnTemplatedParentBindingContextChanged(object? sender, EventArgs e) => BindingContext = ((BindableObject)sender!).BindingContext;

    private ISkUiTemplatedContent? FindTemplatedParent()
    {
        var skip = 0;
        for (var element = Parent; element is not null; element = element.Parent)
        {
            if (element is ISkUiTemplatedContent { IsTemplated: true } templated)
            {
                if (skip == 0) return templated;
                skip--;
            }
            if (element is SkUiContentPresenter)
                skip++;
        }
        return null;
    }

    /// <summary>Shows <paramref name="view"/> (<c>null</c>: nothing).</summary>
    internal void Present(ISkUiView? view)
    {
        TextLabel = null;
        if (!ReferenceEquals(Content, view))
            Content = view;
    }

    /// <summary>Shows <paramref name="text"/> in this presenter's label (<c>null</c>: nothing).</summary>
    internal void PresentText(string? text)
    {
        if (text is null)
        {
            Present(null);
            return;
        }
        if (TextLabel is not { } label || !ReferenceEquals(Content, label))
        {
            label = new SkUiLabel();
            Present(label);
            TextLabel = label;
        }
        label.Text = text;
    }
}
