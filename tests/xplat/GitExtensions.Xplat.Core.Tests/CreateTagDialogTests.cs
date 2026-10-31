using AwesomeAssertions;
using GitCommands.Git.Tag;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class CreateTagDialogTests
{
    [Test]
    public void Operations_should_be_upstreams_kinds_in_its_order()
        => CreateTagDialog.Operations.Should().Equal(
            (TagOperation.Lightweight, "Lightweight tag"),
            (TagOperation.Annotate, "Annotated tag"),
            (TagOperation.SignWithDefaultKey, "Sign with default GPG"),
            (TagOperation.SignWithSpecificKey, "Sign with specific GPG"));

    [TestCase(TagOperation.Lightweight, false, false)]
    [TestCase(TagOperation.Annotate, false, true)]
    [TestCase(TagOperation.SignWithDefaultKey, false, true)]
    [TestCase(TagOperation.SignWithSpecificKey, true, true)]
    public void UsesKeyId_and_UsesMessage_should_follow_the_kind(TagOperation operation, bool keyId, bool message)
    {
        CreateTagDialog.UsesKeyId(operation).Should().Be(keyId);
        CreateTagDialog.UsesMessage(operation).Should().Be(message);
    }
}
