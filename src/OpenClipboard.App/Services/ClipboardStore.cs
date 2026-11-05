using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Clipboard.Data.Enums;
using Microsoft.Data.Sqlite;
using OpenClipboard.App.Models;

namespace OpenClipboard.App.Services;

public sealed class ClipboardStore
{
    private readonly string _connectionString;

    public ClipboardStore(string? databasePath = null)
    {
        var path = databasePath ?? Path.Combine(GetDataDirectory(), "OpenClipboard", "cross-platform.db");
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))
            ?? throw new InvalidOperationException("Could not determine the clipboard database directory.");
        Directory.CreateDirectory(directory);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(path)
        }.ToString();
    }

    public async Task InitializeAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS ClipboardEntries (
                Id TEXT NOT NULL PRIMARY KEY,
                DataType INTEGER NOT NULL,
                Data TEXT NOT NULL,
                CreateTime TEXT NOT NULL,
                IsFavorite INTEGER NOT NULL DEFAULT 0
            );
            """;
        await command.ExecuteNonQueryAsync();

        var hasFavoriteColumn = false;
        command.CommandText = "PRAGMA table_info(ClipboardEntries);";
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                hasFavoriteColumn |= string.Equals(reader.GetString(1), "IsFavorite", StringComparison.OrdinalIgnoreCase);
            }
        }

        if (!hasFavoriteColumn)
        {
            command.CommandText = "ALTER TABLE ClipboardEntries ADD COLUMN IsFavorite INTEGER NOT NULL DEFAULT 0;";
            await command.ExecuteNonQueryAsync();
        }
    }

    public async Task<IReadOnlyList<ClipboardHistoryItem>> GetRecentAsync(HistoryMode historyMode, int limit = 500)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, DataType, Data, CreateTime, IsFavorite
            FROM ClipboardEntries
            WHERE IsFavorite = 1 OR ($mode <> 0 AND ($cutoff IS NULL OR CreateTime >= $cutoff))
            ORDER BY CreateTime DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);
        command.Parameters.AddWithValue("$mode", (int)historyMode);
        command.Parameters.AddWithValue("$cutoff", GetCutoff(historyMode) is { } cutoff
            ? cutoff.ToString("O", CultureInfo.InvariantCulture)
            : DBNull.Value);

        var entries = new List<ClipboardHistoryItem>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(new ClipboardHistoryItem
            {
                Id = reader.GetString(0),
                DataType = (ClipboardDataTypes)reader.GetInt32(1),
                Data = reader.GetString(2),
                CreateTime = DateTime.Parse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToLocalTime(),
                IsFavorite = reader.GetBoolean(4)
            });
        }

        return entries;
    }

    public async Task PruneExpiredAsync(HistoryMode historyMode)
    {
        if (historyMode == HistoryMode.Forever)
        {
            return;
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        var cutoff = GetCutoff(historyMode);
        command.CommandText = cutoff is null
            ? "DELETE FROM ClipboardEntries WHERE IsFavorite = 0;"
            : "DELETE FROM ClipboardEntries WHERE IsFavorite = 0 AND CreateTime < $cutoff;";
        if (cutoff is not null)
        {
            command.Parameters.AddWithValue("$cutoff", cutoff.Value.ToString("O", CultureInfo.InvariantCulture));
        }

        await command.ExecuteNonQueryAsync();
    }

    public async Task AddAsync(ClipboardHistoryItem item)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ClipboardEntries (Id, DataType, Data, CreateTime, IsFavorite)
            VALUES ($id, $dataType, $data, $createTime, 0);
            """;
        command.Parameters.AddWithValue("$id", item.Id);
        command.Parameters.AddWithValue("$dataType", (int)item.DataType);
        command.Parameters.AddWithValue("$data", item.Data);
        command.Parameters.AddWithValue("$createTime", item.CreateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync();
    }

    public async Task SetFavoriteAsync(string id, bool isFavorite)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE ClipboardEntries SET IsFavorite = $favorite WHERE Id = $id;";
        command.Parameters.AddWithValue("$favorite", isFavorite);
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(string id)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM ClipboardEntries WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task ClearAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM ClipboardEntries;";
        await command.ExecuteNonQueryAsync();
    }

    public async Task<int> ImportLegacyDatabaseAsync(string databasePath)
    {
        var sourceConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();
        await using var source = new SqliteConnection(sourceConnectionString);
        await source.OpenAsync();
        await using var destination = new SqliteConnection(_connectionString);
        await destination.OpenAsync();
        using var transaction = destination.BeginTransaction();
        await using var read = source.CreateCommand();
        read.CommandText = """
            SELECT ID, DataType, Data, CreateTime, IsFavorite
            FROM ClipboardData
            WHERE IsDeleted = 0;
            """;
        await using var reader = await read.ExecuteReaderAsync();
        var imported = 0;
        while (await reader.ReadAsync())
        {
            await using var insert = destination.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT OR IGNORE INTO ClipboardEntries (Id, DataType, Data, CreateTime, IsFavorite)
                VALUES ($id, $dataType, $data, $createTime, $favorite);
                """;
            insert.Parameters.AddWithValue("$id", reader.GetString(0));
            insert.Parameters.AddWithValue("$dataType", reader.GetInt32(1));
            insert.Parameters.AddWithValue("$data", reader.IsDBNull(2) ? string.Empty : reader.GetString(2));
            insert.Parameters.AddWithValue("$createTime", DateTime.Parse(reader.GetString(3), CultureInfo.InvariantCulture).ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            insert.Parameters.AddWithValue("$favorite", reader.GetBoolean(4));
            imported += await insert.ExecuteNonQueryAsync();
        }

        transaction.Commit();
        return imported;
    }

    private static string GetDataDirectory()
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

    private static DateTime? GetCutoff(HistoryMode historyMode) => historyMode switch
    {
        HistoryMode.None => null,
        HistoryMode._7 => DateTime.UtcNow.AddDays(-7),
        HistoryMode._30 => DateTime.UtcNow.AddDays(-30),
        HistoryMode.Forever => DateTime.MinValue,
        _ => throw new ArgumentOutOfRangeException(nameof(historyMode), historyMode, "Unknown history retention mode.")
    };
}
