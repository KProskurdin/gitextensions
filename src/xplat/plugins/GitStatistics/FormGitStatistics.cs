using Avalonia.Controls;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Plugins.GitStatistics;

/// <summary>
///  Stands in for upstream's WinForms <c>FormGitStatistics</c>, which the unchanged plugin class creates and shows.
/// </summary>
public sealed class FormGitStatistics(
    IGitExecutorProvider executorProvider,
    IGitModule module,
    string codeFilePattern,
    bool countSubmodules) : PluginDialog
{
    /// <summary>
    ///  The directory names to leave out, ";"-separated, matched at the end of a directory's path. The plugin writes them with
    ///  Windows separators, as upstream's Windows paths have them; off Windows they are turned back into the platform's.
    /// </summary>
    public string DirectoriesToIgnore { get; set; } = "";

    protected override Window CreateWindow()
        => new StatisticsWindow(executorProvider, module, codeFilePattern, countSubmodules,
            OperatingSystem.IsWindows()
                ? DirectoriesToIgnore
                : DirectoriesToIgnore.Replace('\\', Path.DirectorySeparatorChar));
}
