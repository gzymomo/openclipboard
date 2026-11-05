using System;
using Clipboard.Data.Enums;

namespace OpenClipboard.App.Models;

public sealed class ClipboardHistoryItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public ClipboardDataTypes DataType { get; init; } = ClipboardDataTypes.Text;

    public string Data { get; init; } = string.Empty;

    public DateTime CreateTime { get; init; } = DateTime.Now;

    public string Preview => Data.Replace("\r", " ").Replace("\n", " ");
}
