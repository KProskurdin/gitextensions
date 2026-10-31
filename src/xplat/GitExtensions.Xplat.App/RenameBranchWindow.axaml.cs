using Avalonia.Controls;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormRenameBranch</c>: a new name for a local branch, normalised as Settings > Advanced says, renamed with
///  upstream's <c>Commands.RenameBranch</c>. Closes with true when git renamed the branch.
/// </summary>
public partial class RenameBranchWindow : Window
{
    private readonly string _oldName;
    private readonly IAppPreferences _preferences;
    private readonly Func<string, Task<bool>> _runGitWithOutput;

    public RenameBranchWindow()
        : this("", AppServices.Preferences, _ => Task.FromResult(false))
    {
    }

    /// <param name="branch">The branch to rename, also the box's first text.</param>
    /// <param name="preferences">Where "Auto normalise branch name" and its symbol are kept.</param>
    /// <param name="runGitWithOutput">Runs the rename with its output shown (upstream's FormProcess).</param>
    public RenameBranchWindow(string branch, IAppPreferences preferences, Func<string, Task<bool>> runGitWithOutput)
    {
        _oldName = branch;
        _preferences = preferences;
        _runGitWithOutput = runGitWithOutput;
        InitializeComponent();
        UpstreamTranslation.Apply(this, "FormRenameBranch");
        InitialFocus.Attach(this, () => BranchNameTextBox);
        BranchNameTextBox.Text = branch;
        BranchNameTextBox.LostFocus += (_, _) => NormaliseName();
        Ok.Click += (_, _) => UiActions.Run(RenameAsync, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
    }

    // Upstream's BranchNameTextBox_Leave.
    private void NormaliseName()
    {
        string text = BranchNameTextBox.Text ?? "";
        string normalised = BranchNames.Normalise(text, _preferences.AutoNormaliseBranchName, _preferences.AutoNormaliseSymbol);
        if (normalised != text)
        {
            int caret = BranchNameTextBox.CaretIndex;
            BranchNameTextBox.Text = normalised;
            BranchNameTextBox.CaretIndex = Math.Min(caret, normalised.Length);
        }
    }

    // Upstream's OkClick: Enter leaves the box first, so the name is normalised; the same name closes as Cancel, a failed
    // rename keeps the window open.
    private async Task RenameAsync()
    {
        NormaliseName();
        string newName = BranchNameTextBox.Text ?? "";
        if (newName == _oldName)
        {
            Close(false);
            return;
        }

        if (await _runGitWithOutput(GitCommands.Git.Commands.RenameBranch(_oldName, newName).ToString()))
        {
            Close(true);
        }
    }
}
