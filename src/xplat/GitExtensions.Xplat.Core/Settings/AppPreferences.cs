using GitCommands;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Repository;
using GitExtUtils.GitUI.Theming;
using GitUIPluginInterfaces;

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
    ///  Upstream <c>addnewlinetocommitmessagewhenmissing</c>: a commit message whose second line is text gets an empty line
    ///  after the subject (upstream's <c>CommitMessageManager.FormatCommitMessage</c>).
    /// </summary>
    bool EnsureCommitMessageSecondLineEmpty { get; set; }

    /// <summary>
    ///  Upstream's commit message settings (<c>CommitValidation*</c>): checks before a commit and formatting while typing.
    /// </summary>
    CommitValidationOptions CommitValidation { get; set; }

    /// <summary>
    ///  Upstream <c>CommitTemplates</c>: the user's commit templates, in upstream's JSON (<c>CommitTemplateItem</c>).
    /// </summary>
    string CommitTemplates { get; set; }

    /// <summary>
    ///  Upstream <c>RememberAmendCommitState</c>: the commit window keeps the Amend check box across closing and opening.
    /// </summary>
    bool RememberAmendCommitState { get; set; }

    /// <summary>
    ///  Upstream <c>showcommitandpush</c>: the commit window shows its Commit and push button.
    /// </summary>
    bool ShowCommitAndPush { get; set; }

    /// <summary>
    ///  Upstream <c>AlwaysShowCheckoutBranchDlg</c>: a checkout with local changes always asks what to do with them.
    /// </summary>
    bool AlwaysShowCheckoutBranchDlg { get; set; }

    /// <summary>
    ///  Upstream <c>UseDefaultCheckoutBranchAction</c>: a checkout with local changes does the last chosen action
    ///  (<see cref="CheckoutBranchAction"/>) without asking, unless <see cref="AlwaysShowCheckoutBranchDlg"/> is set.
    /// </summary>
    bool UseDefaultCheckoutBranchAction { get; set; }

    /// <summary>
    ///  Upstream <c>AutoNormaliseBranchName</c>: branch names are fixed up as upstream's normaliser does.
    /// </summary>
    bool AutoNormaliseBranchName { get; set; }

    /// <summary>
    ///  Upstream <c>AutoNormaliseSymbol</c>: what replaces a character git does not allow ("_", "-" or "").
    /// </summary>
    string AutoNormaliseSymbol { get; set; }

    /// <summary>
    ///  Upstream <c>CommitAndPushForcedWhenAmend</c>: Commit and push after an amend pushes with force-with-lease.
    /// </summary>
    bool CommitAndPushForcedWhenAmend { get; set; }

    /// <summary>
    ///  Upstream <c>relativedate</c>: the grid shows "3 days ago" instead of the full date.
    /// </summary>
    bool RelativeDate { get; set; }

    /// <summary>
    ///  Upstream <c>translation</c>: the language of upstream's translations (a file name in the Translation folder, such as
    ///  "German"); empty or "English" for upstream's English texts.
    /// </summary>
    string Translation { get; set; }

    /// <summary>
    ///  Upstream <c>showbuildstatusiconcolumn</c>: the grid's build status column shows the status symbol.
    /// </summary>
    bool ShowBuildStatusIconColumn { get; set; }

    /// <summary>
    ///  Upstream <c>showbuildstatustextcolumn</c>: the grid's build status column shows the build's description.
    /// </summary>
    bool ShowBuildStatusTextColumn { get; set; }

    /// <summary>
    ///  Upstream <c>showauthordate</c>: the grid shows the author date instead of the commit date.
    /// </summary>
    bool ShowAuthorDate { get; set; }

    /// <summary>
    ///  Upstream <c>StartWithRecentWorkingDir</c>: started without a repository, the app opens the one used last.
    /// </summary>
    bool StartWithRecentWorkingDir { get; set; }

    /// <summary>
    ///  Upstream <c>RecentWorkingDir</c>: the repository opened last.
    /// </summary>
    string? RecentWorkingDir { get; set; }

    /// <summary>
    ///  Upstream <c>defaultclonedestinationpath</c>: the folder the clone window suggests for a new clone.
    /// </summary>
    string DefaultCloneDestinationPath { get; set; }

    /// <summary>
    ///  Upstream <c>showgitstatusinbrowsetoolbar</c>: the Commit button shows the number of changed files.
    /// </summary>
    bool ShowGitStatusInBrowseToolbar { get; set; }

    /// <summary>
    ///  Upstream <c>ShowRevisionGridTooltips</c>: the grid shows a tooltip on each column of a commit.
    /// </summary>
    bool ShowRevisionGridTooltips { get; set; }

    /// <summary>
    ///  Upstream <c>MergeGraphLanesHavingCommonParent</c>: lanes of commits with a common parent join before it.
    /// </summary>
    bool MergeGraphLanesHavingCommonParent { get; set; }

    /// <summary>
    ///  Upstream <c>RenderGraphWithDiagonals</c>: lanes change columns along diagonals instead of curves.
    /// </summary>
    bool RenderGraphWithDiagonals { get; set; }

    /// <summary>
    ///  Upstream <c>StraightenGraphDiagonals</c>: lanes are moved to avoid short diagonals.
    /// </summary>
    bool StraightenGraphDiagonals { get; set; }

    /// <summary>
    ///  Upstream's blame settings: <c>IgnoreWhitespaceOnBlame</c>, <c>DetectCopyInFileOnBlame</c>,
    ///  <c>DetectCopyInAllOnBlame</c> and the <c>Blame.*</c> display settings.
    /// </summary>
    BlameOptions BlameOptions { get; set; }

    /// <summary>
    ///  Upstream <c>rememberIgnoreWhiteSpacePreference</c>: a new diff view keeps the whitespace choice made in this run
    ///  instead of starting from the saved default.
    /// </summary>
    bool RememberIgnoreWhiteSpacePreference { get; set; }

    /// <summary>
    ///  Upstream <c>RememberShowEntireFilePreference</c>, as <see cref="RememberIgnoreWhiteSpacePreference"/> for "show entire
    ///  file".
    /// </summary>
    bool RememberShowEntireFilePreference { get; set; }

    /// <summary>
    ///  Upstream <c>RememberNumberOfContextLines</c>: the number of context lines is stored as it changes; otherwise each
    ///  diff view starts with 3.
    /// </summary>
    bool RememberNumberOfContextLines { get; set; }

    /// <summary>
    ///  The options a new diff view starts with, by upstream's rules (FileViewer's constructor).
    /// </summary>
    DiffOptions InitialDiffOptions();

    /// <summary>
    ///  Records a diff view's change, as upstream's FileViewer does for the next view in this run.
    /// </summary>
    void SetDiffOptions(DiffOptions options);

    /// <summary>
    ///  Upstream's "Save current view settings as default": the current whitespace and entire file choices become the
    ///  saved defaults.
    /// </summary>
    void SaveDiffOptionsAsDefault();

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
    ///  Upstream <c>LinuxToolsDir</c>: where Git for Windows' sh is, put on PATH for git (Windows only).
    /// </summary>
    string LinuxToolsDir { get; set; }

    /// <summary>
    ///  Upstream <c>customhomedir</c>: the HOME git gets, or empty for upstream's choice.
    /// </summary>
    string CustomHomeDir { get; set; }

    /// <summary>
    ///  Upstream <c>userprofilehomedir</c>: git gets <c>USERPROFILE</c> as HOME (Windows).
    /// </summary>
    bool UserProfileHomeDir { get; set; }

    /// <summary>
    ///  Upstream <c>gitssh</c>: the ssh program git uses (<c>GIT_SSH</c>), or empty for git's own.
    /// </summary>
    string SshPath { get; set; }

    /// <summary>
    ///  Upstream <c>plink</c>: PuTTY's plink.
    /// </summary>
    string Plink { get; set; }

    /// <summary>
    ///  Upstream <c>puttygen</c>: PuTTY's key generator.
    /// </summary>
    string Puttygen { get; set; }

    /// <summary>
    ///  Upstream <c>pageant</c>: PuTTY's authentication agent.
    /// </summary>
    string Pageant { get; set; }

    /// <summary>
    ///  Upstream <c>autostartpageant</c>: start Pageant when a remote has a PuTTY key.
    /// </summary>
    bool AutoStartPageant { get; set; }

    /// <summary>
    ///  Upstream <c>RevisionSortOrder</c>: the grid's commit order (git's default, author date, topology).
    /// </summary>
    RevisionSortOrder RevisionSortOrder { get; set; }

    /// <summary>
    ///  Upstream <c>RefsSortBy</c>: the key git sorts the branches by.
    /// </summary>
    GitRefsSortBy RefsSortBy { get; set; }

    /// <summary>
    ///  Upstream <c>RefsSortOrder</c>: ascending or descending branch order.
    /// </summary>
    GitRefsSortOrder RefsSortOrder { get; set; }

    /// <summary>
    ///  Upstream <c>PrioritizedBranchNames</c>: regular expressions, separated by ';', of branches listed first.
    /// </summary>
    string PrioritizedBranchNames { get; set; }

    /// <summary>
    ///  Upstream <c>PrioritizedRemoteNames</c>: regular expressions, separated by ';', of remotes listed first.
    /// </summary>
    string PrioritizedRemoteNames { get; set; }

    /// <summary>
    ///  Upstream's <c>font</c>, <c>commitfont</c>, <c>difffont</c> or <c>monospacefont</c>; null when not stored (the app's
    ///  default font) or unreadable.
    /// </summary>
    FontSetting? GetFont(AppFont font);

    /// <summary>
    ///  Stores a font under upstream's key; null removes it, so the default applies again.
    /// </summary>
    void SetFont(AppFont font, FontSetting? value);

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

    public bool EnsureCommitMessageSecondLineEmpty
    {
        get => AppSettings.EnsureCommitMessageSecondLineEmpty;
        set => AppSettings.EnsureCommitMessageSecondLineEmpty = value;
    }

    public CommitValidationOptions CommitValidation
    {
        get => new(AppSettings.CommitValidationMaxCntCharsFirstLine, AppSettings.CommitValidationMaxCntCharsPerLine,
            AppSettings.CommitValidationSecondLineMustBeEmpty, AppSettings.CommitValidationIndentAfterFirstLine,
            AppSettings.CommitValidationAutoWrap, AppSettings.CommitValidationRegEx);
        set
        {
            AppSettings.CommitValidationMaxCntCharsFirstLine = value.MaxFirstLineLength;
            AppSettings.CommitValidationMaxCntCharsPerLine = value.MaxLineLength;
            AppSettings.CommitValidationSecondLineMustBeEmpty = value.SecondLineMustBeEmpty;
            AppSettings.CommitValidationIndentAfterFirstLine = value.IndentAfterFirstLine;
            AppSettings.CommitValidationAutoWrap = value.AutoWrap;
            AppSettings.CommitValidationRegEx = value.RegEx;
        }
    }

    public string CommitTemplates
    {
        get => AppSettings.CommitTemplates;
        set => AppSettings.CommitTemplates = value;
    }

    public bool RememberAmendCommitState
    {
        get => AppSettings.RememberAmendCommitState;
        set => AppSettings.RememberAmendCommitState = value;
    }

    public bool ShowCommitAndPush
    {
        get => AppSettings.ShowCommitAndPush;
        set => AppSettings.ShowCommitAndPush = value;
    }

    public bool AlwaysShowCheckoutBranchDlg
    {
        get => AppSettings.AlwaysShowCheckoutBranchDlg;
        set => AppSettings.AlwaysShowCheckoutBranchDlg = value;
    }

    public bool UseDefaultCheckoutBranchAction
    {
        get => AppSettings.UseDefaultCheckoutBranchAction;
        set => AppSettings.UseDefaultCheckoutBranchAction = value;
    }

    public bool AutoNormaliseBranchName
    {
        get => AppSettings.AutoNormaliseBranchName;
        set => AppSettings.AutoNormaliseBranchName = value;
    }

    public string AutoNormaliseSymbol
    {
        get => AppSettings.AutoNormaliseSymbol;
        set => AppSettings.AutoNormaliseSymbol = value;
    }

    public bool CommitAndPushForcedWhenAmend
    {
        get => AppSettings.CommitAndPushForcedWhenAmend;
        set => AppSettings.CommitAndPushForcedWhenAmend = value;
    }

    public bool RelativeDate
    {
        get => AppSettings.RelativeDate;
        set => AppSettings.RelativeDate = value;
    }

    public string Translation
    {
        get => AppSettings.Translation;
        set => AppSettings.Translation = value;
    }

    public bool ShowBuildStatusIconColumn
    {
        get => AppSettings.ShowBuildStatusIconColumn;
        set => AppSettings.ShowBuildStatusIconColumn = value;
    }

    public bool ShowBuildStatusTextColumn
    {
        get => AppSettings.ShowBuildStatusTextColumn;
        set => AppSettings.ShowBuildStatusTextColumn = value;
    }

    public bool ShowAuthorDate
    {
        get => AppSettings.ShowAuthorDate;
        set => AppSettings.ShowAuthorDate = value;
    }

    public bool StartWithRecentWorkingDir
    {
        get => AppSettings.StartWithRecentWorkingDir;
        set => AppSettings.StartWithRecentWorkingDir = value;
    }

    public string? RecentWorkingDir
    {
        get => AppSettings.RecentWorkingDir;
        set => AppSettings.RecentWorkingDir = value ?? "";
    }

    public string DefaultCloneDestinationPath
    {
        get => AppSettings.DefaultCloneDestinationPath;
        set => AppSettings.DefaultCloneDestinationPath = value;
    }

    public bool ShowGitStatusInBrowseToolbar
    {
        get => AppSettings.ShowGitStatusInBrowseToolbar;
        set => AppSettings.ShowGitStatusInBrowseToolbar = value;
    }

    public bool ShowRevisionGridTooltips
    {
        get => AppSettings.ShowRevisionGridTooltips.Value;
        set => AppSettings.ShowRevisionGridTooltips.Value = value;
    }

    public bool MergeGraphLanesHavingCommonParent
    {
        get => AppSettings.MergeGraphLanesHavingCommonParent.Value;
        set => AppSettings.MergeGraphLanesHavingCommonParent.Value = value;
    }

    public bool RenderGraphWithDiagonals
    {
        get => AppSettings.RenderGraphWithDiagonals.Value;
        set => AppSettings.RenderGraphWithDiagonals.Value = value;
    }

    public bool StraightenGraphDiagonals
    {
        get => AppSettings.StraightenGraphDiagonals.Value;
        set => AppSettings.StraightenGraphDiagonals.Value = value;
    }

    public BlameOptions BlameOptions
    {
        get => new(AppSettings.IgnoreWhitespaceOnBlame, AppSettings.DetectCopyInFileOnBlame,
            AppSettings.DetectCopyInAllOnBlame, AppSettings.BlameDisplayAuthorFirst, AppSettings.BlameShowAuthor,
            AppSettings.BlameShowAuthorDate, AppSettings.BlameShowAuthorTime, AppSettings.BlameShowLineNumbers,
            AppSettings.BlameShowOriginalFilePath);
        set
        {
            AppSettings.IgnoreWhitespaceOnBlame = value.IgnoreWhitespace;
            AppSettings.DetectCopyInFileOnBlame = value.DetectMoveInFile;
            AppSettings.DetectCopyInAllOnBlame = value.DetectMoveInAllFiles;
            AppSettings.BlameDisplayAuthorFirst = value.DisplayAuthorFirst;
            AppSettings.BlameShowAuthor = value.ShowAuthor;
            AppSettings.BlameShowAuthorDate = value.ShowAuthorDate;
            AppSettings.BlameShowAuthorTime = value.ShowAuthorTime;
            AppSettings.BlameShowLineNumbers = value.ShowLineNumbers;
            AppSettings.BlameShowOriginalFilePath = value.ShowOriginalFilePath;
        }
    }

    public bool RememberIgnoreWhiteSpacePreference
    {
        get => AppSettings.RememberIgnoreWhiteSpacePreference;
        set => AppSettings.RememberIgnoreWhiteSpacePreference = value;
    }

    public bool RememberShowEntireFilePreference
    {
        get => AppSettings.RememberShowEntireFilePreference;
        set => AppSettings.RememberShowEntireFilePreference = value;
    }

    public bool RememberNumberOfContextLines
    {
        get => AppSettings.RememberNumberOfContextLines;
        set => AppSettings.RememberNumberOfContextLines = value;
    }

    // Upstream's runtime settings: the value of this run, read back from the saved default unless remembered.
    public DiffOptions InitialDiffOptions()
        => new(AppSettings.IgnoreWhitespaceKind.GetValue(reload: !AppSettings.RememberIgnoreWhiteSpacePreference),
            AppSettings.NumberOfContextLines,
            AppSettings.ShowEntireFile.GetValue(reload: !AppSettings.RememberShowEntireFilePreference));

    public void SetDiffOptions(DiffOptions options)
    {
        AppSettings.IgnoreWhitespaceKind.Value = options.IgnoreWhitespace;
        AppSettings.ShowEntireFile.Value = options.ShowEntireFile;

        // Stored only when remembered (upstream's setter decides).
        AppSettings.NumberOfContextLines = options.ContextLines;
    }

    public void SaveDiffOptionsAsDefault()
    {
        AppSettings.IgnoreWhitespaceKind.Save();
        AppSettings.ShowEntireFile.Save();
        AppSettings.SaveSettings();
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

    public string LinuxToolsDir
    {
        get => AppSettings.LinuxToolsDir;
        set => AppSettings.LinuxToolsDir = value;
    }

    public string CustomHomeDir
    {
        get => AppSettings.CustomHomeDir;
        set => AppSettings.CustomHomeDir = value;
    }

    public bool UserProfileHomeDir
    {
        get => AppSettings.UserProfileHomeDir;
        set => AppSettings.UserProfileHomeDir = value;
    }

    public string SshPath
    {
        get => AppSettings.SshPath;
        set => AppSettings.SshPath = value;
    }

    public string Plink
    {
        get => AppSettings.Plink;
        set => AppSettings.Plink = value;
    }

    public string Puttygen
    {
        get => AppSettings.Puttygen;
        set => AppSettings.Puttygen = value;
    }

    public string Pageant
    {
        get => AppSettings.Pageant;
        set => AppSettings.Pageant = value;
    }

    public bool AutoStartPageant
    {
        get => AppSettings.AutoStartPageant;
        set => AppSettings.AutoStartPageant = value;
    }

    // A runtime setting upstream's page sets and saves at once.
    public RevisionSortOrder RevisionSortOrder
    {
        get => AppSettings.RevisionSortOrder.Value;
        set
        {
            AppSettings.RevisionSortOrder.Value = value;
            AppSettings.RevisionSortOrder.Save();
        }
    }

    public GitRefsSortBy RefsSortBy
    {
        get => AppSettings.RefsSortBy;
        set => AppSettings.RefsSortBy = value;
    }

    public GitRefsSortOrder RefsSortOrder
    {
        get => AppSettings.RefsSortOrder;
        set => AppSettings.RefsSortOrder = value;
    }

    public string PrioritizedBranchNames
    {
        get => AppSettings.PrioritizedBranchNames;
        set => AppSettings.PrioritizedBranchNames = value;
    }

    public string PrioritizedRemoteNames
    {
        get => AppSettings.PrioritizedRemoteNames;
        set => AppSettings.PrioritizedRemoteNames = value;
    }

    public FontSetting? GetFont(AppFont font) => FontSetting.Parse(AppSettings.GetString(FontSetting.KeyOf(font), null));

    public void SetFont(AppFont font, FontSetting? value)
        => AppSettings.SettingsContainer.SetString(FontSetting.KeyOf(font), value?.ToSettingString());

    public void Save() => AppSettings.SaveSettings();
}

