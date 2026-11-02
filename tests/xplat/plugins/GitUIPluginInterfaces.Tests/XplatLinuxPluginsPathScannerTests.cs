namespace GitUIPluginInterfaces.Tests;

/// <summary>
///  POSIX counterpart of <c>PluginsPathScannerTests</c>, whose case passes a Windows relative path (see XplatPlatformSkips.cs).
/// </summary>
[Platform(Include = "Linux,MacOsX")]
// Mirrors: PluginsPathScannerTests
internal sealed class XplatLinuxPluginsPathScannerTests
{
    [TestCase("./PathScanningData", "PluginInRootDir.dll", "PluginInOwnDir.dll")]
    public void PathScanning(string userPluginsPath, params string[] expectedFileNames)
    {
        List<FileInfo> pluginFiles =
            [.. PluginsPathScanner.GetFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, userPluginsPath))];

        pluginFiles.Should().HaveCount(expectedFileNames.Length);
        pluginFiles.Select(f => f.Name).Should().Contain(expectedFileNames);
    }
}
