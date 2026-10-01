namespace ClipVault.Core.Abstractions.Repositories;

public interface ISettingsRepository
{
    Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default);
    Task SetValueAsync(string key, string? value, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken = default);
}
