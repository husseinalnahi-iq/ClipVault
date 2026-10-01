namespace ClipVault.Core.Domain.Models;

public sealed class VaultCryptoMetadata
{
    public int Version { get; init; } = 1;
    public string Salt { get; init; } = string.Empty;
    public string Nonce { get; init; } = string.Empty;
    public int Iterations { get; init; }
    public int KeySizeBytes { get; init; }
}
