using System.Diagnostics;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A single-child Skia composition host; GPU rendering is the standalone default. The child is <see cref="Content"/>
/// or comes from <see cref="ContentTemplate"/>; with <see cref="ContentLoading"/> set to
/// <see cref="SkUiContentLoading.WhenShown"/> it is attached (and a template instantiated) only once the view is
/// first shown, so content that is never shown costs nothing. A <see cref="ControlTemplate"/> of drawn views wraps the
/// content, shown by an <see cref="SkUiContentPresenter"/> in it; it is created when the content loads.
/// </summary>
[ContentProperty(nameof(Content))]
public class SkUiContentView : SkUiView, ISkUiTemplatedContent
{
    private ISkUiView? _content;
    private Thickness _padding;
    private bool _loaded = true;
    private readonly SkUiContentSlot _main;
    private SkUiContentLoading _contentLoading;
    private TimeSpan _contentLoadingDelay;
    private SkUiViewAnimation? _contentLoadedAnimation;
    private IDisposable? _loadTimer;
    private ControlTemplate? _controlTemplate;
    private ControlTemplate? _appliedTemplate;
    private ISkUiView? _templateRoot;
    private List<SkUiContentPresenter>? _presenters;

    /// <summary>Bindable inset around content.</summary>
    public static readonly BindableProperty PaddingProperty = BindableProperty.Create(nameof(Padding), typeof(Thickness), typeof(SkUiContentView), default(Thickness),
        propertyChanged: (view, _, value) => ((SkUiContentView)view).OnPaddingChanged((Thickness)value));
    /// <summary>Inset around the hosted content.</summary>
    public Thickness Padding { get => (Thickness)GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }
    /// <summary>Sets padding (same as the property setter).</summary>
    public SkUiContentView SetPadding(Thickness value) { Padding = value; return this; }
    private void OnPaddingChanged(Thickness value) { _padding = value; InvalidateMeasureOverride(); }

    /// <summary>The bindable single-child content property.</summary>
    public static readonly BindableProperty ContentProperty = BindableProperty.Create(
        nameof(Content), typeof(ISkUiView), typeof(SkUiContentView), null,
        validateValue: (bindable, value) =>
        {
            if (value is ISkUiView child && !ReferenceEquals(((SkUiContentView)bindable).Content, child))
                ((SkUiContentView)bindable).ValidateChild(child);
            return true;
        },
        propertyChanged: (bindable, _, newValue) => ((SkUiContentView)bindable).OnContentPropertyChanged((ISkUiView?)newValue));

    /// <summary>Bindable <see cref="ContentTemplate"/>.</summary>
    public static readonly BindableProperty ContentTemplateProperty = BindableProperty.Create(
        nameof(ContentTemplate), typeof(DataTemplate), typeof(SkUiContentView), null,
        propertyChanged: (bindable, _, _) => ((SkUiContentView)bindable).OnContentTemplateChanged());

    private static readonly BindablePropertyKey IsContentLoadedPropertyKey = BindableProperty.CreateReadOnly(
        nameof(IsContentLoaded), typeof(bool), typeof(SkUiContentView), true);

    /// <summary>Bindable read-only <see cref="IsContentLoaded"/> (for triggers).</summary>
    public static readonly BindableProperty IsContentLoadedProperty = IsContentLoadedPropertyKey.BindableProperty;

    /// <summary>Bindable <see cref="ControlTemplate"/>.</summary>
    public static readonly BindableProperty ControlTemplateProperty = BindableProperty.Create(
        nameof(ControlTemplate), typeof(ControlTemplate), typeof(SkUiContentView), null,
        propertyChanged: (bindable, _, newValue) => ((SkUiContentView)bindable).OnControlTemplateChanged((ControlTemplate?)newValue));

    /// <summary>Creates a GPU-backed composition root when used in the MAUI visual tree.</summary>
    public SkUiContentView()
    {
        _main = new SkUiContentSlot(this, ContentProperty, ContentTemplateProperty);
        HwAccelerated = true;
    }

