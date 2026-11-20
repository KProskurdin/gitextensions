namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormCommandlineHelp</c>: the command line's usage, shown for <c>help</c> and for any
///  word the command line does not know.
/// </summary>
public sealed class CommandLineHelpViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormCommandlineHelp";

    public string Title => CommandLine.UsageCaption;

    public string Label => CommandLine.UsageLabel;

    public string Commands => CommandLine.UsageCommands;
}
