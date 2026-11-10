using System;
using System.Globalization;
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

    public int GlassOpacity { get; set; } = 50;

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
        else
        {
            Language = GetDefaultLanguage();
        }
    }

    public void Save()
    {
        var temporaryPath = _path + ".tmp";
        var data = new ClipboardPreferencesData(Language, ActivationHotkey, QuickCopyHotkey, HistoryMode, GlassOpacity);
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(data, JsonOptions));
        File.Move(temporaryPath, _path, overwrite: true);
    }

    private static string GetDefaultLanguage()
    {
        var cultureName = CultureInfo.CurrentUICulture.Name;
        string[] supported = ["zh-CN", "en-US", "de-DE", "fr-FR", "ja-JP", "ko-KR", "ru-RU", "es-ES", "pt-PT", "th-TH", "vi-VN"];

        foreach (var code in supported)
        {
            if (string.Equals(code, cultureName, StringComparison.OrdinalIgnoreCase))
            {
                return code;
            }
        }

        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return language.ToLowerInvariant() switch
        {
            "zh" => "zh-CN",
            "en" => "en-US",
            "de" => "de-DE",
            "fr" => "fr-FR",
            "ja" => "ja-JP",
            "ko" => "ko-KR",
            "ru" => "ru-RU",
            "es" => "es-ES",
            "pt" => "pt-PT",
            "th" => "th-TH",
            "vi" => "vi-VN",
            _ => "en-US"
        };
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
        int GlassOpacity = 50);
}
