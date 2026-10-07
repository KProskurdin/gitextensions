using GitCommands;
using GitExtUtils.GitUI.Theming;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  App preferences the new shell edits. Each one is an upstream setting, read and written under the upstream key, so
///  the WinForms app and the new shell share them (PLAN.md section 11.1: upstream keys are read as they are).
/// </summary>
public interface IAppPreferences
{
    /// <summary>
    ///  Upstream <c>closecommitdialogaftercommit</c>: the commit window closes after a successful commit.
    /// </summary>
    bool CloseCommitDialogAfterCommit { get; set; }

    /// <summary>
    ///  Upstream <c>closeprocessdialog</c>: the output window of a remote operation closes by itself when git succeeds.
    /// </summary>
    bool CloseProcessDialog { get; set; }

    /// <summary>
    ///  Upstream <c>ShowCurrentBranchOnly</c>: the grid shows only the checked-out branch instead of every branch.
    /// </summary>
    bool ShowCurrentBranchOnly { get; set; }

    /// <summary>
    ///  Whether the app asks before <paramref name="confirmation"/>'s action: upstream's <c>DontConfirm...</c> settings,
    ///  inverted, and <c>ConfirmBranchCheckout</c> as it is.
    /// </summary>
    bool Asks(Confirmation confirmation);

    void SetAsks(Confirmation confirmation, bool ask);

    /// <summary>
    ///  Upstream <c>checkoutbranchaction</c>: what a checkout does with local changes unless the user chooses otherwise.
    /// </summary>
    LocalChangesAction CheckoutBranchAction { get; set; }

    /// <summary>
    ///  Upstream <c>SerializedHotkeys</c>: the hotkeys users changed, as upstream's XML (see <see cref="UpstreamHotkeys"/>).
    /// </summary>
    string? SerializedHotkeys { get; set; }

    /// <summary>
    ///  Upstream <c>uitheme_v2</c> and <c>uithemeisbuiltin_v2</c>: a built-in or user CSS theme, or
    ///  <see cref="ThemeId.WindowsAppColorModeId"/> to follow the operating system (see <see cref="IAppThemeService"/>).
    /// </summary>
    ThemeId Theme { get; set; }

    /// <summary>
    ///  Upstream <c>uithemevariations</c>, e.g. <see cref="GitExtUtils.GitUI.Theming.ThemeVariations.Colorblind"/>.
    /// </summary>
    IReadOnlyList<string> ThemeVariations { get; set; }

    /// <summary>
    ///  Upstream <c>history size</c> setting: how many recent repositories are kept.
    /// </summary>
    int RecentRepositoriesHistorySize { get; set; }

    /// <summary>
    ///  Upstream <c>gitcommand</c>: the git executable, or empty to look it up on PATH.
    /// </summary>
    string GitCommand { get; set; }

    /// <summary>
    ///  Writes the preferences to their store. Changes are kept in memory until then.
    /// </summary>
    void Save();
}

/// <summary>
///  Reads and writes the preferences in the upstream settings file through <see cref="AppSettings"/>.
/// </summary>
public sealed class SettingsAppPreferences : IAppPreferences
{
    public bool CloseCommitDialogAfterCommit
    {
        get => AppSettings.CloseCommitDialogAfterCommit;
        set => AppSettings.CloseCommitDialogAfterCommit = value;
    }

    public bool CloseProcessDialog
    {
        get => AppSettings.CloseProcessDialog;
        set => AppSettings.CloseProcessDialog = value;
    }

    public bool ShowCurrentBranchOnly
    {
        get => AppSettings.ShowCurrentBranchOnly.Value;
        set => AppSettings.ShowCurrentBranchOnly.Value = value;
    }

