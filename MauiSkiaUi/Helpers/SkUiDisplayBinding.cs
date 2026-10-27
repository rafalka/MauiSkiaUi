using System.Collections;

namespace MauiSkiaUi;

/// <summary>
/// Reads the text an item shows through a binding, as MAUI's <c>Picker.ItemDisplayBinding</c> does: <c>{Binding Name}</c>
/// gives each item's <c>Name</c>, without a binding the item's <c>ToString()</c>. The drawn pickers read their item texts with
/// it; use it in your own controls that show items as text (chips, tags, a custom list) instead of evaluating the binding
/// yourself.
/// </summary>
/// <remarks>
/// <para>
/// The binding is applied to a private bindable object whose binding context is set to each item in turn. Once the texts
/// are read, the context is cleared: neither this object nor the binding (which may live in app resources) keeps the last
/// item alive.
/// </para>
/// <para>
/// Texts are read when you ask for them; a change of an item's property is not followed. Read them again when the items
/// change. Use one instance per binding and only on the UI thread (it is not thread-safe).
/// </para>
/// </remarks>
public sealed class SkUiDisplayBinding
{
    /// <summary>Reads each item's <c>ToString()</c>; no state, so shared.</summary>
    internal static readonly SkUiDisplayBinding ItemText = new(null);

    private readonly SkUiItemBindingProbe? _probe;

    /// <summary>Creates a reader for <paramref name="binding"/>; <c>null</c> reads each item's <c>ToString()</c>.</summary>
    public SkUiDisplayBinding(BindingBase? binding)
    {
        Binding = binding;
        if (binding is not null)
            _probe = new SkUiItemBindingProbe(binding, SkUiItemBindingProbe.TextProperty);
    }

    /// <summary>The binding the texts are read through (<c>null</c>: <c>ToString()</c>).</summary>
    public BindingBase? Binding { get; }

    /// <summary>The text of <paramref name="item"/>: the binding's value, else its <c>ToString()</c>; empty for <c>null</c>.</summary>
    public string GetText(object? item)
    {
        var text = ReadOne(item);
        _probe?.Release();
        return text;
    }

    /// <summary>The texts of <paramref name="items"/> in their order (see <see cref="GetText"/>); empty for <c>null</c>.</summary>
    public string[] GetTexts(IEnumerable? items)
    {
        switch (items)
        {
            case null:
                return [];
            case IList list:
            {
                var texts = new string[list.Count];
                for (var index = 0; index < texts.Length; index++)
                    texts[index] = ReadOne(list[index]);
                _probe?.Release();
                return texts;
            }
            default:
            {
                var texts = new List<string>();
                foreach (var item in items)
                    texts.Add(ReadOne(item));
                _probe?.Release();
                return texts.ToArray();
            }
        }
    }

    /// <summary>Stops the binding updating the probe, when the owner drops this reader for another binding.</summary>
    internal void Detach() => _probe?.Detach();

    private string ReadOne(object? item) => (_probe is null ? item?.ToString() : (string?)_probe.Read(item)) ?? string.Empty;
}

/// <summary>
/// A bindable object that evaluates one binding for one item at a time (its binding context): the display texts of
/// <see cref="SkUiDisplayBinding"/> and the pickers' <c>ItemEnabledBinding</c>. The owner calls <see cref="Release"/> once it
/// has read what it needs, so the probe does not keep the last item alive.
/// </summary>
internal sealed class SkUiItemBindingProbe : BindableObject
{
    /// <summary>A text, converted by the binding as MAUI converts to a <c>string</c> property.</summary>
    internal static readonly BindableProperty TextProperty = BindableProperty.Create("Text", typeof(string), typeof(SkUiItemBindingProbe), null);

    /// <summary>A flag that is <c>true</c> unless the binding gives <c>false</c>.</summary>
    internal static readonly BindableProperty FlagProperty = BindableProperty.Create("Flag", typeof(bool), typeof(SkUiItemBindingProbe), true);

    private readonly BindableProperty _property;

    internal SkUiItemBindingProbe(BindingBase binding, BindableProperty property)
    {
        _property = property;
        SetBinding(property, binding);
    }

    /// <summary>The binding's value for <paramref name="item"/>; the item stays the context until <see cref="Release"/>.</summary>
    internal object? Read(object? item)
    {
        BindingContext = item;
        return GetValue(_property);
    }

    /// <summary>Whether <see cref="FlagProperty"/> is not <c>false</c> for <paramref name="item"/>.</summary>
    internal bool IsSet(object? item) => Read(item) is not false;

    /// <summary>Clears the item read last.</summary>
    internal void Release() => BindingContext = null;

    /// <summary>Removes the binding (its expression no longer targets this probe).</summary>
    internal void Detach() => RemoveBinding(_property);
}
