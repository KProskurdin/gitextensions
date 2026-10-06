using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class AskPassWindowTests
{
    [AvaloniaTest]
    public void Ok_returns_the_typed_password_which_is_shown_hidden()
    {
        AskPassWindow window = new("Enter passphrase for key '/home/me/.ssh/id_ed25519': ");
        window.Show();
        TextBox answer = Find<TextBox>(window, "AnswerBox");

        answer.PasswordChar.Should().NotBe(default(char));
        Find<TextBlock>(window, "PromptText").Text.Should().Be("Enter passphrase for key '/home/me/.ssh/id_ed25519':");
        answer.Text = "s3cret";
        Click(window, "OkButton");

        window.IsVisible.Should().BeFalse();
        window.Answer.Should().Be("s3cret");
    }

    [AvaloniaTest]
    public void A_user_name_is_typed_in_the_clear()
    {
        AskPassWindow window = new("Username for 'https://example.com': ");
        window.Show();

        Find<TextBox>(window, "AnswerBox").PasswordChar.Should().Be(default(char));
        window.Close();
    }

    [AvaloniaTest]
    public void Cancel_returns_no_answer()
    {
        AskPassWindow window = new("Password for 'https://example.com': ");
        window.Show();
        Find<TextBox>(window, "AnswerBox").Text = "typed";

        Click(window, "CancelButton");

        window.IsVisible.Should().BeFalse();
        window.Answer.Should().BeNull();
    }

    private static void Click(Window window, string buttonName)
    {
        Find<Button>(window, buttonName).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static T Find<T>(Window window, string name) where T : Control
        => window.FindControl<T>(name) ?? throw new InvalidOperationException($"Control {name} not found");
}
