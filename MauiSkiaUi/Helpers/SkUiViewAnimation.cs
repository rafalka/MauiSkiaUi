using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Microsoft.Maui.Converters;

namespace MauiSkiaUi;

/// <summary>
/// One property of a <see cref="SkUiViewAnimation"/>: where it starts and where it ends. Read-only once its
/// animation is in use.
/// </summary>
public sealed class SkUiPropertyAnimation
{
    private SkUiAnimatableProperty _property;
    private double? _from;
    private double? _to;

    /// <summary>Creates an opacity animation from the current value to the view's own value (set the properties).</summary>
    public SkUiPropertyAnimation() { }

    /// <summary>Creates an animation of <paramref name="property"/>.</summary>
    /// <param name="property">The composite-time property.</param>
    /// <param name="from">Start value, set when the animation starts; <c>null</c> starts from the current value.</param>
    /// <param name="to">End value; <c>null</c> ends at the view's own value (the one it had when the animation started).</param>
    public SkUiPropertyAnimation(SkUiAnimatableProperty property, double? from = null, double? to = null)
    {
        _property = property;
        _from = from;
        _to = to;
    }

    /// <summary>The composite-time property.</summary>
    public SkUiAnimatableProperty Property { get => _property; set { ThrowIfFrozen(); _property = value; } }

    /// <summary>Start value, set when the animation starts; <c>null</c> (default) starts from the current value.</summary>
    public double? From { get => _from; set { ThrowIfFrozen(); _from = value; } }

    /// <summary>End value; <c>null</c> (default) ends at the view's own value (the one it had when the animation started).</summary>
    public double? To { get => _to; set { ThrowIfFrozen(); _to = value; } }

    internal bool IsFrozen { get; set; }

    private void ThrowIfFrozen()
    {
        if (IsFrozen)
            throw new InvalidOperationException($"This {nameof(SkUiPropertyAnimation)} belongs to an animation in use; create a new one.");
    }
}

/// <summary>
/// A reusable description of a render-thread animation of a view's composite-time properties (opacity, translation,
/// rotation, scale), run with <see cref="SkUiView.AnimateAsync"/>: it stays smooth while the UI thread is busy and
/// never re-lays out or re-records the view. Containers run it on the views they show or hide
/// (<see cref="SkUiStateContainer"/>'s state change animations). The properties run together.
/// </summary>
/// <remarks>
/// <para>
/// XAML: <c>"FadeIn"</c> / <c>"FadeOut"</c>, optionally followed by a length in milliseconds and an easing
/// (<c>"FadeIn 300 CubicOut"</c>), or an element listing <see cref="SkUiPropertyAnimation"/>s.
/// </para>
/// <para>
/// The SkiaUi counterpart of a MAUI <see cref="Animation"/> whose callbacks set those properties:
/// <c>new Animation(v =&gt; view.Opacity = v, 1, 0, Easing.CubicIn)</c> becomes
/// <c>new SkUiViewAnimation([new(SkUiAnimatableProperty.Opacity, from: 1, to: 0)], 250, Easing.CubicIn)</c>.
/// </para>
/// <para>An animation becomes read-only once it is used (run, or set on a container), so one instance can be shared.</para>
/// </remarks>
[ContentProperty(nameof(Properties))]
[TypeConverter(typeof(SkUiViewAnimationTypeConverter))]
public sealed class SkUiViewAnimation
{
    private uint _length = 250;
    private Easing? _easing;

    /// <summary>Creates an empty animation of 250 ms (add <see cref="Properties"/>).</summary>
    public SkUiViewAnimation() => Properties = new PropertyCollection(this);

    /// <summary>Creates an animation of <paramref name="properties"/>.</summary>
    public SkUiViewAnimation(IEnumerable<SkUiPropertyAnimation> properties, uint length = 250, Easing? easing = null) : this()
    {
        ArgumentNullException.ThrowIfNull(properties);
        foreach (var property in properties)
            Properties.Add(property);
        _length = length;
        _easing = easing;
    }

    /// <summary>The animated properties; they run together.</summary>
    public IList<SkUiPropertyAnimation> Properties { get; }

    /// <summary>Duration in milliseconds (default 250).</summary>
    public uint Length { get => _length; set { ThrowIfFrozen(); _length = value; } }

    /// <summary>Curve (default linear). In XAML, an easing name such as <c>CubicOut</c>.</summary>
    [TypeConverter(typeof(EasingTypeConverter))]
    public Easing? Easing { get => _easing; set { ThrowIfFrozen(); _easing = value; } }

    /// <summary>Whether the animation is in use and read-only.</summary>
    public bool IsFrozen { get; private set; }

    /// <summary>Fades to transparent.</summary>
    public static SkUiViewAnimation FadeOut(uint length = 250, Easing? easing = null) => new([new(SkUiAnimatableProperty.Opacity, to: 0)], length, easing);

    /// <summary>Fades from transparent to the view's own opacity.</summary>
    public static SkUiViewAnimation FadeIn(uint length = 250, Easing? easing = null) => new([new(SkUiAnimatableProperty.Opacity, from: 0)], length, easing);

