// Upstream's CommonAssemblyInfo.cs declares SupportedOSPlatform("windows7.0"), which NUnit and the platform-compatibility
// analyzer take literally. The cross-platform build additionally supports these; the attribute allows multiple values.
[assembly: System.Runtime.Versioning.SupportedOSPlatform("linux")]
[assembly: System.Runtime.Versioning.SupportedOSPlatform("macos")]
