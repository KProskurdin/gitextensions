using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class CheckoutRevisionDialogTests
{
    private static readonly ObjectId _commit = ObjectId.Parse("0123456789abcdef0123456789abcdef01234567");

    [Test]
    public void The_texts_are_upstreams()
    {
        string form = UpstreamSource("src/app/GitUI/CommandsDialogs/FormCheckoutRevision.cs");
        form.Should().Contain($"\"{CheckoutRevisionDialog.NoRevisionSelected}\"")
            .And.Contain($"\"{CheckoutRevisionDialog.NoRevisionSelectedCaption}\"");
    }

    [Test]
    public void Validate_should_ask_for_a_commit()
    {
        CheckoutRevisionDialog dialog = new();
        dialog.Validate().Should().Be(CheckoutRevisionDialog.NoRevisionSelected);

        dialog.Revision = _commit;
        dialog.Validate().Should().BeNull();
    }

    [Test]
    public void Arguments_should_be_upstreams_checkout_forced_or_not()
    {
        CheckoutRevisionDialog dialog = new() { Revision = _commit };
        dialog.Arguments().ToString().Should().Be($"checkout \"{_commit}\"");

        dialog.Force = true;
        dialog.Arguments().ToString().Should().Be($"checkout --force \"{_commit}\"");
    }

    private static string UpstreamSource(string relativePath, [System.Runtime.CompilerServices.CallerFilePath] string here = "")
        => File.ReadAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..", relativePath)));
}
