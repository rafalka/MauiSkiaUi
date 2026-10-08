using System.ComponentModel;
using MauiSkiaUi;

namespace MauiSkiaUiSamples.Samples.Controls.Contacts;

internal static class GridCells
{
    /// <summary>Adds <paramref name="view"/> to <paramref name="grid"/> in <paramref name="column"/>.</summary>
    public static void Place(SkUiGrid grid, SkUiView view, int column)
    {
        grid.Children.Add(view);
        Grid.SetColumn(view, column);
    }
}

/// <summary>A contact's photo, or its initials on a colored circle when it has none.</summary>
public sealed class ContactAvatar : SkUiGrid
{
    public ContactAvatar(double size)
    {
        WidthRequest = HeightRequest = size;
        VerticalOptions = LayoutOptions.Center;
        var circle = new SkUiBox { CornerRadius = size / 2 };
        circle.SetBinding(SkUiBox.ColorProperty, static (Contact contact) => contact.AvatarColor);
        var initials = new SkUiLabel
        {
            FontSize = size * 0.38, FontAttributes = FontAttributes.Bold, TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
        };
        initials.SetBinding(SkUiLabel.TextProperty, static (Contact contact) => contact.Initials);
        // Over the initials; a photo from the gallery is cut to a circle as the bundled ones are.
        var photo = new SkUiImage { Aspect = Aspect.AspectFill, DownsampleWidth = size * 2 };
        photo.Transformations.Add(new SkUiCircleTransformation());
        photo.SetBinding(SkUiImage.SourceProperty, static (Contact contact) => contact.Photo);
        Children.Add(circle);
        Children.Add(initials);
        Children.Add(photo);
    }
}

/// <summary>
/// The part of a contact's row or tile shared by both: a long press starts the selection mode with the contact; in that
/// mode a checkbox shows whether it is checked (the list's <c>Selected</c> visual state: the list toggles it on taps).
/// </summary>
internal static class ContactItem
{
    public static SkUiCheckBox? CheckBox(bool selecting) =>
        selecting ? new SkUiCheckBox { InputTransparent = true, VerticalOptions = LayoutOptions.Center } : null;

    /// <summary>Wires the long press and the checkbox of an item view.</summary>
    public static void Wire(SkUiView view, ContactsViewModel model)
    {
        view.LongPressed += (_, _) =>
        {
            if (!model.IsSelecting && view.BindingContext is Contact contact)
                model.BeginSelection([contact]);
        };
        // The item root's state: checked while selected (MAUI's CommonStates group, set by the list).
        VisualStateManager.SetVisualStateGroups(view, [new VisualStateGroup
        {
            Name = "CommonStates",
            States = { new VisualState { Name = "Normal" }, new VisualState { Name = "PointerOver" }, new VisualState { Name = "Selected" } }
        }]);
    }

    public static bool IsSelectedState(VisualElement view) =>
        VisualStateManager.GetVisualStateGroups(view) is [{ CurrentState.Name: "Selected" }, ..];
}

/// <summary>A contact in the list: checkbox (selecting), avatar, name and phone, star.</summary>
public sealed class ContactRow : SkUiGrid
{
    private readonly SkUiCheckBox? _check;

    public ContactRow(ContactsViewModel model, bool selecting)
    {
        Padding = new Thickness(14, 8);
        ColumnSpacing = 12;
        _check = ContactItem.CheckBox(selecting);
        var column = 0;
        if (_check is not null)
        {
            ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            GridCells.Place(this, _check, column++);
        }
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        GridCells.Place(this, new ContactAvatar(44), column++);

        var name = new SkUiLabel { FontSize = 15, TextColor = SampleColors.Ink, MaxLines = 1, LineBreakMode = LineBreakMode.TailTruncation };
        name.SetBinding(SkUiLabel.TextProperty, static (Contact contact) => contact.DisplayName);
        var phone = new SkUiLabel { FontSize = 12, TextColor = SampleColors.Caption };
        phone.SetBinding(SkUiLabel.TextProperty, static (Contact contact) => contact.Phone);
        GridCells.Place(this, new SkUiVerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center, Children = { name, phone } }, column++);

