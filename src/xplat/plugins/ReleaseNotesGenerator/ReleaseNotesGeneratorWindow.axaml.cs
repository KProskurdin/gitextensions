using System.Net;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Ui;
using GitExtUtils;
using TextBox = Avalonia.Controls.TextBox;

namespace GitExtensions.Plugins.ReleaseNotesGenerator;

/// <summary>
///  The new shell's version of upstream's <c>ReleaseNotesGeneratorForm</c>: runs <c>git log</c> over a commit range with the
///  given arguments, parses the "hash@message" lines with upstream's <see cref="GitLogLineParser"/>, and copies them to the
///  clipboard as text tables or as an HTML table.
/// </summary>
public partial class ReleaseNotesGeneratorWindow : Window
{
    private const string Category = "ReleaseNotesGeneratorForm";
    private const string MostRecentHint = "most recent changes are listed on top";
    private static string CommitLogFrom => UpstreamTranslation.Text(Category, "_commitLogFrom", "Commit log from '{0}' to '{1}' ({2}):");
    private static string FromCommitNotSpecified => UpstreamTranslation.Text(Category, "_fromCommitNotSpecified", "'From' commit must be specified");
    private static string ToCommitNotSpecified => UpstreamTranslation.Text(Category, "_toCommitNotSpecified", "'To' commit must be specified");
    private static string InvalidInput => UpstreamTranslation.Text(Category, "_caption", "Invalid input");

    private readonly IGitModule _module;
    private readonly GitLogLineParser _parser = new();
    private IReadOnlyList<LogLine> _logLines = [];

    public ReleaseNotesGeneratorWindow(IGitModule module)
    {
        _module = module;
        InitializeComponent();
        UpstreamTranslation.Apply(this, Category);
        buttonGenerate.Click += (_, _) => Generate();
        buttonCopyOrigOutput.Click += (_, _) => _ = CopyTextAsync(ResultBox.Text ?? "");
        buttonCopyAsTextTableTab.Click += (_, _) => _ = CopyTextAsync(CreateTextTable(separateWithTabs: true));
        buttonCopyAsTextTableSpace.Click += (_, _) => _ = CopyTextAsync(CreateTextTable(separateWithTabs: false));
        buttonCopyAsHtml.Click += (_, _) => _ = CopyHtmlAsync();
    }

    /// <summary>
    ///  The text the last copy button put on the clipboard (the HTML code for the HTML table).
    /// </summary>
    public string? CopiedText { get; private set; }

    // Upstream's buttonGenerate_Click.
    private void Generate()
    {
        ResultBox.Text = string.Empty;
        if (!IsGiven(FromBox, FromCommitNotSpecified) || !IsGiven(ToBox, ToCommitNotSpecified))
        {
            return;
        }

        GitArgumentBuilder args = new("log")
        {
            string.Format(LogArgumentsBox.Text ?? "", FromBox.Text, ToBox.Text),
        };
        string result = _module.GitExecutable.GetOutput(args).ReplaceLineEndings();
        ResultBox.Text = result;
        try
        {
            _logLines = [.. _parser.Parse(result.Split(Environment.NewLine))];
            labelRevCount.Text = _logLines.Count.ToString();
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException)
        {
            _logLines = [];
            labelRevCount.Text = "n/a";
        }

        groupBoxCopy.IsEnabled = _logLines.Count > 0;
    }

    private bool IsGiven(TextBox box, string message)
    {
        if (!string.IsNullOrWhiteSpace(box.Text))
        {
            return true;
        }

        MessageBoxes.ShowError(new WindowOwner(this), message, InvalidInput);
        box.Focus();
        return false;
    }

    // Upstream's CreateTextTable: the hash, then the message lines, each further line indented to the message column.
    private string CreateTextTable(bool separateWithTabs)
    {
        string header = string.Format(CommitLogFrom, FromBox.Text, ToBox.Text, MostRecentHint);
        string firstSeparator = separateWithTabs ? "\t" : " ";
        string restSeparator = separateWithTabs ? "\t" : "        ";

        StringBuilder table = new();
        foreach (LogLine line in _logLines)
        {
            string message = string.Join(Environment.NewLine + restSeparator,
                line.MessageLines.Where(text => !string.IsNullOrWhiteSpace(text)));
            table.Append(line.Commit).Append(firstSeparator).Append(message).Append(Environment.NewLine);
        }

        return header + Environment.NewLine + table;
    }

    private string CreateHtmlTable()
    {
        StringBuilder html = new("<table>\r\n");
        foreach (LogLine line in _logLines)
        {
            string message = string.Join("<br/>", line.MessageLines.Select(WebUtility.HtmlEncode));
            html.Append($"<tr>\r\n  <td>{line.Commit}</td>\r\n  <td>{message}</td>\r\n</tr>\r\n");
        }

        return html.Append("</table>").ToString();
    }

    private async Task CopyTextAsync(string text)
    {
        CopiedText = text;
        if (Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    private async Task CopyHtmlAsync()
    {
        string html = $"<p>Commit log from '{FromBox.Text}' to '{ToBox.Text}' ({MostRecentHint}):</p>" + CreateHtmlTable();
        CopiedText = html;
        if (Clipboard is { } clipboard)
        {
            await clipboard.SetDataAsync(HtmlClipboard.Create(html));
        }
    }
}
