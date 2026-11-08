using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.App.RepositoryHosts;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using GitUIPluginInterfaces.RepositoryHosts;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>BlameControl</c>: for each line of a file as of a commit, the commit and author that last changed it, with
///  upstream's blame settings in the menu. Shared by the blame window and the file history's Blame tab.
/// </summary>
public partial class BlameView : UserControl
{
    private const int NoLine = 0;
    private const int FirstLine = 1;
    private const string SameCommitClass = "sameCommit";

    private readonly IAppPreferences _preferences = AppServices.Preferences;
    private readonly GitCommitHistory _history;
    private readonly List<object> _hostMenuItems = [];
    private IReadOnlyList<BlameLine> _lines = [];
    private string _repositoryPath = "";
    private string _hash = "";
    private string _filePath = "";
    private int _loadVersion;

    // Upstream's _clickedBlameLine: the line to show once the commit asked for has loaded.
    private int? _clickedLine;

    // What the menu's "Blame previous revision" goes to, read as the menu opens.
    private BlamePreviousRevision? _previousRevision;
    private int _menuVersion;

    private string? _commitInfoHash;
    private int _commitInfoVersion;

    // Upstream's _highlightedCommit: the short hash of the commit under the mouse.
    private string? _highlightedHash;

    public BlameView()
    {
        _history = new GitCommitHistory(() => new CommitDateStyle(_preferences.RelativeDate, _preferences.ShowAuthorDate),
            links: AppServices.RevisionLinks);
        InitializeComponent();
        BlameList.SelectionChanged += (_, _) =>
        {
            // Upstream's SelectedLineChanged.
            if (SelectedCommitLine()?.Commit is { } commit)
            {
                ShowCommitInfo(commit.ObjectId.ToString());
            }
        };
        BlameList.PointerMoved += (_, e) => HighlightCommit(
            ((e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext as BlameRow)?.Line.Hash);
        BlameList.ContainerPrepared += (_, e) => ShowHighlight(e.Container);
        BlameList.DoubleTapped += (_, _) => RequestSelectedLineCommit();
        BlameList.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                e.Handled = true;
                RequestSelectedLineCommit();
            }
            else if (RunFindHotkey(e))
            {
                e.Handled = true;
            }
        };

        Toggle(IgnoreWhitespaceMenuItem, options => options with { IgnoreWhitespace = !options.IgnoreWhitespace });
        Toggle(DetectMoveInFileMenuItem, options => options with { DetectMoveInFile = !options.DetectMoveInFile });
        Toggle(DetectMoveInAllFilesMenuItem,
            options => options with { DetectMoveInAllFiles = !options.DetectMoveInAllFiles });
        Toggle(DisplayAuthorFirstMenuItem,
            options => options with { DisplayAuthorFirst = !options.DisplayAuthorFirst });
        Toggle(ShowAuthorMenuItem, options => options.ToggleShowAuthor());
        Toggle(ShowAuthorDateMenuItem, options => options.ToggleShowAuthorDate());
        Toggle(ShowAuthorTimeMenuItem, options => options with { ShowAuthorTime = !options.ShowAuthorTime });
        Toggle(ShowLineNumbersMenuItem, options => options with { ShowLineNumbers = !options.ShowLineNumbers });
        Toggle(ShowOriginalFilePathMenuItem,
            options => options with { ShowOriginalFilePath = !options.ShowOriginalFilePath });
        Toggle(ShowAuthorAvatarMenuItem, options => options with { ShowAuthorAvatar = !options.ShowAuthorAvatar });
        BlameContextMenu.Opening += (_, _) =>
        {
            ShowCommitItems();
            ShowRepositoryHostItems();
        };
        BlameRevisionMenuItem.Click += (_, _) => RequestSelectedLineCommit();
        BlamePreviousRevisionMenuItem.Click += (_, _) => UiActions.Run(BlamePreviousRevisionAsync,
            ex => ErrorOccurred?.Invoke(this, ex.Message));
        ShowChangesMenuItem.Click += (_, _) => ShowChanges();
        CommitHashMenuItem.Click += (_, _) => CopyToClipboard(commit => commit.ObjectId.ToString());
        CommitMessageMenuItem.Click += (_, _) => CopyToClipboard(commit => commit.Summary);
        AllCommitInfoMenuItem.Click += (_, _) => CopyToClipboard(commit => commit.ToString());
        ShowMenuChecks();
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
    ///  The repository host plugin of the repository, whose items the menu shows, as upstream's blame menu shows the
    ///  GitHub plugin's "View in GitHub".
    /// </summary>
    public Func<IRepositoryHostPlugin?>? RepositoryHost { get; set; }

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
    ///  The commit information above the lines, whose links the host may handle.
    /// </summary>
    public CommitInfoView Info => CommitInfo;

    /// <summary>
    ///  The lines shown, for tests.
    /// </summary>
    public IReadOnlyList<BlameLine> Lines => _lines;

    /// <summary>
    ///  Upstream's <c>CurrentFileLine</c>: the selected line's number, or null.
    /// </summary>
    public int? CurrentFileLine => BlameList.SelectedItem is BlameRow { Line: var line } ? line.LineNumber : null;

