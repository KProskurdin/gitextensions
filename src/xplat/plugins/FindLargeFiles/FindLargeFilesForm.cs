using Avalonia.Controls;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Plugins.FindLargeFiles;

/// <summary>
///  Stands in for upstream's WinForms <c>FindLargeFilesForm</c>, which the unchanged plugin class creates and shows.
/// </summary>
public sealed class FindLargeFilesForm(float threshold, IGitUICommands? commands) : PluginDialog
{
    protected override Window CreateWindow()
        => new FindLargeFilesWindow(threshold, commands ?? throw new InvalidOperationException("No repository is open."));
}
