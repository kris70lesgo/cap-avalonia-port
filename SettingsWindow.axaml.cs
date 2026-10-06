using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using System;
using System.IO;
using System.Linq;

namespace CapAvalonia;

public partial class SettingsWindow : Window
{
    private string _currentSection = "General";

    public SettingsWindow()
    {
        InitializeComponent();
        LoadAndPersistGeneralControls();
        UpdateProjectNamePreview(ProjectNameInput.Text);
        RefreshRecordingFolderDisplay();
    }

    private void OnProjectNameChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox input)
            UpdateProjectNamePreview(input.Text);
    }

    private void UpdateProjectNamePreview(string? template)
    {
        var now = DateTime.Now;
        var preview = string.IsNullOrWhiteSpace(template)
            ? "Recording_" + now.ToString("yyyyMMdd_HHmm")
            : template.Replace("{target_name}", "Screen Recording", StringComparison.OrdinalIgnoreCase)
                .Replace("{target_kind}", "Display", StringComparison.OrdinalIgnoreCase)
                .Replace("{recording_mode}", "Studio", StringComparison.OrdinalIgnoreCase)
                .Replace("{mode}", "Studio", StringComparison.OrdinalIgnoreCase)
                .Replace("{date}", now.ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase)
                .Replace("{time}", now.ToString("HH-mm"), StringComparison.OrdinalIgnoreCase);
        ProjectNamePreview.Text = preview;
    }

    public void NavigateTo(string section)
    {
        var navigationButtons = this.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.Classes.Contains("nav"))
            .ToArray();

        var selectedButton = navigationButtons.FirstOrDefault(button =>
            string.Equals(button.Tag?.ToString(), section, StringComparison.OrdinalIgnoreCase));

        if (selectedButton is null)
        {
            selectedButton = NavGeneral;
            section = "General";
        }

        _currentSection = section;

        foreach (var button in navigationButtons)
        {
            var selected = ReferenceEquals(button, selectedButton);
            if (selected && !button.Classes.Contains("selected"))
                button.Classes.Add("selected");
            else if (!selected)
                button.Classes.Remove("selected");

            foreach (var icon in button.GetVisualDescendants().OfType<PathIcon>())
                icon.Foreground = new SolidColorBrush(Color.Parse(selected ? "#1684FF" : "#A4A4A4"));
        }

        var isGeneral = section == "General";
        GeneralContent.IsVisible = isGeneral;
        OtherPageContent.IsVisible = !isGeneral;
        OtherPageBody.Children.Clear();

        if (!isGeneral)
            OtherPageBody.Children.Add(SettingsPageFactory.Build(section, this));
    }

    public void RefreshCurrentPage()
    {
        if (_currentSection is "Recordings" or "Screenshots")
            NavigateTo(_currentSection);
    }

    private void LoadAndPersistGeneralControls()
    {
        foreach (var toggle in GeneralContent.GetVisualDescendants().OfType<ToggleSwitch>())
        {
            var key = GetPreferenceKey(toggle);
            if (key is null)
                continue;
            if (bool.TryParse(AppPreferencesStore.Get("general." + key), out var isChecked))
                toggle.IsChecked = isChecked;
            toggle.IsCheckedChanged += (_, _) => SavePreference(key, toggle.IsChecked?.ToString() ?? "false");
        }

        foreach (var combo in GeneralContent.GetVisualDescendants().OfType<ComboBox>())
        {
            var key = GetPreferenceKey(combo);
            if (key is null)
                continue;
            var savedValue = AppPreferencesStore.Get("general." + key);
            if (savedValue is not null)
            {
                var savedItem = combo.Items.Cast<object>().FirstOrDefault(item =>
                    string.Equals(GetComboItemText(item), savedValue, StringComparison.Ordinal));
                if (savedItem is not null)
                    combo.SelectedItem = savedItem;
            }
            combo.SelectionChanged += (_, _) =>
            {
                var value = GetComboItemText(combo.SelectedItem);
                if (value is not null)
                    SavePreference(key, value);
            };
        }

        foreach (var textBox in GeneralContent.GetVisualDescendants().OfType<TextBox>())
        {
            var key = GetPreferenceKey(textBox);
            if (key is null)
                continue;
            var savedValue = AppPreferencesStore.Get("general." + key);
            if (savedValue is not null)
                textBox.Text = savedValue;
            textBox.TextChanged += (_, _) => SavePreference(key, textBox.Text ?? string.Empty);
        }
    }

    private static string? GetPreferenceKey(Control control)
    {
        if (!string.IsNullOrWhiteSpace(control.Tag?.ToString()))
            return control.Tag!.ToString();

        if (control.Parent is Grid grid)
        {
            foreach (var text in grid.Children.OfType<StackPanel>().SelectMany(panel => panel.Children.OfType<TextBlock>()))
            {
                if (!string.IsNullOrWhiteSpace(text.Text))
                    return text.Text.Trim();
            }
        }

        return null;
    }

    private static string? GetComboItemText(object? item) => item switch
    {
        ComboBoxItem comboItem => comboItem.Content?.ToString(),
        null => null,
        _ => item.ToString()
    };

    private static void SavePreference(string key, string value)
    {
        try
        {
            AppPreferencesStore.Set("general." + key, value);
        }
        catch
        {
            // A settings write must not interrupt recording setup or normal navigation.
        }
    }

    private void RefreshRecordingFolderDisplay()
    {
        var folder = AppPreferencesStore.Get("recordings.directory");
        RecordingFolderDisplay.Text = string.IsNullOrWhiteSpace(folder)
            ? "Default (Application Support)"
            : folder;
        ResetRecordingFolderButton.IsVisible = !string.IsNullOrWhiteSpace(folder);
    }

    private async void OnChooseRecordingsFolder(object? sender, RoutedEventArgs e)
    {
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Choose where Cap Avalonia saves recordings",
                AllowMultiple = false
            });
            if (folders.Count == 0)
                return;

            AppPreferencesStore.Set("recordings.directory", folders[0].Path.LocalPath);
            RefreshRecordingFolderDisplay();
        }
        catch (Exception error)
        {
            RecordingFolderDisplay.Text = "Could not choose folder: " + error.Message;
        }
    }

    private void OnResetRecordingsFolder(object? sender, RoutedEventArgs e)
    {
        try
        {
            AppPreferencesStore.Set("recordings.directory", string.Empty);
            RefreshRecordingFolderDisplay();
        }
        catch (Exception error)
        {
            RecordingFolderDisplay.Text = "Could not reset folder: " + error.Message;
        }
    }

    private void OnNavigationClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string section)
            NavigateTo(section);
    }
}
