using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClipboard.App.Services.Source.Mac;

internal sealed class MacSourceProvider : ISourceAppProvider
{
    public async Task<SourceAppInfo?> GetCurrentSourceAsync()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return null;
        }

        try
        {
            return await Task.Run(GetMacSource).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private static SourceAppInfo? GetMacSource()
    {
        var front = GetFrontmostProcess();
        if (front is null)
        {
            return null;
        }

        var (pid, appName) = front.Value;
        if (pid <= 0 || string.IsNullOrWhiteSpace(appName))
        {
            return null;
        }

        // Best-effort self filtering.
        if (string.Equals(appName, "OpenClipboard.App", StringComparison.OrdinalIgnoreCase)
            || string.Equals(appName, "OpenClipboard", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var bundleId = GetBundleIdentifier(pid);
        var bundlePath = GetBundlePath(pid);
        if (IsSelfBundle(bundleId, bundlePath))
        {
            return null;
        }

        var stableKey = !string.IsNullOrWhiteSpace(bundleId)
            ? bundleId!
            : !string.IsNullOrWhiteSpace(bundlePath)
                ? bundlePath!
                : $"{appName}:{pid}";

        var appId = !string.IsNullOrWhiteSpace(bundleId)
            ? bundleId!
            : SourceIconCache.BuildStableId(stableKey);

        var iconCacheKey = SourceIconCache.BuildStableId(!string.IsNullOrWhiteSpace(bundleId) ? bundleId! : stableKey);
        var iconPath = TryGetOrCreateIcon(iconCacheKey, bundlePath);

        return new SourceAppInfo(appName, appId, iconPath);
    }

    private static (int pid, string name)? GetFrontmostProcess()
    {
        // Returns: pid|||name
        var script = "tell application \"System Events\" to set p to first application process whose frontmost is true\n" +
                     "set pidValue to unix id of p\n" +
                     "set nameValue to name of p\n" +
                     "return (pidValue as string) & \"|||\" & nameValue";
        var output = RunProcess("/usr/bin/osascript", 1200, "-e", script);
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        var parts = output.Trim().Split("|||", 2, StringSplitOptions.None);
        if (parts.Length != 2 || !int.TryParse(parts[0].Trim(), out var pid))
        {
            return null;
        }

        return (pid, parts[1].Trim());
    }

    private static string? GetBundleIdentifier(int pid)
    {
        // lsappinfo info -only bundleid <pid> => "bundleid=\"com.apple.Safari\"" or similar
        var output = RunProcess("/usr/bin/lsappinfo", 1200, "info", "-only", "bundleid", pid.ToString());
        if (string.IsNullOrWhiteSpace(output))
        {
            // Fallback: ask System Events for bundle identifier if available.
            var script = "tell application \"System Events\" to return bundle identifier of first application process whose unix id is " + pid;
            output = RunProcess("/usr/bin/osascript", 1200, "-e", script);
            return NormalizeScalar(output);
        }

        var s = output.Trim();
        var idx = s.IndexOf('"');
        if (idx >= 0)
        {
            var end = s.LastIndexOf('"');
            if (end > idx)
            {
                return NormalizeScalar(s.Substring(idx + 1, end - idx - 1));
            }
        }

        // Try parsing key=value without quotes
        var eq = s.IndexOf('=');
        if (eq >= 0 && eq + 1 < s.Length)
        {
            return NormalizeScalar(s[(eq + 1)..]);
        }

        return NormalizeScalar(s);
    }

    private static string? GetBundlePath(int pid)
    {
        var output = RunProcess("/usr/bin/lsappinfo", 1200, "info", "-only", "bundlepath", pid.ToString());
        if (!string.IsNullOrWhiteSpace(output))
        {
            var s = output.Trim();
            var idx = s.IndexOf('"');
            if (idx >= 0)
            {
                var end = s.LastIndexOf('"');
                if (end > idx)
                {
                    var path = NormalizeScalar(s.Substring(idx + 1, end - idx - 1));
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        return path;
                    }
                }
            }
        }

        // Fallback via System Events POSIX path of application file.
        var script = "tell application \"System Events\"\n" +
                     "set p to first application process whose unix id is " + pid + "\n" +
                     "try\n" +
                     "return POSIX path of (application file of p as alias)\n" +
                     "on error\n" +
                     "return \"\"\n" +
                     "end try\n" +
                     "end tell";
        var fallback = RunProcess("/usr/bin/osascript", 1500, "-e", script);
        return NormalizeScalar(fallback);
    }

    private static string? TryGetOrCreateIcon(string cacheKey, string? bundlePath)
    {
        try
        {
            if (SourceIconCache.TryGetCachedIconPath(cacheKey, out var cached))
            {
                return cached;
            }

            if (string.IsNullOrWhiteSpace(bundlePath) || !Directory.Exists(bundlePath))
            {
                return null;
            }

            var icns = FindIcnsPath(bundlePath);
            if (string.IsNullOrWhiteSpace(icns) || !File.Exists(icns))
            {
                return null;
            }

            SourceIconCache.EnsureIconsDirectory();
            var outPath = SourceIconCache.GetIconPath(cacheKey);
            if (File.Exists(outPath))
            {
                return outPath;
            }

            // Convert icns to png. --resampleHeightWidthMax keeps it reasonably small.
            var result = RunProcess("/usr/bin/sips", 4000, "-s", "format", "png", icns, "--out", outPath, "--resampleHeightWidthMax", "128");
            if (File.Exists(outPath))
            {
                return outPath;
            }

            // Some icns may fail resample option ordering/variants; try simpler conversion.
            _ = result;
            RunProcess("/usr/bin/sips", 4000, "-s", "format", "png", icns, "--out", outPath);
            return File.Exists(outPath) ? outPath : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindIcnsPath(string bundlePath)
    {
        try
        {
            var infoPlist = Path.Combine(bundlePath, "Contents", "Info.plist");
            if (File.Exists(infoPlist))
            {
                var iconFile = RunProcess("/usr/bin/defaults", 1200, "read", infoPlist, "CFBundleIconFile");
                iconFile = NormalizeScalar(iconFile);
                if (!string.IsNullOrWhiteSpace(iconFile))
                {
                    if (!iconFile.EndsWith(".icns", StringComparison.OrdinalIgnoreCase))
                    {
                        iconFile += ".icns";
                    }

                    var candidate = Path.Combine(bundlePath, "Contents", "Resources", iconFile);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
        }
        catch
        {
            // Fall through to scan.
        }

        try
        {
            var resources = Path.Combine(bundlePath, "Contents", "Resources");
            if (!Directory.Exists(resources))
            {
                return null;
            }

            // Prefer common app icon names, else first icns.
            string? first = null;
            foreach (var file in Directory.EnumerateFiles(resources, "*.icns", SearchOption.TopDirectoryOnly))
            {
                first ??= file;
                var name = Path.GetFileName(file);
                if (name.Contains("AppIcon", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("app", StringComparison.OrdinalIgnoreCase))
                {
                    return file;
                }
            }

            return first;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsSelfBundle(string? bundleId, string? bundlePath)
    {
        if (!string.IsNullOrWhiteSpace(bundleId)
            && bundleId.Contains("openclipboard", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(bundlePath)
            && bundlePath.Contains("OpenClipboard", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static string? NormalizeScalar(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

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

            foreach (var arg in args)
            {
                psi.ArgumentList.Add(arg);
            }

            using var process = Process.Start(psi);
            if (process is null)
            {
                return null;
            }

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
