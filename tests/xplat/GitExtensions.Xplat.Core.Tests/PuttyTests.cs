using System.Runtime.CompilerServices;
using AwesomeAssertions;
using GitExtensions.Xplat.Core.Platform;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class PuttyTests
{
    [TestCase("git@github.com:user/repo.git", "\"git@github.com:user/repo.git\"")]
    [TestCase("https://github.com/user/repo.git", "\"https://github.com/user/repo.git\"")]
    [TestCase("ssh://git@example.com/user/repo.git", "\"git@example.com:user/repo.git\"")]
    [TestCase("ssh://git@example.com:2222/user/repo.git", "-P 2222 \"git@example.com:user/repo.git\"")]
    [TestCase("ssh://example.com/repo.git", "\"example.com:repo.git\"")]
    public void PlinkCompatibleUrl_should_turn_an_ssh_url_into_plinks_form(string url, string expected)
    {
        Putty.PlinkCompatibleUrl(url).Should().Be(expected);
    }

    [Test]
    public void The_texts_are_upstreams()
    {
        string root =
            Path.GetFullPath(Path.Combine(TestFolder(), "..", "..", ".."));
        string strings = File.ReadAllText(Path.Combine(root, "src", "app", "GitUI", "TranslatedStrings.cs"));

        foreach (string text in new[]
                 {
                     Putty.KeyNotFound, Putty.NotInstalled, Putty.NotConfigured, Putty.WhereConfigure
                 })
        {
            strings.Should().Contain($"\"{text}\"");
        }
    }

    private static string TestFolder([CallerFilePath] string testFile = "") => Path.GetDirectoryName(testFile)!;
}
