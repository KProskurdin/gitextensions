using System.IO;
using NetSpell.SpellChecker;
using NetSpell.SpellChecker.Dictionary;

namespace GitExtensions.Xplat.Core.Spelling;

/// <summary>
///  A part of a text: <paramref name="Start"/> and <paramref name="Length"/> in characters.
/// </summary>
public sealed record TextRange(int Start, int Length)
{
    public int End => Start + Length;
}

/// <summary>
///  The misspelled word at a place in the text, with upstream's suggestions for it (<c>EditNetSpell.AddWordSuggestions</c>).
/// </summary>
public sealed record SpellingSuggestions(string Word, TextRange Range, IReadOnlyList<string> Suggestions);

/// <summary>
///  Upstream's spelling checker of the commit message (<c>EditNetSpell</c>): upstream's NetSpell engine
///  (externals/NetSpell.SpellChecker, compiled unchanged) with upstream's options, its dictionaries (<c>.dic</c> files in
///  <c>AppSettings.GetDictionaryDir</c>) and its user word list. Calls are serialized, so a check can run off the UI thread.
/// </summary>
public sealed class CommitSpellChecker
{
    /// <summary>
    ///  The dictionary setting that turns the checker off; upstream's page stores "none", its menu "None".
    /// </summary>
    public const string NoDictionary = "None";

    /// <summary>
    ///  Upstream's <c>CheckSpelling</c> leaves longer texts unchecked.
    /// </summary>
    public const int MaxCheckedLength = 5000;

    /// <summary>
    ///  Upstream's <c>TextBoxTextChanged</c> does not check a shorter text.
    /// </summary>
    public const int MinCheckedLength = 4;

    // Upstream's AddWordSuggestions.
    private const int MaxSuggestions = 5;

    private const string DictionaryExtension = ".dic";

    private readonly Lock _lock = new();
    private readonly string _folder;
    private readonly bool _enableUserFile;
    private readonly NetSpell.SpellChecker.Spelling _spelling;
    private WordDictionary? _dictionary;

    /// <summary>
    ///  A checker of the dictionaries in <paramref name="dictionaryFolder"/>. With <paramref name="enableUserFile"/> the words
    ///  added to the dictionary are kept in NetSpell's user word list, as upstream's (<c>NetSpell\user.dic</c> in the
    ///  application data folder).
    /// </summary>
    public CommitSpellChecker(string dictionaryFolder, bool enableUserFile = true)
    {
        _folder = dictionaryFolder;
        _enableUserFile = enableUserFile;
        _spelling = new NetSpell.SpellChecker.Spelling
        {
            ShowDialog = false,
            IgnoreAllCapsWords = true,
            IgnoreWordsWithDigits = true,
            MaxSuggestions = MaxSuggestions,
        };
    }

    /// <summary>
    ///  Upstream's dictionary list (<c>AddDictionaries</c>, the settings page's drop-down): the names of the <c>.dic</c> files
    ///  in <paramref name="folder"/>, by name; none when the folder is missing.
    /// </summary>
    public static IReadOnlyList<string> Dictionaries(string folder)
        => Directory.Exists(folder)
            ? [.. Directory.GetFiles(folder, "*" + DictionaryExtension, SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileNameWithoutExtension).OfType<string>().Order(StringComparer.OrdinalIgnoreCase)]
            : [];

    /// <summary>
    ///  Whether <paramref name="dictionary"/> turns the checker off: "None" in either case, empty, or a dictionary whose file
    ///  is missing, as upstream checks only when the file exists.
    /// </summary>
    public bool IsOff(string? dictionary)
        => string.IsNullOrEmpty(dictionary) || string.Equals(dictionary, NoDictionary, StringComparison.OrdinalIgnoreCase)
           || !File.Exists(FileOf(dictionary));

    /// <summary>
    ///  Upstream's <c>AddAutoCompleteWords</c>: words the auto-completion offers (file names, identifiers) are never
    ///  misspelled.
    /// </summary>
    public void AddKnownWords(IEnumerable<string> words)
    {
        lock (_lock)
        {
            _spelling.AddAutoCompleteWords(words);
        }
    }

    /// <summary>
    ///  Upstream's <c>CheckSpelling</c>: the misspelled words of <paramref name="text"/> with <paramref name="dictionary"/>;
    ///  none while the dictionary is off or the text is too long.
    /// </summary>
    public IReadOnlyList<TextRange> Misspelled(string text, string? dictionary)
    {
        if (text.Length >= MaxCheckedLength || IsOff(dictionary))
        {
            return [];
        }

        lock (_lock)
        {
            Use(dictionary!);
            List<TextRange> words = [];
            void Collect(object? sender, SpellingEventArgs e) => words.Add(new TextRange(e.TextIndex, e.Word.Length));
            _spelling.MisspelledWord += Collect;
            try
            {
                _spelling.Text = text;
                _spelling.SpellCheck();
            }
            finally
            {
                _spelling.MisspelledWord -= Collect;
            }

            return words;
        }
    }

