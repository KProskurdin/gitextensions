using AwesomeAssertions;
using CommonTestUtils;
using NUnit.Framework;

namespace GitCommandsTests.Git;

/// <summary>
///  POSIX counterpart of the <c>GetTagMessage</c> cases in <c>GitModuleTests</c>. Same inputs against a real repository;
///  the messages come back with LF line endings, the platform's line ending.
/// </summary>
[Platform(Include = "Linux,MacOsX")]
internal sealed class XplatLinuxGetTagMessageTests
{
    [TestCase("", "")] // empty message
    [TestCase("a\r\nb\r\n\r\nc\r\n\r\n\r\n\r\nd", "a\nb\n\nc\n\nd")] // various amount of new lines between text lines
    [TestCase("\r\n\r\n\r\n\r\na\r\nb\r\n\r\n\r\n\r\n\r\n\r\n", "a\nb")] // trimmable message
    [TestCase("a\n\n\nb\r\n\r\nc\n\nd", "a\n\nb\n\nc\n\nd")] // mix of new line types
    [TestCase("Hello, this is a single line message", "Hello, this is a single line message")] // single line message
    [TestCase("1\n2\n3\n4\n5\n6\n7\n8\n9\n10\n11\n12\n13", "1\n2\n3\n4\n5\n6\n7\n8\n9\n10\n11\n12\n13")] // message with more than 10 lines
    public void GetTagMessage(string tagMessage, string expectedReturnedMessage)
    {
        using ReferenceRepository repo = new();
        repo.CreateAnnotatedTag("test_tag", repo.CommitHash!, tagMessage);

        string? actualReturnedMessage = repo.Module.GetTagMessage("test_tag", cancellationToken: default);

        actualReturnedMessage.Should().Be(expectedReturnedMessage);
    }
}
