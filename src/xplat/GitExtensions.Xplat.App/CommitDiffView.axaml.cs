using System.Globalization;
using Avalonia.Controls;
using GitCommands;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>CommitDiff</c>: a commit's information, its changed files and the selected file's diff. Shared by the
///  commit diff window (upstream's <c>FormCommitDiff</c>) and the file history's Commit tab.
/// </summary>
public partial class CommitDiffView : UserControl
{
    // Upstream's CommitDiff title.
    private const string TitlePrefix = "Diff";

    private readonly IAppPreferences _preferences = AppServices.Preferences;
    private readonly GitCommitHistory _history;
    private string _repositoryPath = "";
    private string? _hash;
    private int _load;

    public CommitDiffView()
    {
        _history = new GitCommitHistory(() => new CommitDateStyle(_preferences.RelativeDate, _preferences.ShowAuthorDate),
            links: AppServices.RevisionLinks);
        InitializeComponent();
        DiffFiles.SelectionChanged += (_, _) => UiActions.Run(ViewSelectedDiffAsync, ShowError);
    }

    /// <summary>
    ///  Raised when <see cref="Title"/> changes, as upstream's <c>TextChanged</c>.
    /// </summary>
    public event EventHandler? TitleChanged;

    /// <summary>
    ///  Upstream's text of the control: "Diff - " and the commit's short hash, author date, author and working directory.
    /// </summary>
    public string Title { get; private set; } = TitlePrefix;

    /// <summary>
    ///  The commit information, whose links the host may handle.
    /// </summary>
    public CommitInfoView Info => CommitInfo;

    /// <summary>
    ///  Upstream's <c>SetRevision</c>: the commit as git has it (not as a filtered grid shows its parents), its files, and
    ///  <paramref name="fileToSelect"/> selected when the commit changed it.
    /// </summary>
    public async Task SetRevisionAsync(string repositoryPath, string hash, string? fileToSelect)
    {
        int load = ++_load;
        _repositoryPath = repositoryPath;
        _hash = hash;
        CommitInfo.RepositoryPath = repositoryPath;
        CommitDetails details;
        IReadOnlyList<CommitFile> files;
        try
        {
            details = await _history.LoadDetailsAsync(repositoryPath, hash);
            files = await _history.LoadFilesAsync(repositoryPath, hash);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (load == _load)
            {
                CommitInfo.Show(null, ex.Message, []);
                DiffFiles.ItemsSource = null;
                DiffText.Clear();
            }

            return;
        }

        if (load != _load)
        {
            return;
        }

        CommitInfo.Show(details, error: null, childHashes: []);
        DiffFiles.ItemsSource = files;
        DiffFiles.SelectedItem = files.FirstOrDefault(file => file.Path == fileToSelect) ?? files.FirstOrDefault();
        SetTitle(details);
    }

    private void SetTitle(CommitDetails details)
    {
        string author = details.Data?.Author ?? details.Author;
        int email = author.LastIndexOf('<');
        string date = details.Data is { } data
            ? data.AuthorDate.LocalDateTime.ToString(CultureInfo.CurrentCulture)
            : details.AuthorDate;
        Title = string.Join(" - ", TitlePrefix, details.Hash[..Math.Min(details.Hash.Length, 8)], date,
            email > 0 ? author[..email].Trim() : author, _repositoryPath.EnsureTrailingPathSeparator());
        TitleChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task ViewSelectedDiffAsync()
    {
        if (_hash is { } hash && DiffFiles.SelectedItem is CommitFile file)
        {
            await DiffText.ShowAsync(_repositoryPath, hash, file.Path, staged: false, file.Path);
        }
        else
        {
            DiffText.Clear();
        }
    }

    private void ShowError(Exception ex) => CommitInfo.Show(null, ex.Message, []);
}
