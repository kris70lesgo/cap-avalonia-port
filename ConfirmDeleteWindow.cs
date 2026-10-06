using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;

namespace CapAvalonia;

internal sealed class ConfirmDeleteWindow : Window
{
    public ConfirmDeleteWindow(string fileName)
    {
        Title = "Delete media";
        Width = 430;
        Height = 220;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.Parse("#111111"));
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;

        var title = new TextBlock
        {
            Text = "Move this file to Trash?",
            FontSize = 17,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        };
        var description = new TextBlock
        {
            Text = $"{fileName}\nYou can restore it from the system Trash later.",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#A7A7A7")),
            TextWrapping = TextWrapping.Wrap
        };
        var cancel = Button("Cancel");
        cancel.Click += (_, _) => Close(false);
        var delete = Button("Move to Trash", destructive: true);
        delete.Click += (_, _) => Close(true);
        var actions = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 8,
            Children = { cancel, delete }
        };

        var content = new StackPanel
        {
            Margin = new Thickness(22),
            Spacing = 14,
            Children = { title, description, actions }
        };
        Content = content;
    }

    private static Button Button(string label, bool destructive = false) => new()
    {
        Content = label,
        MinWidth = 88,
        Padding = new Thickness(14, 8),
        Background = new SolidColorBrush(Color.Parse(destructive ? "#A52E35" : "#242424")),
        BorderBrush = new SolidColorBrush(Color.Parse(destructive ? "#C23A43" : "#383838")),
        BorderThickness = new Thickness(1),
        Foreground = Brushes.White,
        CornerRadius = new CornerRadius(8)
    };
}