    /// <summary>
    ///  Upstream's <c>initialLine</c>: the line to select when the file is next blamed.
    /// </summary>
    public void GoToLineWhenBlamed(int? line) => _clickedLine = line;

    /// <summary>
    ///  Blames <paramref name="filePath"/> as of <paramref name="hash"/>.
    /// </summary>
    public async Task LoadAsync(string repositoryPath, string hash, string filePath)
    {
        CommitInfo.RepositoryPath = repositoryPath;

        // Upstream's LoadBlameAsync: the clicked line, else the current line while the file stays the same.
        int line = _clickedLine ?? (filePath == _filePath ? BlameList.SelectedIndex + 1 : NoLine);
        _clickedLine = null;
        _repositoryPath = repositoryPath;
        _hash = hash;
        _filePath = filePath;
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
        ShowMenuChecks();
        OptionsChanged?.Invoke(this, EventArgs.Empty);
        int line = BlameList.SelectedIndex + 1;
        if (blameAgain && _filePath.Length > 0)
        {
            await LoadAsync(line);
        }
        else
        {
            ShowRows(line);
        }
    }

    /// <summary>
    ///  Upstream's <c>HideCommitInfo</c>, for a host that shows the commit's information itself.
    /// </summary>
    public void HideCommitInfo()
    {
        CommitInfo.IsVisible = false;
        CommitInfoSplitter.IsVisible = false;
        Layout.RowDefinitions[0].Height = new GridLength(0);
        Layout.RowDefinitions[1].Height = new GridLength(0);
    }

    private void Toggle(MenuItem item, Func<BlameOptions, BlameOptions> change)
        => item.Click += (_, _) => UiActions.Run(() => ChangeOptionsAsync(change(_preferences.BlameOptions)),
            ex => ErrorOccurred?.Invoke(this, ex.Message));

    private void ShowMenuChecks()
    {
        BlameOptions options = _preferences.BlameOptions;
        IgnoreWhitespaceMenuItem.IsChecked = options.IgnoreWhitespace;
        DetectMoveInFileMenuItem.IsChecked = options.DetectMoveInFile;
        DetectMoveInAllFilesMenuItem.IsChecked = options.DetectMoveInAllFiles;
        DisplayAuthorFirstMenuItem.IsChecked = options.DisplayAuthorFirst;
        ShowAuthorMenuItem.IsChecked = options.ShowAuthor;
        ShowAuthorDateMenuItem.IsChecked = options.ShowAuthorDate;
        ShowAuthorTimeMenuItem.IsChecked = options.ShowAuthorTime;
        ShowAuthorTimeMenuItem.IsEnabled = options.ShowAuthorDate;
        ShowLineNumbersMenuItem.IsChecked = options.ShowLineNumbers;
        ShowOriginalFilePathMenuItem.IsChecked = options.ShowOriginalFilePath;
        ShowAuthorAvatarMenuItem.IsChecked = options.ShowAuthorAvatar;
    }

    // Like upstream's blame, a line leads to the commit that last changed it. Lines not committed yet carry git's all-zero
    // hash and have no commit.
    private BlameLine? SelectedCommitLine()
        => BlameList.SelectedItem is BlameRow { Line: { Commit.ObjectId.IsZero: false } line } ? line : null;

    private void RequestSelectedLineCommit()
    {
        if (SelectedCommitLine() is { Commit: { } commit } line)
        {
            RequestCommit(commit.ObjectId.ToString(), commit.FileName, line.OriginLineNumber);
        }
    }

    // Upstream's BlameRevision. The file is blamed again when the window's grid selects the commit, unless it is the one
    // shown, which has only to go to the line.
    private void RequestCommit(string hash, string fileName, int line)
    {
        if (GridRevision?.Invoke(hash) is not null)
        {
            if (hash == _hash && fileName == _filePath)
            {
                GoToLine(line);
                return;
            }

            _clickedLine = line;
        }

        CommitRequested?.Invoke(this, new BlameCommitRequest(hash, fileName));
    }

    // Upstream's contextMenu_Opened. Reading the real parents takes git, so the item is enabled when they are known.
    private void ShowCommitItems()
    {
        GitBlameCommit? commit = SelectedCommitLine()?.Commit;
        ShowChangesMenuItem.IsEnabled = commit is not null;
        CopyToClipboardMenuItem.IsEnabled = commit is not null;
        CommitRow? row = commit is null ? null : GridRevision?.Invoke(commit.ObjectId.ToString());
        BlameRevisionMenuItem.IsEnabled = row is not null;
        BlamePreviousRevisionMenuItem.IsEnabled = false;
        _previousRevision = null;
        int version = ++_menuVersion;
        if (row is not null)
        {
            UiActions.Run(() => ShowPreviousRevisionItemAsync(row, version), ex => ErrorOccurred?.Invoke(this, ex.Message));
        }
    }

