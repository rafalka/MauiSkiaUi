using System.Windows.Input;

namespace MauiSkiaUi;

public partial class SkUiCollectionView
{
    private SkUiLoadMoreMode _loadMoreMode;
    private bool _userScrolled;
    private bool _runningLoadMore;
    private bool _recheckLoadMore;
    private SkUiWeakListener<SkUiCollectionView>? _loadMoreCommandListener;
    private static Func<string> _loadMoreText = () => "Load more";

    /// <summary>Bindable property for <see cref="LoadMoreMode"/>.</summary>
    public static readonly BindableProperty LoadMoreModeProperty = BindableProperty.Create(nameof(LoadMoreMode), typeof(SkUiLoadMoreMode), typeof(SkUiCollectionView), SkUiLoadMoreMode.None,
        validateValue: (_, value) => Enum.IsDefined((SkUiLoadMoreMode)value),
        propertyChanged: (view, _, value) => ((SkUiCollectionView)view).OnLoadMoreModeChanged((SkUiLoadMoreMode)value));

    /// <summary>Bindable property for <see cref="LoadMorePosition"/>.</summary>
    public static readonly BindableProperty LoadMorePositionProperty = BindableProperty.Create(nameof(LoadMorePosition), typeof(SkUiLoadMorePosition), typeof(SkUiCollectionView), SkUiLoadMorePosition.End,
        validateValue: (_, value) => Enum.IsDefined((SkUiLoadMorePosition)value),
        propertyChanged: (view, _, _) => ((SkUiCollectionView)view).UpdateLoadMoreRow());

    /// <summary>Bindable property for <see cref="LoadMoreCommand"/>.</summary>
    public static readonly BindableProperty LoadMoreCommandProperty = BindableProperty.Create(nameof(LoadMoreCommand), typeof(ICommand), typeof(SkUiCollectionView), null,
        propertyChanged: (view, _, value) => ((SkUiCollectionView)view).OnLoadMoreCommandChanged((ICommand?)value));

    /// <summary>Bindable property for <see cref="LoadMoreCommandParameter"/>.</summary>
    public static readonly BindableProperty LoadMoreCommandParameterProperty = BindableProperty.Create(nameof(LoadMoreCommandParameter), typeof(object), typeof(SkUiCollectionView), null,
        propertyChanged: (view, _, _) => ((SkUiCollectionView)view).OnCanLoadMoreChanged());

    /// <summary>Bindable property for <see cref="LoadMoreTemplate"/>.</summary>
    public static readonly BindableProperty LoadMoreTemplateProperty = BindableProperty.Create(nameof(LoadMoreTemplate), typeof(DataTemplate), typeof(SkUiCollectionView), null,
        propertyChanged: (view, _, _) => ((SkUiCollectionView)view).OnLoadMoreTemplateChanged());

    /// <summary>Bindable property for <see cref="IsLoadMoreActive"/> (two-way by default: the list sets it when it loads more).</summary>
    public static readonly BindableProperty IsLoadMoreActiveProperty = BindableProperty.Create(nameof(IsLoadMoreActive), typeof(bool), typeof(SkUiCollectionView), false,
        BindingMode.TwoWay,
        propertyChanged: (view, _, value) => ((SkUiCollectionView)view).OnIsLoadMoreActiveChanged((bool)value));

    /// <summary>
    /// When the list asks for more items (<see cref="LoadingMore"/>, <see cref="LoadMoreCommand"/>):
    /// <see cref="SkUiLoadMoreMode.None"/> (default), <see cref="SkUiLoadMoreMode.Manual"/> (a load-more row with a button at
    /// <see cref="LoadMorePosition"/>), <see cref="SkUiLoadMoreMode.Auto"/> (when the scroll reaches that end, also when the
    /// items do not fill the list) or <see cref="SkUiLoadMoreMode.AutoOnUserScroll"/> (as Auto, once the user has scrolled).
    /// <see cref="RemainingItemsThreshold"/> works independently (to prefetch a page before the end shows).
    /// </summary>
    public SkUiLoadMoreMode LoadMoreMode { get => (SkUiLoadMoreMode)GetValue(LoadMoreModeProperty); set => SetValue(LoadMoreModeProperty, value); }

