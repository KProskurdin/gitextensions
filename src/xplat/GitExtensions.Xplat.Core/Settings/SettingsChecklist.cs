using System.Drawing;
using GitCommands;
using GitCommands.DiffMergeTools;
using GitCommands.Git;
using GitExtensions.Extensibility.Configurations;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  A row of upstream's Checklist page, in its order. Upstream's Git Extensions install and shell extension rows check
///  the Windows installer's registration, which this app does not have.
/// </summary>
public enum ChecklistItem
{
    Git,
    UserName,
    MergeTool,
    DiffTool,
    LinuxTools,
    Ssh,
    Language,
    CredentialWinStore,
}

/// <summary>
///  How upstream shows a row: green when set, red when not, yellow when it works but is not recommended.
/// </summary>
public enum ChecklistState
{
    Set,
    Unset,
    NotRecommended,
}

public sealed record ChecklistEntry(ChecklistItem Item, ChecklistState State, string Text);

/// <summary>
///  What the checklist looks at, read by the caller (git, git config, the app's settings, the file system).
/// </summary>
public sealed record ChecklistFacts
{
    /// <summary>
    ///  The version of the git the app runs; null when no git was found.
    /// </summary>
    public GitVersion? GitVersion { get; init; }

    public string? UserName { get; init; }

    public string? UserEmail { get; init; }

    /// <summary>
    ///  The editor git uses (upstream's <c>CommonLogic.GetGlobalEditor</c>); it has no row, but makes the list invalid.
    /// </summary>
    public string? Editor { get; init; }

    public string? MergeTool { get; init; }

    public string? MergeToolCommand { get; init; }

    /// <summary>
    ///  True when git itself can start the merge tool (it is in <c>git mergetool --tool-help</c>), so it needs no
    ///  command; upstream knows only its Windows tools.
    /// </summary>
    public bool MergeToolKnownToGit { get; init; }

    public string? DiffTool { get; init; }

    public string? DiffToolCommand { get; init; }

    /// <summary>
    ///  True when git itself can start the diff tool.
    /// </summary>
    public bool DiffToolKnownToGit { get; init; }

    public string? Translation { get; init; }

    /// <summary>
    ///  True on Windows, where upstream also checks the linux tools, the ssh client and the credential helper.
    /// </summary>
    public bool IsWindows { get; init; }

    public bool LinuxToolsFound { get; init; }

    /// <summary>
    ///  The stored ssh client (upstream's <c>SshPath</c>): empty for OpenSSH.
    /// </summary>
    public string SshPath { get; init; } = "";

    public bool SshPathExists { get; init; }

    public bool PuttyFilesExist { get; init; }

    public string? CredentialHelper { get; init; }
}

/// <summary>
///  Upstream's <c>ChecklistSettingsPage.CheckSettings</c>: the rows it shows and whether every setting is right.
/// </summary>
public static class SettingsChecklist
{
    public const string Introduction =
        "The checklist below validates the basic settings needed for Git Extensions to work properly.";

    public const string RepairText = "Repair";
    public const string SaveAndRescanText = "Save and rescan";
    public const string InstantSaveNotice =
        "Changes on the selected page will be saved instantly. \r\nTherefore the Cancel button does NOT revert any changes made.";

    public const string CredentialWinStoreHelp =
        "https://github.com/gitextensions/gitextensions/wiki/Fix-GitCredentialWinStore-missing";

    // Upstream's texts.
    public const string WrongGitVersion = "Git found but version {0} is not supported. Upgrade to version {1} or later";
    public const string NotRecommendedGitVersion =
        "Git found but version {0} is older than recommended. Upgrade to version {1} or later";

    public const string GitVersionFound = "Git {0} is found on your computer.";
    public const string GitNotFound = "Git not found. To solve this problem you can set the correct path in settings.";
    public const string SshClientNotFound = "SSH client not found: {0}.";
    public const string OtherSshClient = "Other SSH client configured: {0}.";
    public const string PuttyNotFound =
        "PuTTY is configured as SSH client but cannot find plink.exe, puttygen.exe or pageant.exe.";

    public const string PuttyConfigured = "SSH client PuTTY is configured properly.";
    public const string OpenSshUsed =
        "Default SSH client, OpenSSH, will be used. (commandline window will appear on pull, push and clone operations)";

    public const string LinuxToolsNotFound =
        "Linux tools (sh) not found. To solve this problem you can set the correct path in settings.";

