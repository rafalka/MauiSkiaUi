using System.Runtime.CompilerServices;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// <see cref="SkUiDisplayBinding"/> (#66): an item's text through a binding (MAUI's <c>Picker.ItemDisplayBinding</c> rule),
/// <c>ToString()</c> without one, and the last item read is not kept alive by the reader or the binding.
/// </summary>
public class DisplayBindingTests
{
    private sealed record Currency(string Code, string Name)
    {
        public override string ToString() => Code;
    }

    private static readonly Currency[] Currencies = [new("PLN", "Polish Zloty"), new("EUR", "Euro")];

    [Fact]
    public void ReadsTextsThroughTheBindingOrToString()
    {
        var names = new SkUiDisplayBinding(new Binding(nameof(Currency.Name)));
        Assert.Equal("Polish Zloty", names.GetText(Currencies[0]));
        Assert.Equal(["Polish Zloty", "Euro"], names.GetTexts(Currencies));
        Assert.Equal(["Polish Zloty", "Euro"], names.GetTexts(Currencies.Select(currency => currency))); // not a list
        Assert.Equal(string.Empty, names.GetText(null));
        Assert.Empty(names.GetTexts(null));

        var formatted = new SkUiDisplayBinding(new Binding(nameof(Currency.Code), stringFormat: "[{0}]"));
        Assert.Equal("[EUR]", formatted.GetText(Currencies[1]));
        var numbers = new SkUiDisplayBinding(new Binding("."));
        Assert.Equal("42", numbers.GetText(42)); // converted to text as MAUI converts to a string property

        var plain = new SkUiDisplayBinding(null);
        Assert.Null(plain.Binding);
        Assert.Equal(["PLN", "EUR"], plain.GetTexts(Currencies));
        Assert.Equal(string.Empty, plain.GetText(null));
    }

    [Fact]
    public void TheLastItemReadIsNotKeptAlive()
    {
        // One binding, as one from app resources, read by a long-lived reader.
        var binding = new Binding(nameof(Currency.Name));
        var reader = new SkUiDisplayBinding(binding);
        var one = Read(reader, list: true);
        var many = Read(reader, list: false);
        var single = ReadOne(reader);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(one.TryGetTarget(out _));
        Assert.False(many.TryGetTarget(out _));
        Assert.False(single.TryGetTarget(out _));
        GC.KeepAlive(reader);
        GC.KeepAlive(binding);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<Currency> Read(SkUiDisplayBinding reader, bool list)
    {
        var last = new Currency("USD", "US Dollar");
        Currency[] items = [new("PLN", "Polish Zloty"), last];
        Assert.Equal("US Dollar", (list ? reader.GetTexts(items) : reader.GetTexts(items.Select(item => item)))[^1]);
        return new WeakReference<Currency>(last);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<Currency> ReadOne(SkUiDisplayBinding reader)
    {
        var item = new Currency("CHF", "Swiss Franc");
        Assert.Equal("Swiss Franc", reader.GetText(item));
        return new WeakReference<Currency>(item);
    }
}
