using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  Upstream's <c>BlameControl</c> without a control (PLAN.md principle 6): for each line of a file as of a commit, the
///  commit and author that last changed it, the commit information above, and the menu with upstream's blame settings. The
///  view keeps the selection (it passes it with <see cref="SetSelection"/>), the highlight of the commit under the mouse,
///  find, go to line and the repository host's menu items.
/// </summary>
public sealed partial class BlameViewModel : ObservableObject
{
    private const int NoLine = 0;
    private const int FirstLine = 1;

    // The properties that follow the blame settings.
    private static readonly string[] _optionProperties =
    [
        nameof(Options), nameof(IgnoreWhitespace), nameof(DetectMoveInFile), nameof(DetectMoveInAllFiles),
        nameof(DisplayAuthorFirst), nameof(ShowAuthor), nameof(ShowAuthorDate), nameof(ShowAuthorTime),
        nameof(CanShowAuthorTime), nameof(ShowLineNumbers), nameof(ShowOriginalFilePath), nameof(ShowAuthorAvatar),
    ];

    private readonly ICommitHistory _history;
    private readonly IAppPreferences _preferences;
    private readonly Func<string, string> _translateMenuText;
    private int _loadVersion;

    // Upstream's _clickedBlameLine: the line to show once the commit asked for has loaded.
    private int? _clickedLine;

    // What "Blame previous revision" goes to, read as the menu opens.
    private BlamePreviousRevision? _previousRevision;
    private int _menuVersion;

    private string? _commitInfoHash;
    private int _commitInfoVersion;
    private int _selectedIndex = -1;

    /// <param name="translateMenuText">Upstream's translation of a menu text, with its access key.</param>
    public BlameViewModel(ICommitHistory history, IAppPreferences preferences, Func<string, string>? translateMenuText = null)
    {
        _history = history;
        _preferences = preferences;
        _translateMenuText = translateMenuText ?? (text => text.Replace('&', '_'));
        PreviousRevisionHeader = _translateMenuText(BlameMenu.BlameActualPreviousRevision);
    }

    /// <summary>
    ///  A line's commit was asked for (double-click, Enter or the menu's blame items); lines not committed yet ask nothing.
    /// </summary>
    public event EventHandler<BlameCommitRequest>? CommitRequested;

    /// <summary>
    ///  The blame settings changed, so a view that shows them too can show the new ones.
    /// </summary>
    public event EventHandler? OptionsChanged;

    /// <summary>
    ///  A loading or option error, for the view's window to show.
    /// </summary>
    public event EventHandler<string>? ErrorOccurred;

    /// <summary>
    ///  Asks the view to select and scroll to a row.
    /// </summary>
    public event EventHandler<int>? SelectLineRequested;

    /// <summary>
    ///  Upstream's <c>IRevisionGridInfo.GetRevision</c>: the row of a commit in the grid of the view's window, null when it
    ///  is not shown. Left null without a grid (upstream's <c>FormBlame</c>), where the menu's blame items are disabled.
    /// </summary>
    public Func<string, CommitRow?>? GridRevision { get; set; }

    /// <summary>
    ///  Upstream's <c>GetRevisionChildren</c>, for the commit information: the commits the view's grid knows as a commit's
    ///  children.
    /// </summary>
    public Func<string, IReadOnlyList<string>>? GridChildren { get; set; }

    /// <summary>
    ///  Puts a text on the clipboard. Set by the view.
    /// </summary>
    public Func<string, Task>? SetClipboard { get; set; }

    /// <summary>
    ///  Shows a commit's changes in their own window (upstream's <c>FormCommitDiff</c>), with the repository and the hash.
    ///  Set by the view.
    /// </summary>
    public Action<string, string>? ShowCommitChanges { get; set; }

    public string RepositoryPath { get; private set; } = "";

    public string Hash { get; private set; } = "";

    public string FilePath { get; private set; } = "";

    /// <summary>
    ///  The lines blamed.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<BlameLine> Lines { get; private set; } = [];

    /// <summary>
    ///  The lines as the gutter shows them, with the settings.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<BlameRow> Rows { get; private set; } = [];

    /// <summary>
    ///  What the commit information above the lines shows.
    /// </summary>
    [ObservableProperty]
    public partial ShownCommit Info { get; private set; } = new(null, null);

    /// <summary>
    ///  Upstream's <c>HideCommitInfo</c>: false for a host that shows the commit's information itself.
    /// </summary>
    [ObservableProperty]
    public partial bool IsCommitInfoVisible { get; private set; } = true;

    [ObservableProperty]
    public partial string PreviousRevisionHeader { get; private set; }

    [ObservableProperty]
    public partial bool CanBlamePreviousRevision { get; private set; }

    [ObservableProperty]
    public partial bool CanBlameRevision { get; private set; }

    /// <summary>
    ///  The selected line has a commit, whose changes and texts the menu offers.
    /// </summary>
    [ObservableProperty]
    public partial bool HasSelectedCommit { get; private set; }

