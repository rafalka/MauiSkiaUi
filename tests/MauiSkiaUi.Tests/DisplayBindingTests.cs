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

    [Fact]
    public void ReadersAndPickersShareOneBinding()
    {
        // MAUI refuses a binding instance applied to two live targets; readers apply it only while reading.
        var binding = new Binding(nameof(Currency.Name));
        var first = new SkUiDisplayBinding(binding);
        var second = new SkUiDisplayBinding(binding);
        Assert.Equal(["Polish Zloty", "Euro"], first.GetTexts(Currencies));
        Assert.Equal("Euro", second.GetText(Currencies[1]));
        Assert.Equal("Polish Zloty", first.GetText(Currencies[0]));

        var picker = new SkUiPicker { ItemsSource = Currencies, ItemDisplayBinding = binding, SelectedItemDisplayBinding = binding };
        var other = new SkUiPicker { ItemsSource = Currencies, ItemDisplayBinding = binding, SelectedIndex = 1 };
        Assert.Equal(["Polish Zloty", "Euro"], picker.Items);
        Assert.Equal(["Polish Zloty", "Euro"], other.Items);

        // Back to the first binding while the reader made for it is still alive.
        var codes = new Binding(nameof(Currency.Code));
        picker.ItemDisplayBinding = codes;
        picker.ItemDisplayBinding = binding;
        Assert.Equal(["Polish Zloty", "Euro"], picker.Items);

        var column = new SkUiPickerColumn { ItemsSource = Currencies, ItemDisplayBinding = binding };
        Assert.Equal("Euro", column.Texts[1]);
        GC.KeepAlive(first);
        GC.KeepAlive(second);
    }

    [Fact]
    public void AReadThatThrowsDoesNotKeepTheItemAlive()
    {
        var reader = new SkUiDisplayBinding(new Binding(nameof(Currency.Name), converter: new ThrowingConverter()));
        var failed = ReadFailing(reader);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(failed.TryGetTarget(out _));
        Assert.Equal(["Polish Zloty", "Euro"], reader.GetTexts(Currencies)); // still reads

        // The pickers' ItemEnabledBinding probes recover the same way.
        var enabled = new Binding(nameof(Currency.Name), converter: new ThrowingConverter());
        var picker = new SkUiPicker { ItemsSource = new[] { Currencies[0], new Currency("XXX", ThrowingConverter.Failing) } };
        Assert.Throws<InvalidOperationException>(() => picker.ItemEnabledBinding = enabled);
        picker.ItemsSource = Currencies;
        Assert.Equal(["PLN", "EUR"], picker.Items);
    }

    /// <summary>Passes names through, throws for <see cref="ThrowingConverter.Failing"/>.</summary>
    private sealed class ThrowingConverter : IValueConverter
    {
        public const string Failing = "Fails";

        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
            value is Failing ? throw new InvalidOperationException(Failing) : value;

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
            throw new NotSupportedException();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<Currency> ReadFailing(SkUiDisplayBinding reader)
    {
        var item = new Currency("XXX", ThrowingConverter.Failing);
        Assert.Throws<InvalidOperationException>(() => reader.GetTexts(new[] { Currencies[0], item }));
        return new WeakReference<Currency>(item);
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
