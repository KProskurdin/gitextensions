using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Scripts;
using GitUI.ScriptsEngine;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Runs user scripts for a window: the new shell's host for upstream's <c>ScriptsManager</c>. It asks the script's questions
///  as dialogs, prepares the script (<see cref="ScriptRunner"/>) and starts it: with its output in the process window,
///  detached, as a URL in the browser, as a commit to select, or as a plugin to run.
/// </summary>
internal sealed class ScriptHost(
    Window owner,
    RepositoryOperationsViewModel actions,
    Func<string, Task>? navigateTo = null,
    Func<string, bool>? runPlugin = null)
    : IScriptPrompts
{
    /// <summary>
    ///  The enabled scripts for <paramref name="scriptEvent"/>.
    /// </summary>
    public static IReadOnlyList<ScriptDefinition> ScriptsFor(ScriptEvent scriptEvent)
        => [.. AppServices.Scripts.Load().Where(script => script.Enabled && script.OnEvent == scriptEvent)];

    /// <summary>
    ///  The script whose hotkey is <paramref name="e"/>, or null. As upstream's <c>GitModuleForm</c>, any script with a hotkey
    ///  runs from it, enabled or not. A key without Ctrl (Cmd) or Alt typed into a text box is text, unless it is a function
    ///  key.
    /// </summary>
    public static ScriptDefinition? MatchHotkey(KeyEventArgs e)
    {
        bool typesText = (e.KeyModifiers & (Hotkeys.CommandModifier | KeyModifiers.Alt)) == 0 &&
                         e.Key is not (>= Key.F1 and <= Key.F24);
        if ((e.Source is TextBox && typesText) || Hotkeys.Scripts.Match(e) is not { } id)
        {
            return null;
        }

        return AppServices.Scripts.Load().FirstOrDefault(script => script.HotkeyCommandIdentifier == id);
    }

    /// <summary>
    ///  Runs every enabled script of <paramref name="scriptEvent"/> in turn, as upstream's <c>RunEventScripts</c>: false as
    ///  soon as one does not run, so a "before" event can stop the operation.
    /// </summary>
    public async Task<bool> RunEventAsync(ScriptEvent scriptEvent, string repositoryPath,
        IReadOnlyList<string> selectedHashes)
    {
        foreach (ScriptDefinition script in ScriptsFor(scriptEvent))
        {
            if (!await RunAsync(script, repositoryPath, selectedHashes))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///  Runs one script; false when it did not run (declined, cancelled, failed), as upstream's <c>RunScript</c>.
    ///  <paramref name="extraOptions"/> are the options of the place it runs from, such as a file list's
    ///  <see cref="ScriptFileOptions"/>.
    /// </summary>
    public async Task<bool> RunAsync(ScriptDefinition script, string repositoryPath,
        IReadOnlyList<string> selectedHashes, IReadOnlyDictionary<string, IReadOnlyList<string>>? extraOptions = null)
    {
        ScriptLaunch? launch;
        try
        {
            RepositoryScriptContext context = await Task.Run(() =>
                new RepositoryScriptContext(repositoryPath, selectedHashes, ChooseAsync, extraOptions));
            launch = await ScriptRunner.PrepareAsync(script, context, this, Environment.ProcessPath ?? "GitExtensions");
        }
        catch (ScriptException ex)
        {
            await new ErrorWindow(ex.Message).ShowDialog(owner);
            return false;
        }

        if (launch is null)
        {
            return false;
        }

        try
        {
            return await StartAsync(script, launch);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException
                                       or UriFormatException)
        {
            await new ErrorWindow($"Failed to execute script: '{script.DisplayName}'{Environment.NewLine}{ex.Message}")
                .ShowDialog(owner);
            return false;
        }
    }

    public Task<bool> ConfirmAsync(string message) => new ConfirmWindow(message, "Run").ShowDialog<bool>(owner);

    public async Task<string?> AskAsync(string caption, string? label, string defaultValue)
        => (await new PromptWindow(caption, label ?? "Enter a value", "OK", initialValue: defaultValue,
                allowEmpty: true)
            .ShowDialog<PromptResult?>(owner))?.Value;

    // As upstream's file prompt: the chosen files, quoted, separated by spaces.
    public async Task<string?> PickFilesAsync()
    {
        IReadOnlyList<IStorageFile> files = await owner.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions { AllowMultiple = true, Title = "Select files" });
        return files.Count == 0
            ? null
            : string.Join(" ", files.Select(file => file.TryGetLocalPath() ?? file.Name).Select(path => path.Quote()));
    }

    private async Task<string> ChooseAsync(IReadOnlyList<string> options)
        => await new ChooseWindow(options).ShowDialog<string?>(owner) ?? "";

    private async Task<bool> StartAsync(ScriptDefinition script, ScriptLaunch launch)
    {
        switch (launch.Kind)
        {
            case ScriptLaunchKind.Foreground:
                return await actions.RunProgramAsync($"Script {script.DisplayName}", launch.FileName, launch.Arguments,
                    launch.WorkingDir);
            case ScriptLaunchKind.OpenUrl:
                return await TopLevel.GetTopLevel(owner)!.Launcher.LaunchUriAsync(new Uri(launch.Arguments));
            case ScriptLaunchKind.Plugin:
                // As upstream: false (the script did not run) when no loaded plugin has that name.
                return runPlugin?.Invoke(launch.FileName) ?? false;
            case ScriptLaunchKind.NavigateTo:
                string? target = await Task.Run(() => FirstOutputLine(launch));
                if (target is not null && navigateTo is not null)
                {
                    await navigateTo(target);
                }

                return true;
            default:
                EnvironmentConfiguration.SetEnvironmentVariables();
                Process.Start(new ProcessStartInfo(launch.FileName, launch.Arguments)
                {
                    UseShellExecute = false, WorkingDirectory = launch.WorkingDir,
                })?.Dispose();
                return true;
        }
    }

    private static string? FirstOutputLine(ScriptLaunch launch)
    {
        ExecutionResult result =
            new Executable(launch.FileName, launch.WorkingDir).Execute(launch.Arguments, throwOnErrorExit: false);
        return result.StandardOutput.Split('\n').Select(line => line.Trim()).FirstOrDefault(line => line.Length > 0);
    }
}
