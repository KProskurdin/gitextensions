using System.Text.RegularExpressions;
using GitCommands;

namespace GitExtensions.Xplat.Core.Scripts;

/// <summary>
///  How a prepared script is started.
/// </summary>
public enum ScriptLaunchKind
{
    /// <summary>
    ///  Runs with its output shown, as upstream's FormProcess; the repository is reloaded after it.
    /// </summary>
    Foreground,

    /// <summary>
    ///  Started and left running.
    /// </summary>
    Background,

    /// <summary>
    ///  <see cref="ScriptLaunch.Arguments"/> is a URL to open in the browser (upstream's <c>{openurl}</c>).
    /// </summary>
    OpenUrl,

    /// <summary>
    ///  Runs <see cref="ScriptLaunch.FileName"/> and selects the commit named by the first line of its output (upstream's
    ///  <c>navigateTo:</c>).
    /// </summary>
    NavigateTo,

    /// <summary>
    ///  Runs the loaded plugin named <see cref="ScriptLaunch.FileName"/> (upstream's <c>plugin:name</c> and
    ///  <c>{plugin:name}</c> commands).
    /// </summary>
    Plugin,
}

/// <summary>
///  A script ready to start: the program, its arguments with every variable and user input replaced, and the folder.
/// </summary>
public sealed record ScriptLaunch(ScriptLaunchKind Kind, string FileName, string Arguments, string WorkingDir);

/// <summary>
///  The questions a script may ask before it starts; the new shell shows them as dialogs.
/// </summary>
public interface IScriptPrompts
{
    Task<bool> ConfirmAsync(string message);

    /// <summary>
    ///  Asks for one value (upstream's SimplePrompt); null when cancelled.
    /// </summary>
    Task<string?> AskAsync(string caption, string? label, string defaultValue);

    /// <summary>
    ///  Asks for files (upstream's FormFilePrompt); the quoted paths separated by spaces, or null when cancelled.
    /// </summary>
    Task<string?> PickFilesAsync();
}

/// <summary>
///  A script that cannot start, with upstream's message.
/// </summary>
public sealed class ScriptException(string message) : Exception(message);

/// <summary>
///  Prepares a user script to run: the new shell's version of upstream's <c>ScriptsManager.ScriptRunner</c>. The steps and
///  messages are upstream's: confirmation, user inputs (<c>{UserInput}</c>, <c>{UserInput:label=default}</c>,
///  <c>{UserFiles}</c>), the variables (<see cref="ScriptVariables"/>), and the command names upstream replaces (git,
///  gitextensions, {openurl}, {WorkingDir}). The host starts the returned <see cref="ScriptLaunch"/>.
/// </summary>
public static partial class ScriptRunner
{
    private const string NavigateToPrefix = "navigateTo:";
    private const string PluginPrefix = "plugin:";
    private const string UserInput = "UserInput";
    private const string UserFiles = "UserFiles";
    private const string OpenUrl = "{openurl}";

    // Upstream's: the default value may hold options, so the braces in it must balance.
    [GeneratedRegex(@"\{UserInput:(?<label>[^}=]+)(=(?<defaultValue>[^{}]*(({[^{}]+})+[^{}]*)*))?\}",
        RegexOptions.ExplicitCapture)]
    private static partial Regex UserInputRegex { get; }

    [GeneratedRegex(@"\{plugin.(?<name>.+)\}", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture)]
    private static partial Regex PluginRegex { get; }

