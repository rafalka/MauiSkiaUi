using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

/// <summary>
/// The geometry of an <see cref="SkUiSheet"/>, with no view types: each detent's height from the room the surface leaves, the content's
/// height and the window's bottom inset; where a drag or fling released at a position comes to rest (a detent, or closed); and which
/// detent a position is nearest. Positions are how much of the sheet shows along its axis, from 0 (closed) to <see cref="MaximumHeight"/>
/// (the tallest detent, also the sheet's own length: lower detents show part of it).
/// </summary>
internal sealed class SkUiSheetEngine
{
    /// <summary>DIPs within which two positions count as the same (a detent reached, a sheet closed).</summary>
    internal const double Tolerance = 0.5;

    private double[] _heights = [];
    private double[] _sorted = [];
    private int _count;

    /// <summary>The number of detents.</summary>
    public int Count => _count;

    /// <summary>The tallest the sheet can be (every detent is capped there).</summary>
    public double Available { get; private set; }

    /// <summary>The tallest detent: the sheet's length along its axis.</summary>
    public double MaximumHeight { get; private set; }

    /// <summary>The lowest detent: the scrim is fully shown from there up.</summary>
    public double LowestHeight { get; private set; }

    /// <summary>Whether the detents have been resolved for a non-empty room.</summary>
    public bool HasGeometry => _count > 0 && MaximumHeight > 0;

    /// <summary>
    /// Resolves <paramref name="detents"/> in <paramref name="available"/> DIPs: a fraction of it, a height plus
    /// <paramref name="bottomInset"/> (the window's inset the sheet covers), or <paramref name="fitContent"/> (the content's height, the inset
    /// included); each capped at <paramref name="available"/>. An empty list is one full detent. Returns whether a height changed.
    /// </summary>
    public bool Resolve(IReadOnlyList<SkUiSheetDetent> detents, double available, double fitContent, double bottomInset)
    {
        available = Math.Max(0, available);
        var count = Math.Max(1, detents.Count);
        if (_heights.Length < count)
        {
            _heights = new double[count];
            _sorted = new double[count];
        }
        var changed = count != _count || Math.Abs(available - Available) >= Tolerance;
        for (var index = 0; index < count; index++)
        {
            var height = detents.Count == 0 ? available : Height(detents[index], available, fitContent, bottomInset);
            changed |= Math.Abs(_heights[index] - height) >= Tolerance;
            _heights[index] = height;
            _sorted[index] = height;
        }
        _count = count;
        Available = available;
        Array.Sort(_sorted, 0, count);
        MaximumHeight = _sorted[count - 1];
        LowestHeight = FirstPositive(_sorted, count, MaximumHeight);
        return changed;

        static double FirstPositive(double[] sorted, int count, double fallback)
        {
            for (var index = 0; index < count; index++)
                if (sorted[index] >= Tolerance)
                    return sorted[index];
            return fallback;
        }
    }

    /// <summary>One detent's height in <paramref name="available"/> DIPs.</summary>
    internal static double Height(SkUiSheetDetent detent, double available, double fitContent, double bottomInset)
    {
        var height = detent.Kind switch
        {
            SkUiSheetDetentKind.Fraction => detent.Value * available,
            SkUiSheetDetentKind.Height => detent.Value + bottomInset,
            _ => fitContent
        };
        return double.IsNaN(height) ? 0 : Math.Clamp(height, 0, available);
    }

    /// <summary>Detent <paramref name="index"/>'s height (the list's order), clamped to the list; 0 without detents.</summary>
    public double HeightOf(int index) => _count == 0 ? 0 : _heights[Math.Clamp(index, 0, _count - 1)];

    /// <summary>
    /// The detent nearest to <paramref name="position"/>: of those as near, the one nearest <paramref name="preferred"/> in the list (the
    /// selected one keeps its place when two detents resolve to the same height).
    /// </summary>
    public int NearestIndex(double position, int preferred = 0)
    {
        var best = -1;
        var bestDistance = double.PositiveInfinity;
        for (var index = 0; index < _count; index++)
        {
            var distance = Math.Abs(_heights[index] - position);
            if (distance < bestDistance - Tolerance
                || (Math.Abs(distance - bestDistance) < Tolerance && Math.Abs(index - preferred) < Math.Abs(best - preferred)))
            {
                best = index;
                bestDistance = Math.Min(distance, bestDistance);
            }
        }
        return Math.Max(0, best);
    }

    /// <summary>
    /// Where a sheet released at <paramref name="position"/> moving at <paramref name="velocity"/> (DIPs per second, positive while it
    /// opens further) comes to rest: below the fling speed, the nearest detent (or 0, closed, when <paramref name="dismissible"/> and
    /// nearer than the lowest detent); faster, the detent in the direction of the motion nearest to where a fling would stop (it may
    /// pass several), the last one that way when the fling would go beyond, and 0 for a fling closing past the lowest detent.
    /// </summary>
    public double RestingPosition(double position, double velocity, bool dismissible)
    {
        if (_count == 0)
            return dismissible ? 0 : position;
        if (Math.Abs(velocity) < SkUiGestureSettings.FlingMinimumVelocity)
        {
            var nearest = _sorted[0];
            for (var index = 1; index < _count; index++)
                if (Math.Abs(_sorted[index] - position) < Math.Abs(nearest - position))
                    nearest = _sorted[index];
            return dismissible && position < Math.Abs(nearest - position) ? 0 : nearest;
        }
        var projected = position + velocity * SkUiRenderFling.TotalTravelPerVelocity;
        double? target = null;
        if (velocity > 0)
        {
            for (var index = 0; index < _count; index++)
                if (_sorted[index] > position + Tolerance && (target is not { } best || Math.Abs(_sorted[index] - projected) < Math.Abs(best - projected)))
                    target = _sorted[index];
            return target ?? MaximumHeight;
        }
        if (dismissible && projected <= 0)
            return 0;
        for (var index = _count - 1; index >= 0; index--)
            if (_sorted[index] < position - Tolerance && _sorted[index] >= Tolerance
                && (target is not { } best || Math.Abs(_sorted[index] - projected) < Math.Abs(best - projected)))
                target = _sorted[index];
        return target ?? (dismissible ? 0 : LowestHeight);
    }

    /// <summary>The detent heights, ascending, the lowest first (snap points), with 0 first when <paramref name="dismissible"/>.</summary>
    public void GetRestingPositions(List<double> positions, bool dismissible)
    {
        if (dismissible)
            positions.Add(0);
        for (var index = 0; index < _count; index++)
            if (_sorted[index] >= Tolerance && (positions.Count == 0 || Math.Abs(positions[^1] - _sorted[index]) >= Tolerance))
                positions.Add(_sorted[index]);
    }

    /// <summary>The next detent above (<paramref name="up"/>) or below <paramref name="position"/>, or <c>null</c> at the end.</summary>
    public double? Step(double position, bool up)
    {
        if (up)
        {
            for (var index = 0; index < _count; index++)
                if (_sorted[index] > position + Tolerance)
                    return _sorted[index];
            return null;
        }
        for (var index = _count - 1; index >= 0; index--)
            if (_sorted[index] < position - Tolerance && _sorted[index] >= Tolerance)
                return _sorted[index];
        return null;
    }
}
