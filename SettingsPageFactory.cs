using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace CapAvalonia;

internal static class SettingsPageFactory
{
    private static readonly IBrush PanelBrush = Brush("#1A1A1A");
    private static readonly IBrush CardBrush = Brush("#1C1C1C");
    private static readonly IBrush PanelBorderBrush = Brush("#282828");
    private static readonly IBrush TextBrush = Brush("#F1F1F1");
    private static readonly IBrush MutedBrush = Brush("#A1A1A1");

    public static Control Build(string section, Window owner) => section switch
    {
        "Recording quality" => BuildQualityPage(),
        "Shortcuts" => BuildShortcutsPage(),
        "CLI" => BuildCliPage(),
        "Recordings" => BuildRecordingsPage(owner),
        "Screenshots" => BuildScreenshotsPage(owner),
        "Automations" => LocalBackupAutomationPage.Build(owner),
        "Transcription" => BuildTranscriptionPage(),
        "Integrations" => BuildIntegrationsPage(),
        "Plan & license" => BuildLicensePage(),
        "Experimental" => BuildExperimentalPage(),
        "Feedback" => BuildFeedbackPage(),
        "Changelog" => BuildChangelogPage(),
        _ => BuildMessagePage(section)
    };

    private static Control BuildQualityPage()
    {
        var studioChoices = ChoiceList("quality.studio",
            ("Balanced", "Recommended", "Clear, detailed recordings with a practical file size. Best for everyday use."),
            ("Smaller files", "", "Uses less disk space. Can reduce detail, especially when recording with a camera."),
            ("Maximum detail", "", "Preserves more detail for demanding edits. Creates larger files and needs more disk space."));
        var instantChoices = ResolutionChoices();

        var shareLinksToggle = new ToggleSwitch
        {
            IsChecked = false,
            Width = 38,
            OnContent = string.Empty,
            OffContent = string.Empty,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
            IsEnabled = false
        };
        ToolTip.SetTip(shareLinksToggle, "Share links are not available in this local build.");
        var shareRows = Card(SettingRow("Open share links automatically", "Share links are not available in this local build.", shareLinksToggle, 62));
        var studioSection = Section("Studio", "Saved to your computer, ready to edit. All three quality options are available on every plan.",
            Card(studioChoices,
                Label("These options affect the original recording. Choose your final export resolution and file size in the editor.", 12, MutedBrush, wrap: true, margin: new Thickness(16, 8, 16, 19)),
                new Border { Height = 2.5 }), contentTopMargin: 9.5);
        studioSection.Margin = new Thickness(0, 0, 0, -3);

        return Page("Recording quality", "Choose how new recordings look and how much space they use.",
            studioSection,
            Section("Instant", "Fast local capture. Recordings appear in your library when you stop.",
                Card(Label("Recording resolution", 13, TextBrush, weight: FontWeight.Medium, margin: new Thickness(16, 16, 16, 14.5)), instantChoices,
                    Label("Local recordings keep the native resolution of the selected display or capture area.", 12, MutedBrush, wrap: true, margin: new Thickness(12, 10))), contentTopMargin: 8.5),
            Section("Sharing", null, shareRows));
    }

    private static Control BuildShortcutsPage()
    {
        var shortcuts = new[]
        {
            ("Screenshot current display", "screenshot.display", "NONE"),
            ("Screenshot current window", "screenshot.window", "NONE"),
            ("Screenshot area picker", "screenshot.area", "NONE"),
            ("Open recording picker", "recording.picker", "NONE"),
            ("Stop recording", "recording.stop", "NONE"),
            ("Restart recording", "recording.restart", "NONE"),
            ("Pause/resume recording", "recording.pause", "NONE"),
            ("Cycle recording mode", "recording.cycle", "NONE"),
            ("Record display", "recording.display", "NONE"),
            ("Record window", "recording.window", "NONE"),
            ("Record area", "recording.area", "NONE")
        };

        var rows = shortcuts.Select(item => ShortcutRow(item.Item1, item.Item2, item.Item3)).ToArray();
        var card = Card(WithSeparators(rows).ToArray());
        card.Margin = new Thickness(-4, -3.5, -4, 0);
        if (rows[0] is Grid firstRow)
            firstRow.Margin = new Thickness(16, 7.5, 16, 4);
        var description = OperatingSystem.IsMacOS()
            ? "Configure system-wide keyboard shortcuts to control Cap."
            : "Configure keyboard shortcuts to control Cap while the app is focused.";
        return PageWithHeader(PageHeader("Shortcuts", description), card);
    }

    private static Control BuildCliPage()
    {
        var installDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin");
        var commandPath = Path.Combine(installDirectory, "cap");
        var targetPath = Path.Combine(AppContext.BaseDirectory, "cap-cli");
        var status = Label("Not installed", 13, TextBrush);
        var description = Label("The Cap CLI is not included in this Avalonia build.", 12, MutedBrush, wrap: true);
        description.MaxWidth = 390;
        description.LineHeight = 16;
        var install = ActionButton("Install CLI", primary: true);
        install.FontSize = 12;
        install.Height = 28;
        install.Padding = new Thickness(12, 0);
        install.CornerRadius = new CornerRadius(15);
        install.VerticalAlignment = VerticalAlignment.Top;
        install.IsEnabled = false;
        ToolTip.SetTip(install, "The Cap CLI is not included in this Avalonia build.");

        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 16 };
        var text = new StackPanel { Spacing = 6 };
        text.Children.Add(status);
        text.Children.Add(description);
        heading.Children.Add(text);
        Grid.SetColumn(install, 1);
        heading.Children.Add(install);

