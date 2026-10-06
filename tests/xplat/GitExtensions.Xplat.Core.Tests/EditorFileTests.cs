using System.Text;
using AwesomeAssertions;
using GitExtensions.Xplat.Core.Editing;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class EditorFileTests
{
    private string _path = null!;

    [SetUp]
    public void Setup() => _path = Path.Combine(Path.GetTempPath(), $"xplat-editor-{Guid.NewGuid():N}.txt");

    [TearDown]
    public void TearDown() => File.Delete(_path);

    [Test]
    public async Task ReadAsync_should_keep_lf_files_as_lf()
    {
        await File.WriteAllTextAsync(_path, "pick 1 one\npick 2 two\n");

        EditorFile file = await EditorFile.ReadAsync(_path);
        await file.WriteAsync(_path, "pick 2 two\r\npick 1 one\n");

        file.Text.Should().Be("pick 1 one\npick 2 two\n");
        file.HasByteOrderMark.Should().BeFalse();
        (await File.ReadAllBytesAsync(_path)).Should().Equal(Encoding.UTF8.GetBytes("pick 2 two\npick 1 one\n"));
    }

    [Test]
    public async Task WriteAsync_should_keep_crlf_and_the_byte_order_mark()
    {
        await File.WriteAllTextAsync(_path, "first\r\nsecond\r\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        EditorFile file = await EditorFile.ReadAsync(_path);
        await file.WriteAsync(_path, file.Text + "third\n");

        file.Text.Should().Be("first\nsecond\n");
        file.LineEnding.Should().Be("\r\n");
        byte[] expected = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("first\r\nsecond\r\nthird\r\n")];
        (await File.ReadAllBytesAsync(_path)).Should().Equal(expected);
    }

    [Test]
    public async Task ReadAsync_should_read_non_ascii_text_as_utf8()
    {
        await File.WriteAllBytesAsync(_path, Encoding.UTF8.GetBytes("Übersetzung ändern\n"));

        (await EditorFile.ReadAsync(_path)).Text.Should().Be("Übersetzung ändern\n");
    }
}
