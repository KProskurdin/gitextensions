using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using GitCommands;
using GitCommands.Logging;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormGitCommandLog</c>: the git commands the app ran (upstream's <see cref="CommandLog"/>, which the
///  shared engine fills for every git call), oldest first with the newest followed, and upstream's cache of git output
///  (<see cref="GitModule.GitCommandCache"/>), each with the selected entry's detail; one window at a time.
/// </summary>
public partial class CommandLogWindow : Window
{
    private const string FormName = "FormGitCommandLog";

    private static CommandLogWindow? _instance;
    private bool _logRefreshQueued;
    private bool _cacheRefreshQueued;
    private IReadOnlyList<CommandLogEntry> _entries = [];
    private IReadOnlyList<string> _cachedCommands = [];

    public CommandLogWindow()
    {
        InitializeComponent();
        UpstreamTranslation.Apply(this, FormName);
        WindowPlacementTracker.Attach(this, "Xplat.CommandLogWindow");
        InitialFocus.Attach(this, () => LogItems);

        chkCaptureCallStacks.IsChecked = AppSettings.LogCaptureCallStacks;
        chkCaptureCallStacks.IsCheckedChanged +=
            (_, _) => AppSettings.LogCaptureCallStacks = chkCaptureCallStacks.IsChecked == true;
        chkWordWrap.IsCheckedChanged += (_, _) => ApplyWordWrap();
        chkAlwaysOnTop.IsCheckedChanged += (_, _) => Topmost = chkAlwaysOnTop.IsChecked == true;
        ApplyWordWrap();

        LogItems.SelectionChanged += (_, _) => LogOutput.Text = SelectedEntry()?.Detail ?? "";
        CommandCacheItems.SelectionChanged += (_, _) => ShowCachedCommand();
        TabControl.SelectionChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Source, TabControl))
            {
                RefreshLogItems();
                RefreshCommandCacheItems();
            }
        };

        mnuSaveToFile.Click += (_, _) => UiActions.Run(SaveToFileAsync, ShowError);
        mnuCopyCommandLine.Click += (_, _) => UiActions.Run(CopyCommandLineAsync, ShowError);
        mnuClear.Click += (_, _) => CommandLog.Clear();
        tsmiClearCache.Click += (_, _) => ClearCache();
        tabPageCommandLog.KeyDown += OnCommandLogKeyDown;
        tabPageCommandCache.KeyDown += OnCommandCacheKeyDown;

        CommandLog.CommandsChanged += OnCommandsChanged;
        GitModule.GitCommandCache.Changed += OnCachedCommandsChanged;
        Closed += (_, _) =>
        {
            CommandLog.CommandsChanged -= OnCommandsChanged;
            GitModule.GitCommandCache.Changed -= OnCachedCommandsChanged;
        };
        Opened += (_, _) =>
        {
            RefreshLogItems();
            RefreshCommandCacheItems();
        };
    }

    /// <summary>
    ///  The commands on show, oldest first.
    /// </summary>
    public IReadOnlyList<CommandLogEntry> Entries => _entries;

    /// <summary>
    ///  The cached commands on show.
    /// </summary>
    public IReadOnlyList<string> CachedCommands => _cachedCommands;

    /// <summary>
    ///  Upstream's <c>ShowOrActivate</c>: shows the log, or restores or activates the open window.
    /// </summary>
    public static void ShowFor(Window owner)
    {
        if (_instance is { } open)
        {
            if (open.WindowState == WindowState.Minimized)
            {
                open.WindowState = WindowState.Normal;
            }
            else
            {
                open.Activate();
            }

            return;
        }

        _instance = new CommandLogWindow();
        _instance.Closed += (_, _) => _instance = null;
        _instance.Show(owner);
    }

    private void ApplyWordWrap()
    {
        TextWrapping wrapping = chkWordWrap.IsChecked == true ? TextWrapping.Wrap : TextWrapping.NoWrap;
        LogOutput.TextWrapping = wrapping;
        commandCacheOutput.TextWrapping = wrapping;
    }

    // The log changes on whichever thread ran git; one refresh per burst is posted to the UI thread.
    private void OnCommandsChanged()
    {
        if (_logRefreshQueued)
        {
            return;
        }

        _logRefreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _logRefreshQueued = false;
            RefreshLogItems();
        }, DispatcherPriority.Background);
    }

    private void OnCachedCommandsChanged(object? sender, EventArgs e)
    {
        if (_cacheRefreshQueued)
        {
            return;
        }

        _cacheRefreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _cacheRefreshQueued = false;
            RefreshCommandCacheItems();
        }, DispatcherPriority.Background);
    }

    // Upstream's RefreshLogItems and RefreshCommandCacheItems: only the tab on show is refilled.
    private void RefreshLogItems()
    {
        if (TabControl.SelectedItem == tabPageCommandLog)
        {
            int oldCount = _entries.Count;
            _entries = [.. CommandLog.Commands];
            Refill(LogItems, oldCount, [.. _entries.Select(entry => entry.ColumnLine)]);
        }
    }

    private void RefreshCommandCacheItems()
    {
        if (TabControl.SelectedItem == tabPageCommandCache)
        {
            int oldCount = _cachedCommands.Count;
            _cachedCommands = GitModule.GitCommandCache.GetCachedCommands();
            Refill(CommandCacheItems, oldCount, [.. _cachedCommands.Select(CommandLogDialog.CacheDisplay)]);
        }
    }

    private static void Refill(ListBox list, int oldCount, IReadOnlyList<string> rows)
    {
        int selected = CommandLogDialog.SelectionAfterRefresh(oldCount, list.SelectedIndex, rows.Count);
        list.ItemsSource = rows;
        list.SelectedIndex = selected;
        if (selected >= 0)
        {
            list.ScrollIntoView(selected);
        }
    }

    private CommandLogEntry? SelectedEntry()
        => LogItems.SelectedIndex is var index and >= 0 && index < _entries.Count ? _entries[index] : null;

    private void ShowCachedCommand()
    {
        int index = CommandCacheItems.SelectedIndex;
        string? command = index >= 0 && index < _cachedCommands.Count ? _cachedCommands[index] : null;
        commandCacheOutput.Text = GitModule.GitCommandCache.TryGet(command, out string? output, out string? error)
            ? CommandLogDialog.CacheDetail(command!, output, error)
            : "";
    }

    private void ClearCache()
    {
        GitModule.GitCommandCache.Clear();
        RefreshCommandCacheItems();
    }

    // Upstream's menu shortcuts, which WinForms runs while the tab page has the focus. The detail box keeps its own copy.
    private void OnCommandLogKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != Hotkeys.CommandModifier)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.S:
                UiActions.Run(SaveToFileAsync, ShowError);
                break;
            case Key.C:
                UiActions.Run(CopyCommandLineAsync, ShowError);
                break;
            case Key.L:
                CommandLog.Clear();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void OnCommandCacheKeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.Handled && e.KeyModifiers == Hotkeys.CommandModifier && e.Key == Key.L)
        {
            ClearCache();
            e.Handled = true;
        }
    }

    // Upstream's mnuSaveToFile_Click.
    private async Task SaveToFileAsync()
    {
        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Title,
            DefaultExtension = CommandLogDialog.TextExtension,
            FileTypeChoices =
            [
                new FilePickerFileType(CommandLogDialog.TextFiles) { Patterns = ["*.txt"] },
                new FilePickerFileType(CommandLogDialog.CsvFiles) { Patterns = ["*.csv"] },
                new FilePickerFileType(CommandLogDialog.AllFiles) { Patterns = ["*.*"] },
            ],
        });
        if (file?.TryGetLocalPath() is { } path)
        {
            await File.WriteAllLinesAsync(path, CommandLogDialog.SaveLines(path, CommandLog.Commands.ToList()));
        }
    }

    private async Task CopyCommandLineAsync()
    {
        if (SelectedEntry() is { } entry && GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(entry.CommandLine);
        }
    }

    private void ShowError(Exception ex) => _ = new ErrorWindow(ex.Message).ShowDialog(this);
}
