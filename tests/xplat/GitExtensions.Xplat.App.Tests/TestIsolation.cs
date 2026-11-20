using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

[assembly: GitExtensions.Xplat.App.Tests.TestIsolation]

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  Keeps each test from carrying the ones before it. The windows a test left open are closed after it: an open window
///  stays in Avalonia's render loop with its view models, its repository's data and its timers, and through the app's
///  shared output history every window opened before it; the test host grew to 7 GB, and the windows left open slowed
///  later tests down by reacting to them. Most tests end with their window open. Each test also gets an empty recent
///  repositories list, which every opened repository joined, so that the Start menu and the dashboard of a late test
///  listed hundreds (the recent repositories test took a minute).
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
internal sealed class TestIsolationAttribute : Attribute, ITestAction
{
    // A window that opens another one as it closes (an error about a cancelled read) is closed in the next round.
    private const int MaxRounds = 5;

    private static readonly List<Window> _openWindows = [];
    private static bool _tracking;

    public ActionTargets Targets => ActionTargets.Test;

    public void BeforeTest(ITest test)
    {
        // A main window joins the app's output history when it is made and leaves it when it closes; a window a test makes
        // but never shows never opens nor closes, so each test gets a history of its own, which drops such windows with it.
        AppServices.OutputHistory = new OutputHistory(TestAppBuilder.Preferences.OutputHistoryDepth);
        AppServices.RecentRepositories = new InMemoryRecentRepositoryStore();
        if (_tracking)
        {
            return;
        }

        _tracking = true;
        Window.WindowOpenedEvent.AddClassHandler(typeof(Window), (sender, _) => _openWindows.Add((Window)sender!),
            RoutingStrategies.Direct, handledEventsToo: true);
        Window.WindowClosedEvent.AddClassHandler(typeof(Window), (sender, _) => _openWindows.Remove((Window)sender!),
            RoutingStrategies.Direct, handledEventsToo: true);
    }

    public void AfterTest(ITest test)
    {
        // The headless test command runs the test actions on the UI thread, as the tests themselves.
        Dispatcher.UIThread.VerifyAccess();
        for (int round = 0; round < MaxRounds && _openWindows.Count > 0; round++)
        {
            // Dialogs first: they opened after their owners.
            foreach (Window window in Enumerable.Reverse(_openWindows.ToList()))
            {
                window.Close();
            }

            Dispatcher.UIThread.RunJobs();
        }

        // A window that refused to close (its Closing cancelled) is dropped from the list, so it is not closed again
        // after every later test.
        _openWindows.Clear();
    }
}
