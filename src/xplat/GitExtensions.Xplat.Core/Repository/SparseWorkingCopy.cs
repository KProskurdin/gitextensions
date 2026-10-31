using GitCommands;
using GitExtensions.Extensibility;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  Upstream's <c>FormSparseWorkingCopyViewModel</c>: whether git's sparse checkout is on for the repository, the rules in
///  .git/info/sparse-checkout, and saving both, then refreshing the working copy against them.
/// </summary>
public sealed class SparseWorkingCopy : ObservableObject
{
    /// <summary>
    ///  The git command that applies the rules to the working copy (upstream's <c>RefreshWorkingCopyCommandName</c>).
    /// </summary>
    public const string RefreshWorkingCopyCommandName = "read-tree -m -u HEAD";

    public const string SettingCoreSparseCheckout = "core.sparsecheckout";

    // The single rule that lets the whole working copy through, which turning sparse checkout off needs.
    private const string PassAllRule = "/*";

    // Upstream's texts (FormSparseWorkingCopy.Globalized), as upstream writes them.
    public const string Cancel = "Cancel";
    public const string CannotLoadTheTextOfTheSparseFile = "Cannot load the text of the sparse file.";
    public const string ConfirmDisableGitSparse = "You are about to disable Git Sparse feature for this repository, {0}.\nGit won't be able to restore the working copy to its full content this way.\n\nWould you like to have the filter modified so that it allowed for the full working copy?";
    public const string CouldNotSave = "Could not save the modified settings and rules.";
    public const string DisableForThisRepository = "Disable for this repository";
    public const string DisableGitSparse = "Disable Git Sparse";
    public const string EditsTheContentsOfTheGitInfoSparseCheckoutFile = "Edits the contents of the “.git/info/sparse-checkout” file.";
    public const string Enable = "&Enable";
    public const string HeaderDetailsText = "Need only a small part of a large repository?\nWith sparse checkout, you can skip the rest from being extracted into your working copy.";
    public const string LoadFile = "Load File";
    public const string RefreshWorkingCopyCheckboxHint = "As the sparse working copy rules are changed, it might become outdated.\nRefreshes the working copy against the current set of the rules to restore any missing files and remove any extra files.\n\nnActual command line: {0}";
    public const string RefreshWorkingCopyUsingTheCurrentSettingsAndRules = "Refresh working copy using the current settings and rules";
    public const string Save = "&Save";
    public const string SaveFile = "Save File";
    public const string SetsTheGitPropertyToFalseForTheLocalRepository = "Sets the Git property “{0}” to False for the local repository.";
    public const string SetsTheGitPropertyToTrueForTheLocalRepository = "Sets the Git property “{0}” to True for the local repository.";
    public const string SparseWorkingCopyTitle = "Sparse Working Copy";
    public const string SparseWorkingCopySupportHasNotBeenEnabledForThisRepository = "Git Sparse feature has not been enabled for this repository.";
    public const string SparseWorkingCopySupportIsEnabled = "Git Sparse feature is currently enabled.";
    public const string SpecifyTheRulesForIncludingOrExcludingFilesAndDirectories = "Specify the pass-filter rules for files and directories:";
    public const string SpecifyTheRulesForIncludingOrExcludingFilesAndDirectoriesLine2 = "The rules have the same format as the “.gitignore” file, matched items are included. To exclude, prefix a rule with an exclamation mark “!”.\n“#” comments a line. This is only a filter, so it cannot change the structure like pulling up a deep subfolder to the first level.";
    public const string WithSomeRulesStillInTheSparsePassFilter = "with some rules still in the sparse pass-filter";
    public const string WithTheSparsePassFilterEmptyOrMissing = "with the sparse pass-filter empty or missing";
    public const string YouHaveMadeChangesToSettingsOrRulesWouldYouLikeToSaveThem = "You have made changes to settings or rules.\nWould you like to save them?";

    private readonly GitModule _module;

    // On by default, otherwise the index bitmap is not updated (upstream's comment).
    private bool _isRefreshWorkingCopyOnSave = true;
    private bool _isSparseCheckoutEnabled;
    private bool _isSparseCheckoutEnabledAsSaved;
    private string? _rulesText;
    private string? _rulesTextAsOnDisk;

    public SparseWorkingCopy(GitModule module)
    {
        _module = module;
        _isSparseCheckoutEnabled = _isSparseCheckoutEnabledAsSaved = GetCurrentSparseEnabledState();
    }

    /// <summary>
    ///  Whether saving runs <see cref="RefreshWorkingCopyCommandName"/>.
    /// </summary>
    public bool IsRefreshWorkingCopyOnSave
    {
        get => _isRefreshWorkingCopyOnSave;
        set => SetProperty(ref _isRefreshWorkingCopyOnSave, value);
    }

