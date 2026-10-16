using Avalonia.Controls;
using Avalonia.Input;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.App;

/// <summary>
///  An entry of the stash window's "Show:" box: upstream's <c>GitStash.Summary</c>.
/// </summary>
public sealed record StashChoice(GitStash Stash, string Text);

/// <summary>
///  A row of the stash window's file list: a group's header or a file.
/// </summary>
public sealed record StashFileRow(string Text, StashFile? File)
{
    public bool IsFile => File is not null;

    public bool IsHeader => File is null;

    public string? Image => File is null ? null : FileStatusList.ImageFor(File.Change.Kind);
}

/// <summary>
///  Upstream's <c>FormStash</c> in the new shell (rules in <see cref="StashDialog"/>): the working directory's changes and
///  the stashes, the selected one's files and their diffs, a new stash of all or the selected changes, and drop and apply
///  of the selected stash. "Keep index" and "Include untracked files" are upstream's settings, kept on close.
/// </summary>
public partial class StashWindow : Window
{
    // Upstream's splitter distance at 100 %.
    private const double LeftPanelWidth = 280;

    private readonly string _repositoryPath;
    private readonly RepositoryOperationsViewModel _actions;
    private readonly IAppPreferences _preferences;
    private readonly StashDialog _dialog;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _showing;
    private int _filesVersion;

    /// <param name="manageStashes">Upstream's <c>ManageStashes</c>: show the newest stash first.</param>
    /// <param name="initialStash">The stash to show first ("stash@{n}").</param>
    public StashWindow(string repositoryPath, RepositoryOperationsViewModel actions, IAppPreferences preferences,
        bool manageStashes = true, string? initialStash = null)
    {
        _repositoryPath = repositoryPath;
        _actions = actions;
        _preferences = preferences;
        _dialog = new StashDialog(manageStashes, initialStash);
        InitializeComponent();
        WindowPlacementTracker.Attach(this, "Xplat.StashWindow");

        KeepIndexCheck.IsChecked = preferences.StashKeepIndex;
        IncludeUntrackedCheck.IsChecked = preferences.IncludeUntrackedFilesInManualStash;
        StashesBox.SelectionChanged += (_, _) =>
        {
            if (!_showing && StashesBox.SelectedIndex >= 0)
            {
                _dialog.Select(StashesBox.SelectedIndex);
                Run(ShowSelectedStashAsync);
            }
        };
        FilesList.SelectionChanged += (_, _) => ShowSelectedFile();
        StashAllButton.Click += (_, _) => Run(() => StashAsync(selectedOnly: false));
        StashSelectedButton.Click += (_, _) => Run(() => StashAsync(selectedOnly: true));
        DropButton.Click += (_, _) => Run(DropAsync);
        ApplyButton.Click += (_, _) => Run(ApplyAsync);
        KeyDown += OnKeyDown;
        Opened += (_, _) =>
        {
            FitLeftPanel();
            Run(LoadAsync);
        };
        Closing += (_, _) =>
        {
            _preferences.StashKeepIndex = KeepIndexCheck.IsChecked == true;
            _preferences.IncludeUntrackedFilesInManualStash = IncludeUntrackedCheck.IsChecked == true;
            _preferences.Save();
        };
    }

    /// <summary>
    ///  The dialog state, for tests.
    /// </summary>
    public StashDialog Dialog => _dialog;

    /// <summary>
    ///  Completes when the stashes and the first stash's files are shown, for tests.
    /// </summary>
    public Task StashesLoaded => _loaded.Task;

    /// <summary>
    ///  True while the selected stash's files are read, for tests.
    /// </summary>
    public bool IsLoadingFiles { get; private set; }

    private IReadOnlyList<StashFile> SelectedFiles
        => [.. FilesList.SelectedItems?.OfType<StashFileRow>().Select(row => row.File).OfType<StashFile>() ?? []];

    // As upstream: the left panel keeps its 280 pixels unless the options and buttons need more (a larger font).
    private void FitLeftPanel()
    {
        OptionsGrid.Measure(Avalonia.Size.Infinity);
        SplitGrid.ColumnDefinitions[0].Width = new GridLength(Math.Max(LeftPanelWidth, OptionsGrid.DesiredSize.Width));
    }

    // Upstream's Initialize: the stashes read again, then the chosen one shown.
    private async Task LoadAsync()
    {
        IReadOnlyList<GitStash> stashes =
            await Task.Run(() => GitModules.Open(_repositoryPath).GetStashes(noLocks: false));
        _dialog.Load(stashes);
        _showing = true;
        StashesBox.ItemsSource = _dialog.Items
            .Select(stash =>
                new StashChoice(stash, stash.Index == -1 ? UpstreamTranslation.ByText(stash.Message) : stash.Summary))
            .ToList();
        StashesBox.SelectedIndex = _dialog.SelectedIndex;
        _showing = false;
        await ShowSelectedStashAsync();
        _loaded.TrySetResult();
    }