    /// <summary>
    /// Parses <c>"FadeIn"</c> / <c>"FadeOut"</c>, optionally followed by a length in milliseconds and an easing name
    /// (<c>"FadeOut 150 CubicIn"</c>; also <c>"FadeOut, 150, CubicIn"</c>).
    /// </summary>
    public static SkUiViewAnimation Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var parts = text.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is 0 or > 3)
            throw new FormatException($"\"{text}\" is not an animation: use FadeIn or FadeOut, optionally followed by a length in milliseconds and an easing.");
        var length = 250u;
        Easing? easing = null;
        foreach (var part in parts.Skip(1))
        {
            if (uint.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var milliseconds))
                length = milliseconds;
            else
                easing = (Easing?)new EasingTypeConverter().ConvertFromInvariantString(part);
        }
        return parts[0].ToLowerInvariant() switch
        {
            "fadein" => FadeIn(length, easing),
            "fadeout" => FadeOut(length, easing),
            _ => throw new FormatException($"Unknown animation \"{parts[0]}\": use FadeIn or FadeOut, or list the properties.")
        };
    }

    /// <summary>
    /// Runs the animation on <paramref name="view"/>; <c>true</c> when every property ran to completion. Each property
    /// ends at its exact end value. Off screen, the view jumps to the end values.
    /// </summary>
    public async Task<bool> RunAsync(SkUiView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        Freeze();
        var own = Snapshot.Of(view);
        if (!view.RenderState.HasCommitted && view.SkiaParent?.RenderState.HasCommitted != true)
        {
            ApplyEnd(view, own);
            return true;
        }
        var runs = new Task<bool>[Properties.Count];
        for (var index = 0; index < runs.Length; index++)
        {
            var property = Properties[index];
            if (property.From is { } from)
                Set(view, property.Property, from);
            runs[index] = view.AnimateAsync(property.Property, property.To ?? own.Get(property.Property), _length, _easing);
        }
        // Awaited in turn rather than with Task.WhenAll: render-thread animations complete their tasks asynchronously, so
        // WhenAll would finish on a pool thread and reach the UI thread a hop later (late when the pool is busy); each
        // await here is posted straight to the UI thread.
        var completed = true;
        foreach (var run in runs)
            completed &= await run;
        if (completed)
            ApplyEnd(view, own);
        return completed;
    }

    /// <summary>Makes the animation read-only (it is shared by the views and containers that use it).</summary>
    internal void Freeze()
    {
        if (IsFrozen)
            return;
        IsFrozen = true;
        foreach (var property in Properties)
            property.IsFrozen = true;
    }

    /// <summary>Sets every animated property of <paramref name="view"/> to its end value (exact, unlike render-thread floats).</summary>
    internal void ApplyEnd(SkUiView view, Snapshot own)
    {
        foreach (var property in Properties)
            Set(view, property.Property, property.To ?? own.Get(property.Property));
    }

    internal static void Set(SkUiView view, SkUiAnimatableProperty property, double value)
    {
        switch (property)
        {
            case SkUiAnimatableProperty.Opacity: view.Opacity = value; break;
            case SkUiAnimatableProperty.TranslationX: view.TranslationX = value; break;
            case SkUiAnimatableProperty.TranslationY: view.TranslationY = value; break;
            case SkUiAnimatableProperty.Rotation: view.Rotation = value; break;
            case SkUiAnimatableProperty.Scale: view.Scale = value; break;
            case SkUiAnimatableProperty.ScaleX: view.ScaleX = value; break;
            case SkUiAnimatableProperty.ScaleY: view.ScaleY = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(property));
        }
    }

    private void ThrowIfFrozen()
    {
        if (IsFrozen)
            throw new InvalidOperationException($"This {nameof(SkUiViewAnimation)} is in use; create a new one.");
    }

    /// <summary>A view's composite-time properties at one moment.</summary>
    internal readonly record struct Snapshot(double Opacity, double TranslationX, double TranslationY, double Rotation, double Scale, double ScaleX, double ScaleY)
    {
        public static Snapshot Of(SkUiView view) =>
            new(view.Opacity, view.TranslationX, view.TranslationY, view.Rotation, view.Scale, view.ScaleX, view.ScaleY);

        public double Get(SkUiAnimatableProperty property) => property switch
        {
            SkUiAnimatableProperty.Opacity => Opacity,
            SkUiAnimatableProperty.TranslationX => TranslationX,
            SkUiAnimatableProperty.TranslationY => TranslationY,
            SkUiAnimatableProperty.Rotation => Rotation,
            SkUiAnimatableProperty.Scale => Scale,
            SkUiAnimatableProperty.ScaleX => ScaleX,
            SkUiAnimatableProperty.ScaleY => ScaleY,
            _ => throw new ArgumentOutOfRangeException(nameof(property))
        };

        public void Restore(SkUiView view)
        {
            view.Opacity = Opacity;
            view.TranslationX = TranslationX;
            view.TranslationY = TranslationY;
            view.Rotation = Rotation;
            view.Scale = Scale;
            view.ScaleX = ScaleX;
            view.ScaleY = ScaleY;
        }
    }

    private sealed class PropertyCollection(SkUiViewAnimation owner) : Collection<SkUiPropertyAnimation>
    {
        protected override void InsertItem(int index, SkUiPropertyAnimation item)
        {
            ArgumentNullException.ThrowIfNull(item);
            owner.ThrowIfFrozen();
            base.InsertItem(index, item);
        }

        protected override void SetItem(int index, SkUiPropertyAnimation item)
        {
            ArgumentNullException.ThrowIfNull(item);
            owner.ThrowIfFrozen();
            base.SetItem(index, item);
        }

        protected override void RemoveItem(int index)
        {
            owner.ThrowIfFrozen();
            base.RemoveItem(index);
        }

        protected override void ClearItems()
        {
            owner.ThrowIfFrozen();
            base.ClearItems();
        }
    }
}

/// <summary>Converts <c>"FadeIn"</c> / <c>"FadeOut"</c> [length] [easing] to a <see cref="SkUiViewAnimation"/> (<see cref="SkUiViewAnimation.Parse"/>).</summary>
public sealed class SkUiViewAnimationTypeConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
        value is string text ? SkUiViewAnimation.Parse(text) : base.ConvertFrom(context, culture, value);
}
