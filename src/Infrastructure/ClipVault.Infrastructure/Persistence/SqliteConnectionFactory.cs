using Microsoft.Data.Sqlite;

namespace ClipVault.Infrastructure.Persistence;

public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(AppDataPaths paths)
    {
        paths.EnsureCreated();
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = paths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public SqliteConnection CreateConnection() => new(_connectionString);
}
