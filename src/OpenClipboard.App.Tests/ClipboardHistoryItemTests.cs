using System;
using Clipboard.Data.Enums;
using OpenClipboard.App.Models;
using Xunit;

namespace OpenClipboard.App.Tests;

public sealed class ClipboardHistoryItemTests
{
    [Fact]
    public void JsonPreviewIsFormatted()
    {
        var item = new ClipboardHistoryItem { DataType = ClipboardDataTypes.Json, Data = "{\"a\":1}" };

        var preview = item.Preview;

        Assert.NotEqual("{\"a\":1}", preview);
        Assert.Contains("\n", preview, StringComparison.Ordinal);
        Assert.Contains("\"a\"", preview, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidJsonFallsBackToNormalizedPreview()
    {
        var item = new ClipboardHistoryItem { DataType = ClipboardDataTypes.Json, Data = "{oops" };

        Assert.Equal("{oops", item.Preview);
    }

    [Fact]
    public void XmlPreviewIsFormatted()
    {
        var item = new ClipboardHistoryItem { DataType = ClipboardDataTypes.Xml, Data = "<root><b>hi</b></root>" };

        var preview = item.Preview;

        Assert.NotEqual("<root><b>hi</b></root>", preview);
        Assert.Contains("<b>", preview, StringComparison.Ordinal);
        Assert.Contains("\n", preview, StringComparison.Ordinal);
    }

    [Fact]
    public void TextPreviewNormalizesLineBreaks()
    {
        var item = new ClipboardHistoryItem { DataType = ClipboardDataTypes.Text, Data = "line1\r\nline2" };

        // NormalizePreview replaces \r and \n independently, so a CRLF becomes two spaces.
        Assert.Equal("line1  line2", item.Preview);
    }

    [Fact]
    public void FilePreviewUsesFileNames()
    {
        var item = new ClipboardHistoryItem
        {
            DataType = ClipboardDataTypes.File,
            Data = """["/tmp/a.txt","/docs/b.png"]"""
        };

        Assert.Equal($"a.txt{Environment.NewLine}b.png", item.FileDisplayName);
        Assert.Equal("a.txt, b.png", item.Preview);
    }
}