    /// <summary>
    /// Where more items load: <see cref="SkUiLoadMorePosition.End"/> (default) or <see cref="SkUiLoadMorePosition.Start"/>
    /// (older messages of a chat): there, items inserted at the start keep what shows in place.
    /// </summary>
    public SkUiLoadMorePosition LoadMorePosition { get => (SkUiLoadMorePosition)GetValue(LoadMorePositionProperty); set => SetValue(LoadMorePositionProperty, value); }

    /// <summary>
    /// Loads more items: runs with <see cref="LoadMoreCommandParameter"/> after <see cref="IsLoadMoreActive"/> became <c>true</c>;
    /// set <see cref="IsLoadMoreActive"/> back to <c>false</c> when the items are added. While it cannot execute there is
    /// nothing more to load: no load-more row, no automatic loading.
    /// </summary>
    public ICommand? LoadMoreCommand { get => (ICommand?)GetValue(LoadMoreCommandProperty); set => SetValue(LoadMoreCommandProperty, value); }

    /// <summary>The parameter of <see cref="LoadMoreCommand"/>.</summary>
    public object? LoadMoreCommandParameter { get => GetValue(LoadMoreCommandParameterProperty); set => SetValue(LoadMoreCommandParameterProperty, value); }

    /// <summary>
    /// Creates the load-more row (drawn views; the list's binding context): shown by <see cref="SkUiLoadMoreMode.Manual"/>
    /// while more can load (a tap on it loads; views in it keep their taps), and by every mode while loading. Default: a
    /// "Load more" button (<see cref="LoadMoreText"/>) and a spinner while loading.
    /// </summary>
    public DataTemplate? LoadMoreTemplate { get => (DataTemplate?)GetValue(LoadMoreTemplateProperty); set => SetValue(LoadMoreTemplateProperty, value); }

    /// <summary>
    /// Whether more items are loading: the list sets it before <see cref="LoadingMore"/> and <see cref="LoadMoreCommand"/>, and
    /// does not ask again until the app sets it back to <c>false</c>. The load-more row shows its loading state meanwhile.
    /// </summary>
    public bool IsLoadMoreActive { get => (bool)GetValue(IsLoadMoreActiveProperty); set => SetValue(IsLoadMoreActiveProperty, value); }

    /// <summary>Raised when the list asks for more items, before <see cref="LoadMoreCommand"/>.</summary>
    public event EventHandler? LoadingMore;

