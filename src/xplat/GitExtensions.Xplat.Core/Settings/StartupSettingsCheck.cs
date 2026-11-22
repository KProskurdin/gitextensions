namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  Upstream's startup check (<c>Program.Main</c>): while "Check settings at startup" is on, the Checklist runs, upstream's
///  <c>AutoSolveAllSettings</c> is tried when something is wrong, and the settings open when it is still wrong. A check
///  that finds every setting right turns the startup check off. With the check off, only the editor is solved.
/// </summary>
/// <remarks>
///  Of <c>AutoSolveAllSettings</c>, the git command is the shell's git discovery at start, which runs before this (on
///  Windows upstream's <c>SolveGitCommand</c>); the linux tools and the install folder are solved on Windows
///  (<see cref="SolveLinuxToolsDir"/>, <see cref="SolveInstallDir"/>). Upstream solves the editor there only on Windows;
///  here on every system, since the app is git's editor everywhere.
/// </remarks>
public sealed class StartupSettingsCheck(
    IAppPreferences preferences,
    Func<Task<ChecklistFacts>> readFacts,
    Func<Task<string?>> readEditor,
    string? editorCommand,
    Action<string, string?> setEnvironment)
{
    /// <summary>
    ///  Upstream's <c>CommonLogic.AmbientGitEditorEnvVariableName</c>, which git reads last for its editor.
    /// </summary>
    public const string EditorVariable = "EDITOR";

    /// <summary>
    ///  Upstream's <c>SolveGitExtensionsDir</c>, which stores the app's folder as the install folder; set on Windows only.
    /// </summary>
    public Func<bool>? SolveInstallDir { get; init; }

    /// <summary>
    ///  Upstream's <c>SolveLinuxToolsDir</c>, which stores the folder with <c>sh</c>; set on Windows only.
    /// </summary>
    public Func<bool>? SolveLinuxToolsDir { get; init; }

    /// <summary>
    ///  Runs the check; true when the settings should open.
    /// </summary>
    public async Task<bool> RunAsync()
    {
        if (!preferences.CheckSettings)
        {
            await SolveEditorAsync();
            return false;
        }

        if (await CheckAsync())
        {
            return false;
        }

        SolveLinuxToolsDir?.Invoke();
        SolveInstallDir?.Invoke();
        await SolveEditorAsync();
        return !await CheckAsync();
    }

    // Upstream's ChecklistSettingsPage.CheckSettings, with IsCheckAtStartupChecked.
    private async Task<bool> CheckAsync()
    {
        ChecklistFacts facts = await readFacts();
        bool isValid = SettingsChecklist.IsValid(facts, SettingsChecklist.Check(facts));
        preferences.CheckSettings = SettingsChecklist.CheckAtStartupAfter(isValid, preferences.CheckSettings);
        return isValid;
    }

    // Upstream's CheckSettingsLogic.SolveEditor: without an editor, the git commands this process starts get the app's.
    private async Task SolveEditorAsync()
    {
        if (editorCommand is not null && string.IsNullOrEmpty(await readEditor()))
        {
            setEnvironment(EditorVariable, editorCommand);
        }
    }
}
