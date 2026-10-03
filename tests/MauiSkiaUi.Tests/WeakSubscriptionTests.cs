using System.Windows.Input;
using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Sources that outlive the views using them (shared image sources, long-lived commands, the app-wide look and color
/// scheme events, an app's own models and events through the public <see cref="SkUiWeakListener{TTarget}"/> and
/// <see cref="SkUiWeakEvent"/>) never keep those views alive, and still reach the live ones.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public class WeakSubscriptionTests
{
    private static void Collect(IReadOnlyCollection<WeakReference> references)
    {
        for (var attempt = 0; attempt < 5 && references.Any(reference => reference.IsAlive); attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    [Fact]
    public void SharedImageSourcesDoNotKeepImagesAlive()
    {
        // An app resource, as <FontImageSource x:Key="Icon" .../>; the images are never disposed.
        var icon = new FontImageSource { Glyph = "A", Size = 12, Color = Colors.Red };
        var references = Create(icon);
        Collect(references);
        Assert.All(references, reference => Assert.False(reference.IsAlive));

        // A live image still reloads when the shared source changes.
        var image = new SkUiImage { Source = icon };
        var changes = 0;
        image.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(SkUiImage.ImageSize)) changes++; };
        icon.Glyph = "WW";
        Assert.True(changes > 0);

        static WeakReference[] Create(ImageSource icon)
        {
            var references = new List<WeakReference>();
            for (var index = 0; index < 4; index++)
            {
                references.Add(new WeakReference(new SkUiImage { Source = icon }));
                references.Add(new WeakReference(new SkUiImageButton { Source = icon }));
                references.Add(new WeakReference(new SkUiSlider { ThumbImageSource = icon }));
            }
            return [.. references];
        }
    }

    [Fact]
    public void LongLivedCommandsDoNotKeepButtonsAlive()
    {
        var canExecute = true;
        var command = new Command(() => { }, () => canExecute);
        var references = Create(command);
        Collect(references);
        Assert.All(references, reference => Assert.False(reference.IsAlive));

        // A live button follows the command, through the command's single subscription.
        var button = new SkUiButton { Command = command };
        var core = new SkUiCoreButton().SetCommand(command);
        var (paints, corePaints) = (0, 0);
        button.PaintInvalidated += (_, _) => paints++;
        core.PaintInvalidated += (_, _) => corePaints++;
        canExecute = false;
        command.ChangeCanExecute();
        Assert.Equal((1, 1), (paints, corePaints));
        GC.KeepAlive(button);
        GC.KeepAlive(core);

        static WeakReference[] Create(ICommand command)
        {
            var references = new List<WeakReference>();
            for (var index = 0; index < 4; index++)
            {
                references.Add(new WeakReference(new SkUiButton { Command = command }));
                references.Add(new WeakReference(new SkUiImageButton { Command = command }));
                references.Add(new WeakReference(new SkUiCoreButton().SetCommand(command)));
                references.Add(new WeakReference(new SkUiCoreImageButton().SetCommand(command)));
            }
            return [.. references];
        }
    }

    [Fact]
    public void LookAndSchemeEventsDoNotKeepSubscribersAlive()
    {
        var forgotten = Subscribe();
        Collect([forgotten]);
        Assert.False(forgotten.IsAlive);

        // Live subscribers are raised: an object's method, a lambda that captures locals (held strongly, or it would
        // vanish), and a static lambda; unsubscribing still works.
        var subscriber = new Subscriber();
        SkUiLook.CurrentChanged += subscriber.OnChanged;
        var captured = 0;
        EventHandler closure = (_, _) => captured++;
        SkUiLook.CurrentChanged += closure;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        SkUiLook.NotifyChanged();
        Assert.Equal((1, 1), (subscriber.Raised, captured));
        SkUiLook.CurrentChanged -= subscriber.OnChanged;
        SkUiLook.CurrentChanged -= closure;
        SkUiLook.NotifyChanged();
        Assert.Equal((1, 1), (subscriber.Raised, captured));

        var scheme = new LightSkUiColorScheme();
        scheme.Changed += subscriber.OnChanged;
        scheme.Accent = Colors.Red;
        Assert.Equal(2, subscriber.Raised);
        scheme.Changed -= subscriber.OnChanged;

        static WeakReference Subscribe()
        {
            var subscriber = new Subscriber();
            SkUiLook.CurrentChanged += subscriber.OnChanged;
            SkUiColorScheme.CurrentChanged += subscriber.OnChanged;
            return new WeakReference(subscriber);
        }
    }

    [Fact]
    public void CustomCoreNodesListenToSharedSourcesWeakly()
    {
        var model = new ChartModel();
        var forgotten = Create(model);
        Collect([forgotten]);
        Assert.False(forgotten.IsAlive);

        var legend = new LegendNode().SetModel(model);
        model.Series = 3;
        Assert.Equal([nameof(ChartModel.Series)], legend.Changes);
        var other = new ChartModel();
        legend.SetModel(other); // re-listening leaves the previous source
        model.Series = 4;
        other.Series = 1;
        Assert.Equal(2, legend.Changes.Count);
        legend.SetModel(null);
        other.Series = 2;
        Assert.Equal(2, legend.Changes.Count);

        static WeakReference Create(ChartModel model) => new(new LegendNode().SetModel(model));
    }

    [Fact]
    public void ListenersReportTheKindOfChange()
    {
        var changes = new List<SkUiChange>();
        var commandListener = new SkUiWeakListener<List<SkUiChange>>(changes, static (list, change) => list.Add(change));
        var collectionListener = new SkUiWeakListener<List<SkUiChange>>(changes, static (list, change) => list.Add(change));
        var command = new Command(() => { });
        var items = new System.Collections.ObjectModel.ObservableCollection<int>();
        commandListener.Listen(command);
        collectionListener.Listen(items);
        Assert.Same(command, commandListener.Source);
        command.ChangeCanExecute();
        items.Add(1);
        // A collection that is also INotifyPropertyChanged reports both (ObservableCollection: Count and Item[]).
        Assert.Equal([SkUiChangeKind.CanExecute, SkUiChangeKind.Property, SkUiChangeKind.Property, SkUiChangeKind.Collection], changes.Select(change => change.Kind));
        Assert.Equal("Count", changes[1].PropertyName);
        Assert.Same(command, changes[0].Sender);
        Assert.Throws<ArgumentNullException>(() => new SkUiWeakListener<object>(null!, static (_, _) => { }));
    }

    [Fact]
    public void GenericWeakEventsDeliverTheirArguments()
    {
        var weakEvent = new SkUiWeakEvent<int>();
        var forgotten = Subscribe(weakEvent);
        Collect([forgotten]);
        Assert.False(forgotten.IsAlive);

        var received = new List<int>();
        var subscriber = new ValueSubscriber(received);
        weakEvent.Add(subscriber.OnValue);
        EventHandler<int> closure = (_, value) => received.Add(-value);
        weakEvent.Add(closure + subscriber.OnValue); // a multicast handler subscribes each part
        GC.Collect();
        GC.WaitForPendingFinalizers();
        weakEvent.Raise(this, 5);
        Assert.Equal([5, -5, 5], received);
        weakEvent.Remove(closure + subscriber.OnValue);
        weakEvent.Raise(this, 7);
        Assert.Equal([5, -5, 5, 7], received);
        GC.KeepAlive(subscriber);

        static WeakReference Subscribe(SkUiWeakEvent<int> weakEvent)
        {
            var subscriber = new ValueSubscriber([]);
            weakEvent.Add(subscriber.OnValue);
            return new WeakReference(subscriber);
        }
    }

    /// <summary>An app's own Core node that follows a shared view model, as in the <c>SkUiWeakListener</c> docs.</summary>
    private sealed class LegendNode : SkUiCoreNode
    {
        private readonly SkUiWeakListener<LegendNode> _modelListener;

        public LegendNode() => _modelListener = new(this, static (node, change) =>
        {
            if (change.PropertyName is { } name) node.Changes.Add(name);
            node.InvalidateMeasure();
        });

        public List<string> Changes { get; } = [];

        public LegendNode SetModel(ChartModel? model)
        {
            _modelListener.Listen(model);
            InvalidateMeasure();
            return this;
        }
    }

    private sealed class ChartModel : System.ComponentModel.INotifyPropertyChanged
    {
        private int _series;

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public int Series
        {
            get => _series;
            set { _series = value; PropertyChanged?.Invoke(this, new(nameof(Series))); }
        }
    }

    private sealed class ValueSubscriber(List<int> received)
    {
        public void OnValue(object? sender, int value) => received.Add(value);
    }

    private sealed class Subscriber
    {
        public int Raised { get; private set; }

        public void OnChanged(object? sender, EventArgs args) => Raised++;
    }
}