/// <summary>
///  Keeps the preferences for the life of the process. Tests use it so they never write the user's settings file.
/// </summary>
public sealed class InMemoryAppPreferences : IAppPreferences
{
    public bool CloseCommitDialogAfterCommit { get; set; } = true;

    public bool CloseProcessDialog { get; set; }

    // Upstream's defaults.
    public bool EnsureCommitMessageSecondLineEmpty { get; set; } = true;

    public CommitValidationOptions CommitValidation { get; set; } = new();

    public string CommitTemplates { get; set; } = "";

    public bool RememberAmendCommitState { get; set; } = true;

    public bool ShowCommitAndPush { get; set; } = true;

    public bool AlwaysShowCheckoutBranchDlg { get; set; }

    public bool UseDefaultCheckoutBranchAction { get; set; }

    public bool AutoNormaliseBranchName { get; set; } = true;

    public string AutoNormaliseSymbol { get; set; } = "_";

    public bool CommitAndPushForcedWhenAmend { get; set; }

    public bool RelativeDate { get; set; } = true;

    public string Translation { get; set; } = "";

    public bool ShowBuildStatusIconColumn { get; set; } = true;

    public bool ShowBuildStatusTextColumn { get; set; }

    public bool ShowAuthorDate { get; set; } = true;

    public bool StartWithRecentWorkingDir { get; set; }

