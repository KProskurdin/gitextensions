using System.Globalization;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class BlameOptionsTests
{
    private static readonly DateTime _time = new(2024, 5, 6, 7, 8, 0);

    [Test]
    public void Arguments_should_be_upstreams_flags_in_upstreams_order()
    {
        new BlameOptions().Arguments.Should().Equal("-w");
        new BlameOptions(IgnoreWhitespace: false, DetectMoveInFile: true, DetectMoveInAllFiles: true).Arguments.Should()
            .Equal("-M", "-C");
        new BlameOptions(DetectMoveInAllFiles: true).Arguments.Should().Equal("-C", "-w");
    }

    [Test]
    public void Toggles_should_keep_the_author_or_the_date_as_upstreams_menu()
    {
        BlameOptions noAuthor = new BlameOptions(ShowAuthorDate: false).ToggleShowAuthor();
        (noAuthor.ShowAuthor, noAuthor.ShowAuthorDate).Should().Be((false, true));

        BlameOptions noDate = new BlameOptions(ShowAuthor: false).ToggleShowAuthorDate();
        (noDate.ShowAuthor, noDate.ShowAuthorDate).Should().Be((true, false));

        BlameOptions dateBack = new BlameOptions(ShowAuthorDate: false).ToggleShowAuthorDate();
        (dateBack.ShowAuthor, dateBack.ShowAuthorDate).Should().Be((true, true));
    }

    [TestCase(false, true, true, true, "05/06/2024 07:08 - Ann")]
    [TestCase(true, true, true, true, "Ann - 05/06/2024 07:08")]
    [TestCase(false, true, true, false, "05/06/2024 - Ann")]
    [TestCase(false, false, true, true, "05/06/2024 07:08")]
    [TestCase(false, true, false, true, "Ann")]
    public void AuthorLine_should_be_built_as_upstreams_gutter(bool authorFirst, bool author, bool date, bool time,
        string expected)
    {
        BlameOptions options = new(DisplayAuthorFirst: authorFirst, ShowAuthor: author, ShowAuthorDate: date,
            ShowAuthorTime: time);

        options.AuthorLine(Line(1, "aaaa", "a.txt"), "a.txt", CultureInfo.InvariantCulture).Should().Be(expected);
    }

    [Test]
    public void AuthorLine_should_name_the_original_file_when_it_differs_and_the_setting_is_on()
    {
        BlameLine moved = Line(1, "aaaa", "old.txt");

        new BlameOptions().AuthorLine(moved, "a.txt", CultureInfo.InvariantCulture).Should()
            .Be("05/06/2024 07:08 - Ann - old.txt");
        new BlameOptions(ShowOriginalFilePath: false).AuthorLine(moved, "a.txt", CultureInfo.InvariantCulture).Should()
            .Be("05/06/2024 07:08 - Ann");
    }

    [Test]
    public void Rows_should_show_a_commit_once_per_run_and_line_numbers_when_chosen()
    {
        BlameLine[] lines = [Line(1, "aaaa", "a.txt"), Line(2, "aaaa", "a.txt"), Line(3, "bbbb", "a.txt"), Line(4, "aaaa", "a.txt")];

        IReadOnlyList<BlameRow> rows = new BlameOptions().Rows(lines, "a.txt", CultureInfo.InvariantCulture);
        IReadOnlyList<BlameRow> numbered = new BlameOptions(ShowLineNumbers: true)
            .Rows(lines, "a.txt", CultureInfo.InvariantCulture);

        rows.Select(row => row.Hash).Should().Equal("aaaa", "", "bbbb", "aaaa");
        rows[1].AuthorLine.Should().BeEmpty();
        rows.Select(row => row.LineNumberText).Should().AllBe("");
        numbered.Select(row => row.LineNumberText).Should().Equal("1", "2", "3", "4");
    }

    [Test]
    public void Parse_should_read_the_author_time_and_the_original_file()
    {
        const string hash = "0123456789abcdef0123456789abcdef01234567";
        string porcelain = $"{hash} 1 1 1\nauthor Ann\nauthor-time 1700000000\nfilename old/a.txt\n\tline\n";

        BlameLine line = BlameParser.Parse(porcelain).Single();

        line.AuthorTime.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1700000000).LocalDateTime);
        line.FileName.Should().Be("old/a.txt");
    }

    [Test]
    public void The_texts_keys_and_defaults_are_upstreams()
    {
        string root = RepositoryRoot();
        string settings = File.ReadAllText(Path.Combine(root, "src", "app", "GitCommands", "Settings", "AppSettings.cs"));
        string page = File.ReadAllText(Path.Combine(root, "src", "app", "GitUI", "CommandsDialogs", "SettingsDialog",
            "Pages", "BlameViewerSettingsPage.Designer.cs"));
        string pageCode = File.ReadAllText(Path.Combine(root, "src", "app", "GitUI", "CommandsDialogs", "SettingsDialog",
            "Pages", "BlameViewerSettingsPage.cs"));
        string menu = File.ReadAllText(Path.Combine(root, "src", "app", "GitUI", "CommandsDialogs",
            "FormFileHistory.Designer.cs"));
        string gitModule = File.ReadAllText(Path.Combine(root, "src", "app", "GitCommands", "Git", "GitModule.cs"));
        BlameOptions defaults = new();

        foreach ((string key, bool value) in (ValueTuple<string, bool>[])
                 [
                     ("IgnoreWhitespaceOnBlame", defaults.IgnoreWhitespace),
                     ("DetectCopyInFileOnBlame", defaults.DetectMoveInFile),
                     ("DetectCopyInAllOnBlame", defaults.DetectMoveInAllFiles),
                     ("Blame.DisplayAuthorFirst", defaults.DisplayAuthorFirst),
                     ("Blame.ShowAuthor", defaults.ShowAuthor), ("Blame.ShowAuthorDate", defaults.ShowAuthorDate),
                     ("Blame.ShowAuthorTime", defaults.ShowAuthorTime),
                     ("Blame.ShowLineNumbers", defaults.ShowLineNumbers),
                     ("Blame.ShowOriginalFilePath", defaults.ShowOriginalFilePath),
                 ])
        {
            settings.Should().Contain($"GetBool(\"{key}\", {(value ? "true" : "false")})");
        }

        gitModule.Should().Contain("{ AppSettings.DetectCopyInFileOnBlame, \"-M\" }")
            .And.Contain("{ AppSettings.DetectCopyInAllOnBlame, \"-C\" }")
            .And.Contain("{ AppSettings.IgnoreWhitespaceOnBlame, \"-w\" }");
        pageCode.Should().Contain(
            "\"Could prevent blame to calculate the accurate line number when blaming previous revisions.\"");
        foreach (string label in (string[])
                 [
                     "Blame settings", "Ignore whitespace", "Detect moved or copied lines within blamed file",
                     "Detect moved or copied lines from all files in same commit", "Display result settings",
                     "Display author first", "Show author", "Show author date", "Show author time", "Show line numbers",
                     "Show original file path",
                 ])
        {
            page.Should().Contain($".Text = \"{label}\";");
        }

        foreach (string label in (string[])
                 [
                     "Blame settings:", "Ignore whitespace", "Detect move and copy in this file",
                     "Detect move and copy in all files",
                 ])
        {
            menu.Should().Contain($".Text = \"{label}\";");
        }
    }

    private static BlameLine Line(int number, string hash, string fileName)
        => new(number, hash, "Ann", "2024-05-06", $"line {number}", _time, fileName);

    private static string RepositoryRoot([CallerFilePath] string testFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFile)!, "..", "..", ".."));
}
