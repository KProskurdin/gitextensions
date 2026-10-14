using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using GitCommands.Git;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class SettingsChecklistTests
{
    private static readonly ChecklistFacts _good = new()
    {
        GitVersion = GitVersion.LastRecommendedVersion,
        UserName = "Test",
        UserEmail = "test@example.com",
        Editor = "vi",
        MergeTool = "custom",
        MergeToolCommand = "custom \"$MERGED\"",
        DiffTool = "custom",
        DiffToolCommand = "custom \"$LOCAL\" \"$REMOTE\"",
        Translation = "English",
    };

    [Test]
    public void The_texts_are_upstreams()
    {
        string page = UpstreamSource("Pages", "ChecklistSettingsPage.cs");
        string designer = UpstreamSource("Pages", "ChecklistSettingsPage.Designer.cs");
        string form = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "app", "GitUI", "CommandsDialogs",
            "FormSettings.Designer.cs"));
        foreach (FieldInfo field in typeof(SettingsChecklist).GetFields(BindingFlags.Public | BindingFlags.Static)
                     .Where(field => field.IsLiteral && field.Name is not nameof(SettingsChecklist.Introduction)
                         and not nameof(SettingsChecklist.InstantSaveNotice)
                         and not nameof(SettingsChecklist.RepairText)
                         and not nameof(SettingsChecklist.SaveAndRescanText)))
        {
            page.Should().Contain($"\"{field.GetRawConstantValue()}\"", field.Name);
        }

        Unwrapped(designer).Should().Contain($"label11.Text = \"{SettingsChecklist.Introduction}\"")
            .And.Contain($"GitFound_Fix.Text = \"{SettingsChecklist.RepairText}\"")
            .And.Contain($"Rescan.Text = \"{SettingsChecklist.SaveAndRescanText}\"");
        Unwrapped(form).Should()
            .Contain($"labelInstantSaveNotice.Text = \"{SettingsChecklist.InstantSaveNotice.Replace("\r\n", "\\r\\n")}\"");
    }

    [Test]
    public void Check_should_list_upstreams_rows_set_when_everything_is_configured()
    {
        IReadOnlyList<ChecklistEntry> entries = SettingsChecklist.Check(_good);

        entries.Select(entry => entry.Item).Should().Equal(ChecklistItem.Git, ChecklistItem.UserName,
            ChecklistItem.MergeTool, ChecklistItem.DiffTool, ChecklistItem.Language);
        entries.Should().OnlyContain(entry => entry.State == ChecklistState.Set);
        entries.Select(entry => entry.Text).Should().Equal($"Git {GitVersion.LastRecommendedVersion} is found on your computer.",
            "A username and an email address are configured.", "There is a mergetool configured: custom",
            "There is a difftool configured: custom", "The configured language is English.");
        SettingsChecklist.IsValid(_good, entries).Should().BeTrue();
    }

    [Test]
    public void Check_should_mark_what_is_missing_and_an_editor_is_needed_too()
    {
        ChecklistFacts facts = new() { MergeTool = "custom" };

        IReadOnlyList<ChecklistEntry> entries = SettingsChecklist.Check(facts);

        entries.Should().OnlyContain(entry => entry.State == ChecklistState.Unset);
        entries.Select(entry => entry.Text).Should().Equal(SettingsChecklist.GitNotFound, SettingsChecklist.NoEmailSet,
            "custom is configured as mergetool, this is a custom mergetool and needs a custom cmd to be configured.",
            SettingsChecklist.AdviceDiffToolConfiguration, SettingsChecklist.NoLanguageConfigured);
        SettingsChecklist.IsValid(_good with { Editor = null }, SettingsChecklist.Check(_good)).Should().BeFalse();
    }

    [Test]
    public void Check_should_take_a_tool_git_starts_itself_as_configured()
    {
        ChecklistFacts facts = _good with
        {
            MergeToolCommand = null, MergeToolKnownToGit = true, DiffToolCommand = null, DiffToolKnownToGit = true,
        };

        SettingsChecklist.Check(facts).Should().OnlyContain(entry => entry.State == ChecklistState.Set);
    }

    [Test]
    public void Check_should_find_an_old_git_unsupported_or_not_recommended()
    {
        GitVersion old = new("2.20.0");
        GitVersion older = new("2.50.0");

        SettingsChecklist.Check(_good with { GitVersion = old })[0].Should().Be(new ChecklistEntry(ChecklistItem.Git,
            ChecklistState.Unset,
            $"Git found but version {old} is not supported. Upgrade to version {GitVersion.LastRecommendedVersion} or later"));
        SettingsChecklist.Check(_good with { GitVersion = older })[0].State.Should().Be(ChecklistState.NotRecommended);
    }

    [Test]
    public void Check_should_add_upstreams_windows_rows_on_windows()
    {
        ChecklistFacts windows = _good with
        {
            IsWindows = true, LinuxToolsFound = true, SshPath = @"C:\putty\plink.exe", PuttyFilesExist = false,
            CredentialHelper = "!git-credential-winstore.exe",
        };

        IReadOnlyList<ChecklistEntry> entries = SettingsChecklist.Check(windows);

        entries.Select(entry => (entry.Item, entry.State)).Should().Equal((ChecklistItem.Git, ChecklistState.Set),
            (ChecklistItem.UserName, ChecklistState.Set), (ChecklistItem.MergeTool, ChecklistState.Set),
            (ChecklistItem.DiffTool, ChecklistState.Set), (ChecklistItem.LinuxTools, ChecklistState.Set),
            (ChecklistItem.Ssh, ChecklistState.Unset), (ChecklistItem.Language, ChecklistState.Set),
            (ChecklistItem.CredentialWinStore, ChecklistState.Unset));
        SettingsChecklist.Check(windows with { SshPath = "" }).Single(entry => entry.Item == ChecklistItem.Ssh).Text
            .Should().Be(SettingsChecklist.OpenSshUsed);
        SettingsChecklist.Check(windows with { SshPath = "/missing/ssh" })
            .Single(entry => entry.Item == ChecklistItem.Ssh).Text.Should().Be("SSH client not found: /missing/ssh.");
    }

    [Test]
    public void ToolCommand_should_be_the_configured_cmd_or_none_for_an_unknown_tool()
    {
        Dictionary<string, string> config = new() { ["mergetool.mine.cmd"] = "mine \"$MERGED\"" };

        SettingsChecklist.ToolCommand("mine", diff: false, key => config.GetValueOrDefault(key)).Should()
            .Be("mine \"$MERGED\"");
        SettingsChecklist.ToolCommand("unknown", diff: true, key => config.GetValueOrDefault(key)).Should().BeEmpty();
    }

    [Test]
    public void RowColor_should_be_upstreams_and_dimmed_in_a_dark_theme()
    {
        SettingsChecklist.RowColor(ChecklistState.Set, null).Should().Be(Color.FromArgb(128, 255, 128));
        SettingsChecklist.RowColor(ChecklistState.NotRecommended, null).Should().Be(Color.FromArgb(255, 255, 128));
        SettingsChecklist.RowColor(ChecklistState.Unset, null).Should().Be(Color.FromArgb(255, 128, 128));

        Color dimmed = SettingsChecklist.RowColor(ChecklistState.Set, Color.Black);
        (dimmed.R, dimmed.G, dimmed.B).Should().Be(((byte)92, (byte)188, (byte)92));
        SettingsChecklist.TextColor(Color.FromArgb(128, 255, 128)).Should().Be(Color.Black);
        SettingsChecklist.TextColor(Color.FromArgb(40, 60, 40)).Should().Be(Color.White);
    }

    // The designer splits long texts as "..." + newline "...".
    private static string Unwrapped(string source) => source.Replace("\" +\r\n\"", "").Replace("\" +\n\"", "");

    private static string UpstreamSource(params string[] path)
        => File.ReadAllText(Path.Combine([RepositoryRoot(), "src", "app", "GitUI", "CommandsDialogs", "SettingsDialog", .. path]));

    private static string RepositoryRoot([CallerFilePath] string testFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFile)!, "..", "..", ".."));
}
