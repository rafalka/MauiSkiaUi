using System.Collections.ObjectModel;
using System.Collections;
using System.ComponentModel;
using MauiSkiaUi.Rendering;
using ILayout = Microsoft.Maui.ILayout;

namespace MauiSkiaUi;

/// <summary>A minimal overlay layout. Children share its slot and use MAUI margins and alignment.</summary>
[ContentProperty(nameof(Children))]
public class SkUiLayout : SkUiView, ILayout, IBindableLayout
{
    private Thickness _padding;
    private ISkUiView[]? _paintOrder;
    private ISkUiView[] PaintOrder => _paintOrder ??= Children.OrderBy(child => child.ZIndex).ToArray();

    /// <summary>Bindable space inside the layout.</summary>
    public static readonly BindableProperty PaddingProperty = BindableProperty.Create(
        nameof(Padding), typeof(Thickness), typeof(SkUiLayout), default(Thickness),
        propertyChanged: (view, _, value) => ((SkUiLayout)view).OnPaddingChanged((Thickness)value));

    /// <summary>Space inside the layout in DIPs.</summary>
    public Thickness Padding { get => (Thickness)GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }

    /// <summary>Sets padding (same as the property setter).</summary>
    public SkUiLayout SetPadding(Thickness value)
    {
        Padding = value;
        return this;
    }

    private void OnPaddingChanged(Thickness value)
    {
        if (_padding == value) return;
        _padding = value;
        InvalidateMeasureOverride();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        // MAUI's RadioButtonGroup attached properties: MAUI's controller only handles its own RadioButton.
        if (propertyName == RadioButtonGroup.GroupNameProperty.PropertyName)
            SkUiRadioGroups.OnLayoutGroupNameChanged(this);
        else if (propertyName == RadioButtonGroup.SelectedValueProperty.PropertyName)
            SkUiRadioGroups.OnLayoutSelectedValueChanged(this);
    }

    bool ILayout.ClipsToBounds => true;
    bool ISafeAreaView.IgnoreSafeArea => true;
    Size ILayout.CrossPlatformMeasure(double widthConstraint, double heightConstraint) => MeasureContent(widthConstraint, heightConstraint);
    Size ILayout.CrossPlatformArrange(Rect bounds) { ArrangeContent(bounds.Size); return bounds.Size; }
    int ICollection<IView>.Count => Children.Count;
    bool ICollection<IView>.IsReadOnly => false;
    IView IList<IView>.this[int index] { get => Children[index]; set => Children[index] = RequireSkia(value); }
    void ICollection<IView>.Add(IView item) => Children.Add(RequireSkia(item));
    void ICollection<IView>.Clear() => Children.Clear();
    bool ICollection<IView>.Contains(IView item) => item is ISkUiView child && Children.Contains(child);
    void ICollection<IView>.CopyTo(IView[] array, int index) { foreach (var child in Children) array[index++] = child; }
    bool ICollection<IView>.Remove(IView item) => item is ISkUiView child && Children.Remove(child);
    int IList<IView>.IndexOf(IView item) => item is ISkUiView child ? Children.IndexOf(child) : -1;
    void IList<IView>.Insert(int index, IView item) => Children.Insert(index, RequireSkia(item));
    void IList<IView>.RemoveAt(int index) => Children.RemoveAt(index);
    IEnumerator<IView> IEnumerable<IView>.GetEnumerator() => Children.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => Children.GetEnumerator();
    // MAUI's BindableLayout casts to IBindableLayout. It writes through ILayout above today; writes through this list take
    // the same path, so a MAUI change (or app code) using it gets the same conversion and errors.
    IList IBindableLayout.Children => _bindableChildren ??= new BindableChildren(this);
    private BindableChildren? _bindableChildren;
    private ISkUiView RequireSkia(IView view)
    {
        if (view is ISkUiView child)
            return child;
        if (view is Label label && IsBindableLayoutDefaultItem(label))
            return CreateDefaultItem(label.BindingContext);
        throw new ArgumentException(
            $"SkiaUi layouts only host drawn (SkUi*) views, not {view?.GetType().Name ?? "null"}; put native views inside an SkUiMauiContentView. " +
            "With BindableLayout, the ItemTemplate / ItemTemplateSelector must create SkUi* views, and EmptyView must be an SkUi* view or come " +
            "from an EmptyViewTemplate (a string EmptyView creates a MAUI Label).", nameof(view));
    }

    // MAUI's BindableLayout uses its default template (a native Label bound to the item) while no template is set, which also
    // happens briefly when ItemsSource is set before ItemTemplate; those children are replaced once the template arrives.
    // Item views get their item as a local BindingContext, unlike the Label MAUI makes from a string EmptyView, which stays
    // an error: MAUI finds the empty view again by reference.
    private bool IsBindableLayoutDefaultItem(Label label) =>
        IsSet(BindableLayout.ItemsSourceProperty) && BindableLayout.GetItemTemplate(this) is null &&
        BindableLayout.GetItemTemplateSelector(this) is null && label.IsSet(BindingContextProperty);

    // MAUI binds its label's text to the item; MAUI never reuses these children (it replaces them), so the text is set once.
    private static SkUiLabel CreateDefaultItem(object item) =>
        new() { HorizontalTextAlignment = TextAlignment.Center, BindingContext = item, Text = item?.ToString() ?? string.Empty };

