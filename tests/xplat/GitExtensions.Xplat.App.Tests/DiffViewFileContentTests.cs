using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.Diff;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  Upstream FileViewer's whole files: a picture instead of an image's diff, a hex dump of a binary file, and the click a
///  large file waits for.
/// </summary>
internal sealed class DiffViewFileContentTests
{
    // A 1x1 PNG.
    private const string OnePixelPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    private const string ImageFile = "picture.png";
    private const string BinaryFile = "data.dat";
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;
    private Window _window = null!;
    private DiffView _diff = null!;

    [SetUp]
    public void SetUp()
    {
        _repo = new TestRepository();
    }

    [TearDown]
    public void TearDown()
    {
        _window.Close();
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void An_images_diff_in_a_commit_shows_the_picture_and_no_lines()
    {
        Commit(ImageFile, Png());
        Open();

        Run(_diff.ShowAsync(_repo.Path, "HEAD", ImageFile, staged: false, ImageFile));

        _diff.IsShowingImage.Should().BeTrue();
        _diff.FindControl<Image>("PictureBox")!.Source.Should().NotBeNull();
        _diff.FindControl<ListBox>("DiffList")!.IsVisible.Should().BeFalse();
        _diff.Lines.Should().BeEmpty();
    }

    [AvaloniaTest]
    public void A_working_directory_or_staged_image_shows_its_picture()
    {
        File.WriteAllBytes(FullPath(ImageFile), Png());
        Open();
        List<byte[]> decoded = [];
        Func<byte[], Bitmap> decode = _diff.DecodeImage;
        _diff.DecodeImage = bytes =>
        {
            decoded.Add(bytes);
            return decode(bytes);
        };

        Run(_diff.ShowAsync(_repo.Path, commitHash: null, ImageFile, staged: false, ImageFile));
        _diff.IsShowingImage.Should().BeTrue("an untracked image is read from the working directory");

        _repo.Run("add", ImageFile);
        File.WriteAllText(FullPath(ImageFile), "changed since it was staged");
        Run(_diff.ShowAsync(_repo.Path, commitHash: null, ImageFile, staged: true, ImageFile));

        _diff.IsShowingImage.Should().BeTrue();
        decoded[1].Should().Equal(Png(), "the index keeps what was staged");
    }

    [AvaloniaTest]
    public void A_deleted_image_keeps_its_diff()
    {
        Commit(ImageFile, Png());
        _repo.Run("rm", "-q", ImageFile);
        _repo.Run("commit", "-q", "-m", "delete it");
        Open();

        Run(_diff.ShowAsync(_repo.Path, "HEAD", ImageFile, staged: false, ImageFile));

        _diff.IsShowingImage.Should().BeFalse();
        _diff.Lines.Should().Contain(line => line.Text.StartsWith("Binary files", StringComparison.Ordinal));
    }

    [AvaloniaTest]
    public void An_image_that_cannot_be_decoded_shows_upstreams_message_and_hex_dump()
    {
        Commit(ImageFile, [1, 2, 3]);
        Open();
        _diff.DecodeImage = _ => throw new ArgumentException("not an image");

        Run(_diff.ShowAsync(_repo.Path, "HEAD", ImageFile, staged: false, ImageFile));

        _diff.IsShowingImage.Should().BeFalse();
        _diff.Lines.Select(line => line.Text).Should().Contain($"Cannot view image {ImageFile}");
        _diff.Lines.Should().Contain(line => line.Text.StartsWith("0000   01 02 03", StringComparison.Ordinal));
    }

    [AvaloniaTest]
    public void The_next_view_replaces_the_picture_and_a_late_picture_does_not_replace_the_next_view()
    {
        Commit(ImageFile, Png());
        Open();
        Run(_diff.ShowAsync(_repo.Path, "HEAD", ImageFile, staged: false, ImageFile));

        _diff.ShowText("a.txt", "one");

        _diff.IsShowingImage.Should().BeFalse();
        _diff.FindControl<ListBox>("DiffList")!.IsVisible.Should().BeTrue();
        _diff.FindControl<Image>("PictureBox")!.Source.Should().BeNull();

        Task late = _diff.ShowAsync(_repo.Path, "HEAD", ImageFile, staged: false, ImageFile);
        _diff.ShowText("a.txt", "two");
        Run(late);

        _diff.IsShowingImage.Should().BeFalse();
        _diff.Lines.Single().Text.Should().Be("two");
    }

    [AvaloniaTest]
    public void A_new_binary_files_diff_shows_its_hex_dump_and_a_changed_ones_stays_gits_line()
    {
        byte[] binary = [0, 0, 0, 0, 0, 0, 0, 65];
        Commit(BinaryFile, binary);
        Open();

        Run(_diff.ShowAsync(_repo.Path, "HEAD", BinaryFile, staged: false, BinaryFile));

        _diff.Lines.Select(line => line.Text).Should().Contain($"Binary file: {BinaryFile}");
        _diff.Lines.Should().Contain(line =>
            line.Text.StartsWith("0000   00 00 00 00 00 00 00 41", StringComparison.Ordinal));

        Commit(BinaryFile, [0, 0, 0, 0, 0, 0, 0, 66]);
        Run(_diff.ShowAsync(_repo.Path, "HEAD", BinaryFile, staged: false, BinaryFile));

        _diff.Lines.Should().Contain(line => line.Text.StartsWith("Binary files", StringComparison.Ordinal));
    }

    [AvaloniaTest]
    public void ShowFile_shows_a_text_with_line_numbers_a_binary_file_as_a_dump_and_an_error_for_a_missing_one()
    {
        Commit("text.txt", "one\ntwo\n"u8.ToArray());
        Commit(BinaryFile, [0, 0, 0, 0, 0, 0, 0, 65]);
        Open();

        Run(_diff.ShowFileAsync(_repo.Path, "HEAD", staged: false, "text.txt"));
        _diff.Lines.Select(line => line.Text).Should().Equal("one", "two");
        _diff.Lines[1].NewNumber.Should().Be("2");

        Run(_diff.ShowFileAsync(_repo.Path, "HEAD", staged: false, BinaryFile));
        _diff.Lines.Select(line => line.Text).Should().Contain($"Binary file: {BinaryFile}");

        Run(_diff.ShowFileAsync(_repo.Path, "HEAD", staged: false, "missing.txt"));
        _diff.FindControl<TextBlock>("ErrorText")!.IsVisible.Should().BeTrue();
    }

    [AvaloniaTest]
    public void A_file_of_gits_attributes_binary_shows_as_a_dump()
    {
        Commit(".gitattributes", "*.foo binary\n"u8.ToArray());
        Commit("a.foo", "plain text\n"u8.ToArray());
        Open();

        Run(_diff.ShowFileAsync(_repo.Path, "HEAD", staged: false, "a.foo"));

        _diff.Lines.Select(line => line.Text).Should().Contain("Binary file: a.foo");
    }

    [AvaloniaTest]
    public void A_large_file_waits_for_a_click()
    {
        const int MoreThanFiveMb = (5 * 1024 * 1024) + 1;
        File.WriteAllText(FullPath("large.txt"), new string('x', MoreThanFiveMb));
        Open();

        Run(_diff.ShowFileAsync(_repo.Path, commitHash: null, staged: false, "large.txt"));

        HyperlinkButton link = _diff.FindControl<HyperlinkButton>("ShowLargeFileLink")!;
        link.IsVisible.Should().BeTrue();
        (link.Content as string).Should().StartWith("This file is 5");
        _diff.Lines.Should().BeEmpty();

        link.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        WaitUntil(() => !link.IsVisible && _diff.Lines.Count == 1 && _diff.Lines[0].Text.Length == MoreThanFiveMb);
    }

    [AvaloniaTest]
    public void A_wheel_turn_on_a_picture_goes_to_the_next_or_previous_file()
    {
        Commit(ImageFile, Png());
        Open();
        Run(_diff.ShowAsync(_repo.Path, "HEAD", ImageFile, staged: false, ImageFile));
        int top = 0;
        int bottom = 0;
        _diff.TopScrollReached += (_, _) => top++;
        _diff.BottomScrollReached += (_, _) => bottom++;
        int delay = TestAppBuilder.Preferences.AutomaticContinuousScrollDelay;
        bool automatic = TestAppBuilder.Preferences.AutomaticContinuousScroll;
        TestAppBuilder.Preferences.AutomaticContinuousScrollDelay = 0;
        TestAppBuilder.Preferences.AutomaticContinuousScroll = true;
        try
        {
            Border panel = _diff.FindControl<Border>("PicturePanel")!;
            Wheel(panel, -1);
            bottom.Should().Be(1);
            Wheel(panel, 1);
            top.Should().Be(1);
        }
        finally
        {
            TestAppBuilder.Preferences.AutomaticContinuousScrollDelay = delay;
            TestAppBuilder.Preferences.AutomaticContinuousScroll = automatic;
        }
    }

    private void Wheel(Border panel, int delta)
    {
        Point point = panel.TranslatePoint(new Point(panel.Bounds.Width / 2, panel.Bounds.Height / 2), _window)!.Value;
        _window.MouseWheel(point, new Vector(0, delta), RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    private void Open()
    {
        _diff = new DiffView();
        _window = new Window { Content = _diff };
        _window.Show();
    }

    private void Commit(string file, byte[] content)
    {
        File.WriteAllBytes(FullPath(file), content);
        _repo.Run("add", file);
        _repo.Run("commit", "-q", "-m", $"commit {file}");
    }

    private string FullPath(string file) => Path.Combine(_repo.Path, file);

    private static byte[] Png() => Convert.FromBase64String(OnePixelPng);

    private static void Run(Task task)
    {
        WaitUntil(() => task.IsCompleted);
        task.Status.Should().Be(TaskStatus.RanToCompletion, task.Exception?.ToString());
        Dispatcher.UIThread.RunJobs();
    }

    private static void WaitUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + _timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Timed out waiting for the viewer");
            }

            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
    }
}
