// Upstream's WordDictionary has "using System.Windows.Forms.Design;" for a designer attribute that is compiled only with
// FULL_PROFILE; net10.0 without WinForms has no such namespace, so this type gives it one.
namespace System.Windows.Forms.Design;

internal sealed class FolderNameEditor
{
}
