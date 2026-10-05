using System.Diagnostics;

namespace MauiSkiaUi;

/// <summary>
/// A content view with a second content: <see cref="ShowsAlternate"/> shows <see cref="SkUiContentView.Content"/>
/// (<c>false</c>), <see cref="AlternateContent"/> (<c>true</c>) or nothing (<c>null</c>, the default). Each side can
/// come from a template (<see cref="SkUiContentView.ContentTemplate"/>, <see cref="AlternateContentTemplate"/>), which
/// runs only when that side is first shown; the other side's content stays created while hidden. With
/// <see cref="BeforeStateChangeAnimation"/> / <see cref="AfterStateChangeAnimation"/> every switch animates on the
/// render thread (a newer value while it animates wins). Read / edit modes, loaded / skeleton, signed in / out.
/// </summary>
[ContentProperty(nameof(Content))]
public class SkUiAlternateContentView : SkUiContentView
{
    private readonly SkUiContentSlot _alternate;
    private bool? _shown;  // the side on screen
    private bool? _target; // the side automatic changes animate to
    private bool _animating;

    /// <summary>Bindable <see cref="AlternateContent"/>.</summary>
    public static readonly BindableProperty AlternateContentProperty = BindableProperty.Create(
        nameof(AlternateContent), typeof(ISkUiView), typeof(SkUiAlternateContentView), null,
        validateValue: (bindable, value) =>
        {
            if (value is ISkUiView child && !ReferenceEquals(((SkUiAlternateContentView)bindable).AlternateContent, child))
                ((SkUiAlternateContentView)bindable).ValidateChild(child);
            return true;
        },
        propertyChanged: (bindable, _, newValue) => ((SkUiAlternateContentView)bindable).OnAlternateContentChanged((ISkUiView?)newValue));

    /// <summary>Bindable <see cref="AlternateContentTemplate"/>.</summary>
    public static readonly BindableProperty AlternateContentTemplateProperty = BindableProperty.Create(
        nameof(AlternateContentTemplate), typeof(DataTemplate), typeof(SkUiAlternateContentView), null,
        propertyChanged: (bindable, _, _) => ((SkUiAlternateContentView)bindable).OnAlternateContentTemplateChanged());

    /// <summary>Bindable <see cref="ShowsAlternate"/>.</summary>
    public static readonly BindableProperty ShowsAlternateProperty = BindableProperty.Create(
        nameof(ShowsAlternate), typeof(bool?), typeof(SkUiAlternateContentView), null,
        propertyChanged: (bindable, _, newValue) => ((SkUiAlternateContentView)bindable).OnShowsAlternateChanged((bool?)newValue));

    /// <summary>Bindable <see cref="BeforeStateChangeAnimation"/> (XAML: <c>"FadeOut"</c>).</summary>
    public static readonly BindableProperty BeforeStateChangeAnimationProperty = BindableProperty.Create(
        nameof(BeforeStateChangeAnimation), typeof(SkUiViewAnimation), typeof(SkUiAlternateContentView), null,
        propertyChanged: (_, _, newValue) => (newValue as SkUiViewAnimation)?.Freeze());

    /// <summary>Bindable <see cref="AfterStateChangeAnimation"/> (XAML: <c>"FadeIn"</c>).</summary>
    public static readonly BindableProperty AfterStateChangeAnimationProperty = BindableProperty.Create(
        nameof(AfterStateChangeAnimation), typeof(SkUiViewAnimation), typeof(SkUiAlternateContentView), null,
        propertyChanged: (_, _, newValue) => (newValue as SkUiViewAnimation)?.Freeze());

    /// <summary>Creates a view that shows nothing until <see cref="ShowsAlternate"/> is set.</summary>
    public SkUiAlternateContentView() => _alternate = new SkUiContentSlot(this, AlternateContentProperty, AlternateContentTemplateProperty);

    /// <summary>The content shown while <see cref="ShowsAlternate"/> is <c>true</c>.</summary>
    public ISkUiView? AlternateContent
    {
        get => (ISkUiView?)GetValue(AlternateContentProperty);
        set => SetValue(AlternateContentProperty, value);
    }

