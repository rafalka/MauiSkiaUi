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
    /// <summary>Bindable property for <see cref="CascadeInputTransparent"/>.</summary>
    public static readonly BindableProperty CascadeInputTransparentProperty = CascadeInputTransparentShared;

    /// <summary>
    /// Whether <see cref="VisualElement.InputTransparent"/> covers the children too (<c>true</c>, the default, as in MAUI).
    /// <c>false</c>: pointers pass through the layout itself, and its children still take them.
    /// </summary>
    public bool CascadeInputTransparent { get => (bool)GetValue(CascadeInputTransparentProperty); set => SetValue(CascadeInputTransparentProperty, value); }

    /// <summary>Sets <see cref="CascadeInputTransparent"/>.</summary>
    public SkUiLayout SetCascadeInputTransparent(bool value) { CascadeInputTransparent = value; return this; }

    /// <summary>Bindable property for <see cref="IsClippedToBounds"/>: MAUI's name for <see cref="SkUiView.ClipToBoundsProperty"/>.</summary>
    public static readonly BindableProperty IsClippedToBoundsProperty = ClipToBoundsProperty;

    /// <summary>MAUI's name for <see cref="SkUiView.ClipToBounds"/>: the same property.</summary>
    public bool IsClippedToBounds { get => ClipToBounds; set => ClipToBounds = value; }

    /// <summary>Bindable property for <see cref="SafeAreaEdges"/>.</summary>
    public static readonly BindableProperty SafeAreaEdgesProperty = SafeAreaEdgesShared;

    /// <summary>
    /// Which edges keep clear of the window's bars, notch and soft keyboard (MAUI 10). Opt-in: the default is
    /// <see cref="SafeAreaRegions.None"/>, edge to edge as in 1.x (MAUI's layouts default to <c>Container</c>: set
    /// <c>SafeAreaEdges="Container"</c> for MAUI's behavior). Applied by a layout that is a surface of its own: it pads its
    /// children by how far the window's insets reach into it on each such edge, as MAUI's layouts pad themselves. Layouts
    /// inside a surface accept it and ignore it (their place in the window is the surface's).
    /// </summary>
    public SafeAreaEdges SafeAreaEdges { get => (SafeAreaEdges)GetValue(SafeAreaEdgesProperty); set => SetValue(SafeAreaEdgesProperty, value); }

    /// <summary>Sets <see cref="SafeAreaEdges"/> (same as the property setter).</summary>
    public SkUiLayout SetSafeAreaEdges(SafeAreaEdges value) { SafeAreaEdges = value; return this; }

    /// <summary>A container in a skeleton made from content, also while empty: its children are the bones.</summary>
    internal override bool IsSkeletonContainer => true;

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

    /// <summary>The padding the children are laid out in: <see cref="Padding"/>, plus the safe area's on a surface root (<see cref="SafeAreaEdges"/>).</summary>
    private protected Thickness EffectivePadding => _padding + SafeAreaInset;

    // MAUI's layout managers read the padding through IPadding.
    Thickness IPadding.Padding => EffectivePadding;

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
    // MAUI's Layout members, public as there, so code-behind (stack.Add(view), collection initializers) ports by renaming the
    // type. They take IView, as MAUI's, and require drawn views; null is ignored by Add / Insert and an index past the end by
    // RemoveAt, as MAUI does.

    /// <summary>Number of children (<see cref="Children"/>).</summary>
    public int Count => Children.Count;

    /// <summary>Always <c>false</c>: children can be added and removed.</summary>
    public bool IsReadOnly => false;

    /// <summary>The child at <paramref name="index"/>; a replacement must be a drawn (SkUi*) view.</summary>
    public IView this[int index] { get => Children[index]; set => Children[index] = RequireSkia(value); }

    /// <summary>Appends a drawn child (MAUI's <c>Layout.Add</c>); <c>null</c> is ignored.</summary>
    /// <exception cref="ArgumentException"><paramref name="child"/> is not a drawn (SkUi*) view.</exception>
    public void Add(IView child)
    {
        if (child is not null)
            Children.Add(RequireSkia(child));
    }

    /// <summary>
    /// Appends many drawn children inside one <see cref="SkUiView.StartUpdating"/> / <see cref="SkUiView.EndUpdating"/> batch,
    /// so measure, arrange and paint invalidate once after all of them. An outer batch stays open until its own end.
    /// </summary>
    public void AddRange(IEnumerable<IView> children)
    {
        ArgumentNullException.ThrowIfNull(children);
        StartUpdating();
        try
        {
            foreach (var child in children)
                Add(child);
        }
        finally
        {
            EndUpdating();
        }
    }

    /// <summary>Inserts a drawn child at <paramref name="index"/> (MAUI's <c>Layout.Insert</c>); <c>null</c> is ignored.</summary>
    /// <exception cref="ArgumentException"><paramref name="child"/> is not a drawn (SkUi*) view.</exception>
    public void Insert(int index, IView child)
    {
        if (child is not null)
            Children.Insert(index, RequireSkia(child));
    }

    /// <summary>Removes a child; <c>false</c> when it is not one.</summary>
    public bool Remove(IView child) => child is ISkUiView view && Children.Remove(view);

    /// <summary>Removes the child at <paramref name="index"/>; an index past the end is ignored, as by MAUI's layouts.</summary>
    public void RemoveAt(int index)
    {
        if (index < Children.Count)
            Children.RemoveAt(index);
    }

    /// <summary>Removes all children.</summary>
    public void Clear() => Children.Clear();

    /// <summary>Whether <paramref name="item"/> is a child.</summary>
    public bool Contains(IView item) => item is ISkUiView child && Children.Contains(child);

    /// <summary>Copies the children to <paramref name="array"/> from <paramref name="arrayIndex"/>.</summary>
    public void CopyTo(IView[] array, int arrayIndex)
    {
        foreach (var child in Children)
            array[arrayIndex++] = child;
    }

    /// <summary>Index of a child, <c>-1</c> when it is not one.</summary>
    public int IndexOf(IView item) => item is ISkUiView child ? Children.IndexOf(child) : -1;

    /// <summary>Enumerates the children in insertion order.</summary>
    public IEnumerator<IView> GetEnumerator() => Children.GetEnumerator();

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
        var padding = EffectivePadding;
        var desired = Size.Zero;
        foreach (var child in Children)
        {
            var size = child.Measure(Math.Max(0, widthConstraint - padding.HorizontalThickness), Math.Max(0, heightConstraint - padding.VerticalThickness));
            desired = new Size(Math.Max(desired.Width, size.Width), Math.Max(desired.Height, size.Height));
        }
        return new Size(desired.Width + padding.HorizontalThickness, desired.Height + padding.VerticalThickness);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        var padding = EffectivePadding;
        foreach (var child in Children)
            child.Arrange(new Rect(padding.Left, padding.Top, Math.Max(0, size.Width - padding.HorizontalThickness), Math.Max(0, size.Height - padding.VerticalThickness)));
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