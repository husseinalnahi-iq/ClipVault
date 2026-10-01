namespace ClipVault.Core.Abstractions.Security;

public sealed record EncryptionResult(byte[] EncryptedPayload, string EncryptionMeta);
