using System.Runtime.CompilerServices;

namespace MauiSkiaUi;

/// <summary>
/// Radio button groups for <see cref="SkUiRadioButton"/> (MAUI's rules). Core radio buttons do not group themselves
/// (<see cref="Core.SkUiCoreRadioButtons"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Exclusion:</b> checking a radio button unchecks the rest of its group. A named group spans the page (or, before the
/// tree is on a page, the grouping layout or the parent); an unnamed one is the radio button's siblings.
/// </para>
/// <para>
/// <b>Group layouts:</b> MAUI's own <see cref="RadioButtonGroup"/> attached properties, set on an <see cref="SkUiLayout"/>
/// (which forwards their changes here), so MAUI markup works unchanged. <c>GroupName</c> names the layout's radio buttons
/// (those without a group name, also ones added later); <c>SelectedValue</c> (two-way) is the
/// <see cref="SkUiRadioButton.Value"/> of the checked one, and setting it checks the radio button with that value
/// (<c>null</c> unchecks the group). MAUI's own controller still runs on the layout; it only handles MAUI's <c>RadioButton</c>.
/// </para>
/// </remarks>
internal static class SkUiRadioGroups
{
    private static readonly ConditionalWeakTable<Element, Controller> Controllers = new();

    /// <summary>A layout's <see cref="RadioButtonGroup.GroupNameProperty"/> changed.</summary>
    internal static void OnLayoutGroupNameChanged(SkUiLayout layout) =>
        ControllerFor(layout).SetGroupName(RadioButtonGroup.GetGroupName(layout));

    /// <summary>A layout's <see cref="RadioButtonGroup.SelectedValueProperty"/> changed.</summary>
    internal static void OnLayoutSelectedValueChanged(SkUiLayout layout) =>
        ControllerFor(layout).SetSelectedValue(RadioButtonGroup.GetSelectedValue(layout));

    private static Controller ControllerFor(Element layout) => Controllers.GetValue(layout, static layout => new Controller(layout));

    /// <summary>A radio button became checked: unchecks the rest of its group, then reports its value to the group's layout.</summary>
    internal static void OnChecked(SkUiRadioButton radio)
    {
        UncheckOthersInScope(radio);
        OnSelectionChanged(radio);
    }

    /// <summary>The checked radio button's value changed, or it was just checked: updates the grouping layout's <c>SelectedValue</c>.</summary>
    internal static void OnSelectionChanged(SkUiRadioButton radio)
    {
        if (FindController(radio, radio.GroupName) is { } controller)
            controller.Layout.SetValue(RadioButtonGroup.SelectedValueProperty, radio.Value);
    }

    /// <summary>A checked radio button left a named group: that group no longer has a selection.</summary>
    internal static void OnGroupNameChanged(SkUiRadioButton radio, string? oldGroupName)
    {
        if (!radio.IsChecked || string.IsNullOrEmpty(oldGroupName))
            return;
        if (FindController(radio, oldGroupName) is { IsRenaming: false } controller)
            controller.Layout.ClearValue(RadioButtonGroup.SelectedValueProperty);
    }

    private static void UncheckOthersInScope(SkUiRadioButton radio)
    {
        var groupName = radio.GroupName;
        if (string.IsNullOrEmpty(groupName))
        {
            if (radio.Parent is not IElementController parent)
                return;
            foreach (var sibling in parent.LogicalChildren)
            {
                if (sibling is SkUiRadioButton other && other != radio && string.IsNullOrEmpty(other.GroupName) && other.IsChecked)
                    other.SetCheckedByGroup(false);
            }
            return;
        }
        Element? root = PageOf(radio) ?? FindController(radio, groupName)?.Layout ?? radio.Parent;
        if (root is null)
            return;
        foreach (var other in RadioButtons(root))
        {
            if (other != radio && other.GroupName == groupName && other.IsChecked)
                other.SetCheckedByGroup(false);
        }
    }

