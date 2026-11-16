using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OpenClipboard.App.Services.Source.Linux;

internal sealed class LinuxSourceProvider : ISourceAppProvider
{
    public async Task<SourceAppInfo?> GetCurrentSourceAsync()
    {
        if (!OperatingSystem.IsLinux())
        {
            return null;
        }

        try
        {
            return await Task.Run(GetLinuxSource).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private static SourceAppInfo? GetLinuxSource()
    {
        var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        var hasWayland = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
        var hasX11 = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY"));

        // Wayland-first sessions without usable X11: degrade gracefully (generic Wayland limitation).
        if (string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase) && !hasX11)
        {
            return null;
        }

        // X11 path (including XWayland when DISPLAY is available).
        if (hasX11)
        {
            return GetX11Source();
        }

        // Unknown/Wayland-only fallback.
        _ = hasWayland;
        return null;
    }

    private static SourceAppInfo? GetX11Source()
    {
        var activeWindowId = GetActiveWindowId();
        if (string.IsNullOrWhiteSpace(activeWindowId))
        {
            return null;
        }

        var wmClass = GetWindowProp(activeWindowId, "WM_CLASS");
        var wmName = GetWindowProp(activeWindowId, "_NET_WM_NAME") ?? GetWindowProp(activeWindowId, "WM_NAME");
        var pidText = GetWindowProp(activeWindowId, "_NET_WM_PID");
        _ = int.TryParse(ExtractFirstInt(pidText), out var pid);

        string? exePath = null;
        string? processName = null;
        if (pid > 0)
        {
            exePath = TryReadLink($"/proc/{pid}/exe");
            processName = TryGetProcessName(pid) ?? TryGetProcessNameFromCmdline(pid);
        }

        var wmClassValues = ParseWmClass(wmClass);
        var wmInstance = wmClassValues.instance;
        var wmClassName = wmClassValues.className;

        var appName = FirstNonEmpty(
            wmName,
            wmClassName,
            wmInstance,
            processName,
            TryFileNameWithoutExtension(exePath));

        if (string.IsNullOrWhiteSpace(appName))
        {
            return null;
        }

        if (IsSelf(appName, processName, exePath))
        {
            return null;
        }

        var desktop = FindDesktopEntry(wmClassName, wmInstance, processName, exePath);
        if (!string.IsNullOrWhiteSpace(desktop?.Name))
        {
            appName = desktop!.Name;
        }

        var stableKey = FirstNonEmpty(
            desktop?.DesktopId,
            wmClassName,
            wmInstance,
            exePath,
            processName,
            appName) ?? appName!;

        var appId = !string.IsNullOrWhiteSpace(desktop?.DesktopId)
            ? desktop!.DesktopId!
            : SourceIconCache.BuildStableId(stableKey);
        var iconCacheKey = SourceIconCache.BuildStableId(stableKey);

        string? iconPath = null;
        if (desktop is not null && !string.IsNullOrWhiteSpace(desktop.Icon))
        {
            iconPath = TryResolveIconFromDesktop(iconCacheKey, desktop.Icon!);
        }
        if (iconPath is null)
        {
            iconPath = TryResolveIconByHints(iconCacheKey, wmClassName, wmInstance, processName, TryFileNameWithoutExtension(exePath));
        }

        return new SourceAppInfo(appName, appId, iconPath);
    }

    private static string? GetActiveWindowId()
    {
        // xprop -root _NET_ACTIVE_WINDOW => "_NET_ACTIVE_WINDOW(WINDOW): window id # 0x3a00007"
        var output = RunProcess("xprop", 1200, "-root", "_NET_ACTIVE_WINDOW");
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        var m = Regex.Match(output, @"0x[0-9a-fA-F]+");
        if (m.Success)
        {
            var id = m.Value;
            if (!string.Equals(id, "0x0", StringComparison.OrdinalIgnoreCase))
            {
                return id;
            }
        }

        return null;
    }

    private static string? GetWindowProp(string windowId, string prop)
    {
        var output = RunProcess("xprop", 1200, "-id", windowId, prop);
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        // Formats vary: WM_CLASS(STRING) = "code", "Code"  /  _NET_WM_NAME(UTF8_STRING) = "Title" / _NET_WM_PID(CARDINAL) = 1234
        var eq = output.IndexOf('=');
        if (eq < 0)
        {
            return null;
        }

        return output[(eq + 1)..].Trim();
    }

    private static (string? instance, string? className) ParseWmClass(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (null, null);
        }

        // raw like: "code", "Code"  or  0x..., 0x...
        var matches = Regex.Matches(raw, "\"([^\"]*)\"");
        if (matches.Count >= 2)
        {
            return (NormalizeScalar(matches[0].Groups[1].Value), NormalizeScalar(matches[1].Groups[1].Value));
        }
        if (matches.Count == 1)
        {
            var v = NormalizeScalar(matches[0].Groups[1].Value);
            return (v, v);
        }

        // Fallback split
        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 2)
        {
            return (NormalizeScalar(parts[0].Trim().Trim('"')), NormalizeScalar(parts[1].Trim().Trim('"')));
        }
        var single = NormalizeScalar(raw.Trim().Trim('"'));
        return (single, single);
    }

    private static string? ExtractFirstInt(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var m = Regex.Match(text, @"\d+");
        return m.Success ? m.Value : null;
    }

    private static string? TryReadLink(string path)
    {
        try
        {
            var fi = new FileInfo(path);
            var target = fi.ResolveLinkTarget(true);
            return target?.FullName ?? (File.Exists(path) ? path : null);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetProcessName(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return NormalizeScalar(p.ProcessName);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetProcessNameFromCmdline(int pid)
    {
        try
        {
            var cmdlinePath = $"/proc/{pid}/cmdline";
            if (!File.Exists(cmdlinePath)) return null;
            var bytes = File.ReadAllBytes(cmdlinePath);
            var text = Encoding.UTF8.GetString(bytes).Split('\0', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(text)) return null;
            return NormalizeScalar(Path.GetFileName(text));
        }
        catch
        {
            return null;
        }
    }

    private sealed record DesktopEntryInfo(string? DesktopId, string? Name, string? Icon, string? StartupWmClass, string? Exec, string? PathHint);

    private static DesktopEntryInfo? FindDesktopEntry(string? wmClassName, string? wmInstance, string? processName, string? exePath)
    {
        try
        {
            var hints = new[] { wmClassName, wmInstance, processName, TryFileNameWithoutExtension(exePath) }
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (hints.Length == 0)
            {
                return null;
            }

            foreach (var dir in GetDesktopFileDirectories())
            {
                if (!Directory.Exists(dir)) continue;
                IEnumerable<string> files;
                try { files = Directory.EnumerateFiles(dir, "*.desktop", SearchOption.TopDirectoryOnly); }
                catch { continue; }

                foreach (var file in files)
                {
                    var entry = ParseDesktopEntry(file);
                    if (entry is null) continue;
                    if (DesktopEntryMatches(entry, hints))
                    {
                        return entry;
                    }
                }
            }
        }
        catch
        {
            // best effort
        }

        return null;
    }

    private static IEnumerable<string> GetDesktopFileDirectories()
    {
        var list = new List<string>();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        var dataHome = !string.IsNullOrWhiteSpace(xdgDataHome) && Path.IsPathFullyQualified(xdgDataHome)
            ? xdgDataHome
            : Path.Combine(home, ".local", "share");
        list.Add(Path.Combine(dataHome, "applications"));

        var xdgDataDirs = Environment.GetEnvironmentVariable("XDG_DATA_DIRS");
        if (!string.IsNullOrWhiteSpace(xdgDataDirs))
        {
            foreach (var part in xdgDataDirs.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                list.Add(Path.Combine(part, "applications"));
            }
        }
        else
        {
            list.Add("/usr/local/share/applications");
            list.Add("/usr/share/applications");
        }

        list.Add(Path.Combine(home, ".local", "share", "flatpak", "exports", "share", "applications"));
        list.Add("/var/lib/flatpak/exports/share/applications");
        list.Add("/var/lib/snapd/desktop/applications");

        return list.Distinct(StringComparer.Ordinal);
    }

    private static DesktopEntryInfo? ParseDesktopEntry(string path)
    {
        try
        {
            string? name = null, icon = null, wm = null, exec = null, type = null;
            var inDesktopEntry = false;
            foreach (var rawLine in File.ReadLines(path))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    inDesktopEntry = string.Equals(line, "[Desktop Entry]", StringComparison.OrdinalIgnoreCase);
                    continue;
                }
                if (!inDesktopEntry) continue;

                var eq = line.IndexOf('=');
                if (eq <= 0) continue;
                var key = line[..eq].Trim();
                var value = line[(eq + 1)..].Trim();
                if (key.Equals("Name", StringComparison.OrdinalIgnoreCase)) name ??= value;
                else if (key.Equals("Icon", StringComparison.OrdinalIgnoreCase)) icon ??= value;
                else if (key.Equals("StartupWMClass", StringComparison.OrdinalIgnoreCase)) wm ??= value;
                else if (key.Equals("Exec", StringComparison.OrdinalIgnoreCase)) exec ??= value;
                else if (key.Equals("Type", StringComparison.OrdinalIgnoreCase)) type ??= value;
            }

            if (!string.IsNullOrWhiteSpace(type) && !string.Equals(type, "Application", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var id = Path.GetFileNameWithoutExtension(path);
            return new DesktopEntryInfo(id, NormalizeScalar(name), NormalizeScalar(icon), NormalizeScalar(wm), NormalizeScalar(exec), path);
        }
        catch
        {
            return null;
        }
    }

    private static bool DesktopEntryMatches(DesktopEntryInfo entry, string[] hints)
    {
        var candidates = new List<string?>();
        candidates.Add(entry.DesktopId);
        candidates.Add(entry.Name);
        candidates.Add(entry.StartupWmClass);
        if (!string.IsNullOrWhiteSpace(entry.Exec))
        {
            try
            {
                var exec0 = entry.Exec.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
                candidates.Add(TryFileNameWithoutExtension(exec0));
            }
            catch { }
        }

        foreach (var c in candidates.Where(c => !string.IsNullOrWhiteSpace(c)))
        {
            foreach (var h in hints)
            {
                if (string.Equals(c, h, StringComparison.OrdinalIgnoreCase)) return true;
                if (c!.Contains(h, StringComparison.OrdinalIgnoreCase) || h.Contains(c, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }

        return false;
    }

    private static string? TryResolveIconFromDesktop(string cacheKey, string iconValue)
    {
        try
        {
            if (SourceIconCache.TryGetCachedIconPath(cacheKey, out var cached))
            {
                return cached;
            }

            string? source = null;
            if (Path.IsPathRooted(iconValue) && File.Exists(iconValue))
            {
                source = iconValue;
            }
            else
            {
                source = FindIconByName(iconValue);
            }

            if (string.IsNullOrWhiteSpace(source))
            {
                return null;
            }

            // Prefer PNG directly; skip SVG conversion in first pass.
            if (source.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                SourceIconCache.EnsureIconsDirectory();
                var dest = SourceIconCache.GetIconPath(cacheKey);
                if (!File.Exists(dest))
                {
                    File.Copy(source, dest, overwrite: false);
                }
                return dest;
            }

            // For non-png (e.g. xpm/svg), try to use it only if Avalonia can load; safer to skip here.
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryResolveIconByHints(string cacheKey, params string?[] hints)
    {
        foreach (var h in hints.Where(h => !string.IsNullOrWhiteSpace(h)).Select(h => h!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var resolved = TryResolveIconFromDesktop(cacheKey, h);
            if (!string.IsNullOrWhiteSpace(resolved)) return resolved;
            var found = FindIconByName(h);
            if (!string.IsNullOrWhiteSpace(found) && found.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (SourceIconCache.TryGetCachedIconPath(cacheKey, out var cached)) return cached;
                    SourceIconCache.EnsureIconsDirectory();
                    var dest = SourceIconCache.GetIconPath(cacheKey);
                    if (!File.Exists(dest)) File.Copy(found, dest, overwrite: false);
                    return dest;
                }
                catch { }
            }
        }
        return null;
    }

    private static string? FindIconByName(string iconName)
    {
        try
        {
            var baseNames = new[] { iconName, iconName.ToLowerInvariant() }.Distinct(StringComparer.Ordinal);
            foreach (var dir in GetIconSearchDirectories())
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var name in baseNames)
                {
                    var png = Path.Combine(dir, name + ".png");
                    if (File.Exists(png)) return png;
                }

                // Common hicolor layout scans (limited depth/targets for performance)
                var hicolor = Path.Combine(dir, "hicolor");
                if (Directory.Exists(hicolor))
                {
                    var preferredSizes = new[] { "128x128", "64x64", "48x48", "256x256", "scalable" };
                    foreach (var size in preferredSizes)
                    {
                        foreach (var sub in new[] { "apps", "categories", "places" })
                        {
                            foreach (var name in baseNames)
                            {
                                var candidate = Path.Combine(hicolor, size, sub, name + ".png");
                                if (File.Exists(candidate)) return candidate;
                            }
                        }
                    }

                    // Last resort: shallow scan for matching png filename
                    try
                    {
                        foreach (var file in Directory.EnumerateFiles(hicolor, iconName + ".png", SearchOption.AllDirectories))
                        {
                            if (file.Contains(Path.Combine("apps", iconName + ".png"), StringComparison.OrdinalIgnoreCase)
                                || file.EndsWith(Path.DirectorySeparatorChar + iconName + ".png", StringComparison.OrdinalIgnoreCase))
                            {
                                return file;
                            }
                        }
                    }
                    catch { }
                }
            }
        }
        catch
        {
            // best effort
        }

        return null;
    }

    private static IEnumerable<string> GetIconSearchDirectories()
    {
        var dirs = new List<string>();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        var dataHome = !string.IsNullOrWhiteSpace(xdgDataHome) && Path.IsPathFullyQualified(xdgDataHome)
            ? xdgDataHome
            : Path.Combine(home, ".local", "share");

        dirs.Add(Path.Combine(dataHome, "icons"));
        dirs.Add(Path.Combine(home, ".icons"));

        var xdgDataDirs = Environment.GetEnvironmentVariable("XDG_DATA_DIRS");
        if (!string.IsNullOrWhiteSpace(xdgDataDirs))
        {
            foreach (var part in xdgDataDirs.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                dirs.Add(Path.Combine(part, "icons"));
                dirs.Add(Path.Combine(part, "pixmaps"));
            }
        }
        else
        {
            dirs.Add("/usr/local/share/icons");
            dirs.Add("/usr/share/icons");
            dirs.Add("/usr/local/share/pixmaps");
            dirs.Add("/usr/share/pixmaps");
        }

        dirs.Add("/var/lib/flatpak/exports/share/icons");
        dirs.Add(Path.Combine(home, ".local", "share", "flatpak", "exports", "share", "icons"));

        return dirs.Distinct(StringComparer.Ordinal);
    }

    private static bool IsSelf(string? appName, string? processName, string? exePath)
    {
        bool Match(string? s) => !string.IsNullOrWhiteSpace(s)
            && (s.Contains("OpenClipboard", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "Clipboard.Desktop", StringComparison.OrdinalIgnoreCase));

        return Match(appName) || Match(processName) || Match(exePath);
    }

    private static string? TryFileNameWithoutExtension(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            var name = Path.GetFileNameWithoutExtension(path);
            return NormalizeScalar(name);
        }
        catch { return null; }
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static string? NormalizeScalar(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim().Trim('"');
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    private static string? RunProcess(string fileName, int timeoutMs, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            foreach (var arg in args) psi.ArgumentList.Add(arg);
            using var process = Process.Start(psi);
            if (process is null) return null;

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return null;
            }

            var stdout = stdoutTask.GetAwaiter().GetResult();
            _ = stderrTask.GetAwaiter().GetResult();
            return stdout;
        }
        catch
        {
            return null;
        }
    }
}
