using System.Text.Json;
using Microsoft.Data.Sqlite;
using Mdv.Windows.Core.Models;

namespace Mdv.Windows.Core.Services;

public sealed class SqliteStorageService(string databasePath) : IStorageService
{
    private readonly string _connectionString = $"Data Source={databasePath}";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = """
                  CREATE TABLE IF NOT EXISTS recent_files (
                      path TEXT PRIMARY KEY,
                      last_opened_utc TEXT NOT NULL,
                      last_scroll_position REAL NOT NULL
                  );

                  CREATE TABLE IF NOT EXISTS bookmarks (
                      slot INTEGER NOT NULL,
                      file_path TEXT NOT NULL,
                      scroll_position REAL NOT NULL,
                      updated_at_utc TEXT NOT NULL,
                      PRIMARY KEY(slot, file_path)
                  );

                  CREATE TABLE IF NOT EXISTS preferences (
                      id INTEGER PRIMARY KEY CHECK(id = 1),
                      json TEXT NOT NULL
                  );
                  """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RecentFileEntry>> GetRecentFilesAsync(int maxCount = 100, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT path, last_opened_utc, last_scroll_position
                              FROM recent_files
                              ORDER BY last_opened_utc DESC
                              LIMIT $limit;
                              """;
        command.Parameters.AddWithValue("$limit", maxCount);

        var result = new List<RecentFileEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new RecentFileEntry
            {
                Path = reader.GetString(0),
                LastOpenedAt = DateTimeOffset.Parse(reader.GetString(1)),
                LastScrollPosition = reader.GetDouble(2)
            });
        }

        return result;
    }

    public async Task UpsertRecentFileAsync(string filePath, double scrollPosition, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
                              INSERT INTO recent_files (path, last_opened_utc, last_scroll_position)
                              VALUES ($path, $now, $position)
                              ON CONFLICT(path)
                              DO UPDATE SET
                                last_opened_utc = excluded.last_opened_utc,
                                last_scroll_position = excluded.last_scroll_position;
                              """;
        command.Parameters.AddWithValue("$path", filePath);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$position", scrollPosition);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteRecentFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM recent_files WHERE path = $path;";
        command.Parameters.AddWithValue("$path", filePath);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveBookmarkAsync(int slot, string filePath, double scrollPosition, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
                              INSERT INTO bookmarks (slot, file_path, scroll_position, updated_at_utc)
                              VALUES ($slot, $path, $position, $now)
                              ON CONFLICT(slot, file_path)
                              DO UPDATE SET
                                scroll_position = excluded.scroll_position,
                                updated_at_utc = excluded.updated_at_utc;
                              """;
        command.Parameters.AddWithValue("$slot", slot);
        command.Parameters.AddWithValue("$path", filePath);
        command.Parameters.AddWithValue("$position", scrollPosition);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BookmarkEntry>> GetBookmarksAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT slot, file_path, scroll_position, updated_at_utc
                              FROM bookmarks
                              WHERE file_path = $path
                              ORDER BY slot ASC;
                              """;
        command.Parameters.AddWithValue("$path", filePath);

        var bookmarks = new List<BookmarkEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            bookmarks.Add(new BookmarkEntry
            {
                Slot = reader.GetInt32(0),
                FilePath = reader.GetString(1),
                ScrollPosition = reader.GetDouble(2),
                UpdatedAt = DateTimeOffset.Parse(reader.GetString(3))
            });
        }

        return bookmarks;
    }

    public async Task<BookmarkEntry?> GetBookmarkAsync(int slot, string filePath, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT slot, file_path, scroll_position, updated_at_utc
                              FROM bookmarks
                              WHERE slot = $slot AND file_path = $path
                              LIMIT 1;
                              """;
        command.Parameters.AddWithValue("$slot", slot);
        command.Parameters.AddWithValue("$path", filePath);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new BookmarkEntry
        {
            Slot = reader.GetInt32(0),
            FilePath = reader.GetString(1),
            ScrollPosition = reader.GetDouble(2),
            UpdatedAt = DateTimeOffset.Parse(reader.GetString(3))
        };
    }

    public async Task SavePreferencesAsync(UserPreferences preferences, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(preferences);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
                              INSERT INTO preferences (id, json)
                              VALUES (1, $json)
                              ON CONFLICT(id)
                              DO UPDATE SET json = excluded.json;
                              """;
        command.Parameters.AddWithValue("$json", payload);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<UserPreferences> LoadPreferencesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT json FROM preferences WHERE id = 1 LIMIT 1;";

        var json = await command.ExecuteScalarAsync(cancellationToken) as string;
        if (string.IsNullOrWhiteSpace(json))
        {
            return new UserPreferences();
        }

        return JsonSerializer.Deserialize<UserPreferences>(json) ?? new UserPreferences();
    }
}