    public string? RecentWorkingDir { get; set; }

    public string DefaultCloneDestinationPath { get; set; } = "";

    public bool ShowGitStatusInBrowseToolbar { get; set; } = true;

    public bool ShowRevisionGridTooltips { get; set; } = true;

    public bool MergeGraphLanesHavingCommonParent { get; set; } = true;

    public bool RenderGraphWithDiagonals { get; set; } = true;

    public bool StraightenGraphDiagonals { get; set; } = true;

    public BlameOptions BlameOptions { get; set; } = new();

    public bool RememberIgnoreWhiteSpacePreference { get; set; } = true;

    public bool RememberShowEntireFilePreference { get; set; }

    public bool RememberNumberOfContextLines { get; set; }

    // The values of this run and the saved defaults, as upstream's runtime settings keep them.
    private DiffOptions _diffOptions = new();
    private DiffOptions _savedDiffOptions = new();

    public DiffOptions InitialDiffOptions()
    {
        _diffOptions = _diffOptions with
        {
            IgnoreWhitespace = RememberIgnoreWhiteSpacePreference
                ? _diffOptions.IgnoreWhitespace
                : _savedDiffOptions.IgnoreWhitespace,
            ShowEntireFile = RememberShowEntireFilePreference
                ? _diffOptions.ShowEntireFile
                : _savedDiffOptions.ShowEntireFile,
        };
        return _diffOptions with
        {
            ContextLines = RememberNumberOfContextLines
                ? _savedDiffOptions.ContextLines
                : DiffOptions.DefaultContextLines,
        };
    }

