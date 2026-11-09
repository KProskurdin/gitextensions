using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  Upstream's output history (GitUI's <c>OutputHistoryModel</c>, internal there): the last outputs of the process window and
///  some trace output, newest last, at most <c>OutputHistoryDepth</c> of them, as the browse window's "Output" tab or panel
///  shows them.
/// </summary>
public sealed partial class OutputHistory
{
    /// <summary>
    ///  Upstream's <c>_endMark</c>, after the last entry.
    /// </summary>
    public const string EndMark = "###";

    // Upstream's _noExecutable, for a run without a command line.
    private const string NoCommand = "---";

    private readonly Lock _lock = new();
    private readonly int _depth;
    private readonly List<string> _entries = [];

    /// <summary>
    ///  An output history of at most <paramref name="depth"/> entries; none with 0, which turns it off.
    /// </summary>
    public OutputHistory(int depth)
    {
        _depth = Math.Max(0, depth);
    }

    /// <summary>
    ///  Raised after an entry was added or the history cleared, on the thread that did it.
    /// </summary>
    public event EventHandler? HistoryChanged;

    /// <summary>
    ///  Upstream's <c>Enabled</c>: a depth of 0 turns the history off, and the browse window shows no tab or panel for it.
    /// </summary>
    public bool Enabled => _depth > 0;

    /// <summary>
    ///  Upstream's <c>History</c>: the entries, then the end mark.
    /// </summary>
    public string History
    {
        get
        {
            StringBuilder history = new();
            lock (_lock)
            {
                foreach (string entry in _entries)
                {
                    history.Append(entry);
                }
            }

            return history.AppendLine(EndMark).ToString();
        }
    }

    [GeneratedRegex(@"\x1B\[[0-9;?]*[ -/]*[@-~]")]
    private static partial Regex AnsiEscapeRegex { get; }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }

        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///  Upstream's <c>RecordHistory(string)</c>: the time and the message, for trace output.
    /// </summary>
    public void Record(string message, DateTime time)
        => Add($"{ShortTime(time)} {message}{Environment.NewLine}");

    /// <summary>
    ///  Upstream's <c>RecordHistory(RunProcessInfo)</c>: the time the run finished and its command (upstream's "---" for
    ///  none), its output without the terminal's color codes, and an empty line.
    /// </summary>
    public void RecordRun(string? command, IEnumerable<string> output, DateTime finished)
    {
        StringBuilder entry = new();
        entry.Append(ShortTime(finished)).Append(' ').AppendLine(string.IsNullOrWhiteSpace(command) ? NoCommand : command);
        entry.Append(AnsiEscapeRegex.Replace(string.Join(Environment.NewLine, output).Trim(), ""));
        entry.AppendLine().AppendLine();
        Add(entry.ToString());
    }

    private void Add(string entry)
    {
        if (!Enabled)
        {
            return;
        }

        lock (_lock)
        {
            if (_entries.Count == _depth)
            {
                _entries.RemoveAt(0);
            }

            _entries.Add(entry);
        }

        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    // Upstream's DateTime.ToShortTimeString.
    private static string ShortTime(DateTime time) => time.ToString("t", CultureInfo.CurrentCulture);
}