    public const string LinuxToolsFound = "Linux tools (sh) found on your computer.";
    public const string LanguageConfigured = "The configured language is {0}.";
    public const string NoLanguageConfigured = "There is no language configured for Git Extensions.";
    public const string NoEmailSet = "You need to configure a username and an email address.";
    public const string EmailSet = "A username and an email address are configured.";
    public const string MergeToolNeedsCommand =
        "{0} is configured as mergetool, this is a custom mergetool and needs a custom cmd to be configured.";

    public const string MergeToolConfigured = "There is a mergetool configured: {0}";
    public const string ConfigureMergeTool = "You need to configure merge tool in order to solve merge conflicts.";
    public const string AdviceDiffToolConfiguration =
        "You should configure a diff tool to show file diff in external program.";

    public const string DiffToolConfigured = "There is a difftool configured: {0}";
    public const string CredentialWinStoreDetected = "Obsolete git-credential-winstore.exe detected";

    private const string CredentialWinStore = "git-credential-winstore.exe";

    // Upstream's OtherColors, before its dark theme dimming.
    private static readonly Color _brightGreen = Color.FromArgb(128, 255, 128);
    private static readonly Color _brightYellow = Color.FromArgb(255, 255, 128);
    private static readonly Color _brightRed = Color.FromArgb(255, 128, 128);

    /// <summary>
    ///  The rows in upstream's order, each with its state and text.
    /// </summary>
    public static IReadOnlyList<ChecklistEntry> Check(ChecklistFacts facts)
    {
        List<ChecklistEntry> entries =
        [
            CheckGit(facts.GitVersion),
            Row(ChecklistItem.UserName,
                !string.IsNullOrEmpty(facts.UserName) && !string.IsNullOrEmpty(facts.UserEmail), EmailSet, NoEmailSet),
            CheckMergeTool(facts.MergeTool, facts.MergeToolKnownToGit ? facts.MergeTool : facts.MergeToolCommand),
            CheckDiffTool(facts.DiffTool, facts.DiffToolKnownToGit ? facts.DiffTool : facts.DiffToolCommand),
        ];

        if (facts.IsWindows)
        {
            entries.Add(Row(ChecklistItem.LinuxTools, facts.LinuxToolsFound, LinuxToolsFound, LinuxToolsNotFound));
            entries.Add(CheckSsh(facts));
        }

        entries.Add(Row(ChecklistItem.Language, !string.IsNullOrEmpty(facts.Translation),
            string.Format(LanguageConfigured, facts.Translation), NoLanguageConfigured));

        // Upstream shows this row only when the obsolete helper is configured.
        if (facts.IsWindows && facts.CredentialHelper?.Contains(CredentialWinStore, StringComparison.OrdinalIgnoreCase) == true)
        {
            entries.Add(new ChecklistEntry(ChecklistItem.CredentialWinStore, ChecklistState.Unset,
                CredentialWinStoreDetected));
        }

        return entries;
    }

    /// <summary>
    ///  True when every row is set and git has an editor, as upstream's <c>CheckSettings</c> returns.
    /// </summary>
    public static bool IsValid(ChecklistFacts facts, IReadOnlyList<ChecklistEntry> entries)
        => entries.All(entry => entry.State == ChecklistState.Set) && !string.IsNullOrEmpty(facts.Editor);

    /// <summary>
    ///  The command upstream's <see cref="DiffMergeToolConfigurationManager"/> finds for a tool: its <c>cmd</c> setting, or
    ///  the command upstream knows for the tool. <paramref name="config"/> gives the effective git config values.
    /// </summary>
    public static string ToolCommand(string tool, bool diff, Func<string, string?> config)
        => new DiffMergeToolConfigurationManager(() => new ConfigReader(config))
            .GetToolCommand(tool, diff ? DiffMergeToolType.Diff : DiffMergeToolType.Merge);

    /// <summary>
    ///  Upstream's row color for a state (<c>OtherColors</c>), dimmed toward the editor background in a dark theme as
    ///  upstream's <c>DimDarkModeColor</c> does.
    /// </summary>
    public static Color RowColor(ChecklistState state, Color? darkEditorBackground)
    {
        Color color = state switch
        {
            ChecklistState.Set => _brightGreen,
            ChecklistState.NotRecommended => _brightYellow,
            _ => _brightRed,
        };

        return darkEditorBackground is { } background ? Dim(color, background) : color;
    }

