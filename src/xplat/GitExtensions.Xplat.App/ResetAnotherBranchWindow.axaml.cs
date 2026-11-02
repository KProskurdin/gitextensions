using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Extensibility;
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
///  Upstream's <c>FormResetAnotherBranch</c> (rules in <see cref="ResetAnotherBranchDialog"/>): moves a local branch that is
///  not the current one to a commit, only as a fast-forward unless forced, then checks it out when asked. OK runs the
///  caller's git command (upstream's FormProcess) and closes with true.
/// </summary>
public partial class ResetAnotherBranchWindow : Window
{
    private readonly GitModule _module;
    private readonly string _revisionText;
    private readonly IAppPreferences _preferences;
    private readonly Func<Window, ArgumentString, Task<bool>> _runGit;
    private readonly Func<Window, string, Task> _checkout;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IReadOnlyList<IGitRef>? _localGitRefs;
    private ObjectId _revision;
    private string? _validatedBranch;
    private CancellationTokenSource? _validation;
    private Task _validated = Task.CompletedTask;

    public ResetAnotherBranchWindow()
        : this("", "HEAD", new InMemoryAppPreferences(), (_, _) => Task.FromResult(false), (_, _) => Task.CompletedTask)
    {
    }

    /// <param name="repositoryPath">The repository.</param>
    /// <param name="revision">The commit to reset a branch to.</param>
    /// <param name="preferences">Holds "Checkout branch after reset".</param>
    /// <param name="runGit">Runs a git command with its output shown; true when it succeeded.</param>
    /// <param name="checkout">Upstream's <c>StartCheckoutBranch</c> for the branch that was reset.</param>
    public ResetAnotherBranchWindow(string repositoryPath, string revision, IAppPreferences preferences,
        Func<Window, ArgumentString, Task<bool>> runGit, Func<Window, string, Task> checkout)
    {
        _module = GitModules.Open(repositoryPath);
        _revisionText = revision;
        _preferences = preferences;
        _runGit = runGit;
        _checkout = checkout;
        InitializeComponent();
        lblResetBranchWarning.Text = ResetAnotherBranchDialog.Warning.ReplaceLineEndings("\n");
        UpstreamTranslation.Apply(this, "FormResetAnotherBranch");
        InitialFocus.Attach(this, () => Branches);
        cbxCheckoutBranch.IsChecked = preferences.CheckoutOtherBranchAfterReset;
        cbxCheckoutBranch.IsCheckedChanged +=
            (_, _) => _preferences.CheckoutOtherBranchAfterReset = cbxCheckoutBranch.IsChecked == true;
        Branches.PropertyChanged += (_, e) =>
        {
            if (e.Property == ComboBox.TextProperty || e.Property == ComboBox.SelectedItemProperty)
            {
                Validate();
            }
        };
        Branches.DropDownClosed += (_, _) => Validate();
        Branches.GotFocus += (_, _) => Validate();
        Branches.LostFocus += (_, _) => Validate();
        Branches.KeyUp += OnBranchesKeyUp;
        ForceReset.IsCheckedChanged += (_, _) => Validate();
        Ok.Click += (_, _) => Run(ResetAsync);
        Cancel.Click += (_, _) => Close(false);
        Opened += (_, _) => Run(LoadAsync);
    }

    /// <summary>
    ///  Completes when the branches and the commit's summary are shown, for tests.
    /// </summary>
    public Task BranchesLoaded => _loaded.Task;

    /// <summary>
    ///  Completes when the last fast-forward check has finished, for tests.
    /// </summary>
    public Task Validated => _validated;

    // Upstream's FormResetAnotherBranch_Load and InitLocalBranchesWithoutCurrent; the list opens at once when no branch is
    // proposed (upstream's Application_Idle).
    private async Task LoadAsync()
    {
        try
        {
            (ObjectId revision, GitRevision? details, IReadOnlyList<IGitRef> candidates, string? proposed) =
                await Task.Run(() =>
                {
                    ObjectId id = _module.RevParse(_revisionText);
                    GitRevision? revision =
                        id.IsZero ? null : _module.GetRevision(id, shortFormat: true, loadRefs: true);
                    IReadOnlyList<IGitRef> refs = [.. revision?.Refs ?? []];
                    IReadOnlyList<IGitRef> candidates = ResetAnotherBranchDialog.Candidates(
                        _module.GetRefs(RefsFilter.Heads), _module.GetSelectedBranch(), id, refs);
                    return (id, revision, candidates, ResetAnotherBranchDialog.ProposedBranch(candidates, refs));
                });
            _revision = revision;
            _localGitRefs = candidates;
            Branches.ItemsSource = candidates.Select(gitRef => gitRef.Name).ToList();
            if (proposed is not null)
            {
                Branches.Text = proposed;
            }

            commitSummaryUserControl.Show(details is null ? null : CreateBranchDialog.Summarize(details));
            Validate();
            if (string.IsNullOrEmpty(Branches.Text) && candidates.Count > 0)
            {
                Dispatcher.UIThread.Post(() => Branches.IsDropDownOpen = true, DispatcherPriority.Background);
            }
        }
        finally
        {
            _loaded.TrySetResult();
        }
    }