    /// <summary>The handlerless child painted into this host's surface.</summary>
    public ISkUiView? Content
    {
        get => (ISkUiView?)GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>
    /// Creates the content when <see cref="Content"/> is not set: a template of drawn views (a
    /// <see cref="DataTemplateSelector"/> chooses by <see cref="BindableObject.BindingContext"/>, again when it changes:
    /// a different template replaces the content). It is instantiated once the view is in a tree and its content loads,
    /// and assigned to <see cref="Content"/>; a new template replaces it.
    /// </summary>
    public DataTemplate? ContentTemplate
    {
        get => (DataTemplate?)GetValue(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    /// <summary>
    /// Wraps the content in a tree of drawn views (MAUI's <c>ControlTemplate</c>; its root must be an
    /// <see cref="ISkUiView"/>); an <see cref="SkUiContentPresenter"/> inside it shows the content. It is created when
    /// the content loads (see <see cref="ContentLoading"/>); without a presenter no content is shown and
    /// <see cref="ContentTemplate"/> does not run. Unlike MAUI, the root inherits this view's binding context, and
    /// <c>TemplateBinding</c> / <c>RelativeSource TemplatedParent</c> do not reach drawn controls: bind with
    /// <c>RelativeSource AncestorType</c> instead.
    /// </summary>
    public ControlTemplate? ControlTemplate
    {
        get => (ControlTemplate?)GetValue(ControlTemplateProperty);
        set => SetValue(ControlTemplateProperty, value);
    }

    /// <summary>The root view created from <see cref="ControlTemplate"/>, or <c>null</c>.</summary>
    public ISkUiView? TemplateRoot => _templateRoot;

    /// <summary>Sets the control template (same as the property setter).</summary>
    public SkUiContentView SetControlTemplate(ControlTemplate? value) { ControlTemplate = value; return this; }

    /// <summary>Whether the content is attached (always, unless <see cref="ContentLoading"/> defers it and the view has not been shown yet).</summary>
    public bool IsContentLoaded => (bool)GetValue(IsContentLoadedProperty);

    /// <summary>
    /// When the content is attached: <see cref="SkUiContentLoading.Immediate"/> (default) or
    /// <see cref="SkUiContentLoading.WhenShown"/>. Deferring content of a view that has already been drawn has no
    /// effect; going back to <see cref="SkUiContentLoading.Immediate"/> loads at once. The <see cref="ControlTemplate"/>
    /// is deferred with the content. Until it loads the view measures as its size requests.
    /// </summary>
    public SkUiContentLoading ContentLoading { get => _contentLoading; set => SetContentLoading(value); }

    /// <summary>
    /// How long the view must stay shown before its deferred content loads (default none): content of panes that are
    /// only passed through (tabs switched quickly) is never created.
    /// </summary>
    public TimeSpan ContentLoadingDelay
    {
        get => _contentLoadingDelay;
        set => _contentLoadingDelay = value >= TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), "The delay cannot be negative.");
    }

    /// <summary>
    /// Runs on the content (or the <see cref="TemplateRoot"/> wrapping it) when deferred content loads (render thread;
    /// e.g. <see cref="SkUiViewAnimation.FadeIn"/>), so it does not pop in. Not run for content that loads at once.
    /// </summary>
    public SkUiViewAnimation? ContentLoadedAnimation
    {
        get => _contentLoadedAnimation;
        set
        {
            value?.Freeze();
            _contentLoadedAnimation = value;
        }
    }

    /// <summary>Raised once deferred content is attached (<see cref="IsContentLoaded"/> became <c>true</c>).</summary>
    public event EventHandler? ContentLoaded;

    /// <summary>Loads deferred content now, shown or not.</summary>
    public void LoadContent() => Load(animate: false);

    /// <summary>Replaces content, cancelling the old capture.</summary>
    public SkUiContentView SetContent(ISkUiView? value)
    {
        if (ReferenceEquals(Content, value)) return this;
        if (value is not null) ValidateChild(value);
        Content = value;
        return this;
    }

    /// <summary>The attached child: <see cref="Content"/> once it is loaded.</summary>
    private protected ISkUiView? LoadedContent => _content;

    /// <summary>The child laid out in this view: the template's root, or the loaded content.</summary>
    private protected ISkUiView? LayoutChild => _templateRoot ?? _content;

    /// <summary>The content to attach once loaded: <see cref="Content"/> here, another content in views that switch.</summary>
    private protected virtual ISkUiView? ContentToShow => Content;

    /// <summary>Whether content would be shown: no control template, or one with a presenter.</summary>
    private protected bool CanPresentContent => _appliedTemplate is null || _presenters is { Count: > 0 };

    /// <summary>Creates the content to show from its template when needed (loaded, in a tree, somewhere to show it, no explicit content).</summary>
    private protected virtual void EnsureContentToShow()
    {
        if (_loaded && Parent is not null && CanPresentContent)
            _main.Ensure();
    }

    /// <summary>Removes content created from templates (deferring again).</summary>
    private protected virtual void ClearTemplateContent() => _main.Clear();

    /// <summary>Attaches <see cref="ContentToShow"/> (created from its template if needed), once loaded.</summary>
    private protected void RefreshContent()
    {
        if (!_loaded)
            return;
        EnsureContentToShow();
        AttachContent(ContentToShow);
    }

    private void OnContentPropertyChanged(ISkUiView? value)
    {
        _main.OnContentChanged();
        // Cleared by the app: the template (if any) provides the content again.
        if (value is null && !_main.IsSetting)
            RefreshContent();
        else if (_loaded)
            AttachContent(ContentToShow);
    }

    private void AttachContent(ISkUiView? value)
    {
        if (ReferenceEquals(_content, value))
            return;
        var previous = _content;
        _content = value;
        if (_templateRoot is null)
        {
            if (previous is not null) DetachChild(previous);
            if (value is not null) AttachChild(value);
        }
        else
        {
            ReleasePresentedContent();
            PresentContent();
        }
        OnContentChanged();
        InvalidateMeasureOverride();
    }

    /// <summary>Called after replacing the hosted child.</summary>
    protected virtual void OnContentChanged() { }

    private void OnControlTemplateChanged(ControlTemplate? value)
    {
        _controlTemplate = value;
        UpdateTemplateRoot();
    }

    /// <summary>Instantiates <see cref="ControlTemplate"/> once the content loads (removes it while deferred).</summary>
    private void UpdateTemplateRoot()
    {
        var template = _loaded ? _controlTemplate : null;
        if (ReferenceEquals(_appliedTemplate, template))
            return;
        var root = template is null ? null : template.CreateContent() as ISkUiView
            ?? throw new InvalidOperationException($"The ControlTemplate of a {GetType().Name} must create a drawn view (ISkUiView, e.g. an SkUiBorder or SkUiGrid).");
        var oldRoot = _templateRoot;
        _appliedTemplate = template;
        _templateRoot = null;
        _presenters = null;
        if (oldRoot is not null)
        {
            DetachChild(oldRoot);
            SkUiContentPresenter.UpdateTemplatedParents(oldRoot); // its presenters let the content go
        }
        else if (_content is not null)
            DetachChild(_content);
        if (root is null)
        {
            if (_content is not null)
                AttachChild(_content);
        }
        else
        {
            _templateRoot = root;
            AttachChild(root);
            SkUiContentPresenter.UpdateTemplatedParents(root);
            OnApplyTemplate();
        }
        InvalidateMeasureOverride();
    }

    /// <summary>Called after a <see cref="ControlTemplate"/> was applied (<see cref="TemplateRoot"/> is set).</summary>
    protected virtual void OnApplyTemplate() { }

    /// <summary>The element named <paramref name="name"/> in the applied template, or <c>null</c> (MAUI's <c>GetTemplateChild</c>).</summary>
    protected object? GetTemplateChild(string name) =>
        _templateRoot is Element root ? Microsoft.Maui.Controls.Internals.NameScope.GetNameScope(root)?.FindByName(name) : null;

    bool ISkUiTemplatedContent.IsTemplated => _appliedTemplate is not null;

    void ISkUiTemplatedContent.AddPresenter(SkUiContentPresenter presenter)
    {
        (_presenters ??= []).Add(presenter);
        RefreshContent(); // the first presenter lets the content template run
        PresentContent();
    }

    void ISkUiTemplatedContent.RemovePresenter(SkUiContentPresenter presenter)
    {
        if (_presenters is null || !_presenters.Remove(presenter)) return;
        presenter.Present(null);
        PresentContent(); // the content may move to another presenter
    }

    /// <summary>Shows the loaded content in the template's first presenter (a view has one parent).</summary>
    private void PresentContent()
    {
        if (_content is { Parent: null } view && _presenters is [var first, ..])
            first.Present(view);
    }

    private void ReleasePresentedContent()
    {
        if (_presenters is null) return;
        foreach (var presenter in _presenters)
            presenter.Present(null);
    }

    /// <inheritdoc />
    protected override void OnParentSet()
    {
        base.OnParentSet();
        // Template content is created once the view is in a tree, so every property set before (XAML attributes,
        // object initializers, ContentLoading) applies first.
        if (Parent is not null)
            RefreshContent();
    }

    private void OnContentTemplateChanged()
    {
        _main.OnTemplateChanged();
        RefreshContent();
    }

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        // A selector picks by the binding context: a new context may need another template (as items of MAUI's
        // CollectionView and BindableLayout get). The same template keeps its content, which just rebinds.
        if (_main.Reselect())
            RefreshContent();
    }

