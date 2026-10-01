using ClipVault.Core.Abstractions.Repositories;
using ClipVault.Core.Domain.Entities;
using ClipVault.Core.Domain.Enums;
using ClipVault.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ClipVault.Infrastructure.Repositories;

public sealed class SqliteClipboardItemRepository : IClipboardItemRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteClipboardItemRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<ClipboardItem>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, CreatedAtUtc, Type, Title, TextContent, ImagePath, IsFavorite, CategoryId, IsLocked, EncryptedPayload, EncryptionMeta
            FROM ClipboardItems
            ORDER BY CreatedAtUtc DESC
            LIMIT $limit;
            """;

        return await QueryAsync(sql, cmd => cmd.Parameters.AddWithValue("$limit", limit), cancellationToken);
    }

    public async Task<IReadOnlyList<ClipboardItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, CreatedAtUtc, Type, Title, TextContent, ImagePath, IsFavorite, CategoryId, IsLocked, EncryptedPayload, EncryptionMeta
            FROM ClipboardItems
            ORDER BY CreatedAtUtc DESC;
            """;

        return await QueryAsync(sql, null, cancellationToken);
    }

    public async Task<IReadOnlyList<ClipboardItem>> GetFavoritesAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, CreatedAtUtc, Type, Title, TextContent, ImagePath, IsFavorite, CategoryId, IsLocked, EncryptedPayload, EncryptionMeta
            FROM ClipboardItems
            WHERE IsFavorite = 1
            ORDER BY CreatedAtUtc DESC;
            """;

        return await QueryAsync(sql, null, cancellationToken);
    }

    public async Task UpsertAsync(ClipboardItem item, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO ClipboardItems (
                Id, CreatedAtUtc, UpdatedAtUtc, ItemType, Type, Title, TextContent, ImagePath, ThumbPath, ContentHash, IsInLibrary, IsFavorite, CategoryId, IsLocked, EncryptedPayload, EncryptionMeta
            ) VALUES (
                $id, $createdAtUtc, $updatedAtUtc, $itemType, $type, $title, $textContent, $imagePath, $thumbPath, $contentHash, $isInLibrary, $isFavorite, $categoryId, $isLocked, $encryptedPayload, $encryptionMeta
            )
            ON CONFLICT(Id) DO UPDATE SET
                CreatedAtUtc = excluded.CreatedAtUtc,
                UpdatedAtUtc = excluded.UpdatedAtUtc,
                ItemType = excluded.ItemType,
                Type = excluded.Type,
                Title = excluded.Title,
                TextContent = excluded.TextContent,
                ImagePath = excluded.ImagePath,
                ThumbPath = excluded.ThumbPath,
                ContentHash = excluded.ContentHash,
                IsInLibrary = excluded.IsInLibrary,
                IsFavorite = excluded.IsFavorite,
                CategoryId = excluded.CategoryId,
                IsLocked = excluded.IsLocked,
                EncryptedPayload = excluded.EncryptedPayload,
                EncryptionMeta = excluded.EncryptionMeta;
            """;

        await using SqliteConnection connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using SqliteCommand command = connection.CreateCommand();
        string contentHash = BuildContentHash(item);
        object categoryValue = await ResolveCategoryValueAsync(connection, item.CategoryId, cancellationToken);
        command.CommandText = sql;
        command.Parameters.AddWithValue("$id", item.Id);
        command.Parameters.AddWithValue("$createdAtUtc", item.CreatedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$updatedAtUtc", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$itemType", (int)item.Type);
        command.Parameters.AddWithValue("$type", (int)item.Type);
        command.Parameters.AddWithValue("$title", (object?)item.Title ?? DBNull.Value);
        command.Parameters.AddWithValue("$textContent", (object?)item.TextContent ?? DBNull.Value);
        command.Parameters.AddWithValue("$imagePath", (object?)item.ImagePath ?? DBNull.Value);
        command.Parameters.AddWithValue("$thumbPath", DBNull.Value);
        command.Parameters.AddWithValue("$contentHash", contentHash);
        command.Parameters.AddWithValue("$isInLibrary", 0);
        command.Parameters.AddWithValue("$isFavorite", item.IsFavorite ? 1 : 0);
        command.Parameters.AddWithValue("$categoryId", categoryValue);
        command.Parameters.AddWithValue("$isLocked", item.IsLocked ? 1 : 0);
        command.Parameters.AddWithValue("$encryptedPayload", (object?)item.EncryptedPayload ?? DBNull.Value);
        command.Parameters.AddWithValue("$encryptionMeta", (object?)item.EncryptionMeta ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM ClipboardItems WHERE Id = $id;";

        await using SqliteConnection connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$id", id);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM ClipboardItems;";

        await using SqliteConnection connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<ClipboardItem>> QueryAsync(
        string sql,
        Action<SqliteCommand>? bind,
        CancellationToken cancellationToken)
    {
        List<ClipboardItem> items = [];

        await using SqliteConnection connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        bind?.Invoke(command);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(Map(reader));
        }

        return items;
    }

    private static ClipboardItem Map(SqliteDataReader reader)
    {
        return new ClipboardItem
        {
            Id = reader.GetString(0),
            CreatedAtUtc = ParseCreatedAtUtc(reader, 1),
            Type = ParseType(reader, 2),
            Title = reader.IsDBNull(3) ? null : reader.GetString(3),
            TextContent = reader.IsDBNull(4) ? null : reader.GetString(4),
            ImagePath = reader.IsDBNull(5) ? null : reader.GetString(5),
            IsFavorite = ParseBool(reader, 6),
            CategoryId = reader.IsDBNull(7) ? null : reader.GetString(7),
            IsLocked = ParseBool(reader, 8),
            EncryptedPayload = reader.IsDBNull(9) ? null : (byte[])reader[9],
            EncryptionMeta = reader.IsDBNull(10) ? null : reader.GetString(10)
        };
    }

    private static DateTime ParseCreatedAtUtc(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return DateTime.UtcNow;
        }

        object raw = reader.GetValue(ordinal);
        if (raw is long longValue)
        {
            return ParseUnix(longValue);
        }

        if (raw is int intValue)
        {
            return ParseUnix(intValue);
        }

        string text = Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime parsed))
        {
            return parsed;
        }

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long epoch))
        {
            return ParseUnix(epoch);
        }

        return DateTime.UtcNow;
    }

    private static DateTime ParseUnix(long value)
    {
        try
        {
            // Heuristic: >= 10^11 is probably milliseconds, otherwise seconds.
            return value >= 100_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(value).UtcDateTime
                : DateTimeOffset.FromUnixTimeSeconds(value).UtcDateTime;
        }
        catch (ArgumentOutOfRangeException)
        {
            return DateTime.UtcNow;
        }
    }

    private static ClipboardItemType ParseType(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return ClipboardItemType.Text;
        }

        object raw = reader.GetValue(ordinal);
        if (raw is long longValue)
        {
            return longValue == 1 ? ClipboardItemType.Image : ClipboardItemType.Text;
        }

        if (raw is int intValue)
        {
            return intValue == 1 ? ClipboardItemType.Image : ClipboardItemType.Text;
        }

        string text = Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
        return text.Equals("image", StringComparison.OrdinalIgnoreCase) || text == "1"
            ? ClipboardItemType.Image
            : ClipboardItemType.Text;
    }

    private static bool ParseBool(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return false;
        }

        object raw = reader.GetValue(ordinal);
        if (raw is long longValue)
        {
            return longValue == 1;
        }

        if (raw is int intValue)
        {
            return intValue == 1;
        }

        string text = Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
        return text == "1" || bool.TryParse(text, out bool parsed) && parsed;
    }

    private static async Task<object> ResolveCategoryValueAsync(
        SqliteConnection connection,
        string? categoryIdOrName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(categoryIdOrName))
        {
            return DBNull.Value;
        }

        if (!await TableExistsAsync(connection, "Categories", cancellationToken))
        {
            return categoryIdOrName;
        }

        const string byIdSql = "SELECT Id FROM Categories WHERE Id = $value LIMIT 1;";
        await using (SqliteCommand byId = connection.CreateCommand())
        {
            byId.CommandText = byIdSql;
            byId.Parameters.AddWithValue("$value", categoryIdOrName);
            object? found = await byId.ExecuteScalarAsync(cancellationToken);
            if (found is string id && !string.IsNullOrWhiteSpace(id))
            {
                return id;
            }
        }

        const string byNameSql = "SELECT Id FROM Categories WHERE Name = $value LIMIT 1;";
        try
        {
            await using SqliteCommand byName = connection.CreateCommand();
            byName.CommandText = byNameSql;
            byName.Parameters.AddWithValue("$value", categoryIdOrName);
            object? found = await byName.ExecuteScalarAsync(cancellationToken);
            if (found is string id && !string.IsNullOrWhiteSpace(id))
            {
                return id;
            }
        }
        catch (SqliteException)
        {
            // Legacy categories tables may not have Name; fallback to null to satisfy FK.
        }

        return DBNull.Value;
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        const string sql = "SELECT 1 FROM sqlite_master WHERE type='table' AND name = $name LIMIT 1;";
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$name", tableName);
        object? value = await command.ExecuteScalarAsync(cancellationToken);
        return value is not null;
    }

    private static string BuildContentHash(ClipboardItem item)
    {
        byte[] input;

        if (item.IsLocked && item.EncryptedPayload is { Length: > 0 })
        {
            input = item.EncryptedPayload;
        }
        else if (item.Type == ClipboardItemType.Text)
        {
            input = Encoding.UTF8.GetBytes(item.TextContent ?? string.Empty);
        }
        else if (!string.IsNullOrWhiteSpace(item.ImagePath) && File.Exists(item.ImagePath))
        {
            input = File.ReadAllBytes(item.ImagePath);
        }
        else
        {
            input = Encoding.UTF8.GetBytes(item.ImagePath ?? string.Empty);
        }

        byte[] hash = SHA256.HashData(input);
        return Convert.ToHexString(hash);
    }
}
