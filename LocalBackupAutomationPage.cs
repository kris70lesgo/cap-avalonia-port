using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;

namespace CapAvalonia;

internal static class LocalBackupAutomationPage
{
    private static readonly IBrush Text = Brush("#F1F1F1");
    private static readonly IBrush Muted = Brush("#A1A1A1");

    public static Control Build(Window owner)
    {
        var content = new StackPanel { Spacing = 18 };
        var header = new StackPanel { Spacing = 4 };
        header.Children.Add(Label("Automations", 22, Text, FontWeight.SemiBold));
        header.Children.Add(Label("Keep a second local copy of new recordings or screenshots. Nothing is uploaded.", 12, Muted, wrap: true));
        content.Children.Add(header);

        var cardContent = new StackPanel();
        cardContent.Children.Add(BackupRow(owner, "Recordings", "automation.recordings"));
        cardContent.Children.Add(new Border { Height = 1, Background = Brush("#2A2A2A"), Margin = new Thickness(12, 0) });
        cardContent.Children.Add(BackupRow(owner, "Screenshots", "automation.screenshots"));
        var card = new Border
        {
            Background = Brush("#1C1C1C"),
            BorderBrush = Brush("#282828"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Child = cardContent
        };
        content.Children.Add(card);

        return content;
    }

    private static Control BackupRow(Window owner, string mediaType, string preferencePrefix)
    {
        var folder = AppPreferencesStore.Get(preferencePrefix + ".folder") ?? string.Empty;
        var enabled = AppPreferencesStore.Get(preferencePrefix + ".enabled") == "true" && !string.IsNullOrWhiteSpace(folder);
        if (AppPreferencesStore.Get(preferencePrefix + ".enabled") == "true" && !enabled)
        {
            try { AppPreferencesStore.Set(preferencePrefix + ".enabled", "false"); }
            catch { }
        }

        var folderLabel = Label(string.IsNullOrWhiteSpace(folder) ? "No backup folder selected" : folder, 11, Muted, wrap: true);
        folderLabel.TextTrimming = TextTrimming.CharacterEllipsis;
        folderLabel.MaxWidth = 520;

        var toggle = new ToggleSwitch
        {
            IsChecked = enabled,
            IsEnabled = !string.IsNullOrWhiteSpace(folder),
            Width = 38,
            OnContent = string.Empty,
            OffContent = string.Empty,
            VerticalAlignment = VerticalAlignment.Center
        };
        toggle.IsCheckedChanged += (_, _) =>
        {
            try { AppPreferencesStore.Set(preferencePrefix + ".enabled", toggle.IsChecked == true ? "true" : "false"); }
            catch { }
        };

        var chooseFolder = new Button
        {
            Content = string.IsNullOrWhiteSpace(folder) ? "Choose folder" : "Change folder",
            MinWidth = 98,
            Height = 28,
            Padding = new Thickness(10, 0),
            FontSize = 11,
            Background = Brush("#2A2A2A"),
            BorderBrush = Brush("#3A3A3A"),
            BorderThickness = new Thickness(1),
            Foreground = Text,
            CornerRadius = new CornerRadius(14),
            VerticalAlignment = VerticalAlignment.Center
        };
        chooseFolder.Click += async (_, _) =>
        {
            try
            {
                var selected = await AutomationService.ChooseFolderAsync(owner,
                    $"Choose a local backup folder for {mediaType.ToLowerInvariant()}");
                if (selected is null)
                    return;

                folder = selected;
                folderLabel.Text = folder;
                toggle.IsEnabled = true;
                chooseFolder.Content = "Change folder";
                AppPreferencesStore.Set(preferencePrefix + ".folder", folder);
            }
            catch (Exception error)
            {
                ToolTip.SetTip(chooseFolder, error.Message);
            }
        };

        var text = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(Label($"Back up new {mediaType.ToLowerInvariant()}", 13, Text));
        text.Children.Add(folderLabel);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        actions.Children.Add(chooseFolder);
        actions.Children.Add(toggle);
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), MinHeight = 70, Margin = new Thickness(12, 8) };
        row.Children.Add(text);
        Grid.SetColumn(actions, 1);
        row.Children.Add(actions);
        return row;
    }

    private static TextBlock Label(string text, double size, IBrush foreground, FontWeight? weight = null, bool wrap = false) => new()
    {
        Text = text,
        FontSize = size,
        Foreground = foreground,
        FontWeight = weight ?? FontWeight.Normal,
        TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
        VerticalAlignment = VerticalAlignment.Center
    };

    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));
}
