using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace CapAvalonia;

internal static class SettingsPageFactory
{
    private static readonly IBrush PanelBrush = Brush("#1A1A1A");
    private static readonly IBrush CardBrush = Brush("#1C1C1C");
    private static readonly IBrush PanelBorderBrush = Brush("#2D2D2D");
    private static readonly IBrush TextBrush = Brush("#F1F1F1");
    private static readonly IBrush MutedBrush = Brush("#A1A1A1");

    public static Control Build(string section, Window owner) => section switch
    {
        "Recording quality" => BuildQualityPage(),
        "Shortcuts" => BuildShortcutsPage(),
        "CLI" => BuildCliPage(),
        "Recordings" => BuildRecordingsPage(owner),
        "Screenshots" => BuildScreenshotsPage(owner),
        "Automations" => BuildAutomationsPage(),
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
        var shareRows = Card(DisabledToggleRow("Open share links automatically", "Open the link in your browser when an upload finishes."));
        var studioSection = Section("Studio", "Saved to your computer, ready to edit. All three quality options are available on every plan.",
            Card(studioChoices,
                Label("These options affect the original recording. Choose your final export resolution and file size in the editor.", 12, MutedBrush, wrap: true, margin: new Thickness(12, 8, 12, 19)),
                new Border { Height = 2.5 }), contentTopMargin: 9.5);
        studioSection.Margin = new Thickness(0, 0, 0, -3);

        return Page("Recording quality", "Choose how new recordings look and how much space they use.",
            studioSection,
            Section("Instant", "Uploads while you record, so your share link is ready when you stop.",
                Card(Label("Maximum resolution", 13, TextBrush, weight: FontWeight.Medium, margin: new Thickness(16, 16, 16, 14.5)), instantChoices,
                    Label("Smaller uploads. Good for quick updates. Resolution is limited by the screen or area you record.", 12, MutedBrush, wrap: true, margin: new Thickness(12, 10)),
                    new Border { Height = 1, Background = Brush("#303030"), Margin = new Thickness(16, 16, 16, 0) },
                    Label("720p is included. Cap Pro unlocks 1080p, 1440p and 4K for Instant recordings.", 12, MutedBrush, wrap: true, margin: new Thickness(12, 13.5, 12, 8)),
                    CreateUnavailablePlansButton()), contentTopMargin: 8.5),
            Section("Sharing", null, shareRows));
    }

    private static Control BuildShortcutsPage()
    {
        var shortcuts = new[]
        {
            ("Screenshot current display", "screenshot.display"),
            ("Screenshot current window", "screenshot.window"),
            ("Screenshot area picker", "screenshot.area"),
            ("Open recording picker", "recording.picker"),
            ("Stop recording", "recording.stop"),
            ("Restart recording", "recording.restart"),
            ("Pause/resume recording", "recording.pause"),
            ("Cycle recording mode", "recording.cycle"),
            ("Record display", "recording.display"),
            ("Record window", "recording.window"),
            ("Record area", "recording.area")
        };

        var rows = shortcuts.Select(item => ShortcutRow(item.Item1, item.Item2, "NONE")).ToArray();
        var card = Card(WithSeparators(rows).ToArray());
        card.Margin = new Thickness(-4, -3.5, -4, 0);
        if (rows[0] is Grid firstRow)
            firstRow.Margin = new Thickness(16, 7.5, 16, 4);
        return PageWithHeader(PageHeader("Shortcuts", "Configure system-wide keyboard shortcuts to control Cap."), card);
    }