    /// <summary>
    ///  The sparse checkout setting as shown, saved by <see cref="SaveChangesAsync"/>.
    /// </summary>
    public bool IsSparseCheckoutEnabled
    {
        get => _isSparseCheckoutEnabled;
        set => SetProperty(ref _isSparseCheckoutEnabled, value);
    }

    /// <summary>
    ///  The rules as edited; null until the file was read.
    /// </summary>
    public string? RulesText
    {
        get => _rulesText;
        set => SetProperty(ref _rulesText, value);
    }

    /// <summary>
    ///  Whether the rules differ from the file.
    /// </summary>
    public bool IsRulesTextChanged => _rulesText is not null && _rulesText != (_rulesTextAsOnDisk ?? "");

    /// <summary>
    ///  The file of the rules, in the repository's git folder.
    /// </summary>
    public FileInfo SparseCheckoutFile => new(Path.Join(_module.ResolveGitInternalPath("info"), "sparse-checkout"));

    /// <summary>
    ///  Upstream's <c>GetCurrentSparseEnabledState</c>: <c>core.sparsecheckout</c> as git reads it.
    /// </summary>
    public bool GetCurrentSparseEnabledState()
        => StringComparer.OrdinalIgnoreCase.Equals(_module.GetEffectiveSetting(SettingCoreSparseCheckout), bool.TrueString);

    /// <summary>
    ///  Reads the rules file, when there is one, as the text the edits are compared with (upstream's
    ///  <c>SetRulesTextAsOnDisk</c>); <see cref="RulesText"/> is left to the editor, as it raises property changes.
    /// </summary>
    public string? LoadRules()
    {
        FileInfo file = SparseCheckoutFile;
        if (!file.Exists)
        {
            return null;
        }

        string text = File.ReadAllText(file.FullName, GitModule.SystemEncoding);
        _rulesTextAsOnDisk = text;
        return text;
    }

    /// <summary>
    ///  Upstream's <c>IsWithUnsavedChanges</c>: the setting or the rules changed.
    /// </summary>
    public bool IsWithUnsavedChanges() => IsSparseCheckoutEnabled != _isSparseCheckoutEnabledAsSaved || IsRulesTextChanged;

    /// <summary>
    ///  Upstream's <c>SaveChanges</c>: when sparse checkout is turned off with rules that do not pass everything, the rules
    ///  are commented out under a single "/*" after <paramref name="confirmAdjustingRules"/> (given whether the rules are
    ///  empty) agrees; then the setting and the rules are written, and the working copy is refreshed when asked, even when
    ///  nothing changed.
    /// </summary>
    public async Task SaveChangesAsync(Func<bool, Task<bool>> confirmAdjustingRules, Func<string, Task> refreshWorkingCopy)
    {
        if (TurnsOffWithFilteringRules(out bool isRuleSetEmpty) && await confirmAdjustingRules(isRuleSetEmpty))
        {
            RulesText = PassAllRules(RulesText!);
        }

        if (IsSparseCheckoutEnabled != _isSparseCheckoutEnabledAsSaved)
        {
            bool enabled = IsSparseCheckoutEnabled;
            await Task.Run(() => _module.SetSetting(SettingCoreSparseCheckout, enabled.ToString().ToLowerInvariant()));
            _isSparseCheckoutEnabledAsSaved = enabled;
        }

        if (IsRulesTextChanged)
        {
            string newText = RulesText ?? "";
            Directory.CreateDirectory(SparseCheckoutFile.DirectoryName!);
            await File.WriteAllBytesAsync(SparseCheckoutFile.FullName, GitModule.SystemEncoding.GetBytes(newText));
            _rulesTextAsOnDisk = newText;
        }

        if (IsRefreshWorkingCopyOnSave)
        {
            await refreshWorkingCopy(RefreshWorkingCopyCommandName);
        }
    }

    /// <summary>
    ///  Upstream's rule rewrite when turning sparse checkout off: "/*" first, then every rule commented out (blank lines and
    ///  comments kept).
    /// </summary>
    public static string PassAllRules(string rulesText)
        => string.Join(Environment.NewLine,
            new[] { PassAllRule }.Concat(rulesText.LazySplit('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => string.IsNullOrWhiteSpace(line) || line[0] == '#' ? line : "#" + line)));

    // Upstream's SaveChangesTurningOffSparseSpecialCase check: turning off, with rules that are not just "/*".
    private bool TurnsOffWithFilteringRules(out bool isRuleSetEmpty)
    {
        isRuleSetEmpty = false;
        if (IsSparseCheckoutEnabled || !_isSparseCheckoutEnabledAsSaved || RulesText is null)
        {
            return false;
        }

        List<string> rules =
        [
            .. RulesText.LazySplit('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim())
                .Where(line => line.Length > 0 && line[0] != '#')
        ];
        isRuleSetEmpty = rules.Count == 0;
        return !rules.All(rule => rule == PassAllRule);
    }
}
