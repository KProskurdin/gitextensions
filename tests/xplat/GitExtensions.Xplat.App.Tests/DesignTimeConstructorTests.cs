using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using AwesomeAssertions;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class DesignTimeConstructorTests
{
    // The windows the XAML previewer and Avalonia's runtime loader can create: every window of the app.
    private static IEnumerable<Type> Windows()
        => typeof(App).Assembly.GetTypes()
            .Where(type => type.IsPublic && !type.IsAbstract && type.IsSubclassOf(typeof(Window)))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

    [Test]
    public void Every_window_should_have_a_public_parameterless_constructor()
    {
        Windows().Where(type => type.GetConstructor(Type.EmptyTypes) is null).Select(type => type.Name)
            .Should().BeEmpty();
    }

    [AvaloniaTest]
    public void Design_time_constructors_should_create_each_window_from_its_XAML_alone()
    {
        List<string> failed = [];
        foreach (Type type in Windows())
        {
            ConstructorInfo? constructor = type.GetConstructor(Type.EmptyTypes);
            if (constructor?.GetCustomAttribute<ObsoleteAttribute>() is null)
            {
                continue;
            }

            try
            {
                Window window = (Window)constructor.Invoke(null);
                window.Content.Should().NotBeNull(type.Name);
            }
            catch (Exception ex)
            {
                failed.Add($"{type.Name}: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        failed.Should().BeEmpty();
    }
}
