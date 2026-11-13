using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

// Upstream's title bar help button, as F1 and a "?" button in the dialogs that show it.
internal sealed class DialogHelpTests
{
    private readonly List<string> _opened = [];
    private Action<string> _open = null!;

    [SetUp]
    public void Setup()
    {
        _opened.Clear();
        _open = DialogHelp.Open;
        DialogHelp.Open = _opened.Add;
    }

    [TearDown]
    public void TearDown() => DialogHelp.Open = _open;

    [AvaloniaTest]
    public void The_help_button_and_F1_open_the_dialogs_manual_page()
    {
        Window owner = new();
        owner.Show();
        DeleteBranchWindow window = new(["topic"], "main", [], null);
        _ = window.ShowDialog(owner);
        Dispatcher.UIThread.RunJobs();

        UiClicks.Raise(window.FindControl<Button>("HelpButton")!, Button.ClickEvent);
        window.KeyPress(Key.F1, RawInputModifiers.None, PhysicalKey.F1, keySymbol: null);

        _opened.Should().HaveCount(2).And.AllSatisfy(url => url.Should().EndWith("branches.html#delete-branch"));
        ToolTip.GetTip(window.FindControl<Button>("HelpButton")!).Should().Be(
            $"Read more about this feature at {_opened[0]}");
        owner.Close();
    }

    [AvaloniaTest]
    public void The_reset_windows_page_is_gits_documentation_of_the_chosen_type()
    {
        using TestRepository repo = new();
        Window owner = new();
        owner.Show();
        ResetCurrentBranchWindow window = new(repo.Path, new RepositoryOperationsViewModel(new GitOperations()),
            TestAppBuilder.Preferences, "HEAD~1", ResetMode.Soft, _ => Task.FromResult(true));
        _ = window.ShowDialog(owner);
        Dispatcher.UIThread.RunJobs();

        window.FindControl<RadioButton>("HardRadio")!.IsChecked = true;
        UiClicks.Raise(window.FindControl<Button>("HelpButton")!, Button.ClickEvent);

        _opened.Should().Equal("https://git-scm.com/docs/git-reset#Documentation/git-reset.txt---hard");
        owner.Close();
    }
}