    public void SetDiffOptions(DiffOptions options)
    {
        _diffOptions = options;
        if (RememberNumberOfContextLines)
        {
            _savedDiffOptions = _savedDiffOptions with { ContextLines = options.ContextLines };
        }
    }

    public void SaveDiffOptionsAsDefault()
        => _savedDiffOptions = _savedDiffOptions with
        {
            IgnoreWhitespace = _diffOptions.IgnoreWhitespace, ShowEntireFile = _diffOptions.ShowEntireFile,
        };

    /// <summary>
    ///  Puts the diff view options back to upstream's defaults.
    /// </summary>
    public void ResetDiffOptions()
    {
        RememberIgnoreWhiteSpacePreference = true;
        RememberShowEntireFilePreference = false;
        RememberNumberOfContextLines = false;
        _diffOptions = new DiffOptions();
        _savedDiffOptions = new DiffOptions();
    }

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

    public string LinuxToolsDir { get; set; } = "";

    public string CustomHomeDir { get; set; } = "";

    public bool UserProfileHomeDir { get; set; }

    public string SshPath { get; set; } = "";

    public string Plink { get; set; } = "";

    public string Puttygen { get; set; } = "";

    public string Pageant { get; set; } = "";

    public bool AutoStartPageant { get; set; } = true;

