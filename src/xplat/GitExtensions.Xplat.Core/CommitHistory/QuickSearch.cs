namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  Upstream's quick search of the revision grid (<c>QuickSearchProvider</c>): letters typed into the grid gather into a
///  search text, and the next row it matches (<c>GitRevisionTester.Matches</c>) is selected. The view shows
///  <see cref="Text"/> while <see cref="IsActive"/> and ends the search after upstream's timeout.
/// </summary>
public sealed class QuickSearch
{
    /// <summary>
    ///  Upstream's <c>TranslatedStrings.SearchingFor</c>.
    /// </summary>
    public const string SearchingFor = "Searching for: ";

    private string _search = "";
    private string _lastSearch = "";

    public bool IsActive => _search.Length > 0;

    /// <summary>
    ///  What the view shows while searching.
    /// </summary>
    public string Text => SearchingFor + _search;

    /// <summary>
    ///  False when the last search found no row; upstream shows the text in red then.
    /// </summary>
    public bool Found { get; private set; } = true;

    /// <summary>
    ///  Adds typed text (a character, or pasted text) and returns the row to select, searching from
    ///  <paramref name="current"/>; null when nothing matches.
    /// </summary>
    public int? Type(string text, IReadOnlyList<CommitRow> rows, int current)
        => Search(_search + text.ToLowerInvariant(), rows, Math.Max(current, 0), reverse: false);

    /// <summary>
    ///  Upstream's backspace: removes the last character while more than one is left.
    /// </summary>
    public int? Backspace(IReadOnlyList<CommitRow> rows, int current)
        => _search.Length > 1 ? Search(_search[..^1], rows, Math.Max(current, 0), reverse: false) : null;

    /// <summary>
    ///  Upstream's next and previous result (Alt+Down, Alt+Up): the last search again, from the row after or before
    ///  <paramref name="current"/>.
    /// </summary>
    public int? Next(IReadOnlyList<CommitRow> rows, int current, bool down)
        => Search(_lastSearch, rows, current < 0 ? 0 : down ? current + 1 : current - 1, reverse: !down);

    /// <summary>
    ///  Ends the search (Escape, the timeout, or any other key).
    /// </summary>
    public void End() => _search = "";

    /// <summary>
    ///  Upstream's <c>GitRevisionTester.Matches</c>: a ref name containing the text, a hash starting with it (three
    ///  characters or more), an author or message containing it.
    /// </summary>
    public static bool Matches(CommitRow row, string criteria)
    {
        if (string.IsNullOrWhiteSpace(criteria))
        {
            return false;
        }

        if ((row.Labels ?? []).Any(label => label.Kind != RefKind.Head
                                            && label.Name.Contains(criteria, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (criteria.Length > 2 && row.Hash.StartsWith(criteria, StringComparison.CurrentCultureIgnoreCase))
        {
            return true;
        }

        return row.Author.Contains(criteria, StringComparison.CurrentCultureIgnoreCase)
               || (row.Body ?? row.Subject).Contains(criteria, StringComparison.OrdinalIgnoreCase);
    }

    private int? Search(string search, IReadOnlyList<CommitRow> rows, int start, bool reverse)
    {
        _search = search;
        _lastSearch = search;
        int? match = rows.Count == 0 ? null : reverse ? Backwards() : Forwards();
        Found = match is not null;
        return match;

        int? Forwards()
        {
            int from = start < 0 || start >= rows.Count ? 0 : start;
            for (int i = 0; i < rows.Count; i++)
            {
                int index = (from + i) % rows.Count;
                if (Matches(rows[index], search))
                {
                    return index;
                }
            }

            return null;
        }

        int? Backwards()
        {
            int from = start < 0 || start >= rows.Count ? rows.Count - 1 : start;
            for (int i = 0; i < rows.Count; i++)
            {
                int index = (from - i + rows.Count) % rows.Count;
                if (Matches(rows[index], search))
                {
                    return index;
                }
            }

            return null;
        }
    }
}
