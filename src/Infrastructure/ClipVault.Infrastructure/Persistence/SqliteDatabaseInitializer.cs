using Microsoft.Data.Sqlite;

namespace ClipVault.Infrastructure.Persistence;

public sealed class SqliteDatabaseInitializer
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteDatabaseInitializer(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            PRAGMA journal_mode = WAL;
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS ClipboardItems (
                Id TEXT PRIMARY KEY,
                CreatedAtUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                ItemType INTEGER NOT NULL,
                Type INTEGER NOT NULL,
                Title TEXT NULL,
                TextContent TEXT NULL,
                ImagePath TEXT NULL,
                ThumbPath TEXT NULL,
                ContentHash TEXT NOT NULL,
                IsInLibrary INTEGER NOT NULL DEFAULT 0,
                IsFavorite INTEGER NOT NULL,
                CategoryId TEXT NULL,
                IsLocked INTEGER NOT NULL,
                EncryptedPayload BLOB NULL,
                EncryptionMeta TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_ClipboardItems_CreatedAtUtc
                ON ClipboardItems (CreatedAtUtc DESC);

            CREATE INDEX IF NOT EXISTS IX_ClipboardItems_CategoryId
                ON ClipboardItems (CategoryId);

            CREATE TABLE IF NOT EXISTS Categories (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Color TEXT NULL,
                SortOrder INTEGER NOT NULL DEFAULT 0
            );

            CREATE UNIQUE INDEX IF NOT EXISTS IX_Categories_Name
                ON Categories (Name COLLATE NOCASE);

            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );
            """;

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);

        await EnsureClipboardItemsSchemaAsync(connection, cancellationToken);
        await EnsureCategoriesSchemaAsync(connection, cancellationToken);
    }

    private static async Task EnsureClipboardItemsSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        HashSet<string> columns = new(StringComparer.OrdinalIgnoreCase);

        await using (SqliteCommand pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA table_info(ClipboardItems);";
            await using SqliteDataReader reader = await pragma.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(reader.GetString(1));
            }
        }

        if (columns.Count == 0)
        {
            return;
        }

        if (!columns.Contains("Id"))
        {
            throw new InvalidOperationException("ClipboardItems table is missing Id column and cannot be auto-migrated.");
        }

        Dictionary<string, string> requiredColumns = new(StringComparer.OrdinalIgnoreCase)
        {
            ["CreatedAtUtc"] = "ALTER TABLE ClipboardItems ADD COLUMN CreatedAtUtc TEXT NOT NULL DEFAULT '1970-01-01T00:00:00.0000000Z';",
            ["UpdatedAtUtc"] = "ALTER TABLE ClipboardItems ADD COLUMN UpdatedAtUtc TEXT NOT NULL DEFAULT '1970-01-01T00:00:00.0000000Z';",
            ["ItemType"] = "ALTER TABLE ClipboardItems ADD COLUMN ItemType INTEGER NOT NULL DEFAULT 0;",
            ["Type"] = "ALTER TABLE ClipboardItems ADD COLUMN Type INTEGER NOT NULL DEFAULT 0;",
            ["Title"] = "ALTER TABLE ClipboardItems ADD COLUMN Title TEXT NULL;",
            ["TextContent"] = "ALTER TABLE ClipboardItems ADD COLUMN TextContent TEXT NULL;",
            ["ImagePath"] = "ALTER TABLE ClipboardItems ADD COLUMN ImagePath TEXT NULL;",
            ["ThumbPath"] = "ALTER TABLE ClipboardItems ADD COLUMN ThumbPath TEXT NULL;",
            ["ContentHash"] = "ALTER TABLE ClipboardItems ADD COLUMN ContentHash TEXT NOT NULL DEFAULT '';",
            ["IsInLibrary"] = "ALTER TABLE ClipboardItems ADD COLUMN IsInLibrary INTEGER NOT NULL DEFAULT 0;",
            ["IsFavorite"] = "ALTER TABLE ClipboardItems ADD COLUMN IsFavorite INTEGER NOT NULL DEFAULT 0;",
            ["CategoryId"] = "ALTER TABLE ClipboardItems ADD COLUMN CategoryId TEXT NULL;",
            ["IsLocked"] = "ALTER TABLE ClipboardItems ADD COLUMN IsLocked INTEGER NOT NULL DEFAULT 0;",
            ["EncryptedPayload"] = "ALTER TABLE ClipboardItems ADD COLUMN EncryptedPayload BLOB NULL;",
            ["EncryptionMeta"] = "ALTER TABLE ClipboardItems ADD COLUMN EncryptionMeta TEXT NULL;"
        };

        foreach ((string name, string alterSql) in requiredColumns)
        {
            if (columns.Contains(name))
            {
                continue;
            }

            await using SqliteCommand alter = connection.CreateCommand();
            alter.CommandText = alterSql;
            await alter.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task EnsureCategoriesSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "Categories", cancellationToken))
        {
            return;
        }

        HashSet<string> columns = new(StringComparer.OrdinalIgnoreCase);
        await using (SqliteCommand pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA table_info(Categories);";
            await using SqliteDataReader reader = await pragma.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(reader.GetString(1));
            }
        }

        Dictionary<string, string> requiredColumns = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = "ALTER TABLE Categories ADD COLUMN Id TEXT;",
            ["Name"] = "ALTER TABLE Categories ADD COLUMN Name TEXT;",
            ["Color"] = "ALTER TABLE Categories ADD COLUMN Color TEXT NULL;",
            ["SortOrder"] = "ALTER TABLE Categories ADD COLUMN SortOrder INTEGER NOT NULL DEFAULT 0;"
        };

        foreach ((string name, string alterSql) in requiredColumns)
        {
            if (columns.Contains(name))
            {
                continue;
            }

            await using SqliteCommand alter = connection.CreateCommand();
            alter.CommandText = alterSql;
            await alter.ExecuteNonQueryAsync(cancellationToken);
        }

        await using SqliteCommand uniqueIndex = connection.CreateCommand();
        uniqueIndex.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS IX_Categories_Name ON Categories (Name COLLATE NOCASE);";
        await uniqueIndex.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name = $name LIMIT 1;";
        command.Parameters.AddWithValue("$name", tableName);
        object? value = await command.ExecuteScalarAsync(cancellationToken);
        return value is not null;
    }
}
