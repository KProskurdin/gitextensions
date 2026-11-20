using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.Rendering;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

// A minified file is one very long line: AvaloniaEdit's time for a line grows with the square of the parts colored in it,
// so past a limit such a line keeps its own color (DiffTextView.MaxColoredPartsPerLine).
internal sealed class DiffTextViewLongLineTests
{
    private const int ManyParts = 2000;

    [AvaloniaTest]
    public void A_line_with_thousands_of_syntax_runs_keeps_its_own_color_and_a_short_one_keeps_its_runs()
    {
        StringBuilder json = new();
        for (int i = 0; i < ManyParts; i++)
        {
            json.Append($"{{\"key{i}\":[{i},true]}},");
        }

        (Window window, DiffTextView lines) = Show("big.json", "{\"short\": [1, true]}\n" + json);

        Brushes(lines, 1).Should().HaveCountGreaterThan(1, "a short line has its syntax colors");
        lines.ItemsSource[1].Runs.Should().HaveCountGreaterThan(ManyParts);
        Brushes(lines, 2).Should().ContainSingle();
        window.Close();
    }

    [AvaloniaTest]
    public void Thousands_of_occurrences_in_one_line_are_found_but_leave_its_text_color()
    {
        (Window window, DiffTextView lines) = Show("notes.txt",
            "ab cd\n" + string.Concat(Enumerable.Repeat("ab ", ManyParts)));

        lines.Editor.Select(0, 2);
        Dispatcher.UIThread.RunJobs();

        lines.Occurrences.Should().HaveCount(ManyParts + 1);
        Brushes(lines, 1).Should().HaveCountGreaterThan(1, "the first line's one occurrence has the marks' text color");
        Brushes(lines, 2).Should().ContainSingle();
        window.Close();
    }

    private static Color[] Brushes(DiffTextView lines, int lineNumber)
    {
        VisualLine line = lines.Editor.TextArea.TextView.GetOrConstructVisualLine(
            lines.Editor.Document.GetLineByNumber(lineNumber));
        return [.. line.Elements.Select(element => ((ISolidColorBrush)element.TextRunProperties.ForegroundBrush!).Color)
            .Distinct()];
    }

    private static (Window Window, DiffTextView Lines) Show(string name, string text)
    {
        DiffView diff = new();
        Window window = new() { Content = diff, Width = 900, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        diff.ShowText(name, text);
        Dispatcher.UIThread.RunJobs();
        return (window, diff.FindControl<DiffTextView>("DiffList")!);
    }
}
