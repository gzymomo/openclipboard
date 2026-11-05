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

    public ClipboardStore()
    {
        var directory = Path.Combine(GetDataDirectory(), "OpenClipboard");
        Directory.CreateDirectory(directory);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(directory, "cross-platform.db")
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
                CreateTime TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<ClipboardHistoryItem>> GetRecentAsync(int limit = 500)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, DataType, Data, CreateTime
            FROM ClipboardEntries
            ORDER BY CreateTime DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        var entries = new List<ClipboardHistoryItem>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(new ClipboardHistoryItem
            {
                Id = reader.GetString(0),
                DataType = (ClipboardDataTypes)reader.GetInt32(1),
                Data = reader.GetString(2),
                CreateTime = DateTime.Parse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToLocalTime()
            });
        }

        return entries;
    }

    public async Task AddAsync(string text)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ClipboardEntries (Id, DataType, Data, CreateTime)
            VALUES ($id, $dataType, $data, $createTime);
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
        command.Parameters.AddWithValue("$dataType", (int)ClipboardDataTypes.Text);
        command.Parameters.AddWithValue("$data", text);
        command.Parameters.AddWithValue("$createTime", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync();
    }

    private static string GetDataDirectory()
    {
        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support");
        }

        if (OperatingSystem.IsLinux())
        {
            return Environment.GetEnvironmentVariable("XDG_DATA_HOME")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    }
}