    /// <summary>The text of the default load-more button ("Load more"). Set it once at startup to localize.</summary>
    public static Func<string> LoadMoreText
    {
        get => _loadMoreText;
        set => _loadMoreText = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>The load-more row, once created (tests).</summary>
    internal SkUiView? LoadMoreRow => _body.LoadMoreHost;

    /// <summary>Whether more can load: the command can execute (without a command, when someone listens to <see cref="LoadingMore"/>).</summary>
    private bool CanLoadMore => LoadMoreCommand is { } command ? command.CanExecute(LoadMoreCommandParameter) : LoadingMore is not null;

    /// <summary>The load-more row takes taps: manual mode, not loading.</summary>
    private bool LoadMoreTappable => _loadMoreMode == SkUiLoadMoreMode.Manual && !IsLoadMoreActive;

    /// <summary>Asks for more items now (the load-more row's tap, or the end reached): <see cref="IsLoadMoreActive"/>, the event, the command.</summary>
    private void RunLoadMore()
    {
        if (_loadMoreMode == SkUiLoadMoreMode.None || IsLoadMoreActive || !CanLoadMore || _runningLoadMore)
            return;
        _runningLoadMore = true;
        try
        {
            IsLoadMoreActive = true;
            LoadingMore?.Invoke(this, EventArgs.Empty);
            if (LoadMoreCommand is { } command && command.CanExecute(LoadMoreCommandParameter))
                command.Execute(LoadMoreCommandParameter);
        }
        finally
        {
            _runningLoadMore = false;
        }
    }

    /// <summary>Automatic modes: asks for more when the load-more end of the rows shows (or there are none).</summary>
    private void CheckLoadMore()
    {
        if (_loadMoreMode is not (SkUiLoadMoreMode.Auto or SkUiLoadMoreMode.AutoOnUserScroll) || IsLoadMoreActive || _runningLoadMore)
            return;
        if (_loadMoreMode == SkUiLoadMoreMode.AutoOnUserScroll && !_userScrolled)
            return;
        var rows = _model.RowCount;
        var atEnd = rows == 0
            || (LoadMorePosition == SkUiLoadMorePosition.End ? _items.LastVisibleIndex == rows - 1 : _items.FirstVisibleIndex == 0);
        if (atEnd && CanLoadMore)
            RunLoadMore();
    }

    private void OnLoadMoreModeChanged(SkUiLoadMoreMode value)
    {
        _loadMoreMode = value;
        UpdateLoadMoreRow();
        CheckLoadMore();
    }

    private void OnLoadMoreCommandChanged(ICommand? value)
    {
        _loadMoreCommandListener ??= new SkUiWeakListener<SkUiCollectionView>(this, static (view, change) =>
        {
            if (change.Kind == SkUiChangeKind.CanExecute)
                view.OnCanLoadMoreChanged();
        });
        _loadMoreCommandListener.Listen(value);
        OnCanLoadMoreChanged();
    }

    /// <summary>The command, its parameter or its <c>CanExecute</c> changed: the row, and an automatic load if the end shows.</summary>
    private void OnCanLoadMoreChanged()
    {
        UpdateLoadMoreRow();
        CheckLoadMore();
    }

    private void OnLoadMoreTemplateChanged()
    {
        if (_body.LoadMoreHost is { } host)
            host.Content = null;
        UpdateLoadMoreRow();
    }

    private void OnIsLoadMoreActiveChanged(bool value)
    {
        UpdateLoadMoreRow();
        // Done: once the new items are laid out, the end may still show (a short page): ask again then.
        if (!value)
            _recheckLoadMore = true;
    }

    /// <summary>After a layout: a check left for when the items loaded have been laid out.</summary>
    private void RecheckLoadMore()
    {
        if (!_recheckLoadMore)
            return;
        _recheckLoadMore = false;
        CheckLoadMore();
    }

    /// <summary>Shows, places and updates the load-more row (created the first time it shows).</summary>
    private void UpdateLoadMoreRow()
    {
        var active = IsLoadMoreActive;
        var visible = _loadMoreMode != SkUiLoadMoreMode.None && (active || (_loadMoreMode == SkUiLoadMoreMode.Manual && CanLoadMore));
        _items.AnchorsAtStart = _loadMoreMode != SkUiLoadMoreMode.None && LoadMorePosition == SkUiLoadMorePosition.Start;
        if (!visible && _body.LoadMoreHost is null)
            return;
        var host = _body.LoadMoreHost ??= _body.Adopt(new LoadMoreHost(this));
        if (host.Content is null)
            host.Content = LoadMoreTemplate is { } template ? CreateFromTemplate(template, nameof(LoadMoreTemplate)) : new LoadMoreDefaultView(this);
        (host.Content as LoadMoreDefaultView)?.Show(manual: _loadMoreMode == SkUiLoadMoreMode.Manual, active);
        _body.LoadMoreAtStart = LoadMorePosition == SkUiLoadMorePosition.Start;
        if (host.IsVisible != visible)
            host.IsVisible = visible;
        _body.InvalidateBody();
    }
}
