using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using GitExtensions.Xplat.App;

[assembly: AvaloniaTestApplication(typeof(GitExtensions.Xplat.App.Tests.TestAppBuilder))]

namespace GitExtensions.Xplat.App.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
