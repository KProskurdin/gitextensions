using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

// Upstream's GitUICommands.RunCommand, InitializeArguments and Program.GetWorkingDir.
internal sealed class CommandLineTests
{
    private string _folder = null!;

    [SetUp]
    public void Setup()
    {
        // A .git folder is all upstream's GitModule.IsValidGitWorkingDir looks for.
        _folder = Directory.CreateTempSubdirectory("xplat-command-line-").FullName;
        Directory.CreateDirectory(Path.Combine(_folder, "repo", ".git"));
        Directory.CreateDirectory(Path.Combine(_folder, "repo", "sub"));
        File.WriteAllText(Path.Combine(_folder, "repo", "sub", "file.txt"), "");
        Directory.CreateDirectory(Path.Combine(_folder, "elsewhere"));
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_folder, recursive: true);

    [Test]
    public void No_arguments_should_be_no_request()
        => CommandLine.Parse([]).Should().BeNull();

    [Test]
    public void The_verb_should_be_the_first_argument_and_the_rest_its_arguments()
    {
        CommandLineRequest request = CommandLine.Parse(["viewpatch", "a.patch"])!;

        request.Verb.Should().Be("viewpatch");
        request.Arguments.Should().Equal("a.patch");
        request.Argument(0).Should().Be("a.patch");
        request.Argument(1).Should().BeNull();
    }

    [Test]
    public void Options_should_take_the_next_argument_unless_it_is_another_option()
    {
        CommandLineRequest request = CommandLine.Parse(
            ["pull", "--rebase", "--remotebranch", "main", "--quiet", "plain", "--last"])!;

        request.Options.Should().BeEquivalentTo(new Dictionary<string, string?>
        {
            ["rebase"] = null,
            ["remotebranch"] = "main",
            ["quiet"] = "plain",
            ["last"] = null,
        });
    }

    [TestCase("blame", "Cannot open blame, there is no file selected.", "Blame")]
    [TestCase("difftool", "Cannot open difftool, there is no file selected.", "Difftool")]
    [TestCase("filehistory", "Cannot open blame / file history, there is no file selected.", "Blame / file history")]
    [TestCase("blamehistory", "Cannot open blame / file history, there is no file selected.", "Blame / file history")]
    [TestCase("fileeditor", "Cannot open file editor, there is no file selected.", "File editor")]
    [TestCase("revert", "Cannot open revert, there is no file selected.", "Revert")]
    public void A_verb_that_needs_a_file_should_have_upstreams_message_without_one(string verb, string text, string caption)
    {
        CommandLine.MissingFile(CommandLine.Parse([verb])!).Should().Be((text, caption));
        CommandLine.MissingFile(CommandLine.Parse([verb, "a.txt"])!).Should().BeNull();
    }

    [Test]
    public void Other_verbs_should_need_no_file()
        => CommandLine.MissingFile(CommandLine.Parse(["settings"])!).Should().BeNull();

    [Test]
    public void Upstreams_verbs_should_all_be_known_and_a_path_should_not()
    {
        CommandLine.Verbs.Should().HaveCount(41).And.Contain(["searchfile", "uninstall", "blamehistory", "compare"]);
        CommandLine.Verbs.Should().NotContain(["help", "/home/me/repo"]);
    }

    [Test]
    public void The_working_directory_should_be_the_repository_of_the_folder_after_the_verb()
        => CommandLine.WorkingDirectory(CommandLine.Parse(["browse", Path.Combine(_folder, "repo", "sub")])!,
                Path.Combine(_folder, "elsewhere"))
            .Should().Be(Path.Combine(_folder, "repo") + Path.DirectorySeparatorChar);

    [Test]
    public void The_working_directory_should_be_the_repository_of_the_file_after_the_verb()
        => CommandLine.WorkingDirectory(
                CommandLine.Parse(["fileeditor", Path.Combine(_folder, "repo", "sub", "file.txt")])!,
                Path.Combine(_folder, "elsewhere"))
            .Should().Be(Path.Combine(_folder, "repo") + Path.DirectorySeparatorChar);

    [Test]
    public void Without_a_path_the_working_directory_should_be_the_repository_around_the_current_directory()
        => CommandLine.WorkingDirectory(CommandLine.Parse(["commit", "--quiet"])!, Path.Combine(_folder, "repo", "sub"))
            .Should().Be(Path.Combine(_folder, "repo") + Path.DirectorySeparatorChar);

    [Test]
    public void Outside_a_repository_there_should_be_no_working_directory()
        => CommandLine.WorkingDirectory(CommandLine.Parse(["settings"])!, Path.Combine(_folder, "elsewhere"))
            .Should().BeNull();

    [Test]
    public void The_working_directory_of_compare_should_be_around_the_current_directory_not_its_revisions()
        => CommandLine.WorkingDirectory(CommandLine.Parse(["compare", Path.Combine(_folder, "repo", "sub"), "HEAD"])!,
                Path.Combine(_folder, "elsewhere"))
            .Should().BeNull("a revision that happens to name a folder is not where the repository is");

    [Test]
    public void Compare_should_show_a_typed_hash_short_and_a_ref_as_typed()
    {
        ObjectId first = ObjectId.Parse("0123456789abcdef0123456789abcdef01234567");
        ObjectId second = ObjectId.Parse("89abcdef0123456789abcdef0123456789abcdef");
        Dictionary<string, ObjectId> commits = new()
        {
            ["0123456^{commit}"] = first,
            ["main^{commit}"] = second,
        };

        (string? error, CompareRevisions? revisions) = CommandLine.Compare(
            CommandLine.Parse(["compare", "0123456", "main"])!, revision => commits.GetValueOrDefault(revision));

        error.Should().BeNull();
        revisions.Should().Be(new CompareRevisions(first, second, first.ToShortString(), "main"));
    }

    [TestCase(new[] { "compare", "main" }, CommandLine.CompareUsage)]
    [TestCase(new[] { "compare", "main", "nothing" }, "No commit found matching: nothing")]
    public void Compare_should_print_upstreams_error_for_wrong_arguments(string[] args, string expected)
    {
        (string? error, CompareRevisions? revisions) = CommandLine.Compare(CommandLine.Parse(args)!,
            revision => revision == "main^{commit}" ? ObjectId.Parse("0123456789abcdef0123456789abcdef01234567") : default);

        error.Should().Be(expected);
        revisions.Should().BeNull();
    }

    [Test]
    public void A_parameter_should_be_the_value_of_its_first_name_equals_argument()
    {
        CommandLineRequest request = CommandLine.Parse(
            ["browse", "/repo", "-filter=fix", "--pathFilter=src/a.txt", "-commit=abc,def", "-filter=later"])!;

        CommandLine.Parameter(request, "-filter").Should().Be("fix");
        CommandLine.Parameter(request, "--pathFilter").Should().Be("src/a.txt");
        CommandLine.Parameter(request, "-commit").Should().Be("abc,def");
        CommandLine.Parameter(request, "-missing").Should().BeEmpty();
    }

    [Test]
    public void The_usage_should_be_upstreams_list()
    {
        CommandLine.Usage.Should().StartWith("Supported commandline arguments for")
            .And.Contain("browse [path] [-filter=] [--pathFilter=<filepath>] [-commit=<selectedSha>[,<firstSha>]]")
            .And.EndWith("viewpatch [filename]");
        CommandLine.UsageCaption.Should().Be("Commandline usage");
    }
}