    private static Control BuildCliPage()
    {
        var installDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin");
        var commandPath = Path.Combine(installDirectory, "cap");
        var targetPath = Path.Combine(AppContext.BaseDirectory, "cap-cli");
        var status = Label("Not installed", 13, TextBrush);
        var description = Label("The desktop app installs a local cap command that points back to the bundled CLI.", 12, MutedBrush, wrap: true);
        description.MaxWidth = 390;
        description.LineHeight = 16;
        var install = ActionButton("Install CLI", primary: true);
        install.FontSize = 12;
        install.Height = 28;
        install.Padding = new Thickness(12, 0);
        install.CornerRadius = new CornerRadius(15);
        install.VerticalAlignment = VerticalAlignment.Top;
        install.Click += (_, _) => ToolTip.SetTip(install, "CLI installation is not available in this Avalonia build.");

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

        var emptyState = Label(screenshots ? "No screenshots found" : "No recordings found", 14, TextBrush,
            align: HorizontalAlignment.Center, margin: new Thickness(0, 0, 0, 0));
        var libraryView = new StackPanel { Spacing = 0 };
        libraryView.Children.Add(controls);
        libraryView.Children.Add(itemsCard);
        var page = new StackPanel { Spacing = 0 };
        page.Children.Add(header);
        page.Children.Add(emptyState);
        page.Children.Add(libraryView);

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
            emptyState.IsVisible = sourceItems.Length == 0;
            libraryView.IsVisible = sourceItems.Length > 0;

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
                itemsPanel.Children.Add(Label(label, 12, MutedBrush, align: HorizontalAlignment.Center,
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
        emptyState.Margin = new Thickness(0, 12, 0, 0);
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
            Background = Brush("#292929"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new Avalonia.Controls.Shapes.Path
            {
                Width = 20,
                Height = 20,
                Data = Geometry.Parse("M13 2 4 13h6l-1 9 9-12h-6l1-8z"),
                Stroke = MutedBrush,
                StrokeThickness = 1.8,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        emptyContent.Children.Add(iconCircle);
        emptyContent.Children.Add(Label("No automations yet", 13, TextBrush, weight: FontWeight.Medium,
            align: HorizontalAlignment.Center));
        var emptyDescription = Label("Pick a template below to get started in one click, or build your own from scratch.",
            12, MutedBrush, wrap: true, align: HorizontalAlignment.Center);
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
            StrokeThickness = 2
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
            ("Auto-copy new screenshots to clipboard", "Snap a screenshot and it's right there, ready to paste.", "M8 8h12v12H8z M4 16H3V4h12v1"),
            ("Pull the text out of screenshots", "Cap reads the text in your screenshot and copies it for you.", "M4 5h16 M4 9h10 M4 13h16 M4 17h10 M18 11v8 M15 14h6"),
            ("Tuck screenshots into a folder", "Send every new screenshot straight to a folder you pick.", "M3 6h7l2 2h9v12H3z M12 11v6 M9 14l3 3 3-3"),
            ("Jump to each new screenshot", "Pop open every screenshot in Finder the moment you take it.", "M3 6h7l2 2h9v12H3z M8 14h8 M12 10v8"),
            ("Auto-export when you finish recording", "Render an MP4 the second a Studio recording wraps up.", "M4 5h16v14H4z M9 5v14 M15 5v14 M4 10h5 M15 10h5 M4 15h5 M15 15h5"),
            ("Upload and grab the share link", "Finish a recording and the link is waiting on your clipboard.", "M10 13a5 5 0 0 0 7.5.5l3-3a5 5 0 0 0-7.1-7.1l-1.7 1.7 M14 11a5 5 0 0 0-7.5-.5l-3 3a5 5 0 0 0 7.1 7.1l1.7-1.7"),
            ("Ping me when an upload is ready", "Get a gentle desktop nudge once your recording is shareable.", "M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9 M10 21h4"),
            ("Tell Slack when you share something", "Send the share link to Slack, Discord, or your own webhook.", "M12 8V4m0 16v-4m4-4h4M4 12h4m5.2-1.2 2.8-2.8m-5.6 5.6-2.8 2.8m5.6 0-2.8-2.8m-2.8-5.6 2.8 2.8")
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
            descriptionMaxWidth: 1032));
        page.Children.Add(emptyCard);
        var templatesSection = Section("Templates", "One click to add a ready-made automation. Tweak anything afterwards.", templateGrid);
        templateGrid.Margin = new Thickness(-4, 8, -9, 0);
        templatesSection.Margin = new Thickness(0, 30.5, 0, 0);
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
        var google = IntegrationCard("Google Drive", "Automatically save finished recordings to Google Drive.", "Connect Google Drive");
        var s3 = IntegrationCard("Custom S3 storage", "Store uploads in an S3-compatible bucket you control.", "Configure storage");
        return Page("Integrations", "Third-party connections are not available in this local Avalonia build.",
            google, s3,
            Note("No credentials or files are sent to these services from this app."));
    }

    private static Control BuildLicensePage()
    {
        var key = new TextBox { PlaceholderText = "Enter your license key", MinHeight = 34, VerticalContentAlignment = VerticalAlignment.Center };
        key.IsEnabled = false;
        var activate = ActionButton("Activate", primary: true);
        activate.IsEnabled = false;
        var keyRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        keyRow.Children.Add(key);
        Grid.SetColumn(activate, 1);
        keyRow.Children.Add(activate);

        return Page("Plan & license", "Cap account, plan, and license services are not connected in this Avalonia build.",
            Section("Your plan", null, Card(
                SettingRow("Local workspace", "Record and edit media saved on this device.", ExternalAction("View Cap plans", "https://cap.so/pricing", primary: true), 72),
                Separator(),
                SettingRow("Desktop license", "License activation needs Cap's account service.", null, 68),
                keyRow,
                Note("License activation is not connected to Cap's account service in this build."))),
            Section("Explore plans", "Open Cap's website to view its current plans and features.",
                Card(ExternalAction("Explore plans", "https://cap.so/pricing", primary: false))));
    }

    private static Control BuildExperimentalPage()
    {
        return Page("Experimental", "Preview features from Cap's native desktop build.",
            Section("Preview", null, Card(DisabledToggleRow("Native camera preview", "Native GPU preview is not available in the Avalonia port."))),
            Section("Reliability", null, Card(DisabledToggleRow("Out-of-process muxer", "The isolated muxer service is not part of this Avalonia build."))),
            Section("Native app", null, Card(DisabledToggleRow("Cap GPUI", "Cap's native renderer is not part of this Avalonia build."))),
            Note("These preview switches are unavailable in the Avalonia port."));
    }

    private static Control BuildFeedbackPage()
    {
        var category = Select(new[] { "General feedback", "Report a bug", "Request a feature" }, 0);
        var message = new TextBox { PlaceholderText = "Tell us what happened or what you would like to see…", AcceptsReturn = true, MinHeight = 110, TextWrapping = TextWrapping.Wrap, VerticalContentAlignment = VerticalAlignment.Top };
        var submit = ActionButton("Save feedback locally", primary: true);
        submit.IsEnabled = false;
        message.TextChanged += (_, _) => submit.IsEnabled = !string.IsNullOrWhiteSpace(message.Text);
        submit.Click += (_, _) =>
        {
            try
            {
                var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CapAvalonia", "Feedback");
                Directory.CreateDirectory(directory);
                var categoryText = GetComboItemText(category.SelectedItem) ?? "General feedback";
                var file = Path.Combine(directory, $"Feedback_{DateTime.Now:yyyyMMdd_HHmmss_fff}.txt");
                File.WriteAllText(file, $"{categoryText}{Environment.NewLine}{DateTime.Now:g}{Environment.NewLine}{Environment.NewLine}{message.Text}");
                submit.Content = "Saved locally";
                submit.IsEnabled = false;
            }
            catch (Exception error)
            {
                submit.Content = "Save failed";
                ToolTip.SetTip(submit, error.Message);
            }
        };

        return Page("Feedback", "Write feedback and save a copy on this device.",
            Section("Save feedback", null, Card(category, message, submit, Note("Feedback is saved locally; it is not sent to Cap."))),
            Section("Join the Community", "Have questions, want to share ideas, or just hang out? Join the Cap Discord community.",
                Card(ExternalAction("Open Cap Discord", "https://cap.link/discord", primary: false))),
            Section("Diagnostic Report", "Diagnostic upload is unavailable in this Avalonia build.",
                Card(DisabledToggleRow("Test microphone too", "Microphone diagnostics are not connected."),
                    DisabledAction("Prepare diagnostic report", "The diagnostic reporting service is not connected."))));
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

    private static Control PageHeader(string title, string description, Control? action = null, double descriptionMaxWidth = 510)
    {
        var text = new StackPanel { Spacing = 4 };
        text.Children.Add(Label(title, 14, TextBrush, weight: FontWeight.SemiBold));
        var descriptionLabel = Label(description, 12, MutedBrush, wrap: true);
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

    private static Control Section(string title, string? description, Control content, double contentTopMargin = 6.5)
    {
        var section = new StackPanel { Spacing = 4 };
        section.Children.Add(Label(title, 14, TextBrush, weight: FontWeight.SemiBold));
        if (!string.IsNullOrWhiteSpace(description))
        {
            var descriptionLabel = Label(description, 12, MutedBrush, wrap: true);
            descriptionLabel.MaxWidth = 510;
            descriptionLabel.LineHeight = 16.5;
            section.Children.Add(descriptionLabel);
        }
        if (content is Border border)
            border.Margin = new Thickness(-4, contentTopMargin, -4, 0);
        else
            content.Margin = new Thickness(0, 8, 0, 0);
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
            SelectedIndex = selected,
            Background = Brush("#282828"),
            BorderBrush = Brush("#383838"),
            Foreground = TextBrush,
            CornerRadius = new CornerRadius(9)
        };
        foreach (var value in values)
            combo.Items.Add(new ComboBoxItem { Content = value });
        return combo;
    }

    private static Control ResolutionChoices()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*"), ColumnSpacing = 8, Margin = new Thickness(16, 2, 16, 4) };
        var labels = new[] { ("720p", false), ("1080p", true), ("1440p", true), ("4K", true) };
        for (var index = 0; index < labels.Length; index++)
        {
            var tile = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            tile.Children.Add(Label(labels[index].Item1, 12, index == 0 ? TextBrush : MutedBrush, align: HorizontalAlignment.Center));
            if (labels[index].Item2)
                tile.Children.Add(Label("Pro", 10, MutedBrush, align: HorizontalAlignment.Center));
            var option = new Border
            {
                Height = 52,
                Background = Brush(index == 0 ? "#102F51" : "#1A1A1A"),
                BorderBrush = Brush(index == 0 ? "#1684FF" : "#303030"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Child = tile
            };
            ToolTip.SetTip(option, "Instant resolution selection is unavailable in this local-only build.");
            Grid.SetColumn(option, index);
            grid.Children.Add(option);
        }
        return grid;
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
                Padding = new Thickness(12, 13),
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
            button.Background = Brush(active ? "#102F51" : "#1A1A1A");
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
        };
        Grid.SetColumn(button, 1);
        row.Children.Add(button);
        return row;
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
            Background = Brush("#252525"),
            Child = new Avalonia.Controls.Shapes.Path
            {
                Width = 18,
                Height = 18,
                Data = Geometry.Parse(iconPath),
                Stroke = MutedBrush,
                StrokeThickness = 1.8,
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
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(new Avalonia.Controls.Shapes.Path
        {
            Width = 16,
            Height = 16,
            Data = Geometry.Parse("M12 3v10 M8 10l4 4 4-4 M5 14v5h14v-5"),
            Stroke = TextBrush,
            StrokeThickness = 1.8,
            StrokeLineCap = PenLineCap.Round,
        });
        content.Children.Add(Label(text, 12, TextBrush));
        return new Button
        {
            Content = content,
            Height = 36,
            Padding = new Thickness(10.25, 0),
            Background = Brush("#2A2A2A"),
            BorderBrush = Brush("#3A3A3A"),
            BorderThickness = new Thickness(1),
            Foreground = TextBrush,
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
        var button = ActionButton(actionText, primary: false);
        button.IsEnabled = false;
        return Card(SettingRow(name, description, button, 78), Separator(), Note("Not connected"));
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
            FontWeight = weight ?? FontWeight.Normal
        };
    }

    private static Button ActionButton(string text, bool primary)
    {
        return new Button
        {
            Content = text,
            Height = 36,
            Padding = new Thickness(14, 0),
            Background = Brush(primary ? "#1677ED" : "#2A2A2A"),
            BorderBrush = Brush(primary ? "#2487FF" : "#3A3A3A"),
            BorderThickness = new Thickness(1),
            Foreground = TextBrush,
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

    private static Control Separator() => new Border { Height = 1, Background = Brush("#2F2F2F"), Margin = new Thickness(0) };

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