    /// <summary>
    /// Creates <see cref="AlternateContent"/> when it is not set, the first time the alternate side is shown (a
    /// <see cref="DataTemplateSelector"/> chooses by the binding context, again when it changes).
    /// </summary>
    public DataTemplate? AlternateContentTemplate
    {
        get => (DataTemplate?)GetValue(AlternateContentTemplateProperty);
        set => SetValue(AlternateContentTemplateProperty, value);
    }

    /// <summary>
    /// Which content is shown: <c>false</c> the <see cref="SkUiContentView.Content"/>, <c>true</c> the
    /// <see cref="AlternateContent"/>, <c>null</c> (default) nothing. With state change animations set, the value
    /// changes at once and the views follow.
    /// </summary>
    public bool? ShowsAlternate
    {
        get => (bool?)GetValue(ShowsAlternateProperty);
        set => SetValue(ShowsAlternateProperty, value);
    }

    /// <summary>Runs on the shown content before each switch (render thread). With either animation set, switches animate.</summary>
    public SkUiViewAnimation? BeforeStateChangeAnimation
    {
        get => (SkUiViewAnimation?)GetValue(BeforeStateChangeAnimationProperty);
        set => SetValue(BeforeStateChangeAnimationProperty, value);
    }

    /// <summary>Runs on the new content after each switch (render thread).</summary>
    public SkUiViewAnimation? AfterStateChangeAnimation
    {
        get => (SkUiViewAnimation?)GetValue(AfterStateChangeAnimationProperty);
        set => SetValue(AfterStateChangeAnimationProperty, value);
    }

    /// <summary>Whether a switch is animating (the shown content still follows <see cref="ShowsAlternate"/>).</summary>
    public bool IsSwitching => _animating;

    /// <inheritdoc />
    private protected override ISkUiView? ContentToShow => _shown switch
    {
        false => Content,
        true => AlternateContent,
        null => null
    };

    /// <inheritdoc />
    private protected override void EnsureContentToShow()
    {
        if (_shown == false)
            base.EnsureContentToShow();
        else if (_shown == true && IsContentLoaded && Parent is not null)
            _alternate.Ensure();
    }

    /// <inheritdoc />
    private protected override void ClearTemplateContent()
    {
        base.ClearTemplateContent();
        _alternate.Clear();
    }

    private void OnAlternateContentChanged(ISkUiView? value)
    {
        _alternate.OnContentChanged();
        // Shown: attach it (cleared by the app, the template, if any, provides it again).
        if (_shown == true && !(value is null && _alternate.IsSetting))
            RefreshContent();
    }

    private void OnAlternateContentTemplateChanged()
    {
        _alternate.OnTemplateChanged();
        if (_shown == true)
            RefreshContent();
    }

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (_alternate.Reselect() && _shown == true)
            RefreshContent();
    }

    private void OnShowsAlternateChanged(bool? value)
    {
        _target = value;
        if (_animating)
            return; // the running switch picks the new value up
        if ((BeforeStateChangeAnimation is not null || AfterStateChangeAnimation is not null) && RenderState.HasCommitted && IsContentLoaded)
            _ = RunSwitches();
        else
            Switch();
    }

    private void Switch()
    {
        _shown = _target;
        RefreshContent();
    }

    // One animated switch at a time, each to the newest value: a value set during the "before" animation replaces the
    // pending one; one set during the "after" animation follows when it ends.
    private async Task RunSwitches()
    {
        _animating = true;
        try
        {
            while (_target != _shown)
            {
                await SkUiStateTransition.RunAsync(this, () => LoadedContent is SkUiView view ? [view] : [], Switch,
                    BeforeStateChangeAnimation, AfterStateChangeAnimation, CancellationToken.None);
            }
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"SkiaUi: {nameof(SkUiAlternateContentView)} could not switch to {(_target is { } target ? target ? "the alternate content" : "the content" : "nothing")}: {exception.Message}");
        }
        finally
        {
            _animating = false;
        }
    }
}
