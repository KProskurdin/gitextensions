using Avalonia.Controls;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;
using GitExtUtils;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormMergeSubmodule</c> in the new shell (rules in <see cref="MergeSubmoduleDialog"/>): a conflicted
///  submodule's commits, opened in a new instance, a branch containing the local and remote commits checked out in it, or
///  its current commit staged. Closes with true once the submodule was staged.
/// </summary>
public partial class MergeSubmoduleWindow : Window
{
    private readonly string _fileName;
    private readonly GitModule _module;
    private readonly Func<Window, string, IReadOnlyList<ObjectId>, Task<bool>>? _checkoutBranch;
    private readonly IProcessLauncher _launcher;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ConflictData _item;

    /// <param name="fileName">The submodule's path in the repository.</param>
    /// <param name="checkoutBranch">Upstream's <c>StartCheckoutBranch</c> in the submodule's folder, limited to the branches
    ///  that contain the given commits; without it "Checkout Branch" is disabled.</param>
    /// <param name="launcher">Starts the new instance "Open submodule" opens.</param>
    public MergeSubmoduleWindow(string repositoryPath, string fileName,
        Func<Window, string, IReadOnlyList<ObjectId>, Task<bool>>? checkoutBranch = null,
        IProcessLauncher? launcher = null)
    {
        _fileName = fileName;
        _module = GitModules.Open(repositoryPath);
        _checkoutBranch = checkoutBranch;
        _launcher = launcher ?? new SystemProcessLauncher();
        InitializeComponent();
        SubmoduleText.Text = fileName;
        CheckoutBranchButton.IsEnabled = false;
        RefreshButton.Click += (_, _) => Run(ShowCurrentAsync);
        StageCurrentButton.Click += (_, _) => Run(async () =>
        {
            await StageAsync();
            Close(true);
        });
        // Upstream's btOpenSubmodule_Click: a new instance on the submodule.
        OpenSubmoduleButton.Click += (_, _) =>
        {
            if (Environment.ProcessPath is { } process)
            {
                string submodulePath = _module.GetSubmoduleFullPath(_fileName);
                _launcher.Start(process, [submodulePath], submodulePath);
            }
        };
        CheckoutBranchButton.Click += (_, _) => Run(CheckoutBranchAsync);
        Opened += (_, _) => Run(LoadAsync);
    }

    /// <summary>
    ///  Completes when the commits are shown, for tests.
    /// </summary>
    public Task SubmoduleLoaded => _loaded.Task;

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));

    // Upstream's FormMergeSubmodule_Load.
    private async Task LoadAsync()
    {
        _item = await Task.Run(() => _module.GetConflictAsync(_fileName));
        (BaseBox.Text, LocalBox.Text, RemoteBox.Text) = MergeSubmoduleDialog.Commits(_item, UpstreamTranslation.ByText);
        CheckoutBranchButton.IsEnabled = _checkoutBranch is not null && MergeSubmoduleDialog.CanCheckoutBranch(_item);
        await ShowCurrentAsync();
        _loaded.TrySetResult();
    }

    // Upstream's btRefresh_Click: the commit checked out in the submodule.
    private async Task ShowCurrentAsync()
        => CurrentBox.Text = await Task.Run(() =>
            _module.GetSubmodule(_fileName).GetCurrentCheckout() is { IsZero: false } id ? id.ToString() : "");

    // Upstream's btCheckoutBranch_Click: a branch containing the local and remote commits checked out in the submodule,
    // then the submodule staged.
    private async Task CheckoutBranchAsync()
    {
        if (_checkoutBranch is null
            || !await _checkoutBranch(this, _module.GetSubmoduleFullPath(_fileName),
                MergeSubmoduleDialog.BranchCommits(_item)))
        {
            return;
        }

        await StageAsync();
        Close(true);
    }

    // Upstream's StageSubmodule: git add, with git's output shown when it says something.
    private async Task StageAsync()
    {
        string output = await Task.Run(() =>
            _module.GitExecutable.GetOutput(new GitArgumentBuilder("add") { "--", _fileName.QuoteNE() }));
        if (!string.IsNullOrWhiteSpace(output))
        {
            await new ErrorWindow(
                string.Format(UpstreamTranslation.ByText(MergeSubmoduleDialog.StageFilename), _fileName)
                + Environment.NewLine + output).ShowDialog(this);
        }
    }
}
