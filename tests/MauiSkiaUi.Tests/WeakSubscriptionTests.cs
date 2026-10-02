using System.Windows.Input;
using MauiSkiaUi.Core;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Sources that outlive the views using them (shared image sources, long-lived commands, the app-wide look and color
/// scheme events) never keep those views alive, and still reach the live ones.
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
        image.Dispose();

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

    private sealed class Subscriber
    {
        public int Raised { get; private set; }

        public void OnChanged(object? sender, EventArgs args) => Raised++;
    }
}
