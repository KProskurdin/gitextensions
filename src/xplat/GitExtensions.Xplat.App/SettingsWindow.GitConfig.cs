using Avalonia.Controls;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.App;

// Upstream's GitConfigSettingsPage, bound to GitConfigPageViewModel: git's own settings at the level its header chooses.
// The pickers, the encodings window and the path boxes' focus events stay here.
public partial class SettingsWindow
{
    private GitConfigPageViewModel GitConfigPage => ViewModel.GitConfigPage;

    /// <summary>
    ///  True once the git config values are on show; saving before that would write blanks over them.
    /// </summary>
    public bool IsGitConfigLoaded => GitConfigPage.IsLoaded;

    /// <summary>
    ///  Completes when the page's drop-down lists are filled, for tests.
    /// </summary>
    public Task GitConfigListsLoaded => GitConfigPage.ListsLoaded;

    private void ShowGitConfigPage()
    {
        RadioButton[] autoCrlfRadios = [AutoCrlfTrueRadio, AutoCrlfInputRadio, AutoCrlfFalseRadio, AutoCrlfNotSetRadio];
        foreach ((RadioButton radio, AutoCrlfOption option) in autoCrlfRadios.Zip(GitConfigPageViewModel.AutoCrlfOptions))
        {
            radio.Content = option.Text;
        }

        BrowseCommitTemplateButton.Click +=
            (_, _) => Browse(() => BrowseFileAsync(CommitTemplateBox, "Path to commit template"));
        ConfigureEncodingButton.Click += (_, _) => Browse(ConfigureEncodingsAsync);
        WireToolRow(MergeToolBox, MergeToolPathBox, BrowseMergeToolButton, SuggestMergeToolButton, diff: false);
        WireToolRow(DiffToolBox, DiffToolPathBox, BrowseDiffToolButton, SuggestDiffToolButton, diff: true);
        Opened += (_, _) => UiActions.Run(LoadGitConfigAsync, ex => ViewModel.ErrorText = ex.Message);
    }

    private void WireToolRow(ComboBox tool, TextBox path, Button browse, Button suggest, bool diff)
    {
        // Upstream's btnDiffToolBrowse_Click and btnMergeToolBrowse_Click, which also suggest the command for the new path.
        browse.Click += (_, _) => Browse(async () =>
        {
            await BrowseFileAsync(path, tool.Text ?? "");
            GitConfigPage.SuggestToolCommand(diff);
        });
        suggest.Click += (_, _) => GitConfigPage.SuggestToolCommand(diff);

        // Upstream's LostFocus and TextChanged of the path boxes; only what the user types suggests a command.
        path.LostFocus += (_, _) => GitConfigPage.NormaliseToolPath(diff);
        path.TextChanged += (_, _) =>
        {
            if (path.IsFocused)
            {
                GitConfigPage.ToolPathTyped(diff);
            }
        };
    }

    // Upstream's ConfigureEncoding_Click.
    private async Task ConfigureEncodingsAsync()
    {
        if (await new AvailableEncodingsWindow().ShowDialog<bool>(this))
        {
            GitConfigPage.RefreshEncodings();
        }
    }

    private Task LoadGitConfigAsync() => GitConfigPage.LoadAsync(SourceHeaderOf(GitConfigTab).Level);

    private void ChangeGitConfigLevel(Extensibility.Settings.SettingLevel level)
        => UiActions.Run(() => GitConfigPage.ChangeLevelAsync(level), ex => ViewModel.ErrorText = ex.Message);

    private Task SaveGitConfigAsync() => GitConfigPage.SaveAsync();
}
