using MauiSkiaUi;

namespace MauiSkiaUiSamples.Samples.Controls.Contacts;

/// <summary>
/// Adds a contact: first and last name (native entries hosted in the drawn page, <see cref="SkUiMauiContentView"/>: drawn
/// text input is not available yet), a photo from the gallery (<see cref="MediaPicker"/>), groups and a star. Shown modally;
/// <see cref="Result"/> completes with the new contact, or <c>null</c> when canceled.
/// </summary>
public sealed class ContactEditorPage : ContentPage
{
    private readonly TaskCompletionSource<Contact?> _result = new();
    private readonly Contact _draft = new() { FirstName = "", LastName = "" };
    private readonly Entry _firstName = new() { Placeholder = "First name", FontSize = 16 };
    private readonly Entry _lastName = new() { Placeholder = "Last name", FontSize = 16 };
    private readonly SkUiCheckBox _family = new() { VerticalOptions = LayoutOptions.Center };
    private readonly SkUiCheckBox _business = new() { VerticalOptions = LayoutOptions.Center };
    private readonly SkUiSwitch _starred = new() { VerticalOptions = LayoutOptions.Center };
    private readonly SkUiButton _save;
    private bool _closed;

    public ContactEditorPage()
    {
        Title = "New contact";
        BackgroundColor = SampleColors.Page;
        _firstName.TextChanged += (_, args) => { _draft.FirstName = args.NewTextValue ?? ""; UpdateSave(); };
        _lastName.TextChanged += (_, args) => { _draft.LastName = args.NewTextValue ?? ""; UpdateSave(); };

        var avatar = new ContactAvatar(96) { HorizontalOptions = LayoutOptions.Center, BindingContext = _draft };
        var choosePhoto = new SkUiButton { Text = "Choose photo…", FontSize = 14, Padding = new Thickness(14, 6), HorizontalOptions = LayoutOptions.Center };
        choosePhoto.Clicked += async (_, _) => await ChoosePhotoAsync();
        _save = new SkUiButton { Text = "Save", Padding = new Thickness(20, 8), IsEnabled = false };
        _save.Clicked += (_, _) => Close(Save());
        var cancel = new SkUiButton { Text = "Cancel", Padding = new Thickness(20, 8), Background = SampleColors.Border, TextColor = SampleColors.Ink };
        cancel.Clicked += (_, _) => Close(null);

        var form = new SkUiVerticalStackLayout
        {
            Spacing = 14,
            Padding = new Thickness(20),
            Children =
            {
                avatar,
                choosePhoto,
                Native(_firstName),
                Native(_lastName),
                Option("Family", _family),
                Option("Business", _business),
                Option("Starred", _starred),
                new SkUiHorizontalStackLayout { Spacing = 12, HorizontalOptions = LayoutOptions.End, Children = { cancel, _save } }
            }
        };
        Content = new SkUiScrollView { Content = form };
    }

    /// <summary>The new contact, or <c>null</c> when canceled.</summary>
    public Task<Contact?> Result => _result.Task;

    /// <summary>A native entry inside the drawn form, with a frame of its own.</summary>
    private static SkUiBorder Native(View entry) => new()
    {
        StrokeThickness = 1,
        Stroke = SampleColors.Border,
        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
        BackgroundColor = SampleColors.Surface,
        Padding = new Thickness(8, 0),
        Content = new SkUiMauiContentView { Content = entry, HeightRequest = 44 }
    };

    private static SkUiGrid Option(string text, SkUiView control)
    {
        var row = new SkUiGrid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
        row.Children.Add(new SkUiLabel { Text = text, FontSize = 16, TextColor = SampleColors.Ink, VerticalOptions = LayoutOptions.Center });
        row.Children.Add(control);
        Grid.SetColumn(control, 1);
        return row;
    }

    private void UpdateSave() => _save.IsEnabled = _draft.DisplayName.Length > 0;

    private async Task ChoosePhotoAsync()
    {
        try
        {
            var photos = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions { Title = "Contact photo", SelectionLimit = 1 });
            if (photos.FirstOrDefault() is not { } photo)
                return;
            // Copied into the app's data: the picked file may be temporary.
            var path = Path.Combine(FileSystem.AppDataDirectory, $"contact_{Guid.NewGuid():N}{Path.GetExtension(photo.FileName)}");
            await using (var source = await photo.OpenReadAsync())
            await using (var target = File.Create(path))
                await source.CopyToAsync(target);
            _draft.Photo = ImageSource.FromFile(path);
        }
        catch (Exception exception) when (exception is FeatureNotSupportedException or PermissionException or IOException)
        {
            await DisplayAlertAsync("Photo", $"No photo could be picked: {exception.Message}", "OK");
        }
    }

    private Contact Save() => new()
    {
        FirstName = _draft.FirstName.Trim(),
        LastName = _draft.LastName.Trim(),
        Photo = _draft.Photo,
        IsStarred = _starred.IsToggled,
        Groups = (_family.IsChecked ? ContactGroups.Family : 0) | (_business.IsChecked ? ContactGroups.Business : 0),
        Phone = "",
        Email = ""
    };

    private async void Close(Contact? contact)
    {
        if (_closed)
            return;
        _closed = true;
        _result.TrySetResult(contact);
        await Navigation.PopModalAsync();
    }

    /// <summary>The system back button cancels.</summary>
    protected override bool OnBackButtonPressed()
    {
        Close(null);
        return true;
    }
}
