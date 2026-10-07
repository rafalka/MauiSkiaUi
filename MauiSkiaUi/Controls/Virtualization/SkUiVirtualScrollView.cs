using System.Collections;
using System.Windows.Input;

namespace MauiSkiaUi;

/// <summary>
/// A vertical <see cref="SkUiScrollView"/> showing one <see cref="SkUiVirtualVerticalStackLayout"/>: the convenience control for
/// a plain virtualized list. The layout is <see cref="Items"/> (and the scroll view's <see cref="SkUiContentView.Content"/>,
/// which must not be replaced); the list members (<see cref="ISkUiVirtualList"/>) are forwarded to it, and its bindable
/// properties are declared from the layout's (same names, types, defaults and validation). In XAML the element's content
/// is the <see cref="ItemTemplate"/>.
/// </summary>
[ContentProperty(nameof(ItemTemplate))]
public class SkUiVirtualScrollView : SkUiScrollView, ISkUiVirtualList
{
    /// <summary>Creates a vertical scroll view with an empty virtual stack.</summary>
    public SkUiVirtualScrollView()
    {
        Items = new SkUiVirtualVerticalStackLayout();
        Items.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(FirstVisibleIndex) or nameof(LastVisibleIndex))
                OnPropertyChanged(args.PropertyName);
        };
        Content = Items;
    }

    /// <summary>The virtual stack this view scrolls.</summary>
    public SkUiVirtualVerticalStackLayout Items { get; }

    /// <summary>
    /// A bindable property of this view for one of <see cref="Items"/>' (its name, type and default), whose changes are set on
    /// <see cref="Items"/>. Validated as the layout's, so a value the layout refuses is refused here too.
    /// </summary>
    private static BindableProperty Forwarded(BindableProperty itemsProperty, BindableProperty.ValidateValueDelegate? validate = null) =>
        BindableProperty.Create(itemsProperty.PropertyName, itemsProperty.ReturnType, typeof(SkUiVirtualScrollView), itemsProperty.DefaultValue,
            validateValue: validate,
            propertyChanged: (view, _, value) => ((SkUiVirtualScrollView)view).Items.SetValue(itemsProperty, value));

    /// <summary>Bindable property for <see cref="ItemsSource"/>.</summary>
    public static readonly BindableProperty ItemsSourceProperty = Forwarded(SkUiVirtualVerticalStackLayout.ItemsSourceProperty);

    /// <summary>Bindable property for <see cref="ItemTemplate"/>.</summary>
    public static readonly BindableProperty ItemTemplateProperty = Forwarded(SkUiVirtualVerticalStackLayout.ItemTemplateProperty);

    /// <summary>Bindable property for <see cref="Spacing"/>.</summary>
    public static readonly BindableProperty SpacingProperty = Forwarded(SkUiVirtualVerticalStackLayout.SpacingProperty, SkUiValidate.NonNegative);

    /// <summary>Bindable property for <see cref="ItemExtent"/>.</summary>
    public static readonly BindableProperty ItemExtentProperty = Forwarded(SkUiVirtualVerticalStackLayout.ItemExtentProperty, SkUiValidate.NonNegative);

    /// <summary>Bindable property for <see cref="EstimatedItemSize"/>.</summary>
    public static readonly BindableProperty EstimatedItemSizeProperty = Forwarded(SkUiVirtualVerticalStackLayout.EstimatedItemSizeProperty, SkUiValidate.NonNegative);

    /// <summary>Bindable property for <see cref="PrefetchFactor"/>.</summary>
    public static readonly BindableProperty PrefetchFactorProperty = Forwarded(SkUiVirtualVerticalStackLayout.PrefetchFactorProperty, SkUiValidate.NonNegative);

    /// <summary>Bindable property for <see cref="PrefetchBehindFactor"/>.</summary>
    public static readonly BindableProperty PrefetchBehindFactorProperty = Forwarded(SkUiVirtualVerticalStackLayout.PrefetchBehindFactorProperty, SkUiValidate.NonNegative);

    /// <summary>Bindable property for <see cref="ReleaseFactor"/>.</summary>
    public static readonly BindableProperty ReleaseFactorProperty = Forwarded(SkUiVirtualVerticalStackLayout.ReleaseFactorProperty, SkUiVirtualVerticalStackLayout.IsValidReleaseFactor);

    /// <summary>Bindable property for <see cref="PrefetchBudget"/>.</summary>
    public static readonly BindableProperty PrefetchBudgetProperty = Forwarded(SkUiVirtualVerticalStackLayout.PrefetchBudgetProperty, SkUiVirtualVerticalStackLayout.IsValidPrefetchBudget);

    /// <summary>Bindable property for <see cref="RemainingItemsThreshold"/>.</summary>
    public static readonly BindableProperty RemainingItemsThresholdProperty = Forwarded(SkUiVirtualVerticalStackLayout.RemainingItemsThresholdProperty, SkUiVirtualVerticalStackLayout.IsValidThreshold);

    /// <summary>Bindable property for <see cref="RemainingItemsThresholdReachedCommand"/>.</summary>
    public static readonly BindableProperty RemainingItemsThresholdReachedCommandProperty = Forwarded(SkUiVirtualVerticalStackLayout.RemainingItemsThresholdReachedCommandProperty);

    /// <summary>Bindable property for <see cref="RemainingItemsThresholdReachedCommandParameter"/>.</summary>
    public static readonly BindableProperty RemainingItemsThresholdReachedCommandParameterProperty = Forwarded(SkUiVirtualVerticalStackLayout.RemainingItemsThresholdReachedCommandParameterProperty);

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayout.ItemsSource" />
    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayout.ItemTemplate" />
    public DataTemplate? ItemTemplate { get => (DataTemplate?)GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.Spacing" />
    public double Spacing { get => (double)GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.ItemExtent" />
    public double ItemExtent { get => (double)GetValue(ItemExtentProperty); set => SetValue(ItemExtentProperty, value); }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.EstimatedItemSize" />
    public double EstimatedItemSize { get => (double)GetValue(EstimatedItemSizeProperty); set => SetValue(EstimatedItemSizeProperty, value); }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.PrefetchFactor" />
    public double PrefetchFactor { get => (double)GetValue(PrefetchFactorProperty); set => SetValue(PrefetchFactorProperty, value); }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.PrefetchBehindFactor" />
    public double PrefetchBehindFactor { get => (double)GetValue(PrefetchBehindFactorProperty); set => SetValue(PrefetchBehindFactorProperty, value); }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.ReleaseFactor" />
    public double ReleaseFactor { get => (double)GetValue(ReleaseFactorProperty); set => SetValue(ReleaseFactorProperty, value); }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.PrefetchBudget" />
    public TimeSpan? PrefetchBudget { get => (TimeSpan?)GetValue(PrefetchBudgetProperty); set => SetValue(PrefetchBudgetProperty, value); }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.RemainingItemsThreshold" />
    public int RemainingItemsThreshold { get => (int)GetValue(RemainingItemsThresholdProperty); set => SetValue(RemainingItemsThresholdProperty, value); }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.RemainingItemsThresholdReachedCommand" />
    public ICommand? RemainingItemsThresholdReachedCommand
    {
        get => (ICommand?)GetValue(RemainingItemsThresholdReachedCommandProperty);
        set => SetValue(RemainingItemsThresholdReachedCommandProperty, value);
    }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.RemainingItemsThresholdReachedCommandParameter" />
    public object? RemainingItemsThresholdReachedCommandParameter
    {
        get => GetValue(RemainingItemsThresholdReachedCommandParameterProperty);
        set => SetValue(RemainingItemsThresholdReachedCommandParameterProperty, value);
    }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayout.ItemFactory" />
    public Func<int, ISkUiView?>? ItemFactory { get => Items.ItemFactory; set => Items.ItemFactory = value; }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayout.ItemFactoryCount" />
    public int? ItemFactoryCount { get => Items.ItemFactoryCount; set => Items.ItemFactoryCount = value; }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.ItemCount" />
    public int ItemCount => Items.ItemCount;

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.FirstVisibleIndex" />
    public int FirstVisibleIndex => Items.FirstVisibleIndex;

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.LastVisibleIndex" />
    public int LastVisibleIndex => Items.LastVisibleIndex;

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.ItemRealized" />
    public event EventHandler<SkUiVirtualItemEventArgs>? ItemRealized { add => Items.ItemRealized += value; remove => Items.ItemRealized -= value; }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.ItemReleased" />
    public event EventHandler<SkUiVirtualItemEventArgs>? ItemReleased { add => Items.ItemReleased += value; remove => Items.ItemReleased -= value; }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.VisibleRangeChanged" />
    public event EventHandler<SkUiVisibleRangeChangedEventArgs>? VisibleRangeChanged { add => Items.VisibleRangeChanged += value; remove => Items.VisibleRangeChanged -= value; }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.RemainingItemsThresholdReached" />
    public event EventHandler? RemainingItemsThresholdReached { add => Items.RemainingItemsThresholdReached += value; remove => Items.RemainingItemsThresholdReached -= value; }

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.ScrollToIndex" />
    public Task ScrollToIndex(int index, ScrollToPosition position = ScrollToPosition.MakeVisible, bool animated = true) =>
        Items.ScrollToIndex(index, position, animated);

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.GetRealizedView" />
    public ISkUiView? GetRealizedView(int index) => Items.GetRealizedView(index);

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayoutBase.RemeasureItem" />
    public void RemeasureItem(int index) => Items.RemeasureItem(index);

    /// <inheritdoc cref="SkUiVirtualVerticalStackLayout.SetItemFactory" />
    public SkUiVirtualScrollView SetItemFactory(Func<int, ISkUiView?>? factory, int? count = null)
    {
        Items.SetItemFactory(factory, count);
        return this;
    }

    /// <summary>Sets <see cref="ItemsSource"/> (same as the property setter).</summary>
    public SkUiVirtualScrollView SetItemsSource(IEnumerable? value) { ItemsSource = value; return this; }

    /// <summary>Sets <see cref="ItemTemplate"/> (same as the property setter).</summary>
    public SkUiVirtualScrollView SetItemTemplate(DataTemplate? value) { ItemTemplate = value; return this; }
}
