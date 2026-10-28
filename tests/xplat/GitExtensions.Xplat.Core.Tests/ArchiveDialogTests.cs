using AwesomeAssertions;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class ArchiveDialogTests
{
    [Test]
    public void FileNameSuggestion_should_add_a_single_path_with_its_dots_as_underscores()
    {
        string workingDir = Path.Combine(Path.GetTempPath(), "repo");

        ArchiveDialog.FileNameSuggestion(workingDir, "abc", pathFilter: false, "src").Should().Be("repo_abc");
        ArchiveDialog.FileNameSuggestion(workingDir, "abc", pathFilter: true, " a.txt ").Should().Be("repo_abc_a_txt");
        ArchiveDialog.FileNameSuggestion(workingDir, "abc", pathFilter: true, "a\nb").Should().Be("repo_abc");
    }

    [Test]
    public void Arguments_should_be_upstreams_archive_command()
    {
        ArchiveDialog.Arguments(zip: true, "abc", "/out/x.zip", ArchiveDialog.PathArgument("a b\nc"))
            .Should().Be("archive --format=\"zip\" abc --output \"/out/x.zip\" \"a b\" \"c\"");
        ArchiveDialog.Arguments(zip: false, "abc", "x.tar", ArchiveDialog.ChangedFilesArgument(["f.txt"]))
            .Should().Be("archive --format=\"tar\" abc --output \"x.tar\" \"f.txt\"");
    }
}
