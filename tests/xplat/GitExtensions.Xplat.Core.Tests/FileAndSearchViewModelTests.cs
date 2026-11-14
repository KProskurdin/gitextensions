using AwesomeAssertions;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using NSubstitute;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class FileAndSearchViewModelTests
{
    private const string RepositoryPath = "/work/repo";

    private string _folder = null!;

    [SetUp]
    public void SetUp()
        => _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "xplat-vm-" + Guid.NewGuid().ToString("N")))
            .FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(_folder, recursive: true);

    [Test]
    public async Task RepositoryTextFileViewModel_should_save_and_close_with_true()
    {
        File.WriteAllText(Path.Combine(_folder, ".mailmap"), "Old <old@example.com>\n");
        FakeDialogHost host = new();
        RepositoryTextFileViewModel viewModel = new(host, _folder, RepositoryTextFiles.MailMap);
        await viewModel.LoadAsync();
        viewModel.Text.Should().Be("Old <old@example.com>\n");
        viewModel.Title.Should().Be(FakeDialogHost.TranslatedMark + "Edit .mailmap");
        viewModel.MayClose.Should().BeTrue();

        viewModel.Text = "New <new@example.com>";
        viewModel.MayClose.Should().BeFalse("the text changed");
        await viewModel.SaveCommand.ExecuteAsync(null);

        File.ReadAllText(Path.Combine(_folder, ".mailmap")).Should().Be("New <new@example.com>" + Environment.NewLine);
        host.Result.Should().Be(true);
        viewModel.MayClose.Should().BeTrue();
    }

    [TestCase(null, false, null)]
    [TestCase(false, true, false)]
    [TestCase(true, true, true)]
    public async Task RepositoryTextFileViewModel_should_close_after_the_question_as_answered(bool? answer, bool closes,
        bool? result)
    {
        FakeDialogHost host = new() { ThreeWayAnswer = answer };
        RepositoryTextFileViewModel viewModel = new(host, _folder, RepositoryTextFiles.GitAttributes);
        await viewModel.LoadAsync();
        viewModel.Text = "*.jpg binary";

        await viewModel.AskToCloseAsync();

        host.Questions.Should().ContainSingle();
        host.IsClosed.Should().Be(closes);
        host.Result.Should().Be(result);
        File.Exists(Path.Combine(_folder, ".gitattributes")).Should().Be(answer == true);
    }

    [Test]
    public async Task SearchViewModel_should_select_the_first_match_go_round_and_take_the_selected()
    {
        FakeDialogHost host = new();
        SearchViewModel viewModel = new(host, text => new[] { "alpha.txt", "beta.txt", "gamma.md" }.Where(name => name.Contains(text)));

        viewModel.SearchText = ".txt";
        await viewModel.Searched;

        viewModel.Candidates.Should().Equal("alpha.txt", "beta.txt");
        viewModel.SelectedIndex.Should().Be(0);
        viewModel.MoveUpCommand.Execute(null);
        viewModel.SelectedIndex.Should().Be(1, "Up from the first goes to the last");
        viewModel.MoveDownCommand.Execute(null);
        viewModel.MoveDownCommand.Execute(null);
        viewModel.TakeCommand.Execute(null);
        viewModel.SelectedItem.Should().Be("beta.txt");
        host.IsClosed.Should().BeTrue();
    }

    [Test]
    public async Task SearchViewModel_should_take_nothing_without_matches_or_when_cancelled()
    {
        SearchViewModel viewModel = new(new FakeDialogHost(), _ => ["a.txt"]);
        viewModel.SearchText = "a";
        await viewModel.Searched;

        viewModel.CancelCommand.Execute(null);
        viewModel.SelectedItem.Should().BeNull();

        SearchViewModel empty = new(new FakeDialogHost(), _ => []);
        empty.SearchText = "x";
        await empty.Searched;
        empty.HasCandidates.Should().BeFalse();
        empty.TakeCommand.Execute(null);
        empty.SelectedItem.Should().BeNull();
    }

    [Test]
    public void ChooseTranslationViewModel_should_list_english_first_with_flags_and_store_the_choice()
    {
        File.WriteAllBytes(Path.Combine(_folder, "German.gif"), [0]);
        InMemoryAppPreferences preferences = new() { Translation = "" };
        FakeDialogHost host = new();
        ChooseTranslationViewModel viewModel = new(host, preferences, ["Japanese", "English", "German"], _folder);

        viewModel.Choices.Should().Equal(new TranslationChoice("English", null),
            new TranslationChoice("German", Path.Join(_folder, "German.gif")), new TranslationChoice("Japanese", null));

        viewModel.ChooseCommand.Execute(viewModel.Choices[1]);

        preferences.Translation.Should().Be("German");
        host.IsClosed.Should().BeTrue();
    }

    [Test]
    public void ChooseTranslationViewModel_should_keep_english_when_closed_without_a_choice()
    {
        InMemoryAppPreferences preferences = new() { Translation = "" };
        ChooseTranslationViewModel viewModel = new(new FakeDialogHost(), preferences, [], _folder);

        viewModel.OnClosing();

        preferences.Translation.Should().Be(ChooseTranslationViewModel.English);
    }

    [Test]
    public async Task WorktreesViewModel_should_offer_only_what_upstream_allows_and_reload_after_a_change()
    {
        IRepositoryService service = Substitute.For<IRepositoryService>();
        IReadOnlyList<WorktreeInfo> worktrees =
        [
            new WorktreeInfo(RepositoryPath, RepositoryPath, IsMain: true, IsDeleted: false, Branch: "main"),
            new WorktreeInfo("/work/gone", "/work/gone", IsMain: false, IsDeleted: true, Branch: "old"),
        ];
        service.GetWorktreesAsync(RepositoryPath).Returns(worktrees);
        IWorktreeActions actions = Substitute.For<IWorktreeActions>();
        actions.CreateAsync(RepositoryPath, RepositoryPath).Returns(true);
        WorktreesViewModel viewModel = new(new FakeDialogHost(), RepositoryPath, service, actions);

        await viewModel.LoadAsync();
        viewModel.SelectedIndex = 0;

        viewModel.OpenCommand.CanExecute(null).Should().BeFalse("it is the open worktree");
        viewModel.DeleteCommand.CanExecute(null).Should().BeFalse();
        viewModel.PruneCommand.CanExecute(null).Should().BeTrue("a worktree lost its folder");
        await viewModel.CreateCommand.ExecuteAsync(null);
        viewModel.ShouldRefresh.Should().BeTrue();
        await service.Received(2).GetWorktreesAsync(RepositoryPath);
    }
}
