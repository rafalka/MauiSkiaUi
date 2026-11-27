using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi;

public partial class SkUiSheet
{
    private Track TrackView => _track ??= new Track(this);

    /// <summary>The scroller whose offset is the sheet's position (tests drive wheel input through it).</summary>
    internal SkUiScrollController TrackController => TrackView.Controller;

    private Strip StripView => _strip ??= new Strip(this);

    private Panel PanelView => _panel ??= new Panel(this);

    private Scrim ScrimView => _scrim ??= new Scrim(this);

    /// <summary>Whether the sheet's surface draws frames (render-thread motions run); otherwise the sheet moves at once.</summary>
    private bool SurfaceIsLive => SkUiDiagnostics.GetSurfaceRoot(this)?.HasLiveSurface == true;

    /// <summary>
    /// The sheet's moving part: a scroller over the whole sheet whose offset is how much of the sheet shows, so drags, flings, the springs
    /// to detents and the nested-scroll hand-off are the scroll views'. Its content is the <see cref="Strip"/>: the sheet's own length
    /// past a viewport of nothing. It takes pointers only over the sheet's panel.
    /// </summary>
    private sealed class Track : SkUiScrollView, ISkUiScrollStepper
    {
        private readonly SkUiSheet _owner;
        private SkUiTapGestureRecognizer? _blocker;

        public Track(SkUiSheet owner)
        {
            _owner = owner;
            IsVisible = false;
            FlowDirection = FlowDirection.LeftToRight; // offsets are physical: the sheet resolves its edge itself
            VerticalScrollBarVisibility = ScrollBarVisibility.Never;
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never;
            Overscroll = SkUiOverscrollMode.None;
            SnapPointsType = SnapPointsType.Mandatory;
            Content = owner.StripView;
            var controller = Controller;
            controller.SnapTargetResolver = owner.SnapTarget;
            controller.MovingChanged += owner.OnTrackMovingChanged;
            controller.CameToRest += owner.OnTrackCameToRest; // also wheel / trackpad scrolling, which never moves
            controller.Stepper = this; // arrow and page keys never move the sheet
        }

        public SkUiScrollController Controller => ((ISkUiScrollHost)this).Scroller;

        /// <summary>The panel's rectangle in the track's coordinates now.</summary>
        private Rect PanelBounds
        {
            get
            {
                var frame = _owner.PanelView.Frame;
                return new Rect(frame.X - Controller.X, frame.Y - Controller.Y, frame.Width, frame.Height);
            }
        }

        internal override bool HitsSelfAt(Point local) => PanelBounds.Contains(local);

        protected override void ArrangeContent(Size size)
        {
            base.ArrangeContent(size);
            _owner.OnTrackArranged();
        }

        internal override void CollectGestureRecognizers(List<SkUiGestureRecognizer> recognizers)
        {
            base.CollectGestureRecognizers(recognizers);
            // A sheet that is not dragged still takes the presses on it, which would otherwise reach the scrim.
            if (!Controller.IsUserScrollEnabled || Orientation == ScrollOrientation.Neither)
                recognizers.Add(_blocker ??= new SkUiTapGestureRecognizer());
        }

        /// <summary>Not a scroll view to screen readers: the sheet's parts are read as they are.</summary>
        protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
        {
            info.Role = SkUiSemanticsRole.None;
            info.Actions = SkUiSemanticsActions.None;
        }

        protected override bool OnSemanticsAction(SkUiSemanticsActions action) => false;

        bool ISkUiScrollStepper.CanStep(bool forward) => false;

        bool ISkUiScrollStepper.Step(bool forward, bool page) => false;

        bool ISkUiScrollStepper.StepToEdge(bool end) => false;
    }

    /// <summary>The track's content: a viewport of nothing and the sheet's length, with the panel at the end the sheet comes from.</summary>
    private sealed class Strip : SkUiView
    {
        private readonly SkUiSheet _owner;

        public Strip(SkUiSheet owner)
        {
            _owner = owner;
            AttachChild(owner.PanelView);
        }

        /// <summary>The strip's size, set by the sheet before it lays the track out.</summary>
        public Size Extent
        {
            get;
            set
            {
                if (field == value) // exact: the same size measures the same
                    return;
                field = value;
                ForgetMeasure();
                _owner._track?.ForgetMeasure(); // the track lays the strip out again when the sheet arranges it next
            }
        }

