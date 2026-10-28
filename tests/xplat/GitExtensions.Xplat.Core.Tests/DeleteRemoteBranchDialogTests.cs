using AwesomeAssertions;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class DeleteRemoteBranchDialogTests
{
    [Test]
    public void CandidatesText_should_list_eight_branches_and_count_the_rest()
    {
        DeleteRemoteBranchDialog.CandidatesText([]).Should().BeEmpty();
        DeleteRemoteBranchDialog.CandidatesText(["a"]).Should()
            .Be("Local tracking branche(s) candidate to deletion:" + Environment.NewLine + " - a" + Environment.NewLine);

        string many = DeleteRemoteBranchDialog.CandidatesText([.. Enumerable.Range(1, 10).Select(i => $"b{i}")]);

        many.Should().Contain(" - b8").And.NotContain(" - b9").And.EndWith("and 2 more...");
    }
}
