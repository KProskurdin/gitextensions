using System.Runtime.CompilerServices;
using AwesomeAssertions;
using GitCommands;
using GitExtensions.Xplat.Core.Scripts;
using GitUI.ScriptsEngine;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class ScriptsTests
{
    private const string AppPath = "/apps/GitExtensions";

    [Test]
    public void ScriptsXml_should_read_and_write_upstreams_format()
    {
        // Upstream's own snapshot of its serialized scripts, so a format change upstream shows up here.
        string upstream = File.ReadAllText(Path.Combine(RepositoryRoot(), "tests", "app", "IntegrationTests",
            "UI.IntegrationTests",
            "ScriptEngine", "ScriptManagerTests.Can_save_settings.verified.xml"));

        IReadOnlyList<ScriptDefinition> scripts = ScriptsXml.Read(upstream);

        scripts.Should().ContainSingle();
        scripts[0].Should().BeEquivalentTo(new ScriptDefinition
        {
            Name = "name", Command = "cmd", Arguments = "args", OnEvent = ScriptEvent.None
        });
        Normalize(ScriptsXml.Write(scripts)).Should().Be(Normalize(upstream));
    }

    [Test]
    public void ScriptsXml_should_offer_upstreams_disabled_defaults_when_nothing_is_stored()
    {
        IReadOnlyList<ScriptDefinition> scripts = ScriptsXml.Read("");

        scripts.Should().HaveCount(8).And.OnlyContain(script => !script.Enabled);
        scripts.Select(script => script.HotkeyCommandIdentifier).Should().OnlyHaveUniqueItems();
        scripts.Single(script => script.Name == "Open on GitHub").AddToRevisionGridContextMenu.Should().BeTrue();
    }

    [Test]
    public void ScriptsXml_should_give_a_duplicate_hotkey_identifier_a_free_one()
    {
        string xml = ScriptsXml.Write([
            new ScriptDefinition { Name = "a", HotkeyCommandIdentifier = 9000 },
            new ScriptDefinition { Name = "b", HotkeyCommandIdentifier = 9000 }
        ]);

        ScriptsXml.Read(xml).Select(script => script.HotkeyCommandIdentifier).Should().Equal(9000, 9001);
    }

    [Test]
    public void ScriptsXml_should_read_the_old_separator_format()
    {
        IReadOnlyList<ScriptDefinition> scripts =
            ScriptsXml.Read("one<_PARAM_SEPARATOR_>git<_PARAM_SEPARATOR_>status<_PARAM_SEPARATOR_>yes");

        scripts.Should().ContainSingle().Which.Should().BeEquivalentTo(new ScriptDefinition
        {
            Name = "one",
            Command = "git",
            Arguments = "status",
            AddToRevisionGridContextMenu = true,
            Enabled = true,
        });
    }

    [Test]
    public async Task ExpandAsync_should_replace_the_selected_and_current_commit_options()
    {
        FakeScriptContext context = new();

        string? arguments = await ScriptVariables.ExpandAsync(
            "{sHash} {{sSubject}} {cBranch} {cDefaultRemotePathFromUrl} {sMessage} {WorkingDir} {RepoName}", context);

        arguments.Should().Be("aaaa111 \"fix \\\"it\\\"\" main /owner/repo line one\\nline two /work/repo repo");
    }

    [Test]
    public async Task ExpandAsync_should_ask_when_a_commit_has_several_matching_refs()
    {
        FakeScriptContext context = new() { Choice = "v2" };

        string? arguments = await ScriptVariables.ExpandAsync("{sTag}", context);

        arguments.Should().Be("v2");
        context.Offered.Should().Equal("v1", "v2");
    }

    [Test]
    public async Task ExpandAsync_should_abort_when_an_option_needs_a_selected_commit_and_there_is_none()
    {
        FakeScriptContext context = new() { NoSelection = true };

        (await ScriptVariables.ExpandAsync("{sHash}", context)).Should().BeNull();
        (await ScriptVariables.ExpandAsync("{cHash}", context)).Should().Be("cccc333");
    }

    [Test]
    public async Task ExpandAsync_should_replace_the_hosts_extra_options()
    {
        FakeScriptContext context = new()
        {
            Extra = new Dictionary<string, IReadOnlyList<string>>
            {
                ["SelectedRelativePaths"] = ["a.txt", "b c.txt"]
            }
        };

        (await ScriptVariables.ExpandAsync("{{SelectedRelativePaths}}", context)).Should().Be("\"a.txt\" \"b c.txt\"");
    }

    [Test]
    public async Task ScriptFileOptions_should_give_the_files_and_the_line_and_no_column_when_unknown()
    {
        FakeScriptContext context = new() { Extra = ScriptFileOptions.For(["a.txt", "dir/b.txt"], line: 12) };

        (await ScriptVariables.ExpandAsync("{SelectedRelativePaths}:{LineNumber}:{ColumnNumber}", context))
            .Should().Be("a.txt dir/b.txt:12:");
    }

    [TestCase("https://github.com/owner/repo.git", "/owner/repo")]
    [TestCase("git@github.com:owner/repo.git", "/owner/repo")]
    [TestCase("", "/")]
    public void GetRemotePath_should_take_the_path_without_the_extension(string url, string expected)
    {
        ScriptVariables.GetRemotePath(url).Should().Be(expected);
    }

    [Test]
    public async Task PrepareAsync_should_run_the_configured_git_in_the_foreground()
    {
        ScriptDefinition script = new() { Name = "&Status", Command = "git", Arguments = "log -1 {sHash}" };

        ScriptLaunch? launch =
            await ScriptRunner.PrepareAsync(script, new FakeScriptContext(), new FakePrompts(), AppPath);

        launch.Should().Be(new ScriptLaunch(ScriptLaunchKind.Foreground, AppSettings.GitCommand, "log -1 aaaa111",
            "/work/repo"));
    }

    [Test]
    public async Task PrepareAsync_should_stop_when_the_confirmation_is_declined()
    {
        ScriptDefinition script = new() { Name = "x", Command = "git", AskConfirmation = true };
        FakePrompts prompts = new() { Confirm = false };

        (await ScriptRunner.PrepareAsync(script, new FakeScriptContext(), prompts, AppPath)).Should().BeNull();
        prompts.Asked.Should().Equal("Do you want to execute script: 'x'?");
    }

    [Test]
    public async Task PrepareAsync_should_ask_for_labeled_input_with_a_default_built_from_options()
    {
        ScriptDefinition script = new()
        {
            Name = "tag",
            Command = "gitex",
            Arguments = "tag {UserInput:Name=v-{sHash}} {UserInput:Name}",
            RunInBackground = true
        };
        FakePrompts prompts = new() { Answer = "v1.0" };

        ScriptLaunch? launch = await ScriptRunner.PrepareAsync(script, new FakeScriptContext(), prompts, AppPath);

        prompts.Asked.Should().Equal("User input for script 'tag' | Name | v-aaaa111");
        launch.Should().Be(new ScriptLaunch(ScriptLaunchKind.Background, AppPath, "tag v1.0 v1.0", "/work/repo"));
    }

    [Test]
    public async Task PrepareAsync_should_open_a_url_and_stop_a_cancelled_input()
    {
        ScriptDefinition url = new()
        {
            Name = "web",
            Command = "{openurl}",
            Arguments = "https://github.com{cDefaultRemotePathFromUrl}/commit/{sHash}"
        };
        ScriptDefinition input = new() { Name = "ask", Command = "git", Arguments = "{UserInput}" };

        (await ScriptRunner.PrepareAsync(url, new FakeScriptContext(), new FakePrompts(), AppPath)).Should()
            .Be(new ScriptLaunch(ScriptLaunchKind.OpenUrl, "", "https://github.com/owner/repo/commit/aaaa111",
                "/work/repo"));
        (await ScriptRunner.PrepareAsync(input, new FakeScriptContext(), new FakePrompts { Answer = null }, AppPath))
            .Should().BeNull();
    }

    [Test]
    public async Task PrepareAsync_should_name_the_plugin_of_a_plugin_command()
    {
        ScriptDefinition braces = new() { Name = "p", Command = "{plugin:Periodic background fetch}" };
        ScriptDefinition prefix = new() { Name = "q", Command = "plugin:Find large files" };
        ScriptDefinition asked = new() { Name = "r", Command = "plugin:x", AskConfirmation = true };

        // As upstream: the braces form is lowered; the host finds the plugin by name ignoring case.
        (await ScriptRunner.PrepareAsync(braces, new FakeScriptContext(), new FakePrompts(), AppPath)).Should()
            .Be(new ScriptLaunch(ScriptLaunchKind.Plugin, "periodic background fetch", "", "/work/repo"));
        (await ScriptRunner.PrepareAsync(prefix, new FakeScriptContext(), new FakePrompts(), AppPath)).Should()
            .Be(new ScriptLaunch(ScriptLaunchKind.Plugin, "Find large files", "", "/work/repo"));
        (await ScriptRunner.PrepareAsync(asked, new FakeScriptContext(), new FakePrompts { Confirm = false }, AppPath))
            .Should().BeNull();
    }

    [Test]
    public async Task PrepareAsync_should_refuse_a_missing_revision()
    {
        ScriptDefinition needsCommit = new() { Name = "n", Command = "git", Arguments = "show {sHash}" };

        Func<Task> runWithoutCommit = () =>
            ScriptRunner.PrepareAsync(needsCommit, new FakeScriptContext { NoSelection = true }, new FakePrompts(),
                AppPath);

        await runWithoutCommit.Should().ThrowAsync<ScriptException>().WithMessage("*A valid revision is required*");
    }

    private static string Normalize(string xml)
        => xml.Replace("﻿", "").Replace("\r\n", "\n").Replace("<?xml version=\"1.0\" encoding=\"utf-16\"?>\n", "")
            .Trim();

    private static string RepositoryRoot([CallerFilePath] string testFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFile)!, "..", "..", ".."));

    private sealed class FakeScriptContext : IScriptContext
    {
        public bool NoSelection { get; init; }

        public string Choice { get; init; } = "";

        public IReadOnlyDictionary<string, IReadOnlyList<string>> Extra { get; init; } =
            new Dictionary<string, IReadOnlyList<string>>();

        public List<string> Offered { get; } = [];

        public string WorkingDir => "/work/repo";

        public string RepoName => "repo";

        public ScriptRevision? Selected => NoSelection
            ? null
            : new ScriptRevision("aaaa111", "fix \"it\"", "line one\nline two", "Ann", "Bob", DateTime.UnixEpoch,
                DateTime.UnixEpoch,
                [new ScriptRef("v1", IsTag: true, IsRemote: false), new ScriptRef("v2", IsTag: true, IsRemote: false)]);

        public IReadOnlyList<string> SelectedHashes => NoSelection ? [] : ["aaaa111"];

        public string CurrentBranch => "main";

        public string CurrentRemote => "";

        public IReadOnlyDictionary<string, IReadOnlyList<string>> ExtraOptions => Extra;

        public Task<ScriptRevision?> GetCurrentAsync(bool loadBody)
            => Task.FromResult<ScriptRevision?>(new ScriptRevision("cccc333", "current", null, "Ann", "Ann",
                DateTime.UnixEpoch,
                DateTime.UnixEpoch, [new ScriptRef("main", IsTag: false, IsRemote: false)]));

        public string GetConfig(string key) => key switch
        {
            "branch.main.remote" => "origin",
            "remote.origin.url" => "git@github.com:owner/repo.git",
            _ => "",
        };

        public Task<string> ChooseAsync(IReadOnlyList<string> options)
        {
            Offered.AddRange(options);
            return Task.FromResult(Choice);
        }
    }

    private sealed class FakePrompts : IScriptPrompts
    {
        public bool Confirm { get; init; } = true;

        public string? Answer { get; init; } = "answer";

        public List<string> Asked { get; } = [];

        public Task<bool> ConfirmAsync(string message)
        {
            Asked.Add(message);
            return Task.FromResult(Confirm);
        }

        public Task<string?> AskAsync(string caption, string? label, string defaultValue)
        {
            Asked.Add($"{caption} | {label} | {defaultValue}");
            return Task.FromResult(Answer);
        }

        public Task<string?> PickFilesAsync() => Task.FromResult<string?>("\"a.txt\"");
    }
}
