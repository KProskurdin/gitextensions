using AwesomeAssertions;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class CleanupDialogTests
{
    [Test]
    public void The_path_boxes_should_make_upstreams_arguments()
    {
        CleanupDialog.IncludePaths(enabled: false, "a").Should().BeNull();
        CleanupDialog.IncludePaths(enabled: true, "a\r\n\nb c").Should().Be("\"a\" \"b c\"");
        CleanupDialog.ExcludePaths(enabled: true, "x y.txt\nsub\\z.txt").Should().Be("--exclude=x?y.txt --exclude=sub/z.txt");
    }

    [Test]
    public void Commands_should_be_upstreams_clean_and_submodule_clean()
    {
        IReadOnlyList<string> commands =
        [
            .. CleanupDialog.Commands(CleanMode.OnlyNonIgnored, dryRun: true, directories: true, submodules: true,
                "\"a\"", "--exclude=b").Select(command => command.ToString())
        ];

        commands.Should().Equal(
            Commands.Clean(CleanMode.OnlyNonIgnored, dryRun: true, directories: true, "\"a\"", "--exclude=b").ToString(),
            Commands.CleanSubmodules(CleanMode.OnlyNonIgnored, dryRun: true, directories: true, "\"a\"").ToString());
        commands[0].Should().Be("clean -d --dry-run \"a\" --exclude=b");
        CleanupDialog.Commands(CleanMode.All, dryRun: false, directories: false, submodules: false, null, null)
            .Select(command => command.ToString()).Should().Equal("clean -x -f");
    }

    [Test]
    public void The_add_buttons_should_take_paths_inside_the_work_tree_only()
    {
        string workingDir = Path.GetTempPath();
        string gitDir = Path.Combine(workingDir, ".git") + Path.DirectorySeparatorChar;

        CleanupDialog.AcceptsIncludePath(workingDir, gitDir, workingDir).Should().BeTrue();
        CleanupDialog.AcceptsIncludePath(workingDir, gitDir, Path.Combine(workingDir, "missing-" + Guid.NewGuid()))
            .Should().BeFalse();
        CleanupDialog.AcceptsIncludePath(workingDir, gitDir, null).Should().BeFalse();
        CleanupDialog.ExcludePath(workingDir, Path.Combine(workingDir, "f.txt")).Should().Be("f.txt");
        CleanupDialog.ExcludePath(workingDir, "/elsewhere/f.txt").Should().BeNull();
        CleanupDialog.AddLine("", "a").Should().Be("a");
        CleanupDialog.AddLine("a", "b").Should().Be("a" + Environment.NewLine + "b");
    }
}