    private async Task ShowPreviousRevisionItemAsync(CommitRow row, int version)
    {
        IReadOnlyList<string> parents = await _history.LoadParentsAsync(_repositoryPath, row.Hash);
        if (version == _menuVersion)
        {
            _previousRevision = BlameMenu.PreviousRevision(row.ParentHashes, parents,
                hash => GridRevision?.Invoke(hash) is not null);
            BlamePreviousRevisionMenuItem.Header =
                UpstreamTranslation.WithAccessKey(UpstreamTranslation.ByText(_previousRevision.Text));
            BlamePreviousRevisionMenuItem.IsEnabled = _previousRevision.Parents is not null;
        }
    }

    // Upstream's blamePreviousRevisionToolStripMenuItem_Click: the parent, on the line this one had there.
    private async Task BlamePreviousRevisionAsync()
    {
        if (SelectedCommitLine() is { Commit: { } commit } line && _previousRevision?.Parents is [var parent, ..] parents)
        {
            int previousLine = await _history.OriginalLineInPreviousCommitAsync(_repositoryPath,
                commit.ObjectId.ToString(), parents, commit.FileName, line.OriginLineNumber);
            RequestCommit(parent, commit.FileName, Math.Max(previousLine, FirstLine));
        }
    }

    private void ShowChanges()
    {
        if (SelectedCommitLine()?.Commit is { } commit && TopLevel.GetTopLevel(this) is Window owner)
        {
            new CommitDiffWindow(_repositoryPath, commit.ObjectId.ToString()).Show(owner);
        }
    }

    private void CopyToClipboard(Func<GitBlameCommit, string> format)
    {
        if (SelectedCommitLine()?.Commit is { } commit && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            UiActions.Run(() => clipboard.SetTextAsync(format(commit)), ex => ErrorOccurred?.Invoke(this, ex.Message));
        }
    }

    private async Task LoadAsync(int line)
    {
        int version = ++_loadVersion;
        IReadOnlyList<BlameLine> lines =
            await _history.LoadBlameAsync(_repositoryPath, _hash, _filePath, _preferences.BlameOptions);
        if (version == _loadVersion)
        {
            _lines = lines;
            ShowRows(line);

            // Upstream's ProcessBlame: the blamed commit, whatever commit the line it went to is from.
            ShowCommitInfo(_hash);
        }
    }

    private void ShowCommitInfo(string hash)
    {
        if (CommitInfo.IsVisible && hash != _commitInfoHash)
        {
            _commitInfoHash = hash;
            UiActions.Run(() => ShowCommitInfoAsync(hash), ex => ErrorOccurred?.Invoke(this, ex.Message));
        }
    }

    private async Task ShowCommitInfoAsync(string hash)
    {
        int version = ++_commitInfoVersion;
        CommitDetails? details = null;
        string? error = null;
        try
        {
            details = await _history.LoadDetailsAsync(_repositoryPath, hash);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            error = ex.Message;
        }

        if (version == _commitInfoVersion)
        {
            CommitInfo.Show(details, error, details is null ? [] : GridChildren?.Invoke(details.Hash) ?? []);
        }
    }

    private void HighlightCommit(string? hash)
    {
        if (hash != _highlightedHash)
        {
            _highlightedHash = hash;
            foreach (Control container in BlameList.GetRealizedContainers())
            {
                ShowHighlight(container);
            }
        }
    }

    private void ShowHighlight(Control container)
        => container.Classes.Set(SameCommitClass, _highlightedHash is not null
            && BlameList.ItemFromContainer(container) is BlameRow row && row.Line.Hash == _highlightedHash);

    private void ShowRows(int line)
    {
        _highlightedHash = null;
        BlameList.ItemsSource = BlameRow.WithSyntax(_preferences.BlameOptions.Rows(_lines, _filePath, CultureInfo.CurrentCulture), _filePath);
        _found = null;
        GoToLine(line);
    }

    // Upstream's BlameFile.GoToLine, limited to the lines there are, as a line can be gone in another commit.
    private void GoToLine(int line)
    {
        if (line != NoLine && _lines.Count > 0)
        {
            int index = Math.Min(line, _lines.Count) - 1;
            BlameList.SelectedIndex = index;
            BlameList.ScrollIntoView(index);
        }
    }

    // Upstream's ConfigureRepositoryHostPlugin and the menu's tag: the host plugin's items for the line under the menu, after
    // the blame settings. A line not committed yet has no commit to show.
    private void ShowRepositoryHostItems()
    {
        foreach (object item in _hostMenuItems)
        {
            BlameContextMenu.Items.Remove(item);
        }

        _hostMenuItems.Clear();
        if (RepositoryHost?.Invoke() is not { } host
            || SelectedCommitLine()?.Commit?.ObjectId is not { } blameId)
        {
            return;
        }

        int lineIndex = BlameList.SelectedIndex;
        IReadOnlyList<MenuItem> items =
            RepositoryHostMenus.ForBlame(host, new GitBlameContext(_filePath, lineIndex, lineIndex, blameId));
        if (items.Count > 0)
        {
            _hostMenuItems.Add(new Separator());
            _hostMenuItems.AddRange(items);
            foreach (object item in _hostMenuItems)
            {
                BlameContextMenu.Items.Add(item);
            }
        }
    }
}
