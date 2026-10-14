using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using GitCommands.Remotes;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;
using UserRepository = GitCommands.UserRepositoryHistory.Repository;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class RemotesDialogTests
{
    [Test]
    public void The_texts_are_upstreams()
    {
        string root = Path.GetFullPath(Path.Combine(TestFolder(), "..", "..", ".."));
        string form = File.ReadAllText(Path.Combine(root, "src", "app", "GitUI", "CommandsDialogs", "FormRemotes.cs"))
            .Replace("\r\n", "\n");
        foreach (FieldInfo field in typeof(RemotesDialog).GetFields(BindingFlags.Public | BindingFlags.Static)
                     .Where(field => field.FieldType == typeof(string)))
        {
            string text = ((string)field.GetValue(null)!).Replace(Environment.NewLine, "\n").Replace("\"", "\\\"");

            form.Should().Contain(text, field.Name);
        }
    }

    [Test]
    public void Load_should_select_the_remote_asked_for_else_the_first_active_one_else_the_first_inactive_one()
    {
        FakeRemotes remotes = new(Remote("old", disabled: true), Remote("origin"), Remote("fork"));
        RemotesDialog dialog = new(remotes);

        dialog.Load("fork");
        dialog.Selected!.Name.Should().Be("fork");
        dialog.ActiveRemotes.Select(remote => remote.Name).Should().Equal("origin", "fork");
        dialog.InactiveRemotes.Select(remote => remote.Name).Should().Equal("old");

        dialog.Load("missing");
        dialog.Selected!.Name.Should().Be("origin");

        RemotesDialog onlyInactive = new(new FakeRemotes(Remote("old", disabled: true)));
        onlyInactive.Load();
        onlyInactive.Selected!.Name.Should().Be("old");
        RemotesDialog none = new(new FakeRemotes());
        none.Load();
        none.Selected.Should().BeNull();
    }

    [Test]
    public void Select_should_show_the_remotes_details_and_an_inactive_one_cannot_be_edited()
    {
        RemotesDialog dialog = new(new FakeRemotes(
            Remote("origin", pushUrl: "git@example.com:push.git", color: "#FF0000", prefix: "o/"),
            Remote("old", disabled: true)));
        dialog.Load();

        dialog.Header.Should().Be(RemotesDialog.HeaderEdit);
        dialog.Url.Should().Be("https://example.com/origin.git");
        dialog.SeparatePushUrl.Should().BeTrue();
        dialog.UrlLabel.Should().Be(RemotesDialog.LabelUrlAsFetch);
        dialog.Color.Should().Be("#FF0000");
        dialog.Prefix.Should().Be("o/");
        dialog.IsEditable.Should().BeTrue();

        dialog.Select(dialog.InactiveRemotes.Single());
        dialog.IsEditable.Should().BeFalse();
        dialog.UrlLabel.Should().Be(RemotesDialog.LabelUrlAsFetchPush);

        dialog.Select(null);
        dialog.Header.Should().Be(RemotesDialog.HeaderNew);
        dialog.Name.Should().BeEmpty();
        dialog.CanSave.Should().BeFalse();
        dialog.CanDelete.Should().BeFalse();
    }

    [Test]
    public void Save_should_refuse_a_new_remote_whose_name_exists_and_pass_upstreams_values()
    {
        FakeRemotes remotes = new(Remote("origin"), Remote("old", disabled: true));
        RemotesDialog dialog = new(remotes);
        dialog.Load();
        dialog.Select(null);

        dialog.Name = " origin ";
        dialog.Save().Should().Be(new RemoteSaveOutcome("An active remote named \"origin\" already exists.", null));
        dialog.Name = "old";
        dialog.Save().Message.Should().Be("An inactive remote named \"old\" already exists.");

        dialog.Name = "fork";
        dialog.Url = " https://example.com/fork.git ";
        dialog.SeparatePushUrl = true;
        dialog.PushUrl = "HTTPS://example.com/fork.git";
        dialog.Save().Should().Be(new RemoteSaveOutcome(null, "fork"));

        remotes.Saved.Should().Equal(((ConfigFileRemote?)null, "fork", "https://example.com/fork.git", (string?)null));
        dialog.SeparatePushUrl.Should().BeFalse("a push URL equal to the URL is not separate");
    }

    [Test]
    public void Save_should_report_gits_message()
    {
        FakeRemotes remotes = new(Remote("origin")) { SaveMessage = "fatal: no" };
        RemotesDialog dialog = new(remotes);
        dialog.Load();
        dialog.Name = "renamed";

        dialog.Save().Should().Be(new RemoteSaveOutcome("fatal: no", null));
        remotes.Saved.Single().Remote!.Name.Should().Be("origin");
    }

    [Test]
    public void ToggleState_should_switch_the_remote_and_keep_it_selected()
    {
        FakeRemotes remotes = new(Remote("origin"), Remote("fork"));
        RemotesDialog dialog = new(remotes);
        dialog.Load("fork");

        dialog.ToggleState();

        remotes.Toggled.Should().Equal(("fork", true));
        dialog.Selected!.Name.Should().Be("fork");
        dialog.Selected.Disabled.Should().BeTrue();
    }

    [Test]
    public void UpdateUrlHistory_should_replace_a_changed_url_and_put_a_new_one_first()
    {
        RemotesDialog dialog = new(new FakeRemotes());
        List<UserRepository> history = [new("https://example.com/a.git"), new("https://example.com/old.git")];

        dialog.UpdateUrlHistory(history, Remote("origin", url: "https://example.com/old.git"),
            "https://example.com/new.git", pushUrl: null);

        history.Select(entry => entry.Path).Should().Equal("https://example.com/new.git", "https://example.com/a.git");
    }

    [Test]
    public void ProposedUrls_should_offer_the_other_remotes_urls_for_the_new_name()
    {
        RemotesDialog dialog = new(new FakeRemotes(Remote("origin", url: "https://github.com/owner/repo.git")));
        dialog.Load();
        dialog.Select(null);
        dialog.Name = "someone";

        dialog.ProposedUrls(pushUrl: false, "", ["https://example.com/other.git"], out bool clearText).Should()
            .Equal("https://github.com/someone/repo.git", "https://example.com/other.git");
        clearText.Should().BeFalse();

        dialog.Name = "upstream";
        dialog.ProposedUrls(false, "", [], out clearText).Should().Equal("https://github.com/TO_REPLACE/repo.git");
        clearText.Should().BeTrue("a generic name only shows the pattern");
    }

    [Test]
    public void NameFromUrl_should_name_a_new_remote_after_the_hosting_owner()
    {
        RemotesDialog dialog = new(new FakeRemotes());
        dialog.Url = "https://github.com/someone/repo.git";

        dialog.NameFromUrl().Should().Be("someone");
        dialog.Name = "taken";
        dialog.NameFromUrl().Should().BeNull();
    }

    private static ConfigFileRemote Remote(string name, bool disabled = false, string? url = null,
        string? pushUrl = null, string? color = null, string? prefix = null)
        => new()
        {
            Name = name, Disabled = disabled, Url = url ?? $"https://example.com/{name}.git", PushUrl = pushUrl,
            Color = color, Prefix = prefix,
        };

    private static string TestFolder([CallerFilePath] string testFile = "") => Path.GetDirectoryName(testFile)!;

    private sealed class FakeRemotes(params ConfigFileRemote[] remotes) : IConfigFileRemoteSettingsManager
    {
        private readonly List<ConfigFileRemote> _remotes = [.. remotes];

        public string? SaveMessage { get; init; }

        public List<(ConfigFileRemote? Remote, string Name, string Url, string? PushUrl)> Saved { get; } = [];

        public List<(string Name, bool Disabled)> Toggled { get; } = [];

        public string? GetDefaultPushRemote(ConfigFileRemote remote, string branch) => null;

        public IEnumerable<ConfigFileRemote> LoadRemotes(bool loadDisabled)
            => _remotes.Where(remote => loadDisabled || !remote.Disabled);

        public string RemoveRemote(ConfigFileRemote remote)
        {
            _remotes.Remove(remote);
            return "";
        }

        public bool EnabledRemoteExists(string remoteName) => _remotes.Any(r => !r.Disabled && r.Name == remoteName);

        public bool DisabledRemoteExists(string remoteName) => _remotes.Any(r => r.Disabled && r.Name == remoteName);

        public ConfigFileRemoteSaveResult SaveRemote(ConfigFileRemote? remote, string remoteName, string remoteUrl,
            string? remotePushUrl, string remotePuttySshKey, string? remoteColor, string? remotePrefix)
        {
            Saved.Add((remote, remoteName, remoteUrl, remotePushUrl));
            return new ConfigFileRemoteSaveResult(SaveMessage, false);
        }

        public void ToggleRemoteState(string remoteName, bool disabled)
        {
            Toggled.Add((remoteName, disabled));
            _remotes.Single(remote => remote.Name == remoteName).Disabled = disabled;
        }

        public IReadOnlyList<Remote> GetDisabledRemotes() => [];

        public IReadOnlyList<string> GetDisabledRemoteNames() => [.. _remotes.Where(r => r.Disabled).Select(r => r.Name!)];

        public IReadOnlyList<string> GetEnabledRemoteNames() => [.. _remotes.Where(r => !r.Disabled).Select(r => r.Name!)];
    }
}

