using AwesomeAssertions;
using GitCommands.DiffMergeTools;
using GitCommands.Git;
using GitExtensions.Extensibility.Configurations;
using NSubstitute;
using NUnit.Framework;

namespace GitCommandsTests.DiffMergeTools;

/// <summary>
///  POSIX counterparts of the user-supplied-path and found-on-PATH cases in <c>DiffMergeToolConfigurationManagerTests</c>.
/// </summary>
[Platform(Include = "Linux,MacOsX")]
// Mirrors: DiffMergeToolConfigurationManagerTests
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

    // Upstream looks for notepad.exe. The ".exe" is added on every OS, so the stand-in needs that name too.
    [Test]
    [NonParallelizable]
    public void LoadDiffMergeToolConfig_should_create_tool_config_if_tool_unregistered_but_exists_path()
    {
        const string pathVariable = "PATH";
        string folder = Directory.CreateTempSubdirectory("xplat-difftool-").FullName;
        string? originalPath = Environment.GetEnvironmentVariable(pathVariable);
        try
        {
            File.WriteAllText(Path.Combine(folder, "xplattool.exe"), string.Empty);
            Environment.SetEnvironmentVariable(pathVariable, $"{folder}{Path.PathSeparator}{originalPath}");
            DiffMergeToolConfigurationManager configurationManager = new(() => Substitute.For<IConfigValueStore>());

            DiffMergeToolConfiguration config = configurationManager.LoadDiffMergeToolConfig("xplattool", null);

            config.Should().NotBeNull();
            config.ExeFileName.Should().Be("xplattool.exe");
            config.Path.Should().EndWith("/xplattool.exe");
            config.DiffCommand.Should().BeEmpty();
            config.MergeCommand.Should().BeEmpty();
        }
        finally
        {
            Environment.SetEnvironmentVariable(pathVariable, originalPath);
            Directory.Delete(folder, recursive: true);
        }
    }
}
