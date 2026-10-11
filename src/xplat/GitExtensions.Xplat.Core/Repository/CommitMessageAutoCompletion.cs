using GitCommands;
using GitCommands.Git;
using GitUI.AutoCompletion;
using GitUI.SpellChecker;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  Upstream <c>EditNetSpell</c>'s auto-completion of the commit message, without its WinForms list: the words of upstream's
///  <see cref="CommitAutoCompleteProvider"/> and <see cref="CommitMessageMetadataProvider"/>, matched against the word
///  before the caret.
/// </summary>
public static class CommitMessageAutoCompletion
{
    private static readonly WordAtCursorExtractor _extractor = new();

    /// <summary>
    ///  The words of the repository's changes (upstream's <c>FormCommit</c> providers), without repeats.
    /// </summary>
    public static Task<IReadOnlyList<AutoCompleteWord>> LoadWordsAsync(string repositoryPath,
        CancellationToken cancellationToken = default)
        => Task.Run<IReadOnlyList<AutoCompleteWord>>(async () =>
        {
            GitModule module = new(new GitExecutorProvider(new GitDirectoryResolver()), repositoryPath);
            IAutoCompleteProvider[] providers =
                [new CommitAutoCompleteProvider(() => module), new CommitMessageMetadataProvider()];
            IEnumerable<AutoCompleteWord>[] results =
                await Task.WhenAll(providers.Select(provider => provider.GetAutoCompleteWordsAsync(cancellationToken)));
            return [.. results.SelectMany(result => result).Distinct()];
        }, cancellationToken);

    /// <summary>
    ///  Upstream's <c>GetWordAtCursor</c>: the part of the word that ends at <paramref name="caret"/>.
    /// </summary>
    public static string WordAtCursor(string text, int caret) => _extractor.Extract(text, caret - 1);

    /// <summary>
    ///  The words that complete <paramref name="typed"/>: by their start or their capitals ("camel humps"), ignoring case.
    /// </summary>
    public static IReadOnlyList<AutoCompleteWord> Matches(IEnumerable<AutoCompleteWord> words, string typed)
        => [.. words.Where(word => word.Matches(typed))];

    /// <summary>
    ///  Upstream's <c>AcceptAutoComplete</c>: the word before the caret replaced by <paramref name="word"/>, and the caret
    ///  after it.
    /// </summary>
    public static (string Text, int Caret) Accept(string text, int caret, string word)
    {
        int start = caret - WordAtCursor(text, caret).Length;
        return (string.Concat(text.AsSpan(0, start), word, text.AsSpan(caret)), start + word.Length);
    }

    /// <summary>
    ///  Upstream's separators: typing one closes the list.
    /// </summary>
    public static bool IsSeparator(char c) => c.IsSeparator();
}
