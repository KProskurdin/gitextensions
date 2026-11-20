using System.Runtime.CompilerServices;
using AwesomeAssertions;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class SelectionFilterViewModelTests
{
    private static readonly FileChange[] _files =
    [
        new("src/Widget.cs", ChangeKind.Modified, Staged: false),
        new("src/widget.txt", ChangeKind.Modified, Staged: false),
        new("README.md", ChangeKind.Untracked, Staged: false),
    ];

    private readonly List<IReadOnlyList<FileChange>> _selections = [];

    [SetUp]
    public void SetUp() => _selections.Clear();

    [Test]
    public void Apply_should_select_the_files_whose_path_matches_ignoring_case_and_remember_the_expression()
    {
        SelectionFilterViewModel filter = New();

        filter.Apply("widget\\.").Should().Be(2);

        _selections.Single().Select(change => change.Path).Should().Equal("src/Widget.cs", "src/widget.txt");
        filter.History.Should().Equal("widget\\.");
        filter.ToolTip.Should().Be(SelectionFilterViewModel.ToolTipText);
    }

    [Test]
    public void Apply_should_select_everything_for_an_empty_expression_and_not_remember_one_without_matches()
    {
        SelectionFilterViewModel filter = New();

        filter.Apply("nothing").Should().Be(0);
        filter.Apply("").Should().Be(3);

        _selections.Select(selection => selection.Count).Should().Equal(0, 3);
        filter.History.Should().Equal("");
    }

    [Test]
    public void Apply_should_show_the_error_of_an_invalid_expression_in_the_tooltip_and_keep_the_selection()
    {
        SelectionFilterViewModel filter = New();

        filter.Apply("(").Should().Be(0);

        _selections.Should().BeEmpty();
        filter.ToolTip.Should().StartWith("Error ");
        filter.Apply("md").Should().Be(1);
        filter.ToolTip.Should().Be(SelectionFilterViewModel.ToolTipText);
    }

    [Test]
    public void History_should_keep_upstreams_ten_latest_expressions_first_without_repeats()
    {
        SelectionFilterViewModel filter = New();

        for (int i = 0; i < 12; i++)
        {
            filter.Apply($"s{i}|src");
        }

        filter.Apply("s11|src");

        filter.History.Should().HaveCount(10);
        filter.History[0].Should().Be("s11|src");
        filter.History[^1].Should().Be("s2|src");
    }

    [Test]
    public async Task Typing_should_select_once_the_typing_pauses()
    {
        SelectionFilterViewModel filter = New();

        filter.Text = "R";
        filter.Text = "READ";
        await Task.Delay(SelectionFilterViewModel.TypingPause * 3);

        _selections.Should().ContainSingle().Which.Select(change => change.Path).Should().Equal("README.md");
    }

    [Test]
    public void Toggle_should_show_and_hide_starting_from_the_setting()
    {
        SelectionFilterViewModel filter = New(visible: true);

        filter.Toggle().Should().BeFalse();
        filter.Toggle().Should().BeTrue();
        filter.IsVisible.Should().BeTrue();
    }

    [Test]
    public void The_texts_are_upstreams()
    {
        string source = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "app", "GitUI", "CommandsDialogs",
            "FormCommit.cs"));
        string designer = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "app", "GitUI", "CommandsDialogs",
            "FormCommit.Designer.cs"));

        source.Should().Contain($"\"{SelectionFilterViewModel.ToolTipText}\"")
            .And.Contain($"\"{SelectionFilterViewModel.ErrorToolTipText}\"");
        designer.Should().Contain($"toolStripLabel1.Text = \"{SelectionFilterViewModel.LabelText}\"");
    }

    private SelectionFilterViewModel New(bool visible = false)
    {
        SelectionFilterViewModel filter = new(visible, () => _files, text => text);
        filter.SelectRequested += (_, changes) => _selections.Add(changes);
        return filter;
    }

    private static string RepositoryRoot([CallerFilePath] string testFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFile)!, "..", "..", ".."));
}
