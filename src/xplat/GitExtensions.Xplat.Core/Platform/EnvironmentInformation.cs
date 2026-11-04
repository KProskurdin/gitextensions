using System.Runtime.InteropServices;
using System.Text;
using GitCommands.Git;

namespace GitExtensions.Xplat.Core.Platform;

/// <summary>
///  Upstream's <c>UserEnvironmentInformation</c> without its WinForms parts: the environment lines FormAbout shows and
///  "Report an issue" copies (version, build, git, OS, .NET, DPI, portable).
/// </summary>
public static class EnvironmentInformation
{
    // Upstream's DpiUtil base: 96 dpi is no scaling.
    private const int StandardDpi = 96;

    // Scalings this close to 1 count as none (Avalonia reports a double).
    private const double ScaleTolerance = 0.001;

    /// <summary>
    ///  Upstream's <c>GetInformation</c>, each line starting with "- ".
    /// </summary>
    /// <param name="productVersion">The application's version (upstream's <c>AppSettings.ProductVersion</c>).</param>
    /// <param name="sha">The commit the application was built from.</param>
    /// <param name="dirty">True when the build had uncommitted changes.</param>
    /// <param name="gitVersion">git's version, or null when git could not be asked.</param>
    /// <param name="scale">The screen's scaling (1 for 96 dpi).</param>
    /// <param name="portable">Upstream's <c>AppSettings.IsPortable()</c>.</param>
    public static string Information(string productVersion, string sha, bool dirty, string? gitVersion, double scale,
        bool portable)
    {
        StringBuilder text = new();
        text.Append("- Git Extensions ").AppendLine(productVersion);
        text.Append("- Build ").Append(sha).AppendLine(dirty ? " (Dirty)" : "");
        text.Append("- Git ")
            .AppendLine(GitVersionInfo(gitVersion, GitVersion.LastSupportedVersion, GitVersion.LastRecommendedVersion));
        text.Append("- ").Append(Environment.OSVersion).AppendLine();
        text.Append("- ").AppendLine(RuntimeInformation.FrameworkDescription);
        text.Append("- DPI ").Append(Math.Round(StandardDpi * scale)).Append("dpi (")
            .Append(Math.Abs(scale - 1) < ScaleTolerance ? "no" : $"{Math.Round(scale * 100)}%")
            .AppendLine(" scaling)");
        text.Append("- Portable: ").Append(portable).AppendLine();
        return text.ToString();
    }

    /// <summary>
    ///  git's version as upstream's <c>GetInformation</c> asks it (<see cref="GitVersion.Current"/>), or null when git
    ///  cannot be asked.
    /// </summary>
    public static string? CurrentGitVersion()
    {
        try
        {
            return GitVersion.Current?.ToString();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    ///  Upstream's <c>GetGitVersionInfo</c>: the version with a note when it is older than the supported or recommended
    ///  one.
    /// </summary>
    public static string GitVersionInfo(string? gitVersion, GitVersion lastSupportedVersion,
        GitVersion recommendedVersion)
    {
        if (string.IsNullOrWhiteSpace(gitVersion))
        {
            return $"- (minimum: {lastSupportedVersion}, recommended: {recommendedVersion})";
        }

        GitVersion actualVersion = new(gitVersion);
        if (actualVersion < lastSupportedVersion)
        {
            return $"{gitVersion} (minimum: {lastSupportedVersion}, please update!)";
        }

        if (actualVersion < recommendedVersion)
        {
            return $"{gitVersion} (recommended: {recommendedVersion} or later)";
        }

        return gitVersion;
    }
}
