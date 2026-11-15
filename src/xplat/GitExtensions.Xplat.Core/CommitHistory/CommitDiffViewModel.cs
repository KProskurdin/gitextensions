using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using GitCommands;
using GitExtensions.Xplat.Core.Diff;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  Upstream's <c>CommitDiff</c> without a control (PLAN.md principle 6): a commit's information, its changed files and the
///  selected file's diff, which this owns (<see cref="Diff"/>). Shared by the commit diff window (upstream's
///  <c>FormCommitDiff</c>) and the file history's Commit tab.
/// </summary>
public sealed partial class CommitDiffViewModel : ObservableObject
{
    // Upstream's CommitDiff title.
    public const string TitlePrefix = "Diff";

    private readonly ICommitHistory _history;
    private string? _hash;
    private int _load;

    public CommitDiffViewModel(ICommitHistory history, DiffViewModel diff)
    {
        _history = history;
        Diff = diff;
    }

    /// <summary>
    ///  The selected file's diff.
    /// </summary>
    public DiffViewModel Diff { get; }

    /// <summary>
    ///  Upstream's text of the control: "Diff - " and the commit's short hash, author date, author and working directory.
    /// </summary>
    [ObservableProperty]
    public partial string Title { get; private set; } = TitlePrefix;

    /// <summary>
    ///  The repository of the commit shown.
    /// </summary>
    [ObservableProperty]
    public partial string RepositoryPath { get; private set; } = "";

    /// <summary>
    ///  What the commit information shows: the commit, or why it could not be read.
    /// </summary>
    [ObservableProperty]
    public partial ShownCommit Info { get; private set; } = new(null, null);

    [ObservableProperty]
    public partial IReadOnlyList<CommitFile> Files { get; private set; } = [];

    /// <summary>
    ///  The file whose diff shows; none clears the diff.
    /// </summary>
    [ObservableProperty]
    public partial CommitFile? SelectedFile { get; set; }

    /// <summary>
    ///  Upstream's <c>SetRevision</c>: the commit as git has it (not as a filtered grid shows its parents), its files, and
    ///  <paramref name="fileToSelect"/> selected when the commit changed it.
    /// </summary>
    public async Task SetRevisionAsync(string repositoryPath, string hash, string? fileToSelect)
    {
        int load = ++_load;
        RepositoryPath = repositoryPath;
        _hash = hash;
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
                Info = new ShownCommit(null, ex.Message);
                SelectedFile = null;
                Files = [];
            }

            return;
        }

        if (load != _load)
        {
            return;
        }

        Info = new ShownCommit(details, null);

        // A file of the same status and path in another commit is an equal record: it is selected again from none, so its
        // diff is read for this commit.
        SelectedFile = null;
        Files = files;
        SelectedFile = files.FirstOrDefault(file => file.Path == fileToSelect) ?? files.FirstOrDefault();
        Title = TitleOf(details, repositoryPath);
    }

    /// <summary>
    ///  Upstream CommitDiff's <c>FileViewer_TopScrollReached</c> and <c>FileViewer_BottomScrollReached</c>: the file after
    ///  (or before) the selected one, none past the ends; the first without a selection.
    /// </summary>
    public void SelectNeighbourFile(bool next)
    {
        int current = SelectedFile is { } selected ? IndexOf(selected) : -1;
        CommitFile? target = current < 0
            ? Files.FirstOrDefault()
            : next
                ? Files.Skip(current + 1).FirstOrDefault()
                : Files.Take(current).LastOrDefault();
        if (target is not null)
        {
            SelectedFile = target;
        }
    }

    partial void OnSelectedFileChanged(CommitFile? value) => _ = ShowDiffAsync(value);

    private static string TitleOf(CommitDetails details, string repositoryPath)
    {
        string author = details.Data?.Author ?? details.Author;
        int email = author.LastIndexOf('<');
        string date = details.Data is { } data
            ? data.AuthorDate.LocalDateTime.ToString(CultureInfo.CurrentCulture)
            : details.AuthorDate;
        return string.Join(" - ", TitlePrefix, details.Hash[..Math.Min(details.Hash.Length, 8)], date,
            email > 0 ? author[..email].Trim() : author, repositoryPath.EnsureTrailingPathSeparator());
    }

    private int IndexOf(CommitFile file)
    {
        for (int i = 0; i < Files.Count; i++)
        {
            if (ReferenceEquals(Files[i], file))
            {
                return i;
            }
        }

        return -1;
    }

    // An error of the diff's read shows where the commit's information shows, as upstream's control did.
    private async Task ShowDiffAsync(CommitFile? file)
    {
        try
        {
            if (_hash is { } hash && file is not null)
            {
                await Diff.ShowAsync(RepositoryPath, hash, file.Path, staged: false, file.Path);
            }
            else
            {
                Diff.Clear();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Info = new ShownCommit(null, ex.Message);
        }
    }
}

/// <summary>
///  A commit's information as the commit info control shows it: the commit, or the error that kept it from being read, and
///  the children the window's grid knows it has.
/// </summary>
public sealed record ShownCommit(CommitDetails? Details, string? Error, IReadOnlyList<string>? Children = null);
