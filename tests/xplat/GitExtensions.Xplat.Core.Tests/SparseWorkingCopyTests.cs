using AwesomeAssertions;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class SparseWorkingCopyTests
{
    [Test]
    public void PassAllRules_should_put_the_pass_all_rule_first_and_comment_out_the_rules()
        => SparseWorkingCopy.PassAllRules("/src/\n# keep\n!/src/big/\n")
            .Should().Be(string.Join(Environment.NewLine, "/*", "#/src/", "# keep", "#!/src/big/"));
}
