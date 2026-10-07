using AwesomeAssertions;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class BranchNamesTests
{
    [TestCase("my feature", "_", "my_feature")]
    [TestCase("my feature", "-", "my-feature")]
    [TestCase("my feature", "", "myfeature")]
    [TestCase("feature..x", "_", "feature_x")]
    [TestCase("ok/name", "_", "ok/name")]
    public void Normalise_should_apply_upstreams_normaliser_with_the_chosen_symbol(string name, string symbol,
        string expected)
    {
        BranchNames.Normalise(name, enabled: true, symbol).Should().Be(expected);
    }

    [Test]
    public void Normalise_should_leave_the_name_alone_when_turned_off()
    {
        BranchNames.Normalise("my feature", enabled: false, "_").Should().Be("my feature");
    }
}
