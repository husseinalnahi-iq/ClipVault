using ClipVault.Core.Domain.Entities;

namespace ClipVault.Core.Abstractions.Repositories;

public interface IClipboardItemRepository
{
    Task<IReadOnlyList<ClipboardItem>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClipboardItem>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClipboardItem>> GetFavoritesAsync(CancellationToken cancellationToken = default);
    Task UpsertAsync(ClipboardItem item, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
