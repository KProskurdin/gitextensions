using CommonTestUtils;

[assembly: GitExtensions.ExtensibilityTests.XplatSkipOnNonWindows]

namespace GitExtensions.ExtensibilityTests;

/// <summary>
///  Upstream tests of this assembly that are ignored off Windows (see <see cref="XplatSkipOnNonWindowsAttributeBase"/>).
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class XplatSkipOnNonWindowsAttribute : XplatSkipOnNonWindowsAttributeBase
{
    // FontParser builds System.Drawing.Font objects; the cross-platform UI parses the same text with Core's FontSetting.
    protected override IReadOnlyList<(string Test, string Reason)> Skips { get; } =
    [
        ("GitUIPluginInterfacesTests.FontParserTests", WindowsGdi),
    ];
}