    public bool Asks(Confirmation confirmation) => confirmation switch
    {
        Confirmation.Amend => !AppSettings.DontConfirmAmend.Value,
        Confirmation.CommitWithoutBranch => !AppSettings.DontConfirmCommitIfNoBranch,
        Confirmation.Rebase => !AppSettings.DontConfirmRebase.Value,
        Confirmation.FetchAndPrune => !AppSettings.DontConfirmFetchAndPruneAll.Value,
        Confirmation.PushNewBranch => !AppSettings.DontConfirmPushNewBranch.Value,
        Confirmation.DeleteUnmergedBranch => !AppSettings.DontConfirmDeleteUnmergedBranch.Value,
        Confirmation.BranchCheckout => AppSettings.ConfirmBranchCheckout.Value,
        Confirmation.StashDrop => !AppSettings.DontConfirmStashDrop,
        Confirmation.SwitchWorktree => !AppSettings.DontConfirmSwitchWorktree.Value,
        _ => throw new ArgumentOutOfRangeException(nameof(confirmation)),
    };

    public void SetAsks(Confirmation confirmation, bool ask)
    {
        switch (confirmation)
        {
            case Confirmation.Amend:
                AppSettings.DontConfirmAmend.Value = !ask;
                break;
            case Confirmation.CommitWithoutBranch:
                AppSettings.DontConfirmCommitIfNoBranch = !ask;
                break;
            case Confirmation.Rebase:
                AppSettings.DontConfirmRebase.Value = !ask;
                break;
            case Confirmation.FetchAndPrune:
                AppSettings.DontConfirmFetchAndPruneAll.Value = !ask;
                break;
            case Confirmation.PushNewBranch:
                AppSettings.DontConfirmPushNewBranch.Value = !ask;
                break;
            case Confirmation.DeleteUnmergedBranch:
                AppSettings.DontConfirmDeleteUnmergedBranch.Value = !ask;
                break;
            case Confirmation.BranchCheckout:
                AppSettings.ConfirmBranchCheckout.Value = ask;
                break;
            case Confirmation.StashDrop:
                AppSettings.DontConfirmStashDrop = !ask;
                break;
            case Confirmation.SwitchWorktree:
                AppSettings.DontConfirmSwitchWorktree.Value = !ask;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(confirmation));
        }
    }

    public LocalChangesAction CheckoutBranchAction
    {
        get => AppSettings.CheckoutBranchAction;
        set => AppSettings.CheckoutBranchAction = value;
    }

    public string? SerializedHotkeys
    {
        get => AppSettings.SerializedHotkeys;
        set => AppSettings.SerializedHotkeys = value ?? "";
    }

    public ThemeId Theme
    {
        get => AppSettings.ThemeId;
        set => AppSettings.ThemeId = value;
    }

    public IReadOnlyList<string> ThemeVariations
    {
        get => AppSettings.ThemeVariations;
        set => AppSettings.ThemeVariations = [.. value];
    }

    public int RecentRepositoriesHistorySize
    {
        get => AppSettings.RecentRepositoriesHistorySize;
        set => AppSettings.RecentRepositoriesHistorySize = value;
    }

    public string GitCommand
    {
        get => AppSettings.GitCommandValue;
        set => AppSettings.GitCommandValue = value;
    }

    public void Save() => AppSettings.SaveSettings();
}

/// <summary>
///  Keeps the preferences for the life of the process. Tests use it so they never write the user's settings file.
/// </summary>
public sealed class InMemoryAppPreferences : IAppPreferences
{
    public bool CloseCommitDialogAfterCommit { get; set; } = true;

    public bool CloseProcessDialog { get; set; }

    public bool ShowCurrentBranchOnly { get; set; }

    private readonly Dictionary<Confirmation, bool> _asks = [];

    // Upstream's defaults: every question is asked except the left panel checkout.
    public bool Asks(Confirmation confirmation)
        => _asks.GetValueOrDefault(confirmation, confirmation != Confirmation.BranchCheckout);

    public void SetAsks(Confirmation confirmation, bool ask) => _asks[confirmation] = ask;

    /// <summary>
    ///  Puts every confirmation back to upstream's default.
    /// </summary>
    public void ResetConfirmations() => _asks.Clear();

    public LocalChangesAction CheckoutBranchAction { get; set; }

    public string? SerializedHotkeys { get; set; }

    public ThemeId Theme { get; set; } = ThemeId.DefaultLight;

    public IReadOnlyList<string> ThemeVariations { get; set; } = [];

    public int RecentRepositoriesHistorySize { get; set; } = 30;

    public string GitCommand { get; set; } = "";

    public int SaveCount { get; private set; }

    public void Save() => SaveCount++;
}
