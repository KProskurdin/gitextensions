using CommonTestUtils;

[assembly: AzureDevOpsIntegrationTests.XplatSkipOnNonWindows]

namespace AzureDevOpsIntegrationTests;

/// <summary>
///  Upstream tests of this assembly that are ignored off Windows (see <see cref="XplatSkipOnNonWindowsAttributeBase"/>).
///  Linux expectations are in <c>XplatLinuxApiClientTests</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class XplatSkipOnNonWindowsAttribute : XplatSkipOnNonWindowsAttributeBase
{
    protected override IReadOnlyList<(string Test, string Reason)> Skips { get; } =
    [
        // ProcessStartInfo.Environment starts from the process's own variables, and a Linux desktop sets DISPLAY.
        ("AzureDevOpsIntegrationTests.ApiClientTests.CreateCredentialFillStartInfo_should_leave_the_ssh_askpass_variables_alone", WindowsEnvironment),
    ];
}
