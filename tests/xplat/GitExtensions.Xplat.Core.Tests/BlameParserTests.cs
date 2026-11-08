using AwesomeAssertions;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class BlameParserTests
{
    private const string FirstHash = "0123456789abcdef0123456789abcdef01234567";
    private const string SecondHash = "fedcba9876543210fedcba9876543210fedcba98";

    // Two lines from one commit and one from another, in the --line-porcelain layout where each line repeats its headers.
    private const string Porcelain =
        FirstHash + " 1 1 1\n" +
        "author Alice\n" +
        "author-time 1700000000\n" +
        "summary first\n" +
        "filename a.txt\n" +
        "\tfirst line\n" +
        FirstHash + " 2 2 1\n" +
        "author Alice\n" +
        "author-time 1700000000\n" +
        "summary first\n" +
        "filename a.txt\n" +
        "\tsecond line\n" +
        SecondHash + " 3 3 1\n" +
        "author Bob\n" +
        "author-mail <bob@example.com>\n" +
        "author-time 1700086400\n" +
        "summary second\n" +
        "filename a.txt\n" +
        "\tthird line\n";

    [Test]
    public void Parse_should_give_each_line_its_number_commit_author_and_content()
    {
        IReadOnlyList<BlameLine> lines = BlameParser.Parse(Porcelain);

        lines.Should().HaveCount(3);
        lines[0].Should()
            .BeEquivalentTo(new { LineNumber = 1, Hash = "01234567", Author = "Alice", Content = "first line" });
        lines[1].LineNumber.Should().Be(2);
        lines[1].Content.Should().Be("second line");
        lines[2].Should()
            .BeEquivalentTo(new { LineNumber = 3, Hash = "fedcba98", Author = "Bob", Content = "third line" });
        lines[2].AuthorEmail.Should().Be("bob@example.com");
        lines[0].AuthorEmail.Should().BeEmpty();
    }

    [Test]
    public void Parse_should_format_the_author_date_as_a_day()
    {
        IReadOnlyList<BlameLine> lines = BlameParser.Parse(Porcelain);

        lines[0].Date.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1700000000).LocalDateTime.ToString("yyyy-MM-dd"));
    }

    [Test]
    public void Parse_of_empty_output_gives_no_lines()
    {
        BlameParser.Parse("").Should().BeEmpty();
    }

    [Test]
    public void Parse_keeps_tabs_inside_content()
    {
        string text = FirstHash + " 1 1 1\nauthor A\nauthor-time 0\n\tcol1\tcol2\n";

        BlameParser.Parse(text).Should().ContainSingle().Which.Content.Should().Be("col1\tcol2");
    }

    [Test]
    public void Parse_should_decode_the_files_lines_and_the_commit_headers_each_in_their_own_encoding()
    {
        string author = System.Text.Encoding.Latin1.GetString(System.Text.Encoding.UTF8.GetBytes("Zoë"));
        string porcelain = $"0123456789012345678901234567890123456789 1 1 1\nauthor {author}\nauthor-time 1\nsummary s\nfilename a.txt\n\tcafé\n";

        BlameLine line = BlameParser.Parse(porcelain, System.Text.Encoding.Latin1, System.Text.Encoding.UTF8).Single();

        line.Author.Should().Be("Zoë");
        line.Content.Should().Be("café");
    }

    [Test]
    public void WithSyntax_should_color_a_known_file_type_and_leave_others_alone()
    {
        const string porcelain = "0123456789012345678901234567890123456789 1 1 1\nauthor A\nauthor-time 1\nsummary s\nfilename a\n\tpublic class X\n";
        IReadOnlyList<BlameRow> rows = new BlameOptions().Rows(BlameParser.Parse(porcelain), "a", System.Globalization.CultureInfo.InvariantCulture);

        BlameRow.WithSyntax(rows, "a.cs").Single().Syntax.Should().Contain(run => run.Style != null);
        BlameRow.WithSyntax(rows, "a.no-such-type").Single().Syntax.Should().BeNull();
    }
}
