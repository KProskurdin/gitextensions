using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using EnvDTE;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtUtils;

namespace GitExtensions.Xplat.Core.Platform;

/// <summary>
///  Upstream's <c>VisualStudioIntegration</c> (Windows only), for the file list menu's "Open in Visual Studio": the
///  installed Visual Studio found once with <c>vswhere</c>, and a file opened in a running instance whose solution holds
///  it (at a line, brought to the front), else in a new instance.
/// </summary>
[SupportedOSPlatform("windows")]
public static class VisualStudio
{
    // Upstream's texts (MessageBoxes).
    public const string RetryOpenText =
        "Visual Studio failed to open the file.\nThis can be caused by an opened dialog window or other pending operations.\nTry again?";
    public const string RetryCaption = "Retry";

    private const uint EFail = 0x8000_4005;
    private const uint RpcECallRejected = 0x8001_0001;

    // Display names of Visual Studio's DTE objects in the running object table, e.g. "!VisualStudio.DTE.16.0:73424".
    private const string RunningObjectPrefix = "!VisualStudio";

    private static Task<string?>? _devEnvPath;

    /// <summary>
    ///  Upstream's <c>vswhere</c> arguments: the latest Visual Studio, prereleases included, of any of the three editions,
    ///  by its <c>devenv</c> path.
    /// </summary>
    public static ArgumentString VswhereArguments { get; } = new ArgumentBuilder
    {
        "-prerelease",
        "-latest",
        "-requires Microsoft.VisualStudio.Product.Enterprise",
        "-requires Microsoft.VisualStudio.Product.Professional",
        "-requires Microsoft.VisualStudio.Product.Community",
        "-requiresAny",
        "-property productPath"
    };

    /// <summary>
    ///  Upstream's <c>IsVisualStudioInstalled</c>: false until the search started by <see cref="InitAsync"/> found
    ///  <c>devenv</c>.
    /// </summary>
    public static bool IsInstalled => _devEnvPath is { IsCompletedSuccessfully: true, Result: not null };

    /// <summary>
    ///  Upstream's <c>Init</c> (its static constructor): starts the search for <c>devenv</c> once.
    /// </summary>
    public static Task<string?> InitAsync() => _devEnvPath ??= Task.Run(FindDevEnvAsync);

    /// <summary>
    ///  Upstream's <c>DevEnvPath</c> from <c>vswhere</c>'s output: null when it printed nothing (upstream takes the empty
    ///  output as a path).
    /// </summary>
    public static string? DevEnvPath(string? vswhereOutput)
        => string.IsNullOrWhiteSpace(vswhereOutput) ? null : vswhereOutput.Trim();

    /// <summary>
    ///  Upstream's <c>OpenFile</c>: <paramref name="filePath"/> in a running instance that has it, at
    ///  <paramref name="lineNumber"/> (0 for none), else in a new instance. While an instance rejects the call (a dialog
    ///  is open in it), <paramref name="confirmRetry"/> asks whether to try again. Call it on the UI thread, which the
    ///  running instances' objects need.
    /// </summary>
    public static async Task OpenFileAsync(string filePath, int lineNumber, Func<Task<bool>> confirmRetry)
    {
        while (true)
        {
            try
            {
                if (TryOpenFileInRunningInstance(filePath, lineNumber))
                {
                    return;
                }

                break;
            }
            catch (COMException exception) when ((uint)exception.HResult == RpcECallRejected)
            {
                if (!await confirmRetry())
                {
                    return;
                }
            }
        }

        // Upstream passes the path unquoted, which splits a path with spaces.
        if (await InitAsync() is { } devEnv)
        {
            using IProcess process = new Executable(devEnv).Start(filePath.Quote());
        }
    }

    private static async Task<string?> FindDevEnvAsync()
    {
        string vswhere = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Microsoft Visual Studio", "Installer", "vswhere.exe");
        return File.Exists(vswhere) ? DevEnvPath(await new Executable(vswhere).GetOutputAsync(VswhereArguments)) : null;
    }

    // Upstream's TryOpenFileInRunningInstanceAsync after its switch to the UI thread.
    private static bool TryOpenFileInRunningInstance(string filePath, int lineNumber)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        foreach (DTE dte in RunningInstances())
        {
            if (dte.Solution.FindProjectItem(filePath) is null)
            {
                continue;
            }

            dte.ExecuteCommand("File.OpenFile", filePath);
            if (lineNumber > 0)
            {
                try
                {
                    dte.ExecuteCommand("Edit.GoTo", lineNumber.ToString());
                }
                catch (COMException exception) when ((uint)exception.HResult == EFail)
                {
                    System.Diagnostics.Debug.WriteLine(exception);
                }
            }

            NativeMethods.SetForegroundWindow(dte.MainWindow.HWnd);
            return true;
        }

        return false;
    }

    // Upstream's GetVisualStudioInstances: the running object table's Visual Studio objects.
    private static IEnumerable<DTE> RunningInstances()
    {
        if (NativeMethods.GetRunningObjectTable(0, out IRunningObjectTable table) != 0)
        {
            yield break;
        }

        table.EnumRunning(out IEnumMoniker monikers);
        IMoniker[] moniker = new IMoniker[1];
        while (monikers.Next(1, moniker, pceltFetched: IntPtr.Zero) == 0)
        {
            NativeMethods.CreateBindCtx(reserved: 0, out IBindCtx bindContext);
            string? displayName = null;
            try
            {
                moniker[0].GetDisplayName(bindContext, pmkToLeft: null, out displayName);
            }
            catch (UnauthorizedAccessException)
            {
                // Some objects of the table need elevated permissions.
            }

            if (displayName?.StartsWith(RunningObjectPrefix, StringComparison.Ordinal) == true)
            {
                table.GetObject(moniker[0], out object instance);
                if (instance is DTE dte)
                {
                    yield return dte;
                }
            }
        }
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hwnd);

        [DllImport("ole32.dll")]
        public static extern void CreateBindCtx(int reserved, out IBindCtx ppbc);

        [DllImport("ole32.dll")]
        public static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable prot);
    }
}