    // Upstream's StashesSelectedIndexChanged and InitializeSoft: the message, the buttons and the files of the selection.
    private async Task ShowSelectedStashAsync()
    {
        if (_dialog.Selected is not { } stash)
        {
            return;
        }

        int version = ++_filesVersion;
        MessageBox.Text = UpstreamTranslation.ByText(_dialog.Message);
        MessageBox.IsReadOnly = _dialog.IsMessageReadOnly;
        DropButton.IsEnabled = ApplyButton.IsEnabled = _dialog.CanApplyOrDrop;
        Diff.Clear();
        FilesList.ItemsSource = null;
        IsLoadingFiles = true;
        StashesBox.IsEnabled = false;
        try
        {
            IReadOnlyList<StashFile> files =
                await Task.Run(() => _dialog.Files(GitModules.Open(_repositoryPath), stash));
            if (version != _filesVersion)
            {
                return;
            }

            List<StashFileRow> rows = [];
            foreach (IGrouping<string, StashFile> group in files.GroupBy(file => file.Group))
            {
                if (group.Key.Length > 0)
                {
                    rows.Add(new StashFileRow(UpstreamTranslation.ByText(group.Key), null));
                }

                rows.AddRange(group.Select(file => new StashFileRow(file.Change.Path, file)));
            }

            FilesList.ItemsSource = rows;
            if (rows.FirstOrDefault(row => row.IsFile) is { } first)
            {
                FilesList.SelectedItem = first;
            }
        }
        finally
        {
            if (version == _filesVersion)
            {
                IsLoadingFiles = false;
                StashesBox.IsEnabled = true;
                UpdateStashSelectedButton();
            }
        }
    }

    private void ShowSelectedFile()
    {
        UpdateStashSelectedButton();
        if (SelectedFiles is [{ } file])
        {
            Run(() => Diff.ShowAsync(_repositoryPath, file.Commit, file.Change.Path, file.Change.Staged,
                file.Change.Path));
        }
    }

    private void UpdateStashSelectedButton()
        => StashSelectedButton.IsEnabled = _dialog.CanStashSelected(SelectedFiles.Count);

    // Upstream's StashClick and StashSelectedFiles_Click: a new stash, then the list read again.
    private async Task StashAsync(bool selectedOnly)
    {
        IReadOnlyList<string>? paths =
            selectedOnly ? [.. SelectedFiles.Select(file => file.Change.Path).Distinct()] : null;
        if (selectedOnly && paths!.Count == 0)
        {
            return;
        }

        await _actions.StashAsync(_repositoryPath, StashDialog.MessageArgument(MessageBox.Text),
            IncludeUntrackedCheck.IsChecked == true, KeepIndexCheck.IsChecked == true, paths);
        await LoadAsync();
    }

    // Upstream's ClearClick: asks first (unless "Don't show me this message again" was chosen), and shows the stash that
    // takes the dropped one's place.
    private async Task DropAsync()
    {
        if (_dialog.Selected is not { } stash || !_dialog.CanApplyOrDrop
                                              || !await ConfirmWindow.AskAsync(this, _preferences,
                                                  Confirmation.StashDrop, Confirmations.StashDropQuestion,
                                                  "Drop", StashDialog.DropConfirmTitle))
        {
            return;
        }

        _dialog.Dropping();
        await _actions.DropStashAsync(_repositoryPath, stash.Name);
        await LoadAsync();
    }

    private async Task ApplyAsync()
    {
        if (_dialog.Selected is { } stash && _dialog.CanApplyOrDrop)
        {
            await _actions.ApplyStashAsync(_repositoryPath, stash.Name);
            await LoadAsync();
        }
    }

    // Upstream's Stash hotkeys (FormStash.Command) and Escape, which closes the window.
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            Close();
            e.Handled = true;
            return;
        }

        StashCommand? command = Hotkeys.Stash.Match(e);
        switch (command)
        {
            case StashCommand.NextStash or StashCommand.PreviousStash when !IsLoadingFiles:
                if (_dialog.SelectNeighbour(next: command == StashCommand.NextStash))
                {
                    StashesBox.SelectedIndex = _dialog.SelectedIndex;
                }

                e.Handled = true;
                break;

            // As upstream's RefreshAll: only the working directory's changes can have changed.
            case StashCommand.Refresh:
                if (_dialog.IsWorkingDirectorySelected)
                {
                    Run(LoadAsync);
                }

                e.Handled = true;
                break;
        }
    }

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
}
