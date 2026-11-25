using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

public partial class SkUiCarouselView : ISkUiAutoPlayHost
{
    /// <summary>Bindable property for <see cref="AutoPlayInterval"/>.</summary>
    public static readonly BindableProperty AutoPlayIntervalProperty = BindableProperty.Create(nameof(AutoPlayInterval), typeof(TimeSpan), typeof(SkUiCarouselView),
        TimeSpan.Zero, validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiCarouselView)view).OnAutoPlayIntervalChanged((TimeSpan)value));

    /// <summary>Bindable property for <see cref="IsAutoPlayPaused"/>.</summary>
    public static readonly BindableProperty IsAutoPlayPausedProperty = BindableProperty.Create(nameof(IsAutoPlayPaused), typeof(bool), typeof(SkUiCarouselView), false,
        propertyChanged: (view, _, value) => ((SkUiCarouselView)view).OnAutoPlayPausedChanged((bool)value));

    private SkUiAutoPlay? _autoPlay;

    /// <summary>
    /// When positive, the carousel moves by itself (SkiaUi extension; default <see cref="TimeSpan.Zero"/>: off): each interval
    /// it steps one snap point forward (a page with <see cref="ItemsPerSnapPoint"/>), animated, and at the end of a carousel
    /// that does not loop it goes back to the first item. It waits while the user drags, flings or scrolls it, and a full
    /// interval after it comes to rest; while a mouse or pen hovers it; while keyboard focus is inside it; while it is not
    /// shown (<see cref="SkUiView.IsShown"/>; shown again, it waits a full interval) or the app is in the background; and
    /// while a screen reader runs. With reduce motion (<see cref="SkUiMotion"/>) the steps jump without the slide.
    /// <see cref="IsAutoPlayPaused"/> is the app's pause (a pause button, as WCAG 2.2.2 asks of content that moves by itself).
    /// </summary>
    public TimeSpan AutoPlayInterval { get => (TimeSpan)GetValue(AutoPlayIntervalProperty); set => SetValue(AutoPlayIntervalProperty, value); }

    /// <summary>Whether autoplay is paused by the app (default <c>false</c>); unpaused, the next step comes a full <see cref="AutoPlayInterval"/> later.</summary>
    public bool IsAutoPlayPaused { get => (bool)GetValue(IsAutoPlayPausedProperty); set => SetValue(IsAutoPlayPausedProperty, value); }

    /// <summary>Sets <see cref="AutoPlayInterval"/> (same as the property setter).</summary>
    public SkUiCarouselView SetAutoPlayInterval(TimeSpan value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);
        AutoPlayInterval = value;
        return this;
    }

    /// <summary>Sets <see cref="IsAutoPlayPaused"/> (same as the property setter).</summary>
    public SkUiCarouselView SetIsAutoPlayPaused(bool value) { IsAutoPlayPaused = value; return this; }

    /// <summary>Whether an autoplay step is scheduled (tests).</summary>
    internal bool IsAutoPlayScheduled => _autoPlay?.IsScheduled == true;

    private void OnAutoPlayIntervalChanged(TimeSpan value)
    {
        var autoPlay = _autoPlay ??= new SkUiAutoPlay(this) { IsPaused = IsAutoPlayPaused };
        var watching = autoPlay.Interval > TimeSpan.Zero;
        autoPlay.Interval = value;
        // Whether it is shown is followed only while autoplay is on.
        if (value > TimeSpan.Zero && !watching)
        {
            IsShownChanged += OnAutoPlayShownChanged;
            autoPlay.IsShown = IsShown;
        }
        else if (value <= TimeSpan.Zero && watching)
            IsShownChanged -= OnAutoPlayShownChanged;
        autoPlay.Restart();
    }

    private void OnAutoPlayPausedChanged(bool value)
    {
        if (_autoPlay is not { } autoPlay)
            return;
        autoPlay.IsPaused = value;
        autoPlay.Restart();
    }

    private void OnAutoPlayShownChanged(object? sender, EventArgs e)
    {
        if (_autoPlay is not { } autoPlay)
            return;
        autoPlay.IsShown = IsShown;
        autoPlay.Restart();
    }

    /// <summary>The carousel came to rest: the next step waits a full interval.</summary>
    private void RestartAutoPlay() => _autoPlay?.Restart();

    bool ISkUiAutoPlayHost.IsAutoPlayHeld =>
        PageCount <= 1 || Controller.IsMoving || Controller.Dragging || IsPointerOver || Scroller.IsPointerOver 
        || SkUiSemantics.FocusedInside(this) is not null || SkUiSemantics.IsScrolledOut(this);

    void ISkUiAutoPlayHost.AutoPlayStep(bool animated)
    {
        // The next page in the reading direction; past the last page the first (a loop takes it as the next one).
        var next = _position + _perPage;
        StepTo(next > PageStart(ItemCount - 1) ? 0 : next, forward: true, animated);
    }
}
