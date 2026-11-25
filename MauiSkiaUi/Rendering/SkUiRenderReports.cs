namespace MauiSkiaUi.Rendering;

/// <summary>What a render-thread animation tells the UI thread about a frame: the values it shows (<see cref="SkUiRenderAnimation.TakeReport"/>).</summary>
/// <param name="X">First value: a scroll motion's horizontal offset (without scroll corrections).</param>
/// <param name="Y">Second value: a scroll motion's vertical offset (without scroll corrections).</param>
/// <param name="OverscrollX">Distance shown past the horizontal edges.</param>
/// <param name="OverscrollY">Distance shown past the vertical edges.</param>
internal readonly record struct SkUiRenderReport(float X, float Y, float OverscrollX = 0, float OverscrollY = 0);

/// <summary>
/// The UI-thread feedback of one rendered frame: animation reports and completions, in order. Filled on the render thread,
/// posted to the UI thread as one delegate that is created once, delivered there and pooled again
/// (<see cref="SkUiCompositor"/>), so a frame of a running motion allocates nothing (NFR-2).
/// </summary>
internal sealed class SkUiRenderReports
{
    private readonly SkUiCompositor _owner;
    private readonly List<Entry> _entries = [];

    internal SkUiRenderReports(SkUiCompositor owner)
    {
        _owner = owner;
        Deliver = DeliverEntries;
    }

    /// <summary>UI thread: hands the entries over and returns the batch to its pool.</summary>
    internal Action Deliver { get; }

    /// <summary>Render thread: an animation's values this frame.</summary>
    internal void AddReport(SkUiRenderAnimation animation, in SkUiRenderReport report) =>
        _entries.Add(new Entry(animation, EntryKind.Report, report));

    /// <summary>Render thread: an animation ended.</summary>
    internal void AddFinished(SkUiRenderAnimation animation, bool completed) =>
        _entries.Add(new Entry(animation, completed ? EntryKind.Completed : EntryKind.Cancelled, default));

    /// <summary>Render thread: a reveal's timed move ended.</summary>
    internal void AddMoveEnded(SkUiRenderReveal reveal) => _entries.Add(new Entry(reveal, EntryKind.MoveEnded, default));

    private void DeliverEntries()
    {
        try
        {
            foreach (var entry in _entries)
                entry.Deliver();
        }
        finally
        {
            _entries.Clear(); // keeps no animation (and what its callbacks capture) alive in the pool
            _owner.ReturnReports(this);
        }
    }

    private enum EntryKind
    {
        Report,
        Completed,
        Cancelled,
        MoveEnded
    }

    private readonly record struct Entry(object Source, EntryKind Kind, SkUiRenderReport Report)
    {
        public void Deliver()
        {
            switch (Kind)
            {
                case EntryKind.Report:
                    ((SkUiRenderAnimation)Source).DeliverReport(Report);
                    break;
                case EntryKind.MoveEnded:
                    ((SkUiRenderReveal)Source).ReportMoveEnded();
                    break;
                default:
                    ((SkUiRenderAnimation)Source).NotifyFinished(Kind == EntryKind.Completed);
                    break;
            }
        }
    }
}
