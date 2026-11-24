using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace AzureDevOpsIntegration;

/// <summary>
///  Fork-side authentication for upstream's <see cref="ApiClient"/>, applied by the seam S9 patches in the shadow project
///  (<c>docs/xplat/PLAN.md</c> section 5).
/// </summary>
/// <remarks>
///  Upstream sends whatever <c>git credential fill</c> returns as a Bearer token, which only holds for the OAuth access token
///  Git Credential Manager can store on Windows. Off Windows, git's own helpers (<c>osxkeychain</c>, <c>libsecret</c>) return
///  what the user typed for the remote, a personal access token. Azure DevOps does not accept that as a Bearer token: it
///  answers as for an anonymous request, with its sign-in page and a success status, and upstream then fails to parse the
///  HTML as JSON.
/// </remarks>
internal static class XplatAzureDevOpsAuth
{
    private const string JsonWebTokenStart = "eyJ";
    private const int JsonWebTokenDotCount = 2;

    /// <summary>
    ///  Gets the authorization header for a password a git credential helper returned for the project's host.
    /// </summary>
    /// <param name="password">The password from <c>git credential fill</c>.</param>
    /// <returns>Bearer for an OAuth access token, as upstream sends; Basic, as git itself sends it, for anything else.</returns>
    public static AuthenticationHeaderValue ForStoredCredential(string password)
    {
        if (IsJsonWebToken(password))
        {
            return new AuthenticationHeaderValue("Bearer", password);
        }

        return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{password}")));
    }

    /// <summary>
    ///  Checks a response of the REST API before its content is read as JSON.
    /// </summary>
    /// <param name="response">The response to check.</param>
    /// <exception cref="UnauthorizedAccessException">
    ///  Azure DevOps answered with its sign-in page, as it does for a request it did not authenticate. The adapter then
    ///  offers to open the settings, where a personal access token can be entered.
    /// </exception>
    /// <exception cref="HttpRequestException">The response has an error status.</exception>
    public static void EnsureJsonResponse(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.NonAuthoritativeInformation
            || (response.IsSuccessStatusCode && response.Content.Headers.ContentType?.MediaType == "text/html"))
        {
            throw new UnauthorizedAccessException();
        }

        response.EnsureSuccessStatusCode();
    }

    private static bool IsJsonWebToken(string token)
        => token.StartsWith(JsonWebTokenStart, StringComparison.Ordinal)
           && token.Count(c => c == '.') == JsonWebTokenDotCount;
}