    private void OnChildPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ZIndex)) { _paintOrder = null; InvalidateRender(SkUiRenderDirty.Children); }
        if (AffectsChildLayout(args.PropertyName))
            InvalidateMeasureOverride();
    }

    /// <summary>
    /// Whether a child's property change (usually an attached property such as <c>Grid.Row</c>) changes this
    /// layout's measure. MAUI's own attached-property callbacks never invalidate SkiaUi layouts, because the
    /// child's parent is not the MAUI layout type, so each layout lists the attached properties it reads.
    /// Compare against each <see cref="BindableProperty.PropertyName"/>, not a literal string.
    /// </summary>
    private protected virtual bool AffectsChildLayout(string? propertyName) => false;

    /// <summary>Called after a child is added, replaced or removed, or the children are cleared.</summary>
    private protected virtual void OnChildrenChanged() { }

    /// <summary>Creates a GPU-default layout with an owned child collection.</summary>
    public SkUiLayout()
    {
        HwAccelerated = true;
        Children = new ChildCollection(this);
    }

    /// <summary>Children in insertion order; ZIndex determines paint and hit-test order.</summary>
    public IList<ISkUiView> Children { get; }

    /// <inheritdoc />
    internal override IEnumerable<ISkUiView> SkiaChildren => Children;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var desired = Size.Zero;
        foreach (var child in Children)
        {
            var size = child.Measure(Math.Max(0, widthConstraint - _padding.HorizontalThickness), Math.Max(0, heightConstraint - _padding.VerticalThickness));
            desired = new Size(Math.Max(desired.Width, size.Width), Math.Max(desired.Height, size.Height));
        }
        return new Size(desired.Width + _padding.HorizontalThickness, desired.Height + _padding.VerticalThickness);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        foreach (var child in Children)
            child.Arrange(new Rect(_padding.Left, _padding.Top, Math.Max(0, size.Width - _padding.HorizontalThickness), Math.Max(0, size.Height - _padding.VerticalThickness)));
    }

    /// <inheritdoc />
    internal override void AddRenderChildren(List<ISkUiRenderable> children)
    {
        foreach (var child in PaintOrder)
            if (child is ISkUiRenderable renderable)
                children.Add(renderable);
    }

    /// <summary><see cref="IBindableLayout.Children"/>: reads <see cref="Children"/>, writes through <see cref="ILayout"/>.</summary>
    private sealed class BindableChildren(SkUiLayout owner) : IList
    {
        private IList<IView> Layout => owner;
        public object? this[int index] { get => owner.Children[index]; set => Layout[index] = AsView(value); }
        public bool IsFixedSize => false;
        public bool IsReadOnly => false;
        public int Count => owner.Children.Count;
        public bool IsSynchronized => false;
        public object SyncRoot => this;
        public int Add(object? value) { Layout.Add(AsView(value)); return owner.Children.Count - 1; }
        public void Clear() => Layout.Clear();
        public bool Contains(object? value) => value is ISkUiView child && owner.Children.Contains(child);
        public int IndexOf(object? value) => value is ISkUiView child ? owner.Children.IndexOf(child) : -1;
        public void Insert(int index, object? value) => Layout.Insert(index, AsView(value));
        public void Remove(object? value) { if (value is IView view) Layout.Remove(view); }
        public void RemoveAt(int index) => Layout.RemoveAt(index);
        public void CopyTo(Array array, int index) => ((ICollection)owner.Children).CopyTo(array, index);
        public IEnumerator GetEnumerator() => owner.Children.GetEnumerator();
        private static IView AsView(object? value) => value as IView ?? throw new ArgumentException($"SkiaUi layouts only host drawn (SkUi*) views, not {value?.GetType().Name ?? "null"}.", nameof(value));
    }

    private sealed class ChildCollection(SkUiLayout owner) : Collection<ISkUiView>
    {
        protected override void InsertItem(int index, ISkUiView item)
        {
            owner.ValidateChild(item);
            base.InsertItem(index, item);
            owner._paintOrder = null;
            ((Element)item).PropertyChanged += owner.OnChildPropertyChanged;
            owner.OnChildrenChanged();
            owner.AttachChild(item);
        }

        protected override void SetItem(int index, ISkUiView item)
        {
            if (ReferenceEquals(this[index], item))
                return;
            owner.ValidateChild(item);
            var previous = this[index];
            base.SetItem(index, item);
            owner._paintOrder = null;
            ((Element)previous).PropertyChanged -= owner.OnChildPropertyChanged;
            ((Element)item).PropertyChanged += owner.OnChildPropertyChanged;
            owner.OnChildrenChanged();
            owner.DetachChild(previous);
            owner.AttachChild(item);
        }

        protected override void RemoveItem(int index)
        {
            var previous = this[index];
            base.RemoveItem(index);
            owner._paintOrder = null;
            ((Element)previous).PropertyChanged -= owner.OnChildPropertyChanged;
            owner.OnChildrenChanged();
            owner.DetachChild(previous);
        }

        protected override void ClearItems()
        {
            var previous = this.ToArray();
            base.ClearItems();
            owner._paintOrder = null;
            owner.OnChildrenChanged();
            foreach (var child in previous)
            {
                ((Element)child).PropertyChanged -= owner.OnChildPropertyChanged;
                owner.DetachChild(child);
            }
        }
    }
}