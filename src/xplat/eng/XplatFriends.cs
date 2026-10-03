// The cross-platform app sets ThreadHelper.JoinableTaskContext at startup, as the WinForms app does. That setter is
// internal upstream, so the shadow GitExtUtils grants friend access to the app here instead of editing upstream.

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("GitExtensions")]