    public BlameOptions Options => _preferences.BlameOptions;

    public bool IgnoreWhitespace => Options.IgnoreWhitespace;

    public bool DetectMoveInFile => Options.DetectMoveInFile;

    public bool DetectMoveInAllFiles => Options.DetectMoveInAllFiles;

    public bool DisplayAuthorFirst => Options.DisplayAuthorFirst;

    public bool ShowAuthor => Options.ShowAuthor;

    public bool ShowAuthorDate => Options.ShowAuthorDate;

    public bool ShowAuthorTime => Options.ShowAuthorTime;

    // Upstream's menu: the time goes with the date.
    public bool CanShowAuthorTime => Options.ShowAuthorDate;

    public bool ShowLineNumbers => Options.ShowLineNumbers;

    public bool ShowOriginalFilePath => Options.ShowOriginalFilePath;

    public bool ShowAuthorAvatar => Options.ShowAuthorAvatar;

    /// <summary>
    ///  Upstream's <c>CurrentFileLine</c>: the selected line's number, or null.
    /// </summary>
    public int? CurrentFileLine => SelectedRow?.Line.LineNumber;

    /// <summary>
    ///  Like upstream's blame, a line leads to the commit that last changed it. Lines not committed yet carry git's all-zero
    ///  hash and have no commit.
    /// </summary>
    public BlameLine? SelectedCommitLine => SelectedRow?.Line is { Commit.ObjectId.IsZero: false } line ? line : null;

    /// <summary>
    ///  The selected row's index, -1 without one.
    /// </summary>
    public int SelectedIndex => _selectedIndex;

    private BlameRow? SelectedRow => _selectedIndex >= 0 && _selectedIndex < Rows.Count ? Rows[_selectedIndex] : null;

    /// <summary>
    ///  Upstream's <c>initialLine</c>: the line to select when the file is next blamed.
    /// </summary>
    public void GoToLineWhenBlamed(int? line) => _clickedLine = line;

    /// <summary>
    ///  Upstream's <c>HideCommitInfo</c>, for a host that shows the commit's information itself.
    /// </summary>
    public void HideCommitInfo() => IsCommitInfoVisible = false;

    /// <summary>
    ///  The view's selected row; upstream's <c>SelectedLineChanged</c> shows its commit's information.
    /// </summary>
    public void SetSelection(int selectedIndex)
    {
        _selectedIndex = selectedIndex;
        HasSelectedCommit = SelectedCommitLine is not null;
        if (SelectedCommitLine?.Commit is { } commit)
        {
            StartShowingCommitInfo(commit.ObjectId.ToString());
        }
    }

    /// <summary>
    ///  Blames <paramref name="filePath"/> as of <paramref name="hash"/>.
    /// </summary>
    public async Task LoadAsync(string repositoryPath, string hash, string filePath)
    {
        // Upstream's LoadBlameAsync: the clicked line, else the current line while the file stays the same.
        int line = _clickedLine ?? (filePath == FilePath ? _selectedIndex + 1 : NoLine);
        _clickedLine = null;
        RepositoryPath = repositoryPath;
        Hash = hash;
        FilePath = filePath;
        await LoadAsync(line);
    }

    /// <summary>
    ///  As upstream's blame menu: a choice is stored at once; a change to git's flags blames the file again.
    /// </summary>
    public async Task ChangeOptionsAsync(BlameOptions options)
    {
        bool blameAgain = !options.Arguments.SequenceEqual(_preferences.BlameOptions.Arguments);
        _preferences.BlameOptions = options;
        _preferences.Save();
        foreach (string name in _optionProperties)
        {
            OnPropertyChanged(name);
        }

        OptionsChanged?.Invoke(this, EventArgs.Empty);
        int line = _selectedIndex + 1;
        if (blameAgain && FilePath.Length > 0)
        {
            await LoadAsync(line);
        }
        else
        {
            ShowRows(line);
        }
    }

    /// <summary>
    ///  Upstream's <c>contextMenu_Opened</c>. Reading the real parents takes git, so "Blame previous revision" is enabled
    ///  when they are known.
    /// </summary>
    public void RefreshMenu()
    {
        GitBlameCommit? commit = SelectedCommitLine?.Commit;
        HasSelectedCommit = commit is not null;
        CommitRow? row = commit is null ? null : GridRevision?.Invoke(commit.ObjectId.ToString());
        CanBlameRevision = row is not null;
        CanBlamePreviousRevision = false;
        _previousRevision = null;
        int version = ++_menuVersion;
        if (row is not null)
        {
            Run(() => ShowPreviousRevisionItemAsync(row, version));
        }
    }

    /// <summary>
    ///  Upstream's <c>BlameFile.GoToLine</c>, limited to the lines there are, as a line can be gone in another commit.
    /// </summary>
    public void GoToLine(int line)
    {
        if (line != NoLine && Lines.Count > 0)
        {
            SelectLineRequested?.Invoke(this, Math.Min(line, Lines.Count) - 1);
        }
    }

