using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands;
using GitCommands.Logging;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  The view model of upstream's <c>FormGitCommandLog</c>: the git commands the app ran (upstream's
///  <see cref="CommandLog"/>, which the shared engine fills for every git call), oldest first with the newest followed,
///  and upstream's cache of git output, each with the selected entry's detail.
/// </summary>
public sealed partial class CommandLogViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormGitCommandLog";

    /// <summary>
    ///  The command log's tab.
    /// </summary>
    public const int LogTab = 0;

    /// <summary>
    ///  The command cache's tab.
    /// </summary>
    public const int CacheTab = 1;

    private readonly CommandCache _cache;
    private readonly Action<bool> _storeCaptureCallStacks;
    private readonly Action<Action> _post;
    private bool _logRefreshQueued;
    private bool _cacheRefreshQueued;

    /// <param name="cache">Upstream's cache of git output.</param>
    /// <param name="captureCallStacks">Upstream's <c>LogCaptureCallStacks</c>.</param>
    /// <param name="storeCaptureCallStacks">Stores upstream's <c>LogCaptureCallStacks</c>.</param>
    /// <param name="post">Runs an action later on the UI thread, as the log changes on whichever thread ran git.</param>
    public CommandLogViewModel(IDialogHost host, CommandCache cache, bool captureCallStacks,
        Action<bool> storeCaptureCallStacks, Action<Action> post)
        : base(host)
    {
        _cache = cache;
        _storeCaptureCallStacks = storeCaptureCallStacks;
        _post = post;
        CaptureCallStacks = captureCallStacks;
    }

    [ObservableProperty]

    public partial bool AlwaysOnTop { get; set; }

    [ObservableProperty]

    public partial bool WordWrap { get; set; } = true;

    [ObservableProperty]

    public partial bool CaptureCallStacks { get; set; }

    /// <summary>
    ///  The tab on show: <see cref="LogTab"/> or <see cref="CacheTab"/>.
    /// </summary>
    [ObservableProperty]
    public partial int SelectedTab { get; set; }

    /// <summary>
    ///  The commands on show, oldest first.
    /// </summary>
    public IReadOnlyList<CommandLogEntry> Entries { get; private set; } = [];

    [ObservableProperty]

    public partial IReadOnlyList<string> LogRows { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LogDetail))]
    public partial int SelectedLogIndex { get; set; } = -1;

    public string LogDetail => SelectedEntry?.Detail ?? "";

    /// <summary>
    ///  The cached commands on show.
    /// </summary>
    public IReadOnlyList<string> CachedCommands { get; private set; } = [];

    [ObservableProperty]

    public partial IReadOnlyList<string> CacheRows { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CacheDetail))]
    public partial int SelectedCacheIndex { get; set; } = -1;

    public string CacheDetail
    {
        get
        {
            int index = SelectedCacheIndex;
            string? command = index >= 0 && index < CachedCommands.Count ? CachedCommands[index] : null;
            return command is not null && _cache.TryGet(command, out string? output, out string? error)
                ? CommandLogDialog.CacheDetail(command, output, error)
                : "";
        }
    }

    /// <summary>
    ///  Picks the file to save the log to; the window sets it, as the picker is the platform's.
    /// </summary>
    public Func<Task<string?>>? PickSaveFile { get; set; }

    /// <summary>
    ///  Copies a text to the clipboard; the window sets it.
    /// </summary>
    public Func<string, Task>? CopyText { get; set; }

    private CommandLogEntry? SelectedEntry
        => SelectedLogIndex is var index and >= 0 && index < Entries.Count ? Entries[index] : null;

    /// <summary>
    ///  Follows the log and the cache; the window calls it when it opens.
    /// </summary>
    public void Attach()
    {
        CommandLog.CommandsChanged += OnCommandsChanged;
        _cache.Changed += OnCachedCommandsChanged;
        Refresh();
    }

    /// <summary>
    ///  Stops following the log and the cache; the window calls it when it closes.
    /// </summary>
    public void Detach()
    {
        CommandLog.CommandsChanged -= OnCommandsChanged;
        _cache.Changed -= OnCachedCommandsChanged;
    }

    partial void OnCaptureCallStacksChanged(bool value) => _storeCaptureCallStacks(value);

    partial void OnSelectedTabChanged(int value) => Refresh();

    private void Refresh()
    {
        RefreshLogItems();
        RefreshCommandCacheItems();
    }

    // One refresh per burst is posted to the UI thread.
    private void OnCommandsChanged()
    {
        if (_logRefreshQueued)
        {
            return;
        }

        _logRefreshQueued = true;
        _post(() =>
        {
            _logRefreshQueued = false;
            RefreshLogItems();
        });
    }

    private void OnCachedCommandsChanged(object? sender, EventArgs e)
    {
        if (_cacheRefreshQueued)
        {
            return;
        }

        _cacheRefreshQueued = true;
        _post(() =>
        {
            _cacheRefreshQueued = false;
            RefreshCommandCacheItems();
        });
    }

    // Upstream's RefreshLogItems and RefreshCommandCacheItems: only the tab on show is refilled, and the newest entry stays
    // selected.
    private void RefreshLogItems()
    {
        if (SelectedTab == LogTab)
        {
            int oldCount = Entries.Count;
            int oldSelected = SelectedLogIndex;
            Entries = [.. CommandLog.Commands];
            LogRows = [.. Entries.Select(entry => entry.ColumnLine)];
            SelectedLogIndex = CommandLogDialog.SelectionAfterRefresh(oldCount, oldSelected, Entries.Count);
            OnPropertyChanged(nameof(LogDetail));
        }
    }

    private void RefreshCommandCacheItems()
    {
        if (SelectedTab == CacheTab)
        {
            int oldCount = CachedCommands.Count;
            int oldSelected = SelectedCacheIndex;
            CachedCommands = _cache.GetCachedCommands();
            CacheRows = [.. CachedCommands.Select(CommandLogDialog.CacheDisplay)];
            SelectedCacheIndex = CommandLogDialog.SelectionAfterRefresh(oldCount, oldSelected, CachedCommands.Count);
            OnPropertyChanged(nameof(CacheDetail));
        }
    }

    // Upstream's mnuSaveToFile_Click.
    [RelayCommand]
    private Task SaveToFileAsync() => GuardAsync(async () =>
    {
        if (PickSaveFile is not null && await PickSaveFile() is { } path)
        {
            await File.WriteAllLinesAsync(path, CommandLogDialog.SaveLines(path, CommandLog.Commands.ToList()));
        }
    });

    [RelayCommand]
    private Task CopyCommandLineAsync() => GuardAsync(async () =>
    {
        if (SelectedEntry is { } entry && CopyText is not null)
        {
            await CopyText(entry.CommandLine);
        }
    });

    [RelayCommand]
    private static void Clear() => CommandLog.Clear();

    [RelayCommand]
    private void ClearCache()
    {
        _cache.Clear();
        RefreshCommandCacheItems();
    }
}
