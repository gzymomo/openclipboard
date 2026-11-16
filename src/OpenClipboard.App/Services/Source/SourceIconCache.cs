using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace OpenClipboard.App.Services.Source;

internal static class SourceIconCache
{
    public static string GetIconsDirectory()
    {
        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "OpenClipboard", "Icons");
        }

        if (OperatingSystem.IsLinux())
        {
            var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            var baseDir = !string.IsNullOrWhiteSpace(xdgDataHome) && Path.IsPathFullyQualified(xdgDataHome)
                ? xdgDataHome
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            return Path.Combine(baseDir, "OpenClipboard", "Icons");
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenClipboard", "Icons");
    }

    public static string BuildStableId(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key.ToLowerInvariant()));
        return Convert.ToHexString(hash[..12]).ToLowerInvariant();
    }

    public static string GetIconPath(string stableId)
    {
        return Path.Combine(GetIconsDirectory(), stableId + ".png");
    }

    public static bool TryGetCachedIconPath(string stableId, out string iconPath)
    {
        iconPath = GetIconPath(stableId);
        return File.Exists(iconPath);
    }

    public static string EnsureIconsDirectory()
    {
        var dir = GetIconsDirectory();
        Directory.CreateDirectory(dir);
        return dir;
    }
}