        var star = new SkUiLabel { FontSize = 16, TextColor = Color.FromArgb("#E3A008"), VerticalOptions = LayoutOptions.Center };
        star.SetBinding(SkUiLabel.TextProperty, static (Contact contact) => contact.StarGlyph);
        GridCells.Place(this, star, column);
        ContactItem.Wire(this, model);
    }

    protected override void ChangeVisualState()
    {
        base.ChangeVisualState();
        if (_check is not null)
            _check.IsChecked = ContactItem.IsSelectedState(this);
    }
}

/// <summary>A contact as a tile: avatar, name, star; a checkbox in the corner while selecting.</summary>
public sealed class ContactTile : SkUiGrid
{
    private readonly SkUiCheckBox? _check;

    public ContactTile(ContactsViewModel model, bool selecting)
    {
        Padding = new Thickness(8, 12);
        var name = new SkUiLabel
        {
            FontSize = 13, TextColor = SampleColors.Ink, HorizontalTextAlignment = TextAlignment.Center,
            MaxLines = 2, LineBreakMode = LineBreakMode.TailTruncation
        };
        name.SetBinding(SkUiLabel.TextProperty, static (Contact contact) => contact.DisplayName);
        Children.Add(new SkUiVerticalStackLayout
        {
            Spacing = 8,
            Children = { new ContactAvatar(72) { HorizontalOptions = LayoutOptions.Center }, name }
        });
        var star = new SkUiLabel { FontSize = 16, TextColor = Color.FromArgb("#E3A008"), HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Start };
        star.SetBinding(SkUiLabel.TextProperty, static (Contact contact) => contact.StarGlyph);
        Children.Add(star);
        _check = ContactItem.CheckBox(selecting);
        if (_check is not null)
        {
            _check.HorizontalOptions = LayoutOptions.Start;
            _check.VerticalOptions = LayoutOptions.Start;
            Children.Add(_check);
        }
        ContactItem.Wire(this, model);
    }

    protected override void ChangeVisualState()
    {
        base.ChangeVisualState();
        if (_check is not null)
            _check.IsChecked = ContactItem.IsSelectedState(this);
    }
}

/// <summary>
/// A group's header: checkbox (selecting: all, some or none of the group checked; a tap checks or unchecks the whole
/// group), title, count, and a chevron that turns while the group is collapsed (a tap collapses it outside the selection
/// mode). A long press starts the selection mode with the whole group.
/// </summary>
public sealed class ContactGroupHeader : SkUiGrid
{
    private readonly ContactsViewModel _model;
    private readonly bool _selecting;
    private readonly SkUiLabel _chevron = new() { Text = "⌄", FontSize = 15, TextColor = SampleColors.Caption, VerticalOptions = LayoutOptions.Center };
    private ItemGroup<string, Contact>? _group;

    public ContactGroupHeader(ContactsViewModel model, bool selecting)
    {
        _model = model;
        _selecting = selecting;
        Padding = new Thickness(14, 6);
        ColumnSpacing = 10;
        BackgroundColor = SampleColors.Page.WithAlpha(0.94f);
        var column = 0;
        if (selecting)
        {
            var check = new SkUiCheckBox { IsThreeState = true, InputTransparent = true, VerticalOptions = LayoutOptions.Center };
            check.SetBinding(SkUiCheckBox.CheckStateProperty, static (ItemGroup<string, Contact> group) => group.CheckState);
            ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            GridCells.Place(this, check, column++);
        }
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        var title = new SkUiLabel { FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = SampleColors.Caption, VerticalOptions = LayoutOptions.Center };
        title.SetBinding(SkUiLabel.TextProperty, static (ItemGroup<string, Contact> group) => group.Title);
        var count = new SkUiLabel { FontSize = 12, TextColor = SampleColors.Caption, VerticalOptions = LayoutOptions.Center };
        count.SetBinding(SkUiLabel.TextProperty, static (ItemGroup<string, Contact> group) => group.Count, stringFormat: "{0}");
        GridCells.Place(this, title, column++);
        GridCells.Place(this, count, column++);
        // While selecting a tap checks the group: no chevron (a collapsed group stays collapsed, its count says what is in it).
        if (!selecting)
            GridCells.Place(this, _chevron, column);
        LongPressed += (_, _) =>
        {
            if (!_model.IsSelecting && _group is not null)
                _model.BeginSelection(_group);
        };
    }

