using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace CommonTestUtils;

/// <summary>
///  Marks upstream tests as ignored, with a reason, when they run on a platform they assert Windows semantics for.
///  Ignored tests are listed in the test report, so the gap stays visible. The upstream test files are not edited.
/// </summary>
/// <remarks>
///  Each shadow test project derives one assembly-level attribute from this in its <c>XplatPlatformSkips.cs</c>.
///  <c>GitCommands.Tests</c> keeps its own copy, which <c>xplat-drift.sh</c> reads.
/// </remarks>
public abstract class XplatSkipOnNonWindowsAttributeBase : Attribute, ITestAction
{
    public const string WindowsPathInput = "Windows path syntax as input (drive letters, backslashes, UNC); '\\' is an ordinary filename character on this OS";
    public const string WindowsGdi = "Calls GDI+ or user32 through System.Drawing or P/Invoke, which exist on Windows only; the cross-platform UI does not use these types";
    public const string WindowsEnvironment = "Assumes the environment of a Windows session; a Linux desktop session sets variables (such as DISPLAY) that the process inherits";

    /// <summary>
    ///  Gets the skipped tests: a full method name (namespace, class and method, no parameters), or a full class name to
    ///  skip every test of the class, each with the reason shown in the report.
    /// </summary>
    protected abstract IReadOnlyList<(string Test, string Reason)> Skips { get; }

    public ActionTargets Targets => ActionTargets.Test;

    public void BeforeTest(ITest test)
    {
        if (OperatingSystem.IsWindows() || test.Method is null)
        {
            return;
        }

        // FullName carries the parameter list; the method identity is everything before it.
        string method = test.FullName.Split('(')[0];
        string? type = test.ClassName;
        foreach ((string skipped, string reason) in Skips)
        {
            if (method == skipped || type == skipped)
            {
                throw new IgnoreException($"Not run on this platform: {reason}.");
            }
        }
    }

    public void AfterTest(ITest test)
    {
    }
}