    // Upstream's Branches_KeyUp: typing opens the list, keeping what was typed.
    private void OnBranchesKeyUp(object? sender, KeyEventArgs e)
    {
        if (Branches.IsDropDownOpen || e.Key is Key.LeftAlt or Key.RightAlt or Key.Enter or Key.Escape or Key.Tab)
        {
            return;
        }

        string? text = Branches.Text;
        Branches.IsDropDownOpen = true;
        if (Branches.Text != text)
        {
            Branches.Text = text;
        }
    }

    // Upstream's Validate: OK needs one of the listed branches, and is enabled when forced or when the branch is an
    // ancestor of the commit; a name that is not listed is marked once the list loses focus, a refused move on OK.
    private void Validate()
    {
        string branch = Branches.Text ?? "";
        bool force = ForceReset.IsChecked == true;
        if (_localGitRefs is null || (branch == _validatedBranch && !force))
        {
            return;
        }

        _validatedBranch = null;
        _validation?.Cancel();
        CancellationTokenSource validation = new();
        _validation = validation;

        IGitRef? gitRefToReset = _localGitRefs.FirstOrDefault(gitRef => gitRef.Name == branch);
        if (gitRefToReset is null && !Branches.IsKeyboardFocusWithin)
        {
            Branches.Background = Coral();
        }
        else
        {
            Branches.ClearValue(BackgroundProperty);
        }

        Ok.IsEnabled = gitRefToReset is not null && force;
        Ok.ClearValue(BackgroundProperty);
        if (gitRefToReset is null || force)
        {
            return;
        }

        _validatedBranch = branch;
        ArgumentString arguments = ResetAnotherBranchDialog.IsAncestorArguments(gitRefToReset, _revision);
        _validated = CheckAncestorAsync(arguments, validation.Token);
    }

    private async Task CheckAncestorAsync(ArgumentString arguments, CancellationToken cancellationToken)
    {
        try
        {
            ExecutionResult result = await Task.Run(() => _module.GitExecutable.ExecuteAsync(arguments,
                throwOnErrorExit: false, cancellationToken: cancellationToken), cancellationToken);
            await Dispatcher.UIThread.InvokeAsync(() => { });
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            Ok.IsEnabled = result.ExitedSuccessfully;
            if (!result.ExitedSuccessfully)
            {
                Ok.Background = Coral();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    // Upstream's Ok_Click: the branch moved with update-ref, then checked out when asked.
    private async Task ResetAsync()
    {
        string branch = Branches.Text ?? "";
        IGitRef? gitRefToReset = _localGitRefs?.FirstOrDefault(gitRef => gitRef.Name == branch);
        if (gitRefToReset is null)
        {
            await new MessageBoxWindow(
                string.Format(UpstreamTranslation.ByText(ResetAnotherBranchDialog.LocalRefInvalid), branch),
                UpstreamTranslation.ByText(ManageWorktreeDialog.Error), MessageBoxButtons.OK).ShowDialog(this);
            return;
        }

        if (!await _runGit(this, ResetAnotherBranchDialog.Arguments(gitRefToReset, _revision)))
        {
            return;
        }

        if (cbxCheckoutBranch.IsChecked == true)
        {
            await _checkout(this, gitRefToReset.Name);
        }

        Close(true);
    }

    // Upstream's Color.LightCoral.AdaptBackColor().
    private static SolidColorBrush Coral()
    {
        System.Drawing.Color coral = System.Drawing.Color.LightCoral.AdaptBackColor();
        return new SolidColorBrush(Color.FromRgb(coral.R, coral.G, coral.B));
    }

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
}
