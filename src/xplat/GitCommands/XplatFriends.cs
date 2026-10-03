// The cross-platform core creates GitModule instances through the internal GitExecutorProvider. Granting friend access
// here keeps upstream's AssemblyInfo untouched.

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("GitExtensions.Xplat.Core")]
