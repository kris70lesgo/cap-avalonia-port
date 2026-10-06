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
        "Automations" => BuildAutomationsPage(owner),
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
        var shareRows = Card(DisabledToggleRow("Open share links automatically", "Share links are unavailable until uploads are connected."));

        return Page("Recording quality", "Choose how new recordings look and how much space they use.",
            Section("Studio", "Saved to your computer, ready to edit. All three quality options are available on every plan.",
                Card(studioChoices, Note("These options affect the original recording. Choose your final export resolution and file size in the editor."))),
            Section("Instant", "Instant is local-only in this Avalonia build. There is no upload or share-link service.",
                Card(Label("Maximum resolution", 13, TextBrush, margin: new Thickness(12, 12, 12, 2)), instantChoices,
                    Note("Upload resolution choices become available when Cap's online service is connected."))),
            Section("Sharing", "Share links are unavailable while uploads are disconnected.", shareRows));
    }

    private static Control BuildShortcutsPage()
    {
        var shortcuts = new[]
        {
            ("Start or stop recording", "record", "Primary+R"),
            ("Take a screenshot", "screenshot", "Primary+Shift+S"),
            ("Switch to Instant mode", "mode.instant", "Primary+D1"),
            ("Switch to Studio mode", "mode.studio", "Primary+D2"),
            ("Switch to Screenshot mode", "mode.screenshot", "Primary+D3")
        };

        var rows = shortcuts.Select(item => ShortcutRow(item.Item1, item.Item2, item.Item3)).ToArray();
        return Page("Shortcuts", "Change shortcuts for when Cap Avalonia is focused. Select a key, then press the replacement combination.",
            Card(WithSeparators(rows).ToArray()),
            Note("System-wide hotkeys are not enabled in this Avalonia build. Press Escape while recording a shortcut to cancel."));
    }

    private static Control BuildCliPage()
    {
        return Page("Command Line", "Cap's command line installer is not included in this Avalonia build.",
            Section("CLI", null, Card(
                SettingRow("Status", "Not connected", DisabledAction("Install CLI", "The native Cap CLI service is not available."), 68),
                Note("The Cap CLI install path and terminal commands are not configured for this app."))));
    }

    private static Control BuildRecordingsPage(Window owner) => BuildLibraryPage(owner, screenshots: false);

    private static Control BuildScreenshotsPage(Window owner) => BuildLibraryPage(owner, screenshots: true);

    private static Control BuildLibraryPage(Window owner, bool screenshots)
    {
        var title = screenshots ? "Screenshots" : "Recordings";
        var import = ActionButton(screenshots ? "Import image" : "Import", primary: false);
        var header = PageHeader(title, "Manage your " + title.ToLowerInvariant() + " and perform actions.", import);
        var search = (TextBox)SearchBox(screenshots ? "Search screenshots" : "Search recordings");
        var itemsPanel = new StackPanel { Spacing = 8 };
        var currentFilter = "Show all";
        var visibleCount = 20;
        var loadMore = ActionButton("Load more", primary: false);
        loadMore.HorizontalAlignment = HorizontalAlignment.Center;
        loadMore.Click += (_, _) =>
        {
            visibleCount += 20;
            RefreshItems();
        };

        void RefreshItems()
        {
            itemsPanel.Children.Clear();
            var filteredItems = MediaLibraryService.GetItems(screenshots)
                .Where(item => item.Name.Contains(search.Text ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                .Where(item => screenshots || currentFilter == "Show all" || item.Mode == currentFilter)
                .ToArray();

            if (filteredItems.Length == 0)
            {
                loadMore.IsVisible = false;
                var label = currentFilter != "Show all" && !screenshots
                    ? $"No {currentFilter} recordings found in this local library."
                    : !string.IsNullOrWhiteSpace(search.Text)
                        ? "No matching " + title.ToLowerInvariant() + "."
                        : "No " + title.ToLowerInvariant() + " found";
                itemsPanel.Children.Add(Label(label, 12, MutedBrush, align: HorizontalAlignment.Center,
                    margin: new Thickness(12, 34)));
                return;
            }

            foreach (var item in filteredItems.Take(visibleCount))
                itemsPanel.Children.Add(MediaItemCard(item, screenshots, owner, RefreshItems));

            loadMore.IsVisible = filteredItems.Length > visibleCount;
            if (loadMore.IsVisible)
                itemsPanel.Children.Add(loadMore);
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
        var sections = new List<Control>();

        if (!screenshots)
        {
            var filterButtons = new List<Button>();
            var filterRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            foreach (var label in new[] { "Show all", "Instant", "Studio" })
            {
                var button = ActionButton(label, primary: false);
                button.Padding = new Thickness(14, 7);
                button.CornerRadius = new CornerRadius(18);
                button.Click += (_, _) =>
                {
                    currentFilter = label;
                    visibleCount = 20;
                    SelectChoice(filterButtons, button);
                    RefreshItems();
                };
                filterButtons.Add(button);
                filterRow.Children.Add(button);
            }
            SelectChoice(filterButtons, filterButtons[0]);
            sections.Add(filterRow);
        }

        sections.Add(search);
        sections.Add(itemsPanel);
        RefreshItems();
        return PageWithHeader(header, sections.ToArray());
    }

    private static Control MediaItemCard(MediaLibraryItem item, bool screenshot, Window owner, Action refresh)
    {
        var details = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        details.Children.Add(Label(item.Name, 13, TextBrush, wrap: true, weight: FontWeight.Medium));
        details.Children.Add(Label(screenshot
            ? $"{item.Modified:g} · {MediaLibraryService.FormatSize(item.Size)}"
            : $"{item.Mode} · {item.Modified:g} · {MediaLibraryService.FormatSize(item.Size)}", 11, MutedBrush));

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        if (screenshot)
        {
            var annotate = ActionButton("Annotate", primary: false);
            annotate.Click += (_, _) =>
            {
                try { new ScreenshotEditorWindow(item.Path).Show(owner); }
                catch (Exception error) { ToolTip.SetTip(annotate, error.Message); }
            };
            actions.Children.Add(annotate);
            var copyImage = ActionButton("Copy", primary: false);
            ToolTip.SetTip(copyImage, "Copy image to the system clipboard");
            copyImage.Click += async (_, _) =>
            {
                try
                {
                    await MediaLibraryService.CopyToClipboardAsync(owner, item.Path, image: true);
                    copyImage.Content = "Copied";
                }
                catch (Exception error)
                {
                    copyImage.Content = "Copy failed";
                    ToolTip.SetTip(copyImage, error.Message);
                }
            };
            actions.Children.Add(copyImage);
        }
        else
        {
            var edit = ActionButton("Edit", primary: true);
            edit.Click += (_, _) => new VideoEditorWindow(item.Path).Show(owner);
            var play = ActionButton("Play", primary: false);
            play.Click += (_, _) =>
            {
                try { MediaLibraryService.OpenVideoAt(item.Path); }
                catch (Exception error) { ToolTip.SetTip(play, error.Message); }
            };
            actions.Children.Add(edit);
            actions.Children.Add(play);
            var copyVideo = ActionButton("Copy", primary: false);
            ToolTip.SetTip(copyVideo, "Copy video file to the system clipboard");
            copyVideo.Click += async (_, _) =>
            {
                try
                {
                    await MediaLibraryService.CopyToClipboardAsync(owner, item.Path, image: false);
                    copyVideo.Content = "Copied";
                }
                catch (Exception error)
                {
                    copyVideo.Content = "Copy failed";
                    ToolTip.SetTip(copyVideo, error.Message);
                }
            };
            actions.Children.Add(copyVideo);
        }
        var folder = ActionButton("Folder", primary: false);
        folder.Click += (_, _) =>
        {
            try { MediaLibraryService.OpenDirectory(item.Path); }
            catch (Exception error) { ToolTip.SetTip(folder, error.Message); }
        };
        actions.Children.Add(folder);
        var delete = ActionButton("Delete", primary: false);
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
                delete.Content = "Could not move";
                ToolTip.SetTip(delete, error.Message);
            }
        };
        actions.Children.Add(delete);

        var content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("72,*,Auto"),
            ColumnSpacing = 12,
            Margin = new Thickness(12, 10)
        };

        var preview = new Border
        {
            Width = 64,
            Height = 52,
            CornerRadius = new CornerRadius(7),
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
        return new Border
        {
            Background = PanelBrush,
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Child = content
        };
    }

    private static Control BuildAutomationsPage(Window owner)
    {
        var recordingEnabled = new ToggleSwitch
        {
            Width = 38,
            OnContent = string.Empty,
            OffContent = string.Empty,
            IsChecked = AppPreferencesStore.Get("automation.recordings.enabled") == "true"
        };
        var screenshotEnabled = new ToggleSwitch
        {
            Width = 38,
            OnContent = string.Empty,
            OffContent = string.Empty,
            IsChecked = AppPreferencesStore.Get("automation.screenshots.enabled") == "true"
        };
        recordingEnabled.IsCheckedChanged += (_, _) => SaveAutomationToggle("automation.recordings.enabled", recordingEnabled.IsChecked);
        screenshotEnabled.IsCheckedChanged += (_, _) => SaveAutomationToggle("automation.screenshots.enabled", screenshotEnabled.IsChecked);

        var recordingFolder = FolderDisplay(AppPreferencesStore.Get("automation.recordings.folder"));
        var screenshotFolder = FolderDisplay(AppPreferencesStore.Get("automation.screenshots.folder"));
        var recordingChoose = ActionButton("Choose folder", primary: false);
        recordingChoose.Click += async (_, _) => await ChooseAutomationFolderAsync(owner, "Choose a backup folder for recordings", recordingFolder, "automation.recordings.folder");
        var screenshotChoose = ActionButton("Choose folder", primary: false);
        screenshotChoose.Click += async (_, _) => await ChooseAutomationFolderAsync(owner, "Choose a backup folder for screenshots", screenshotFolder, "automation.screenshots.folder");

        var recordingDestination = FolderRow(recordingFolder, recordingChoose);
        var screenshotDestination = FolderRow(screenshotFolder, screenshotChoose);
        return Page("Automations", "Run small local actions automatically when you save new media.",
            Section("Recordings", "Keep a second copy of every finished local recording.",
                Card(SettingRow("Copy recordings to another folder", "Runs after a recording is finalized.", recordingEnabled), recordingDestination)),
            Section("Screenshots", "Keep a second copy of each screenshot you take.",
                Card(SettingRow("Copy screenshots to another folder", "Runs after the screenshot is saved.", screenshotEnabled), screenshotDestination)),
            Note("These automations run only on this computer. Share-link and cloud-drive rules need Cap's online services."));
    }

    private static TextBlock FolderDisplay(string? folder)
    {
        var display = Label(string.IsNullOrWhiteSpace(folder) ? "Choose a destination folder" : folder, 12, MutedBrush, wrap: true);
        display.TextTrimming = TextTrimming.CharacterEllipsis;
        return display;
    }

    private static Control FolderRow(TextBlock display, Button choose)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 10, Margin = new Thickness(12, 4, 12, 10) };
        row.Children.Add(display);
        Grid.SetColumn(choose, 1);
        row.Children.Add(choose);
        return row;
    }

    private static async Task ChooseAutomationFolderAsync(Window owner, string title, TextBlock display, string key)
    {
        try
        {
            var folder = await AutomationService.ChooseFolderAsync(owner, title);
            if (folder is null)
                return;
            AppPreferencesStore.Set(key, folder);
            display.Text = folder;
        }
        catch (Exception error)
        {
            display.Text = "Could not choose folder: " + error.Message;
        }
    }

    private static void SaveAutomationToggle(string key, bool? value)
    {
        try { AppPreferencesStore.Set(key, value == true ? "true" : "false"); }
        catch { }
    }

    private static Control BuildTranscriptionPage()
    {
        var input = new TextBox { PlaceholderText = "Add a name, spelling, domain, or phrase", MinHeight = 34, VerticalContentAlignment = VerticalAlignment.Center };
        var hints = new StackPanel { Spacing = 8 };
        var hintValues = new List<string>();
        try
        {
            hintValues = JsonSerializer.Deserialize<List<string>>(AppPreferencesStore.Get("transcription.hints") ?? "[]") ?? new();
        }
        catch
        {
            hintValues = new();
        }
        void PersistHints()
        {
            try { AppPreferencesStore.Set("transcription.hints", JsonSerializer.Serialize(hintValues)); }
            catch { }
        }

        void RefreshHints()
        {
            hints.Children.Clear();
            if (hintValues.Count == 0)
            {
                hints.Children.Add(Note("No hints added yet."));
                return;
            }
            foreach (var hint in hintValues.ToArray())
            {
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
                row.Children.Add(Chip(hint));
                var remove = ActionButton("Remove", primary: false);
                remove.Click += (_, _) =>
                {
                    hintValues.Remove(hint);
                    PersistHints();
                    RefreshHints();
                };
                Grid.SetColumn(remove, 1);
                row.Children.Add(remove);
                hints.Children.Add(row);
            }
        }

        RefreshHints();
        var add = ActionButton("Add", primary: true);
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

        return Page("Transcription", "Keep a local list of names and spellings for reference.",
            Section("Add a hint", null, Card(entry, Note("Transcription and caption generation are not connected in this build."))),
            Section("Saved hints", "These hints stay on this device.",
                Card(hints)));
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
        var content = new StackPanel { Spacing = 28 };
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

    private static Control PageHeader(string title, string description, Control? action = null)
    {
        var text = new StackPanel { Spacing = 4 };
        text.Children.Add(Label(title, 14, TextBrush, weight: FontWeight.SemiBold));
        text.Children.Add(Label(description, 12, MutedBrush, wrap: true));

        if (action is null)
            return text;

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        header.Children.Add(text);
        Grid.SetColumn(action, 1);
        header.Children.Add(action);
        return header;
    }

    private static Control Section(string title, string? description, Control content)
    {
        var section = new StackPanel { Spacing = 4 };
        section.Children.Add(Label(title, 14, TextBrush, weight: FontWeight.SemiBold));
        if (!string.IsNullOrWhiteSpace(description))
            section.Children.Add(Label(description, 12, MutedBrush, wrap: true));
        if (content is Border border)
            border.Margin = new Thickness(-4, 8, -4, 0);
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
            Background = PanelBrush,
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
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*"), ColumnSpacing = 8, Margin = new Thickness(12, 2, 12, 10) };
        var labels = new[] { "720p", "1080p\nPro", "1440p\nPro", "4K\nPro" };
        for (var index = 0; index < labels.Length; index++)
        {
            var button = ActionButton(labels[index], primary: index == 0);
            button.Height = 52;
            button.IsEnabled = false;
            ToolTip.SetTip(button, "Instant upload resolution is unavailable in this local-only build.");
            Grid.SetColumn(button, index);
            grid.Children.Add(button);
        }
        return grid;
    }

    private static Control ChoiceList(string preferenceKey, params (string Label, string Badge, string Description)[] choices)
    {
        var stack = new StackPanel { Spacing = 8, Margin = new Thickness(16) };
        var buttons = new List<Button>();
        var selectedLabel = AppPreferencesStore.Get(preferenceKey) ?? choices.FirstOrDefault().Label;
        Button? selectedButton = null;
        foreach (var choice in choices)
        {
            var content = new StackPanel { Spacing = 4 };
            var heading = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            heading.Children.Add(Label(choice.Label, 13, TextBrush, weight: FontWeight.Medium));
            if (!string.IsNullOrWhiteSpace(choice.Badge))
                heading.Children.Add(new Border { Padding = new Thickness(7, 3), Background = Brush("#103257"), CornerRadius = new CornerRadius(8), Child = Label(choice.Badge, 11, Brush("#77B9FF")) });
            content.Children.Add(heading);
            content.Children.Add(Label(choice.Description, 12, MutedBrush, wrap: true));
            var button = new Button
            {
                Content = content,
                Padding = new Thickness(14, 12),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Background = PanelBrush,
                BorderBrush = PanelBorderBrush,
                BorderThickness = new Thickness(1),
                Foreground = TextBrush,
                CornerRadius = new CornerRadius(10),
                MinHeight = 66
            };
            buttons.Add(button);
            stack.Children.Add(button);
            if (choice.Label == selectedLabel)
                selectedButton = button;
            button.Click += (_, _) =>
            {
                SelectChoice(buttons, button);
                try { AppPreferencesStore.Set(preferenceKey, choice.Label); }
                catch { }
            };
        }
        SelectChoice(buttons, selectedButton ?? buttons[0]);
        return stack;
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

    private static Control ShortcutRow(string name, string key, string defaultValue)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), MinHeight = 56, Margin = new Thickness(16, 4) };
        row.Children.Add(Label(name, 13, TextBrush, wrap: true));
        var value = AppPreferencesStore.Get("shortcut." + key) ?? defaultValue;
        var button = ActionButton(FormatShortcut(value), primary: false);
        button.MinWidth = 52;
        button.Height = 28;
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

    private static Control SearchBox(string watermark) => new TextBox
    {
        PlaceholderText = watermark,
        MinHeight = 36,
        Width = 260,
        Background = Brush("#222222"),
        BorderBrush = PanelBorderBrush,
        Foreground = TextBrush,
        CornerRadius = new CornerRadius(9),
        Margin = new Thickness(0, 4)
    };

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
