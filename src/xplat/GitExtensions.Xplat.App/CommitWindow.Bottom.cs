using Avalonia.Controls;
using GitCommands;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.App;

// Upstream FormCommit's bottom panel: the column of commit buttons (flowCommitButtons, AmendPanel) and the message toolstrip
// (toolbarCommit) with its Options menu. Their state is CommitViewModel's; the buttons' upstream images and the module's
// reads stay here.
public partial class CommitWindow
{
    // Upstream FormCommit and GitUI TranslatedStrings texts.
    private const string ResetSoftText = "Reset so_ft";
    private const string StashStagedText = "Stas_h staged changes";
    private const string ResetAllText = "_Reset all changes";
    private const string ResetUnstagedText = "Reset u_nstaged changes";

    // Upstream's constructor: the check box only in a submodule, and no "Stash staged" for a git without stash --staged.
    private Task<(bool InSubmodule, bool SupportsStashStaged)> ReadModuleAsync()
        => Task.Run(() =>
        {
            GitModule module = GitModules.Open(_repositoryPath);
            return (module.SuperprojectModule is not null, module.GitVersion.SupportStashStaged);
        });

    // Upstream's DoCommit: the submodule's new commit is staged in its superproject.
    private Task StageInSuperprojectAsync()
        => Task.Run(() =>
        {
            GitModule module = GitModules.Open(_repositoryPath);
            if (module.SuperprojectModule is { } superproject && !string.IsNullOrWhiteSpace(module.SubmodulePath))
            {
                superproject.StageFile(module.SubmodulePath);
            }
        });

    private void WireBottomPanel()
    {
        ResetSoftButton.Content = ToolStripContent.Build("ResetCurrentBranchToHere", ResetSoftText);
        StashStagedButton.Content = ToolStripContent.Build("Stash", StashStagedText);
        ResetAllButton.Content = ToolStripContent.Build("ResetWorkingDirChanges", ResetAllText);
        ResetUnstagedButton.Content = ToolStripContent.Build("ResetWorkingDirChanges", ResetUnstagedText);

        // After a soft reset Amend is unchecked, and the message is next.
        ResetSoftButton.Click += (_, _) => CommitMessageBox.Focus();
        ((MenuFlyout)OptionsButton.Flyout!).Opening += (_, _) => ViewModel.RefreshOptions();
        Activated += (_, _) => ViewModel.RefreshOnActivation();
        CommitMessageBox.GotFocus += (_, _) => SelectStagedOnEnterMessage();
    }

    // The commit buttons' upstream image and text: a key while the commit is signed, and the push button's text.
    private void ShowCommitButtons()
    {
        CommitButton.Content = ToolStripContent.Build(ViewModel.CommitIcon, CommitViewModel.CommitText);
        CommitAndPushButton.Content = ToolStripContent.Build("ArrowUp", ViewModel.CommitAndPushContent);
    }

    // Upstream's Message_Enter (CommitDialogSelectStagedOnEnterMessage): a staged file is selected, so its diff shows.
    private void SelectStagedOnEnterMessage()
    {
        if (ViewModel.SelectStagedOnEnterMessage && StagedList.SelectedItems is { Count: 0 }
            && StagedList.Items.OfType<FileChange>().FirstOrDefault() is { } first)
        {
            StagedList.SelectedItem = first;
        }
    }
}
