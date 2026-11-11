using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Core.Layouts;
using CommunityToolkit.Maui.Layouts;

namespace MauiSkiaUi.CommunityToolkit;

/// <summary>
/// A drawn dock layout with the Community Toolkit's <see cref="DockLayout"/> API, laid out by the toolkit's
/// <see cref="DockLayoutManager"/>: children are docked in order to the <see cref="DockPosition.Top"/>,
/// <see cref="DockPosition.Bottom"/>, <see cref="DockPosition.Left"/> or <see cref="DockPosition.Right"/> of the space the
/// earlier children left, and the last child fills what remains (<see cref="ShouldExpandLastChild"/>). Children use the
/// toolkit's attached <c>DockLayout.DockPosition</c>, also reachable through this type. Children must be drawn views.
/// </summary>
public class SkUiDockLayout : SkUiLayout, IDockLayout
{
    private readonly DockLayoutManager _manager;
    private bool _shouldExpandLastChild = true;
    private double _horizontalSpacing;
    private double _verticalSpacing;

    /// <summary>Bindable property for <see cref="ShouldExpandLastChild"/>.</summary>
    public static readonly BindableProperty ShouldExpandLastChildProperty = BindableProperty.Create(
        nameof(ShouldExpandLastChild), typeof(bool), typeof(SkUiDockLayout), true,
        propertyChanged: (view, _, value) => ((SkUiDockLayout)view).OnShouldExpandLastChildChanged((bool)value));

    /// <summary>Bindable property for <see cref="HorizontalSpacing"/>.</summary>
    public static readonly BindableProperty HorizontalSpacingProperty = BindableProperty.Create(
        nameof(HorizontalSpacing), typeof(double), typeof(SkUiDockLayout), 0d, validateValue: IsSpacing,
        propertyChanged: (view, _, value) => ((SkUiDockLayout)view).OnHorizontalSpacingChanged((double)value));

    /// <summary>Bindable property for <see cref="VerticalSpacing"/>.</summary>
    public static readonly BindableProperty VerticalSpacingProperty = BindableProperty.Create(
        nameof(VerticalSpacing), typeof(double), typeof(SkUiDockLayout), 0d, validateValue: IsSpacing,
        propertyChanged: (view, _, value) => ((SkUiDockLayout)view).OnVerticalSpacingChanged((double)value));

    /// <summary>The toolkit's attached <c>DockLayout.DockPosition</c>: where a child docks (default <see cref="DockPosition.None"/>, docked as <see cref="DockPosition.Top"/>).</summary>
    public static readonly BindableProperty DockPositionProperty = DockLayout.DockPositionProperty;

    /// <summary>Creates a dock layout laid out by the toolkit's <see cref="DockLayoutManager"/>.</summary>
    public SkUiDockLayout() => _manager = new DockLayoutManager(this);

    /// <summary>Whether the last child fills the space the others left, whatever its dock position; default <see langword="true"/>.</summary>
    public bool ShouldExpandLastChild { get => (bool)GetValue(ShouldExpandLastChildProperty); set => SetValue(ShouldExpandLastChildProperty, value); }

    /// <summary>The gap after a child docked left or right, in DIPs (≥ 0); default 0.</summary>
    public double HorizontalSpacing { get => (double)GetValue(HorizontalSpacingProperty); set => SetValue(HorizontalSpacingProperty, value); }

    /// <summary>The gap after a child docked top or bottom, in DIPs (≥ 0); default 0.</summary>
    public double VerticalSpacing { get => (double)GetValue(VerticalSpacingProperty); set => SetValue(VerticalSpacingProperty, value); }

    bool IDockLayout.ShouldExpandLastChild => _shouldExpandLastChild;
    double IDockLayout.HorizontalSpacing => _horizontalSpacing;
    double IDockLayout.VerticalSpacing => _verticalSpacing;

    /// <summary>Sets <see cref="ShouldExpandLastChild"/> (same as the property setter).</summary>
    public SkUiDockLayout SetShouldExpandLastChild(bool value)
    {
        ShouldExpandLastChild = value;
        return this;
    }

    /// <summary>Sets <see cref="HorizontalSpacing"/>; throws for a negative or non-finite value.</summary>
    public SkUiDockLayout SetHorizontalSpacing(double value)
    {
        ThrowIfNotSpacing(value, nameof(value));
        HorizontalSpacing = value;
        return this;
    }

    /// <summary>Sets <see cref="VerticalSpacing"/>; throws for a negative or non-finite value.</summary>
    public SkUiDockLayout SetVerticalSpacing(double value)
    {
        ThrowIfNotSpacing(value, nameof(value));
        VerticalSpacing = value;
        return this;
    }

    /// <summary>Gets a child's dock position.</summary>
    public static DockPosition GetDockPosition(BindableObject view) => (DockPosition)view.GetValue(DockPositionProperty);

    /// <summary>Sets a child's dock position.</summary>
    public static void SetDockPosition(BindableObject view, DockPosition value) => view.SetValue(DockPositionProperty, value);

    /// <inheritdoc />
    public DockPosition GetDockPosition(IView view) => GetDockPosition((BindableObject)view);

    /// <summary>Adds a drawn child docked at <paramref name="position"/>, as the toolkit's <see cref="DockLayout.Add(IView, DockPosition)"/>.</summary>
    public void Add(IView view, DockPosition position = DockPosition.None)
    {
        base.Add(view); // not this overload: Add(view) would resolve here, to this method
        if (view is BindableObject bindable)
            SetDockPosition(bindable, position);
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => _manager.Measure(widthConstraint, heightConstraint);

    /// <inheritdoc />
    protected override void ArrangeContent(Size size) => _manager.ArrangeChildren(new Rect(Point.Zero, size));

    /// <inheritdoc />
    protected override bool AffectsChildLayout(string? propertyName) => propertyName == DockPositionProperty.PropertyName;

    private void OnShouldExpandLastChildChanged(bool value)
    {
        _shouldExpandLastChild = value;
        InvalidateMeasureOverride();
    }

    private void OnHorizontalSpacingChanged(double value)
    {
        _horizontalSpacing = value;
        InvalidateMeasureOverride();
    }

    private void OnVerticalSpacingChanged(double value)
    {
        _verticalSpacing = value;
        InvalidateMeasureOverride();
    }

    private static bool IsSpacing(BindableObject bindable, object? value) => value is double number && double.IsFinite(number) && number >= 0;

    private static void ThrowIfNotSpacing(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0)
            throw new ArgumentOutOfRangeException(name, value, "Spacing must be a finite number of DIPs, at least 0.");
    }
}
