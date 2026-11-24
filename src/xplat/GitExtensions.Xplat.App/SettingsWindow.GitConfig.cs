using System.ComponentModel;
using Avalonia.Controls;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.App;

// Upstream's GitConfigSettingsPage, bound to GitConfigPageViewModel: git's own settings at the level its header chooses;
// and upstream's GitConfigAdvancedSettingsPage, bound to GitConfigAdvancedPageViewModel.
// The tool path boxes' focus and typing events stay here; the Browse, Suggest and Configure buttons are commands.
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

        WireToolPath(MergeToolPathBox, diff: false);
        WireToolPath(DiffToolPathBox, diff: true);
        Opened += (_, _) => UiActions.Run(LoadGitConfigAsync, ex => ViewModel.ErrorText = ex.Message);
    }

    private void WireToolPath(TextBox path, bool diff)
    {
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

    // The Config page first: it finds out whether git can be run. The Advanced page (upstream's
    // GitConfigAdvancedSettingsPage) is read once the Config page's values are in, not after its tool lists, which git
    // can take many seconds to list.
    private async Task LoadGitConfigAsync()
    {
        TaskCompletionSource valuesShown = new(TaskCreationOptions.RunContinuationsAsynchronously);
        PropertyChangedEventHandler onChanged = (_, e) =>
        {
            if (e.PropertyName == nameof(GitConfigPageViewModel.IsLoaded) && GitConfigPage.IsLoaded)
            {
                valuesShown.TrySetResult();
            }
        };
        GitConfigPage.PropertyChanged += onChanged;
        try
        {
            Task config = GitConfigPage.LoadAsync(SourceHeaderOf(GitConfigTab).Level);
            await Task.WhenAny(config, valuesShown.Task);
            if (GitConfigPage.IsGitFound && GitConfigPage.IsLoaded && !ViewModel.GitConfigAdvancedPage.IsLoaded)
            {
                await ViewModel.GitConfigAdvancedPage.LoadAsync(SourceHeaderOf(GitConfigAdvancedTab).Level);
            }

            await config;
        }
        finally
        {
            GitConfigPage.PropertyChanged -= onChanged;
        }
    }

    private void ChangeGitConfigLevel(Extensibility.Settings.SettingLevel level)
        => UiActions.Run(() => GitConfigPage.ChangeLevelAsync(level), ex => ViewModel.ErrorText = ex.Message);

    private async Task SaveGitConfigAsync()
    {
        await GitConfigPage.SaveAsync();
        await ViewModel.GitConfigAdvancedPage.SaveAsync();
    }
}
