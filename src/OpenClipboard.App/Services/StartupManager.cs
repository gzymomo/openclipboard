using System;
using System.IO;
using System.Reflection;
using System.Xml;

namespace OpenClipboard.App.Services;

public sealed class StartupManager
{
    private const string ApplicationName = "OpenClipboard";
    private const string WindowsRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool IsEnabled()
    {
        if (OperatingSystem.IsWindows())
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(WindowsRunKey);
            return key?.GetValue(ApplicationName) is string;
        }

        return File.Exists(GetStartupFilePath());
    }

    public void SetEnabled(bool enabled)
    {
        if (OperatingSystem.IsWindows())
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(WindowsRunKey)
                ?? throw new InvalidOperationException("Could not open the current-user startup registry key.");
            if (enabled)
            {
                key.SetValue(ApplicationName, GetLaunchCommand());
            }
            else
            {
                key.DeleteValue(ApplicationName, throwOnMissingValue: false);
            }

            return;
        }

        var path = GetStartupFilePath();
        if (!enabled)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (OperatingSystem.IsMacOS())
        {
            WriteMacLaunchAgent(path);
        }
        else
        {
            File.WriteAllText(path, $"[Desktop Entry]\nType=Application\nName={ApplicationName}\nExec={EscapeDesktopCommand(GetLaunchCommand())}\nX-GNOME-Autostart-enabled=true\n");
        }
    }

    private static string GetStartupFilePath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(home, "Library", "LaunchAgents", "com.openclipboard.app.plist");
        }

        var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        return Path.Combine(string.IsNullOrWhiteSpace(configHome) ? Path.Combine(home, ".config") : configHome, "autostart", "openclipboard.desktop");
    }

    private static string GetLaunchCommand()
    {
        var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("Could not determine the application executable path.");
        var assemblyPath = Assembly.GetEntryAssembly()?.Location;
        if (Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(assemblyPath))
        {
            return $"{Quote(processPath)} {Quote(assemblyPath)} --startup";
        }

        return $"{Quote(processPath)} --startup";
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    private static string EscapeDesktopCommand(string command) =>
        command.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(" ", "\\ ", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

    private static void WriteMacLaunchAgent(string path)
    {
        var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("Could not determine the application executable path.");
        var assemblyPath = Assembly.GetEntryAssembly()?.Location;
        var arguments = Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(assemblyPath)
            ? new[] { processPath, assemblyPath, "--startup" }
            : new[] { processPath, "--startup" };

        using var writer = XmlWriter.Create(path, new XmlWriterSettings { Indent = true });
        writer.WriteStartDocument();
        writer.WriteStartElement("plist");
        writer.WriteAttributeString("version", "1.0");
        writer.WriteStartElement("dict");
        writer.WriteElementString("key", "Label");
        writer.WriteElementString("string", "com.openclipboard.app");
        writer.WriteElementString("key", "ProgramArguments");
        writer.WriteStartElement("array");
        foreach (var argument in arguments)
        {
            writer.WriteElementString("string", argument);
        }

        writer.WriteEndElement();
        writer.WriteElementString("key", "RunAtLoad");
        writer.WriteElementString("true", string.Empty);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }
}
