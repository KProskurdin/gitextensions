using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using GitExtensions.Xplat.Core.Platform;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  Makes <see cref="Path.GetTempPath"/> return the real folder. On macOS it is below <c>/var</c>, a link to
///  <c>/private/var</c>, and git prints the real path, so a test comparing a repository's path with git's output would
///  compare two names of one folder. <c>SymlinkedRepositoryTests</c> covers a repository opened through a link on purpose.
/// </summary>
internal static class TestTemporaryFolder
{
    private const string TemporaryFolderVariable = "TMPDIR";

    [ModuleInitializer]
    [SuppressMessage("Usage", "CA2255:The 'ModuleInitializer' attribute should not be used in libraries", Justification = "Every test and static initializer reads the folder, so this must run before all of them.")]
    internal static void UseRealPath()
    {
        if (!OperatingSystem.IsWindows())
        {
            Environment.SetEnvironmentVariable(TemporaryFolderVariable, RealPath.Resolve(Path.GetTempPath()));
        }
    }
}
