using MauiSkiaUi;

namespace MauiSkiaUiSamples.Samples.Controls;

/// <summary>
/// HOWTO: a sign-in / create-account form that password managers fill and save, then a one-time code step. Each drawn field
/// says what it holds with <see cref="SkUiInputView.AutofillHint"/> (<see cref="SkUiAutofillHints"/>): Android's autofill
/// service sees the surface's fields as one form (D14), iOS reads them as text content types. Signing in calls
/// <see cref="SkUiAutofill.Commit"/>, so Android offers to save or update the password at once, then opens the code step.
/// <para>
/// The code is asked for on a page of its own, as real sign-ins do: a password manager saves what a form holds when it is
/// committed, and a code field in the same form ends up saved as the account's user name (Samsung Pass does so with native
/// fields too). The code page calls <see cref="SkUiAutofill.Cancel"/> when it is done, so the code is never offered for saving.
/// </para>
/// <para>
/// Below each form, the same with MAUI's native <see cref="Entry"/>, to compare what a password manager does with each (the
/// device steps are in docs/design/Testing.md, "Password managers and autofill"). The page is pushed from the samples list:
/// on Android, Shell keeps flyout and tab root pages in a <c>ViewPager2</c> that hides them from autofill, native fields included.
/// </para>
/// </summary>
public sealed class SignInFormSample : SamplePage, ISample
{
    public static SampleInfo Info { get; } = new(
        SampleSection.Controls,
        Title: "Sign-in form",
        Summary: "Drawn fields that password managers fill and save: autofill hints on each field, a commit when the form is " +
                 "submitted, and a one-time code on a step of its own. Native MAUI forms below compare the two.",
        HowTo:
        [
            "Say what each field holds: `<sk:SkUiEntry AutofillHint=\"username\" Keyboard=\"Email\" />`, " +
                "`<sk:SkUiEntry IsPassword=\"True\" AutofillHint=\"password\" />` (constants in `SkUiAutofillHints`: `Username`, " +
                "`Password`, `NewPassword`, `OneTimeCode`, …).",
            "A new account: `AutofillHint=\"newPassword\"` on the new and the confirm field, so managers offer a generated password.",
            "When the sign-in succeeds, call `SkUiAutofill.Commit()` (Android offers to save at once); when it is refused, " +
                "`SkUiAutofill.Cancel()`.",
            "Ask for a one-time code on its own page: `AutofillHint=\"oneTimeCode\"`, `Keyboard=\"Numeric\"`, `MaxLength=\"6\"`; " +
                "call `SkUiAutofill.Cancel()` when it is verified, so nothing is saved from it.",
            "Put the form on a pushed page in a Shell app (`Navigation.PushAsync`, a route), not on a flyout or tab root page."
        ],
        ThingsToKnow:
        [
            "All drawn fields of a surface are one form to Android's autofill service; hosted native fields on the surface keep their own autofill.",
            "A code field in the sign-in form is saved with it, as the user name, by some managers (Samsung Pass): keep it on its own step.",
            "A fill is one undo step and goes through the field's `InputFormatters` and `MaxLength`, as a paste; `Text` follows.",
            "Without a commit, Android services that save once the fields leave the screen still offer to save when the page goes.",
            "iOS offers saved passwords from the key button in the keyboard's bar; saving and strong passwords need the app's " +
                "associated domains (`webcredentials:`), an app setting.",
            "Use a throwaway account (skiaui-test@example.com and a made-up password) when trying it, never a real one."
        ]);

    private const string SignInText = "Sign in";
    private const string CreateText = "Create account";
    private const string VerifyText = "Verify";
    private const string DrawnSource = "drawn form";
    private const string NativeSource = "native form";

