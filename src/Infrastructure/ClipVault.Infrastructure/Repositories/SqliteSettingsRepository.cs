using ClipVault.Core.Abstractions.Repositories;
using ClipVault.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace ClipVault.Infrastructure.Repositories;

public sealed class SqliteSettingsRepository : ISettingsRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private bool? _hasUpdatedAtUtcColumn;

    public SqliteSettingsRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT Value FROM Settings WHERE Key = $key LIMIT 1;";

        await using SqliteConnection connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$key", key);

        object? value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null || value == DBNull.Value ? null : (string)value;
    }

    public async Task SetValueAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        bool hasUpdatedAtUtc = await HasUpdatedAtUtcColumnAsync(connection, cancellationToken);

        string sql = hasUpdatedAtUtc
            ? """
              INSERT INTO Settings (Key, Value, UpdatedAtUtc)
              VALUES ($key, $value, $updatedAtUtc)
              ON CONFLICT(Key) DO UPDATE SET
                  Value = excluded.Value,
                  UpdatedAtUtc = excluded.UpdatedAtUtc;
              """
            : """
              INSERT INTO Settings (Key, Value)
              VALUES ($key, $value)
              ON CONFLICT(Key) DO UPDATE SET
                  Value = excluded.Value;
              """;

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", (object?)value ?? DBNull.Value);
        if (hasUpdatedAtUtc)
        {
            command.Parameters.AddWithValue("$updatedAtUtc", DateTime.UtcNow.ToString("O"));
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT Key, Value FROM Settings;";
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

        await using SqliteConnection connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            string key = reader.GetString(0);
            string value = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            values[key] = value;
        }

        return values;
    }

    private async Task<bool> HasUpdatedAtUtcColumnAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        if (_hasUpdatedAtUtcColumn.HasValue)
        {
            return _hasUpdatedAtUtcColumn.Value;
        }

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(Settings);";

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            string columnName = reader.GetString(1);
            if (columnName.Equals("UpdatedAtUtc", StringComparison.OrdinalIgnoreCase))
            {
                _hasUpdatedAtUtcColumn = true;
                return true;
            }
        }

        _hasUpdatedAtUtcColumn = false;
        return false;
    }
}
