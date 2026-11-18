using AwesomeAssertions;
using GitExtensions.Xplat.Core.Platform;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

// The workaround for upstream's thread-unsafe default output encoding (PLAN.md section 5, upstream defects): it must keep
// reaching upstream's lazy without starting a process, which fails here first if upstream's GetOutputAsync changes.
internal sealed class GitOutputEncodingWarmUpTests
{
    [Test]
    public void Run_should_read_the_encoding_without_starting_a_process()
    {
        Action run = GitOutputEncodingWarmUp.Run;

        run.Should().NotThrow();
        run.Should().NotThrow("a second run finds the value made");
    }
}