        var cardContent = new StackPanel { Spacing = 0, Margin = new Thickness(16, 17.5, 16, 17.5) };
        cardContent.Children.Add(heading);
        var paths = new StackPanel { Spacing = 8.5, Margin = new Thickness(0, 16, 0, 0) };
        paths.Children.Add(CliPathRow("Command", commandPath));
        paths.Children.Add(CliPathRow("Target", targetPath));
        cardContent.Children.Add(paths);
        var card = Card(cardContent);
        card.Margin = new Thickness(-4, -3, -4, 0);
        return PageWithHeader(PageHeader("Command Line", "Install the Cap command for terminals, agents, scripts, and local automation."), card);
    }

    private static Control BuildRecordingsPage(Window owner) => BuildLibraryPage(owner, screenshots: false);

    private static Control BuildScreenshotsPage(Window owner) => BuildLibraryPage(owner, screenshots: true);

    private static Control BuildLibraryPage(Window owner, bool screenshots)
    {
        var title = screenshots ? "Screenshots" : "Recordings";
        var import = ImportButton(screenshots ? "Import image" : "Import");
        import.Margin = new Thickness(0, 2.5, -3, 0);
        var header = PageHeader(title, "Manage your " + title.ToLowerInvariant() + " and perform actions.", import);
        var search = (TextBox)SearchBox(screenshots ? "Search screenshots" : "Search recordings");
        var itemsPanel = new StackPanel { Spacing = 0 };
        var emptyState = Label(screenshots ? "No screenshots found" : "No recordings found", 14,
            Brush("#B0B0B0"), align: HorizontalAlignment.Center,
            margin: new Thickness(0, 11.5, 0, 0));
        var currentFilter = "Show all";
        var visibleCount = 20;
        var loadMore = ActionButton("Load more", primary: false);
        loadMore.HorizontalAlignment = HorizontalAlignment.Center;
        var sourceItems = MediaLibraryService.GetItems(screenshots).ToArray();
        var itemsCard = new Border
        {
            Background = PanelBrush,
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Margin = new Thickness(0, 16, 0, 0),
            MinHeight = 280,
            Child = itemsPanel
        };

        var filterButtons = new List<Button>();
        var filterRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        if (!screenshots)
        {
            foreach (var label in new[] { "Show all", "Instant", "Studio" })
            {
                var button = FilterButton(label);
                button.Tag = label;
                filterButtons.Add(button);
                filterRow.Children.Add(button);
            }
            SelectLibraryFilter(filterButtons, filterButtons[0]);
        }

        filterRow.Margin = new Thickness(0, 0, 0, 8);
        var controls = new StackPanel { Spacing = 0 };
        if (!screenshots)
            controls.Children.Add(filterRow);
        controls.Children.Add(SearchField(search));
        controls.Children.Add(new Border { Height = 1, Background = Brush("#303030"), Margin = new Thickness(0, 12, 0, 0) });

        var libraryView = new StackPanel { Spacing = 0 };
        libraryView.Children.Add(controls);
        libraryView.Children.Add(itemsCard);
        var page = new StackPanel { Spacing = 0 };
        page.Children.Add(header);
        page.Children.Add(libraryView);
        page.Children.Add(emptyState);

        loadMore.Click += (_, _) =>
        {
            visibleCount += 20;
            RefreshItems();
        };

        void RefreshItems()
        {
            itemsPanel.Children.Clear();
            sourceItems = MediaLibraryService.GetItems(screenshots).ToArray();
            var filteredItems = sourceItems
                .Where(item => item.Name.Contains(search.Text ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                .Where(item => screenshots || currentFilter == "Show all" || item.Mode == currentFilter)
                .ToArray();

            controls.IsVisible = sourceItems.Length > 0;
            itemsCard.IsVisible = sourceItems.Length > 0;
            libraryView.IsVisible = sourceItems.Length > 0;
            emptyState.IsVisible = sourceItems.Length == 0;

            if (filteredItems.Length == 0)
            {
                loadMore.IsVisible = false;
                if (sourceItems.Length == 0)
                    return;

                var label = currentFilter != "Show all" && !screenshots
                    ? $"No {currentFilter} recordings found in this local library."
                    : !string.IsNullOrWhiteSpace(search.Text)
                        ? "No matching " + title.ToLowerInvariant() + "."
                        : "No " + title.ToLowerInvariant() + " found";
                itemsPanel.Children.Add(Label(label, 12, screenshots ? Brush("#111111") : MutedBrush, align: HorizontalAlignment.Center,
                    margin: new Thickness(12, 34)));
                return;
            }

            var visibleItems = filteredItems.Take(visibleCount).ToArray();
            for (var index = 0; index < visibleItems.Length; index++)
                itemsPanel.Children.Add(MediaItemCard(visibleItems[index], screenshots, owner, RefreshItems,
                    index < visibleItems.Length - 1));

            loadMore.IsVisible = filteredItems.Length > visibleCount;
            if (loadMore.IsVisible)
                itemsPanel.Children.Add(loadMore);
        }

        foreach (var button in filterButtons)
        {
            button.Click += (_, _) =>
            {
                currentFilter = button.Tag?.ToString() ?? "Show all";
                visibleCount = 20;
                SelectLibraryFilter(filterButtons, button);
                RefreshItems();
            };
        }

        import.Click += async (_, _) =>
        {
            try
            {
                var imported = await MediaLibraryService.ImportAsync(owner, screenshots);
                RefreshItems();
                if (imported.Count > 0)
                {
                    import.Content = imported.Count == 1 ? "Imported" : $"Imported {imported.Count}";
                    if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
                        desktop.MainWindow is MainWindow mainWindow)
                        mainWindow.MarkActivityUnread();
                }
            }
            catch (Exception error)
            {
                import.Content = "Import failed";
                ToolTip.SetTip(import, error.Message);
            }
        };

        search.TextChanged += (_, _) =>
        {
            visibleCount = 20;
            RefreshItems();
        };
        RefreshItems();
        return page;
    }

    private static Control MediaItemCard(MediaLibraryItem item, bool screenshot, Window owner, Action refresh, bool hasSeparator)
    {
        var details = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        details.Children.Add(Label(item.Name, 14, TextBrush, wrap: true, weight: FontWeight.Medium));

        var badges = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        if (!screenshot)
        {
            var modeBadge = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            modeBadge.Children.Add(new Avalonia.Controls.Shapes.Path
            {
                Width = 12,
                Height = 12,
                Data = Geometry.Parse("M3 7h18v13H3z M3 7l2-4 4 2 2-3 4 2 3-2 3 5"),
                Stroke = TextBrush,
                StrokeThickness = 1.5,
                StrokeLineCap = PenLineCap.Round,
            });
            modeBadge.Children.Add(Label(item.Mode, 11, TextBrush));
            badges.Children.Add(new Border
            {
                Padding = new Thickness(8, 4),
                Background = Brush("#292929"),
                CornerRadius = new CornerRadius(16),
                Child = modeBadge
            });
        }
        else
            badges.Children.Add(Label($"{item.Modified:g} · {MediaLibraryService.FormatSize(item.Size)}", 11, MutedBrush));
        details.Children.Add(badges);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        if (screenshot)
        {
            var folderScreenshot = ActionIconButton("M3 6a2 2 0 0 1 2-2h5l2 2h7a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2Z", "Open folder");
            folderScreenshot.Click += (_, _) =>
            {
                try { MediaLibraryService.OpenDirectory(item.Path); }
                catch (Exception error) { ToolTip.SetTip(folderScreenshot, error.Message); }
            };
            actions.Children.Add(folderScreenshot);
            var annotate = ActionIconButton("M12 20h9 M16.5 3.5a2.1 2.1 0 0 1 3 3L8 18l-4 1 1-4Z", "Open in editor");
            annotate.Click += (_, _) =>
            {
                try { new ScreenshotEditorWindow(item.Path).Show(owner); }
                catch (Exception error) { ToolTip.SetTip(annotate, error.Message); }
            };
            actions.Children.Add(annotate);
            var copyImage = ActionIconButton("M8 8h12v12H8z M4 16H3V4h12v1", "Copy image");
            copyImage.Click += async (_, _) =>
            {
                try { await MediaLibraryService.CopyToClipboardAsync(owner, item.Path, image: true); }
                catch (Exception error) { ToolTip.SetTip(copyImage, error.Message); }
            };
            actions.Children.Add(copyImage);
            actions.Children.Add(ActionIconButton("M10 13a5 5 0 0 0 7.5.5l3-3a5 5 0 0 0-7.1-7.1l-1.7 1.7 M14 11a5 5 0 0 0-7.5-.5l-3 3a5 5 0 0 0 7.1 7.1l1.7-1.7", "Share link unavailable"));
        }
        else
        {
            var edit = ActionIconButton("M12 20h9 M16.5 3.5a2.1 2.1 0 0 1 3 3L8 18l-4 1 1-4Z", "Edit");
            edit.Click += (_, _) => new VideoEditorWindow(item.Path).Show(owner);
            actions.Children.Add(edit);
            var folder = ActionIconButton("M3 6a2 2 0 0 1 2-2h5l2 2h7a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2Z", "Open folder");
            folder.Click += (_, _) =>
            {
                try { MediaLibraryService.OpenDirectory(item.Path); }
                catch (Exception error) { ToolTip.SetTip(folder, error.Message); }
            };
            actions.Children.Add(folder);
        }
        var delete = ActionIconButton("M3 6h18 M8 6V4h8v2 M6 6l1 14h10l1-14 M10 10v6 M14 10v6", "Delete");
        ToolTip.SetTip(delete, "Move this file to the system Trash");
        delete.Click += async (_, _) =>
        {
            var confirmation = new ConfirmDeleteWindow(item.Name);
            var confirmed = await confirmation.ShowDialog<bool>(owner);
            if (!confirmed)
                return;
            try
            {
                await MediaLibraryService.MoveToTrashAsync(item.Path);
                if (!screenshot)
                {
                    AppPreferencesStore.Remove("recording.mode." + item.Path);
                    AppPreferencesStore.Remove("trim.start." + item.Path);
                    AppPreferencesStore.Remove("trim.end." + item.Path);
                }
                refresh();
                if (!screenshot && AppPreferencesStore.Get("general.After deleting a recording") == "Reopen window" &&
                    Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
                    desktop.MainWindow is { } mainWindow)
                {
                    mainWindow.Show();
                    mainWindow.Activate();
                }
            }
            catch (Exception error)
            {
                ToolTip.SetTip(delete, error.Message);
            }
        };
        actions.Children.Add(delete);

        var content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("48,*,Auto"),
            ColumnSpacing = 20,
            Margin = new Thickness(12)
        };

        var preview = new Border
        {
            Width = 48,
            Height = 48,
            CornerRadius = new CornerRadius(4),
            ClipToBounds = true,
            Background = Brush("#252525")
        };
        if (screenshot)
        {
            try
            {
                preview.Child = new Image { Source = new Avalonia.Media.Imaging.Bitmap(item.Path), Stretch = Avalonia.Media.Stretch.UniformToFill };
            }
            catch
            {
                preview.Child = Label("Image", 10, MutedBrush, align: HorizontalAlignment.Center);
            }
        }
        else
        {
            preview.Child = Label("Video", 10, MutedBrush, align: HorizontalAlignment.Center);
            Avalonia.Media.Imaging.Bitmap? thumbnailBitmap = null;
            var loadingThumbnail = false;
            var detached = false;
            preview.AttachedToVisualTree += async (_, _) =>
            {
                if (loadingThumbnail || thumbnailBitmap is not null)
                    return;
                loadingThumbnail = true;
                try
                {
                    var thumbnailPath = await MediaLibraryService.CreateVideoThumbnailAsync(item.Path, 0);
                    var bitmap = new Avalonia.Media.Imaging.Bitmap(thumbnailPath);
                    if (detached)
                    {
                        bitmap.Dispose();
                        return;
                    }
                    thumbnailBitmap = bitmap;
                    preview.Child = new Image { Source = bitmap, Stretch = Avalonia.Media.Stretch.UniformToFill };
                }
                catch
                {
                    // Keep the placeholder when FFmpeg or the source video is unavailable.
                }
                finally
                {
                    loadingThumbnail = false;
                }
            };
            preview.DetachedFromVisualTree += (_, _) =>
            {
                detached = true;
                thumbnailBitmap?.Dispose();
                thumbnailBitmap = null;
            };
        }

        content.Children.Add(preview);
        Grid.SetColumn(details, 1);
        Grid.SetColumn(actions, 2);
        content.Children.Add(details);
        content.Children.Add(actions);
        var row = new Border
        {
            BorderBrush = hasSeparator ? PanelBorderBrush : Brushes.Transparent,
            BorderThickness = hasSeparator ? new Thickness(0, 0, 0, 1) : default,
            Child = content
        };
        if (!screenshot)
        {
            var contextMenu = new ContextMenu();
            var play = new MenuItem { Header = "Play" };
            play.Click += (_, _) =>
            {
                try { MediaLibraryService.OpenVideoAt(item.Path); }
                catch { }
            };
            var copy = new MenuItem { Header = "Copy video" };
            copy.Click += async (_, _) =>
            {
                try { await MediaLibraryService.CopyToClipboardAsync(owner, item.Path, image: false); }
                catch { }
            };
            contextMenu.Items.Add(play);
            contextMenu.Items.Add(copy);
            row.ContextMenu = contextMenu;
        }
        return row;
    }

    private static Control BuildAutomationsPage()
    {
        var emptyContent = new StackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(20, 24)
        };
        var iconCircle = new Border
        {
            Width = 44,
            Height = 44,
            CornerRadius = new CornerRadius(22),
            Background = Brush("#282828"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new Avalonia.Controls.Shapes.Path
            {
                Width = 20,
                Height = 20,
                Data = Geometry.Parse("M15.914 4a1.5 1.5 0 0 0-2.474-1.561l-9 9A1.5 1.5 0 0 0 5.5 14h4.002a.5.5 0 0 1 .471.666L8.086 20a1.5 1.5 0 0 0 2.475 1.56l9-9A1.5 1.5 0 0 0 18.5 10h-3.997a.5.5 0 0 1-.472-.667z"),
                Stroke = MutedBrush,
                StrokeThickness = 2,
                StrokeLineCap = PenLineCap.Round,
                StrokeJoin = PenLineJoin.Round,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        emptyContent.Children.Add(iconCircle);
        emptyContent.Children.Add(Label("No automations yet", 13, TextBrush, weight: FontWeight.Medium,
            align: HorizontalAlignment.Center));
        var emptyDescription = Label("Pick a template below to get started in one click, or build your own from scratch.",
            12, MutedBrush, wrap: true, align: HorizontalAlignment.Center, weight: FontWeight.Medium);
        emptyDescription.MaxWidth = 320;
        emptyDescription.LineHeight = 18;
        iconCircle.Margin = new Thickness(0, 0, 0, 5);
        emptyContent.RenderTransform = new TranslateTransform(0, 0);
        emptyContent.Children.Add(emptyDescription);
        var startContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        startContent.Children.Add(new Avalonia.Controls.Shapes.Path
        {
            Width = 14,
            Height = 14,
            Data = Geometry.Parse("M12 5v14 M5 12h14"),
            Stroke = TextBrush,
            StrokeThickness = 2,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round
        });
        startContent.Children.Add(Label("Start from scratch", 12, TextBrush));
        var start = new Button
        {
            Content = startContent,
            Height = 28,
            Padding = new Thickness(12, 0),
            Background = Brush("#2A2A2A"),
            BorderBrush = Brush("#3A3A3A"),
            BorderThickness = new Thickness(1),
            Foreground = TextBrush,
            CornerRadius = new CornerRadius(16),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        start.HorizontalAlignment = HorizontalAlignment.Center;
        start.Margin = new Thickness(0, 5.5, 0, 0);
        start.Click += (_, _) => ToolTip.SetTip(start, "Custom automation rules need Cap's automation service.");
        emptyContent.Children.Add(start);
        var emptyCard = new Border
        {
            Background = CardBrush,
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            MinHeight = 244,
            Margin = new Thickness(-4, 16, -4, 0),
            Child = emptyContent
        };

        var templates = new (string Name, string Description, string Icon)[]
        {
            ("Auto-copy new screenshots to clipboard", "Snap a screenshot and it's right there, ready to paste.", "M10 8h10a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H10a2 2 0 0 1-2-2V10a2 2 0 0 1 2-2z M6 16c-1.1 0-2-.9-2-2V4a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2v2"),
            ("Pull the text out of screenshots", "Cap reads the text in your screenshot and copies it for you.", "M3 7V5a2 2 0 0 1 2-2h2 M17 3h2a2 2 0 0 1 2 2v2 M21 17v2a2 2 0 0 1-2 2h-2 M7 21H5a2 2 0 0 1-2-2v-2 M7 8h8 M7 12h10 M7 16h6"),
            ("Tuck screenshots into a folder", "Send every new screenshot straight to a folder you pick.", "M20 20a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.9a2 2 0 0 1-1.69-.9L9.6 3.9A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2z M12 10v6 M15 13l-3 3-3-3"),
            ("Jump to each new screenshot", "Pop open every screenshot in Finder the moment you take it.", "M6 14l1.5-2.9A2 2 0 0 1 9.24 10H20a2 2 0 0 1 1.94 2.5l-1.54 6a2 2 0 0 1-1.95 1.5H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h3.9a2 2 0 0 1 1.69.9l.81 1.2a2 2 0 0 0 1.67.9H18a2 2 0 0 1 2 2v2"),
            ("Auto-export when you finish recording", "Render an MP4 the second a Studio recording wraps up.", "M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z M7 3v18 M17 3v18 M3 7.5h4 M3 12h18 M3 16.5h4 M17 7.5h4 M17 16.5h4"),
            ("Upload and grab the share link", "Finish a recording and the link is waiting on your clipboard.", "M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71 M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"),
            ("Ping me when an upload is ready", "Get a gentle desktop nudge once your recording is shareable.", "M10.268 21a2 2 0 0 0 3.464 0 M3.262 15.326A1 1 0 0 0 4 17h16a1 1 0 0 0 .74-1.673C19.41 13.956 18 12.499 18 8A6 6 0 0 0 6 8c0 4.499-1.411 5.956-2.738 7.326"),
            ("Tell Slack when you share something", "Send the share link to Slack, Discord, or your own webhook.", "M18 16.98h-5.99c-1.1 0-1.95.94-2.48 1.9A4 4 0 0 1 2 17c.01-.7.2-1.4.57-2 M6 17l3.13-5.78c.53-.97.1-2.18-.5-3.1a4 4 0 1 1 6.89-4.06 M12 6l3.13 5.73C15.66 12.7 16.9 13 18 13a4 4 0 0 1 0 8")
        };
        var templateGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 10,
            RowSpacing = 10,
            Margin = new Thickness(-4, 8, -9, 0)
        };
        for (var index = 0; index < templates.Length; index++)
        {
            var template = templates[index];
            var row = index / 2;
            var column = index % 2;
            if (templateGrid.RowDefinitions.Count <= row)
                templateGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var tile = AutomationTemplateCard(template.Name, template.Description, template.Icon);
            tile.Click += (_, _) => ToolTip.SetTip(tile, "Automation templates need Cap's automation service.");
            Grid.SetRow(tile, row);
            Grid.SetColumn(tile, column);
            templateGrid.Children.Add(tile);
        }

        var page = new StackPanel { Spacing = 0 };
        page.Children.Add(PageHeader("Automations", "Run actions automatically when something happens in Cap. Rules are shared with the Cap CLI.",
            descriptionMaxWidth: 1032, descriptionWeight: FontWeight.Medium));
        page.Children.Add(emptyCard);
        var templatesSection = Section("Templates", "One click to add a ready-made automation. Tweak anything afterwards.", templateGrid);
        templateGrid.Margin = new Thickness(-4, 8, -9, 0);
        templatesSection.Margin = new Thickness(0, 29.5, 0, 0);
        page.Children.Add(templatesSection);
        return page;
    }

    private static Control BuildTranscriptionPage()
    {
        var input = new TextBox
        {
            PlaceholderText = "Add a term",
            Height = 36,
            MinHeight = 36,
            FontSize = 12,
            Padding = new Thickness(12, 0),
            Background = Brush("#111111"),
            BorderBrush = Brush("#242424"),
            CornerRadius = new CornerRadius(8),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        var hints = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var storedHints = AppPreferencesStore.Get("transcription.hints");
        var hintValues = new List<string> { "Cap", "TypeScript", "My Brand Name", "mywebsite.com" };
        try
        {
            if (storedHints is not null)
                hintValues = JsonSerializer.Deserialize<List<string>>(storedHints) ?? new();
        }
        catch
        {
            hintValues = new();
        }
        TextBlock? countLabel = null;
        void PersistHints()
        {
            try { AppPreferencesStore.Set("transcription.hints", JsonSerializer.Serialize(hintValues)); }
            catch { }
        }

        void RefreshHints()
        {
            hints.Children.Clear();
            if (countLabel is not null)
                countLabel.Text = $"{hintValues.Count} {(hintValues.Count == 1 ? "item" : "items")}";
            if (hintValues.Count == 0)
            {
                hints.Children.Add(Label("No remembered terms yet.", 12, MutedBrush));
                return;
            }
            foreach (var hint in hintValues.ToArray())
                hints.Children.Add(TranscriptionChip(hint, () =>
                {
                    hintValues.Remove(hint);
                    PersistHints();
                    RefreshHints();
                }));
        }

        RefreshHints();
        var clear = ActionButton("Clear", primary: false);
        clear.FontSize = 12;
        clear.Height = 28;
        clear.Padding = new Thickness(12, 0);
        clear.CornerRadius = new CornerRadius(16);
        clear.Click += (_, _) =>
        {
            hintValues.Clear();
            PersistHints();
            RefreshHints();
        };

        var rememberTitle = new StackPanel { Spacing = 8 };
        rememberTitle.Children.Add(Label("Remembered terms", 13, TextBrush, weight: FontWeight.Medium));
        var rememberDescription = Label("Add one term at a time to reduce typos and formatting mistakes.", 12, MutedBrush, wrap: true);
        rememberDescription.MaxWidth = 420;
        rememberDescription.LineHeight = 18;
        rememberTitle.Children.Add(rememberDescription);
        var topRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        topRow.Children.Add(rememberTitle);
        Grid.SetColumn(clear, 1);
        clear.VerticalAlignment = VerticalAlignment.Top;
        topRow.Children.Add(clear);

        var addContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        addContent.Children.Add(new Avalonia.Controls.Shapes.Path
        {
            Width = 14,
            Height = 14,
            Data = Geometry.Parse("M12 5v14 M5 12h14"),
            Stroke = TextBrush,
            StrokeThickness = 1.8
        });
        addContent.Children.Add(Label("Add", 12, TextBrush));
        var add = new Button
        {
            Content = addContent,
            Height = 36,
            Padding = new Thickness(12, 0),
            Background = Brush("#2A2A2A"),
            BorderBrush = Brush("#3A3A3A"),
            BorderThickness = new Thickness(1),
            Foreground = TextBrush,
            CornerRadius = new CornerRadius(18)
        };
        add.IsEnabled = false;
        input.TextChanged += (_, _) => add.IsEnabled = !string.IsNullOrWhiteSpace(input.Text);
        add.Click += (_, _) =>
        {
            var value = input.Text?.Trim();
            if (string.IsNullOrWhiteSpace(value) || hintValues.Contains(value, StringComparer.OrdinalIgnoreCase))
                return;
            hintValues.Add(value);
            PersistHints();
            RefreshHints();
            input.Clear();
            add.IsEnabled = false;
        };

        var entry = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        entry.Children.Add(input);
        Grid.SetColumn(add, 1);
        entry.Children.Add(add);

        var rememberCardContent = new StackPanel { Spacing = 16, Margin = new Thickness(16) };
        rememberCardContent.Children.Add(topRow);
        rememberCardContent.Children.Add(entry);
        rememberCardContent.Children.Add(Label("These hints are applied when you generate captions in the editor.", 12, MutedBrush, wrap: true));
        var rememberCard = Card(rememberCardContent);
        rememberCard.MinHeight = 166;
        rememberCard.Margin = new Thickness(-4, 0, -4, 0);
        var activeHeader = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        activeHeader.Children.Add(Label("Active hints", 14, TextBrush, weight: FontWeight.SemiBold));
        var count = Label($"{hintValues.Count} {(hintValues.Count == 1 ? "item" : "items")}", 12, MutedBrush);
        countLabel = count;
        Grid.SetColumn(count, 1);
        activeHeader.Children.Add(count);
        var hintsCard = Card(hints);
        hintsCard.Margin = new Thickness(-4, 6.5, -4, 0);
        hintsCard.Child = new Border { Padding = new Thickness(16), Child = hints };
        var activeSection = new StackPanel { Spacing = 12 };
        activeSection.Children.Add(activeHeader);
        activeSection.Children.Add(hintsCard);
        return PageWithHeader(PageHeader("Transcription", "Add names, spellings, domains, and capitalization preferences that caption generation should keep in mind."),
            rememberCard, activeSection);
    }

    private static Control TranscriptionChip(string text, Action remove)
    {
        var contents = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        contents.Children.Add(Label(text, 12, TextBrush));
        var dismiss = new Button
        {
            Content = "×",
            Width = 16,
            Height = 20,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = TextBrush,
            FontSize = 16,
            CornerRadius = new CornerRadius(10)
        };
        dismiss.Click += (_, _) => remove();
        contents.Children.Add(dismiss);
        return new Border
        {
            Padding = new Thickness(10, 3),
            Background = Brush("#292929"),
            BorderBrush = Brush("#3A3A3A"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Child = contents
        };
    }

    private static Control BuildIntegrationsPage()
    {
        var integrations = new StackPanel { Spacing = 12 };
        integrations.Children.Add(IntegrationCard(
            "Google Drive",
            "Connect Google Drive for new shareable link uploads. Cap stores new videos in a private Cap folder in your Drive and continues serving them through Cap after normal access checks.",
            "Upgrade to Pro"));
        integrations.Children.Add(IntegrationCard(
            "S3 Config",
            "Connect your own S3 bucket for complete control over your data storage. All new shareable link uploads will be automatically uploaded to your configured S3 bucket, ensuring you maintain complete ownership and control over your content. Perfect for organizations requiring data sovereignty and custom storage policies.",
            "Upgrade to Pro"));
        return Section("Integrations", "Configure integrations to extend Cap's functionality and connect with third-party services.", integrations);
    }

    private static Control BuildLicensePage()
    {
        var planContent = new StackPanel { Spacing = 12, Margin = new Thickness(16) };
        planContent.Children.Add(Label("Cap Free", 18, TextBrush, weight: FontWeight.SemiBold));
        planContent.Children.Add(Label("Record and edit locally for personal use. Choose a paid plan for commercial use or more cloud features.",
            12, MutedBrush, wrap: true));
        var checking = ActionButton("Refresh plan", primary: false);
        checking.Height = 28;
        checking.HorizontalAlignment = HorizontalAlignment.Left;
        checking.IsEnabled = false;
        ToolTip.SetTip(checking, "Online plan refresh is not connected in this local build.");
        planContent.Children.Add(checking);
        var planCard = Card(planContent);
        planCard.MinHeight = 144;

        var planDetails = new StackPanel { Spacing = 12, Margin = new Thickness(16) };
        var desktopLicense = Label("", 12, MutedBrush);
        desktopLicense.Inlines!.Add(new Avalonia.Controls.Documents.Run("Desktop License") { Foreground = TextBrush, FontWeight = FontWeight.Medium });
        desktopLicense.Inlines!.Add(new Avalonia.Controls.Documents.Run("  ·  Commercial use of the desktop recorder and editor."));
        var proPlan = Label("", 12, MutedBrush);
        proPlan.Inlines!.Add(new Avalonia.Controls.Documents.Run("Cap Pro") { Foreground = TextBrush, FontWeight = FontWeight.Medium });
        proPlan.Inlines!.Add(new Avalonia.Controls.Documents.Run("  ·  A desktop license, plus cloud sharing, AI features and collaboration."));
        planDetails.Children.Add(desktopLicense);
        planDetails.Children.Add(proPlan);
        var plansButton = ExternalAction("View plans & pricing ↗", "https://cap.so/pricing", primary: true);
        plansButton.Height = 28;
        plansButton.HorizontalAlignment = HorizontalAlignment.Left;
        planDetails.Children.Add(plansButton);
        planDetails.Children.Add(Label("Opens cap.so/pricing in your browser.", 12, MutedBrush));
        var planOptions = Card(planDetails);
        planOptions.MinHeight = 155;

        var licenseKey = new TextBox
        {
            PlaceholderText = "License key",
            Height = 36,
            MinHeight = 36,
            Padding = new Thickness(12, 0),
            Background = Brush("#242424"),
            BorderBrush = Brush("#383838"),
            CornerRadius = new CornerRadius(8),
            FontWeight = FontWeight.Normal,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        var activate = ActionButton("Activate License", primary: false);
        activate.IsEnabled = false;
        activate.HorizontalAlignment = HorizontalAlignment.Center;
        activate.MinWidth = 126;
        var licenseContent = new StackPanel { Spacing = 10.75, Margin = new Thickness(24) };
        licenseContent.Children.Add(Label("Have a license key?", 20, TextBrush, align: HorizontalAlignment.Center,
            weight: FontWeight.Medium));
        licenseContent.Children.Add(licenseKey);
        activate.Margin = new Thickness(0, 18, 0, 0);
        licenseContent.Children.Add(activate);
        var licenseCard = Card(licenseContent);
        licenseCard.MinHeight = 186;

        var yourPlan = Section("Your plan", null, planCard);
        yourPlan.Margin = new Thickness(0, 0, 0, -3);
        var explorePlans = Section("Explore plans", "Compare current pricing and everything included on our website.", planOptions);
        explorePlans.Margin = new Thickness(0, 0, 0, -3);

        return Page("Plan & license", "Your Cap plan and desktop license, in one place.",
            yourPlan,
            explorePlans,
            licenseCard);
    }

    private static Control BuildExperimentalPage()
    {
        var content = new StackPanel { Spacing = 28 };
        content.Children.Add(Section("Preview", null, Card(DisabledToggleRow(
            "Native camera preview",
            "Render the camera preview using a native GPU surface instead of through the webview. Experimental and off by default."))));
        content.Children.Add(Section("Reliability", null, Card(DisabledToggleRow(
            "Out-of-process muxer",
            "Run the fragmented-MP4 muxer in an isolated subprocess so muxer crashes can't take down your recording. Requires the bundled cap-muxer binary."))));
        content.Children.Add(Section("Native app", null, Card(DisabledToggleRow(
            "Cap GPUI",
            "Close this app and reopen the experimental fully-native version of Cap. It is unfinished, so expect missing features. Your recordings and settings are shared, and you can switch back from its Experimental settings."))));
        return content;
    }

    private static Control BuildFeedbackPage()
    {
        var message = new TextBox
        {
            PlaceholderText = "Online feedback is not connected in this local build.",
            AcceptsReturn = true,
            Height = 128,
            MinHeight = 128,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            VerticalContentAlignment = VerticalAlignment.Top,
            Padding = new Thickness(8, 10),
            Background = Brush("#1A1A1A"),
            BorderBrush = Brush("#303030"),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(-4, 0, -6.5, 0),
            IsEnabled = false
        };
        var submit = ActionButton("Submit Feedback", primary: false);
        submit.IsEnabled = false;
        submit.HorizontalAlignment = HorizontalAlignment.Left;
        submit.Classes.Add("action");
        submit.FontSize = 13;
        submit.Height = 38;
        submit.Padding = new Thickness(12, 0);
        submit.Background = Brush("#1C1C1C");
        submit.BorderBrush = Brush("#303030");
        submit.Foreground = Brush("#777777");
        submit.Opacity = 1;
        ToolTip.SetTip(submit, "Online feedback is not connected in this Avalonia build.");
        var submitFace = new Border
        {
            Background = Brush("#1C1C1C"),
            BorderBrush = Brush("#303030"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(22),
            Child = Label("Submit Feedback", 13, Brush("#777777"), align: HorizontalAlignment.Center)
        };
        var submitButtonHost = new Grid { Width = 134, Height = 38, HorizontalAlignment = HorizontalAlignment.Left };
        submitButtonHost.Margin = new Thickness(-4, 0, 0, 0);
        submitButtonHost.Children.Add(submitFace);
        submit.Width = 134;
        submit.Height = 38;
        submit.Opacity = 0;
        submit.HorizontalAlignment = HorizontalAlignment.Stretch;
        submit.VerticalAlignment = VerticalAlignment.Stretch;
        submitButtonHost.Children.Add(submit);
        var feedbackForm = new StackPanel { Spacing = 12.5 };
        feedbackForm.Children.Add(message);
        feedbackForm.Children.Add(submitButtonHost);

        var communityButton = ExternalAction("Join Discord", "https://cap.link/discord", primary: false);
        communityButton.HorizontalAlignment = HorizontalAlignment.Left;
        communityButton.FontSize = 13;
        communityButton.Padding = new Thickness(12, 0);
        communityButton.Height = 38;
        var community = Section("Join the Community",
            "Have questions, want to share ideas, or just hang out? Join the Cap Discord community.", communityButton);
        if (community is StackPanel communityPanel && communityPanel.Children.Count > 1 && communityPanel.Children[1] is TextBlock communityDescription)
            communityDescription.LineHeight = 19.5;
        communityButton.Margin = new Thickness(-4, 8, -4, 0);
        if (community is StackPanel communitySpacingPanel)
            communitySpacingPanel.Spacing = 2;
        community.Margin = new Thickness(0, 2.5, 0, 0);

        var warningText = "The audio/video sync diagnostic is not included in this local build. Screen capture permission and FFmpeg availability are shown in System Information below.";
        var warningContent = new Grid { ColumnDefinitions = new ColumnDefinitions("14,*"), ColumnSpacing = 10, VerticalAlignment = VerticalAlignment.Top };
        warningContent.Children.Add(new Avalonia.Controls.Shapes.Path
        {
            Width = 14,
            Height = 14,
            Data = Geometry.Parse("M12 3 2 21h20L12 3z M12 9v4 M12 17h.01"),
            Stroke = Brush("#F2C14E"),
            StrokeThickness = 1.2,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Top
        });
        var warningLabel = Label(warningText, 12, MutedBrush, wrap: true);
        warningLabel.LineHeight = 19.5;
        Grid.SetColumn(warningLabel, 1);
        warningContent.Children.Add(warningLabel);
        var warning = new Border
        {
            Padding = new Thickness(12, 8.25),
            Background = Brush("#2A220C"),
            BorderBrush = Brush("#8D650C"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Child = warningContent
        };

        var pipelines = Select(new[] { "Studio and Instant", "Studio", "Instant" });
        pipelines.Width = 146;
        pipelines.IsEnabled = false;
        var duration = Select(new[] { "20 seconds", "10 seconds", "30 seconds" });
        duration.Width = 110;
        duration.IsEnabled = false;
        var microphone = new ToggleSwitch { IsChecked = false, Width = 35, OnContent = string.Empty, OffContent = string.Empty, IsEnabled = false };
        var diagnosticOptions = Card(
            SettingRow("Pipelines to test", "Studio recordings, Instant recordings, or both in one run.", pipelines, 46),
            new Border { Height = 1, Background = Brush("#303030") },
            SettingRow("Test duration", "Longer runs are more sensitive to slow drift.", duration, 46),
            new Border { Height = 1, Background = Brush("#303030") },
            SettingRow("Test microphone too", "Also records your microphone and checks it against the beeps it can hear.", microphone, 46));
        var runDiagnostic = ActionButton("Run Diagnostic", primary: true);
        runDiagnostic.FontSize = 13;
        runDiagnostic.Padding = new Thickness(12, 0);
        runDiagnostic.Height = 38;
        runDiagnostic.HorizontalAlignment = HorizontalAlignment.Left;
        runDiagnostic.IsEnabled = false;
        ToolTip.SetTip(runDiagnostic, "The audio/video diagnostic is not connected in this local build.");
        var diagnosticContent = new StackPanel { Spacing = 10 };
        diagnosticContent.Children.Add(warning);
        diagnosticContent.Children.Add(diagnosticOptions);
        diagnosticContent.Children.Add(runDiagnostic);
        var diagnostics = Section("Diagnostic Report",
            "The audio/video sync test and diagnostic report are not connected in this local build.",
            diagnosticContent);
        if (diagnostics is StackPanel diagnosticsPanel && diagnosticsPanel.Children.Count > 1 && diagnosticsPanel.Children[1] is TextBlock diagnosticsDescription)
            diagnosticsDescription.LineHeight = 19.5;
        diagnosticContent.Margin = new Thickness(-4, 6.5, -6.5, 0);
        diagnosticContent.Spacing = 10.5;
        if (diagnostics is StackPanel diagnosticSpacingPanel)
        {
            diagnosticSpacingPanel.Spacing = 3;
            diagnostics.Margin = new Thickness(0, 1, 0, 0);
        }

        var uploadLogs = ActionButton("Upload Logs", primary: false);
        uploadLogs.FontSize = 13;
        uploadLogs.Padding = new Thickness(12, 0);
        uploadLogs.Height = 38;
        uploadLogs.HorizontalAlignment = HorizontalAlignment.Left;
        uploadLogs.IsEnabled = false;
        ToolTip.SetTip(uploadLogs, "Log upload is not connected in this local build.");
        var debug = Section("Debug Information",
            "Upload Cap's log file to help us diagnose issues. It records what the app did, which can include file paths and the names of things you recorded.",
            uploadLogs);
        if (debug is StackPanel debugPanel && debugPanel.Children.Count > 1 && debugPanel.Children[1] is TextBlock debugDescription)
            debugDescription.LineHeight = 19.5;

        var osName = OperatingSystem.IsMacOS() ? "macOS" : OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsLinux() ? "Linux" : "Unknown OS";
        var osDescription = $"{osName} {Environment.OSVersion.Version} ({RuntimeInformation.ProcessArchitecture})";
        var osInfo = new StackPanel { Spacing = 4 };
        osInfo.Children.Add(Label("Operating System", 14, MutedBrush, weight: FontWeight.Medium));
        osInfo.Children.Add(new Border
        {
            Padding = new Thickness(8, 6),
            Background = Brush("#1A1A1A"),
            CornerRadius = new CornerRadius(2),
            Child = new TextBlock { Text = osDescription, FontSize = 12, FontFamily = "monospace", Foreground = MutedBrush }
        });

        var captureInfo = new StackPanel { Spacing = 4 };
        captureInfo.Children.Add(Label("Capture Support", 14, MutedBrush, weight: FontWeight.Medium));
        var capturePermissionAvailable = OperatingSystem.IsMacOS() && MacScreenCaptureAccess.HasPermission;
        var ffmpegAvailable = MediaLibraryService.IsFfmpegAvailable;
        var captureStatus = !OperatingSystem.IsMacOS()
            ? "Screen Capture: Platform support varies"
            : capturePermissionAvailable
                ? "Screen Capture: Permission granted"
                : "Screen Capture: Permission required";
        var captureStatusColor = capturePermissionAvailable ? "#4ADE80" : "#F6C453";
        captureInfo.Children.Add(new Border
        {
            Padding = new Thickness(8, 6),
            Background = Brush("#1A1A1A"),
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new StackPanel { Spacing = 4, Children =
            {
                Label(captureStatus, 12, Brush(captureStatusColor)),
                Label("FFmpeg: " + (ffmpegAvailable ? "Available" : "Not found"), 12, Brush(ffmpegAvailable ? "#4ADE80" : "#F6C453"))
            }}
        });

        var encoderInfo = new StackPanel { Spacing = 4 };
        encoderInfo.Children.Add(Label("Available Encoders", 14, MutedBrush, weight: FontWeight.Medium));
        var encoders = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var availableEncoders = MediaLibraryService.GetAvailableVideoEncoders();
        foreach (var encoder in new[] { "h264_videotoolbox", "libx264", "hevc_videotoolbox", "libx265" }
                     .Where(availableEncoders.Contains))
        {
            encoders.Children.Add(new Border
            {
                Padding = new Thickness(8, 4),
                Background = Brush("#1A1A1A"),
                CornerRadius = new CornerRadius(2),
                Child = new TextBlock { Text = encoder, FontSize = 12, FontFamily = "monospace", Foreground = MutedBrush }
            });
        }
        if (encoders.Children.Count == 0)
            encoders.Children.Add(Label("No supported FFmpeg encoders detected", 12, MutedBrush));
        encoderInfo.Children.Add(encoders);
        var systemContent = new StackPanel { Spacing = 12 };
        systemContent.Children.Add(osInfo);
        systemContent.Children.Add(captureInfo);
        systemContent.Children.Add(encoderInfo);
        var system = Section("System Information", null, systemContent);
        systemContent.Margin = new Thickness(-4, 8, -6.5, 0);

        var page = new StackPanel { Spacing = 28 };
        page.Children.Add(PageHeader("Feedback", "Online feedback submission and log upload are not connected in this local build."));
        feedbackForm.Margin = new Thickness(0, -16, 0, 0);
        page.Children.Add(feedbackForm);
        page.Children.Add(community);
        page.Children.Add(diagnostics);
        page.Children.Add(debug);
        page.Children.Add(system);
        return page;
    }

    private static Control BuildChangelogPage()
    {
        return Page("Changelog", "Features included in this Avalonia build.",
            Section("Recorder", null, Card(
                ChangelogEntry("Capture targets", "Record a display, selected area, or camera, with optional audio inputs."),
                Separator(),
                ChangelogEntry("Local libraries", "Import, search, edit, annotate, and organize recordings and screenshots."),
                Separator(),
                ChangelogEntry("Screenshot markup", "Draw with a pen, add arrows and rectangles, highlight details, and place text before exporting a PNG."))),
            Section("Tools", null, Card(
                ChangelogEntry("Video editor", "Preview recordings, set trim points, and export an MP4 clip."),
                Separator(),
                ChangelogEntry("Teleprompter and backups", "Save a script locally and copy finished media to another folder automatically."))));
    }

    private static Control BuildMessagePage(string section) => Page(section, "Cap settings for this section.",
        Section(section, null, EmptyState("No items yet", "This view is ready for Cap content.", null)));

    private static Control Page(string title, string description, params Control[] sections)
    {
        var content = new StackPanel { Spacing = 32.5 };
        content.Children.Add(PageHeader(title, description));
        foreach (var section in sections)
            content.Children.Add(section);
        return content;
    }

    private static Control PageWithHeader(Control header, params Control[] sections)
    {
        var content = new StackPanel { Spacing = 16 };
        content.Children.Add(header);
        foreach (var section in sections)
            content.Children.Add(section);
        return content;
    }

    private static Control PageHeader(string title, string description, Control? action = null, double descriptionMaxWidth = 510,
        FontWeight? descriptionWeight = null)
    {
        var text = new StackPanel { Spacing = 4 };
        text.Children.Add(Label(title, 14, TextBrush, weight: FontWeight.SemiBold));
        var descriptionLabel = Label(description, 12, MutedBrush, wrap: true, weight: descriptionWeight);
        descriptionLabel.MaxWidth = descriptionMaxWidth;
        text.Children.Add(descriptionLabel);

        if (action is null)
            return text;

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        header.Children.Add(text);
        Grid.SetColumn(action, 1);
        header.Children.Add(action);
        return header;
    }

    private static Control Section(string title, string? description, Control content, double contentTopMargin = 6.5,
        double descriptionMaxWidth = 510)
    {
        var section = new StackPanel { Spacing = 4 };
        section.Children.Add(Label(title, 14, TextBrush, weight: FontWeight.SemiBold));
        if (!string.IsNullOrWhiteSpace(description))
        {
            var descriptionLabel = Label(description, 12, MutedBrush, wrap: true);
            descriptionLabel.MaxWidth = descriptionMaxWidth;
            descriptionLabel.LineHeight = 16.5;
            section.Children.Add(descriptionLabel);
        }
        if (content is Border border)
            border.Margin = new Thickness(-4, contentTopMargin, -4, 0);
        else
            content.Margin = new Thickness(-4, 8, -4, 0);
        section.Children.Add(content);
        return section;
    }

    private static Border Card(params Control[] children)
    {
        var stack = new StackPanel();
        foreach (var child in children)
            stack.Children.Add(child);
        return new Border
        {
            Background = CardBrush,
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Child = stack
        };
    }

    private static Control ToggleRow(string label, string description, bool isChecked)
    {
        var toggle = new ToggleSwitch { IsChecked = isChecked, Width = 38, OnContent = string.Empty, OffContent = string.Empty, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        return SettingRow(label, description, toggle, 62);
    }

    private static Control DisabledToggleRow(string label, string description)
    {
        var toggle = new ToggleSwitch { IsChecked = false, Width = 38, IsEnabled = false, OnContent = string.Empty, OffContent = string.Empty };
        return SettingRow(label, description, toggle, 62);
    }

    private static Control SettingRow(string label, string description, Control? control, double minHeight = 62)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            MinHeight = minHeight,
            Margin = new Thickness(12, 8)
        };
        var text = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(Label(label, 13, TextBrush, wrap: true));
        if (!string.IsNullOrWhiteSpace(description))
            text.Children.Add(Label(description, 12, MutedBrush, wrap: true));
        grid.Children.Add(text);
        if (control is not null)
        {
            Grid.SetColumn(control, 1);
            control.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(control);
        }
        return grid;
    }

    private static Control SelectRow(string label, string description, string[] values, int selected = 0)
    {
        return SettingRow(label, description, Select(values, selected));
    }

    private static ComboBox Select(IEnumerable<string> values, int selected = 0)
    {
        var combo = new ComboBox
        {
            Width = 130,
            MinHeight = 30,
            FontSize = 12,
            Background = Brush("#282828"),
            BorderBrush = Brush("#383838"),
            Foreground = TextBrush,
            CornerRadius = new CornerRadius(9)
        };
        foreach (var value in values)
            combo.Items.Add(new ComboBoxItem { Content = value });
        combo.SelectedIndex = selected;
        return combo;
    }

    private static Control ResolutionChoices()
    {
        var source = new Border
        {
            MinHeight = 48,
            Margin = new Thickness(16, 2, 16, 4),
            Padding = new Thickness(12, 10),
            Background = Brush("#0C2A49"),
            BorderBrush = Brush("#1684FF"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Child = Label("Source resolution", 12, TextBrush)
        };
        return source;
    }

    private static Control ChoiceList(string preferenceKey, params (string Label, string Badge, string Description)[] choices)
    {
        var stack = new StackPanel { Spacing = 8, Margin = new Thickness(16, 16, 16, 5), HorizontalAlignment = HorizontalAlignment.Stretch };
        var buttons = new List<Button>();
        var selectedLabel = AppPreferencesStore.Get(preferenceKey) ?? choices.FirstOrDefault().Label;
        Button? selectedButton = null;
        foreach (var choice in choices)
        {
            var content = new StackPanel { Spacing = 5 };
            var heading = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            heading.Children.Add(Label(choice.Label, 13, TextBrush, weight: FontWeight.Medium));
            if (!string.IsNullOrWhiteSpace(choice.Badge))
                heading.Children.Add(new Border { Padding = new Thickness(6, 3), Background = Brush("#063B69"), CornerRadius = new CornerRadius(4), Child = Label(choice.Badge, 11, Brush("#79B8FF")) });
            content.Children.Add(heading);
            var description = Label(choice.Description, 12, MutedBrush, wrap: true);
            description.MaxWidth = 440;
            description.LineHeight = 20;
            content.Children.Add(description);
            var choiceGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("16,*"), ColumnSpacing = 12, VerticalAlignment = VerticalAlignment.Top };
            var radio = new Border
            {
                Width = 16,
                Height = 16,
                BorderThickness = new Thickness(1),
                BorderBrush = Brush("#555555"),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 2, 0, 0),
                Child = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = Brushes.Transparent }
            };
            choiceGrid.Children.Add(radio);
            Grid.SetColumn(content, 1);
            choiceGrid.Children.Add(content);
            var button = new Button
            {
                Content = choiceGrid,
                Padding = new Thickness(12, 12.25),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = PanelBrush,
                BorderBrush = PanelBorderBrush,
                BorderThickness = new Thickness(1),
                Foreground = TextBrush,
                CornerRadius = new CornerRadius(10),
                MinHeight = 73,
                Tag = radio
            };
            buttons.Add(button);
            stack.Children.Add(button);
            if (choice.Label == selectedLabel)
                selectedButton = button;
            button.Click += (_, _) =>
            {
                SetQualityChoice(buttons, button);
                try { AppPreferencesStore.Set(preferenceKey, choice.Label); }
                catch { }
            };
        }
        SetQualityChoice(buttons, selectedButton ?? buttons[0]);
        return stack;
    }

    private static void SetQualityChoice(IEnumerable<Button> buttons, Button selected)
    {
        foreach (var button in buttons)
        {
            var active = ReferenceEquals(button, selected);
            button.Background = Brush(active ? "#0C2A49" : "#1A1A1A");
            button.BorderBrush = Brush(active ? "#1684FF" : "#303030");
            if (button.Tag is Border radio)
            {
                radio.BorderBrush = Brush(active ? "#1684FF" : "#555555");
                if (radio.Child is Border dot)
                    dot.Background = active ? Brush("#1684FF") : Brushes.Transparent;
            }
        }
    }

    private static Control ChoicePills(params string[] labels)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var buttons = new List<Button>();
        foreach (var label in labels)
        {
            var button = ActionButton(label, primary: false);
            button.CornerRadius = new CornerRadius(20);
            button.Padding = new Thickness(14, 7);
            buttons.Add(button);
            row.Children.Add(button);
            button.Click += (_, _) => SelectChoice(buttons, button);
        }
        if (buttons.Count > 0)
            SelectChoice(buttons, buttons[0]);
        return row;
    }

    private static void SelectChoice(IEnumerable<Button> buttons, Button selected)
    {
        foreach (var button in buttons)
        {
            var active = ReferenceEquals(button, selected);
            button.Background = Brush(active ? "#252525" : "#1A1A1A");
            button.BorderBrush = Brush(active ? "#1684FF" : "#303030");
            button.Foreground = TextBrush;
        }
    }

    private static void SelectLibraryFilter(IEnumerable<Button> buttons, Button selected)
    {
        foreach (var button in buttons)
        {
            var active = ReferenceEquals(button, selected);
            button.Background = Brush(active ? "#252525" : "#1A1A1A");
            button.BorderBrush = Brush(active ? "#303030" : "#252525");
        }
    }

    private static Control ShortcutRow(string name, string key, string defaultValue)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), MinHeight = 48.4, Margin = new Thickness(16, 4, 16, 4) };
        row.Children.Add(Label(name, 13, TextBrush, wrap: true));
        var value = AppPreferencesStore.Get("shortcut." + key) ?? defaultValue;
        var button = ActionButton(FormatShortcut(value), primary: false);
        button.FontSize = 11;
        button.Foreground = MutedBrush;
        button.Background = Brush("#303030");
        button.BorderBrush = Brush("#3A3A3A");
        button.MinWidth = 52;
        button.Height = 26;
        button.Padding = new Thickness(8, 0);
        button.CornerRadius = new CornerRadius(8);
        button.Focusable = true;
        var listening = false;
        button.Click += (_, _) =>
        {
            listening = true;
            MacGlobalHotkeyService.SuspendCurrent();
            button.Content = "Press keys…";
            button.Focus();
        };
        button.KeyDown += (_, e) =>
        {
            if (!listening)
                return;
            e.Handled = true;
            if (e.Key == Avalonia.Input.Key.Escape)
            {
                listening = false;
                button.Content = FormatShortcut(AppPreferencesStore.Get("shortcut." + key) ?? defaultValue);
                RefreshGlobalHotkeys(button);
                return;
            }
            if (e.Key is Avalonia.Input.Key.Back or Avalonia.Input.Key.Delete)
            {
                try
                {
                    AppPreferencesStore.Set("shortcut." + key, "NONE");
                    button.Content = "NONE";
                }
                catch (Exception error)
                {
                    ToolTip.SetTip(button, error.Message);
                }
                listening = false;
                RefreshGlobalHotkeys(button);
                return;
            }
            var modifiers = e.KeyModifiers;
            if (e.Key is Avalonia.Input.Key.LeftShift or Avalonia.Input.Key.RightShift or
                Avalonia.Input.Key.LeftCtrl or Avalonia.Input.Key.RightCtrl or
                Avalonia.Input.Key.LeftAlt or Avalonia.Input.Key.RightAlt or
                Avalonia.Input.Key.LWin or Avalonia.Input.Key.RWin)
                return;
            var parts = new List<string>();
            var usesPrimary = OperatingSystem.IsMacOS()
                ? modifiers.HasFlag(Avalonia.Input.KeyModifiers.Meta)
                : modifiers.HasFlag(Avalonia.Input.KeyModifiers.Control);
            if (usesPrimary)
                parts.Add("Primary");
            if (modifiers.HasFlag(Avalonia.Input.KeyModifiers.Control) &&
                !(usesPrimary && !OperatingSystem.IsMacOS()))
                parts.Add("Control");
            if (modifiers.HasFlag(Avalonia.Input.KeyModifiers.Meta) &&
                !(usesPrimary && OperatingSystem.IsMacOS()))
                parts.Add("Meta");
            if (modifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift))
                parts.Add("Shift");
            if (modifiers.HasFlag(Avalonia.Input.KeyModifiers.Alt))
                parts.Add("Alt");
            if (parts.Count == 0)
            {
                button.Content = "Add modifier";
                return;
            }
            parts.Add(e.Key.ToString());
            var binding = string.Join('+', parts);
            try
            {
                AppPreferencesStore.Set("shortcut." + key, binding);
                button.Content = FormatShortcut(binding);
            }
            catch (Exception error)
            {
                ToolTip.SetTip(button, error.Message);
            }
            listening = false;
            RefreshGlobalHotkeys(button);
        };
        Grid.SetColumn(button, 1);
        row.Children.Add(button);
        return row;
    }

    private static void RefreshGlobalHotkeys(Control control)
    {
        ToolTip.SetTip(control, MacGlobalHotkeyService.RefreshCurrent());
    }

    private static string FormatShortcut(string shortcut)
    {
        var parts = shortcut.Split('+', StringSplitOptions.RemoveEmptyEntries).Select(part => part switch
        {
            "Primary" => OperatingSystem.IsMacOS() ? "⌘" : "Ctrl",
            "Control" => OperatingSystem.IsMacOS() ? "⌃" : "Ctrl",
            "Meta" => "⌘",
            "Shift" => OperatingSystem.IsMacOS() ? "⇧" : "Shift",
            "Alt" => OperatingSystem.IsMacOS() ? "⌥" : "Alt",
            "D1" => "1",
            "D2" => "2",
            "D3" => "3",
            _ => part
        }).ToArray();
        return OperatingSystem.IsMacOS()
            ? string.Concat(parts)
            : string.Join('+', parts);
    }

    private static Control PathRow(string label, string value)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12, Margin = new Thickness(14, 8) };
        row.Children.Add(Label(label, 12, MutedBrush, VerticalAlignment.Center));
        var path = new Border
        {
            Padding = new Thickness(8, 5),
            Background = Brush("#242424"),
            CornerRadius = new CornerRadius(7),
            Child = new TextBlock { Text = value, FontFamily = "monospace", FontSize = 11, Foreground = TextBrush, TextTrimming = TextTrimming.CharacterEllipsis }
        };
        Grid.SetColumn(path, 1);
        row.Children.Add(path);
        return row;
    }

    private static Control CliPathRow(string label, string value)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("64,Auto,*"), ColumnSpacing = 12 };
        row.Children.Add(Label(label, 12, MutedBrush, VerticalAlignment.Center));
        var path = new Border
        {
            Padding = new Thickness(8, 5),
            Background = Brush("#292929"),
            CornerRadius = new CornerRadius(7),
            MaxWidth = 314,
            MinWidth = label == "Command" ? 212 : 0,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = value,
                FontFamily = "monospace",
                FontSize = 11,
                Foreground = TextBrush,
                TextTrimming = TextTrimming.CharacterEllipsis
            }
        };
        Grid.SetColumn(path, 1);
        row.Children.Add(path);
        return row;
    }

    private static Button AutomationTemplateCard(string title, string description, string iconPath)
    {
        var icon = new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(8),
            Background = Brush("#282828"),
            Child = new Avalonia.Controls.Shapes.Path
            {
                Width = 18,
                Height = 18,
                Data = Geometry.Parse(iconPath),
                Stroke = MutedBrush,
                StrokeThickness = 2,
                StrokeLineCap = PenLineCap.Round,
                StrokeJoin = PenLineJoin.Round,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(Label(title, 13, TextBrush, wrap: true, weight: FontWeight.Medium, verticalAlignment: VerticalAlignment.Top));
        text.Children.Add(Label(description, 11, MutedBrush, wrap: true, verticalAlignment: VerticalAlignment.Top));
        var content = new Grid { ColumnDefinitions = new ColumnDefinitions("36,*"), ColumnSpacing = 12, VerticalAlignment = VerticalAlignment.Top };
        content.Children.Add(icon);
        Grid.SetColumn(text, 1);
        content.Children.Add(text);
        return new Button
        {
            Content = content,
            Padding = new Thickness(12),
            MinHeight = 98,
            Background = CardBrush,
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Top
        };
    }

    private static Control SearchBox(string watermark) => new TextBox
    {
        PlaceholderText = "Search",
        Height = 36,
        MinHeight = 36,
        Width = 260,
        HorizontalAlignment = HorizontalAlignment.Left,
        Background = Brush("#222222"),
        BorderBrush = PanelBorderBrush,
        Foreground = TextBrush,
        CornerRadius = new CornerRadius(9),
        Padding = new Thickness(24, 0, 8, 0),
        Margin = new Thickness(0, 0, 0, 0)
    };

    private static Control SearchField(TextBox input)
    {
        var field = new Grid { Width = 260, Height = 36, HorizontalAlignment = HorizontalAlignment.Left };
        field.Children.Add(input);
        field.Children.Add(new Avalonia.Controls.Shapes.Path
        {
            Width = 12,
            Height = 12,
            Data = Geometry.Parse("M11 11 20 20 M17 10a7 7 0 1 1-14 0 7 7 0 0 1 14 0Z"),
            Stroke = MutedBrush,
            StrokeThickness = 1.8,
            StrokeLineCap = PenLineCap.Round,
            Margin = new Thickness(8, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        });
        return field;
    }

    private static Button ActionIconButton(string pathData, string tooltip)
    {
        var icon = new Avalonia.Controls.Shapes.Path
        {
            Width = 16,
            Height = 16,
            Data = Geometry.Parse(pathData),
            Stroke = MutedBrush,
            StrokeThickness = 1.8,
            StrokeLineCap = PenLineCap.Round,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var button = new Button
        {
            Content = icon,
            Width = 36,
            Height = 36,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(18),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(button, tooltip);
        return button;
    }

    private static Button ImportButton(string text)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(new Viewbox
        {
            Width = 14,
            Height = 14,
            Child = new Avalonia.Controls.Shapes.Path
            {
                Data = Geometry.Parse("M12 3v12 M7 10l5 5 5-5 M5 19h14"),
                Stroke = TextBrush,
                StrokeThickness = 1.8,
                StrokeLineCap = PenLineCap.Round,
                StrokeJoin = PenLineJoin.Round,
            }
        });
        content.Children.Add(Label(text, 12, TextBrush, weight: FontWeight.Medium));
        return new Button
        {
            Content = content,
            Height = 36,
            Padding = new Thickness(12, 0),
            Background = Brush("#2A2A2A"),
            BorderBrush = Brush("#3A3A3A"),
            BorderThickness = new Thickness(1),
            Foreground = TextBrush,
            FontWeight = FontWeight.Medium,
            CornerRadius = new CornerRadius(18),
            HorizontalAlignment = HorizontalAlignment.Right
        };
    }

    private static Button FilterButton(string label)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        if (label == "Instant")
            content.Children.Add(new Avalonia.Controls.Shapes.Path { Width = 12, Height = 12, Data = Geometry.Parse("M13 2 4 13h6l-1 9 9-12h-6l1-8z"), Fill = TextBrush });
        else if (label == "Studio")
            content.Children.Add(new Avalonia.Controls.Shapes.Path
            {
                Width = 14,
                Height = 12,
                Data = Geometry.Parse("M3 7h18v13H3z M3 7l2-4 4 2 2-3 4 2 3-2 3 5 M7 10v2 M12 10v2 M17 10v2"),
                Stroke = TextBrush,
                StrokeThickness = 1.5,
                StrokeLineCap = PenLineCap.Round,
            });
        content.Children.Add(Label(label, 12, TextBrush));
        return new Button
        {
            Content = content,
            Height = 33,
            Padding = new Thickness(12, 0),
            Background = Brush("#1A1A1A"),
            BorderBrush = Brush("#2D2D2D"),
            BorderThickness = new Thickness(1),
            Foreground = TextBrush,
            CornerRadius = new CornerRadius(18),
            HorizontalAlignment = HorizontalAlignment.Left
        };
    }

    private static Control EmptyState(string title, string description, string? actionLabel, Action<Button>? onAction = null)
    {
        var stack = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(24) };
        stack.Children.Add(Label(title, 14, TextBrush, weight: FontWeight.Medium));
        stack.Children.Add(Label(description, 12, MutedBrush, wrap: true, align: HorizontalAlignment.Center));
        if (actionLabel is not null)
        {
            var action = ActionButton(actionLabel, primary: false);
            if (onAction is not null)
                action.Click += (_, _) => onAction(action);
            stack.Children.Add(action);
        }
        return new Border
        {
            MinHeight = 250,
            Background = PanelBrush,
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Child = stack
        };
    }

    private static Control IntegrationCard(string name, string description, string actionText)
    {
        var button = ActionButton(actionText, primary: true);
        button.Height = 28;
        button.Padding = new Thickness(11, 0);
        button.FontSize = 12;
        ToolTip.SetTip(button, "Integrations are not connected in this Avalonia build.");
        button.IsEnabled = false;

        var titleRow = new Grid { ColumnDefinitions = new ColumnDefinitions("16,*,Auto"), ColumnSpacing = 8 };
        titleRow.Children.Add(IntegrationIcon(name));
        var nameLabel = Label(name, 13, TextBrush);
        Grid.SetColumn(nameLabel, 1);
        titleRow.Children.Add(nameLabel);
        Grid.SetColumn(button, 2);
        titleRow.Children.Add(button);

        var content = new StackPanel { Spacing = 12, Margin = new Thickness(16) };
        content.Children.Add(titleRow);
        var body = Label(description, 12, MutedBrush, wrap: true);
        body.LineHeight = 16;
        content.Children.Add(body);
        return Card(content);
    }

    private static Control IntegrationIcon(string name)
    {
        if (name == "Google Drive")
        {
            var drawing = new DrawingGroup();
            AddIconGeometry(drawing,
                "M6.6 66.85 10.45 73.5c.8 1.4 1.95 2.5 3.3 3.3l13.75-23.8H0c0 1.55.4 3.1 1.2 4.5z",
                "#0066DA");
            AddIconGeometry(drawing,
                "M43.65 25 29.9 1.2c-1.35.8-2.5 1.9-3.3 3.3l-25.4 44c-.8 1.4-1.2 2.95-1.2 4.5h27.5z",
                "#00AC47");
            AddIconGeometry(drawing,
                "M73.55 76.8c1.35-.8 2.5-1.9 3.3-3.3l1.6-2.75 7.65-13.25c.8-1.4 1.2-2.95 1.2-4.5h-27.502l5.852 11.5z",
                "#EA4335");
            AddIconGeometry(drawing,
                "M43.65 25 57.4 1.2C56.05.4 54.5 0 52.9 0H34.4c-1.6 0-3.15.45-4.5 1.2z",
                "#00832D");
            AddIconGeometry(drawing,
                "M59.8 53H27.5L13.75 76.8c1.35.8 2.9 1.2 4.5 1.2h50.8c1.6 0 3.15-.45 4.5-1.2z",
                "#2684FC");
            AddIconGeometry(drawing,
                "M73.4 26.5 60.7 4.5c-.8-1.4-1.95-2.5-3.3-3.3L43.65 25l16.15 28h27.45c0-1.55-.4-3.1-1.2-4.5z",
                "#FFBA00");
            return new Image
            {
                Source = new DrawingImage { Drawing = drawing },
                Width = 16,
                Height = 16,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        return new Avalonia.Controls.Shapes.Path
        {
            Width = 16,
            Height = 16,
            Data = Geometry.Parse("M12 3c5 0 9 1.34 9 3s-4 3-9 3-9-1.34-9-3 4-3 9-3Z M3 6v6c0 1.66 4.03 3 9 3s9-1.34 9-3V6 M3 12v6c0 1.66 4.03 3 9 3s9-1.34 9-3v-6"),
            Stroke = TextBrush,
            StrokeThickness = 1.5,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private static void AddIconGeometry(DrawingGroup drawing, string path, string color)
    {
        drawing.Children.Add(new GeometryDrawing
        {
            Geometry = Geometry.Parse(path),
            Brush = Brush(color)
        });
    }

    private static Control TemplateRow(string title, string description)
    {
        return SettingRow(title, description, DisabledAction("Use template", "Automation templates need Cap's native automation service."), 68);
    }

    private static Button DisabledAction(string text, string reason)
    {
        var button = ActionButton(text, primary: false);
        button.IsEnabled = false;
        ToolTip.SetTip(button, reason);
        return button;
    }

    private static Button ExternalAction(string text, string url, bool primary)
    {
        var button = ActionButton(text, primary);
        button.Click += (_, _) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception error)
            {
                ToolTip.SetTip(button, error.Message);
            }
        };
        return button;
    }

    private static string? GetComboItemText(object? item) => item switch
    {
        ComboBoxItem comboItem => comboItem.Content?.ToString(),
        null => null,
        _ => item.ToString()
    };

    private static Control ChangelogEntry(string title, string description)
    {
        var content = new StackPanel { Spacing = 4, Margin = new Thickness(16, 12) };
        content.Children.Add(Label(title, 13, TextBrush, weight: FontWeight.Medium));
        content.Children.Add(Label(description, 12, MutedBrush, wrap: true));
        return content;
    }

    private static Control Note(string text) => Label(text, 12, MutedBrush, wrap: true, margin: new Thickness(12, 8));

    private static TextBlock Label(string text, double size, IBrush foreground, VerticalAlignment verticalAlignment = VerticalAlignment.Center, bool wrap = false, HorizontalAlignment align = HorizontalAlignment.Left, Thickness? margin = null, FontWeight? weight = null)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = size,
            Foreground = foreground,
            VerticalAlignment = verticalAlignment,
            HorizontalAlignment = align,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            Margin = margin ?? default,
            FontWeight = weight ?? FontWeight.Medium
        };
    }

    private static Button ActionButton(string text, bool primary)
    {
        return new Button
        {
            Content = text,
            Height = 36,
            Padding = new Thickness(14, 0),
            Background = Brush(primary ? "#0A84FF" : "#2A2A2A"),
            BorderBrush = Brush(primary ? "#1689FF" : "#3A3A3A"),
            BorderThickness = new Thickness(1),
            Foreground = TextBrush,
            FontWeight = FontWeight.Medium,
            CornerRadius = new CornerRadius(18),
            HorizontalAlignment = HorizontalAlignment.Right
        };
    }

    private static Button CreateUnavailablePlansButton()
    {
        var button = ActionButton("View plans ↗", primary: false);
        button.IsEnabled = false;
        button.Margin = new Thickness(12, 0, 0, 12);
        button.Height = 30;
        button.HorizontalAlignment = HorizontalAlignment.Left;
        ToolTip.SetTip(button, "Cap plans are unavailable in this local-only build.");
        return button;
    }

    private static Control Chip(string text) => new Border
    {
        Padding = new Thickness(10, 6),
        Background = Brush("#292929"),
        BorderBrush = Brush("#3B3B3B"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(14),
        Child = Label(text, 12, TextBrush)
    };

    private static Control Separator() => new Border { Height = 1, Background = Brush("#2F2F2F"), Margin = new Thickness(16, 0) };

    private static IEnumerable<Control> WithSeparators(IEnumerable<Control> rows)
    {
        var first = true;
        foreach (var row in rows)
        {
            if (!first)
                yield return Separator();
            first = false;
            yield return row;
        }
    }

    private static IBrush Brush(string value) => new SolidColorBrush(Color.Parse(value));
}
