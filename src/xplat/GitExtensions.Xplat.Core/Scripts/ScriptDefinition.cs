using System.Text.RegularExpressions;
using System.Xml.Serialization;
using GitUI.ScriptsEngine;

namespace GitExtensions.Xplat.Core.Scripts;

/// <summary>
///  A user script as upstream stores it: the same properties, in the same order and under the same XML names as upstream's
///  <c>ScriptInfo</c> (GitUI/ScriptsEngine), so the scripts in upstream's <c>ownScripts</c> setting are shared by both apps.
///  Upstream's icon is not shown by the new shell, but it is kept so a save does not drop it.
/// </summary>
[XmlType("ScriptInfo")]
public sealed partial class ScriptDefinition
{
    // A single '&' marks the menu mnemonic upstream; "&&" is a literal ampersand.
    [GeneratedRegex("&(?!&)", RegexOptions.ExplicitCapture)]
    private static partial Regex MnemonicAmpersandRegex { get; }

    public bool Enabled { get; set; } = true;

    public string? Name { get; set; }

    public string? Command { get; set; }

    public string? Arguments { get; set; }

    public bool AddToRevisionGridContextMenu { get; set; }

    public ScriptEvent OnEvent { get; set; }

    public bool AskConfirmation { get; set; }

    public bool RunInBackground { get; set; }

    public bool IsPowerShell { get; set; }

    public int HotkeyCommandIdentifier { get; set; }

    public string? Icon { get; set; }

    public string? IconFilePath { get; set; }

    /// <summary>
    ///  The name without upstream's mnemonic ampersand, as upstream's <c>GetDisplayName</c>.
    /// </summary>
    public string DisplayName => MnemonicAmpersandRegex.Replace(Name ?? "", "");

    /// <summary>
    ///  A copy to edit, so cancelling an edit leaves the stored script as it was.
    /// </summary>
    public ScriptDefinition Clone() => (ScriptDefinition)MemberwiseClone();
}