    private readonly SkUiEntry _user = Field("Email or user name", SkUiAutofillHints.Username);
    private readonly SkUiEntry _password = Field("Password", SkUiAutofillHints.Password);
    private readonly SkUiEntry _newPassword = Field("New password", SkUiAutofillHints.NewPassword);
    private readonly SkUiEntry _confirmPassword = Field("Confirm new password", SkUiAutofillHints.NewPassword);
    private readonly SkUiButton _submit = new() { Text = SignInText, FillColor = SampleColors.Accent, CornerRadius = 8 };
    private readonly SkUiSwitch _commit = new() { IsToggled = true };
    private readonly SkUiLabel _status = new() { FontSize = 13, TextColor = SampleColors.Caption, LineBreakMode = LineBreakMode.WordWrap };
    private readonly Entry _nativeUser = NativeField("Email or user name", Keyboard.Email);
    private readonly Entry _nativePassword = NativeField("Password", Keyboard.Default, isPassword: true);
    private readonly Button _nativeSubmit = NativeButton(SignInText);

    public SignInFormSample() : base(Info) => SampleContent = Build();

    private View Build()
    {
        _user.Keyboard = Keyboard.Email;
        _password.IsPassword = true;
        _password.ReturnType = ReturnType.Done;
        _newPassword.IsPassword = true;
        _confirmPassword.IsPassword = true;
        _confirmPassword.ReturnType = ReturnType.Done;
        _submit.Clicked += (_, _) => SignIn(_user.Text, DrawnSource);
        _nativeSubmit.Clicked += (_, _) => SignIn(_nativeUser.Text, NativeSource);

        // Sign in or create an account: the password field, or the new-password pair.
        var signIn = new SkUiRadioButton { Content = SignInText, Value = SignInText, IsChecked = true, TextColor = SampleColors.Ink, Color = SampleColors.Accent };
        var create = new SkUiRadioButton { Content = CreateText, Value = CreateText, TextColor = SampleColors.Ink, Color = SampleColors.Accent };
        create.CheckedChanged += (_, args) => ShowMode(args.Value);
        var mode = new SkUiHorizontalStackLayout { Spacing = 16, Children = { signIn, create } };
        RadioButtonGroup.SetGroupName(mode, "mode");
        ShowMode(createAccount: false);

        var commit = new SkUiHorizontalStackLayout
        {
            Spacing = 8,
            Children = { _commit, new SkUiLabel { Text = "Commit autofill on sign in", FontSize = 14, TextColor = SampleColors.Ink, VerticalOptions = LayoutOptions.Center } }
        };
        var drawn = new SkUiVerticalStackLayout
        {
            Spacing = 12,
            Padding = new Thickness(16),
            BackgroundColor = SampleColors.Surface,
            Children = { Heading("Drawn fields (SkUiEntry)"), mode, _user, _password, _newPassword, _confirmPassword, commit, _submit, _status }
        };
        return new VerticalStackLayout { Spacing = 16, Children = { drawn, NativeForm(_nativeUser, _nativePassword, _nativeSubmit) } };
    }

    private static SkUiEntry Field(string placeholder, string hint) => new()
    {
        Placeholder = placeholder,
        AutofillHint = hint,
        ReturnType = ReturnType.Next,
        FontSize = 15,
        TextColor = SampleColors.Ink
    };

    // Native fields with the page's colors: the platform's defaults follow the system theme (white text in dark mode).
    private static Entry NativeField(string placeholder, Keyboard keyboard, bool isPassword = false) => new()
    {
        Placeholder = placeholder,
        Keyboard = keyboard,
        IsPassword = isPassword,
        ReturnType = ReturnType.Next,
        TextColor = SampleColors.Ink,
        PlaceholderColor = SampleColors.Caption
    };

    private static Button NativeButton(string text) => new() { Text = text, BackgroundColor = SampleColors.Accent, TextColor = Colors.White };

    private static View NativeForm(params View[] views)
    {
        var form = new VerticalStackLayout
        {
            Spacing = 12,
            Padding = new Thickness(16),
            BackgroundColor = SampleColors.Surface,
            Children = { new Label { Text = "NATIVE FIELDS (MAUI ENTRY), FOR COMPARISON", FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = SampleColors.Caption } }
        };
        foreach (var view in views)
            form.Children.Add(view);
        return form;
    }

