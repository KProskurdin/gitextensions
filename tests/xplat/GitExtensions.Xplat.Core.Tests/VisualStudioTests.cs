using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using AwesomeAssertions;
using GitExtensions.Xplat.Core.Platform;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

[Platform("Win")]
[SupportedOSPlatform("windows")]
internal sealed class VisualStudioTests
{
    [Test]
    public void VswhereArguments_should_be_upstreams()
        => VisualStudio.VswhereArguments.ToString().Should().Be(
            "-prerelease -latest -requires Microsoft.VisualStudio.Product.Enterprise"
            + " -requires Microsoft.VisualStudio.Product.Professional -requires Microsoft.VisualStudio.Product.Community"
            + " -requiresAny -property productPath");

    [Test]
    public void DevEnvPath_should_take_vswheres_trimmed_output_and_none_for_empty_output()
    {
        VisualStudio.DevEnvPath("C:\\VS\\devenv.exe\r\n").Should().Be("C:\\VS\\devenv.exe");
        VisualStudio.DevEnvPath("").Should().BeNull("vswhere prints nothing when no Visual Studio is installed");
        VisualStudio.DevEnvPath(null).Should().BeNull();
    }

    [Test]
    public void The_texts_are_upstreams()
    {
        string source = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "app", "GitUI", "MessageBoxes.cs"));
        source.Should().Contain($"\"{VisualStudio.RetryOpenText.Replace("\n", "\\n")}\"");
        source.Should().Contain($"_retry = new(\"{VisualStudio.RetryCaption}\")");
    }

    private static string RepositoryRoot([CallerFilePath] string testFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFile)!, "..", "..", ".."));
}