    private void SetContentLoading(SkUiContentLoading value)
    {
        if (value is not (SkUiContentLoading.Immediate or SkUiContentLoading.WhenShown))
            throw new ArgumentOutOfRangeException(nameof(value));
        if (_contentLoading == value)
            return;
        _contentLoading = value;
        if (value == SkUiContentLoading.Immediate)
            Load(animate: false);
        else if (_loaded && !RenderState.HasCommitted)
            Defer();
    }

    // Takes the content back out until the view is shown (only before it has been drawn).
    private void Defer()
    {
        _loaded = false;
        AttachContent(null);
        ClearTemplateContent();
        UpdateTemplateRoot();
        SetValue(IsContentLoadedPropertyKey, false);
        IsShownChanged += OnShownChangedForLoading;
        OnShownChangedForLoading(this, EventArgs.Empty);
    }

    private void Load(bool animate)
    {
        if (_loaded)
            return;
        _loaded = true;
        try
        {
            UpdateTemplateRoot();
            RefreshContent();
        }
        catch
        {
            _loaded = false; // a template that fails leaves the view waiting, not "loaded" without content
            UpdateTemplateRoot();
            throw;
        }
        StopWaiting();
        // Published once the content is attached, so observers see it in place.
        SetValue(IsContentLoadedPropertyKey, true);
        if (animate && _contentLoadedAnimation is { } animation && LayoutChild is SkUiView view)
            _ = RunLoadedAnimation(animation, view);
        ContentLoaded?.Invoke(this, EventArgs.Empty);
    }

