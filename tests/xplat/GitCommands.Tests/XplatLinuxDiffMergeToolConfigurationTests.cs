using AwesomeAssertions;
using GitCommands.DiffMergeTools;
using GitCommands.Git;
using GitExtensions.Extensibility.Configurations;
using NSubstitute;
using NUnit.Framework;

namespace GitCommandsTests.DiffMergeTools;

/// <summary>
///  POSIX counterpart of the user-supplied-path case in <c>DiffMergeToolConfigurationManagerTests</c>.
/// </summary>
[Platform(Include = "Linux,MacOsX")]
internal sealed class XplatLinuxDiffMergeToolConfigurationTests
{
    [Test]
    public void LoadDiffMergeToolConfig_should_create_tool_config_with_userSuppliedPath_if_tool_unregistered()
    {
        DiffMergeToolConfigurationManager configurationManager = new(() => Substitute.For<IConfigValueStore>());

        DiffMergeToolConfiguration config = configurationManager.LoadDiffMergeToolConfig("bla", "/some/path/to the tool/bla.exe");

        config.Should().NotBeNull();
        config.ExeFileName.Should().Be("bla.exe");
        config.Path.Should().Be("/some/path/to the tool/bla.exe");
        config.DiffCommand.Should().BeEmpty();
        config.MergeCommand.Should().BeEmpty();
    }
}
