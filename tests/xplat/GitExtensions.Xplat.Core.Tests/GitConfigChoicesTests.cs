using AwesomeAssertions;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class GitConfigChoicesTests
{
    [Test]
    public void CredentialHelpers_should_list_the_helpers_of_git_for_windows_as_upstream()
    {
        Dictionary<string, string[]> files = new()
        {
            [Path.Combine("C:", "Git")] =
            [
                Path.Combine("C:", "Git", "mingw64", "bin", "git-credential-manager.exe"),
                Path.Combine("C:", "Git", "mingw64", "bin", "git-credential-helper-selector.exe"),
                Path.Combine("C:", "Git", "mingw64", "libexec", "git-core", "git-credential-wincred.exe"),
            ],
        };

        IReadOnlyList<string> helpers = GitConfigChoices.CredentialHelpers(windows: true,
            Path.Combine("C:", "Git", "bin", "git.exe"), execPath: null, path: null, List(files));

        helpers.Should().Equal("manager", "wincred", "store", "cache");
    }

    [Test]
    public void CredentialHelpers_should_list_the_installed_keychain_helpers_on_other_oses()
    {
        Dictionary<string, string[]> files = new()
        {
            ["/usr/libexec/git"] =
            [
                "/usr/libexec/git/git-credential-libsecret",
                "/usr/libexec/git/git-credential-store",
                "/usr/libexec/git/git-credential-cache",
                "/usr/libexec/git/git-credential-cache--daemon",
            ],
            ["/usr/local/bin"] = ["/usr/local/bin/git-credential-manager"],
        };

        IReadOnlyList<string> helpers = GitConfigChoices.CredentialHelpers(windows: false, "git", "/usr/libexec/git",
            string.Join(Path.PathSeparator, "/usr/local/bin", "/missing"), List(files));

        helpers.Should().Equal("libsecret", "store", "cache", "manager", "oauth");
    }

    [Test]
    public void CredentialHelpers_should_offer_upstreams_choices_when_git_tells_nothing()
    {
        IReadOnlyList<string> helpers = GitConfigChoices.CredentialHelpers(windows: false, "git", execPath: null, path: null,
            List([]));

        helpers.Should().Equal("oauth", "store", "cache");
    }

    [Test]
    public void Editors_should_offer_this_apps_editor_first()
    {
        IReadOnlyList<string> editors = GitConfigChoices.Editors();

        editors[0].Should().EndWith(" fileeditor");
        editors.Should().Contain("vi");
    }

    private static Func<string, bool, IEnumerable<string>> List(Dictionary<string, string[]> files)
        => (folder, _) => files.GetValueOrDefault(folder, []);
}
