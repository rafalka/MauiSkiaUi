namespace MauiSkiaUi.Core;

/// <summary>
/// A typed value that a Core layout (the owner) stores on its children, such as a grid row or a shrink factor: the
/// Core counterpart of a MAUI attached property, without bindings or styles. Read and write it with
/// <see cref="SkUiCoreNode.GetValue{T}"/> / <see cref="SkUiCoreNode.SetValue{T}"/>. The value lives on the child,
/// so it can be set before the child is added and stays with it when it moves to another layout.
/// </summary>
/// <typeparam name="T">Value type.</typeparam>
public sealed class SkUiCoreAttachedProperty<T>
{
    private readonly Func<T, bool>? _validate;

    /// <summary>Creates a property.</summary>
    /// <param name="name">Name raised in <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/> when the value changes.</param>
    /// <param name="ownerType">The layout type that reads the value.</param>
    /// <param name="defaultValue">Value of nodes that never set it.</param>
    /// <param name="affectsParentMeasure">Whether a change re-measures the node's parent (layout placement data).</param>
    /// <param name="validate">Returns <c>false</c> for values <see cref="SkUiCoreNode.SetValue{T}"/> must reject.</param>
    public SkUiCoreAttachedProperty(string name, Type ownerType, T defaultValue = default!, bool affectsParentMeasure = true, Func<T, bool>? validate = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(ownerType);
        if (validate is not null && !validate(defaultValue))
            throw new ArgumentException("The default value is not valid.", nameof(defaultValue));
        Name = name;
        OwnerType = ownerType;
        DefaultValue = defaultValue;
        AffectsParentMeasure = affectsParentMeasure;
        _validate = validate;
    }

    /// <summary>Property name, as raised in property-changed notifications.</summary>
    public string Name { get; }

    /// <summary>The layout type that reads the value.</summary>
    public Type OwnerType { get; }

    /// <summary>Value of nodes that never set it.</summary>
    public T DefaultValue { get; }

    /// <summary>Whether a change re-measures the node's parent.</summary>
    public bool AffectsParentMeasure { get; }

    internal void Validate(T value)
    {
        if (_validate is not null && !_validate(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, $"Invalid value for {OwnerType.Name}.{Name}.");
    }

    /// <inheritdoc />
    public override string ToString() => $"{OwnerType.Name}.{Name}";
}
