using Avalonia.Controls;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormChooseCommit</c>: a revision grid to pick one commit (OK, or a double-click), with "Go to commit..."
///  and links to the selected commit's parents. Closes with the chosen row, or null.
/// </summary>
public partial class ChooseCommitWindow : Window
{
    private readonly string _repositoryPath;
    private readonly string? _preselect;
    private readonly CommitListViewModel _commits;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ChooseCommitWindow()
        : this("", null, AppServices.Preferences)
    {
    }

    /// <param name="repositoryPath">The repository whose commits are listed.</param>
    /// <param name="preselect">The commit selected at first (upstream's <c>preselectCommit</c>), or null.</param>
    /// <param name="preferences">The grid's columns and dates, as the browse window shows them.</param>
    public ChooseCommitWindow(string repositoryPath, string? preselect, IAppPreferences preferences)
    {
        _repositoryPath = repositoryPath;
        _preselect = preselect;
        _commits = new CommitListViewModel(new GitCommitHistory(
            () => new CommitDateStyle(preferences.RelativeDate, preferences.ShowAuthorDate),
            () => preferences.RevisionSortOrder));
        InitializeComponent();
        UpstreamTranslation.Apply(this, "FormChooseCommit");
        RevisionGridColumns.ShowAsSettingsSay(this, revisionGrid, preferences, _commits);
        flowLayoutPanelParents.IsVisible = false;
        revisionGrid.SelectionChanged += (_, _) => ShowParents();
        revisionGrid.DoubleTapped += (_, _) => Choose();
        btnOK.Click += (_, _) => Choose();
        buttonGotoCommit.Click += (_, _) => Run(GoToCommitAsync);
        linkLabelParent.Click += (_, _) => SelectOrTell(linkLabelParent.Tag as string);
        linkLabelParent2.Click += (_, _) => SelectOrTell(linkLabelParent2.Tag as string);
        Opened += (_, _) => Run(LoadAsync);
    }

    /// <summary>
    ///  Completes when the commits are listed, for tests.
    /// </summary>
    public Task CommitsLoaded => _loaded.Task;

    private CommitRow? SelectedRow => (revisionGrid.SelectedItem as CommitListItem)?.Row;

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));

    // Upstream's OnLoad and its constructor's preselected commit.
    private async Task LoadAsync()
    {
        await _commits.OpenAsync(_repositoryPath);
        revisionGrid.ItemsSource = _commits.VisibleRows;
        string? hash = string.IsNullOrEmpty(_preselect)
            ? null
            : await Task.Run(() => GoToCommit.Resolve(GitModules.Open(_repositoryPath), _preselect)) is { IsZero: false } id
                ? id.ToString()
                : null;
        if (hash is null || !Select(hash))
        {
            revisionGrid.SelectedIndex = _commits.VisibleRows.Count > 0 ? 0 : -1;
        }

        _loaded.TrySetResult();
    }

    // Upstream's btnOK_Click and revisionGrid_DoubleClickRevision.
    private void Choose()
    {
        if (SelectedRow is { } row)
        {
            Close(row);
        }
    }

    // Upstream's revisionGrid_SelectionChanged: the selected commit's first two parents as links.
    private void ShowParents()
    {
        IReadOnlyList<string> parents = SelectedRow?.ParentHashes ?? [];
        flowLayoutPanelParents.IsVisible = parents.Count > 0;
        ShowParent(linkLabelParent, parents.Count > 0 ? parents[0] : null);
        ShowParent(linkLabelParent2, parents.Count > 1 ? parents[1] : null);

        static void ShowParent(HyperlinkButton link, string? hash)
        {
            link.IsVisible = hash is not null;
            link.Tag = hash;
            link.Content = hash is null ? null : ObjectId.Parse(hash).ToShortString();
        }
    }

    // Upstream's buttonGotoCommit_Click: the grid's GotoCommitExecute.
    private async Task GoToCommitAsync()
    {
        if (await new GoToCommitWindow(_repositoryPath).ShowDialog<ObjectId?>(this) is not { } commit)
        {
            return;
        }

        if (commit.IsZero)
        {
            ShowError(UpstreamTranslation.ByText(GoToCommit.NoRevisionFound));
        }
        else
        {
            SelectOrTell(commit.ToString());
        }
    }

    // Upstream's linkLabelParent_LinkClicked: the commit, or upstream's RevisionFilteredInGrid message.
    private void SelectOrTell(string? hash)
    {
        if (hash is not null && !Select(hash))
        {
            ShowError(string.Format(UpstreamTranslation.ByText(GoToCommit.RevisionNotVisible),
                hash[..Math.Min(hash.Length, 8)]));
        }
    }

    // Upstream's SetSelectedRevision: false when the grid does not show the commit.
    private bool Select(string hash)
    {
        if (_commits.IndexOfHash(hash) is not { } index)
        {
            return false;
        }

        revisionGrid.SelectedIndex = index;
        revisionGrid.ScrollIntoView(index);
        return true;
    }

    private void ShowError(string message) => _ = new ErrorWindow(message).ShowDialog(this);
}
