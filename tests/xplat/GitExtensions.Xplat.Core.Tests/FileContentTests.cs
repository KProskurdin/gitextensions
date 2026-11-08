using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Diff;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class FileContentTests
{
    [Test]
    public void The_texts_are_upstreams()
    {
        string viewer = File.ReadAllText(UpstreamFileViewer());
        foreach (FieldInfo field in typeof(FileContent).GetFields(BindingFlags.Public | BindingFlags.Static)
                     .Where(field => field.FieldType == typeof(string)))
        {
            viewer.Should().Contain($"\"{field.GetValue(null)}\"", field.Name);
        }
    }

    [Test]
    public void HexDump_should_give_the_name_size_offset_bytes_and_characters_as_upstream()
    {
        string dump = FileContent.HexDump(FileContent.BinaryFile, "a.dat", "AB\0CDEFGHIJKLMNOPQ", text => text);

        dump.ReplaceLineEndings("\n").Should().Be(
            "Binary file: a.dat\n" +
            "\n" +
            "18 bytes:\n" +
            "\n" +
            "0000   41 42 00 43 44 45 46 47  48 49 4A 4B 4C 4D 4E 4F   AB.CDEFG HIJKLMNO\n" +
            "0010   50 51                                              PQ               ");
    }

    [Test]
    public void HexDump_should_say_the_size_in_MB_from_a_tenth_and_truncate_after_256_lines()
    {
        string data = new('x', 200 * 1024);

        string dump = FileContent.HexDump(FileContent.BinaryFile, "a.dat", data, text => text);

        dump.Should().Contain(" MB / ");
        dump.Should().EndWith("[Truncated]");
        dump.ReplaceLineEndings("\n").Split('\n').Should().Contain(line => line.StartsWith("0FF0   ", StringComparison.Ordinal))
            .And.NotContain(line => line.StartsWith("1000   ", StringComparison.Ordinal));
    }

    [Test]
    public void BinaryData_should_keep_a_character_per_byte()
    {
        byte[] bytes = [0xFF, 0x00, 0x80];

        FileContent.BinaryData(bytes).Select(c => (byte)c).Should().Equal(bytes);
    }

    [TestCase(null, false, null, false)]
    [TestCase(null, true, null, true)]
    [TestCase("HEAD", false, "HEAD", false)]
    public void Of_should_keep_a_name_the_index_and_the_working_directory(string? hash, bool staged, string? expected, bool expectedStaged)
    {
        FileRevision revision = FileRevision.Of(hash, staged);

        revision.Hash.Should().Be(expected);
        revision.Staged.Should().Be(expectedStaged);
    }

    [Test]
    public void Of_should_take_artificial_ids_for_the_index_and_the_working_directory()
    {
        FileRevision.Of(ObjectId.WorkTreeId).IsWorkTree.Should().BeTrue();
        FileRevision.Of(ObjectId.WorkTreeId.ToString()).IsWorkTree.Should().BeTrue();
        FileRevision.Of(ObjectId.IndexId).Should().Be(new FileRevision(null, true));
        FileRevision.Of(ObjectId.IndexId).BlobName("a b.png").Should().Be(":a b.png");
        FileRevision.Of("HEAD").BlobName("a.png").Should().Be("HEAD:a.png");
        FileRevision.Of(ObjectId.WorkTreeId).BlobName("a.png").Should().BeNull();
    }

    private static string UpstreamFileViewer([CallerFilePath] string here = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..",
            "src", "app", "GitUI", "Editor", "FileViewer.cs"));
}
