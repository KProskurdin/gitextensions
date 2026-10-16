using Avalonia.Controls;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Scripts;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

// Upstream FormCommit's bottom panel: the column of commit buttons (flowCommitButtons, AmendPanel) and the message toolstrip
// (toolbarCommit) with its Options menu.
public partial class CommitWindow
{
    // Upstream FormCommit and GitUI TranslatedStrings texts.
    private const string CommitText = "_Commit";
    private const string CommitAndPushText = "Commit & _push";
    private const string CommitAndForcePushText = "Commit & force _push";
    private const string PushText = "_Push";
    private const string ResetSoftText = "Reset so_ft";
    private const string StashStagedText = "Stas_h staged changes";
    private const string ResetAllText = "_Reset all changes";
    private const string ResetUnstagedText = "Reset u_nstaged changes";
    private const string AmendCommitCaption = "Amend commit";
    private const string AmendResetSoftQuestion =
        "You are about to rewrite history by Soft Reset to the previous commit.\nOnly use Amend / Reset if the commit has not been published yet!\n\nDo you want to continue?";
    private const string ResetSoftRevision = "HEAD~1";

    // Upstream's GPG choices, by their index in the box.
    private const int GpgDefault = 0;
    private const int GpgNoSign = 1;
    private const int GpgSpecificKey = 3;

    // Upstream refreshes on activation; here at most every 2 seconds, as the browse window does.
    private static readonly TimeSpan _activationRefreshInterval = TimeSpan.FromSeconds(2);
    private DateTime _lastActivationRefresh;

    private void WireBottomPanel()
    {
        ShowCommitButtonImage();
        CommitAndPushButton.Content = ToolStripContent.Build("ArrowUp", CommitAndPushText);
        ResetSoftButton.Content = ToolStripContent.Build("ResetCurrentBranchToHere", ResetSoftText);
        StashStagedButton.Content = ToolStripContent.Build("Stash", StashStagedText);
        ResetAllButton.Content = ToolStripContent.Build("ResetWorkingDirChanges", ResetAllText);
        ResetUnstagedButton.Content = ToolStripContent.Build("ResetWorkingDirChanges", ResetUnstagedText);

        ResetSoftButton.Click += (_, _) => Run(ResetSoftAsync);
        StashStagedButton.Click += (_, _) => Run(() => _actions.StashStagedAsync(_repositoryPath));
        CreateBranchButton.Click += (_, _) => Run(CreateBranchAsync);
        AmendCheck.IsCheckedChanged += (_, _) => Run(ShowResetSoftAsync);

        ((MenuFlyout)OptionsButton.Flyout!).Opening += (_, _) => ShowOptions();
        CloseAfterEachCommitMenuItem.Click += (_, _) =>
            SavePreference(() => _preferences.CloseCommitDialogAfterCommit = CloseAfterEachCommitMenuItem.IsChecked);
        CloseAfterAllCommittedMenuItem.Click += (_, _) =>
            SavePreference(() => _preferences.CloseCommitDialogAfterLastCommit = CloseAfterAllCommittedMenuItem.IsChecked);
        RefreshOnFocusMenuItem.Click += (_, _) =>
            SavePreference(() => _preferences.RefreshCommitDialogOnFocus = RefreshOnFocusMenuItem.IsChecked);
        SelectStagedOnEnterMessageMenuItem.Click += (_, _) => SavePreference(() =>
            _preferences.CommitDialogSelectStagedOnEnterMessage = SelectStagedOnEnterMessageMenuItem.IsChecked);

        // Upstream's author label: set while an author is typed; clicking it clears the author.
        CommitAuthorBox.TextChanged += (_, _) =>
            AuthorLabelMenuItem.IsEnabled = AuthorLabelMenuItem.IsChecked = !string.IsNullOrEmpty(CommitAuthorBox.Text);
        AuthorLabelMenuItem.Click += (_, _) => CommitAuthorBox.Text = "";
        GpgSignComboBox.SelectionChanged += (_, _) =>
        {
            ShowCommitButtonImage();
            GpgKeyMenuItem.IsVisible = GpgSignComboBox.SelectedIndex == GpgSpecificKey;
        };

        Activated += (_, _) => RefreshOnActivation();
        CommitMessageBox.GotFocus += (_, _) => SelectStagedOnEnterMessage();
    }

