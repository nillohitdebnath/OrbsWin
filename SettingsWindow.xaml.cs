using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using OrbsWin.Services;

namespace OrbsWin;

public partial class SettingsWindow : Window
{
    public ObservableCollection<WheelItem> RootItems { get; } = new();

    private WheelItem? _selectedItem;
    private bool _isUpdatingForm;

    public SettingsWindow()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        StartWithWindowsCheckBox.IsChecked = StartupService.IsStartWithWindowsEnabled();

        List<WheelItem> config = WheelConfigService.LoadConfig();
        RootItems.Clear();
        foreach (var item in config)
        {
            RootItems.Add(item);
        }

        ItemsTreeView.ItemsSource = RootItems;
    }

    private void OnStartWithWindowsChanged(object sender, RoutedEventArgs e)
    {
        bool enable = StartWithWindowsCheckBox.IsChecked == true;
        StartupService.SetStartWithWindows(enable);

        if (System.Windows.Application.Current is App app)
        {
            app.SyncAutostartMenuItem(enable);
        }
    }

    private void OnTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        _selectedItem = ItemsTreeView.SelectedItem as WheelItem;

        if (_selectedItem == null)
        {
            DetailFormPanel.IsEnabled = false;
            ClearForm();
            return;
        }

        _isUpdatingForm = true;
        DetailFormPanel.IsEnabled = true;

        ItemNameTextBox.Text = _selectedItem.Name;
        ItemValueTextBox.Text = _selectedItem.Value ?? string.Empty;

        SetComboBoxSelectedType(_selectedItem.ItemType);
        UpdateValueLabelText(_selectedItem.ItemType);

        _isUpdatingForm = false;
    }

    private void SetComboBoxSelectedType(string type)
    {
        foreach (ComboBoxItem item in ItemTypeComboBox.Items)
        {
            if (string.Equals(item.Content?.ToString(), type, StringComparison.OrdinalIgnoreCase))
            {
                ItemTypeComboBox.SelectedItem = item;
                return;
            }
        }
        ItemTypeComboBox.SelectedIndex = 0;
    }

    private void UpdateValueLabelText(string type)
    {
        switch (type.ToLowerInvariant())
        {
            case "builtin_tool":
                ValueLabel.Text = "Tool Name (e.g. Color Picker, Calculator):";
                ItemValueTextBox.IsEnabled = true;
                break;
            case "app_launch":
                ValueLabel.Text = "Executable Path (e.g. C:\\Windows\\notepad.exe):";
                ItemValueTextBox.IsEnabled = true;
                break;
            case "shell_command":
                ValueLabel.Text = "Shell Command (e.g. echo Hello):";
                ItemValueTextBox.IsEnabled = true;
                break;
            case "snippet":
                ValueLabel.Text = "Text Snippet to Copy:";
                ItemValueTextBox.IsEnabled = true;
                break;
            case "submenu":
                ValueLabel.Text = "Submenu (uses child items):";
                ItemValueTextBox.IsEnabled = false;
                break;
        }
    }

    private void OnItemTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingForm || _selectedItem == null) return;

        if (ItemTypeComboBox.SelectedItem is ComboBoxItem selectedComboItem)
        {
            string newType = selectedComboItem.Content?.ToString() ?? "builtin_tool";
            _selectedItem.ItemType = newType;
            UpdateValueLabelText(newType);
            RefreshTreeDisplay();
        }
    }

    private void OnFormFieldsChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingForm || _selectedItem == null) return;

        _selectedItem.Name = ItemNameTextBox.Text;
        _selectedItem.Value = ItemValueTextBox.Text;
        RefreshTreeDisplay();
    }

    private void ClearForm()
    {
        _isUpdatingForm = true;
        ItemNameTextBox.Text = string.Empty;
        ItemValueTextBox.Text = string.Empty;
        _isUpdatingForm = false;
    }

    private void OnAddItemClick(object sender, RoutedEventArgs e)
    {
        WheelItem newItem = new WheelItem("New Item", "builtin_tool", "Calculator");

        if (_selectedItem != null && _selectedItem.ItemType == "submenu")
        {
            _selectedItem.Children.Add(newItem);
        }
        else
        {
            RootItems.Add(newItem);
        }

        RefreshTreeDisplay();
    }

    private void OnAddSubitemClick(object sender, RoutedEventArgs e)
    {
        if (_selectedItem == null) return;

        _selectedItem.ItemType = "submenu";
        SetComboBoxSelectedType("submenu");

        WheelItem child = new WheelItem("Child Item", "snippet", "Sample Snippet");
        _selectedItem.Children.Add(child);

        RefreshTreeDisplay();
    }

    private void OnDeleteItemClick(object sender, RoutedEventArgs e)
    {
        if (_selectedItem == null) return;

        RemoveItemRecursive(RootItems, _selectedItem);
        _selectedItem = null;
        DetailFormPanel.IsEnabled = false;
        ClearForm();
        RefreshTreeDisplay();
    }

    private bool RemoveItemRecursive(IList<WheelItem> list, WheelItem target)
    {
        if (list.Remove(target)) return true;

        foreach (var item in list)
        {
            if (RemoveItemRecursive(item.Children, target)) return true;
        }

        return false;
    }

    private void OnMoveUpClick(object sender, RoutedEventArgs e)
    {
        if (_selectedItem == null) return;
        MoveItemInList(RootItems, _selectedItem, -1);
    }

    private void OnMoveDownClick(object sender, RoutedEventArgs e)
    {
        if (_selectedItem == null) return;
        MoveItemInList(RootItems, _selectedItem, 1);
    }

    private bool MoveItemInList(ObservableCollection<WheelItem> list, WheelItem target, int direction)
    {
        int index = list.IndexOf(target);
        if (index >= 0)
        {
            int newIndex = index + direction;
            if (newIndex >= 0 && newIndex < list.Count)
            {
                list.Move(index, newIndex);
                RefreshTreeDisplay();
                return true;
            }
        }
        return false;
    }

    private void RefreshTreeDisplay()
    {
        ItemsTreeView.Items.Refresh();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        List<WheelItem> itemsToSave = new List<WheelItem>(RootItems);
        WheelConfigService.SaveConfig(itemsToSave);

        if (System.Windows.Application.Current is App app)
        {
            app.ReloadConfig();
        }

        System.Windows.MessageBox.Show("Configuration saved successfully!", "OrbsWin", MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }
}
