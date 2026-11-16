namespace MauiSkiaUi;

/// <summary>
/// The popup request of a value picker (<see cref="SkUiValuePicker{T}"/>, <see cref="Core.SkUiCoreValuePicker{T}"/>): the value the
/// popup shows, changed as the user goes. For presenters that show their own UI instead of <see cref="SkUiPopupRequest.Content"/>
/// (a platform color dialog): read <see cref="Value"/>, call <see cref="SetValue"/> as the user changes it, or
/// <see cref="SkUiPopupRequest{TResult}.Complete"/> with the final value; <see cref="SkUiPopupRequest.Clear"/> chooses none.
/// </summary>
/// <typeparam name="T">The picker's value type.</typeparam>
public sealed class SkUiValuePopupRequest<T> : SkUiPopupRequest<T>
{
    private readonly Action<T> _apply;
    private readonly Action _accepted;
    private readonly Action _clear;

    internal SkUiValuePopupRequest(View anchor, View? platformAnchor, View? content, in SkUiPopupLayout layout, T value, Action<T> apply, Action accepted,
        Action clear)
        : base(anchor, platformAnchor, content, layout)
    {
        Value = value;
        _apply = apply;
        _accepted = accepted;
        _clear = clear;
    }

    /// <summary>
    /// What the popup shows: the picker's value when it opened, then each <see cref="SetValue"/>. With
    /// <see cref="SkUiPopupRequest.RequiresAccept"/> it is a draft that <see cref="SkUiPopupRequest.Accept"/> applies; otherwise it is
    /// the picker's value.
    /// </summary>
    public T Value { get; private set; }

    /// <summary>Raised when <see cref="Value"/> changed: <see cref="SetValue"/>, the Clear button, or the app changing the picker's value while the popup is open.</summary>
    public event EventHandler? ValueChanged;

    /// <summary>
    /// The user changed the value: applied to the picker at once, or kept as the draft OK applies (with
    /// <see cref="SkUiPopupRequest.RequiresAccept"/>). Returns <c>false</c> when the popup is closing (nothing changes then).
    /// </summary>
    public bool SetValue(T value)
    {
        if (IsCompleted || CloseRequested.IsCancellationRequested)
            return false;
        Show(value);
        if (!RequiresAccept)
            _apply(value);
        return true;
    }

    /// <summary>The picker's value changed while open (the app, or a choice applied): the popup shows it.</summary>
    internal void Follow(T value) => Show(value);

    private void Show(T value)
    {
        if (EqualityComparer<T>.Default.Equals(Value, value))
            return;
        Value = value;
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private protected override void OnCompleting(T result)
    {
        Show(result);
        _apply(result);
    }

    private protected override bool AcceptCore()
    {
        _accepted();
        if (Complete(Value))
            return true;
        Close();
        return false;
    }

    private protected override bool ClearCore()
    {
        _clear();
        Close();
        return true;
    }

    private protected override void OnCompleted() => ValueChanged = null;
}

/// <summary>
/// The content of a value picker's popup (<see cref="SkUiValuePicker{T}.CreateValueContent"/>): a drawn body that shows
/// <see cref="Value"/> and changes it with <see cref="SetValue"/> (applied at once, or a draft that OK applies with
/// <c>UpdateMode</c> <see cref="SkUiPickerUpdateMode.WhenFinished"/>) or ends with <see cref="Complete"/>. SkiaUi shows it in its popup
/// chrome; <see cref="OnValueChanged"/> tells the body to follow (the Clear button, the app's change).
/// </summary>
/// <typeparam name="T">The picker's value type.</typeparam>
public abstract class SkUiValuePopupContent<T> : SkUiPickerPopupContent
{
    private Func<T>? _current;
    private Action<T>? _apply;
    private T _value = default!;
    private SkUiValuePopupRequest<T>? _request;

    /// <summary>What the popup shows: the picker's value when it opened, then the user's changes (a draft until OK, when the popup waits for it).</summary>
    public T Value => _request is { } request ? request.Value : _value;

    /// <summary>
    /// The user changed the value: applied to the picker at once, or kept for OK (with <see cref="SkUiPickerPopupContext.RequiresAccept"/>).
    /// The popup stays open; <see cref="OnValueChanged"/> runs.
    /// </summary>
    protected void SetValue(T value)
    {
        if (_request is { } request)
            request.SetValue(value);
    }

    /// <summary>The user chose <paramref name="value"/> and the popup ends: applied (also when it waits for OK, as if OK was pressed), then it closes.</summary>
    protected void Complete(T value) => _request?.Complete(value);

    /// <summary><see cref="Value"/> changed (the user, the Clear button, or the app changing the picker's value while open): the body follows.</summary>
    protected virtual void OnValueChanged()
    {
    }

    /// <inheritdoc />
    /// <remarks>The picker's value is followed (<see cref="Value"/>, <see cref="OnValueChanged"/>); call the base when overriding.</remarks>
    protected override void OnPickerChanged()
    {
        if (_request is { } request && _current is { } current)
            request.Follow(current());
    }

    internal void Bind(Func<T> current, Action<T> apply)
    {
        _current = current;
        _apply = apply;
        _value = current();
    }

    internal override SkUiPopupRequest CreateRequest(View anchor, View? platformAnchor, View surface, in SkUiPopupLayout layout, in SkUiPopupRequestOptions options,
        Action accept, Action clear)
    {
        var request = new SkUiValuePopupRequest<T>(anchor, platformAnchor, surface, layout, _current is { } current ? current() : _value,
            value => _apply?.Invoke(value), accept, clear)
        {
            Title = options.Title,
            OverlayColor = options.OverlayColor,
            IsAnimated = options.IsAnimated,
            RequiresAccept = options.Chrome.RequiresAccept,
            AcceptText = options.Chrome.AcceptText,
            CancelText = options.Chrome.CancelText
        };
        request.ValueChanged += (_, _) => OnValueChanged();
        _request = request;
        return request;
    }

    /// <inheritdoc />
    /// <remarks>The Clear button cleared the picker: <see cref="Value"/> follows it; call the base when overriding.</remarks>
    protected override void OnCleared()
    {
        if (_request is { } request && _current is { } current)
            request.Follow(current());
    }

    /// <inheritdoc />
    /// <remarks>Lets go of the request and the picker; call the base when overriding.</remarks>
    protected override void OnClosed()
    {
        if (_request is { } request)
            _value = request.Value;
        _request = null;
        _current = null;
        _apply = null;
    }
}