    private static Page? PageOf(Element element)
    {
        for (var parent = element.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is Page page)
                return page;
        }
        return null;
    }

    /// <summary>The nearest ancestor layout that groups <paramref name="groupName"/>.</summary>
    private static Controller? FindController(SkUiRadioButton radio, string? groupName)
    {
        if (string.IsNullOrEmpty(groupName))
            return null;
        for (var parent = radio.Parent; parent is not null; parent = parent.Parent)
        {
            if (Controllers.TryGetValue(parent, out var controller) && controller.GroupName == groupName)
                return controller;
        }
        return null;
    }

    /// <summary>Every radio button below <paramref name="root"/> in the logical tree (a snapshot: callers change check states).</summary>
    private static List<SkUiRadioButton> RadioButtons(Element root)
    {
        var found = new List<SkUiRadioButton>();
        Collect(root, found);
        return found;

        static void Collect(Element element, List<SkUiRadioButton> found)
        {
            foreach (var child in ((IElementController)element).LogicalChildren)
            {
                if (child is SkUiRadioButton radio)
                    found.Add(radio);
                Collect(child, found);
            }
        }
    }

    /// <summary>The group state of one layout with <see cref="RadioButtonGroup.GroupNameProperty"/> or <see cref="RadioButtonGroup.SelectedValueProperty"/> set.</summary>
    private sealed class Controller
    {
        private object? _selectedValue;

        public Controller(Element layout)
        {
            Layout = layout;
            layout.DescendantAdded += OnDescendantAdded;
        }

        public Element Layout { get; }

        public string? GroupName { get; private set; }

        /// <summary>While renaming, the radio buttons' group name changes are this layout's own and keep its selection.</summary>
        public bool IsRenaming { get; private set; }

        public void SetGroupName(string? value)
        {
            var old = GroupName;
            GroupName = value;
            IsRenaming = true;
            try
            {
                foreach (var radio in RadioButtons(Layout))
                {
                    if (string.IsNullOrEmpty(radio.GroupName) || radio.GroupName == old)
                        radio.GroupName = value;
                }
            }
            finally
            {
                IsRenaming = false;
            }
            if (string.IsNullOrEmpty(value))
                return;
            // SelectedValue may have been set first (attribute order in XAML); without one, report an already checked radio.
            if (_selectedValue is not null)
                ApplySelectedValue();
            else if (RadioButtons(Layout).Find(radio => radio.GroupName == value && radio.IsChecked) is { } selected)
                Layout.SetValue(RadioButtonGroup.SelectedValueProperty, selected.Value);
        }

        public void SetSelectedValue(object? value)
        {
            if (Equals(_selectedValue, value))
                return;
            _selectedValue = value;
            ApplySelectedValue();
        }

        private void ApplySelectedValue()
        {
            if (string.IsNullOrEmpty(GroupName))
                return;
            var value = _selectedValue;
            foreach (var radio in RadioButtons(Layout))
            {
                if (radio.GroupName != GroupName)
                    continue;
                if (value is null)
                {
                    if (radio.IsChecked)
                        radio.SetCheckedByGroup(false);
                }
                else if (!radio.IsChecked && Equals(radio.Value, value))
                {
                    radio.SetCheckedByGroup(true);
                }
            }
        }

        private void OnDescendantAdded(object? sender, ElementEventArgs args)
        {
            if (string.IsNullOrEmpty(GroupName) || args.Element is not SkUiRadioButton radio)
                return;
            if (string.IsNullOrEmpty(radio.GroupName))
            {
                IsRenaming = true;
                try
                {
                    radio.GroupName = GroupName;
                }
                finally
                {
                    IsRenaming = false;
                }
            }
            if (radio.GroupName != GroupName)
                return;
            if (radio.IsChecked)
                Layout.SetValue(RadioButtonGroup.SelectedValueProperty, radio.Value);
            else if (_selectedValue is not null && Equals(radio.Value, _selectedValue))
                radio.SetCheckedByGroup(true);
        }
    }
}
