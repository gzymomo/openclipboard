using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace OpenClipboard.App.Services.Source.Windows;

[SupportedOSPlatform("windows")]
internal sealed class WindowsSourceProvider : ISourceAppProvider
{
    public Task<SourceAppInfo?> GetCurrentSourceAsync()
    {
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

        var appId = SourceIconCache.BuildStableId(string.IsNullOrWhiteSpace(exePath) ? processName : exePath);
        var appName = TryGetFriendlyName(exePath) ?? processName;
        var iconPath = TryGetOrCreateIconPath(appId, exePath);
        return new SourceAppInfo(appName, appId, iconPath);
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
            if (SourceIconCache.TryGetCachedIconPath(appId, out var cached))
            {
                return cached;
            }

            SourceIconCache.EnsureIconsDirectory();
            var iconPath = SourceIconCache.GetIconPath(appId);

            using var icon = Icon.ExtractAssociatedIcon(exePath);
            if (icon is null)
            {
                return null;
            }

            using var bitmap = icon.ToBitmap();
            bitmap.Save(iconPath, ImageFormat.Png);
            return iconPath;
        }
        catch
        {
            return null;
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
}
