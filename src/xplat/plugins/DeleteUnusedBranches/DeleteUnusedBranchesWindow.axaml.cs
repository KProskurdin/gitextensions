using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.Ui;
using GitExtUtils;
using CheckBox = Avalonia.Controls.CheckBox;
using Control = Avalonia.Controls.Control;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using MessageBoxIcon = System.Windows.Forms.MessageBoxIcon;
using TextBox = Avalonia.Controls.TextBox;

namespace GitExtensions.Plugins.DeleteUnusedBranches;

/// <summary>
///  The new shell's version of upstream's <c>DeleteUnusedBranchesForm</c>: finds the branches merged into a branch (or all of
///  them) whose last commit is older than a number of days, optionally on a remote and filtered by a regex, and deletes the
///  ticked ones, with upstream's git commands and messages.
/// </summary>
public partial class DeleteUnusedBranchesWindow : Window
{
    private const string Category = "DeleteUnusedBranchesForm";
    private static string DeleteCaption => UpstreamTranslation.Text(Category, "_deleteCaption", "Delete");
    private static string SelectBranchesToDelete => UpstreamTranslation.Text(Category, "_selectBranchesToDelete", "Select branches to delete using checkboxes in '{0}' column.");
    private static string AreYouSureToDelete => UpstreamTranslation.Text(Category, "_areYouSureToDelete", "Are you sure to delete {0} selected branches?");
    private static string DangerousAction =>
        UpstreamTranslation.Text(Category, "_dangerousAction", "DANGEROUS ACTION!\nBranches will be deleted on the remote '{0}'. This can not be undone.\nAre you sure you want to continue?");
    private static string DeletingBranches => UpstreamTranslation.Text(Category, "_deletingBranches", "Deleting branches...");
    private static string DeletingUnmergedBranches =>
        UpstreamTranslation.Text(Category, "_deletingUnmergedBranches", "Deleting unmerged branches will result in dangling commits. Use with caution!");
    private static string ChooseBranchesToDelete =>
        UpstreamTranslation.Text(Category, "_chooseBranchesToDelete", "Choose branches to delete. Only branches that are fully merged in '{0}' will be deleted.");
    private static string PressToSearch => UpstreamTranslation.Text(Category, "_pressToSearch", "Press '{0}' to search for branches to delete.");
    private static string CancelText => UpstreamTranslation.Text(Category, "_cancel", "Cancel");
    private static string SearchBranches => UpstreamTranslation.Text(Category, "_searchBranches", "Search branches");
    private static string Loading => UpstreamTranslation.Text(Category, "_loading", "Loading...");
    private static string BranchesSelected => UpstreamTranslation.Text(Category, "_branchesSelected", "{0}/{1} branches selected.");

    private readonly IGitModule _module;
    private readonly IGitUICommands _commands;
    private readonly IGitPlugin _plugin;
    private readonly GitBranchOutputCommandParser _parser = new();
    private readonly ObservableCollection<BranchRow> _rows = [];
    private CancellationTokenSource? _refreshCancellation;
    private bool _updatingSelectAll;

    public DeleteUnusedBranchesWindow(DeleteUnusedBranchesFormSettings settings, IGitModule module,
        IGitUICommands commands, IGitPlugin plugin)
    {
        _module = module;
        _commands = commands;
        _plugin = plugin;
        InitializeComponent();
        UpstreamTranslation.Apply(this, Category);
        BranchList.ItemsSource = _rows;

        // Upstream's OnLoad. The values are set before the change handlers are added, so opening the window neither clears
        // the list nor warns about unmerged branches.
        MergedIntoBranchBox.Text = settings.MergedInBranch;
        OlderThanDaysBox.Value = settings.DaysOlderThan;
        IncludeRemoteBranches.IsChecked = settings.DeleteRemoteBranchesFromFlag;
        RemoteBox.Text = settings.RemoteName;
        useRegexFilter.IsChecked = settings.UseRegexToFilterBranchesFlag;
        RegexBox.Text = settings.RegexFilter;
        useRegexCaseInsensitive.IsChecked = settings.RegexCaseInsensitiveFlag;
        regexDoesNotMatch.IsChecked = settings.RegexInvertedFlag;
        includeUnmergedBranches.IsChecked = settings.IncludeUnmergedBranchesFlag;
        ShowInstructions();

        foreach (TextBox box in new[] { MergedIntoBranchBox, RemoteBox, RegexBox })
        {
            box.PropertyChanged += (_, e) =>
            {
                if (e.Property == TextBox.TextProperty)
                {
                    ClearResults();
                }
            };
        }

        IncludeRemoteBranches.IsCheckedChanged += (_, _) => ClearResults();
        useRegexFilter.IsCheckedChanged += (_, _) => ClearResults();
        OlderThanDaysBox.ValueChanged += (_, _) => ClearResults();
        includeUnmergedBranches.IsCheckedChanged += (_, _) => OnIncludeUnmergedChanged();
        SelectAllCheck.IsCheckedChanged += (_, _) => SelectAll();
        RefreshBtn.Click += (_, _) => _ = RefreshObsoleteBranchesAsync();
        Delete.Click += (_, _) => DeleteSelected();
        Cancel.Click += (_, _) => Close();
        buttonSettings.Click += (_, _) => OpenSettings();
        Opened += (_, _) => _ = RefreshObsoleteBranchesAsync();
        Closed += (_, _) => _refreshCancellation?.Cancel();
    }

