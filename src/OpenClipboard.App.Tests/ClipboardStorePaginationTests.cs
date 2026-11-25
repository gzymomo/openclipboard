using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Clipboard.Data.Enums;
using OpenClipboard.App.Models;
using OpenClipboard.App.Services;
using Xunit;

namespace OpenClipboard.App.Tests;

public sealed class ClipboardStorePaginationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "OpenClipboardTests", Guid.NewGuid().ToString("N"));
    private readonly ClipboardStore _store;

    public ClipboardStorePaginationTests()
    {
        _store = new ClipboardStore(Path.Combine(_directory, "pagination.db"));
    }

    [Fact]
    public async Task GetRecentAsyncPaginatesWithCursorWithoutDuplicatesOrGaps()
    {
        await _store.InitializeAsync();
        var baseTime = DateTime.UtcNow.AddMinutes(-10);
        for (var i = 0; i < 5; i++)
        {
            await _store.AddAsync(new ClipboardHistoryItem
            {
                Id = $"item-{i}",
                Data = $"value-{i}",
                CreateTime = baseTime.AddMinutes(i)
            });
        }

        var collected = new List<string>();
        DateTime? cursorTime = null;
        string? cursorId = null;
        for (var page = 0; page < 10; page++)
        {
            var items = await _store.GetRecentAsync(
                HistoryMode.Forever,
                limit: 2,
                beforeCreateTimeUtc: cursorTime,
                beforeId: cursorId);
            if (items.Count == 0)
            {
                break;
            }

            collected.AddRange(items.Select(item => item.Id));
            var last = items[^1];
            cursorTime = last.CreateTime.ToUniversalTime();
            cursorId = last.Id;
        }

        Assert.Equal(new[] { "item-4", "item-3", "item-2", "item-1", "item-0" }, collected);
    }

    [Fact]
    public async Task GetRecentAsyncCursorHandlesIdenticalTimestamps()
    {
        await _store.InitializeAsync();
        var sameTime = DateTime.UtcNow.AddMinutes(-1);
        foreach (var id in new[] { "a", "b", "c" })
        {
            await _store.AddAsync(new ClipboardHistoryItem
            {
                Id = id,
                Data = id,
                CreateTime = sameTime
            });
        }

        var collected = new List<string>();
        DateTime? cursorTime = null;
        string? cursorId = null;
        for (var page = 0; page < 10; page++)
        {
            var items = await _store.GetRecentAsync(
                HistoryMode.Forever,
                limit: 1,
                beforeCreateTimeUtc: cursorTime,
                beforeId: cursorId);
            if (items.Count == 0)
            {
                break;
            }

            collected.Add(items[0].Id);
            cursorTime = items[0].CreateTime.ToUniversalTime();
            cursorId = items[0].Id;
        }

        Assert.Equal(new[] { "c", "b", "a" }, collected);
    }

    [Fact]
    public async Task GetRecentAsyncSearchesDataWithLikeEscaping()
    {
        await _store.InitializeAsync();
        await _store.AddAsync(new ClipboardHistoryItem { Id = "plain", Data = "hello world", CreateTime = DateTime.UtcNow });
        await _store.AddAsync(new ClipboardHistoryItem { Id = "percent", Data = "100% done", CreateTime = DateTime.UtcNow });
        await _store.AddAsync(new ClipboardHistoryItem { Id = "underscore", Data = "a_b", CreateTime = DateTime.UtcNow });

        var percentMatches = await _store.GetRecentAsync(HistoryMode.Forever, searchText: "%");
        Assert.Single(percentMatches);
        Assert.Equal("percent", percentMatches[0].Id);

        var underscoreMatches = await _store.GetRecentAsync(HistoryMode.Forever, searchText: "_");
        Assert.Single(underscoreMatches);
        Assert.Equal("underscore", underscoreMatches[0].Id);

        var caseMatches = await _store.GetRecentAsync(HistoryMode.Forever, searchText: "WORLD");
        Assert.Single(caseMatches);
        Assert.Equal("plain", caseMatches[0].Id);
    }

    [Fact]
    public async Task GetRecentAsyncFavoritesOnlyIncludesFavoriteRegardlessOfRetention()
    {
        await _store.InitializeAsync();
        await _store.AddAsync(new ClipboardHistoryItem { Id = "old-fav", Data = "fav", CreateTime = DateTime.UtcNow.AddDays(-10) });
        await _store.AddAsync(new ClipboardHistoryItem { Id = "old-plain", Data = "old", CreateTime = DateTime.UtcNow.AddDays(-10) });
        await _store.AddAsync(new ClipboardHistoryItem { Id = "new-plain", Data = "new", CreateTime = DateTime.UtcNow });
        await _store.SetFavoriteAsync("old-fav", true);

        var favorites = await _store.GetRecentAsync(HistoryMode._1, favoritesOnly: true);
        Assert.Single(favorites);
        Assert.Equal("old-fav", favorites[0].Id);
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
