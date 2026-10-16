using GitCommands;
using GitCommands.Git;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Settings;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The repository's merge settings upstream's merge dialog starts from and keeps: <c>NoFastForwardMerge</c> (detached
///  settings) and <c>DetailedSettings.AddMergeLogMessages</c> and <c>MergeLogMessagesCount</c>, all in the repository's
///  effective settings.
/// </summary>
public sealed record MergeRepositorySettings(bool NoFastForward, bool AddLogMessages, int LogMessagesCount)
{
    public static MergeRepositorySettings Load(GitModule module)
    {
        SettingsSource settings = module.GetEffectiveSettings();
        return new MergeRepositorySettings(settings.Detached().NoFastForwardMerge,
            DetailedSettings.AddMergeLogMessages.ValueOrDefault(settings),
            DetailedSettings.MergeLogMessagesCount.ValueOrDefault(settings));
    }

    public void Save(GitModule module)
    {
        SettingsSource settings = module.GetEffectiveSettings();
        settings.Detached().NoFastForwardMerge = NoFastForward;
        DetailedSettings.AddMergeLogMessages[settings] = AddLogMessages;
        DetailedSettings.MergeLogMessagesCount[settings] = LogMessagesCount;
    }
}

/// <summary>
///  Where the merge dialog's repository settings are read and kept (see <see cref="MergeRepositorySettings"/>).
/// </summary>
public interface IMergeSettingsStore
{
    MergeRepositorySettings Load(GitModule module);

    void Save(GitModule module, MergeRepositorySettings settings);
}

/// <summary>
///  Upstream's settings, which fall through to the user's global settings when the repository sets none.
/// </summary>
public sealed class UpstreamMergeSettingsStore : IMergeSettingsStore
{
    public MergeRepositorySettings Load(GitModule module) => MergeRepositorySettings.Load(module);

    public void Save(GitModule module, MergeRepositorySettings settings) => settings.Save(module);
}

/// <summary>
///  Merge settings kept in memory, for tests.
/// </summary>
public sealed class InMemoryMergeSettingsStore : IMergeSettingsStore
{
    public MergeRepositorySettings Settings { get; set; } = new(NoFastForward: false, AddLogMessages: false, LogMessagesCount: 20);

    public MergeRepositorySettings Load(GitModule module) => Settings;

    public void Save(GitModule module, MergeRepositorySettings settings) => Settings = settings;
}

/// <summary>
///  Upstream's <c>FormMergeBranch</c> without its window: the branch to merge into the current one, fast-forward or a merge
///  commit, no commit, the advanced options and their rules, and the merge command.
/// </summary>
public sealed class MergeBranchDialog
{
    // Upstream's text.
    public const string HoverShowImageText = "Hover to see scenario when fast forward is possible.";

    private bool _noFastForward;
    private bool _advanced;

    /// <param name="noCommit">Upstream's <c>DontCommitMerge</c>.</param>
    /// <param name="advanced">Upstream's <c>AlwaysShowAdvOpt</c>.</param>
    public MergeBranchDialog(MergeRepositorySettings settings, bool noCommit, bool advanced)
    {
        _noFastForward = settings.NoFastForward;
        AddLogMessages = settings.AddLogMessages;
        LogMessagesCount = settings.LogMessagesCount;
        NoCommit = noCommit;
        _advanced = advanced;
    }

    public string Branch { get; set; } = "";

    /// <summary>
    ///  "Always create a new merge commit"; when false, upstream's "Keep a single branch line if possible (fast forward)".
    /// </summary>
    public bool NoFastForward
    {
        get => _noFastForward;
        set
        {
            _noFastForward = value;

            // Upstream's noFastForward_CheckedChanged: a squash makes no merge commit.
            if (value)
            {
                Squash = false;
            }
        }
    }

    public bool NoCommit { get; set; }

    /// <summary>
    ///  "Show advanced options"; hiding them clears them, as upstream's <c>advanced_CheckedChanged</c>.
    /// </summary>
    public bool Advanced
    {
        get => _advanced;
        set
        {
            _advanced = value;
            if (!value)
            {
                UseStrategy = false;
                Strategy = "";
                Squash = false;
                AllowUnrelatedHistories = false;
                AddMergeMessage = false;
            }
        }
    }

    public bool UseStrategy { get; set; }

    public string Strategy { get; set; } = "";

    public bool Squash { get; set; }

    public bool CanSquash => !NoFastForward;

    public bool AllowUnrelatedHistories { get; set; }

    public bool AddLogMessages { get; set; }

    public int LogMessagesCount { get; set; }

    public bool AddMergeMessage { get; set; }

    public string MergeMessage { get; set; } = "";

    /// <summary>
    ///  The settings to keep after the merge, as upstream's <c>OkClick</c> and change handlers store them.
    /// </summary>
    public MergeRepositorySettings RepositorySettings => new(NoFastForward, AddLogMessages, LogMessagesCount);

    /// <summary>
    ///  Writes the specified merge message to upstream's merge message file (.git/MERGE_MSG) and returns its path.
    /// </summary>
    public static async Task<string> WriteMergeMessageAsync(GitModule module, string message)
    {
        // As upstream's OkClick, through its CommitMessageManager (the owner control is only used for its dialogs).
        CommitMessageManager manager = new(new System.Windows.Forms.Control(), module.WorkingDirGitDir,
            module.CommitEncoding);
        await manager.WriteCommitMessageToFileAsync(message, CommitMessageType.Merge, usingCommitTemplate: false,
            ensureCommitMessageSecondLineEmpty: false);
        return manager.MergeMessagePath;
    }

    /// <summary>
    ///  Upstream's <c>Commands.MergeBranch</c> for the choices; <paramref name="mergeMessagePath"/> is the file holding the
    ///  specified merge message.
    /// </summary>
    public ArgumentString Arguments(string? mergeMessagePath, Func<string?, string?> getPathForGitExecution)
        => Commands.MergeBranch(Branch.Trim(), allowFastForward: !NoFastForward, Squash && CanSquash, NoCommit,
            UseStrategy ? Strategy.Trim() : "", AllowUnrelatedHistories,
            AddMergeMessage ? mergeMessagePath : null, getPathForGitExecution,
            AddLogMessages ? LogMessagesCount : null);
}