    /// <summary>
    ///  Black or white, whichever reads better on <paramref name="background"/>.
    /// </summary>
    public static Color TextColor(Color background)
        => (0.2126 * Linearize(background.R)) + (0.7152 * Linearize(background.G)) + (0.0722 * Linearize(background.B)) > 0.18
            ? Color.Black
            : Color.White;

    private static ChecklistEntry CheckGit(GitVersion? version)
    {
        if (version is null)
        {
            return new ChecklistEntry(ChecklistItem.Git, ChecklistState.Unset, GitNotFound);
        }

        if (version < GitVersion.LastSupportedVersion)
        {
            return new ChecklistEntry(ChecklistItem.Git, ChecklistState.Unset,
                string.Format(WrongGitVersion, version, GitVersion.LastRecommendedVersion));
        }

        return version < GitVersion.LastRecommendedVersion
            ? new ChecklistEntry(ChecklistItem.Git, ChecklistState.NotRecommended,
                string.Format(NotRecommendedGitVersion, version, GitVersion.LastRecommendedVersion))
            : new ChecklistEntry(ChecklistItem.Git, ChecklistState.Set, string.Format(GitVersionFound, version));
    }

    private static ChecklistEntry CheckMergeTool(string? tool, string? command)
    {
        if (string.IsNullOrEmpty(tool))
        {
            return new ChecklistEntry(ChecklistItem.MergeTool, ChecklistState.Unset, ConfigureMergeTool);
        }

        return string.IsNullOrWhiteSpace(command)
            ? new ChecklistEntry(ChecklistItem.MergeTool, ChecklistState.Unset, string.Format(MergeToolNeedsCommand, tool))
            : new ChecklistEntry(ChecklistItem.MergeTool, ChecklistState.Set, string.Format(MergeToolConfigured, tool));
    }

    private static ChecklistEntry CheckDiffTool(string? tool, string? command)
        => string.IsNullOrEmpty(tool) || string.IsNullOrWhiteSpace(command)
            ? new ChecklistEntry(ChecklistItem.DiffTool, ChecklistState.Unset, AdviceDiffToolConfiguration)
            : new ChecklistEntry(ChecklistItem.DiffTool, ChecklistState.Set, string.Format(DiffToolConfigured, tool));

    private static ChecklistEntry CheckSsh(ChecklistFacts facts)
    {
        if (SshClients.KindOf(facts.SshPath) == SshClientKind.Putty)
        {
            return Row(ChecklistItem.Ssh, facts.PuttyFilesExist, PuttyConfigured, PuttyNotFound);
        }

        if (!string.IsNullOrEmpty(facts.SshPath) && !facts.SshPathExists)
        {
            return new ChecklistEntry(ChecklistItem.Ssh, ChecklistState.Unset,
                string.Format(SshClientNotFound, facts.SshPath));
        }

        return new ChecklistEntry(ChecklistItem.Ssh, ChecklistState.Set,
            string.IsNullOrEmpty(facts.SshPath) ? OpenSshUsed : string.Format(OtherSshClient, facts.SshPath));
    }

    private static ChecklistEntry Row(ChecklistItem item, bool isSet, string setText, string unsetText)
        => new(item, isSet ? ChecklistState.Set : ChecklistState.Unset, isSet ? setText : unsetText);

    // Upstream's ColorHelper.DimColor: halfway to the background in linear light.
    private static Color Dim(Color color, Color background)
        => Color.FromArgb(color.A, Mix(color.R, background.R), Mix(color.G, background.G), Mix(color.B, background.B));

    private static byte Mix(byte channel, byte background)
    {
        double linear = (Linearize(channel) + Linearize(background)) * 0.5;
        double normalized = linear <= 0.0031308 ? 12.92 * linear : (1.055 * Math.Pow(linear, 1.0 / 2.4)) - 0.055;
        return (byte)Math.Round(Math.Clamp(normalized * 255.0, 0.0, 255.0));
    }

    private static double Linearize(byte channel)
    {
        double normalized = channel / 255.0;
        return normalized <= 0.04045 ? normalized / 12.92 : Math.Pow((normalized + 0.055) / 1.055, 2.4);
    }

    // The git config values upstream's tool manager reads; it never writes here.
    private sealed class ConfigReader(Func<string, string?> config) : IConfigValueStore
    {
        public string? GetValue(string setting) => config(setting);

        public void SetValue(string setting, string? value) => throw new NotSupportedException();
    }
}