    /// <summary>
    ///  Upstream's <c>AddWordSuggestions</c>: the misspelled word at <paramref name="position"/> of <paramref name="text"/> and
    ///  up to five suggestions; null when there is no word there or it is spelled right.
    /// </summary>
    public SpellingSuggestions? SuggestionsAt(string text, int position, string? dictionary)
    {
        if (IsOff(dictionary))
        {
            return null;
        }

        lock (_lock)
        {
            Use(dictionary!);
            _spelling.Text = text;
            _spelling.WordIndex = _spelling.GetWordIndexFromTextIndex(position);
            string word = _spelling.CurrentWord;
            int start = _spelling.TextIndex;
            if (word.Length == 0 || position < start || position > start + word.Length || _spelling.TestWord()
                || _spelling.IgnoreList.Contains(word))
            {
                return null;
            }

            _spelling.Suggest();
            return new SpellingSuggestions(word, new TextRange(start, word.Length), [.. _spelling.Suggestions]);
        }
    }

    /// <summary>
    ///  Upstream's "Add to dictionary": the word goes to the user word list of the dictionary.
    /// </summary>
    public void AddToDictionary(string word, string? dictionary)
    {
        if (IsOff(dictionary))
        {
            return;
        }

        lock (_lock)
        {
            Use(dictionary!);
            if (!_spelling.Dictionary.Contains(word))
            {
                _spelling.Dictionary.Add(word);
            }
        }
    }

    /// <summary>
    ///  Upstream's "Ignore word", for as long as the checker lives. Upstream calls NetSpell's <c>IgnoreWord</c>, which only
    ///  moves past the word, so its next check marks the word again; this adds it to the ignore list (NetSpell's
    ///  <c>IgnoreAllWord</c>), which is what the item says.
    /// </summary>
    public void Ignore(string word)
    {
        lock (_lock)
        {
            if (!_spelling.IgnoreList.Contains(word))
            {
                _spelling.IgnoreList.Add(word);
            }
        }
    }

    /// <summary>
    ///  Upstream's "Remove word" (NetSpell's <c>DeleteWord</c>): <paramref name="text"/> without the word at
    ///  <paramref name="range"/>, and the space after it when it starts the text.
    /// </summary>
    public static string RemoveWord(string text, TextRange range)
    {
        int length = range.Start == 0 && range.End < text.Length && text[range.End] == ' ' ? range.Length + 1 : range.Length;
        return text.Remove(range.Start, length);
    }

    /// <summary>
    ///  Upstream's <c>MarkLines</c> (with "Mark ill formed lines"): the characters past the 50th of the first line, every
    ///  character of the second (which should be empty), and those past the 72nd of the others.
    /// </summary>
    public static IReadOnlyList<TextRange> IllFormedLines(string text)
    {
        List<TextRange> ranges = [];
        int lineStart = 0;
        int lineNumber = 0;
        while (lineStart <= text.Length)
        {
            int end = text.IndexOf('\n', lineStart);
            int lineEnd = end < 0 ? text.Length : end;
            int length = lineEnd > lineStart && text[lineEnd - 1] == '\r' ? lineEnd - 1 - lineStart : lineEnd - lineStart;
            int maxLength = lineNumber switch
            {
                0 => 50,
                1 => 0,
                _ => 72,
            };
            if (length > maxLength)
            {
                ranges.Add(new TextRange(lineStart + maxLength, length - maxLength));
            }

            if (end < 0)
            {
                break;
            }

            lineStart = end + 1;
            lineNumber++;
        }

        return ranges;
    }

    /// <summary>
    ///  Upstream's <c>SpellCheckEditControl</c> also marks the whole first row of a message whose first line is empty: the
    ///  subject is missing.
    /// </summary>
    public static bool MarksEmptySubject(string text)
        => text.Length > 1 && (text[0] == '\n' || text.StartsWith("\r\n", StringComparison.Ordinal));

    private string FileOf(string dictionary) => Path.Join(_folder, dictionary) + DictionaryExtension;

    // Upstream's LoadDictionary: a dictionary is read again only when another file is chosen.
    private void Use(string dictionary)
    {
        string file = FileOf(dictionary);
        if (_dictionary is null || _dictionary.DictionaryFile != Path.GetFileName(file))
        {
            _dictionary = new WordDictionary
            {
                DictionaryFolder = _folder,
                DictionaryFile = Path.GetFileName(file),
                EnableUserFile = _enableUserFile,
            };
            _spelling.Dictionary = _dictionary;
        }

        // NetSpell reads its user word list when it reads the dictionary, so a word added before would be forgotten.
        if (!_dictionary.Initialized)
        {
            _dictionary.Initialize();
        }
    }
}
