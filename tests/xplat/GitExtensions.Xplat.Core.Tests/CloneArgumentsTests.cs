using AwesomeAssertions;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class CloneArgumentsTests
{
    [TestCase("git://example.com/a.git", "git://example.com/a.git")]
    [TestCase("http://example.com/a.git", "http://example.com/a.git")]
    [TestCase("https://example.com/a.git", "https://example.com/a.git")]
    [TestCase("github-windows://openRepo/https://github.com/o/r", "https://github.com/o/r")]
    [TestCase("github-mac://openRepo/https://github.com/o/r", "https://github.com/o/r")]
    public void A_url_as_the_first_argument_should_open_the_clone_dialog_on_it(string argument, string url)
    {
        CloneArguments.AsksForClone([argument]).Should().BeTrue();
        CloneArguments.Url([argument]).Should().Be(url);
    }

    [Test]
    public void The_clone_verb_should_open_the_clone_dialog_with_its_argument_if_any()
    {
        CloneArguments.AsksForClone(["clone"]).Should().BeTrue();
        CloneArguments.Url(["clone"]).Should().BeNull();
        CloneArguments.Url(["clone", "https://example.com/a.git"]).Should().Be("https://example.com/a.git");
    }

    [TestCase("/home/me/repo")]
    [TestCase("repo")]
    [TestCase("https-folder")]
    public void A_folder_should_not_open_the_clone_dialog(string argument)
    {
        CloneArguments.AsksForClone([argument]).Should().BeFalse();
        CloneArguments.Url([argument]).Should().BeNull();
    }

    [Test]
    public void No_arguments_should_not_open_the_clone_dialog()
    {
        CloneArguments.AsksForClone([]).Should().BeFalse();
        CloneArguments.Url([]).Should().BeNull();
    }
}
