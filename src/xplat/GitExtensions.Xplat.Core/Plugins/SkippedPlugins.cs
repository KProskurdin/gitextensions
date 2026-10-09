using System.Reflection;
using System.Runtime.Loader;
using GitCommands;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.Core.Plugins;

/// <summary>
///  A plugin file in one of the plugin folders that upstream's loader passed over, with the reason.
/// </summary>
public sealed record SkippedPlugin(string FileName, string Path, string Reason)
{
    public override string ToString() => FileName;
}

/// <summary>
///  Finds the plugin files that upstream's <see cref="ManagedExtensibility"/> skips. It skips a file whose types cannot all
///  be resolved and only writes a trace line, so a plugin built for the WinForms app (it references
///  <c>System.Windows.Forms</c>, which the cross-platform app does not have) or for another plugin interface version would
///  silently be missing; the settings show these files with the reason instead (PLAN.md, section 9, question 5).
/// </summary>
public static class SkippedPlugins
{
    private const string WinFormsAssembly = "System.Windows.Forms";

    // Upstream's naming rules: bundled plugins are GitExtensions.Plugins.*.dll, user plugins GitExtensions.*.dll.
    private const string BundledPrefix = "GitExtensions.Plugins.";
    private const string UserPrefix = "GitExtensions.";

    /// <summary>
    ///  The skipped files among the ones upstream's loader looks at: the Plugins folder next to the app and the user's
    ///  plugins folder.
    /// </summary>
    public static IReadOnlyList<SkippedPlugin> Find()
        => Find([
            .. PluginsPathScanner.GetFiles(System.IO.Path.Join(AppContext.BaseDirectory, "Plugins"))
                .Where(file => file.Name.StartsWith(BundledPrefix, StringComparison.Ordinal)),
            .. PluginsPathScanner.GetFiles(AppSettings.UserPluginsPath)
                .Where(file => file.Name.StartsWith(UserPrefix, StringComparison.Ordinal)),
        ]);

    /// <summary>
    ///  Repeats upstream's check on each file: it loads the assembly and resolves all its types.
    /// </summary>
    public static IReadOnlyList<SkippedPlugin> Find(IEnumerable<FileInfo> files)
    {
        List<SkippedPlugin> skipped = [];
        foreach (FileInfo file in files)
        {
            try
            {
                _ = AssemblyLoadContext.Default.LoadFromAssemblyPath(file.FullName).GetTypes();
            }
            catch (Exception ex)
            {
                skipped.Add(new SkippedPlugin(file.Name, file.FullName, Reason(ex)));
            }
        }

        return skipped;
    }

    /// <summary>
    ///  Says why a plugin could not be loaded, naming the assemblies it needs that are missing.
    /// </summary>
    public static string Reason(Exception exception)
    {
        IEnumerable<Exception> causes = exception is ReflectionTypeLoadException typeLoad
            ? typeLoad.LoaderExceptions.OfType<Exception>()
            : [exception];
        List<string> missing = [.. causes.OfType<FileNotFoundException>()
            .Select(cause => cause.FileName is { } name ? new AssemblyName(name).Name ?? name : null)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)];
        if (missing.Contains(WinFormsAssembly, StringComparer.OrdinalIgnoreCase))
        {
            return "Built for Git Extensions for Windows: it uses Windows Forms, which this version does not have.";
        }

        if (missing.Count > 0)
        {
            return $"It needs {string.Join(", ", missing)}, which this version does not have.";
        }

        return causes.FirstOrDefault()?.Message ?? exception.Message;
    }
}
