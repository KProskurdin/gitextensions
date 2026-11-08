using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  Upstream FormStatus's "Keep dialog open" in the output window of an operation.
/// </summary>
internal sealed class ProcessWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;
    private bool _closeProcessDialog;

    [SetUp]
    public void SetUp()
    {
        _repo = new TestRepository();
        _closeProcessDialog = TestAppBuilder.Preferences.CloseProcessDialog;
    }

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.Preferences.CloseProcessDialog = _closeProcessDialog;
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void Keep_dialog_open_shows_the_setting_and_turning_it_off_after_success_closes_the_window()
    {
        TestAppBuilder.Preferences.CloseProcessDialog = false;
        (ProcessWindow process, CheckBox keep) = RunOperation();

        keep.IsChecked.Should().BeTrue();
        keep.IsVisible.Should().BeTrue();
        process.IsVisible.Should().BeTrue("upstream keeps the dialog open");

        keep.IsChecked = false;

        TestAppBuilder.Preferences.CloseProcessDialog.Should().BeTrue();
        WaitUntil(() => !process.IsVisible);
    }

    [AvaloniaTest]
    public void With_the_setting_off_a_successful_operation_closes_the_window_by_itself()
    {
        TestAppBuilder.Preferences.CloseProcessDialog = true;

        (ProcessWindow process, CheckBox keep) = RunOperation();

        keep.IsChecked.Should().BeFalse();
        WaitUntil(() => !process.IsVisible);
    }

    private (ProcessWindow Process, CheckBox Keep) RunOperation()
    {
        Window owner = new();
        owner.Show();
        RepositoryOperationsViewModel actions = new(new GitOperations());
        ProcessWindow process = new(actions, TestAppBuilder.Preferences);
        CheckBox keep = process.FindControl<CheckBox>("KeepDialogOpen")!;
        process.Show(owner);
        Task<bool> run = actions.RebaseAsync(_repo.Path, "HEAD", "rebase HEAD", interactive: false);
        WaitUntil(() => run.IsCompleted);
        run.IsCompletedSuccessfully.Should().BeTrue();
        return (process, keep);
    }

    private static void WaitUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + _timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Timed out waiting for the UI");
            }

            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
    }
}
