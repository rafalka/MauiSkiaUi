using System.Collections.ObjectModel;
using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiSwipeView"/> rows in an <see cref="SkUiCollectionView"/>, next to MAUI's <see cref="SwipeView"/> rows in
/// a <see cref="CollectionView"/>, on the same messages: swipe a row left to archive or delete it, right to flag it;
/// vertical drags scroll, taps still select. Scrolling closes an open row; a recycled row shows closed.
/// </summary>
public sealed class SwipeViewListDemoPage : ComponentDemoPage
{
    private const string Flag = "Flag";
    private const string Unflag = "Unflag";
    private const string Archive = "Archive";
    private const string Delete = "Delete";
    private const string FlaggedMark = "⚑ ";
    private static readonly Color FlagColor = Color.FromArgb("#EF6C00");
    private static readonly Color ArchiveColor = Color.FromArgb("#546E7A");
    private static readonly string[] Senders = ["Ada", "Grace", "Linus", "Margaret", "Ken", "Barbara", "Dennis", "Frances"];
    private static readonly string[] Subjects = ["Lunch on Friday?", "Build is green again", "Review: swipe actions", "Slides for the demo",
        "Re: memory numbers", "Trip photos", "Release notes draft", "Standup moved"];

    private readonly SkUiCollectionView _skia;
    private readonly CollectionView _native;
    private readonly ObservableCollection<Message> _messages = [];
    private SwipeMode _deleteMode;
    private string _last = "";

    public SwipeViewListDemoPage() : base("SkUiSwipeView rows", new SkUiCollectionView(), new CollectionView(),
        widthRange: (200, 400, 300), heightRange: (160, 600, 360))
    {
        _skia = (SkUiCollectionView)SkiaControl;
        _native = (CollectionView)NativeControl!;
        Fill();
        _skia.ItemsSource = _messages;
        _native.ItemsSource = _messages;
        _skia.ItemTemplate = new DataTemplate(() => new SkiaRow(this));
        _native.ItemTemplate = new DataTemplate(() => new NativeRow(this));
        _skia.SelectionMode = SkUiSelectionMode.Single;
        _native.SelectionMode = SelectionMode.Single;

        Choice("Delete " + nameof(SwipeItems.Mode), [SwipeMode.Reveal, SwipeMode.Execute], SwipeMode.Reveal,
            value => _deleteMode = value, () => _deleteMode);
        ActionButton("Restore messages", Fill);
    }

    private sealed record Message(int Id, string From, string Subject, bool Flagged = false);

    private void Fill()
    {
        _messages.Clear();
        for (var index = 0; index < 40; index++)
            _messages.Add(new Message(index, Senders[index % Senders.Length], Subjects[index * 3 % Subjects.Length]));
        Report($"{_messages.Count} messages");
    }

    private void ToggleFlag(Message message)
    {
        var index = _messages.IndexOf(message);
        if (index >= 0)
            _messages[index] = message with { Flagged = !message.Flagged };
        Report($"{(message.Flagged ? Unflag : Flag)} {message.From}");
    }

    private void Remove(Message message, string action)
    {
        _messages.Remove(message);
        Report($"{action} {message.From} · {_messages.Count} left");
    }

    private void Report(string text)
    {
        _last = text;
        Feedback(_last, _last);
    }

    private static string Heading(Message message) => (message.Flagged ? FlaggedMark : "") + message.From;

    /// <summary>A drawn row: MAUI's <see cref="SwipeItem"/>s acting on the row's message.</summary>
    private sealed class SkiaRow : SkUiSwipeView
    {
        private readonly SwipeItem _flag = new() { Text = Flag, BackgroundColor = FlagColor };
        private readonly SwipeItems _right;
        private readonly SkUiLabel _from = new() { FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Ink };
        private readonly SkUiLabel _subject = new() { FontSize = 13, TextColor = DemoColors.Caption };

        public SkiaRow(SwipeViewListDemoPage page)
        {
            var archive = new SwipeItem { Text = Archive, BackgroundColor = ArchiveColor };
            var delete = new SwipeItem { Text = Delete, BackgroundColor = DemoColors.SampleA };
            _flag.Invoked += (_, _) => { if (BindingContext is Message message) page.ToggleFlag(message); };
            archive.Invoked += (_, _) => { if (BindingContext is Message message) page.Remove(message, Archive); };
            delete.Invoked += (_, _) => { if (BindingContext is Message message) page.Remove(message, Delete); };
            LeftItems = [_flag];
            _right = [archive, delete];
            RightItems = _right;
            SwipeStarted += (_, _) => _right.Mode = page._deleteMode; // in execute mode a full swipe archives
            Content = new SkUiVerticalStackLayout
            {
                Background = Colors.White,
                Padding = new Thickness(16, 10),
                Children = { _from, _subject }
            };
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is not Message message)
                return;
            _from.Text = Heading(message);
            _subject.Text = message.Subject;
            _flag.Text = message.Flagged ? Unflag : Flag;
        }
    }

    /// <summary>The same row with MAUI's views.</summary>
    private sealed class NativeRow : SwipeView
    {
        private readonly SwipeItem _flag = new() { Text = Flag, BackgroundColor = FlagColor };
        private readonly SwipeItems _right;
        private readonly Label _from = new() { FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Ink };
        private readonly Label _subject = new() { FontSize = 13, TextColor = DemoColors.Caption };

        public NativeRow(SwipeViewListDemoPage page)
        {
            var archive = new SwipeItem { Text = Archive, BackgroundColor = ArchiveColor };
            var delete = new SwipeItem { Text = Delete, BackgroundColor = DemoColors.SampleA };
            _flag.Invoked += (_, _) => { if (BindingContext is Message message) page.ToggleFlag(message); };
            archive.Invoked += (_, _) => { if (BindingContext is Message message) page.Remove(message, Archive); };
            delete.Invoked += (_, _) => { if (BindingContext is Message message) page.Remove(message, Delete); };
            LeftItems = [_flag];
            _right = [archive, delete];
            RightItems = _right;
            SwipeStarted += (_, _) => _right.Mode = page._deleteMode;
            Content = new VerticalStackLayout
            {
                Background = Colors.White,
                Padding = new Thickness(16, 10),
                Children = { _from, _subject }
            };
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is not Message message)
                return;
            _from.Text = Heading(message);
            _subject.Text = message.Subject;
            _flag.Text = message.Flagged ? Unflag : Flag;
        }
    }
}
