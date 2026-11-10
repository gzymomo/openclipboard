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

    public string TypeLabel { get; init; } = string.Empty;

    public string ItemInfo { get; init; } = string.Empty;

    public string DisplayPreview { get; init; } = string.Empty;

    public Bitmap? ImageBitmap { get; init; }

    public bool HasImagePreview => ImageBitmap is not null;

    public IBrush TypeBrush => DataType switch
    {
        ClipboardDataTypes.Text => Brushes.DarkOrange,
        ClipboardDataTypes.Image => Brushes.RoyalBlue,
        ClipboardDataTypes.File => Brushes.Goldenrod,
        _ => Brushes.Gray
    };

    public string FavoriteMarker => IsFavorite ? "★" : string.Empty;

    public string Preview => DataType switch
    {
        ClipboardDataTypes.Image => "Image",
        ClipboardDataTypes.File => GetFilePreview(),
        _ => Data.Replace("\r", " ").Replace("\n", " ")
    };

    private string GetFilePreview()
    {
        try
        {
            var paths = JsonSerializer.Deserialize<string[]>(Data) ?? Array.Empty<string>();
            var names = paths.Select(Path.GetFileName).Where(name => !string.IsNullOrEmpty(name));
            return string.Join(", ", names);
        }
        catch (JsonException)
        {
            return "Files";
        }
    }
}
