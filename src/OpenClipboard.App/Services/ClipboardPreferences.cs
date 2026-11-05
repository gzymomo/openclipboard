using System;
using System.IO;
using System.Text.Json;
using Clipboard.Data.Enums;

namespace OpenClipboard.App.Services;

public sealed class ClipboardPreferences
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path;

    public string Language { get; set; } = "en-US";

    public string ActivationHotkey { get; set; } = "F2";

    public string QuickCopyHotkey { get; set; } = string.Empty;

    public HistoryMode HistoryMode { get; set; } = HistoryMode._30;

    public int GlassOpacity { get; set; } = 88;

    public ClipboardPreferences()
    {
        var applicationDataDirectory = GetApplicationDataDirectory();
        Directory.CreateDirectory(applicationDataDirectory);
        _path = Path.Combine(applicationDataDirectory, "OpenClipboard", "preferences.json");
        var directory = Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException("Could not determine the preferences directory.");
        Directory.CreateDirectory(directory);

        if (File.Exists(_path))
        {
            var stored = JsonSerializer.Deserialize<ClipboardPreferencesData>(File.ReadAllText(_path));
            if (stored is not null)
            {
                Language = stored.Language;
                ActivationHotkey = stored.ActivationHotkey;
                QuickCopyHotkey = stored.QuickCopyHotkey;
                HistoryMode = Enum.IsDefined(stored.HistoryMode) ? stored.HistoryMode : HistoryMode._30;
                GlassOpacity = Math.Clamp(stored.GlassOpacity, 0, 100);
            }
        }
    }

    public void Save()
    {
        var temporaryPath = _path + ".tmp";
        var data = new ClipboardPreferencesData(Language, ActivationHotkey, QuickCopyHotkey, HistoryMode, GlassOpacity);
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(data, JsonOptions));
        File.Move(temporaryPath, _path, overwrite: true);
    }

    private static string GetApplicationDataDirectory()
    {
        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support");
        }

        if (OperatingSystem.IsLinux())
        {
            var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            return !string.IsNullOrWhiteSpace(xdgDataHome) && Path.IsPathFullyQualified(xdgDataHome)
                ? xdgDataHome
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    }

    private sealed record ClipboardPreferencesData(
        string Language,
        string ActivationHotkey,
        string QuickCopyHotkey,
        HistoryMode HistoryMode,
        int GlassOpacity = 88);
}
