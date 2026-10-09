using System.Text;
using Avalonia.Controls;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Plugins.GitStatistics;

/// <summary>
///  The new shell's version of upstream's <c>FormGitStatistics</c>: commits per contributor, and lines of code per language,
///  per type and in tests, counted by upstream's <see cref="LineCounter"/> in the files of HEAD's tree (and the submodules'
///  when the plugin counts them), with upstream's texts.
/// </summary>
public partial class StatisticsWindow : Window
{
    private const string Category = "FormGitStatistics";
    private static string CommitsText => UpstreamTranslation.Text(Category, "_commits", "{0:N0} Commits");
    private static string CommitsBy => UpstreamTranslation.Text(Category, "_commitsBy", "{0:N0} Commits by {1}");
    private static string LinesOfCodeInFiles => UpstreamTranslation.Text(Category, "_linesOfCodeInFiles", "{0:N0} Lines of code in {1} files ({2:P1})");
    private static string LinesOfCode => UpstreamTranslation.Text(Category, "_linesOfCode", "{0:N0} Lines of code");
    private static string LinesOfCodeP => UpstreamTranslation.Text(Category, "_linesOfCodeP", "{0:N0} Lines of code ({1:P1})");
    private static string LinesOfTestCode => UpstreamTranslation.Text(Category, "_linesOfTestCode", "{0:N0} Lines of test code");
    private static string LinesOfTestCodeP => UpstreamTranslation.Text(Category, "_linesOfTestCodeP", "{0:N0} Lines of test code ({1:P1})");
    private static string LinesOfProductionCodeP => UpstreamTranslation.Text(Category, "_linesOfProductionCodeP", "{0:N0} Lines of production code ({1:P1})");
    private static string BlankLinesP => UpstreamTranslation.Text(Category, "_blankLinesP", "{0:N0} Blank lines ({1:P1})");
    private static string CommentLinesP => UpstreamTranslation.Text(Category, "_commentLinesP", "{0:N0} Comment lines ({1:P1})");
    private static string LinesOfDesignerFilesP => UpstreamTranslation.Text(Category, "_linesOfDesignerFilesP", "{0:N0} Lines in designer files ({1:P1})");

    private readonly IGitExecutorProvider _executorProvider;
    private readonly IGitModule _module;
    private readonly string _codeFilePattern;
    private readonly bool _countSubmodules;
    private readonly string _directoriesToIgnore;
    private readonly LineCounter _lineCounter = new();

    public StatisticsWindow(IGitExecutorProvider executorProvider, IGitModule module, string codeFilePattern,
        bool countSubmodules, string directoriesToIgnore)
    {
        _executorProvider = executorProvider;
        _module = module;
        _codeFilePattern = codeFilePattern;
        _countSubmodules = countSubmodules;
        _directoriesToIgnore = directoriesToIgnore;
        InitializeComponent();
        UpstreamTranslation.Apply(this, Category);
        _lineCounter.Updated += OnLineCounterUpdated;
        Opened += (_, _) =>
        {
            _ = CountCommitsAsync();
            _ = Task.Run(CountLinesOfCode);
        };
        Closed += (_, _) => _lineCounter.Updated -= OnLineCounterUpdated;
    }

    /// <summary>
    ///  True once the lines of code are counted.
    /// </summary>
    public bool IsLinesOfCodeDone { get; private set; }

    // Upstream's InitializeCommitCount.
    private async Task CountCommitsAsync()
    {
        (int totalCommits, Dictionary<string, int> commitsPerUser) = await Task.Run(() => _module.GetCommitsByContributor());
        TotalCommits.Text = string.Format(CommitsText, totalCommits);

        StringBuilder text = new();
        List<decimal> values = [];
        List<string> labels = [];
        foreach ((string user, int commits) in commitsPerUser)
        {
            text.AppendLine($"{commits:N0} {user}");
            values.Add(commits);
            labels.Add(string.Format(CommitsBy, commits, user));
        }

        CommitCountPie.SetValues([.. values]);
        CommitCountPie.ToolTips = labels;
        CommitStatistics.Text = text.ToString();
    }

