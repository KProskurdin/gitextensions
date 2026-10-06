using AwesomeAssertions;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class GitAskPassTests
{
    [Test]
    public void Environment_should_set_ssh_askpass_force_it_and_mark_the_process()
    {
        GitAskPass.Environment("/apps/GitExtensions").Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["SSH_ASKPASS"] = "/apps/GitExtensions",
            ["SSH_ASKPASS_REQUIRE"] = "force",
            [GitAskPass.MarkerVariable] = "1",
        });
    }

    [Test]
    public void Environment_should_not_set_git_askpass_so_the_users_own_comes_first()
    {
        GitAskPass.Environment("/apps/GitExtensions").Should().NotContainKey("GIT_ASKPASS");
    }

    [TestCase("1", new[] { "Password for 'https://host': " }, true)]
    [TestCase(null, new[] { "Password for 'https://host': " }, false)]
    [TestCase("1", new string[0], false)]
    [TestCase("1", new[] { "fileeditor", "todo" }, false)]
    public void IsAskPassRun_should_need_the_marker_and_exactly_one_argument(string? marker, string[] args, bool expected)
    {
        GitAskPass.IsAskPassRun(marker, args).Should().Be(expected);
    }

    [TestCase("Enter passphrase for key '/home/me/.ssh/id_ed25519': ", true)]
    [TestCase("me@host's password: ", true)]
    [TestCase("Password for 'https://me@example.com': ", true)]
    [TestCase("Enter PIN for 'token': ", true)]
    [TestCase("Username for 'https://example.com': ", false)]
    [TestCase("Are you sure you want to continue connecting (yes/no/[fingerprint])? ", false)]
    [TestCase("Spinning up the pipeline: ", false)]
    public void IsSecret_should_hide_passwords_passphrases_and_pins_only(string prompt, bool expected)
    {
        GitAskPass.IsSecret(prompt).Should().Be(expected);
    }
}
