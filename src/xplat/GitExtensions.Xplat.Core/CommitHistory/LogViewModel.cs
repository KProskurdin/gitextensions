using CommunityToolkit.Mvvm.ComponentModel;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  Upstream's <c>FormLog</c> (<c>StartCompareRevisionsDialog</c>, which a double-click on the browse window's grid with
///  nothing selected opens): a revision grid, the files of the selected commits in upstream's diff groups (its
///  <c>FileStatusList.SetDiffs</c>, core <see cref="DiffGroups"/>) and the selected file's diff, which the window shows.
/// </summary>
public sealed partial class LogViewModel : DialogViewModel
{
    /// <summary>
    ///  Upstream's translation category, the form's class.
    /// </summary>
    public const string Category = "FormLog";

    private readonly string _repositoryPath;
    private readonly RevisionFilter _filter;
    private readonly GridSelection _selection = new();
    private int _selections;

    /// <param name="host">The window.</param>
    /// <param name="repositoryPath">The repository whose commits are listed.</param>
    /// <param name="history">The grid's commits, as the browse window reads them.</param>
    /// <param name="filter">The grid's filter, as upstream's grid starts from the stored one.</param>
    public LogViewModel(IDialogHost host, string repositoryPath, ICommitHistory history, RevisionFilter filter)
        : base(host)
    {
        _repositoryPath = repositoryPath;
        _filter = filter;
        Commits = new CommitListViewModel(history);
    }

    /// <summary>
    ///  The grid's commits.
    /// </summary>
    public CommitListViewModel Commits { get; }

    /// <summary>
    ///  The selected commits' files, each with its group; empty while none is selected or the groups are being read.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<CommitFile> Files { get; private set; } = [];

    /// <summary>
    ///  Upstream's <c>FormDiffLoad</c>: the grid loads. The window calls it when it opens.
    /// </summary>
    public Task LoadAsync() => GuardAsync(async () =>
    {
        await Commits.ApplyFilterAsync(_filter);
        await Commits.OpenAsync(_repositoryPath);
    });

    /// <summary>
    ///  The grid's selection changed: <paramref name="current"/> is selected now, <paramref name="added"/> was just added.
    /// </summary>
    public Task SelectionChangedAsync(IReadOnlyList<CommitRow> current, IReadOnlyList<CommitRow> added)
    {
        _selection.Update(current, added);
        return SelectAsync(_selection.Revisions);
    }

    /// <summary>
    ///  Upstream's <c>RevisionGridSelectionChanged</c>: the files of <paramref name="selected"/> (the latest selected
    ///  first, as upstream's <c>GetSelectedRevisions</c>); a later selection wins over this one.
    /// </summary>
    public Task SelectAsync(IReadOnlyList<CommitRow> selected) => GuardAsync(async () =>
    {
        int selection = ++_selections;
        Files = [];
        if (selected.Count == 0)
        {
            return;
        }

        IReadOnlyList<GitUIPluginInterfaces.GitRevision> revisions = [.. selected.Select(GridRevisions.Revision)];
        Func<ObjectId, string> describe = GridRevisions.Describer(Commits.VisibleRows.Select(item => item.Row));
        IReadOnlyList<DiffGroup> groups = await Task.Run(() =>
        {
            GitModule module = GitModules.Open(_repositoryPath);
            return DiffGroups.Calculate(module, revisions, module.GetCurrentCheckout(), describe);
        });
        if (selection == _selections)
        {
            Files =
            [
                .. groups.SelectMany(group => group.Files.Select(file =>
                    new CommitFile(file.Status, file.Path) { Group = group, DiffFile = file }))
            ];
        }
    });
}