    /// <summary>
    ///  True once a deletion was started, so the plugin asks the browse window to refresh.
    /// </summary>
    public bool HasDeletedBranch { get; private set; }

    /// <summary>
    ///  True while the branches are searched.
    /// </summary>
    public bool IsRefreshing => _refreshCancellation is not null;

    private static bool IsChecked(CheckBox box) => box.IsChecked == true;

    private void ShowInstructions()
    {
        InstructionText.Text = string.Format(ChooseBranchesToDelete, MergedIntoBranchBox.Text);
        StatusText.Text = string.Format(PressToSearch, RefreshBtn.Content);
    }

    private void ClearResults()
    {
        ShowInstructions();
        _rows.Clear();
        SetSelectAll(false);
    }

    private void OnIncludeUnmergedChanged()
    {
        ClearResults();
        if (IsChecked(includeUnmergedBranches))
        {
            MessageBoxes.Show(new WindowOwner(this), DeletingUnmergedBranches, DeleteCaption, MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void SelectAll()
    {
        if (_updatingSelectAll)
        {
            return;
        }

        foreach (BranchRow row in _rows)
        {
            row.Delete = IsChecked(SelectAllCheck);
        }

        StatusText.Text = DefaultStatus();
    }

    private void SetSelectAll(bool value)
    {
        _updatingSelectAll = true;
        SelectAllCheck.IsChecked = value;
        _updatingSelectAll = false;
    }

    private void OnRowChanged()
    {
        SetSelectAll(_rows.All(row => row.Delete));
        StatusText.Text = DefaultStatus();
    }

    private string DefaultStatus()
        => string.Format(BranchesSelected, _rows.Count(row => row.Delete), _rows.Count);

    // Upstream's RefreshObsoleteBranchesAsync: a second press while the search runs cancels it.
    private async Task RefreshObsoleteBranchesAsync()
    {
        if (_refreshCancellation is { } running)
        {
            await running.CancelAsync();
            SetRefreshing(null);
            return;
        }

        CancellationTokenSource cancellation = new();
        SetRefreshing(cancellation);
        string currentBranch = _module.GetSelectedBranch();
        RefreshContext context = new(
            IsChecked(IncludeRemoteBranches),
            IsChecked(includeUnmergedBranches),
            MergedIntoBranchBox.Text ?? "",
            RemoteBox.Text ?? "",
            IsChecked(useRegexFilter) ? RegexBox.Text : null,
            IsChecked(useRegexCaseInsensitive),
            IsChecked(regexDoesNotMatch),
            TimeSpan.FromDays((int)(OlderThanDaysBox.Value ?? 0)),
            cancellation.Token);

        List<Branch> branches;
        try
        {
            branches = await Task.Run(() => GetObsoleteBranches(context, currentBranch).ToList(), cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (cancellation.IsCancellationRequested)
        {
            return;
        }

        _rows.Clear();
        foreach (Branch branch in branches)
        {
            BranchRow row = new(branch);
            row.PropertyChanged += (_, _) => OnRowChanged();
            _rows.Add(row);
        }

        SetSelectAll(_rows.All(row => row.Delete));
        SetRefreshing(null);
    }

    private void SetRefreshing(CancellationTokenSource? cancellation)
    {
        _refreshCancellation = cancellation;
        RefreshBtn.Content = cancellation is null ? SearchBranches : CancelText;
        StatusText.Text = cancellation is null ? DefaultStatus() : Loading;
    }

    private IEnumerable<Branch> GetObsoleteBranches(RefreshContext context, string currentBranch)
    {
        DateTime oldBranchLimitDate = DateTime.Now - context.ObsolescenceDuration;
        foreach (string branchName in GetObsoleteBranchNames(context, currentBranch))
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            GitArgumentBuilder args = new("log")
            {
                "--pretty=\"format:%ci\n%an\n%s\"",
                "--max-count=1",
                branchName.Quote(),
                "--",
            };

            string[] commitLog = _module.GitExecutable.GetOutput(args).Split('\n');
            if (!DateTime.TryParse(commitLog[0], out DateTime commitDate))
            {
                Trace.WriteLine($"Failed to parse commit date from git log output: '{commitLog[0]}' from {commitLog}");
                commitDate = DateTime.MinValue;
            }

            string authorName = commitLog.Length > 1 ? commitLog[1] : string.Empty;
            string message = commitLog.Length > 2 ? commitLog[2] : string.Empty;

            yield return new Branch(branchName, commitDate, authorName, message, commitDate < oldBranchLimitDate);
        }
    }

    private IEnumerable<string> GetObsoleteBranchNames(RefreshContext context, string currentBranch)
    {
        RegexOptions options = context.RegexIgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
        bool regexMustMatch = !context.RegexDoesNotMatch;

        GitArgumentBuilder args = new("branch")
        {
            "--list",
            { context.RemoteBranches, "-r" },
            { !context.IncludeUnmerged, $"--merged {context.ReferenceBranch}" },
        };

        ExecutionResult result = _module.GitExecutable.Execute(args, throwOnErrorExit: false);
        if (!result.ExitedSuccessfully)
        {
            MessageBoxes.ShowError(new WindowOwner(this), result.AllOutput, $"git {args}");
            return [];
        }

        bool withoutRegexFilter = string.IsNullOrEmpty(context.RegexFilter);
        return _parser.GetBranchNames(result.StandardOutput, context.RemoteBranches)
            .Where(branchName => branchName != currentBranch && branchName != context.ReferenceBranch)
            .Where(branchName => (!context.RemoteBranches || branchName.StartsWith(context.RemoteRepositoryName + "/"))
                                 && (withoutRegexFilter
                                     || Regex.IsMatch(branchName, context.RegexFilter ?? "", options) == regexMustMatch));
    }

    // Upstream's Delete_Click.
    private void DeleteSelected()
    {
        WindowOwner owner = new(this);
        List<Branch> selected = [.. _rows.Where(row => row.Delete).Select(row => row.Branch)];
        if (selected.Count == 0)
        {
            // Upstream names the check box column, whose header it leaves empty.
            MessageBoxes.Show(string.Format(SelectBranchesToDelete, string.Empty), DeleteCaption, MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        if (MessageBoxes.Show(owner, string.Format(AreYouSureToDelete, selected.Count), DeleteCaption,
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != System.Windows.Forms.DialogResult.Yes)
        {
            return;
        }

        string remoteName = RemoteBox.Text ?? "";
        string remoteBranchPrefix = remoteName + "/";
        List<Branch> remoteBranches = IsChecked(IncludeRemoteBranches)
            ? [.. selected.Where(branch => branch.Name.StartsWith(remoteBranchPrefix))]
            : [];

        if (remoteBranches.Count > 0
            && MessageBoxes.Show(owner, string.Format(DangerousAction, remoteName), DeleteCaption,
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != System.Windows.Forms.DialogResult.Yes)
        {
            return;
        }

        HasDeletedBranch = true;
        List<Branch> localBranches = [.. selected.Except(remoteBranches)];
        bool force = IsChecked(includeUnmergedBranches);
        SetWorking(true);
        StatusText.Text = DeletingBranches;
        _ = DeleteAsync(remoteName, remoteBranchPrefix.Length, remoteBranches, localBranches, force);
    }

    private async Task DeleteAsync(string remoteName, int remotePrefixLength, List<Branch> remoteBranches,
        List<Branch> localBranches, bool force)
    {
        try
        {
            await Task.Run(() =>
            {
                // One by one, because one may fail.
                foreach (Branch remoteBranch in remoteBranches)
                {
                    GitArgumentBuilder args = new("push") { remoteName, $":{remoteBranch.Name[remotePrefixLength..]}" };
                    _module.GitExecutable.GetOutput(args);
                }

                foreach (Branch localBranch in localBranches)
                {
                    GitArgumentBuilder args = new("branch") { force ? "-D" : "-d", localBranch.Name };
                    _module.GitExecutable.GetOutput(args);
                }
            });
        }
        catch (Exception ex)
        {
            // Upstream reports the failure as a bug report, which the new shell does not have.
            MessageBoxes.ShowError(new WindowOwner(this), ex.Message);
        }

        _commands.RepoChangedNotifier.Notify();
        SetWorking(false);
        await RefreshObsoleteBranchesAsync();
    }

    private void SetWorking(bool working)
    {
        foreach (Control control in new Control[] { RefreshBtn, Delete, buttonSettings, BranchList, SelectAllCheck })
        {
            control.IsEnabled = !working;
        }
    }

    private void OpenSettings()
    {
        Close();
        _commands.StartSettingsDialog(_plugin);
    }

    private readonly record struct RefreshContext(
        bool RemoteBranches,
        bool IncludeUnmerged,
        string ReferenceBranch,
        string RemoteRepositoryName,
        string? RegexFilter,
        bool RegexIgnoreCase,
        bool RegexDoesNotMatch,
        TimeSpan ObsolescenceDuration,
        CancellationToken CancellationToken);
}