    /// <summary>
    ///  Upstream's commit options from the Options menu, for the commit.
    /// </summary>
    private CommitExtras Extras => new(
        NoVerify: NoVerifyMenuItem.IsChecked,
        GpgSign: GpgSignComboBox.SelectedIndex switch
        {
            GpgDefault => null,
            GpgNoSign => false,
            _ => true,
        },
        GpgKeyId: GpgSignComboBox.SelectedIndex == GpgSpecificKey ? GpgKeyBox.Text ?? "" : "",
        ResetAuthor: AmendCheck.IsChecked == true && ResetAuthorCheck.IsChecked == true);

    private bool SignOff => SignOffMenuItem.IsChecked;

    // Upstream's gpgSignCommitChanged: the commit button shows a key while the commit is signed.
    private void ShowCommitButtonImage()
        => CommitButton.Content = ToolStripContent.Build(GpgSignComboBox.SelectedIndex > GpgDefault ? "Key" : "RepoStateClean",
            CommitText);

    // Upstream's UpdateButtonStates: the push button pushes with force after an amend (CommitAndPushForcedWhenAmend), and
    // only pushes when there is nothing to commit.
    private void ShowCommitAndPushText()
    {
        bool amend = AmendCheck.IsChecked == true;
        string text = amend && _preferences.CommitAndPushForcedWhenAmend ? CommitAndForcePushText
            : _repository.Changes.Count > 0 || amend ? CommitAndPushText
            : PushText;
        CommitAndPushButton.Content = ToolStripContent.Build("ArrowUp", text);
        ResetAllButton.IsEnabled = _repository.Changes.Count > 0;
    }

    private bool PushOnly => AmendCheck.IsChecked != true && _repository.Changes.Count == 0;

    private void ShowOptions()
    {
        CloseAfterEachCommitMenuItem.IsChecked = _preferences.CloseCommitDialogAfterCommit;
        CloseAfterAllCommittedMenuItem.IsChecked = _preferences.CloseCommitDialogAfterLastCommit;
        RefreshOnFocusMenuItem.IsChecked = _preferences.RefreshCommitDialogOnFocus;
        SelectStagedOnEnterMessageMenuItem.IsChecked = _preferences.CommitDialogSelectStagedOnEnterMessage;
    }

    private void SavePreference(Action change)
    {
        change();
        _preferences.Save();
    }

    // Upstream's Amend_CheckedChanged: "Reset soft" works only while amending and when HEAD has a parent.
    private async Task ShowResetSoftAsync()
    {
        ShowCommitAndPushText();
        if (AmendCheck.IsChecked != true)
        {
            ResetAuthorCheck.IsChecked = false;
            return;
        }

        ResetSoftButton.IsEnabled = await RepositoryScriptContext.ResolveCommitAsync(_repositoryPath, ResetSoftRevision) is not null;
    }

    // Upstream's ResetSoftClick: after the amend question, a soft reset to the previous commit; Amend is then unchecked.
    private async Task ResetSoftAsync()
    {
        if (!await ConfirmWindow.AskAsync(this, _preferences, Confirmation.Amend,
                UpstreamTranslation.ByText(AmendResetSoftQuestion), "Yes", UpstreamTranslation.ByText(AmendCommitCaption)))
        {
            return;
        }

        if (await RepositoryScriptContext.ResolveCommitAsync(_repositoryPath, ResetSoftRevision) is { } previous
            && await _actions.ResetAsync(_repositoryPath, previous, ResetMode.Soft))
        {
            AmendCheck.IsChecked = false;
            CommitMessageBox.Focus();
        }
    }

    // Upstream's createBranchToolStripButton_Click: the create branch dialog at HEAD.
    private async Task CreateBranchAsync()
        => await new CreateBranchWindow(_repositoryPath, _actions, _preferences).ShowDialog<bool>(this);

    // Upstream's refreshDialogOnFormFocus (RefreshArtificialCommitOnApplicationActivated).
    private void RefreshOnActivation()
    {
        if (!_preferences.RefreshCommitDialogOnFocus || _actions.IsBusy || _repository.IsLoading
            || DateTime.UtcNow - _lastActivationRefresh < _activationRefreshInterval)
        {
            return;
        }

        _lastActivationRefresh = DateTime.UtcNow;
        Run(_refresh);
    }

    // Upstream's Message_Enter (CommitDialogSelectStagedOnEnterMessage): a staged file is selected, so its diff shows.
    private void SelectStagedOnEnterMessage()
    {
        if (_preferences.CommitDialogSelectStagedOnEnterMessage && StagedList.SelectedItems is { Count: 0 }
            && StagedList.ItemCount > 0)
        {
            StagedList.SelectedIndex = 0;
        }
    }
}
