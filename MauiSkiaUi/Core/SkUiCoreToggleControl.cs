namespace MauiSkiaUi.Core;

/// <summary>Shared boolean toggle state and tap-to-toggle behavior for Core Switch/CheckBox/RadioButton.</summary>
public abstract class SkUiCoreToggleControl : SkUiCoreNode
{
    private bool _isChecked;
    private long? _pressedPointer;
    private bool _isPressed;

    /// <summary>Whether the control is checked/toggled on.</summary>
    public bool IsChecked
    {
        get => _isChecked;
        set => SetIsChecked(value);
    }

    /// <summary>Whether an eligible pointer is currently pressed inside this control.</summary>
    public bool IsPressed => _isPressed;

    /// <summary>Raised whenever <see cref="IsChecked"/> changes, including from a tap.</summary>
    public event EventHandler<bool>? CheckedChanged;

    /// <summary>Sets checked state and raises <see cref="CheckedChanged"/> when it changes.</summary>
    public SkUiCoreToggleControl SetIsChecked(bool value)
    {
        if (!SetProperty(ref _isChecked, value, nameof(IsChecked))) return this;
        InvalidatePaint();
        CheckedChanged?.Invoke(this, value);
        return this;
    }

    /// <summary>Called on a completed tap; default toggles <see cref="IsChecked"/>.</summary>
    protected virtual void OnToggled() => SetIsChecked(!_isChecked);

    /// <inheritdoc />
    public override bool Touch(SkUiTouchEvent touch)
    {
        switch (touch.Action)
        {
            case SkUiTouchAction.Pressed:
                if (_pressedPointer is not null) return false;
                _pressedPointer = touch.Id;
                SetPressed(true);
                return true;
            case SkUiTouchAction.Moved:
                if (_pressedPointer != touch.Id) return false;
                if (!Contains(touch.Position))
                {
                    _pressedPointer = null;
                    SetPressed(false);
                }
                return true;
            case SkUiTouchAction.Released:
                if (_pressedPointer != touch.Id) return false;
                _pressedPointer = null;
                var wasPressed = _isPressed;
                SetPressed(false);
                if (wasPressed && Contains(touch.Position))
                    OnToggled();
                return true;
            case SkUiTouchAction.Cancelled:
                if (_pressedPointer != touch.Id) return false;
                _pressedPointer = null;
                SetPressed(false);
                return true;
            default:
                return false;
        }
    }

    private bool Contains(Point position) =>
        position.X >= 0 && position.Y >= 0 && position.X < Frame.Width && position.Y < Frame.Height;

    private void SetPressed(bool value)
    {
        if (!SetProperty(ref _isPressed, value, nameof(IsPressed))) return;
        InvalidatePaint();
    }
}
