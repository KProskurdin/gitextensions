using AwesomeAssertions;
using GitCommands;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class GitIgnoreDialogTests
{
    private string _folder = "";

    [SetUp]
    public void SetUp()
    {
        _folder = Path.Combine(Path.GetTempPath(), "xplat-gitignore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(_folder, recursive: true);
    }

    [Test]
    public void AddDefaults_should_add_the_missing_defaults_after_a_blank_line()
    {
        GitIgnoreDialog.AddDefaults("bin/\nobj/\n", ["obj/", "*.log", "*.log", ".vs/"])
            .Should().Be("bin/\nobj/\n\n*.log\n.vs/\n");
    }

    [Test]
    public void AddDefaults_should_keep_the_text_line_ending()
    {
        GitIgnoreDialog.AddDefaults("bin/\r\n", ["*.log"]).Should().Be("bin/\r\n\r\n*.log\r\n");
    }

    [Test]
    public void AddDefaults_should_return_null_when_the_text_has_every_default()
    {
        GitIgnoreDialog.AddDefaults("*.log\r\n.vs/", ["*.log", ".vs/"]).Should().BeNull();
    }

    [Test]
    public void HelpText_should_be_upstreams_introduction_with_the_defaults_as_example()
    {
        string[] lines = GitIgnoreDialog.HelpText.Split('\n');

        lines[..3].Should().Equal("Specify filepatterns you want git to ignore.", "", "Example:");
        lines[3..].Should().Equal(GitIgnoreDialog.DefaultIgnorePatterns);
    }

    [TestCase("a\r\nb", "\r\n")]
    [TestCase("a\nb\r\n", "\n")]
    public void NewLineOf_should_be_the_first_line_ending(string text, string expected)
    {
        GitIgnoreDialog.NewLineOf(text).Should().Be(expected);
    }

    [Test]
    public void NewLineOf_should_be_the_platform_one_for_a_single_line()
    {
        GitIgnoreDialog.NewLineOf("a").Should().Be(Environment.NewLine);
    }

    [Test]
    public void PatternsOf_should_be_the_non_empty_lines()
    {
        GitIgnoreDialog.PatternsOf("/a.txt\r\n\r\n*.dll\n").Should().Equal("/a.txt", "*.dll");
    }

    [Test]
    public void Save_should_end_the_text_with_its_line_ending_and_create_the_folder()
    {
        string path = Path.Combine(_folder, "info", "exclude");

        string saved = GitIgnoreDialog.Save(path, "bin/\nobj/");

        saved.Should().Be("bin/\nobj/\n");
        File.ReadAllText(path, GitModule.SystemEncoding).Should().Be("bin/\nobj/\n");
    }

    [Test]
    public void Save_should_write_a_read_only_file_and_keep_it_read_only()
    {
        string path = Path.Combine(_folder, ".gitignore");
        File.WriteAllText(path, "old\n");
        File.SetAttributes(path, FileAttributes.ReadOnly);

        GitIgnoreDialog.Save(path, "new\n");

        File.ReadAllText(path).Should().Be("new\n");
        File.GetAttributes(path).Should().HaveFlag(FileAttributes.ReadOnly);
        File.SetAttributes(path, FileAttributes.Normal);
    }

    [Test]
    public void Append_should_add_each_pattern_on_a_line_after_the_last_one()
    {
        string path = Path.Combine(_folder, ".gitignore");
        File.WriteAllText(path, "bin/\r\nobj/");

        GitIgnoreDialog.Append(path, ["/a.txt", "obj/"]);

        File.ReadAllText(path).Should().Be("bin/\r\nobj/\r\n/a.txt\r\nobj/\r\n");
    }

    [Test]
    public void Append_should_create_a_missing_file()
    {
        string path = Path.Combine(_folder, "info", "exclude");

        GitIgnoreDialog.Append(path, ["/a.txt"]);

        File.ReadAllText(path).Should().Be("/a.txt" + Environment.NewLine);
    }

    [Test]
    public void TextsFor_should_be_upstreams_for_each_file()
    {
        GitIgnoreDialog.TextsFor(localExclude: false).Caption.Should().Be("Edit .gitignore");
        GitIgnoreDialog.TextsFor(localExclude: true).SaveQuestion.Should().Be("Save changes to .git/info/exclude?");
    }
}
