using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace CommonTestUtils;

/// <summary>
///  Gives a test process its own application data folder, before any test or static initializer can read the settings.
///  Without it, tests read and write the settings of the user running them, and upstream's attribute deletes them. This file
///  is compiled into every test assembly under <c>tests/xplat</c> (<c>Directory.Build.props</c>), so a new project cannot
///  forget it.
/// </summary>
internal static class XplatTestUserData
{
    // The same name as the shim's Application.UserDataFolderVariable; XplatTestUserDataTests pins it. Not every test project
    // references the shim.
    private const string UserDataFolderVariable = "GITEXTENSIONS_XPLAT_USER_DATA";

    public static bool Contains(string path)
        => Environment.GetEnvironmentVariable(UserDataFolderVariable) is { Length: > 0 } folder
            && Path.GetFullPath(path).StartsWith(Path.GetFullPath(folder) + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    [ModuleInitializer]
    [SuppressMessage("Usage", "CA2255:The 'ModuleInitializer' attribute should not be used in libraries", Justification = "It must run before anything in the test process reads the settings.")]
    internal static void UseOwnFolder()
    {
        // Set already by another module of this process, or on purpose by whoever started it.
        if (Environment.GetEnvironmentVariable(UserDataFolderVariable) is { Length: > 0 })
        {
            return;
        }

        string folder = Path.Combine(Path.GetTempPath(), "xplat-test-user-data-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable(UserDataFolderVariable, folder);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => DeleteFolder(folder);
    }

    private static void DeleteFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A process still running in the folder keeps it; it is in the temporary folder, so the OS removes it in time.
        }
    }
}
