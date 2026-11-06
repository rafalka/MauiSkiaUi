namespace MauiSkiaUi;

/// <summary>What <see cref="SkUiZoomState"/> moves: the content's composite-time scale and offset, so zooming re-records nothing.</summary>
internal interface ISkUiZoomTarget
{
    /// <summary>Where the content is at scale 1, in the view's coordinates: what it scales about (its center) and whose edges it keeps covering.</summary>
    Rect ZoomContentFrame { get; }

    /// <summary>Applies the content's scale (about its center) and offset.</summary>
    void ApplyZoom(double scale, double x, double y);

    /// <summary>The scale went above 1 or back: a pan only takes the drag while there is something to pan.</summary>
    void OnCanPanChanged(bool canPan);

    /// <summary>The scale changed.</summary>
    void OnZoomChanged();
}

/// <summary>
/// Pinch, pan, double tap and wheel for the zoom views of both layers: a pinch keeps the point under the fingers where it is, and zoomed in, the content's
/// edges never come inside where it was at scale 1.
/// </summary>
internal sealed class SkUiZoomState(ISkUiZoomTarget target)
{
    // A wheel notch (about 100 DIPs) zooms by a third or so.
    private const double WheelUnit = 350;
    private const double Tolerance = 0.001;

    private double _panStartX;
    private double _panStartY;

    /// <summary>The scale a reset goes to: 1, or as close as the limits allow.</summary>
    private double RestingScale => Math.Clamp(1, MinimumScale, Math.Max(MinimumScale, MaximumScale));

    internal double MinimumScale { get; set; } = 1;

    internal double MaximumScale { get; set; } = 5;

    internal double DoubleTapScale { get; set; } = 2.5;

    internal bool IsWheelEnabled { get; set; } = true;

    internal double Scale { get; private set; } = 1;

    internal double X { get; private set; }

    internal double Y { get; private set; }

    internal bool CanPan => Scale > 1 + Tolerance;

    internal void Set(double scale, double x, double y)
    {
        scale = Math.Clamp(scale, MinimumScale, Math.Max(MinimumScale, MaximumScale));
        var frame = target.ZoomContentFrame;
        // At a scale under 1 the content stays centered: the range is empty.
        var rangeX = Math.Max(0, frame.Width * (scale - 1) / 2);
        var rangeY = Math.Max(0, frame.Height * (scale - 1) / 2);
        x = Math.Clamp(x, -rangeX, rangeX);
        y = Math.Clamp(y, -rangeY, rangeY);
        var scaleChanged = Math.Abs(scale - Scale) > double.Epsilon;
        var couldPan = CanPan;
        (Scale, X, Y) = (scale, x, y);
        target.ApplyZoom(scale, x, y);
        if (couldPan != CanPan)
            target.OnCanPanChanged(CanPan);
        if (scaleChanged)
            target.OnZoomChanged();
    }

    /// <summary>The viewport changed: the offset may no longer hold.</summary>
    internal void Reclamp() => Set(Scale, X, Y);

    internal void Reset() => Set(RestingScale, 0, 0);

    /// <summary>Zooms to <paramref name="scale"/>, keeping the point at <paramref name="focus"/> (the view's coordinates) where it is.</summary>
    internal void ZoomAbout(double scale, Point focus)
    {
        scale = Math.Clamp(scale, MinimumScale, Math.Max(MinimumScale, MaximumScale));
        var center = target.ZoomContentFrame.Center;
        var ratio = scale / Scale;
        Set(scale,
            X + (1 - ratio) * (focus.X - center.X - X),
            Y + (1 - ratio) * (focus.Y - center.Y - Y));
    }

    internal void OnPinch(SkUiPinchUpdatedEventArgs args)
    {
        if (args.Status == GestureStatus.Running)
            ZoomAbout(Scale * args.Scale, args.Origin); // the recognizer's origin is in the view's DIPs
    }

    internal void OnPan(SkUiPanUpdatedEventArgs args)
    {
        switch (args.Status)
        {
            case GestureStatus.Started:
                (_panStartX, _panStartY) = (X, Y);
                break;
            case GestureStatus.Running:
                Set(Scale, _panStartX + args.TotalX, _panStartY + args.TotalY);
                break;
        }
    }

    internal void OnDoubleTap(Point position)
    {
        if (Scale > RestingScale + Tolerance)
            Reset();
        else
            ZoomAbout(DoubleTapScale, position);
    }

    /// <summary>Zooms for a wheel turn about the content's center, in when the wheel turns away from the reader (a positive delta); returns what is left for what is around, as a scroller does.</summary>
    internal Point OnWheel(double deltaX, double deltaY)
    {
        if (!IsWheelEnabled)
            return new Point(deltaX, deltaY);
        if (Math.Abs(deltaY) <= double.Epsilon)
            return new Point(deltaX, 0);
        var before = Scale;
        ZoomAbout(Scale * Math.Exp(deltaY / WheelUnit), target.ZoomContentFrame.Center);
        return Math.Abs(Scale - before) > double.Epsilon ? new Point(deltaX, 0) : new Point(deltaX, deltaY);
    }
}
