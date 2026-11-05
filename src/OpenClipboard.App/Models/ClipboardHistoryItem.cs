using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Clipboard.Data.Enums;

namespace OpenClipboard.App.Models;

public sealed record ClipboardHistoryItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public ClipboardDataTypes DataType { get; init; } = ClipboardDataTypes.Text;

    public string Data { get; init; } = string.Empty;

    public DateTime CreateTime { get; init; } = DateTime.Now;

    public bool IsFavorite { get; init; }

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
