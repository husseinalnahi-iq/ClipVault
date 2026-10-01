using ClipVault.Core.Abstractions.Repositories;
using ClipVault.Core.Abstractions.Security;
using System.Security.Cryptography;
using System.Text.Json;

namespace ClipVault.Infrastructure.Security;

public sealed class PinSecurityService : IPinSecurityService
{
    private const string PinKey = "security.pin.hash";
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 210_000;

    private readonly ISettingsRepository _settingsRepository;

    public PinSecurityService(ISettingsRepository settingsRepository)
    {
        _settingsRepository = settingsRepository;
    }

    public async Task<bool> HasPinAsync(CancellationToken cancellationToken = default)
    {
        string? value = await _settingsRepository.GetValueAsync(PinKey, cancellationToken);
        return !string.IsNullOrWhiteSpace(value);
    }

    public async Task SetPinAsync(string pin, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pin))
        {
            throw new ArgumentException("PIN cannot be empty.", nameof(pin));
        }

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

        StoredPinHash payload = new()
        {
            Salt = Convert.ToBase64String(salt),
            Hash = Convert.ToBase64String(hash),
            Iterations = Iterations,
            HashSizeBytes = HashSize
        };

        await _settingsRepository.SetValueAsync(PinKey, JsonSerializer.Serialize(payload), cancellationToken);
    }

    public async Task<bool> VerifyPinAsync(string pin, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pin))
        {
            return false;
        }

        string? raw = await _settingsRepository.GetValueAsync(PinKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        StoredPinHash? stored = JsonSerializer.Deserialize<StoredPinHash>(raw);
        if (stored is null)
        {
            return false;
        }

        byte[] salt = Convert.FromBase64String(stored.Salt);
        byte[] expectedHash = Convert.FromBase64String(stored.Hash);
        byte[] candidateHash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, stored.Iterations, HashAlgorithmName.SHA256, stored.HashSizeBytes);

        return CryptographicOperations.FixedTimeEquals(expectedHash, candidateHash);
    }

    private sealed class StoredPinHash
    {
        public string Salt { get; init; } = string.Empty;
        public string Hash { get; init; } = string.Empty;
        public int Iterations { get; init; }
        public int HashSizeBytes { get; init; }
    }
}
