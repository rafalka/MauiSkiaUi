using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUiDemo;

/// <summary>
/// Accessibility playground (P10): a drawn form read by TalkBack / VoiceOver / Narrator (MAUI <c>SemanticProperties</c>,
/// tappable groups, a scroller, a hosted Entry, Core nodes), keyboard focus (Tab, Space / Enter, arrows, <c>Focus()</c>),
/// the app-wide text prescale (<see cref="SkUiLook.FontScale"/>, on top of the system text size) and the semantics tree the screen readers get, live.
/// The buttons share one click handler that reads their <see cref="SkUiView.Tag"/>.
/// </summary>
public sealed class AccessibilityPage : ContentPage
{
    private static readonly string[] _scaleNames = ["Text scale 85 %", "Text scale 100 %", "Text scale 130 %", "Text scale 160 %", "Text scale 200 %"];
    private static readonly double[] _scaleFactors = [0.85, 1, 1.3, 1.6, 2];

    private readonly SkUiVerticalStackLayout _surface;
    private readonly SkUiButton _continue;
    private readonly Label _status;
    private readonly Label _action;
    private readonly Label _tree;
    private IDispatcherTimer? _timer;

    public AccessibilityPage()
    {
        Title = "Accessibility";
        Background = DemoColors.PageBackground;
        AutomationId = "AccessibilityPage";

        var description = Caption(
            "The drawn form below is read by TalkBack, VoiceOver and Narrator: turn one on, or use Tab, Shift+Tab, Space / Enter " +
            "and the arrow keys with a hardware keyboard. The tree on the right (below on phones) is what they get. Text scale " +
            "(SkUiLook.FontScale) prescales all drawn text in the app, on top of the system text size, until it is set back to 100 %.");

        var scale = new Picker
        {
            Title = "Text scale",
            ItemsSource = _scaleNames,
            SelectedIndex = Math.Max(0, Array.IndexOf(_scaleFactors, SkUiLook.Current.FontScale)),
            AutomationId = "TextScalePicker",
            TextColor = DemoColors.Ink,
            FontFamily = DemoFonts.OpenSansRegular
        };
        scale.SelectedIndexChanged += (_, _) => SkUiLook.Current.FontScale = _scaleFactors[Math.Max(0, scale.SelectedIndex)];

        _status = Caption("Focus: none");
        _status.AutomationId = "AccessibilityStatus";
        _action = Caption("Last action: none");
        _action.AutomationId = "AccessibilityAction";
        _tree = new Label
        {
            FontFamily = DemoFonts.RobotoMono,
            FontSize = 11,
            TextColor = DemoColors.Ink,
            LineBreakMode = LineBreakMode.WordWrap,
            VerticalOptions = LayoutOptions.Start,
            AutomationId = "SemanticsTree"
        };

        _surface = new SkUiVerticalStackLayout { Spacing = 10, Padding = new Thickness(16), BackgroundColor = Colors.White, AutomationId = "AccessibilitySurface" };
        _continue = Button("Continue", "continue");
        BuildForm();

        var focusFirst = new Button { Text = "Focus Continue", AutomationId = "FocusContinue" };
        focusFirst.Clicked += (_, _) => _action.Text = $"Last action: Focus() returned {_continue.Focus()}";
        var unfocus = new Button { Text = "Unfocus", AutomationId = "UnfocusDrawn" };
        unfocus.Clicked += (_, _) =>
        {
            if (_surface.FocusManagerIfCreated?.Focused is VisualElement element)
                element.Unfocus();
            else if (_surface.FocusManagerIfCreated?.Focused is SkUiCoreNode core)
                core.Unfocus();
        };
        var announce = new Button { Text = "Read Continue", AutomationId = "SemanticFocusContinue" };
        announce.Clicked += (_, _) => _continue.SetSemanticFocus();

        // App-wide switch (SkUiAccessibility.IsEnabled): off, the drawn form is one native view to screen readers and takes no
        // keyboard focus; text scaling stays.
        var enabled = new Switch { IsToggled = SkUiAccessibility.IsEnabled, AutomationId = "DrawnAccessibilitySwitch" };
        enabled.Toggled += (_, args) => SkUiAccessibility.IsEnabled = args.Value;
        var enabledRow = new HorizontalStackLayout
        {
            Spacing = 8,
            Children = { enabled, new Label { Text = "Drawn accessibility (app-wide)", TextColor = DemoColors.Ink, VerticalOptions = LayoutOptions.Center } }
        };
        var tools = new HorizontalStackLayout { Spacing = 8, Children = { focusFirst, unfocus, announce } };
        var form = new Border
        {
            StrokeThickness = 1,
            Stroke = DemoColors.Border,
            Content = _surface
        };
        var tree = new ScrollView { Orientation = ScrollOrientation.Both, Content = _tree, HeightRequest = 520 };
        var columns = new Grid
        {
            ColumnSpacing = 16,
            RowSpacing = 16,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }
        };
        columns.Add(form, 0, 0);
        columns.Add(tree, 1, 0);
        SizeChanged += (_, _) =>
        {
            var wide = Width >= 700;
            columns.ColumnDefinitions[1].Width = wide ? GridLength.Star : new GridLength(0);
            Grid.SetColumn(tree, wide ? 1 : 0);
            Grid.SetRow(tree, wide ? 0 : 1);
            if (columns.RowDefinitions.Count == 0)
                columns.RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)];
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16),
                Spacing = 12,
                // The picker last: on Mac Catalyst it opens its selection sheet when Tab reaches it, which keeps Tab inside.
                Children = { description, enabledRow, tools, _status, _action, columns, scale }
            }
        };
    }

    private void BuildForm()
    {
        var heading = new SkUiLabel { Text = "Delivery options", FontSize = 22, FontFamily = DemoFonts.OpenSansSemibold, TextColor = DemoColors.Ink };
        SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level1);
        Add(heading);
        Add(new SkUiLabel
        {
            Text = "Choose how your order arrives. Every control here is drawn; screen readers and the keyboard reach all of them.",
            FontFamily = DemoFonts.OpenSansRegular,
            TextColor = DemoColors.Caption
        });

        // Toggles labelled by a description; the labels beside them are decoration (read once, as the toggle's name).
        var gift = new SkUiCheckBox();
        SemanticProperties.SetDescription(gift, "Gift wrap");
        Add(Row(gift, Decoration("Gift wrap")));

        var express = new SkUiSwitch();
        SemanticProperties.SetDescription(express, "Express delivery");
        SemanticProperties.SetHint(express, "Delivers the next working day");
        Add(Row(express, Decoration("Express delivery")));

        var method = new SkUiLabel { Text = "Delivery method", FontFamily = DemoFonts.OpenSansSemibold, TextColor = DemoColors.Ink };
        SemanticProperties.SetHeadingLevel(method, SemanticHeadingLevel.Level2);
        Add(method);
        foreach (var name in new[] { "Courier", "Pickup point", "Parcel locker" })
            Add(new SkUiRadioButton { Content = name, GroupName = "delivery", IsChecked = name == "Courier", FontFamily = DemoFonts.OpenSansRegular });

        var tip = new SkUiSlider { Maximum = 20, Value = 5 };
        SemanticProperties.SetDescription(tip, "Courier tip");
        Add(tip);
        var progress = new SkUiProgressBar { Progress = 0.6 };
        SemanticProperties.SetDescription(progress, "Checkout progress");
        Add(progress);

        // A tappable card: read as one button with its text.
        var card = new SkUiBorder
        {
            Padding = 12,
            Stroke = DemoColors.Border,
            StrokeThickness = 1,
            ShowsPressEffect = true,
            Content = new SkUiVerticalStackLayout
            {
                Children =
                {
                    new SkUiLabel { Text = "Order 42", FontFamily = DemoFonts.OpenSansSemibold, TextColor = DemoColors.Ink },
                    new SkUiLabel { Text = "Shipped yesterday · tap for details", FontFamily = DemoFonts.OpenSansRegular, TextColor = DemoColors.Caption }
                }
            }
        };
        card.Tapped += (_, _) => _action.Text = "Last action: card activated";
        Add(card);

        // Rows in a drawn scroller: screen readers scroll it (TalkBack past the last row, VoiceOver with three fingers).
        var rows = new SkUiVerticalStackLayout { Spacing = 4 };
        for (var index = 1; index <= 12; index++)
            rows.Children.Add(Button($"Time slot {index}", $"slot {index}"));
        Add(new SkUiScrollView { Content = rows, HeightRequest = 150 });

        // A hosted native Entry keeps its own accessibility.
        Add(new SkUiMauiContentView { Content = new Entry { Placeholder = "Note to the courier", AutomationId = "CourierNote" }, HeightRequest = 48 });

        // Core nodes: the semantic properties and focus API of the Core layer.
        var core = new SkUiCoreVerticalStackLayout { Spacing = 8 };
        core.Add(new SkUiCoreLabel { Text = "Core section", FontSize = 18 }.SetSemanticHeadingLevel(SemanticHeadingLevel.Level2));
        core.Add(new SkUiCoreSwitch().SetHorizontalAlignment(LayoutAlignment.Start).SetSemanticDescription("Notify me by SMS"));
        var coreButton = new SkUiCoreButton { Text = "Core button", Tag = "core button" };
        coreButton.Tapped += (sender, _) => _action.Text = $"Last action: clicked {((SkUiCoreNode)sender!).Tag}";
        core.Add(coreButton);
        Add(new SkUiCoreHost().SetContent(core));

        var share = new SkUiImageButton
        {
            Source = new FontImageSource { Glyph = "↗", FontFamily = DemoFonts.OpenSansSemibold, Color = DemoColors.Accent, Size = 24 },
            WidthRequest = 44,
            HeightRequest = 44,
            HorizontalOptions = LayoutOptions.Start,
            Tag = "share"
        };
        SemanticProperties.SetDescription(share, "Share order");
        share.Clicked += OnTaggedClicked;
        var cancel = Button("Cancel", "cancel");
        Add(Row(_continue, cancel, share));
    }

    private void Add(ISkUiView view) => _surface.Children.Add(view);

    /// <summary>A label screen readers skip (<c>AutomationProperties.IsInAccessibleTree</c> = false).</summary>
    private static SkUiLabel Decoration(string text)
    {
        var label = new SkUiLabel { Text = text, FontFamily = DemoFonts.OpenSansRegular, TextColor = DemoColors.Ink, VerticalOptions = LayoutOptions.Center };
        AutomationProperties.SetIsInAccessibleTree(label, false);
        return label;
    }

    private static SkUiHorizontalStackLayout Row(params ISkUiView[] views)
    {
        var row = new SkUiHorizontalStackLayout { Spacing = 10 };
        foreach (var view in views)
            row.Children.Add(view);
        return row;
    }

    /// <summary>A button whose <see cref="SkUiView.Tag"/> tells the shared handler which one it is.</summary>
    private SkUiButton Button(string text, string tag)
    {
        var button = new SkUiButton { Text = text, Tag = tag, FontFamily = DemoFonts.OpenSansSemibold };
        button.Clicked += OnTaggedClicked;
        return button;
    }

    private void OnTaggedClicked(object? sender, EventArgs args) => _action.Text = $"Last action: clicked {(sender as SkUiView)?.Tag}";

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _timer ??= Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += OnTick;
        _timer.Start();
        OnTick(null, EventArgs.Empty);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_timer is null) return;
        _timer.Stop();
        _timer.Tick -= OnTick;
    }

    private void OnTick(object? sender, EventArgs args)
    {
        _status.Text = _surface.FocusManagerIfCreated?.Focused is { } focused ? $"Focus: {Describe(focused)}" : "Focus: none";
        _tree.Text = Dump(SkUiSemanticsTree.Build(_surface, _surface.FocusManagerIfCreated));
    }

    private static string Describe(object node) => node switch
    {
        SkUiButton button => $"button \"{button.Text}\"",
        SkUiCoreLabel label => $"{label.GetType().Name} \"{label.Text}\"",
        _ => node.GetType().Name
    };

    /// <summary>The tree as indented lines: role, name, state, actions and bounds.</summary>
    private static string Dump(SkUiSemanticsTree tree)
    {
        var builder = new System.Text.StringBuilder();
        void Write(SkUiSemanticsNode node, int depth)
        {
            builder.Append(' ', depth * 2).Append(node.IsNative ? "Native" : node.Role.ToString());
            if (node.Label is { } label) builder.Append(" \"").Append(label).Append('"');
            if (node.HeadingLevel != SemanticHeadingLevel.None) builder.Append(" heading ").Append((int)node.HeadingLevel);
            if (node.CheckState is { } state) builder.Append(' ').Append(state);
            if (node.DisplayValue is { } value) builder.Append(" = ").Append(value);
            if (node.Hint is { } hint) builder.Append(" (").Append(hint).Append(')');
            if (node.Actions != SkUiSemanticsActions.None) builder.Append(" [").Append(node.Actions).Append(']');
            if (!node.IsEnabled) builder.Append(" disabled");
            if (node.IsFocused) builder.Append(" *focused*");
            builder.Append(FormattableString.Invariant($" @{node.Bounds.X:0},{node.Bounds.Y:0} {node.Bounds.Width:0}x{node.Bounds.Height:0}")).AppendLine();
            foreach (var child in node.Children)
                Write(child, depth + 1);
        }
        foreach (var node in tree.Root.Children)
            Write(node, 0);
        return builder.ToString();
    }

    private static Label Caption(string text) => new()
    {
        Text = text,
        TextColor = DemoColors.Caption,
        FontFamily = DemoFonts.OpenSansRegular,
        FontSize = 12,
        LineBreakMode = LineBreakMode.WordWrap
    };
}
