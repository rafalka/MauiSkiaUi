using System.ComponentModel;
using System.Runtime.CompilerServices;
using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// The typed steppers (<see cref="SkUiStepper{T}"/>, <see cref="SkUiCoreStepper{T}"/>): exact decimal and integer steps, steps that stop
/// at the type's limits, MAUI's rounding for binary floating point, validation, MAUI's coercion, typed events, screen readers.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public class TypedStepperTests
{
    private static bool Increment(SkUiView stepper) => ((ISkUiAccessibleNode)stepper).PerformSemanticsAction(SkUiSemanticsActions.Increment);

    private static bool Decrement(SkUiView stepper) => ((ISkUiAccessibleNode)stepper).PerformSemanticsAction(SkUiSemanticsActions.Decrement);

    [Fact]
    public void DecimalStepsAreExact()
    {
        var stepper = new SkUiStepper<decimal> { Increment = 0.1m, Maximum = 0.3m };
        Assert.True(Increment(stepper));
        Assert.True(Increment(stepper));
        Assert.True(Increment(stepper));
        Assert.Equal(0.3m, stepper.Value);
        Assert.False(Increment(stepper));
        // Decimals keep every digit: no rounding by the increment.
        stepper.Value = 0.123456789m;
        Assert.Equal(0.123456789m, stepper.Value);
    }

    [Fact]
    public void StepsStopAtTheTypesLimits()
    {
        // A uint below its increment goes to the minimum instead of wrapping to 4294967295.
        var unsigned = new SkUiStepper<uint> { Increment = 5, Value = 3 };
        Assert.True(Decrement(unsigned));
        Assert.Equal(0u, unsigned.Value);
        Assert.False(Decrement(unsigned));

        // The whole int range: no overflow at either end.
        var whole = new SkUiStepper<int> { Minimum = int.MinValue, Maximum = int.MaxValue, Increment = 10, Value = int.MaxValue - 3 };
        Assert.True(Increment(whole));
        Assert.Equal(int.MaxValue, whole.Value);
        whole.Value = int.MinValue + 3;
        Assert.True(Decrement(whole));
        Assert.Equal(int.MinValue, whole.Value);

        // An increment wider than the room left lands on the range's end.
        var bytes = new SkUiStepper<byte> { Maximum = 255, Increment = 200, Value = 100 };
        Assert.True(Increment(bytes));
        Assert.Equal((byte)255, bytes.Value);
    }

    [Fact]
    public void BinaryFloatingPointIsRoundedAsMauisStepper()
    {
        var single = new SkUiStepper<float> { Increment = 0.1f };
        var native = new Stepper { Increment = 0.1 };
        for (var step = 0; step < 3; step++)
        {
            Increment(single);
            native.Value += native.Increment;
        }
        Assert.Equal(0.3f, single.Value);
        Assert.Equal(0.3, native.Value);
        var half = new SkUiStepper<Half> { Increment = (Half)0.5 };
        Increment(half);
        Assert.Equal((Half)0.5, half.Value);
    }

    [Fact]
    public void InvalidValuesAreIgnoredAndRefusedBySetters()
    {
        var stepper = new SkUiStepper<double> { Minimum = 0, Maximum = 10 };
        stepper.Increment = 0;
        stepper.Increment = -1;
        Assert.Equal(1, stepper.Increment);
        stepper.Value = double.NaN;
        stepper.Maximum = double.PositiveInfinity;
        stepper.Minimum = 20;
        Assert.Equal((0d, 0d, 10d), (stepper.Value, stepper.Minimum, stepper.Maximum));
        Assert.Throws<ArgumentOutOfRangeException>(() => stepper.SetIncrement(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => stepper.SetValue(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => stepper.SetMinimum(20));
        Assert.Throws<ArgumentOutOfRangeException>(() => stepper.SetMaximum(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiStepper<int>(5, 5, 5, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiStepper<int>(0, 5, 5, 0));
        var constructed = new SkUiStepper<int>(1, 9, 20, 2);
        Assert.Equal((1, 9, 9, 2), (constructed.Minimum, constructed.Maximum, constructed.Value, constructed.Increment));
    }

    [Fact]
    public void TheAskedValueComesBackAndEventsAreTyped()
    {
        var stepper = new SkUiStepper<long> { Maximum = 10 };
        var changes = new List<(long Old, long New)>();
        stepper.ValueChanged += (_, args) => changes.Add((args.OldValue, args.NewValue));
        stepper.Value = 15;
        Assert.Equal(10, stepper.Value);
        stepper.Maximum = 20;
        Assert.Equal(15, stepper.Value);
        Assert.Equal([(0L, 10L), (10L, 15L)], changes);
        // The fluent setters keep the typed stepper.
        SkUiStepper<long> chained = stepper.SetIncrement(5).SetOrientation(StackOrientation.Vertical).SetDecrementText("Less");
        Assert.Same(stepper, chained);
    }

    [Fact]
    public void ScreenReadersSeeAndSetTheValueAsANumber()
    {
        var stepper = new SkUiStepper<int> { Minimum = -5, Maximum = 5, Value = 2 };
        var info = new SkUiSemanticsInfo();
        ((ISkUiAccessibleNode)stepper).GetSemantics(info);
        Assert.Equal(SkUiSemanticsRole.Slider, info.Role);
        Assert.Equal(new SkUiSemanticsRange(-5, 5, 2), info.Range);
        Assert.Equal("2", info.Value);
        Assert.True(((ISkUiAccessibleNode)stepper).SetSemanticsValue(4));
        Assert.Equal(4, stepper.Value);
        Assert.True(((ISkUiAccessibleNode)stepper).SetSemanticsValue(1e12));
        Assert.Equal(5, stepper.Value);
    }

    [Fact]
    public void ButtonsAndKeysStepATypedValue()
    {
        var stepper = new SkUiStepper<short> { Value = 3, Increment = 2, HorizontalOptions = LayoutOptions.Start };
        var root = new SkUiVerticalStackLayout { Children = { stepper } };
        SkUiTestHelpers.Arrange(root, 400, 200);
        var plus = new Point(stepper.Width - SkUiLook.Current.StepperButtonSize.Width / 2, stepper.Height / 2);
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Pressed, plus, TimeSpan.Zero));
        root.Touch(new SkUiTouchEvent(1, SkUiTouchAction.Released, plus, TimeSpan.FromMilliseconds(30)));
        Assert.Equal((short)5, stepper.Value);
        Assert.True(stepper.Focus());
        root.FocusManager.KeyDown(SkUiKey.Down);
        root.FocusManager.KeyDown(SkUiKey.Down);
        root.FocusManager.KeyDown(SkUiKey.Home);
        Assert.Equal((short)0, stepper.Value);
    }

    [Fact]
    public void CoreStepperIsTypedToo()
    {
        var core = new SkUiCoreStepper<uint>().SetIncrement(4).SetValue(6).SetOrientation(StackOrientation.Vertical);
        var changes = new List<uint>();
        core.ValueChanged += (_, args) => changes.Add(args.NewValue);
        var root = new SkUiVerticalStackLayout { Children = { new SkUiCoreHost().SetContent(core) } };
        SkUiTestHelpers.Arrange(root, 400, 200);
        Assert.True(core.Focus());
        root.FocusManager.KeyDown(SkUiKey.Down);
        root.FocusManager.KeyDown(SkUiKey.Down);
        root.FocusManager.KeyDown(SkUiKey.Down);
        Assert.Equal([2u, 0u], changes);
        core.SetMaximum(5).SetValue(9);
        Assert.Equal(5u, core.Value);
        core.SetMaximum(50);
        Assert.Equal(9u, core.Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => core.SetIncrement(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiCoreStepper<float>().SetValue(float.NaN));
        var info = new SkUiSemanticsInfo();
        ((ISkUiAccessibleNode)core).GetSemantics(info);
        Assert.Equal(new SkUiSemanticsRange(0, 50, 9), info.Range);
    }
}

/// <summary>Typed steppers in XAML: <c>x:TypeArguments</c> picks <see cref="SkUiStepper{T}"/> over <see cref="SkUiStepper"/>.</summary>
[Collection(RuntimeXamlCollection.Name)]
public class TypedStepperXamlTests
{
    private const string Xaml = """
        <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                     xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                     xmlns:sys="clr-namespace:System;assembly=System.Runtime"
                     xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
          <sk:SkUiVerticalStackLayout>
            <sk:SkUiStepper Maximum="5" Value="2.5" />
            <sk:SkUiStepper x:TypeArguments="x:Int32" Minimum="1" Maximum="20" Value="{Binding Quantity}" />
            <sk:SkUiStepper x:TypeArguments="x:Decimal" Increment="0.25" Maximum="2.5" Value="{Binding Price}" />
            <sk:SkUiStepper x:TypeArguments="sys:UInt32" Increment="3" Value="7" Orientation="Vertical" />
          </sk:SkUiVerticalStackLayout>
        </ContentView>
        """;

    [Fact]
    public void TypeArgumentsPickTheTypedStepperAndBindTwoWay()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        var model = new Order { Quantity = 3, Price = 1.75m };
        var root = new ContentView { BindingContext = model };
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, Xaml);
        var children = ((SkUiVerticalStackLayout)root.Content).Children;
        Assert.Equal(2.5, Assert.IsType<SkUiStepper>(children[0]).Value);
        var quantity = Assert.IsType<SkUiStepper<int>>(children[1]);
        var price = Assert.IsType<SkUiStepper<decimal>>(children[2]);
        var count = Assert.IsType<SkUiStepper<uint>>(children[3]);
        Assert.Equal((1, 20, 3), (quantity.Minimum, quantity.Maximum, quantity.Value));
        Assert.Equal((0.25m, 2.5m, 1.75m), (price.Increment, price.Maximum, price.Value));
        Assert.Equal((3u, 7u, StackOrientation.Vertical), (count.Increment, count.Value, count.Orientation));
        // Two-way: the steppers write the model's typed properties.
        ((ISkUiAccessibleNode)quantity).PerformSemanticsAction(SkUiSemanticsActions.Increment);
        ((ISkUiAccessibleNode)price).PerformSemanticsAction(SkUiSemanticsActions.Increment);
        Assert.Equal((4, 2.0m), (model.Quantity, model.Price));
        model.Quantity = 50;
        Assert.Equal(20, quantity.Value);
    }

    private sealed class Order : INotifyPropertyChanged
    {
        public int Quantity { get; set => Set(ref field, value); }

        public decimal Price { get; set => Set(ref field, value); }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
