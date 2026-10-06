using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using GitCommands.Logging;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The git commands the app ran, newest first, with the detail of the selected one: the new shell's version of upstream
///  <c>FormGitCommandLog</c>. It reads upstream's <see cref="CommandLog"/>, which the shared engine fills for every git call.
/// </summary>
public partial class CommandLogWindow : Window
{
    private static CommandLogWindow? _instance;
    private bool _refreshQueued;
    private IReadOnlyList<CommandLogEntry> _entries = [];

    public CommandLogWindow()
    {
        InitializeComponent();
        WindowPlacementTracker.Attach(this, "Xplat.CommandLogWindow");
        EntryList.SelectionChanged += (_, _) => ShowDetail();
        ClearButton.Click += (_, _) => CommandLog.Clear();
        CopyButton.Click += (_, _) => UiActions.Run(CopyCommandLineAsync, _ => { });
        CommandLog.CommandsChanged += OnCommandsChanged;
        Closed += (_, _) => CommandLog.CommandsChanged -= OnCommandsChanged;
        ShowEntries();
    }

    /// <summary>
    ///  The entries on show, newest first.
    /// </summary>
    public IReadOnlyList<CommandLogEntry> Entries => _entries;

    /// <summary>
    ///  Shows the log, reusing the open window if there is one.
    /// </summary>
    public static void ShowFor(Window owner)
    {
        if (_instance is { } open)
        {
            open.Activate();
            return;
        }

        _instance = new CommandLogWindow();
        _instance.Closed += (_, _) => _instance = null;
        _instance.Show(owner);
    }

    // The log changes on whichever thread ran git; one refresh per burst is posted to the UI thread.
    private void OnCommandsChanged()
    {
        if (_refreshQueued)
        {
            return;
        }

        _refreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _refreshQueued = false;
            ShowEntries();
        }, DispatcherPriority.Background);
    }

    private void ShowEntries()
    {
        CommandLogEntry? selected = EntryList.SelectedItem is string ? SelectedEntry() : null;
        _entries = [.. CommandLog.Commands.Reverse()];
        EntryList.ItemsSource = _entries.Select(entry => entry.ColumnLine).ToList();
        CountText.Text = _entries.Count == 1 ? "1 command" : $"{_entries.Count} commands";
        if (selected is not null && IndexOf(selected) is var index and >= 0)
        {
            EntryList.SelectedIndex = index;
        }
    }

    private int IndexOf(CommandLogEntry entry)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (ReferenceEquals(_entries[i], entry))
            {
                return i;
            }
        }

        return -1;
    }

    private CommandLogEntry? SelectedEntry()
        => EntryList.SelectedIndex is var index and >= 0 && index < _entries.Count ? _entries[index] : null;

    private void ShowDetail()
    {
        CommandLogEntry? entry = SelectedEntry();
        DetailText.Text = entry?.Detail ?? "";
        CopyButton.IsEnabled = entry is not null;
    }

    private async Task CopyCommandLineAsync()
    {
        if (SelectedEntry() is { } entry && GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(entry.CommandLine);
        }
    }
}
