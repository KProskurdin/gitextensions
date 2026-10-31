using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using GitUIPluginInterfaces;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  A parent of the merge to cherry pick, as upstream's list shows it: its number, message, author and date.
/// </summary>
public sealed record CherryPickParent(int Number, string Subject, string Author, string Date);

/// <summary>
///  Upstream's <c>FormCherryPick</c> (rules in <see cref="CherryPickDialog"/>): the commit to pick (another one chosen in
///  upstream's FormChooseCommit), the parent of a merge, whether to commit and to add the reference. Cherry pick runs the
///  caller's pick (upstream's FormProcess and conflict handling) and closes with true; the options are kept then.
/// </summary>
public partial class CherryPickWindow : Window
{
    private readonly string _repositoryPath;
    private readonly IAppPreferences _preferences;
    private readonly Func<Window, string, ArgumentString, Task>? _pick;
    private readonly GitModule _module;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private string? _revision;
    private GitRevision? _selected;
    private bool _isMerge;

    public CherryPickWindow()
        : this("", "HEAD", AppServices.Preferences, null)
    {
    }

    /// <param name="repositoryPath">The repository.</param>
    /// <param name="revision">The commit to cherry pick.</param>
    /// <param name="preferences">Upstream's two cherry pick options and the commit chooser's grid.</param>
    /// <param name="pick">Runs the cherry pick command and handles its conflicts as upstream does.</param>
    public CherryPickWindow(string repositoryPath, string revision, IAppPreferences preferences,
        Func<Window, string, ArgumentString, Task>? pick)
    {
        _repositoryPath = repositoryPath;
        _revision = revision;
        _preferences = preferences;
        _pick = pick;
        _module = GitModules.Open(repositoryPath);
        InitializeComponent();
        UpstreamTranslation.Apply(this, "FormCherryPick");
        InitialFocus.Attach(this, () => lvParentsList.IsVisible ? lvParentsList : cbxAutoCommit);
        lvParentsList.ItemTemplate = new FuncDataTemplate<CherryPickParent>((parent, _) => ParentRow(parent));

        // Upstream's LoadSettings.
        cbxAutoCommit.IsChecked = preferences.CommitAutomaticallyAfterCherryPick;
        cbxAddReference.IsChecked = preferences.AddCommitReferenceToCherryPick;
        btnChooseRevision.Click += (_, _) => Run(ChooseRevisionAsync);
        btnPick.Click += (_, _) => Run(PickAsync);
        btnAbort.Click += (_, _) => Close(false);
        Opened += (_, _) => Run(LoadAsync);
    }

    /// <summary>
    ///  Completes when the commit and its parents are shown, for tests.
    /// </summary>
    public Task RevisionLoaded => _loaded.Task;

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));

    private async Task LoadAsync()
    {
        await ShowRevisionAsync();
        _loaded.TrySetResult();
    }

    // Upstream's OnRevisionChanged: the summary, and for a merge its parents with the first selected.
    private async Task ShowRevisionAsync()
    {
        (GitRevision? selected, bool isMerge, IReadOnlyList<GitRevision> parents) = await Task.Run(() =>
        {
            if (_revision is null || _module.RevParse(_revision) is not { IsZero: false } id)
            {
                return ((GitRevision?)null, false, (IReadOnlyList<GitRevision>)[]);
            }

            GitRevision? revision = _module.GetRevision(id, shortFormat: true, loadRefs: true);
            bool merge = _module.IsMerge(id);
            return (revision, merge, merge ? _module.GetParentRevisions(id) : []);
        });
        _selected = selected;
        _isMerge = isMerge;
        commitSummaryUserControl1.Show(selected is null ? null : CreateBranchDialog.Summarize(selected));
        lblParents.IsVisible = lvParentsList.IsVisible = isMerge;
        lvParentsList.ItemsSource = parents
            .Select((parent, index) => new CherryPickParent(index + 1, parent.Subject, parent.Author ?? "",
                parent.CommitDate.ToShortDateString()))
            .ToList();
        lvParentsList.SelectedIndex = isMerge && parents.Count > 0 ? 0 : -1;
    }

    // Upstream's btnChooseRevision_Click.
    private async Task ChooseRevisionAsync()
    {
        if (await new ChooseCommitWindow(_repositoryPath, _selected?.Guid, _preferences).ShowDialog<CommitRow?>(this)
            is { } row)
        {
            _revision = row.Hash;
        }

        await ShowRevisionAsync();
    }

    // Upstream's btnPick_Click and SaveSettings.
    private async Task PickAsync()
    {
        int? parent = null;
        if (_isMerge)
        {
            if (lvParentsList.SelectedItem is not CherryPickParent chosen)
            {
                await new MessageBoxWindow(UpstreamTranslation.ByText(CherryPickDialog.NoneParentSelected),
                    UpstreamTranslation.ByText(ManageWorktreeDialog.Error), MessageBoxButtons.OK).ShowDialog(this);
                return;
            }

            parent = chosen.Number;
        }

        if (_selected is null)
        {
            return;
        }

        bool autoCommit = cbxAutoCommit.IsChecked == true;
        bool addReference = cbxAddReference.IsChecked == true;
        if (_pick is not null)
        {
            await _pick(this, _selected.Guid,
                CherryPickDialog.Arguments(_selected.ObjectId, autoCommit, parent, addReference));
        }

        _preferences.CommitAutomaticallyAfterCherryPick = autoCommit;
        _preferences.AddCommitReferenceToCherryPick = addReference;
        Close(true);
    }

    // Upstream's list columns: No., Message, Author, Date (shared with the revert window).
    internal static Control ParentRow(CherryPickParent? parent)
    {
        Grid row = new() { ColumnDefinitions = new ColumnDefinitions("40,*,120,90") };
        if (parent is null)
        {
            return row;
        }

        string[] texts = [parent.Number.ToString(System.Globalization.CultureInfo.CurrentCulture), parent.Subject,
            parent.Author, parent.Date];
        for (int column = 0; column < texts.Length; column++)
        {
            TextBlock text = new()
            {
                Text = texts[column],
                TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(text, column);
            row.Children.Add(text);
        }

        return row;
    }
}
