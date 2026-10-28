using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class CherryPickDialogTests
{
    private static readonly ObjectId _commit = ObjectId.Parse("1234567890123456789012345678901234567890");

    [Test]
    public void Arguments_should_be_upstreams_cherry_pick_with_the_options()
    {
        CherryPickDialog.Arguments(_commit, autoCommit: true, parentNumber: null, addReference: false).ToString()
            .Should().Be($"cherry-pick {_commit}");
        CherryPickDialog.Arguments(_commit, autoCommit: false, parentNumber: 2, addReference: true).ToString()
            .Should().Be($"cherry-pick --no-commit -m 2 -x {_commit}");
    }
}
