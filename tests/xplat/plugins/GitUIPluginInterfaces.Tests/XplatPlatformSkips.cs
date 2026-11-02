using CommonTestUtils;

[assembly: GitUIPluginInterfacesTests.XplatSkipOnNonWindows]

namespace GitUIPluginInterfacesTests;

/// <summary>
///  Upstream tests of this assembly that are ignored off Windows (see <see cref="XplatSkipOnNonWindowsAttributeBase"/>).
///  Linux expectations are in <c>XplatLinuxPluginsPathScannerTests</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class XplatSkipOnNonWindowsAttribute : XplatSkipOnNonWindowsAttributeBase
{
    protected override IReadOnlyList<(string Test, string Reason)> Skips { get; } =
    [
        // The case passes @".\PathScanningData".
        ("GitUIPluginInterfaces.Tests.PluginsPathScannerTests.PathScanning", WindowsPathInput),
    ];
}
