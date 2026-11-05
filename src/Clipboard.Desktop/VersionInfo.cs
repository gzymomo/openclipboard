using System.Reflection;

namespace Clipboard.Desktop;

public static class VersionInfo
{
    public static string DisplayVersion { get; } =
        $"v{typeof(VersionInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0"}";
}