    private static SkUiLabel Heading(string text) =>
        new() { Text = text, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = SampleColors.Caption, TextTransform = TextTransform.Uppercase };

    private void ShowMode(bool createAccount)
    {
        _password.IsVisible = !createAccount;
        _newPassword.IsVisible = createAccount;
        _confirmPassword.IsVisible = createAccount;
        _submit.Text = _nativeSubmit.Text = createAccount ? CreateText : SignInText;
    }

    private async void SignIn(string? user, string source)
    {
        // The app's own sign-in would go here; on success the platform may offer to save what was typed.
        if (_commit.IsToggled)
            SkUiAutofill.Commit();
        _status.Text = $"Signed in from the {source} as \"{user}\".";
        await Push(VerifyPage(user, source));
    }

    /// <summary>The second step: a one-time code, drawn and native, in a form of its own that is never saved.</summary>
    private ContentPage VerifyPage(string? user, string source)
    {
        var code = Field("One-time code", SkUiAutofillHints.OneTimeCode);
        code.Keyboard = Keyboard.Numeric;
        code.MaxLength = 6;
        code.ReturnType = ReturnType.Done;
        var verify = new SkUiButton { Text = VerifyText, FillColor = SampleColors.Accent, CornerRadius = 8 };
        var nativeCode = NativeField("One-time code", Keyboard.Numeric);
        nativeCode.MaxLength = 6;
        var nativeVerify = NativeButton(VerifyText);
        var page = new ContentPage
        {
            Title = "Verify",
            BackgroundColor = SampleColors.Page,
            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Spacing = 16,
                    Padding = new Thickness(16),
                    Children =
                    {
                        new SkUiVerticalStackLayout
                        {
                            Spacing = 12,
                            Padding = new Thickness(16),
                            BackgroundColor = SampleColors.Surface,
                            Children =
                            {
                                Heading("Drawn field (SkUiEntry)"),
                                new SkUiLabel { Text = "Enter the code sent to your phone.", FontSize = 14, TextColor = SampleColors.Ink },
                                code, verify
                            }
                        },
                        NativeForm(nativeCode, nativeVerify)
                    }
                }
            }
        };
        SampleColors.ApplyNavigationBar(page);
        verify.Clicked += async (_, _) => await Verified(page, user, source);
        nativeVerify.Clicked += async (_, _) => await Verified(page, user, source);
        return page;
    }

    private async Task Verified(Page verifyPage, string? user, string source)
    {
        // Nothing on this page is a credential: the platform forgets it instead of offering to save the code.
        SkUiAutofill.Cancel();
        var signOut = NativeButton("Sign out");
        var signedIn = new ContentPage
        {
            Title = "Signed in",
            BackgroundColor = SampleColors.Page,
            Content = new VerticalStackLayout
            {
                Spacing = 16,
                Padding = new Thickness(16),
                Children = { new Label { Text = $"Signed in as \"{user}\" ({source}).", TextColor = SampleColors.Ink }, signOut }
            }
        };
        SampleColors.ApplyNavigationBar(signedIn);
        signOut.Clicked += async (_, _) => await Navigation.PopAsync();
        await Push(signedIn);
        // Signing out goes back to the form, past the code step.
        if (Navigation.NavigationStack.Contains(verifyPage))
            Navigation.RemovePage(verifyPage);
    }

    private async Task Push(Page page)
    {
        try
        {
            await Navigation.PushAsync(page);
        }
        catch (Exception ex)
        {
            _status.Text = $"Navigation failed: {ex.Message}";
        }
    }

    /// <summary>Back from the later steps: an empty form for the next try.</summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        foreach (var field in new[] { _user, _password, _newPassword, _confirmPassword })
            field.Text = string.Empty;
        _nativeUser.Text = _nativePassword.Text = string.Empty;
    }
}
