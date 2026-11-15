using System.Text;
using AwesomeAssertions;
using GitCommands.Settings;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class DiffViewModelTests
{
    private const string RepositoryPath = "/work/repo";

    private const string SampleDiff =
        "diff --git a/a.txt b/a.txt\n" +
        "--- a/a.txt\n" +
        "+++ b/a.txt\n" +
        "@@ -1,3 +1,3 @@\n" +
        " first\n" +
        "-old\n" +
        "+new\n" +
        " last\n";

    private RecordingDiffService _diff = null!;
    private InMemoryAppPreferences _preferences = null!;
    private List<(string Path, bool ShowLarge)> _fileReads = null!;
    private ViewedFile _file = null!;
    private DateTime _now;

    [SetUp]
    public void Setup()
    {
        _diff = new RecordingDiffService(DiffParser.Parse(SampleDiff));
        _preferences = new InMemoryAppPreferences();
        _fileReads = [];
        _file = new ViewedFile { Text = "text\n" };
        _now = new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Local);
    }

    [Test]
    public async Task ShowAsync_should_show_the_diff_with_its_options_and_title()
    {
        DiffViewModel viewModel = NewViewModel();

        await viewModel.ShowAsync(RepositoryPath, commitHash: null, "a.txt", staged: false, "a.txt (working directory)");

        viewModel.Title.Should().Be("a.txt (working directory)");
        viewModel.Content.Lines.Should().HaveCount(8);
        viewModel.Content.IsDiff.Should().BeTrue();
        viewModel.Content.SyntaxPath.Should().Be("a.txt");
        viewModel.HasOptions.Should().BeTrue();
        viewModel.ErrorMessage.Should().BeNull();
        _diff.Options.Should().ContainSingle().Which.ContextLines.Should().Be(DiffOptions.DefaultContextLines);
    }

    [Test]
    public async Task An_option_that_changes_the_diff_should_read_it_again_and_keep_it_for_the_run()
    {
        DiffViewModel viewModel = await ShownDiffAsync();
        List<string?> changed = [];
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.IgnoreAllWhitespace = true;
        await WaitUntilAsync(() => _diff.Options.Count == 2);

        _diff.Options[^1].Arguments.Should().Contain("--ignore-all-space");
        viewModel.IgnoreAllWhitespace.Should().BeTrue();
        changed.Should().Contain([nameof(DiffViewModel.IgnoreAllWhitespace), nameof(DiffViewModel.IgnoreWhitespaceEol)]);
        _preferences.InitialDiffOptions().IgnoreWhitespace.Should().Be(IgnoreWhitespaceKind.AllSpace);

        viewModel.IgnoreWhitespaceChange = true;

        viewModel.IgnoreAllWhitespace.Should().BeFalse("upstream's three whitespace choices are one setting");
        viewModel.Options.IgnoreWhitespace.Should().Be(IgnoreWhitespaceKind.Change);
    }

    [Test]
    public async Task An_option_that_changes_only_the_display_should_draw_the_same_lines_again_without_reading()
    {
        DiffViewModel viewModel = await ShownDiffAsync();
        DiffContent before = viewModel.Content;

        viewModel.ShowNonPrintingChars = true;

        _diff.Options.Should().HaveCount(1);
        viewModel.Content.Should().NotBeSameAs(before);
        viewModel.Content.Lines.Should().BeSameAs(before.Lines);
    }

    [Test]
    public async Task The_context_commands_should_follow_the_entire_file_as_upstreams()
    {
        DiffViewModel viewModel = await ShownDiffAsync();

        viewModel.IncreaseContextCommand.Execute(null);

        viewModel.Options.ContextLines.Should().Be(4);
        viewModel.IncreaseContextTip.Should().EndWith("(4)");
        viewModel.CanDecreaseContext.Should().BeTrue();

        viewModel.ShowEntireFile = true;

        viewModel.CanIncreaseContext.Should().BeFalse();
        viewModel.CanDecreaseContext.Should().BeFalse();
    }

    [Test]
    public async Task The_menu_should_offer_line_actions_only_for_a_patch_the_window_can_stage()
    {
        DiffViewModel viewModel = await ShownDiffAsync();
        viewModel.RefreshMenu();
        viewModel.IsStageLinesVisible.Should().BeFalse();
        viewModel.IsResetLinesVisible.Should().BeFalse();

        viewModel.LineAction = DiffLineAction.Unstage;
        viewModel.SetSelection(6, [6]);

        viewModel.IsUnstageLinesVisible.Should().BeTrue();
        viewModel.IsStageLinesVisible.Should().BeFalse();
        viewModel.IsResetLinesVisible.Should().BeTrue();
        viewModel.HasSelectedLines.Should().BeTrue();
        viewModel.IsDiffAppearanceVisible.Should().BeTrue();
        viewModel.IsPartialTextView.Should().BeTrue();

        DiffLinesCommand? requested = null;
        viewModel.LinesRequested += (_, command) => requested = command;
        viewModel.RequestLinesCommand.Execute(DiffLinesCommand.Unstage);
        requested.Should().Be(DiffLinesCommand.Unstage);

        viewModel.ShowText("a.txt", "text");

        viewModel.IsUnstageLinesVisible.Should().BeFalse("a file's text has no hunks to patch");
        viewModel.HasSelectedLines.Should().BeFalse();
        viewModel.IsPartialTextView.Should().BeFalse();
        viewModel.HasOptions.Should().BeFalse();
    }

    [Test]
    public async Task A_word_diff_should_hide_the_patch_copies_and_the_line_actions()
    {
        DiffViewModel viewModel = await ShownDiffAsync();
        viewModel.LineAction = DiffLineAction.Stage;

        viewModel.TryToggleGitWordDiff().Should().BeTrue();
        await WaitUntilAsync(() => _diff.Options.Count == 2);

        _preferences.DiffDisplayAppearance.Should().Be(DiffDisplayAppearance.GitWordDiff);
        _diff.Options[^1].Appearance.Should().Be(DiffDisplayAppearance.GitWordDiff);
        viewModel.IsGitWordDiffAppearance.Should().BeTrue();
        viewModel.IsCopyPatchVisible.Should().BeFalse();
        viewModel.AreCopyVersionsVisible.Should().BeFalse();
        viewModel.IsStageLinesVisible.Should().BeFalse();

        viewModel.ToggleAppearanceCommand.Execute(DiffDisplayAppearance.GitWordDiff);

        viewModel.IsPatchAppearance.Should().BeTrue("upstream's toggle goes back to the patch");
        viewModel.TryToggleDifftastic().Should().BeFalse("git does not know Difftastic here");
    }

    [Test]
    public async Task Copy_new_version_should_copy_the_selected_lines_as_the_new_file_has_them()
    {
        DiffViewModel viewModel = await ShownDiffAsync();
        List<string> copied = [];
        viewModel.SetClipboard = text =>
        {
            copied.Add(text);
            return Task.CompletedTask;
        };
        viewModel.SetSelection(4, [6, 4, 5]);

        await viewModel.CopyNewVersionCommand.ExecuteAsync(null);
        await viewModel.CopyPatchCommand.ExecuteAsync(null);

        copied.Should().Equal("first\nnew", " first\n-old\n+new");
    }

    [Test]
    public async Task Next_change_should_ask_the_view_for_the_first_line_of_the_next_change()
    {
        DiffViewModel viewModel = await ShownDiffAsync();
        List<int> requested = [];
        viewModel.SelectLineRequested += (_, index) => requested.Add(index);
        viewModel.SetSelection(-1, []);

        viewModel.GoToChangeCommand.Execute(true);

        requested.Should().Equal(5);
        viewModel.SetSelection(5, [5]);
        viewModel.CurrentFileLine.Should().Be(2, "a removed line has its old number");
    }

    [Test]
    public async Task The_toolbar_should_show_while_the_pointer_is_over_a_diff_with_the_repositorys_encoding()
    {
        DiffViewModel viewModel = await ShownDiffAsync();

        viewModel.IsPointerOver = true;

        viewModel.IsToolbarVisible.Should().BeTrue();
        viewModel.SelectedEncoding.Should().Be(Encoding.UTF8.EncodingName);
        _diff.Options.Should().HaveCount(1, "showing the repository's encoding reads nothing again");

        viewModel.SelectedEncoding = Encoding.Latin1.EncodingName;
        await WaitUntilAsync(() => _diff.Options.Count == 2);

        _diff.Options[^1].Encoding.Should().Be(Encoding.Latin1);

        viewModel.ShowText("a.txt", content: null);

        viewModel.IsToolbarVisible.Should().BeFalse("a file's text has no options");
        viewModel.ErrorMessage.Should().Be(DiffViewModel.BinaryFileNotShown);
    }

    [Test]
    public async Task A_large_file_should_wait_for_the_link_and_then_be_read_whole()
    {
        _file = new ViewedFile { TooLarge = 3 * 1024 * 1024 };
        DiffViewModel viewModel = NewViewModel();

        await viewModel.ShowFileAsync(RepositoryPath, commitHash: "abc", staged: false, "big.txt");

        viewModel.LargeFileText.Should().StartWith("[t]This file is 3");
        viewModel.Content.Lines.Should().BeEmpty();

        _file = new ViewedFile { Text = "a\nb\n" };
        await viewModel.ShowLargeFileCommand.ExecuteAsync(null);

        _fileReads.Should().Equal(("big.txt", false), ("big.txt", true));
        viewModel.LargeFileText.Should().BeNull();
        viewModel.Content.Lines.Select(line => line.Text).Should().Equal("a", "b");
    }

    [Test]
    public async Task An_image_should_show_as_the_decoded_picture_which_the_next_view_disposes()
    {
        _file = new ViewedFile { Image = [1, 2, 3] };
        DiffViewModel viewModel = NewViewModel();
        DisposablePicture picture = new();
        viewModel.DecodeImage = _ => picture;

        await viewModel.ShowAsync(RepositoryPath, commitHash: "abc", "logo.png", staged: false, "logo.png");

        viewModel.IsShowingImage.Should().BeTrue();
        viewModel.Picture.Should().BeSameAs(picture);
        _diff.Options.Should().BeEmpty("an image that the revision has shows instead of its diff");

        viewModel.ShowText("a.txt", "text");

        viewModel.IsShowingImage.Should().BeFalse();
        picture.IsDisposed.Should().BeTrue();
    }

    [Test]
    public async Task An_image_that_cannot_be_decoded_should_show_upstreams_hex_dump()
    {
        _file = new ViewedFile { Image = [0x41, 0x42] };
        DiffViewModel viewModel = NewViewModel();
        viewModel.DecodeImage = _ => throw new InvalidOperationException("not an image");

        await viewModel.ShowFileAsync(RepositoryPath, commitHash: "abc", staged: false, "logo.png");

        viewModel.IsShowingImage.Should().BeFalse();
        viewModel.Content.Lines[0].Text.Should().StartWith("[t]Cannot view image");
    }

    [Test]
    public async Task Git_greps_matches_should_be_copied_as_the_lines_shown()
    {
        DiffViewModel viewModel = NewViewModel();
        GrepLine[] grep = [new(3, GrepLineKind.Match, "found here", [(0, 5)]), new(null, GrepLineKind.Separator, "--", [])];
        string? copied = null;
        viewModel.SetClipboard = text =>
        {
            copied = text;
            return Task.CompletedTask;
        };

        await viewModel.ShowGrepAsync("a.txt", _ => Task.FromResult<(IReadOnlyList<GrepLine>, string?)>((grep, null)));
        await viewModel.CopyCommand.ExecuteAsync(null);

        viewModel.Content.Grep.Should().BeSameAs(grep);
        viewModel.HasOptions.Should().BeTrue();
        viewModel.TryToggleTreatAllFilesAsText().Should().BeTrue("git-grep's matches are a partial text view");
        copied.Should().Be("found here\n--");
    }

    [Test]
    public void A_scroll_past_the_end_should_ask_for_another_file_only_with_alt_or_the_setting_and_not_too_fast()
    {
        DiffViewModel viewModel = NewViewModel();

        viewModel.TakeScrollReached(altHeld: false).Should().BeFalse();
        viewModel.TakeScrollReached(altHeld: true).Should().BeTrue();
        viewModel.TakeScrollReached(altHeld: true).Should().BeFalse("upstream waits its delay between two files");

        _now += TimeSpan.FromMilliseconds(_preferences.AutomaticContinuousScrollDelay);
        _preferences.AutomaticContinuousScroll = true;

        viewModel.TakeScrollReached(altHeld: false).Should().BeTrue();
    }

    [Test]
    public async Task A_failed_read_should_show_gits_error_instead_of_the_lines()
    {
        _diff = new RecordingDiffService(new InvalidOperationException("fatal: bad revision"));
        DiffViewModel viewModel = NewViewModel();

        await viewModel.ShowAsync(RepositoryPath, commitHash: "nope", "a.txt", staged: false, "a.txt");

        viewModel.ErrorMessage.Should().Be("fatal: bad revision");
        viewModel.Content.Lines.Should().BeEmpty();
        viewModel.WholeRange().Should().BeNull();
    }

    private async Task<DiffViewModel> ShownDiffAsync()
    {
        DiffViewModel viewModel = NewViewModel();
        await viewModel.ShowAsync(RepositoryPath, commitHash: null, "a.txt", staged: false, "a.txt");
        return viewModel;
    }

    private DiffViewModel NewViewModel()
        => new(_diff, _preferences, new DiffViewModelServices
        {
            ReadFile = (_, _, path, _, showLarge) =>
            {
                _fileReads.Add((path, showLarge));
                return Task.FromResult(_file);
            },
            IsDifftasticEnabled = _ => false,
            FilesEncoding = _ => Encoding.UTF8,
            Encodings = () => [Encoding.UTF8, Encoding.Latin1],
            Translate = text => FakeDialogHost.TranslatedMark + text,
            Now = () => _now,
        });

    // A change of options reads again without waiting; the fake answers at once, on another thread.
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Timed out");
            }

            await Task.Delay(10);
        }
    }

    private sealed class RecordingDiffService : IDiffService
    {
        private readonly IReadOnlyList<DiffLine> _lines;
        private readonly Exception? _failure;

        public RecordingDiffService(IReadOnlyList<DiffLine> lines)
        {
            _lines = lines;
        }

        public RecordingDiffService(Exception failure)
        {
            _lines = [];
            _failure = failure;
        }

        public List<DiffOptions> Options { get; } = [];

        public Task<IReadOnlyList<DiffLine>> GetDiffAsync(string repositoryPath, string? commitHash, string? filePath,
            bool staged, DiffOptions? options = null)
        {
            lock (Options)
            {
                Options.Add(options ?? new DiffOptions());
            }

            return _failure is null ? Task.FromResult(_lines) : Task.FromException<IReadOnlyList<DiffLine>>(_failure);
        }
    }

    private sealed class DisposablePicture : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }
}
