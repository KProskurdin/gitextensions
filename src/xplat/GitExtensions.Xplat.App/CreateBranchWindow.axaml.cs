using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using GitExtUtils.GitUI.Theming;
using GitUIPluginInterfaces;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormCreateBranch</c> in the new shell (rules in <see cref="CreateBranchDialog"/>): the branch name
///  (proposed from the commit's refs or a folder), the commit with upstream's commit summary, checkout after create and
///  the orphan options. Closes with true once the branch is created.
/// </summary>
public partial class CreateBranchWindow : Window
{
    // CommitSummaryUserControl's label colors.
    private static readonly System.Drawing.Color _tagsBackColor = System.Drawing.Color.LightSteelBlue;
    private static readonly System.Drawing.Color _branchesBackColor = System.Drawing.Color.LightSalmon;

    private readonly string _repositoryPath;
    private readonly RepositoryOperationsViewModel _actions;
    private readonly IAppPreferences _preferences;
    private readonly string? _revision;
    private readonly string? _namePrefix;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CreateBranchDialog _dialog = new(hasCommits: true);
    private ObjectId _originalCheckout;

    /// <param name="revision">The commit to create the branch at (a hash or ref); HEAD when null or an artificial commit.</param>
    /// <param name="namePrefix">The proposed name, e.g. a left panel folder's path with a trailing slash.</param>
    public CreateBranchWindow(string repositoryPath, RepositoryOperationsViewModel actions, IAppPreferences preferences,
        string? revision = null, string? namePrefix = null)
    {
        _repositoryPath = repositoryPath;
        _actions = actions;
        _preferences = preferences;
        _revision = revision;
        _namePrefix = namePrefix;
        InitializeComponent();
        AuthorCaption.Text = ResourceManager.TranslatedStrings.Author + ":";
        DateCaption.Text = ResourceManager.TranslatedStrings.CommitDate + ":";

        BranchNameBox.LostFocus += (_, _) => NormaliseName();
        CommitHashBox.LostFocus += (_, _) => Run(() => SelectCommitAsync(CommitHashBox.Text?.Trim() ?? ""));
        CheckoutAfterCreateCheck.IsCheckedChanged += (_, _) => _dialog.CheckoutAfterCreate = CheckoutAfterCreateCheck.IsChecked == true;
        CreateOrphanCheck.IsCheckedChanged += (_, _) =>
        {
            _dialog.SetCreateOrphan(CreateOrphanCheck.IsChecked == true);
            ShowOptions();
        };
        ClearOrphanCheck.IsCheckedChanged += (_, _) => _dialog.ClearOrphan = ClearOrphanCheck.IsChecked == true;
        CreateButton.Click += (_, _) => Run(CreateAsync);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close(false);
            }
        };
        Opened += (_, _) => Run(LoadAsync);
    }

    /// <summary>
    ///  The dialog state, for tests.
    /// </summary>
    public CreateBranchDialog Dialog => _dialog;

    /// <summary>
    ///  Completes when the commit is read and shown, for tests.
    /// </summary>
    public Task BranchLoaded => _loaded.Task;

    /// <summary>
    ///  True when the created branch was checked out at another commit than before, which upstream follows with a
    ///  submodule update.
    /// </summary>
    public bool CheckedOutOtherCommit { get; private set; }

    private async Task LoadAsync()
    {
        GitModule module = GitModules.Open(_repositoryPath);
        (ObjectId current, ObjectId start, GitRevision? revision) = await Task.Run(() =>
        {
            ObjectId head = module.GetCurrentCheckout();
            ObjectId id = _revision is null ? default : module.RevParse(_revision);
            if (id.IsZero || id.IsArtificial)
            {
                id = head;
            }

            return (head, id, id.IsZero ? null : module.GetRevision(id, shortFormat: true, loadRefs: true));
        });
        _originalCheckout = current;
        _dialog = new CreateBranchDialog(hasCommits: !start.IsZero, _namePrefix);
        if (revision is not null)
        {
            _dialog.ProposeName(revision.Refs);
            await ShowCommitAsync(module, start, revision);
        }

        if (_dialog.IsOrphanOnly)
        {
            // Upstream's ConfigureForOrphanBranch: the label takes the commit picker's place.
            CreateAtLabel.Text = UpstreamTranslation.ByText(CreateBranchDialog.CreatingOrphanBranch);
            Grid.SetColumnSpan(CreateAtLabel, 2);
            CommitPicker.IsVisible = false;
            ShowSummary(null);
        }

        BranchNameBox.Text = _dialog.BranchName;
        CheckoutAfterCreateCheck.IsChecked = _dialog.CheckoutAfterCreate;
        CreateOrphanCheck.IsChecked = _dialog.CreateOrphan;
        ShowOptions();
        BranchNameBox.Focus();
        BranchNameBox.CaretIndex = BranchNameBox.Text?.Length ?? 0;
        _loaded.TrySetResult();
    }

    // Upstream's CommitPickerSmallControl.SetSelectedCommitHash: a hash git does not know is refused and the old one kept.
    private async Task SelectCommitAsync(string text)
    {
        if (text.Length == 0 || text == (_dialog.Revision?.ToShortString() ?? ""))
        {
            return;
        }

        GitModule module = GitModules.Open(_repositoryPath);
        ObjectId id = await Task.Run(() => module.RevParse(text));
        if (id.IsZero)
        {
            await new MessageBoxWindow(UpstreamTranslation.ByText(CreateBranchDialog.InvalidCommitHash),
                UpstreamTranslation.ByText(CreateBranchDialog.ErrorCaption), MessageBoxButtons.OK).ShowDialog(this);
            CommitHashBox.Text = _dialog.Revision?.ToShortString() ?? "";
            return;
        }

        GitRevision revision = await Task.Run(() => module.GetRevision(id, shortFormat: true, loadRefs: true));
        await ShowCommitAsync(module, id, revision);
    }

    private async Task ShowCommitAsync(GitModule module, ObjectId id, GitRevision revision)
    {
        _dialog.Revision = id;
        CommitHashBox.Text = id.ToShortString();
        ShowSummary(CreateBranchDialog.Summarize(revision));
        CommitCountText.Text = _originalCheckout.IsZero
            ? ""
            : await Task.Run(() => module.GetCommitCountString(_originalCheckout, id.ToString()));
    }

    private void ShowSummary(CommitSummary? summary)
    {
        string notAvailable = UpstreamTranslation.ByText(CreateBranchDialog.NotAvailable);
        SummaryGroup.Header = summary?.ShortId ?? UpstreamTranslation.ByText(CreateBranchDialog.NoRevision);
        AuthorText.Text = summary?.Author ?? "---";
        DateText.Text = summary?.Date ?? "---";
        MessageText.Text = summary?.Subject ?? "---";
        ShowRefs(TagsBorder, TagsText, summary?.Tags, _tagsBackColor);
        ShowRefs(BranchesBorder, BranchesText, summary?.Branches, _branchesBackColor);

        // Upstream colors and bolds the tags and branches when there are some, else shows "n/a".
        void ShowRefs(Border border, TextBlock text, string? refs, System.Drawing.Color color)
        {
            bool any = !string.IsNullOrEmpty(refs);
            text.Text = refs is null ? "---" : any ? refs : notAvailable;
            text.FontWeight = any ? FontWeight.Bold : FontWeight.Normal;
            System.Drawing.Color back = color.AdaptBackColor();
            border.Background = any ? new SolidColorBrush(Color.FromRgb(back.R, back.G, back.B)) : null;
            text.Foreground = any ? Brushes.Black : null;
        }
    }

    private void ShowOptions()
    {
        CommitPicker.IsEnabled = _dialog.CanChangeRevision;
        CheckoutAfterCreateCheck.IsEnabled = _dialog.CanChangeCheckout;
        CheckoutAfterCreateCheck.IsChecked = _dialog.CheckoutAfterCreate;
        CreateOrphanCheck.IsEnabled = _dialog.CanChangeOrphan;
        ClearOrphanCheck.IsEnabled = _dialog.CanClearOrphan;
    }

    // Upstream's BranchNameTextBox_Leave: the name normalised when "Auto normalise branch name" is on.
    private void NormaliseName()
    {
        string text = BranchNameBox.Text ?? "";
        string normalised = BranchNames.Normalise(text, _preferences.AutoNormaliseBranchName, _preferences.AutoNormaliseSymbol);
        if (normalised != text)
        {
            int caret = BranchNameBox.CaretIndex;
            BranchNameBox.Text = normalised;
            BranchNameBox.CaretIndex = Math.Min(caret, normalised.Length);
        }
    }

    // Upstream's cmdOk_Click: the checks, then the commands; the window stays open when git fails.
    private async Task CreateAsync()
    {
        NormaliseName();
        _dialog.BranchName = BranchNameBox.Text ?? "";
        GitModule module = GitModules.Open(_repositoryPath);
        string? error = await Task.Run(() => _dialog.Validate(module.CheckBranchFormat, UpstreamTranslation.ByText));
        if (error is not null)
        {
            await new MessageBoxWindow(error, Title ?? "", MessageBoxButtons.OK).ShowDialog(this);
            return;
        }

        if (await _actions.CreateBranchAsync(_repositoryPath, _dialog))
        {
            CheckedOutOtherCommit = _dialog.CheckoutAfterCreate && !_dialog.CreateOrphan
                                    && _dialog.Revision is { } start && start != _originalCheckout;
            Close(true);
        }
    }

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
}
