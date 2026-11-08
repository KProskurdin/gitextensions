using GitCommands;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace CommonTestUtils;

/// <summary>
///  Replaces upstream's attribute of the same name: that one serializes test assemblies with a named
///  <see cref="Semaphore"/>, which is not supported on Unix and makes NUnit fail to load the whole assembly.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class TestAppSettingsAttribute : Attribute, ITestAction
{
    private const string LockFileName = "GitExtensionsTestAssemblySerializer.lock";
    private static readonly TimeSpan _retryDelay = TimeSpan.FromMilliseconds(100);

    // A lock file rather than a named Mutex, because a Mutex must be released by the thread that acquired it
    // and NUnit may run BeforeTest and AfterTest of a suite on different threads.
    private FileStream? _lock;

    public ActionTargets Targets => ActionTargets.Suite;

    public void BeforeTest(ITest test)
    {
        AcquireLock();

        // A test host may run under the shared dotnet runtime rather than a native testhost.exe apphost, in which
        // case Application.ExecutablePath - and therefore AppSettings.GetGitExtensionsDirectory() - points at the
        // dotnet install directory instead of the test output. Pin the path to the test's own directory.
        AppSettings.GetTestAccessor().ApplicationExecutablePath = Path.Combine(AppContext.BaseDirectory, "GitExtensions.exe");

        // Upstream's attribute deletes the file because a test host has its own settings folder on Windows. The stand-in's
        // folder is the user's, so this once deleted the user's settings on macOS; XplatTestUserData moves it, and this checks.
        string settingsFile = AppSettings.SettingsContainer.SettingsCache.SettingsFilePath;
        if (!XplatTestUserData.Contains(settingsFile))
        {
            throw new InvalidOperationException($"Refusing to delete a settings file outside the tests' own folder: {settingsFile}");
        }

        File.Delete(settingsFile);
        AppSettings.SettingsContainer.SettingsCache.Load();

        AppSettings.CheckForUpdates = false;
        AppSettings.ShowAvailableDiffTools = false;

        // Create the settings file so that the SettingsCache does not think it should reload the file again and again
        AppSettings.SettingsContainer.SettingsCache.Save();
    }

    public void AfterTest(ITest test)
    {
        AppSettings.SettingsContainer.SettingsCache.Dispose();

        _lock?.Dispose();
        _lock = null;
    }

    private void AcquireLock()
    {
        string path = Path.Combine(Path.GetTempPath(), LockFileName);
        while (true)
        {
            try
            {
                _lock = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                return;
            }
            catch (IOException)
            {
                // Another test assembly holds the lock.
                Thread.Sleep(_retryDelay);
            }
        }
    }
}
