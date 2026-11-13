using System.Text.RegularExpressions;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  What a WinForms form gets from its designer, checked for every window that reimplements one (each row of
///  <c>docs/xplat/PORTING-MAP.md</c> that names an upstream form and a window): Enter presses the form's
///  <c>AcceptButton</c>, Escape its <c>CancelButton</c>, and the form keeps its <c>MinimumSize</c>. A window may set its default
///  button in code, as upstream switches FormRebase's <c>AcceptButton</c> during a rebase.
/// </summary>
internal sealed partial class DialogKeysParityTests
{
    private const string PortingMap = "docs/xplat/PORTING-MAP.md";
    private const string AppFolder = "src/xplat/GitExtensions.Xplat.App";

    [Test]
    public void Every_ported_form_with_an_AcceptButton_has_a_default_button_or_handles_Enter()
    {
        List<string> missing =
        [
            .. Ports()
                .Where(port => FormAssigns(port.Designer, "AcceptButton") && !port.Markup.Contains("IsDefault=\"True\"")
                                                                          && !port.Code.Contains(".IsDefault =",
                                                                              StringComparison.Ordinal)
                                                                          && !Regex.IsMatch(port.Code,
                                                                              @"Key\.(Enter|Return)\b"))
                .Select(port => port.ToString())
        ];

        missing.Should().BeEmpty();
    }

    [Test]
    public void Every_ported_form_with_a_CancelButton_has_a_cancel_button_or_handles_Escape()
    {
        List<string> missing =
        [
            .. Ports()
                .Where(port => FormAssigns(port.Designer, "CancelButton") && !port.Markup.Contains("IsCancel=\"True\"")
                                                                          && !port.Code.Contains(".IsCancel =",
                                                                              StringComparison.Ordinal)
                                                                          && !port.Code.Contains("Key.Escape",
                                                                              StringComparison.Ordinal))
                .Select(port => port.ToString())
        ];

        missing.Should().BeEmpty();
    }

    [Test]
    public void Every_ported_form_with_a_MinimumSize_has_a_minimum_size()
    {
        List<string> missing =
        [
            .. Ports()
                .Where(port =>
                    FormMinimumSize().IsMatch(port.Designer) && !WindowMinimumSize().IsMatch(WindowTag(port.Markup)))
                .Select(port => port.ToString())
        ];

        missing.Should().BeEmpty();
    }

    [Test]
    public void The_check_finds_the_ported_dialogs()
    {
        Ports().Select(port => port.Window).Should()
            .Contain(["DeleteBranchWindow", "AvailableEncodingsWindow", "PullWindow"]);
    }

    // A form-level assignment in InitializeComponent: no control name before it. "CancelButton = null" (FormStash) is none.
    private static bool FormAssigns(string designer, string property)
        => Regex.IsMatch(designer, $@"^\s+(this\.)?{property} = (?!null;)", RegexOptions.Multiline);

    // The window's own tag, which may span several lines.
    private static string WindowTag(string markup) => WindowTagRegex().Match(markup).Groups[1].Value;

    private static IEnumerable<Port> Ports()
    {
        string root = RepositoryRoot();
        Dictionary<string, string> windows = Directory
            .EnumerateFiles(Path.Combine(root, AppFolder), "*Window.axaml", SearchOption.AllDirectories)
            .ToDictionary(file => Path.GetFileNameWithoutExtension(file), StringComparer.Ordinal);
        HashSet<string> seen = [];
        foreach (string row in File.ReadLines(Path.Combine(root, PortingMap))
                     .Where(line => line.StartsWith("| `src/app", StringComparison.Ordinal)))
        {
            string[] cells = row.Split('|');
            Match upstream = UpstreamFormRegex().Match(cells[1]);
            string? window = WindowRegex().Matches(cells[2]).Select(match => match.Groups[1].Value)
                .FirstOrDefault(name => name != "MainWindow" && windows.ContainsKey(name));
            if (!upstream.Success || window is null)
            {
                continue;
            }

            string designer = Path.Combine(root, upstream.Value[..^".cs".Length] + ".Designer.cs");
            if (!File.Exists(designer) || !seen.Add(designer + window))
            {
                continue;
            }

            string markup = windows[window];
            string code = string.Concat(Directory.EnumerateFiles(Path.GetDirectoryName(markup)!, window + "*.cs")
                .Select(File.ReadAllText));
            yield return new Port(Path.GetFileName(upstream.Value), window, File.ReadAllText(designer),
                File.ReadAllText(markup), code);
        }
    }

    private static string RepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string here = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", ".."));

    [GeneratedRegex(@"src/app/[\w/]+/(?:Form\w+|\w+Dialog\w*)\.cs")]
    private static partial Regex UpstreamFormRegex();

    [GeneratedRegex(@"(\w+Window)\.axaml")]
    private static partial Regex WindowRegex();

    [GeneratedRegex(@"^\s+(?:this\.)?MinimumSize = new System\.Drawing\.Size\(\d+, \d+\)", RegexOptions.Multiline)]
    private static partial Regex FormMinimumSize();

    [GeneratedRegex(@"Min(Width|Height)=")]
    private static partial Regex WindowMinimumSize();

    [GeneratedRegex(@"^<Window(.*?)>", RegexOptions.Singleline | RegexOptions.Multiline)]
    private static partial Regex WindowTagRegex();

    private sealed record Port(string Form, string Window, string Designer, string Markup, string Code)
    {
        public override string ToString() => $"{Form} -> {Window}";
    }
}