        internal override IEnumerable<ISkUiView> SkiaChildren
        {
            get { yield return _owner.PanelView; }
        }

        internal override bool HitsSelfAt(Point local) => false;

        protected override Size MeasureContent(double widthConstraint, double heightConstraint) => Extent;

        protected override void ArrangeContent(Size size)
        {
            var panel = _owner.PanelView;
            var frame = panel.PlannedFrame;
            ((IView)panel).Measure(frame.Width, frame.Height);
            ((IView)panel).Arrange(frame);
        }
    }

    /// <summary>
    /// The sheet's surface, drawn by the look: the grabber, the header (title and description), the content and the actions, in a column
    /// inside the padding that keeps them clear of the window's insets.
    /// </summary>
    private sealed class Panel : SkUiView
    {
        private readonly SkUiSheet _owner;
        private readonly SkUiGrid _column;
        private SkUiVerticalStackLayout? _header;
        private SkUiLabel? _title;
        private SkUiLabel? _description;
        private SkUiContentView? _actions;
        private View? _content;
        private Thickness _padding;
        private double _grabberBand;
        private bool? _shadowForDialog;

        public Panel(SkUiSheet owner)
        {
            _owner = owner;
            ClipToBounds = true;
            _column = new SkUiGrid
            {
                RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)]
            };
            Grabber = new GrabberView(owner);
            AttachChild(_column);
            AttachChild(Grabber);
        }

        public GrabberView Grabber { get; }

        /// <summary>The title's label (screen readers move to it when a modal sheet opens), or <c>null</c>.</summary>
        public SkUiLabel? TitleLabel => _title;

        /// <summary>Where the strip places the panel.</summary>
        public Rect PlannedFrame { get; private set; }

        public void SetContent(View? content)
        {
            if (ReferenceEquals(_content, content))
                return;
            if (_content is not null)
                _column.Children.Remove((ISkUiView)_content);
            _content = content;
            if (content is not null)
            {
                Grid.SetRow(content, 1);
                _column.Children.Add((ISkUiView)content);
            }
            _owner.OnGeometryChanged();
        }

        public void SetTitle(string? title)
        {
            if (title is null)
            {
                RemoveHeaderPart(ref _title);
                return;
            }
            if (_title is null)
            {
                _title = SkUiPopupParts.Title(title, new SkUiPickerRowStyle(null, SkUiLook.Current.DefaultFontSize, FontAttributes.None, true, 0, null));
                Header.Children.Insert(0, _title);
            }
            _title.Text = title;
            _owner.OnGeometryChanged();
        }

        public void SetDescription(string? description)
        {
            if (description is null)
            {
                RemoveHeaderPart(ref _description);
                return;
            }
            if (_description is null)
            {
                var padding = SkUiLook.Current.PopupItemPadding;
                _description = new SkUiLabel { TextColor = SkUiColors.Muted, Padding = new Thickness(padding.Left, 0, padding.Right, 8) };
                Header.Children.Add(_description);
            }
            _description.Text = description;
            _owner.OnGeometryChanged();
        }

        public void SetActions(View? actions)
        {
            if (actions is null)
            {
                if (_actions is not null)
                {
                    _column.Children.Remove(_actions);
                    _actions.Content = null;
                    _actions = null;
                }
                _owner.OnGeometryChanged();
                return;
            }
            if (_actions is null)
            {
                var padding = SkUiLook.Current.PopupItemPadding;
                // The picker popups' button row: the same padding around the app's row.
                _actions = new SkUiContentView { Padding = new Thickness(padding.Left, 8, padding.Right, 12) };
                Grid.SetRow(_actions, 2);
                _column.Children.Add(_actions);
            }
            _actions.Content = (ISkUiView)actions;
            _owner.OnGeometryChanged();
        }

        private SkUiVerticalStackLayout Header
        {
            get
            {
                if (_header is not null)
                    return _header;
                _header = new SkUiVerticalStackLayout();
                Grid.SetRow(_header, 0);
                _column.Children.Add(_header);
                return _header;
            }
        }

        private void RemoveHeaderPart(ref SkUiLabel? part)
        {
            if (part is null || _header is null)
                return;
            _header.Children.Remove(part);
            part = null;
            if (_header.Children.Count == 0)
            {
                _column.Children.Remove(_header);
                _header = null;
            }
            _owner.OnGeometryChanged();
        }

        /// <summary>
        /// The panel's length along its axis that holds its column with <paramref name="padding"/>, at <paramref name="cross"/> DIPs across:
        /// the height of a bottom sheet or dialog, the width of a side sheet (<paramref name="horizontal"/>).
        /// </summary>
        public double Fit(double cross, Thickness padding, bool horizontal)
        {
            IView column = _column;
            if (horizontal)
                return column.Measure(double.PositiveInfinity, Math.Max(0, cross - padding.VerticalThickness)).Width + padding.HorizontalThickness;
            return column.Measure(Math.Max(0, cross - padding.HorizontalThickness), double.PositiveInfinity).Height + padding.VerticalThickness;
        }

        /// <summary>The sheet laid itself out: the panel goes to <paramref name="frame"/> (in the strip) with <paramref name="padding"/> around its column.</summary>
        public void Layout(Rect frame, Thickness padding, double grabberBand)
        {
            var paddingChanged = padding != _padding || Math.Abs(grabberBand - _grabberBand) >= SkUiSheetEngine.Tolerance;
            PlannedFrame = frame;
            _padding = padding;
            _grabberBand = grabberBand;
            Grabber.IsVisible = grabberBand > 0;
            if (paddingChanged)
                ForgetMeasure();
            var dialog = _owner._isDialog;
            if (_shadowForDialog != dialog)
            {
                _shadowForDialog = dialog;
                Shadow = SkUiLook.Current.GetSheetShadow(dialog)!;
            }
        }

        internal override IEnumerable<ISkUiView> SkiaChildren
        {
            get
            {
                yield return Grabber;
                yield return _column;
            }
        }

        protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
            new(double.IsFinite(widthConstraint) ? widthConstraint : 0, double.IsFinite(heightConstraint) ? heightConstraint : 0);

        protected override void ArrangeContent(Size size)
        {
            var inner = new Rect(_padding.Left, _padding.Top, Math.Max(0, size.Width - _padding.HorizontalThickness),
                Math.Max(0, size.Height - _padding.VerticalThickness));
            ((IView)_column).Measure(inner.Width, inner.Height);
            ((IView)_column).Arrange(inner);
            var band = _grabberBand;
            var grabber = _owner._physical switch
            {
                _ when _owner._isDialog => Rect.Zero,
                SkUiSheetEdgePhysical.Left => new Rect(size.Width - band, 0, band, size.Height),
                SkUiSheetEdgePhysical.Right => new Rect(0, 0, band, size.Height),
                _ => new Rect(0, 0, size.Width, band)
            };
            ((IView)Grabber).Measure(grabber.Width, grabber.Height);
            ((IView)Grabber).Arrange(grabber);
        }

        protected override void OnPaintBackground(SKCanvas canvas)
        {
            var look = SkUiLook.Current;
            var radius = _owner.CornerRadius < 0 ? look.DefaultSheetCornerRadius : _owner.CornerRadius;
            var corners = _owner._isDialog ? new CornerRadius(radius) : _owner._physical switch
            {
                SkUiSheetEdgePhysical.Left => new CornerRadius(0, radius, 0, radius),
                SkUiSheetEdgePhysical.Right => new CornerRadius(radius, 0, radius, 0),
                _ => new CornerRadius(radius, radius, 0, 0)
            };
            var color = _owner.BackgroundColor ?? (_owner.Background as SolidColorBrush)?.Color ?? SkUiColors.DefaultBackground;
            look.DrawSheet(canvas, new SkUiSheetPaint(new SKRect(0, 0, (float)Width, (float)Height), ToSkColor(color), corners, _owner._isDialog,
                _owner.IsEnabled));
        }

        /// <summary>The panel's shape for its shadow: the drawn corners.</summary>
        internal override CornerRadius PressEffectCornerRadii
        {
            get
            {
                var radius = _owner.CornerRadius < 0 ? SkUiLook.Current.DefaultSheetCornerRadius : _owner.CornerRadius;
                return _owner._isDialog ? new CornerRadius(radius) : default;
            }
        }
    }

    /// <summary>
    /// The handle at the sheet's inner edge, drawn by the look: a button for screen readers that moves the sheet to its next detent
    /// (round from the tallest to the lowest), with increment and decrement for the next detent up or down. A tap does the same.
    /// </summary>
    internal sealed class GrabberView : SkUiView
    {
        private readonly SkUiSheet _owner;

        public GrabberView(SkUiSheet owner)
        {
            _owner = owner;
            IsTabStop = false; // keyboard users have the content first, and Escape; screen readers still reach it
        }

        protected override bool HandlesTap => true;

        protected override void OnTapped(SkUiTappedEventArgs args)
        {
            RaiseTapped(args);
            _owner.StepDetent(up: true, wraps: true);
        }

        protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
        {
            base.OnPopulateSemantics(info);
            info.Role = SkUiSemanticsRole.Button;
            info.Text = SkUiStrings.Current.SheetGrabberText;
            info.Actions |= SkUiSemanticsActions.Activate | SkUiSemanticsActions.Increment | SkUiSemanticsActions.Decrement;
        }

        protected override bool OnSemanticsAction(SkUiSemanticsActions action) => action switch
        {
            SkUiSemanticsActions.Activate => _owner.StepDetent(up: true, wraps: true),
            SkUiSemanticsActions.Increment => _owner.StepDetent(up: true, wraps: false),
            SkUiSemanticsActions.Decrement => _owner.StepDetent(up: false, wraps: false),
            _ => base.OnSemanticsAction(action)
        };

        public void TakeAutomationId(string? id, string suffix)
        {
            if (AutomationId is null && !string.IsNullOrEmpty(id))
                AutomationId = id + suffix;
        }

        protected override void OnPaintContent(SKCanvas canvas) =>
            SkUiLook.Current.DrawSheetGrabber(canvas, new SkUiSheetGrabberPaint(new SKRect(0, 0, (float)Width, (float)Height),
                ToSkColor(SkUiColors.Muted.WithAlpha(0.4f)), _owner._physical != SkUiSheetEdgePhysical.Bottom, IsPressed));
    }

    /// <summary>
    /// The scrim over the page behind a modal sheet: it takes every pointer there (a tap closes the sheet with
    /// <see cref="DismissOnScrimTap"/>), and its opacity follows how far the sheet shows, up to the lowest detent, on the render thread.
    /// </summary>
    private sealed class Scrim : SkUiView
    {
        private readonly SkUiSheet _owner;
        private SkUiRenderLink[]? _links;
        private (SkUiRenderNode? Source, double Closed, double Lowest, bool Horizontal) _linked;

        public Scrim(SkUiSheet owner)
        {
            _owner = owner;
            IsVisible = false;
            IsTabStop = false;
            AutomationProperties.SetIsInAccessibleTree(this, false);
        }

        protected override bool HandlesTap => true;

        protected override void OnTapped(SkUiTappedEventArgs args)
        {
            RaiseTapped(args);
            if (_owner.DismissOnScrimTap)
                _owner.Dismiss();
        }

        public void TakeAutomationId(string? id, string suffix)
        {
            if (AutomationId is null && !string.IsNullOrEmpty(id))
                AutomationId = id + suffix;
        }

        /// <summary>The geometry changed: the channel is built again for it.</summary>
        public void ForgetLinks()
        {
            _links = null;
            InvalidateRender(SkUiRenderDirty.Props);
        }

        internal override void ReleaseDrawingResources()
        {
            base.ReleaseDrawingResources();
            _links = null; // linked to the track's render node, which is new after a reset
        }

        internal override void OnGetRenderProps(ref SkUiRenderProps props)
        {
            if (_owner._isDialog || !_owner._engine.HasGeometry || _owner._track is not { } track)
                return;
            var source = ((ISkUiRenderable)track).RenderState.Node;
            var key = (source, _owner.OffsetFor(0), _owner.OffsetFor(_owner._engine.LowestHeight), _owner.Horizontal);
            if (_links is not { } links || _linked != key)
            {
                links = _links = [new SkUiRenderLink(source, key.Item4, SkUiRenderLinkTarget.Opacity, (float)key.Item2, (float)key.Item3, 0, 1)];
                _linked = key;
            }
            props.Links = links;
            var input = track.Controller.LinkInput;
            props.LinkValues = SkUiRenderLinkValues.Evaluate(links, input.X, input.Y);
        }

        protected override void OnPaintBackground(SKCanvas canvas)
        {
            var color = _owner.ScrimColor ?? SkUiLook.Current.SheetScrimColor;
            if (color.Alpha <= 0)
                return;
            using var paint = SkUiScratchPaint.Rent(ToSkColor(color));
            canvas.DrawRect(new SKRect(0, 0, (float)Width, (float)Height), paint.Paint);
        }
    }
}
