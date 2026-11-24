using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AzureDevOpsIntegration;

namespace AzureDevOpsIntegrationTests;

internal sealed class XplatAzureDevOpsAuthTests
{
    private const string AccessToken = "eyJ0eXAiOiJKV1QifQ.eyJzdWIiOiJ1c2VyIn0.c2lnbmF0dXJl";
    private const string PersonalAccessToken = "abcdefghijklmnopqrstuvwxyz234567abcdefghijklmnopqrst";

    [Test]
    public void ForStoredCredential_should_send_an_access_token_as_bearer()
    {
        AuthenticationHeaderValue header = XplatAzureDevOpsAuth.ForStoredCredential(AccessToken);

        header.Scheme.Should().Be("Bearer");
        header.Parameter.Should().Be(AccessToken);
    }

    [Test]
    public void ForStoredCredential_should_send_a_personal_access_token_as_basic()
    {
        AuthenticationHeaderValue header = XplatAzureDevOpsAuth.ForStoredCredential(PersonalAccessToken);

        header.Scheme.Should().Be("Basic");
        Encoding.ASCII.GetString(Convert.FromBase64String(header.Parameter!)).Should().Be($":{PersonalAccessToken}");
    }

    [TestCase(HttpStatusCode.NonAuthoritativeInformation, "text/html")]
    [TestCase(HttpStatusCode.NonAuthoritativeInformation, "application/json")]
    [TestCase(HttpStatusCode.OK, "text/html")]
    public void EnsureJsonResponse_should_report_the_sign_in_page_as_unauthorized(HttpStatusCode status, string mediaType)
    {
        using HttpResponseMessage response = Response(status, mediaType);

        ((Action)(() => XplatAzureDevOpsAuth.EnsureJsonResponse(response))).Should().Throw<UnauthorizedAccessException>();
    }

    [Test]
    public void EnsureJsonResponse_should_accept_json()
    {
        using HttpResponseMessage response = Response(HttpStatusCode.OK, "application/json");

        ((Action)(() => XplatAzureDevOpsAuth.EnsureJsonResponse(response))).Should().NotThrow();
    }

    [Test]
    public void EnsureJsonResponse_should_fail_on_an_error_status()
    {
        using HttpResponseMessage response = Response(HttpStatusCode.InternalServerError, "text/html");

        ((Action)(() => XplatAzureDevOpsAuth.EnsureJsonResponse(response))).Should().Throw<HttpRequestException>();
    }

    private static HttpResponseMessage Response(HttpStatusCode status, string mediaType)
        => new(status) { Content = new StringContent("\r\n<!DOCTYPE html>", Encoding.UTF8, mediaType) };
}