    // Upstream's blame menu's check items.
    [RelayCommand]
    private Task ToggleOptionAsync(BlameOption option) => GuardAsync(() => ChangeOptionsAsync(Options.Toggle(option)));

    // Upstream's BlameRevision: the selected line's commit; also a double-click and Enter.
    [RelayCommand]
    private void BlameRevision()
    {
        if (SelectedCommitLine is { Commit: { } commit } line)
        {
            RequestCommit(commit.ObjectId.ToString(), commit.FileName, line.OriginLineNumber);
        }
    }

    // Upstream's blamePreviousRevisionToolStripMenuItem_Click: the parent, on the line this one had there.
    [RelayCommand]
    private Task BlamePreviousRevisionAsync() => GuardAsync(async () =>
    {
        if (SelectedCommitLine is { Commit: { } commit } line && _previousRevision?.Parents is [var parent, ..] parents)
        {
            int previousLine = await _history.OriginalLineInPreviousCommitAsync(RepositoryPath,
                commit.ObjectId.ToString(), parents, commit.FileName, line.OriginLineNumber);
            RequestCommit(parent, commit.FileName, Math.Max(previousLine, FirstLine));
        }
    });

    [RelayCommand]
    private void ShowChanges()
    {
        if (SelectedCommitLine?.Commit is { } commit)
        {
            ShowCommitChanges?.Invoke(RepositoryPath, commit.ObjectId.ToString());
        }
    }

    [RelayCommand]
    private Task CopyCommitHashAsync() => CopyAsync(commit => commit.ObjectId.ToString());

    [RelayCommand]
    private Task CopyCommitMessageAsync() => CopyAsync(commit => commit.Summary);

    [RelayCommand]
    private Task CopyAllCommitInfoAsync() => CopyAsync(commit => commit.ToString());

    private Task CopyAsync(Func<GitBlameCommit, string> format)
        => SelectedCommitLine?.Commit is { } commit && SetClipboard is { } setClipboard
            ? GuardAsync(() => setClipboard(format(commit)))
            : Task.CompletedTask;

    // The file is blamed again when the window's grid selects the commit, unless it is the one shown, which has only to go
    // to the line.
    private void RequestCommit(string hash, string fileName, int line)
    {
        if (GridRevision?.Invoke(hash) is not null)
        {
            if (hash == Hash && fileName == FilePath)
            {
                GoToLine(line);
                return;
            }

            _clickedLine = line;
        }

        CommitRequested?.Invoke(this, new BlameCommitRequest(hash, fileName));
    }

    private async Task ShowPreviousRevisionItemAsync(CommitRow row, int version)
    {
        IReadOnlyList<string> parents = await _history.LoadParentsAsync(RepositoryPath, row.Hash);
        if (version == _menuVersion)
        {
            _previousRevision = BlameMenu.PreviousRevision(row.ParentHashes, parents,
                hash => GridRevision?.Invoke(hash) is not null);
            PreviousRevisionHeader = _translateMenuText(_previousRevision.Text);
            CanBlamePreviousRevision = _previousRevision.Parents is not null;
        }
    }

    private async Task LoadAsync(int line)
    {
        int version = ++_loadVersion;
        IReadOnlyList<BlameLine> lines =
            await _history.LoadBlameAsync(RepositoryPath, Hash, FilePath, _preferences.BlameOptions);
        if (version == _loadVersion)
        {
            Lines = lines;
            ShowRows(line);

            // Upstream's ProcessBlame: the blamed commit, whatever commit the line it went to is from.
            StartShowingCommitInfo(Hash);
        }
    }

    private void ShowRows(int line)
    {
        Rows = BlameRow.WithSyntax(_preferences.BlameOptions.Rows(Lines, FilePath, CultureInfo.CurrentCulture), FilePath);
        GoToLine(line);
    }

    // Not awaited: the blame shows without waiting for the commit info, whose errors go to ErrorOccurred.
    private void StartShowingCommitInfo(string hash)
    {
        if (IsCommitInfoVisible && hash != _commitInfoHash)
        {
            _commitInfoHash = hash;
            Run(() => ShowCommitInfoAsync(hash));
        }
    }

    private async Task ShowCommitInfoAsync(string hash)
    {
        int version = ++_commitInfoVersion;
        CommitDetails? details = null;
        string? error = null;
        try
        {
            details = await _history.LoadDetailsAsync(RepositoryPath, hash);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            error = ex.Message;
        }

        if (version == _commitInfoVersion)
        {
            Info = new ShownCommit(details, error, details is null ? [] : GridChildren?.Invoke(details.Hash) ?? []);
        }
    }

    private void Run(Func<Task> action) => _ = GuardAsync(action);

    // An exception that escapes an asynchronous command ends the app; it goes to the window instead, as the view did.
    private async Task GuardAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            // Nothing to report.
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex.Message);
        }
    }
}
