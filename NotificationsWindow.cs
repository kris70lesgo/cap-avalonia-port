using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CapAvalonia;

internal sealed class NotificationsWindow : Window
{
    public NotificationsWindow(Window owner)
    {
        Title = "Cap Activity";
        Width = 420;
        Height = 620;
        MinWidth = 360;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.Parse("#111111"));
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;

        var heading = new TextBlock
        {
            Text = "Activity",
            FontSize = 17,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        };
        var subtitle = new TextBlock
        {
            Text = "Recent recordings and screenshots saved on this device.",
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.Parse("#9B9B9B"))
        };
        var content = new StackPanel { Spacing = 9 };
        try
        {
            var recentVideos = MediaLibraryService.GetItems(screenshots: false).Take(8).ToArray();
            var recentImages = MediaLibraryService.GetItems(screenshots: true).Take(8).ToArray();
            AddSection(content, "Recordings", recentVideos.Select(item =>
                (item.Name, $"{item.Modified:g} · {MediaLibraryService.FormatSize(item.Size)}", item.Path, false)), owner);
            AddSection(content, "Screenshots", recentImages.Select(item =>
                (item.Name, $"{item.Modified:g} · {MediaLibraryService.FormatSize(item.Size)}", item.Path, true)), owner);
        }
        catch (Exception error)
        {
            content.Children.Add(Label("Could not load recent activity: " + error.Message, 12, "#B8B8B8"));
        }

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*"), Margin = new Thickness(18) };
        root.Children.Add(heading);
        Grid.SetRow(subtitle, 1);
        subtitle.Margin = new Thickness(0, 5, 0, 14);
        root.Children.Add(subtitle);
        var scroll = new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };
        Grid.SetRow(scroll, 2);
        root.Children.Add(scroll);
        Content = root;
    }

    private static void AddSection(StackPanel host, string title,
        IEnumerable<(string Name, string Detail, string Path, bool Screenshot)> items, Window owner)
    {
        var values = items.ToArray();
        var header = Label(title, 13, "#F2F2F2");
        header.FontWeight = FontWeight.SemiBold;
        header.Margin = new Thickness(0, 8, 0, 0);
        host.Children.Add(header);
        if (values.Length == 0)
        {
            host.Children.Add(Label("Nothing saved here yet.", 12, "#898989"));
            return;
        }

        foreach (var item in values)
        {
            var details = new StackPanel { Spacing = 3, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            var name = Label(item.Name, 12, "#EEEEEE");
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            details.Children.Add(name);
            details.Children.Add(Label(item.Detail, 10, "#929292"));
            var actionRow = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            var action = Button(item.Screenshot ? "Annotate" : "Edit");
            action.Click += (_, _) =>
            {
                try
                {
                    if (item.Screenshot)
                        new ScreenshotEditorWindow(item.Path).Show(owner);
                    else
                        new VideoEditorWindow(item.Path).Show(owner);
                }
                catch (Exception error)
                {
                    action.Content = "Could not open";
                    ToolTip.SetTip(action, error.Message);
                }
            };
            actionRow.Children.Add(action);
            var copy = Button("Copy");
            copy.Click += async (_, _) =>
            {
                try
                {
                    await MediaLibraryService.CopyToClipboardAsync(owner, item.Path, image: item.Screenshot);
                    copy.Content = "Copied";
                }
                catch (Exception error)
                {
                    copy.Content = "Failed";
                    ToolTip.SetTip(copy, error.Message);
                }
            };
            actionRow.Children.Add(copy);
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 9, Margin = new Thickness(10, 8) };
            row.Children.Add(details);
            Grid.SetColumn(actionRow, 1);
            row.Children.Add(actionRow);
            host.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.Parse("#1A1A1A")),
                BorderBrush = new SolidColorBrush(Color.Parse("#2D2D2D")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Child = row
            });
        }
    }

    private static TextBlock Label(string text, double size, string color) => new()
    {
        Text = text,
        FontSize = size,
        Foreground = new SolidColorBrush(Color.Parse(color)),
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
    };

    private static Button Button(string text) => new()
    {
        Content = text,
        Padding = new Thickness(10, 6),
        Background = new SolidColorBrush(Color.Parse("#242424")),
        BorderBrush = new SolidColorBrush(Color.Parse("#383838")),
        BorderThickness = new Thickness(1),
        Foreground = Brushes.White,
        CornerRadius = new CornerRadius(8),
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
    };
}