    public RevisionSortOrder RevisionSortOrder { get; set; }

    public GitRefsSortBy RefsSortBy { get; set; }

    public GitRefsSortOrder RefsSortOrder { get; set; } = GitRefsSortOrder.Descending;

    public string PrioritizedBranchNames { get; set; } = DefaultPrioritizedBranchNames;

    public string PrioritizedRemoteNames { get; set; } = DefaultPrioritizedRemoteNames;

    private readonly Dictionary<AppFont, FontSetting> _fonts = [];

    public FontSetting? GetFont(AppFont font) => _fonts.GetValueOrDefault(font);

    public void SetFont(AppFont font, FontSetting? value)
    {
        if (value is null)
        {
            _fonts.Remove(font);
        }
        else
        {
            _fonts[font] = value;
        }
    }

    /// <summary>
    ///  Removes every stored font, so the defaults apply.
    /// </summary>
    public void ResetFonts() => _fonts.Clear();

    /// <summary>
    ///  Upstream's default <c>PrioritizedBranchNames</c>.
    /// </summary>
    public const string DefaultPrioritizedBranchNames = "main[^/]*|master[^/]*|release/.*";

    /// <summary>
    ///  Upstream's default <c>PrioritizedRemoteNames</c>.
    /// </summary>
    public const string DefaultPrioritizedRemoteNames = "origin|upstream";

    public int SaveCount { get; private set; }

    public void Save() => SaveCount++;
}
