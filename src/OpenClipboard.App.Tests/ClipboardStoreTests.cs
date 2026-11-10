using System;
using System.IO;
using System.Threading.Tasks;
using Clipboard.Data.Enums;
using Microsoft.Data.Sqlite;
using OpenClipboard.App.Models;
using OpenClipboard.App.Services;
using Xunit;

namespace OpenClipboard.App.Tests;

public sealed class ClipboardStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "OpenClipboardTests", Guid.NewGuid().ToString("N"));
    private readonly ClipboardStore _store;

    public ClipboardStoreTests()
    {
        _store = new ClipboardStore(Path.Combine(_directory, "history.db"));
    }

    [Fact]
    public async Task StoreSupportsFavoriteDeleteAndClear()
    {
        await _store.InitializeAsync();
        var textItem = new ClipboardHistoryItem { DataType = ClipboardDataTypes.Text, Data = "keep me" };
        var imageItem = new ClipboardHistoryItem { DataType = ClipboardDataTypes.Image, Data = "aW1hZ2U=" };
        await _store.AddAsync(textItem);
        await _store.AddAsync(imageItem);

        var entries = await _store.GetRecentAsync(HistoryMode._30);
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, entry => entry.Id == textItem.Id);
        Assert.Contains(entries, entry => entry.Id == imageItem.Id);

        await _store.SetFavoriteAsync(entries[0].Id, true);
        Assert.True((await _store.GetRecentAsync(HistoryMode._30))[0].IsFavorite);

        await _store.DeleteAsync(entries[1].Id);
        Assert.Single(await _store.GetRecentAsync(HistoryMode._30));

        await _store.ClearAsync();
        Assert.Empty(await _store.GetRecentAsync(HistoryMode._30));
    }

    [Fact]
    public async Task InitializeAddsFavoriteColumnToExistingStore()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "old-history.db");
        await using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Pooling = false
        }.ToString()))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE ClipboardEntries (
                    Id TEXT NOT NULL PRIMARY KEY,
                    DataType INTEGER NOT NULL,
                    Data TEXT NOT NULL,
                    CreateTime TEXT NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync();
        }

        using var oldStore = new ClipboardStore(path);
        await oldStore.InitializeAsync();
        await oldStore.AddAsync(new ClipboardHistoryItem { DataType = ClipboardDataTypes.Text, Data = "after upgrade" });
        var entry = Assert.Single(await oldStore.GetRecentAsync(HistoryMode._30));
        Assert.False(entry.IsFavorite);
    }

    [Fact]
    public async Task HistoryRetentionFiltersAndPrunesExpiredEntriesButKeepsFavorites()
    {
        await _store.InitializeAsync();
        var oldEntry = new ClipboardHistoryItem
        {
            Id = "old",
            Data = "old",
            CreateTime = DateTime.UtcNow.AddDays(-31)
        };
        var recentEntry = new ClipboardHistoryItem
        {
            Id = "recent",
            Data = "recent",
            CreateTime = DateTime.UtcNow.AddDays(-2)
        };
        var favoriteEntry = oldEntry with { Id = "favorite", IsFavorite = true };
        await _store.AddAsync(oldEntry);
        await _store.AddAsync(recentEntry);
        await _store.AddAsync(favoriteEntry);
        await _store.SetFavoriteAsync(favoriteEntry.Id, true);

        Assert.Equal(2, (await _store.GetRecentAsync(HistoryMode._30)).Count);
        Assert.Single(await _store.GetRecentAsync(HistoryMode.None));

        await _store.PruneExpiredAsync(HistoryMode.Forever);
        Assert.Equal(3, (await _store.GetRecentAsync(HistoryMode.Forever)).Count);

        await _store.PruneExpiredAsync(HistoryMode._30);
        var remaining = await _store.GetRecentAsync(HistoryMode.Forever);
        Assert.Equal(2, remaining.Count);
        Assert.Contains(remaining, item => item.Id == recentEntry.Id);
        Assert.Contains(remaining, item => item.Id == favoriteEntry.Id && item.IsFavorite);
        Assert.DoesNotContain(remaining, item => item.Id == oldEntry.Id);
    }

    public void Dispose()
    {
        _store.Dispose();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