    // Upstream's InitializeLinesOfCode, run off the UI thread.
    private void CountLinesOfCode()
    {
        CountModule(_module);
        if (_countSubmodules)
        {
            foreach (GitModule submodule in _module.GetSubmodulesInfo()
                         .WhereNotNull()
                         .Select(info => new GitModule(_executorProvider, Path.Combine(_module.WorkingDir, info.LocalPath))))
            {
                CountModule(submodule);
            }
        }

        // The last update, when everything is counted.
        OnLineCounterUpdated(_lineCounter, EventArgs.Empty);
        Dispatcher.UIThread.Post(() => IsLinesOfCodeDone = true);

        void CountModule(IGitModule module)
        {
            List<string> files = [.. module.GetTree(commitId: default, full: true)
                .Select(file => Path.Combine(module.WorkingDir, file.Name))];
            _lineCounter.FindAndAnalyzeCodeFiles(_codeFilePattern, _directoriesToIgnore, files);
        }
    }

    // Upstream's OnLineCounterUpdated: the figures are copied on the counting thread, then shown on the UI thread.
    private void OnLineCounterUpdated(object? sender, EventArgs e)
    {
        List<KeyValuePair<string, int>> perExtension = [.. _lineCounter.LinesOfCodePerExtension];
        perExtension.Sort((first, next) => -first.Value.CompareTo(next.Value));

        StringBuilder perLanguage = new();
        List<decimal> values = [];
        List<string> labels = [];
        foreach ((string extension, int lines) in perExtension)
        {
            string line = string.Format(LinesOfCodeInFiles, lines, extension, (double)lines / _lineCounter.CodeLineCount);
            perLanguage.AppendLine(line);
            values.Add(lines);
            labels.Add(line);
        }

        Counts counts = new(_lineCounter.CodeLineCount, _lineCounter.TestCodeLineCount, _lineCounter.BlankLineCount,
            _lineCounter.CommentLineCount, _lineCounter.DesignerLineCount, _lineCounter.TotalLineCount);
        string perLanguageText = perLanguage.ToString();
        Dispatcher.UIThread.Post(() => ShowLinesOfCode(counts, perLanguageText, [.. values], labels));
    }

    // Upstream's UpdateUI.
    private void ShowLinesOfCode(Counts counts, string perLanguageText, decimal[] extensionValues, List<string> extensionLabels)
    {
        int productionLines = counts.Code - counts.Test;
        double percentTest = (double)counts.Test / counts.Code;
        double percentProduction = (double)productionLines / counts.Code;
        TotalLinesOfTestCode.Text = string.Format(LinesOfTestCode, counts.Test);
        TestCodePie.SetValues([counts.Test, productionLines]);
        TestCodePie.ToolTips =
        [
            string.Format(LinesOfTestCodeP, counts.Test, percentTest),
            string.Format(LinesOfProductionCodeP, productionLines, percentProduction),
        ];
        TestCodeText.Text = string.Join(Environment.NewLine, TestCodePie.ToolTips);

        LinesOfCodePie.SetValues([counts.Blank, counts.Comment, counts.Code, counts.Designer]);
        LinesOfCodePie.ToolTips =
        [
            string.Format(BlankLinesP, counts.Blank, (double)counts.Blank / counts.Total),
            string.Format(CommentLinesP, counts.Comment, (double)counts.Comment / counts.Total),
            string.Format(LinesOfCodeP, counts.Code, (double)counts.Code / counts.Total),
            string.Format(LinesOfDesignerFilesP, counts.Designer, (double)counts.Designer / counts.Total),
        ];
        LinesOfCodePerTypeText.Text = string.Join(Environment.NewLine, LinesOfCodePie.ToolTips);

        LinesOfCodePerLanguageText.Text = perLanguageText;
        LinesOfCodeExtensionPie.SetValues(extensionValues);
        LinesOfCodeExtensionPie.ToolTips = extensionLabels;
        TotalLinesOfCode2.Text = TotalLinesOfCode.Text = string.Format(LinesOfCode, counts.Code);
    }

    private readonly record struct Counts(int Code, int Test, int Blank, int Comment, int Designer, int Total);
}
