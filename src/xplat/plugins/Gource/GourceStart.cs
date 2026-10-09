using Avalonia.Controls;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Plugins.Gource;

/// <summary>
///  Stands in for upstream's WinForms <c>GourceStart</c>, which the unchanged plugin class creates and shows, then reads the
///  path and arguments from to save them.
/// </summary>
public sealed class GourceStart(string pathToGource, GitUIEventArgs gitUIArgs, string gourceArguments) : PluginDialog
{
    public string PathToGource { get; set; } = pathToGource;

    public string? GitWorkingDir { get; set; } = gitUIArgs.GitModule.WorkingDir;

    public string GourceArguments { get; set; } = gourceArguments;

    protected override Window CreateWindow()
        => new GourceStartWindow(this, gitUIArgs.GitModule);
}
