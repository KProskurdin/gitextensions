using GitExtensions.Xplat.Core.Platform;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

/// <summary>
///  The program that starts the app again, which from an AppImage is the AppImage file. Plain rules, so they run on every OS.
/// </summary>
internal sealed class AppExecutableTests
{
    private const string AppImage = "/home/me/Apps/GitExtensions-xplat-x86_64.AppImage";

    private static readonly string _mount = Path.Combine(Path.GetTempPath(), ".mount_GitExtAbc123");
    private static readonly string _mountedExecutable = Path.Combine(_mount, "usr", "lib", "gitextensions-xplat", "GitExtensions");

    [Test]
    public void For_should_give_the_AppImage_for_an_executable_in_its_mount()
    {
        AppExecutable.For(_mountedExecutable, AppImage, _mount).Should().Be(AppImage);
        AppExecutable.For(_mountedExecutable, AppImage, _mount + Path.DirectorySeparatorChar).Should().Be(AppImage);
    }

    [Test]
    public void For_should_keep_the_process_outside_an_AppImage()
    {
        string installed = Path.Combine(Path.GetTempPath(), "usr", "lib", "gitextensions-xplat", "GitExtensions");

        AppExecutable.For(installed, null, null).Should().Be(installed);
        AppExecutable.For(installed, "", "").Should().Be(installed);
        AppExecutable.For(null, AppImage, _mount).Should().BeNull();
    }

    [Test]
    public void For_should_ignore_an_inherited_AppImage_that_does_not_hold_the_process()
    {
        string installed = Path.Combine(Path.GetTempPath(), "usr", "lib", "gitextensions-xplat", "GitExtensions");

        AppExecutable.For(installed, AppImage, _mount).Should().Be(installed);
        AppExecutable.For(_mountedExecutable, AppImage, null).Should().Be(_mountedExecutable);

        // A folder whose name only starts with the mount's is another folder.
        string sibling = _mount + "X" + Path.DirectorySeparatorChar + "GitExtensions";
        AppExecutable.For(sibling, AppImage, _mount).Should().Be(sibling);
    }
}
