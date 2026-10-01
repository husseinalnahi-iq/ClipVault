using ClipVault.Core.Domain.Entities;

namespace ClipVault.Core.Abstractions.Repositories;

public interface ICategoryRepository
{
    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Category> CreateAsync(string name, string? color = null, CancellationToken cancellationToken = default);
    Task RenameAsync(string id, string newName, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default);
}
