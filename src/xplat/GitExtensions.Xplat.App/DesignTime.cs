namespace GitExtensions.Xplat.App;

/// <summary>
///  The parameterless constructors of the windows whose real constructor takes the window's data (a repository, a
///  dialog, a callback). Avalonia's XAML previewer and its runtime loader create a window without arguments; without such
///  a constructor the window cannot be previewed and the XAML compiler warns (AVLN3001). Such a window only shows its
///  XAML: none of the code that fills it runs, so the app never opens one.
/// </summary>
internal static class DesignTime
{
    /// <summary>
    ///  The <see cref="ObsoleteAttribute"/> message of those constructors, so code that calls one by mistake is warned.
    /// </summary>
    public const string ConstructorOnly = "For the XAML previewer and Avalonia's runtime loader only (see DesignTime).";
}
