using System.Globalization;
using Avalonia.Controls;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.App.RepositoryHosts;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Settings;
using GitUIPluginInterfaces.RepositoryHosts;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>BlameControl</c>: for each line of a file as of a commit, the commit and author that last changed it, with
///  upstream's blame settings in the menu. Shared by the blame window and the file history's Blame tab.
/// </summary>
public partial class BlameView : UserControl
{
    private readonly GitCommitHistory _history = new();
    private readonly IAppPreferences _preferences = AppServices.Preferences;
    private readonly List<object> _hostMenuItems = [];
    private IReadOnlyList<BlameLine> _lines = [];
    private string _repositoryPath = "";
    private string _hash = "";
    private string _filePath = "";

    public BlameView()
    {
        InitializeComponent();
        BlameList.DoubleTapped += (_, _) => RequestSelectedLineCommit();
        BlameList.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                e.Handled = true;
                RequestSelectedLineCommit();
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
        BlameContextMenu.Opening += (_, _) => ShowRepositoryHostItems();
        ShowMenuChecks();
    }

    /// <summary>
    ///  A line's commit was asked for (double-click or Enter), with the commit's hash; lines not committed yet ask nothing.
    /// </summary>
    public event EventHandler<string>? CommitRequested;

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
    ///  The lines shown, for tests.
    /// </summary>
    public IReadOnlyList<BlameLine> Lines => _lines;

    /// <summary>
    ///  Blames <paramref name="filePath"/> as of <paramref name="hash"/>.
    /// </summary>
    public async Task LoadAsync(string repositoryPath, string hash, string filePath)
    {
        _repositoryPath = repositoryPath;
        _hash = hash;
        _filePath = filePath;
        await LoadAsync();
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
        if (blameAgain && _filePath.Length > 0)
        {
            await LoadAsync();
        }
        else
        {
            ShowRows();
        }
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
    private void RequestSelectedLineCommit()
    {
        if (BlameList.SelectedItem is BlameRow { Line: var line } && line.Hash.Any(c => c != '0'))
        {
            CommitRequested?.Invoke(this, line.Hash);
        }
    }

    private async Task LoadAsync()
    {
        _lines = await _history.LoadBlameAsync(_repositoryPath, _hash, _filePath, _preferences.BlameOptions);
        ShowRows();
    }

    private void ShowRows()
        => BlameList.ItemsSource = _preferences.BlameOptions.Rows(_lines, _filePath, CultureInfo.CurrentCulture);

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
            || BlameList.SelectedItem is not BlameRow { Line: var line }
            || !ObjectId.TryParse(line.Hash, out ObjectId blameId)
            || blameId.IsZero)
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
