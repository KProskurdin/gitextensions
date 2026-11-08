using Avalonia.Controls;
using Avalonia.Input;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Ui;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

// Upstream's blame text is a FileViewer, so it has the viewer's find and go to line. The rows here select whole lines, so an
// occurrence found selects its line, without a mark in it, and "Find & highlight all" is left out.
public partial class BlameView
{
    // Upstream's message caption of a search from the viewer.
    private const string NoCaption = " ";

    private FindWindow? _findWindow;
    private (int Offset, int Length)? _found;

    /// <summary>
    ///  The find window, once it was asked for.
    /// </summary>
    public FindWindow? FindWindow => _findWindow;

    private string SearchText => string.Join('\n', _lines.Select(line => line.Content));

    private bool RunFindHotkey(KeyEventArgs e)
    {
        switch (Hotkeys.Diff.Match(e))
        {
            // Upstream's replace item finds in a viewer that cannot be edited.
            case DiffCommand.Find:
            case DiffCommand.Replace:
                if (TopLevel.GetTopLevel(this) is Window owner)
                {
                    (_findWindow ??= NewFindWindow()).ShowFor(owner, _filePath, selectionOnly: false);
                }

                return true;
            case DiffCommand.FindNextOrOpenWithDifftool when _findWindow is { LookFor.Length: > 0 }:
                RunFind(searchBackward: false);
                return true;
            case DiffCommand.FindPrevious when _findWindow is { LookFor.Length: > 0 }:
                RunFind(searchBackward: true);
                return true;
            case DiffCommand.GoToLine:
                UiActions.Run(GoToLineAsync, _ => { });
                return true;
            default:
                return false;
        }
    }

    private FindWindow NewFindWindow()
    {
        FindWindow window = new() { CanHighlightAll = false };
        window.FindNextRequested += (_, backward) => RunFind(backward);
        window.Hidden += (_, _) => _found = null;
        return window;
    }

    private void RunFind(bool searchBackward) => UiActions.Run(() => FindNextAsync(searchBackward), _ => { });

    // Upstream's FindNextAsync: after the occurrence found last while its line is still selected, else at the selected line.
    private async Task FindNextAsync(bool searchBackward)
    {
        FindWindow window = _findWindow ??= NewFindWindow();
        if (window.LookFor.Length == 0)
        {
            await ShowMessageAsync(window, UpstreamTranslation.ByText(DiffView.NoSearchText), window.Title ?? "");
            return;
        }

        string text = SearchText;
        TextSearch search = new(text)
        {
            LookFor = window.LookFor,
            MatchCase = window.MatchCase,
            MatchWholeWordOnly = window.MatchWholeWord,
        };
        int caret = Caret(text);
        int startFrom = caret - (searchBackward ? 1 : 0);
        if (startFrom == -1)
        {
            startFrom = search.EndOffset;
        }

        if (search.FindNext(startFrom, searchBackward, out _) is { } found)
        {
            _found = found;
            int line = TextReplace.LineOf(text, found.Offset);
            BlameList.SelectedIndex = line - 1;
            BlameList.ScrollIntoView(line - 1);
        }
        else
        {
            await ShowMessageAsync(window, UpstreamTranslation.ByText(DiffView.TextNotFoundText), NoCaption);
        }
    }

    private int Caret(string text)
    {
        if (_found is { } found && TextReplace.LineOf(text, found.Offset) - 1 == BlameList.SelectedIndex)
        {
            return found.Offset + found.Length;
        }

        return BlameList.SelectedIndex >= 0 ? TextReplace.OffsetOfLine(text, BlameList.SelectedIndex + 1) : 0;
    }

    private async Task GoToLineAsync()
    {
        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            GoToLineWindow window = new();
            window.SetMaxLineNumber(_lines.Count);
            if (await window.ShowAsync(owner) is { } line)
            {
                GoToLine(line);
            }
        }
    }

    private static async Task ShowMessageAsync(Window owner, string text, string caption)
        => await new MessageBoxWindow(text, caption, MessageBoxButtons.OK).ShowDialog(owner);
}
