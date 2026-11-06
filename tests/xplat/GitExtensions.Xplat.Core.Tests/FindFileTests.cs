using AwesomeAssertions;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class FindFileTests
{
    private static readonly string WorkingDir = Path.Combine(Path.GetTempPath(), "repo");

    private static readonly (string Path, string? OldPath)[] Files =
    [
        ("src/App.cs", null),
        ("src/app/Window.cs", null),
        ("docs/readme.md", "README.md"),
    ];

    [Test]
    public void Matches_should_find_the_paths_that_contain_the_text_ignoring_case()
        => FindFile.Matches(Files, "APP", WorkingDir).Should().Equal("src/App.cs", "src/app/Window.cs");

    [Test]
    public void Matches_should_find_a_file_by_its_name_before_a_rename()
        => FindFile.Matches(Files, "README", WorkingDir).Should().Equal("docs/readme.md");

    [Test]
    public void Matches_should_find_the_files_a_path_in_the_working_directory_starts()
        => FindFile.Matches(Files, Path.Combine(WorkingDir, "src", "app"), WorkingDir)
            .Should().Equal("src/App.cs", "src/app/Window.cs");

    [Test]
    public void Matches_should_find_nothing_for_text_no_path_contains()
        => FindFile.Matches(Files, "missing", WorkingDir).Should().BeEmpty();
}