    // Observed: a failing animation is reported and the content ends at the animation's end values instead of staying
    // half faded. ContentLoaded does not wait for it (it reports the attachment).
    private static async Task RunLoadedAnimation(SkUiViewAnimation animation, SkUiView view)
    {
        var own = SkUiViewAnimation.Snapshot.Of(view);
        try
        {
            await animation.RunAsync(view);
        }
        catch (Exception exception)
        {
            animation.ApplyEnd(view, own);
            Trace.WriteLine($"SkiaUi: the content loaded animation of {view.GetType().Name} failed: {exception.Message}");
        }
    }

    private void StopWaiting()
    {
        _loadTimer?.Dispose();
        _loadTimer = null;
        IsShownChanged -= OnShownChangedForLoading;
    }

    // Loads the deferred content once the view is shown, after ContentLoadingDelay of staying shown.
    private void OnShownChangedForLoading(object? sender, EventArgs e)
    {
        if (_loaded)
            return;
        if (!IsShown)
        {
            _loadTimer?.Dispose();
            _loadTimer = null;
            return;
        }
        if (_contentLoadingDelay <= TimeSpan.Zero)
        {
            Load(animate: true);
            return;
        }
        if (_loadTimer is not null)
            return;
        _loadTimer = SkUiGestureSettings.Schedule(_contentLoadingDelay, () =>
        {
            _loadTimer = null;
            if (!_loaded && IsShown)
                Load(animate: true);
        });
        if (_loadTimer is null)
            Load(animate: true); // no timer source (headless without a dispatcher)
    }

    /// <inheritdoc />
    internal override IEnumerable<ISkUiView> SkiaChildren { get { if (LayoutChild is { } child) yield return child; } }

    /// <summary>The space around the content: the padding (borders add their stroke).</summary>
    private protected virtual Thickness ContentInset => _padding;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var inset = ContentInset;
        var size = LayoutChild?.Measure(Math.Max(0, widthConstraint - inset.HorizontalThickness), Math.Max(0, heightConstraint - inset.VerticalThickness)) ?? Size.Zero;
        return new Size(size.Width + inset.HorizontalThickness, size.Height + inset.VerticalThickness);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        var inset = ContentInset;
        LayoutChild?.Arrange(new Rect(inset.Left, inset.Top,
            Math.Max(0, size.Width - inset.HorizontalThickness), Math.Max(0, size.Height - inset.VerticalThickness)));
    }

}
