using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Clipboard.Data.Enums;
using Microsoft.Data.Sqlite;
using OpenClipboard.App.Models;

namespace OpenClipboard.App.Services;

public sealed class ClipboardStore : IDisposable
{
    private readonly string _connectionString;
    private bool _disposed;

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
                IsFavorite INTEGER NOT NULL DEFAULT 0,
                SourceAppName TEXT,
                SourceAppId TEXT,
                SourceIconPath TEXT
            );
            """;
        await command.ExecuteNonQueryAsync();

        var hasFavoriteColumn = false;
        var hasSourceAppNameColumn = false;
        var hasSourceAppIdColumn = false;
        var hasSourceIconPathColumn = false;
        command.CommandText = "PRAGMA table_info(ClipboardEntries);";
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var columnName = reader.GetString(1);
                hasFavoriteColumn |= string.Equals(columnName, "IsFavorite", StringComparison.OrdinalIgnoreCase);
                hasSourceAppNameColumn |= string.Equals(columnName, "SourceAppName", StringComparison.OrdinalIgnoreCase);
                hasSourceAppIdColumn |= string.Equals(columnName, "SourceAppId", StringComparison.OrdinalIgnoreCase);
                hasSourceIconPathColumn |= string.Equals(columnName, "SourceIconPath", StringComparison.OrdinalIgnoreCase);
            }
        }

        if (!hasFavoriteColumn)
        {
            command.CommandText = "ALTER TABLE ClipboardEntries ADD COLUMN IsFavorite INTEGER NOT NULL DEFAULT 0;";
            await command.ExecuteNonQueryAsync();
        }

        if (!hasSourceAppNameColumn)
        {
            command.CommandText = "ALTER TABLE ClipboardEntries ADD COLUMN SourceAppName TEXT;";
            await command.ExecuteNonQueryAsync();
        }

        if (!hasSourceAppIdColumn)
        {
            command.CommandText = "ALTER TABLE ClipboardEntries ADD COLUMN SourceAppId TEXT;";
            await command.ExecuteNonQueryAsync();
        }

        if (!hasSourceIconPathColumn)
        {
            command.CommandText = "ALTER TABLE ClipboardEntries ADD COLUMN SourceIconPath TEXT;";
            await command.ExecuteNonQueryAsync();
        }
    }

    public async Task<IReadOnlyList<ClipboardHistoryItem>> GetRecentAsync(
        HistoryMode historyMode,
        int limit = 50,
        DateTime? beforeCreateTimeUtc = null,
        string? beforeId = null,
        string? searchText = null,
        bool favoritesOnly = false)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();

        var where = new List<string>();
        var cutoff = GetCutoff(historyMode);
        if (cutoff is not null)
        {
            where.Add("(IsFavorite = 1 OR CreateTime >= $cutoff)");
            command.Parameters.AddWithValue("$cutoff", cutoff.Value.ToString("O", CultureInfo.InvariantCulture));
        }

        if (favoritesOnly)
        {
            where.Add("IsFavorite = 1");
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            where.Add("Data LIKE $search ESCAPE '\\'");
            command.Parameters.AddWithValue("$search", $"%{EscapeLike(searchText.Trim())}%");
        }

        if (beforeCreateTimeUtc is not null && !string.IsNullOrWhiteSpace(beforeId))
        {
            var before = beforeCreateTimeUtc.Value.ToString("O", CultureInfo.InvariantCulture);
            where.Add("(CreateTime < $beforeCreateTime OR (CreateTime = $beforeCreateTime AND Id < $beforeId))");
            command.Parameters.AddWithValue("$beforeCreateTime", before);
            command.Parameters.AddWithValue("$beforeId", beforeId);
        }

        var whereClause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : string.Empty;
        command.CommandText = $"""
            SELECT Id, DataType, Data, CreateTime, IsFavorite, SourceAppName, SourceAppId, SourceIconPath
            FROM ClipboardEntries
            {whereClause}
            ORDER BY CreateTime DESC, Id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        var entries = new List<ClipboardHistoryItem>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(ReadItem(reader));
        }

        return entries;
    }

    private static ClipboardHistoryItem ReadItem(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        DataType = (ClipboardDataTypes)reader.GetInt32(1),
        Data = reader.GetString(2),
        CreateTime = DateTime.Parse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToLocalTime(),
        IsFavorite = reader.GetBoolean(4),
        SourceAppName = reader.IsDBNull(5) ? null : reader.GetString(5),
        SourceAppId = reader.IsDBNull(6) ? null : reader.GetString(6),
        SourceIconPath = reader.IsDBNull(7) ? null : reader.GetString(7)
    };

    private static string EscapeLike(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

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
        if (cutoff is null)
        {
            return;
        }

        command.CommandText = "DELETE FROM ClipboardEntries WHERE IsFavorite = 0 AND CreateTime < $cutoff;";
        command.Parameters.AddWithValue("$cutoff", cutoff.Value.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync();
    }

    public async Task AddAsync(ClipboardHistoryItem item)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ClipboardEntries (Id, DataType, Data, CreateTime, IsFavorite, SourceAppName, SourceAppId, SourceIconPath)
            VALUES ($id, $dataType, $data, $createTime, 0, $sourceAppName, $sourceAppId, $sourceIconPath);
            """;
        command.Parameters.AddWithValue("$id", item.Id);
        command.Parameters.AddWithValue("$dataType", (int)item.DataType);
        command.Parameters.AddWithValue("$data", item.Data);
        command.Parameters.AddWithValue("$createTime", item.CreateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$sourceAppName", (object?)item.SourceAppName ?? DBNull.Value);
        command.Parameters.AddWithValue("$sourceAppId", (object?)item.SourceAppId ?? DBNull.Value);
        command.Parameters.AddWithValue("$sourceIconPath", (object?)item.SourceIconPath ?? DBNull.Value);
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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        using var connection = new SqliteConnection(_connectionString);
        SqliteConnection.ClearPool(connection);
        _disposed = true;
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
        HistoryMode._1 => DateTime.UtcNow.AddDays(-1),
        HistoryMode._3 => DateTime.UtcNow.AddDays(-3),
        HistoryMode._5 => DateTime.UtcNow.AddDays(-5),
        HistoryMode._7 => DateTime.UtcNow.AddDays(-7),
        HistoryMode._15 => DateTime.UtcNow.AddDays(-15),
        HistoryMode._30 => DateTime.UtcNow.AddDays(-30),
        HistoryMode.Forever => null,
        _ => throw new ArgumentOutOfRangeException(nameof(historyMode), historyMode, "Unknown history retention mode.")
    };
}
