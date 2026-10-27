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
/// The binding is applied to a private bindable object whose binding context is set to each item in turn, and only while
/// the texts are read. Once they are read, the binding is removed and the context cleared: neither this object nor the
/// binding keeps the last item alive, and readers (several pickers, your own controls) can share one binding, such as one
/// in app resources, as long as it is not also applied to a view.
/// </para>
/// <para>
/// Texts are read when you ask for them; a change of an item's property is not followed. Read them again when the items
/// change. Use it only on the UI thread (it is not thread-safe).
/// </para>
/// </remarks>
public sealed class SkUiDisplayBinding
{
    /// <summary>Reads each item's <c>ToString()</c>; no state, so shared.</summary>
    internal static readonly SkUiDisplayBinding ItemText = new(null);

    // Created by the first read through the binding, and again after a read that threw.
    private SkUiItemBindingProbe? _probe;

    /// <summary>Creates a reader for <paramref name="binding"/>; <c>null</c> reads each item's <c>ToString()</c>.</summary>
    public SkUiDisplayBinding(BindingBase? binding) => Binding = binding;

    /// <summary>The binding the texts are read through (<c>null</c>: <c>ToString()</c>).</summary>
    public BindingBase? Binding { get; }

    /// <summary>The text of <paramref name="item"/>: the binding's value, else its <c>ToString()</c>; empty for <c>null</c>.</summary>
    public string GetText(object? item)
    {
        try
        {
            return ReadOne(item);
        }
        catch
        {
            SkUiItemBindingProbe.Discard(ref _probe);
            throw;
        }
        finally
        {
            _probe?.Release();
        }
    }

    /// <summary>The texts of <paramref name="items"/> in their order (see <see cref="GetText"/>); empty for <c>null</c>.</summary>
    public string[] GetTexts(IEnumerable? items)
    {
        try
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
                    return texts;
                }
                default:
                {
                    var texts = new List<string>();
                    foreach (var item in items)
                        texts.Add(ReadOne(item));
                    return texts.ToArray();
                }
            }
        }
        catch
        {
            SkUiItemBindingProbe.Discard(ref _probe);
            throw;
        }
        finally
        {
            _probe?.Release();
        }
    }

    private string ReadOne(object? item) =>
        (Binding is null ? item?.ToString() : (string?)(_probe ??= new SkUiItemBindingProbe(Binding, SkUiItemBindingProbe.TextProperty)).Read(item))
        ?? string.Empty;
}

/// <summary>
/// A bindable object that evaluates one binding for one item at a time (its binding context): the display texts of
/// <see cref="SkUiDisplayBinding"/> and the pickers' <c>ItemEnabledBinding</c>. The binding is applied by the first
/// <see cref="Read"/> and removed by <see cref="Release"/>, which the owner calls (in a <c>finally</c>) once it has read what
/// it needs: the probe does not keep the last item alive, and a binding is not left applied to it, so several probes (and
/// MAUI controls that apply a binding only while reading, as <c>Picker</c> does) can share one binding instance, which
/// MAUI otherwise refuses ("Binding instances cannot be reused"). After a read that threw, the owner calls
/// <see cref="Discard"/> instead.
/// </summary>
internal sealed class SkUiItemBindingProbe : BindableObject
{
    /// <summary>A text, converted by the binding as MAUI converts to a <c>string</c> property.</summary>
    internal static readonly BindableProperty TextProperty = BindableProperty.Create("Text", typeof(string), typeof(SkUiItemBindingProbe), null);

    /// <summary>A flag that is <c>true</c> unless the binding gives <c>false</c>.</summary>
    internal static readonly BindableProperty FlagProperty = BindableProperty.Create("Flag", typeof(bool), typeof(SkUiItemBindingProbe), true);

    private readonly BindingBase _binding;
    private readonly BindableProperty _property;
    private bool _applied;

    internal SkUiItemBindingProbe(BindingBase binding, BindableProperty property)
    {
        _binding = binding;
        _property = property;
    }

    /// <summary>The binding's value for <paramref name="item"/>; the binding stays applied until <see cref="Release"/>.</summary>
    internal object? Read(object? item)
    {
        BindingContext = item;
        if (!_applied)
        {
            // Applied with the item as context: one evaluation for the first item.
            SetBinding(_property, _binding);
            _applied = true;
        }
        return GetValue(_property);
    }

    /// <summary>Whether <see cref="FlagProperty"/> is not <c>false</c> for <paramref name="item"/>.</summary>
    internal bool IsSet(object? item) => Read(item) is not false;

    /// <summary>Removes the binding (it no longer targets this probe) and clears the item read last.</summary>
    internal void Release()
    {
        if (_applied)
        {
            _applied = false;
            RemoveBinding(_property);
        }
        BindingContext = null;
    }

    /// <summary>
    /// Releases <paramref name="probe"/> after a read that threw and drops it; the owner creates a new one for the next read.
    /// MAUI does not clear its re-entrancy flag when a binding context change throws (a converter or an item's getter), so
    /// later contexts would never reach this probe: it would keep the item and go on reading it.
    /// </summary>
    internal static void Discard(ref SkUiItemBindingProbe? probe)
    {
        probe?.Release();
        probe = null;
    }
}
