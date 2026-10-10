using System.Diagnostics;
using System.Reflection;
using System.Xml;

namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  How a piece of text is drawn: a color as the definition names it (a color name or "#rrggbb"; null for the default text
///  color), bold and italic.
/// </summary>
public sealed record SyntaxStyle(string? Color, bool Bold = false, bool Italic = false);

/// <summary>
///  A part of a line and its style; a null style is the default text.
/// </summary>
public readonly record struct SyntaxRun(int Start, int Length, SyntaxStyle? Style);

/// <summary>
///  Upstream's syntax highlighting definitions: the <c>.xshd</c> files of ICSharpCode.TextEditor
///  (<c>externals/ICSharpCode.TextEditor/Project/Resources</c>), which upstream's file viewer chooses by the file's
///  extension. They are linked into the core unchanged and read here.
/// </summary>
public static class SyntaxDefinitions
{
    private const string ResourcePrefix = "Xshd.";

    private static readonly Lazy<IReadOnlyList<SyntaxDefinition>> _all = new(LoadAll);

    /// <summary>
    ///  Every definition, by its name.
    /// </summary>
    public static IReadOnlyList<SyntaxDefinition> All => _all.Value;

    /// <summary>
    ///  The definition for <paramref name="path"/>'s extension, as upstream's <c>HighlightingManager.FindHighlighterForFile</c>
    ///  finds it, or null.
    /// </summary>
    public static SyntaxDefinition? ForFile(string path)
    {
        string extension = Path.GetExtension(path);
        if (extension.Length == 0)
        {
            return null;
        }

        return All.FirstOrDefault(definition =>
            definition.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    ///  Reads one definition.
    /// </summary>
    public static SyntaxDefinition Load(Stream xshd)
    {
        XmlDocument document = new() { XmlResolver = null };
        document.Load(xshd);
        return SyntaxDefinition.Parse(document.DocumentElement!);
    }

    private static IReadOnlyList<SyntaxDefinition> LoadAll()
    {
        Assembly assembly = typeof(SyntaxDefinitions).Assembly;
        List<SyntaxDefinition> definitions = [];
        foreach (string name in assembly.GetManifestResourceNames().Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
        {
            try
            {
                using Stream stream = assembly.GetManifestResourceStream(name)!;
                definitions.Add(Load(stream));
            }
            catch (Exception ex) when (ex is XmlException or FormatException or InvalidOperationException)
            {
                Trace.WriteLine($"Syntax definition {name} cannot be read: {ex.Message}");
            }
        }

        return definitions;
    }
}

/// <summary>
///  One <c>.xshd</c> definition: its rule sets with their spans, keywords and markers.
/// </summary>
public sealed class SyntaxDefinition
{
    private SyntaxDefinition(string name, IReadOnlyList<string> extensions, SyntaxStyle? digits,
        Dictionary<string, SyntaxRuleSet> ruleSets)
    {
        Name = name;
        Extensions = extensions;
        Digits = digits;
        RuleSets = ruleSets;
    }

    public string Name { get; }

    /// <summary>
    ///  The file extensions with their dot (".cs").
    /// </summary>
    public IReadOnlyList<string> Extensions { get; }

    internal SyntaxStyle? Digits { get; }

    internal Dictionary<string, SyntaxRuleSet> RuleSets { get; }

    internal SyntaxRuleSet Default => RuleSets[""];

    internal static SyntaxDefinition Parse(XmlElement root)
    {
        string name = root.GetAttribute("name");

        // Upstream's extension list is ';'-separated; a few definitions write "*.bat".
        List<string> extensions = [.. root.GetAttribute("extensions").Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(extension => extension.Trim().TrimStart('*'))];
        SyntaxStyle? digits = root["Digits"] is { } digitsElement ? Style(digitsElement, null) : null;
        Dictionary<string, SyntaxRuleSet> ruleSets = new(StringComparer.Ordinal);
        foreach (XmlElement element in root["RuleSets"]?.GetElementsByTagName("RuleSet").OfType<XmlElement>() ?? [])
        {
            SyntaxRuleSet ruleSet = SyntaxRuleSet.Parse(element);
            ruleSets[ruleSet.Name] = ruleSet;
        }

        if (!ruleSets.ContainsKey(""))
        {
            throw new InvalidOperationException($"No default rule set in {name}");
        }

        return new SyntaxDefinition(name, extensions, digits, ruleSets);
    }

    // Upstream's HighlightColor from an element: color, bold, italic; a "SystemColors." name is the default text color.
    internal static SyntaxStyle Style(XmlElement element, SyntaxStyle? inherited)
    {
        string? color = element.HasAttribute("color") ? element.GetAttribute("color") : inherited?.Color;
        if (color?.StartsWith("SystemColors.", StringComparison.Ordinal) == true)
        {
            color = null;
        }

        bool bold = element.HasAttribute("bold") ? bool.Parse(element.GetAttribute("bold")) : inherited?.Bold ?? false;
        bool italic = element.HasAttribute("italic") ? bool.Parse(element.GetAttribute("italic")) : inherited?.Italic ?? false;
        return new SyntaxStyle(color, bold, italic);
    }
}

internal sealed class SyntaxRuleSet
{
    public string Name { get; private init; } = "";

    public bool IgnoreCase { get; private init; }

    public char EscapeCharacter { get; private init; }

    public bool[] Delimiters { get; } = new bool[256];

    public List<SyntaxSpan> Spans { get; } = [];

    public Dictionary<string, SyntaxStyle> Keywords { get; private init; } = new(StringComparer.Ordinal);

    // MarkPrevious: the word before the text gets the style. MarkFollowing: the word after the keyword gets it (and the
    // keyword too with markmarker).
    public Dictionary<string, SyntaxStyle> PreviousMarkers { get; private init; } = new(StringComparer.Ordinal);

    public Dictionary<string, (SyntaxStyle Style, bool MarkMarker)> NextMarkers { get; private init; } = new(StringComparer.Ordinal);

    public static SyntaxRuleSet Parse(XmlElement element)
    {
        bool ignoreCase = element.HasAttribute("ignorecase") && bool.Parse(element.GetAttribute("ignorecase"));
        StringComparer comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        SyntaxRuleSet ruleSet = new()
        {
            Name = element.GetAttribute("name"),
            IgnoreCase = ignoreCase,
            EscapeCharacter = element.GetAttribute("escapecharacter") is { Length: > 0 } escape ? escape[0] : '\0',
            Keywords = new Dictionary<string, SyntaxStyle>(comparer),
            PreviousMarkers = new Dictionary<string, SyntaxStyle>(comparer),
            NextMarkers = new Dictionary<string, (SyntaxStyle, bool)>(comparer),
        };

        if (element["Delimiters"] is { } delimiters)
        {
            foreach (char ch in delimiters.InnerText.Where(ch => ch < 256))
            {
                ruleSet.Delimiters[ch] = true;
            }
        }

        foreach (XmlElement child in element.ChildNodes.OfType<XmlElement>())
        {
            switch (child.Name)
            {
                case "Span":
                    ruleSet.Spans.Add(SyntaxSpan.Parse(child, ignoreCase));
                    break;
                case "KeyWords":
                    SyntaxStyle keywordStyle = SyntaxDefinition.Style(child, null);
                    foreach (XmlElement key in child.GetElementsByTagName("Key").OfType<XmlElement>())
                    {
                        ruleSet.Keywords[key.GetAttribute("word")] = keywordStyle;
                    }

                    break;
                case "MarkPrevious":
                    ruleSet.PreviousMarkers[child.InnerText] = SyntaxDefinition.Style(child, null);
                    break;
                case "MarkFollowing":
                    ruleSet.NextMarkers[child.InnerText] = (SyntaxDefinition.Style(child, null),
                        child.HasAttribute("markmarker") && bool.Parse(child.GetAttribute("markmarker")));
                    break;
            }
        }

        return ruleSet;
    }
}

internal sealed class SyntaxSpan
{
    public string? Rule { get; private init; }

    public bool StopAtEndOfLine { get; private init; }

    public char EscapeCharacter { get; private init; }

    public SyntaxStyle Style { get; private init; } = new(null);

    public SyntaxStyle BeginStyle { get; private init; } = new(null);

    public SyntaxStyle EndStyle { get; private init; } = new(null);

    public string Begin { get; private init; } = "";

    public string? End { get; private init; }

    public bool BeginSingleWord { get; private init; }

    public bool? BeginStartOfLine { get; private init; }

    public bool IgnoreCase { get; private init; }

    public static SyntaxSpan Parse(XmlElement element, bool ignoreCase)
    {
        SyntaxStyle style = SyntaxDefinition.Style(element, null);
        XmlElement begin = element["Begin"]!;
        XmlElement? end = element["End"];
        return new SyntaxSpan
        {
            Rule = element.HasAttribute("rule") ? element.GetAttribute("rule") : null,
            StopAtEndOfLine = element.HasAttribute("stopateol") && bool.Parse(element.GetAttribute("stopateol")),
            EscapeCharacter = element.GetAttribute("escapecharacter") is { Length: > 0 } escape ? escape[0] : '\0',
            Style = style,
            Begin = begin.InnerText,
            BeginStyle = SyntaxDefinition.Style(begin, style),
            End = end?.InnerText,
            EndStyle = end is null ? style : SyntaxDefinition.Style(end, style),
            BeginSingleWord = begin.HasAttribute("singleword") && bool.Parse(begin.GetAttribute("singleword")),
            BeginStartOfLine = begin.HasAttribute("startofline") ? bool.Parse(begin.GetAttribute("startofline")) : null,
            IgnoreCase = ignoreCase,
        };
    }
}

/// <summary>
///  Highlights the lines of one text in order, as upstream's <c>DefaultHighlightingStrategy.ParseLine</c> does: a span
///  that does not stop at the end of the line (a block comment) carries on to the next line.
/// </summary>
public sealed class SyntaxHighlighter(SyntaxDefinition definition)
{
    private readonly List<SyntaxSpan> _spans = [];

    /// <summary>
    ///  Forgets the open spans, e.g. at a new hunk of a diff.
    /// </summary>
    public void Reset() => _spans.Clear();

    public IReadOnlyList<SyntaxRun> HighlightLine(string line)
    {
        // Spans that end with the line are closed before the next one.
        while (_spans.Count > 0 && _spans[^1].StopAtEndOfLine)
        {
            _spans.RemoveAt(_spans.Count - 1);
        }

        return new LineParser(definition, _spans, line).Parse();
    }

    private sealed class LineParser(SyntaxDefinition definition, List<SyntaxSpan> spans, string line)
    {
        private readonly List<(int Start, int Length, SyntaxStyle? Style, bool IsDefault, bool IsWhiteSpace)> _words = [];
        private int _offset;
        private int _length;
        private SyntaxStyle? _markNext;

        private SyntaxSpan? ActiveSpan => spans.Count > 0 ? spans[^1] : null;

        private SyntaxRuleSet? ActiveRuleSet
            => ActiveSpan is null ? definition.Default
                : ActiveSpan.Rule is { } rule ? definition.RuleSets.GetValueOrDefault(rule)
                : null;

        public IReadOnlyList<SyntaxRun> Parse()
        {
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (ch is ' ' or '\t' or '\r' or '\n')
                {
                    PushWord();
                    _words.Add((_offset, 1, ActiveSpan?.Style, true, true));
                    _offset++;
                    continue;
                }

                // Escape characters: a doubled end character in a span that ends with it, or a backslash-style escape.
                char escape = ActiveSpan is { EscapeCharacter: not '\0' } span ? span.EscapeCharacter
                    : ActiveRuleSet?.EscapeCharacter ?? '\0';
                if (escape != '\0' && ch == escape)
                {
                    if (ActiveSpan?.End is { Length: 1 } end && end[0] == escape)
                    {
                        if (i + 1 < line.Length && line[i + 1] == escape)
                        {
                            _length += 2;
                            PushWord();
                            i++;
                            continue;
                        }
                    }
                    else
                    {
                        _length += i + 1 < line.Length ? 2 : 1;
                        PushWord();
                        i++;
                        continue;
                    }
                }

                if (ActiveSpan is null && _length == 0
                    && (char.IsDigit(ch) || (ch == '.' && i + 1 < line.Length && char.IsDigit(line[i + 1]))))
                {
                    int end = DigitsEnd(i);
                    _words.Add((_offset, end - i, definition.Digits, definition.Digits is null, false));
                    _offset += end - i;
                    i = end - 1;
                    continue;
                }

                if (ActiveSpan is { End: { Length: > 0 } spanEnd } activeSpan && Matches(spanEnd, i, activeSpan.IgnoreCase))
                {
                    PushWord();
                    int length = MatchedLength(spanEnd, i);
                    _length += length;
                    _words.Add((_offset, _length, activeSpan.EndStyle, false, false));
                    _offset += _length;
                    _length = 0;
                    i += length - 1;
                    spans.RemoveAt(spans.Count - 1);
                    continue;
                }

                if (ActiveRuleSet is { } ruleSet && BeginningSpan(ruleSet, i) is { } beginning)
                {
                    PushWord();
                    int length = MatchedLength(beginning.Begin, i);
                    _length += length;
                    _words.Add((_offset, _length, beginning.BeginStyle, false, false));
                    _offset += _length;
                    _length = 0;
                    i += length - 1;
                    spans.Add(beginning);
                    continue;
                }

                if (ActiveRuleSet is { } delimiterRules && ch < 256 && delimiterRules.Delimiters[ch])
                {
                    PushWord();
                    if (_offset + _length + 1 < line.Length)
                    {
                        _length++;
                        PushWord();
                        continue;
                    }
                }

                _length++;
            }

            PushWord();
            return [.. _words.Select(word => new SyntaxRun(word.Start, word.Length, word.Style))];
        }

        private SyntaxSpan? BeginningSpan(SyntaxRuleSet ruleSet, int index)
        {
            foreach (SyntaxSpan span in ruleSet.Spans)
            {
                if (span.BeginSingleWord && _length != 0)
                {
                    continue;
                }

                if (span.BeginStartOfLine is { } startOfLine
                    && startOfLine != (_length == 0 && _words.All(word => word.IsWhiteSpace)))
                {
                    continue;
                }

                if (Matches(span.Begin, index, ruleSet.IgnoreCase))
                {
                    return span;
                }
            }

            return null;
        }

        // Upstream's PushCurWord: the word gets the span's style, or a keyword's; a MarkPrevious text styles the word before
        // it, and a MarkFollowing keyword the word after it.
        private void PushWord()
        {
            if (_length <= 0)
            {
                return;
            }

            SyntaxRuleSet? ruleSet = ActiveRuleSet;
            string word = line.Substring(_offset, _length);
            if (_words.Count > 0 && ruleSet is not null)
            {
                for (int index = _words.Count - 1; index >= 0; index--)
                {
                    if (_words[index].IsWhiteSpace)
                    {
                        continue;
                    }

                    if (_words[index].IsDefault && ruleSet.PreviousMarkers.TryGetValue(word, out SyntaxStyle? previous))
                    {
                        _words[index] = _words[index] with { Style = previous, IsDefault = false };
                    }

                    break;
                }
            }

            if (ActiveSpan is { } span)
            {
                SyntaxStyle? keyword = span.Rule is null ? null : ruleSet?.Keywords.GetValueOrDefault(word);
                _words.Add((_offset, _length, _markNext ?? keyword ?? span.Style, keyword is null, false));
            }
            else
            {
                SyntaxStyle? style = _markNext ?? ruleSet?.Keywords.GetValueOrDefault(word);
                _words.Add((_offset, _length, style, style is null, false));
            }

            if (ruleSet is not null && ruleSet.NextMarkers.TryGetValue(word, out (SyntaxStyle Style, bool MarkMarker) next))
            {
                if (next.MarkMarker)
                {
                    _words[^1] = _words[^1] with { Style = next.Style, IsDefault = false };
                }

                _markNext = next.Style;
            }
            else
            {
                _markNext = null;
            }

            _offset += _length;
            _length = 0;
        }

        // Upstream's number forms: hex, decimals, exponent and the type suffixes.
        private int DigitsEnd(int start)
        {
            int i = start;
            bool hex = false;
            bool floating = false;
            if (line[i] == '0' && i + 1 < line.Length && char.ToUpperInvariant(line[i + 1]) == 'X')
            {
                hex = true;
                i += 2;
                while (i < line.Length && Uri.IsHexDigit(line[i]))
                {
                    i++;
                }
            }
            else
            {
                i++;
                while (i < line.Length && char.IsDigit(line[i]))
                {
                    i++;
                }
            }

            if (!hex && i < line.Length && line[i] == '.')
            {
                floating = true;
                i++;
                while (i < line.Length && char.IsDigit(line[i]))
                {
                    i++;
                }
            }

            if (i < line.Length && char.ToUpperInvariant(line[i]) == 'E')
            {
                floating = true;
                i++;
                if (i < line.Length && line[i] is '+' or '-')
                {
                    i++;
                }

                while (i < line.Length && char.IsDigit(line[i]))
                {
                    i++;
                }
            }

            if (i < line.Length && char.ToUpperInvariant(line[i]) is 'F' or 'M' or 'D')
            {
                floating = true;
                i++;
            }

            if (!floating)
            {
                bool unsigned = false;
                if (i < line.Length && char.ToUpperInvariant(line[i]) == 'U')
                {
                    i++;
                    unsigned = true;
                }

                if (i < line.Length && char.ToUpperInvariant(line[i]) == 'L')
                {
                    i++;
                    if (!unsigned && i < line.Length && char.ToUpperInvariant(line[i]) == 'U')
                    {
                        i++;
                    }
                }
            }

            return i;
        }

        // Upstream's MatchExpr: "@!x@" fails when x follows, "@-x@" when x precedes, "@C" is whitespace or punctuation (or
        // the line's end), "@@" is an @.
        private bool Matches(string expression, int index, bool ignoreCase)
        {
            for (int i = 0, j = 0; i < expression.Length; ++i, ++j)
            {
                if (expression[i] == '@' && i + 1 < expression.Length)
                {
                    i++;
                    switch (expression[i])
                    {
                        case 'C':
                            if (index + j < line.Length && !char.IsWhiteSpace(line[index + j]) && !char.IsPunctuation(line[index + j]))
                            {
                                return false;
                            }

                            break;
                        case '!':
                        {
                            string text = ReadUntilAt(expression, ref i);
                            if (index + j + text.Length <= line.Length
                                && string.Compare(line, index + j, text, 0, text.Length, Comparison(ignoreCase)) == 0)
                            {
                                return false;
                            }

                            break;
                        }

                        case '-':
                        {
                            string text = ReadUntilAt(expression, ref i);
                            if (index - text.Length >= 0
                                && string.Compare(line, index - text.Length, text, 0, text.Length, Comparison(ignoreCase)) == 0)
                            {
                                return false;
                            }

                            break;
                        }

                        case '@':
                            if (index + j >= line.Length || line[index + j] != '@')
                            {
                                return false;
                            }

                            break;
                    }

                    continue;
                }

                if (index + j >= line.Length
                    || (ignoreCase
                        ? char.ToUpperInvariant(line[index + j]) != char.ToUpperInvariant(expression[i])
                        : line[index + j] != expression[i]))
                {
                    return false;
                }
            }

            return true;
        }

        // Upstream's GetRegString: how many characters of the line the expression matched (the "@!" parts match none).
        private int MatchedLength(string expression, int index)
        {
            int length = 0;
            for (int i = 0; i < expression.Length && index + length < line.Length; ++i)
            {
                if (expression[i] == '@' && i + 1 < expression.Length)
                {
                    i++;
                    switch (expression[i])
                    {
                        case '!' or '-':
                            ReadUntilAt(expression, ref i);
                            break;
                        case '@':
                            length++;
                            break;
                    }

                    continue;
                }

                if (line[index + length] != expression[i] &&
                    char.ToUpperInvariant(line[index + length]) != char.ToUpperInvariant(expression[i]))
                {
                    break;
                }

                length++;
            }

            return Math.Max(length, 1);
        }

        private static string ReadUntilAt(string expression, ref int i)
        {
            int start = ++i;
            while (i < expression.Length && expression[i] != '@')
            {
                i++;
            }

            return expression[start..i];
        }

        private static StringComparison Comparison(bool ignoreCase)
            => ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    }
}
