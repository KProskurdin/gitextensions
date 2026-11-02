using System.Diagnostics;
using AzureDevOpsIntegration;

namespace AzureDevOpsIntegrationTests;

/// <summary>
///  POSIX counterpart of <c>ApiClientTests.CreateCredentialFillStartInfo_should_leave_the_ssh_askpass_variables_alone</c>
///  (see XplatPlatformSkips.cs): the start info may inherit the variables, but must not set them itself.
/// </summary>
[Platform(Include = "Linux,MacOsX")]
// Mirrors: ApiClientTests
internal sealed class XplatLinuxApiClientTests
{
    [TestCase("SSH_ASKPASS")]
    [TestCase("SSH_ASKPASS_REQUIRE")]
    [TestCase("DISPLAY")]
    public void CreateCredentialFillStartInfo_should_leave_the_ssh_askpass_variables_alone(string name)
    {
        ProcessStartInfo startInfo = ApiClient.CreateCredentialFillStartInfo(gitExecutable: null);

        startInfo.Environment.TryGetValue(name, out string? value);
        value.Should().Be(Environment.GetEnvironmentVariable(name));
    }
}
