using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Clipboard.Data.Enums;

namespace OpenClipboard.App.Models;

public sealed record ClipboardHistoryItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public ClipboardDataTypes DataType { get; init; } = ClipboardDataTypes.Text;

    public string Data { get; init; } = string.Empty;

    public DateTime CreateTime { get; init; } = DateTime.Now;

    public bool IsFavorite { get; init; }

    public string? SourceAppName { get; init; }

    public string? SourceAppId { get; init; }

    public string? SourceIconPath { get; init; }

    public string TypeLabel { get; init; } = string.Empty;

    public string ItemInfo { get; init; } = string.Empty;

    public string DisplayPreview { get; init; } = string.Empty;

    public Bitmap? ImageBitmap { get; init; }

    public Bitmap? SourceIconBitmap { get; init; }

    public bool HasSourceIcon => SourceIconBitmap is not null;

    public bool HasImagePreview => ImageBitmap is not null;

    public bool IsText => DataType == ClipboardDataTypes.Text;

    public bool IsImage => DataType == ClipboardDataTypes.Image;

    public bool IsFile => DataType == ClipboardDataTypes.File;

    public bool IsColor => DataType == ClipboardDataTypes.Color;

    public bool IsUrl => DataType == ClipboardDataTypes.Url;

    public bool IsJson => DataType == ClipboardDataTypes.Json;

    public bool IsXml => DataType == ClipboardDataTypes.Xml;

    public bool IsMarkdown => DataType == ClipboardDataTypes.Markdown;

    public bool IsTextPreview => DataType != ClipboardDataTypes.Image
        && DataType != ClipboardDataTypes.File
        && DataType != ClipboardDataTypes.Color
        && DataType != ClipboardDataTypes.Markdown;

    public bool HasColorPreview => IsColor && TryParseColor(Data, out _);

    public string MarkdownContent => Data;

    public IBrush? ColorPreviewBrush => TryParseColor(Data, out var color) ? new SolidColorBrush(color) : null;

    public string FileDisplayName
    {
        get
        {
            var paths = GetFilePaths();
            if (paths.Length == 0)
            {
                return string.Empty;
            }

            var names = paths.Select(Path.GetFileName).Where(name => !string.IsNullOrWhiteSpace(name)).ToArray();
            return names.Length == 0 ? string.Empty : string.Join(Environment.NewLine, names);
        }
    }

    public string FileDisplayPath
    {
        get
        {
            var paths = GetFilePaths();
            return paths.Length == 0 ? string.Empty : string.Join(Environment.NewLine, paths);
        }
    }

    public IBrush TypeBrush => DataType switch
    {
        ClipboardDataTypes.Text => Brushes.DarkOrange,
        ClipboardDataTypes.Image => Brushes.RoyalBlue,
        ClipboardDataTypes.File => Brushes.Goldenrod,
        ClipboardDataTypes.Color => Brushes.MediumSeaGreen,
        ClipboardDataTypes.Url => Brushes.SteelBlue,
        ClipboardDataTypes.Json => Brushes.SlateBlue,
        ClipboardDataTypes.Xml => Brushes.Teal,
        ClipboardDataTypes.Markdown => Brushes.DimGray,
        _ => Brushes.Gray
    };

    public IBrush PreviewForeground => DataType switch
    {
        ClipboardDataTypes.Color => ColorPreviewBrush ?? Brushes.Black,
        ClipboardDataTypes.Url => Brushes.DodgerBlue,
        _ => Brushes.Black
    };

    public double PreviewFontSize => DataType == ClipboardDataTypes.Color ? 28 : 14;

    public FontWeight PreviewFontWeight => DataType == ClipboardDataTypes.Color ? FontWeight.SemiBold : FontWeight.Normal;

    public string FavoriteMarker => IsFavorite ? "★" : string.Empty;

    public string Preview => DataType switch
    {
        ClipboardDataTypes.Image => "Image",
        ClipboardDataTypes.File => GetFilePreview(),
        ClipboardDataTypes.Json => TryFormatJson(Data) ?? NormalizePreview(Data),
        ClipboardDataTypes.Xml => TryFormatXml(Data) ?? NormalizePreview(Data),
        _ => NormalizePreview(Data)
    };

    private static string NormalizePreview(string text) => text.Replace("\r", " ").Replace("\n", " ");

    private string GetFilePreview()
    {
        try
        {
            var paths = GetFilePaths();
            var names = paths.Select(Path.GetFileName).Where(name => !string.IsNullOrEmpty(name));
            return string.Join(", ", names);
        }
        catch
        {
            return "Files";
        }
    }

    private string[] GetFilePaths()
    {
        if (DataType != ClipboardDataTypes.File)
        {
            return Array.Empty<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(Data) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static bool TryParseColor(string? value, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            return Color.TryParse(value.Trim(), out color);
        }
        catch
        {
            return false;
        }
    }

    private static string? TryFormatJson(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return null;
        }
    }

    private static string? TryFormatXml(string text)
    {
        try
        {
            var document = System.Xml.Linq.XDocument.Parse(text);
            return document.ToString();
        }
        catch
        {
            return null;
        }
    }
}
