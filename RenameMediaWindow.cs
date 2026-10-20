using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System;
using System.IO;

namespace CapAvalonia;

internal sealed class RenameMediaWindow : Window
{
    private readonly TextBox _name;
    private readonly TextBlock _error;
    private readonly string _extension;

    public RenameMediaWindow(string fileName)
    {
        Title = "Rename media";
        Width = 440;
        Height = 230;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.Parse("#111111"));
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;

        var title = new TextBlock
        {
            Text = "Rename this file",
            FontSize = 17,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        };
        _extension = Path.GetExtension(fileName);
        var description = new TextBlock
        {
            Text = string.IsNullOrEmpty(_extension) ? "Choose a new name." : $"The {_extension} file extension will be kept.",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#A7A7A7"))
        };
        _name = new TextBox
        {
            Text = Path.GetFileNameWithoutExtension(fileName),
            MinHeight = 36,
            Padding = new Thickness(9, 6),
            Background = new SolidColorBrush(Color.Parse("#242424")),
            BorderBrush = new SolidColorBrush(Color.Parse("#383838")),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(7)
        };
        _error = new TextBlock { FontSize = 11, Foreground = new SolidColorBrush(Color.Parse("#F27A7A")), IsVisible = false };
        var cancel = Button("Cancel");
        cancel.Click += (_, _) => Close(false);
        var save = Button("Save");
        save.Background = new SolidColorBrush(Color.Parse("#2677EA"));
        save.Click += (_, _) => Save();
        var actions = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 8,
            Children = { cancel, save }
        };

        Content = new StackPanel
        {
            Margin = new Thickness(22),
            Spacing = 10,
            Children = { title, description, _name, _error, actions }
        };
        Opened += (_, _) =>
        {
            _name.SelectAll();
            _name.Focus();
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { Save(); e.Handled = true; }
            else if (e.Key == Key.Escape) { Close(false); e.Handled = true; }
        };
    }

    public string NewName
    {
        get
        {
            var name = _name.Text?.Trim() ?? string.Empty;
            return !string.IsNullOrEmpty(_extension) && name.EndsWith(_extension, StringComparison.OrdinalIgnoreCase)
                ? name[..^_extension.Length]
                : name;
        }
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(NewName) || NewName is "." or "..")
        {
            ShowError("Enter a valid file name.");
            return;
        }
        if (NewName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || NewName.Contains('/') || NewName.Contains('\\'))
        {
            ShowError("The name contains characters that cannot be used in a file name.");
            return;
        }
        Close(true);
    }

    private void ShowError(string message)
    {
        _error.Text = message;
        _error.IsVisible = true;
    }

    private static Button Button(string label) => new()
    {
        Content = label,
        MinWidth = 88,
        Padding = new Thickness(14, 8),
        Background = new SolidColorBrush(Color.Parse("#242424")),
        BorderBrush = new SolidColorBrush(Color.Parse("#383838")),
        BorderThickness = new Thickness(1),
        Foreground = Brushes.White,
        CornerRadius = new CornerRadius(8)
    };
}
