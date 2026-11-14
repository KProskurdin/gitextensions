using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormRenameBranch</c>: a new name for a local branch, normalised as Settings > Advanced
///  says, renamed with upstream's <c>Commands.RenameBranch</c>. Closes its window with true when git renamed the branch,
///  with false for the same name; a failed rename keeps it open.
/// </summary>
public sealed partial class RenameBranchViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormRenameBranch";

    private readonly string _oldName;
    private readonly IAppPreferences _preferences;
    private readonly Func<string, Task<bool>> _runGitWithOutput;

    /// <param name="branch">The branch to rename, also the first name.</param>
    /// <param name="preferences">Where "Auto normalise branch name" and its symbol are kept.</param>
    /// <param name="runGitWithOutput">Runs the rename with its output shown (upstream's FormProcess).</param>
    public RenameBranchViewModel(IDialogHost host, string branch, IAppPreferences preferences,
        Func<string, Task<bool>> runGitWithOutput)
        : base(host)
    {
        _oldName = branch;
        _preferences = preferences;
        _runGitWithOutput = runGitWithOutput;
        BranchName = branch;
    }

    [ObservableProperty]
    public partial string BranchName { get; set; }

    /// <summary>
    ///  Upstream's <c>BranchNameTextBox_Leave</c>: the name as the preferences normalise it.
    /// </summary>
    public void NormaliseName()
        => BranchName = BranchNames.Normalise(BranchName, _preferences.AutoNormaliseBranchName, _preferences.AutoNormaliseSymbol);

    // Upstream's OkClick: Enter leaves the box first, so the name is normalised; the same name closes as Cancel.
    [RelayCommand]
    private Task RenameAsync() => GuardAsync(async () =>
    {
        NormaliseName();
        string newName = BranchName;
        if (newName == _oldName)
        {
            Host.Close(false);
            return;
        }

        if (await _runGitWithOutput(GitCommands.Git.Commands.RenameBranch(_oldName, newName).ToString()))
        {
            Host.Close(true);
        }
    });
}