    /// <summary>While selecting, a tap (un)checks the group; otherwise the list collapses or expands it.</summary>
    protected override bool HandlesTap => _selecting;

    protected override void OnTapped(SkUiTappedEventArgs args)
    {
        base.OnTapped(args);
        if (_group is not null)
            _model.ToggleGroup(_group);
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (_group is not null)
            ((INotifyPropertyChanged)_group).PropertyChanged -= OnGroupChanged;
        _group = BindingContext as ItemGroup<string, Contact>;
        if (_group is not null)
            ((INotifyPropertyChanged)_group).PropertyChanged += OnGroupChanged;
        ShowExpanded();
    }

    private void OnGroupChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ItemGroup<string, Contact>.IsExpanded))
            ShowExpanded();
    }

    /// <summary>The chevron turns (composite-time: nothing re-records).</summary>
    private void ShowExpanded() => _chevron.Rotation = _group is { IsExpanded: false } ? -90 : 0;
}

/// <summary>
/// A contact's details: avatar, name, a star toggle, phone, email and groups (the binding context is the contact; none
/// shows a hint).
/// </summary>
public sealed class ContactPreview : SkUiGrid
{
    private readonly SkUiVerticalStackLayout _details;
    private readonly SkUiLabel _hint = new()
    {
        Text = "Select a contact", FontSize = 15, TextColor = SampleColors.Caption,
        HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
    };

    public ContactPreview()
    {
        Padding = new Thickness(20);
        var name = new SkUiLabel { FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = SampleColors.Ink, HorizontalTextAlignment = TextAlignment.Center };
        name.SetBinding(SkUiLabel.TextProperty, static (Contact contact) => contact.DisplayName);
        var star = new SkUiButton { FontSize = 14, Padding = new Thickness(14, 6), HorizontalOptions = LayoutOptions.Center };
        star.SetBinding(SkUiButton.TextProperty, static (Contact contact) => contact.IsStarred, converter: new StarTextConverter());
        star.Clicked += (_, _) =>
        {
            if (BindingContext is Contact contact)
                contact.IsStarred = !contact.IsStarred;
        };
        _details = new SkUiVerticalStackLayout
        {
            Spacing = 14,
            Children =
            {
                new ContactAvatar(120) { HorizontalOptions = LayoutOptions.Center },
                name,
                star,
                Field("Phone", out var phone),
                Field("Email", out var email),
                Field("Groups", out var groups)
            }
        };
        // Lambda bindings are compiled where they are written: each field's binding is here.
        phone.SetBinding(SkUiLabel.TextProperty, static (Contact contact) => contact.Phone);
        email.SetBinding(SkUiLabel.TextProperty, static (Contact contact) => contact.Email);
        groups.SetBinding(SkUiLabel.TextProperty, static (Contact contact) => contact.GroupsText);
        Children.Add(_details);
        Children.Add(_hint);
        ShowContact();
    }

    private static SkUiVerticalStackLayout Field(string caption, out SkUiLabel text)
    {
        text = new SkUiLabel { FontSize = 15, TextColor = SampleColors.Ink };
        return new SkUiVerticalStackLayout
        {
            Spacing = 2,
            Children = { new SkUiLabel { Text = caption, FontSize = 12, TextColor = SampleColors.Caption }, text }
        };
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        ShowContact();
    }

    private void ShowContact()
    {
        var shown = BindingContext is Contact;
        _details.IsVisible = shown;
        _hint.IsVisible = !shown;
    }

    private sealed class StarTextConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
            value is true ? "★ Starred" : "☆ Star";

        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