    /// <summary>
    ///  The launch for <paramref name="script"/>, or null when the user declined or cancelled it. Throws
    ///  <see cref="ScriptException"/> when it cannot run. <paramref name="appPath"/> is this app, for the "gitextensions" command.
    /// </summary>
    public static async Task<ScriptLaunch?> PrepareAsync(ScriptDefinition script, IScriptContext context,
        IScriptPrompts prompts,
        string appPath)
    {
        if (string.IsNullOrEmpty(script.Command))
        {
            return null;
        }

        string name = script.DisplayName;
        if (script.AskConfirmation && !await prompts.ConfirmAsync($"Do you want to execute script: '{name}'?"))
        {
            return null;
        }

        if (await ReadUserInputsAsync(name, script.Arguments, context, prompts) is not (true, var withInputs))
        {
            return null;
        }

        string? arguments = await ScriptVariables.ExpandAsync(withInputs, context);
        if (arguments is null && !string.IsNullOrWhiteSpace(withInputs))
        {
            throw new ScriptException(
                $"Script: '{name}'{Environment.NewLine}A valid revision is required to substitute the argument options");
        }

        arguments ??= "";
        string command = script.Command.Replace("{WorkingDir}", context.WorkingDir);
        if (command.Equals(OpenUrl, StringComparison.CurrentCultureIgnoreCase))
        {
            return new ScriptLaunch(ScriptLaunchKind.OpenUrl, "", arguments, context.WorkingDir);
        }

        if (script.IsPowerShell)
        {
            // Upstream starts Windows PowerShell; elsewhere PowerShell 7 (pwsh) is the one that exists.
            string shell = OperatingSystem.IsWindows() ? "powershell.exe" : "pwsh";
            string shellArguments =
                (script.RunInBackground ? "" : "-NoExit") + " -ExecutionPolicy Unrestricted -Command \""
                                                          + command + " " + arguments + "\"";
            return new ScriptLaunch(ScriptLaunchKind.Background, shell, shellArguments.Trim(), context.WorkingDir);
        }

        // As upstream's OverrideCommandWhenNecessary: {plugin:name} becomes plugin:name, the name in lower case; the host
        // finds the plugin ignoring case.
        if (PluginRegex.Match(command) is { Success: true } plugin)
        {
            command = PluginPrefix + plugin.Groups["name"].Value.ToLower();
        }

        if (command.StartsWith(PluginPrefix, StringComparison.Ordinal))
        {
            return new ScriptLaunch(ScriptLaunchKind.Plugin, command[PluginPrefix.Length..], arguments, context.WorkingDir);
        }

        if (command.StartsWith(NavigateToPrefix, StringComparison.Ordinal))
        {
            return new ScriptLaunch(ScriptLaunchKind.NavigateTo,
                OverrideCommand(command[NavigateToPrefix.Length..], appPath),
                arguments, context.WorkingDir);
        }

        return new ScriptLaunch(script.RunInBackground ? ScriptLaunchKind.Background : ScriptLaunchKind.Foreground,
            OverrideCommand(command, appPath), arguments, context.WorkingDir);
    }

    // As upstream's ParseUserInputs: each labeled input once (its default may hold options), then a plain {UserInput},
    // then {UserFiles}. False when the user cancelled.
    private static async Task<(bool Ok, string? Arguments)> ReadUserInputsAsync(string scriptName, string? arguments,
        IScriptContext context, IScriptPrompts prompts)
    {
        if (arguments is null)
        {
            return (true, null);
        }

        string caption = $"User input for script '{scriptName}'";
        Match match;
        while ((match = UserInputRegex.Match(arguments)).Success)
        {
            string defaultValue = await ScriptVariables.ExpandAsync(match.Groups["defaultValue"].Value, context) ?? "";
            string label = match.Groups["label"].Value;
            if (await prompts.AskAsync(caption, label, defaultValue) is not { } input)
            {
                return (false, null);
            }

            arguments = ScriptVariables.ReplaceOption($"{UserInput}:{label}", arguments, [input]);
            arguments = ScriptVariables.ReplaceOption(match.Value[1..^1], arguments, [input]);
        }

        if (ScriptVariables.Contains(arguments, UserInput))
        {
            if (await prompts.AskAsync(caption, label: null, defaultValue: "") is not { } input)
            {
                return (false, null);
            }

            arguments = ScriptVariables.ReplaceOption(UserInput, arguments, [input]);
        }

        if (ScriptVariables.Contains(arguments, UserFiles))
        {
            if (await prompts.PickFilesAsync() is not { } files)
            {
                return (false, null);
            }

            arguments = ScriptVariables.ReplaceOption(UserFiles, arguments, [files]);
        }

        return (true, arguments);
    }

    // As upstream's OverrideCommandWhenNecessary: git runs the configured git, gitextensions this app.
    private static string OverrideCommand(string command, string appPath)
    {
        if (command.Equals("git", StringComparison.CurrentCultureIgnoreCase)
            || command.Equals("{git}", StringComparison.CurrentCultureIgnoreCase))
        {
            return AppSettings.GitCommand;
        }

        string[] gitExtensions = ["gitextensions", "{gitextensions}", "gitex", "{gitex}"];
        return gitExtensions.Any(name => command.Equals(name, StringComparison.CurrentCultureIgnoreCase))
            ? appPath
            : command;
    }
}
