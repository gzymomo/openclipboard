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

    public int ScrollWheelSpeed { get; set; } = 40;

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
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(_path));
                var root = document.RootElement;
                if (root.TryGetProperty("Language", out var language)) Language = language.GetString() ?? Language;
                if (root.TryGetProperty("ActivationHotkey", out var activationHotkey)) ActivationHotkey = activationHotkey.GetString() ?? ActivationHotkey;
                if (root.TryGetProperty("QuickCopyHotkey", out var quickCopyHotkey)) QuickCopyHotkey = quickCopyHotkey.GetString() ?? QuickCopyHotkey;
                if (root.TryGetProperty("HistoryMode", out var historyMode)) HistoryMode = ParseHistoryMode(historyMode);
                if (root.TryGetProperty("GlassOpacity", out var glassOpacity) && glassOpacity.TryGetInt32(out var opacity)) GlassOpacity = Math.Clamp(opacity, 0, 100);
                if (root.TryGetProperty("ScrollWheelSpeed", out var scrollSpeed) && scrollSpeed.TryGetInt32(out var speed)) ScrollWheelSpeed = NormalizeScrollWheelSpeed(speed);
            }
            catch
            {
                // Ignore malformed preferences and keep defaults.
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
        var data = new ClipboardPreferencesData(Language, ActivationHotkey, QuickCopyHotkey, HistoryMode, GlassOpacity, ScrollWheelSpeed);
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

    private static int NormalizeScrollWheelSpeed(int value) => value switch
    {
        <= 70 => 40,
        <= 125 => 100,
        _ => 150
    };

    private static HistoryMode ParseHistoryMode(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString();
            if (Enum.TryParse<HistoryMode>(value, ignoreCase: true, out var mode))
            {
                return mode;
            }

            // Legacy string names
            return value switch
            {
                "None" => HistoryMode._1,
                "_7" or "7" => HistoryMode._7,
                "_30" or "30" => HistoryMode._30,
                "Forever" => HistoryMode.Forever,
                _ => HistoryMode._30
            };
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var intValue))
        {
            // Legacy numeric mapping: 0=None, 1=7 days, 2=30 days, 3=Forever
            return intValue switch
            {
                0 => HistoryMode._1,
                1 => HistoryMode._7,
                2 => HistoryMode._30,
                3 => HistoryMode.Forever,
                4 => HistoryMode._15,
                5 => HistoryMode._30,
                6 => HistoryMode.Forever,
                _ => HistoryMode._30
            };
        }

        return HistoryMode._30;
    }

    private sealed record ClipboardPreferencesData(
        string Language,
        string ActivationHotkey,
        string QuickCopyHotkey,
        HistoryMode HistoryMode,
        int GlassOpacity = 50,
        int ScrollWheelSpeed = 40);
}
