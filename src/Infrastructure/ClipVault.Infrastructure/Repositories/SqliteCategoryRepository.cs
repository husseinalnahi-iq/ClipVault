using ClipVault.Core.Abstractions.Repositories;
using ClipVault.Core.Domain.Entities;
using ClipVault.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace ClipVault.Infrastructure.Repositories;

public sealed class SqliteCategoryRepository : ICategoryRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteCategoryRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT Id, Name, Color, SortOrder FROM Categories ORDER BY SortOrder, Name;";
        List<Category> categories = [];

        await using SqliteConnection connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            categories.Add(new Category
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                Color = reader.IsDBNull(2) ? null : reader.GetString(2),
                SortOrder = reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
            });
        }

        return categories;
    }

    public async Task<Category> CreateAsync(string name, string? color = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Category name is required.", nameof(name));
        }

        string trimmed = name.Trim();
        if (await ExistsByNameAsync(trimmed, cancellationToken))
        {
            throw new InvalidOperationException("Category already exists.");
        }

        string id = Guid.NewGuid().ToString("N");
        int sortOrder = await GetNextSortOrderAsync(cancellationToken);

        const string sql = "INSERT INTO Categories (Id, Name, Color, SortOrder) VALUES ($id, $name, $color, $sortOrder);";

        await using SqliteConnection connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$name", trimmed);
        command.Parameters.AddWithValue("$color", (object?)color ?? DBNull.Value);
        command.Parameters.AddWithValue("$sortOrder", sortOrder);
        await command.ExecuteNonQueryAsync(cancellationToken);

        return new Category { Id = id, Name = trimmed, Color = color, SortOrder = sortOrder };
    }

    public async Task RenameAsync(string id, string newName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        string trimmed = newName.Trim();

        const string sql = "UPDATE Categories SET Name = $name WHERE Id = $id;";

        await using SqliteConnection connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$name", trimmed);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        await using SqliteConnection connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using SqliteTransaction tx = connection.BeginTransaction();

        await using (SqliteCommand clearCategory = connection.CreateCommand())
        {
            clearCategory.Transaction = tx;
            clearCategory.CommandText = "UPDATE ClipboardItems SET CategoryId = NULL WHERE CategoryId = $id;";
            clearCategory.Parameters.AddWithValue("$id", id);
            await clearCategory.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (SqliteCommand deleteCategory = connection.CreateCommand())
        {
            deleteCategory.Transaction = tx;
            deleteCategory.CommandText = "DELETE FROM Categories WHERE Id = $id;";
            deleteCategory.Parameters.AddWithValue("$id", id);
            await deleteCategory.ExecuteNonQueryAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        const string sql = "SELECT 1 FROM Categories WHERE Name = $name COLLATE NOCASE LIMIT 1;";

        await using SqliteConnection connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$name", name.Trim());

        object? value = await command.ExecuteScalarAsync(cancellationToken);
        return value is not null;
    }

    private async Task<int> GetNextSortOrderAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT COALESCE(MAX(SortOrder), 0) + 1 FROM Categories;";

        await using SqliteConnection connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;

        object? value = await command.ExecuteScalarAsync(cancellationToken);
        return value is int i ? i : Convert.ToInt32(value ?? 1);
    }
}
