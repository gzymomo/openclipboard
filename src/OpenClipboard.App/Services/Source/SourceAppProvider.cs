using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace OpenClipboard.App.Services.Source;

public sealed class SourceAppProvider : ISourceAppProvider
{
    public Task<SourceAppInfo?> GetCurrentSourceAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult<SourceAppInfo?>(null);
        }

        try
        {
            return Task.FromResult(GetWindowsSource());
        }
        catch
        {
            return Task.FromResult<SourceAppInfo?>(null);
        }
    }

    private static SourceAppInfo? GetWindowsSource()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }

        _ = GetWindowThreadProcessId(hwnd, out var processId);
        if (processId == 0)
        {
            return null;
        }

        using var process = Process.GetProcessById((int)processId);
        var processName = process.ProcessName;
        if (string.IsNullOrWhiteSpace(processName))
        {
            return null;
        }

        // Avoid attributing clipboard changes to this app itself.
        if (string.Equals(processName, "OpenClipboard.App", StringComparison.OrdinalIgnoreCase)
            || string.Equals(processName, "Clipboard.Desktop", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string? exePath = null;
        try
        {
            exePath = process.MainModule?.FileName;
        }
        catch
        {
            // Access can fail for some protected/system processes.
        }

        var appId = BuildAppId(processName, exePath);
        var appName = TryGetFriendlyName(exePath) ?? processName;
        var iconPath = TryGetOrCreateIconPath(appId, exePath);
        return new SourceAppInfo(appName, appId, iconPath);
    }

    private static string BuildAppId(string processName, string? exePath)
    {
        var key = string.IsNullOrWhiteSpace(exePath)
            ? processName.ToLowerInvariant()
            : exePath.ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hash[..12]).ToLowerInvariant();
    }

    private static string? TryGetFriendlyName(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            return null;
        }

        try
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(exePath);
            if (!string.IsNullOrWhiteSpace(versionInfo.FileDescription))
            {
                return versionInfo.FileDescription;
            }

            if (!string.IsNullOrWhiteSpace(versionInfo.ProductName))
            {
                return versionInfo.ProductName;
            }
        }
        catch
        {
            // Best effort.
        }

        return null;
    }

    private static string? TryGetOrCreateIconPath(string appId, string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            return null;
        }

        try
        {
            var iconsDir = GetIconsDirectory();
            Directory.CreateDirectory(iconsDir);
            var iconPath = Path.Combine(iconsDir, appId + ".png");
            if (File.Exists(iconPath))
            {
                return iconPath;
            }

            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
            if (icon is null)
            {
                return null;
            }

            using var bitmap = icon.ToBitmap();
            bitmap.Save(iconPath, System.Drawing.Imaging.ImageFormat.Png);
            return iconPath;
        }
        catch
        {
            return null;
        }
    }

    private static string GetIconsDirectory()
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

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
}
