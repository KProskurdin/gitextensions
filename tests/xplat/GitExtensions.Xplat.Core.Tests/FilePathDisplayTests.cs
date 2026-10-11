using AwesomeAssertions;
using GitCommands;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class FilePathDisplayTests
{
    [TestCase("src/app/file.cs", TruncatePathMethod.None, "src/app/file.cs")]
    [TestCase("src/app/file.cs", TruncatePathMethod.Compact, "src/app/file.cs")]
    [TestCase("src/app/file.cs", TruncatePathMethod.TrimStart, "src/app/file.cs")]
    [TestCase("src/app/file.cs", TruncatePathMethod.FileNameOnly, "file.cs")]
    [TestCase("src/submodule/", TruncatePathMethod.FileNameOnly, "submodule")]
    public void Text_should_keep_only_the_file_name_for_filename_only(string path, TruncatePathMethod method,
        string expected)
    {
        FilePathDisplay.Text(path, method).Should().Be(expected);
    }
}
