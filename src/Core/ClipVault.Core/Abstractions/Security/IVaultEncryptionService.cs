namespace ClipVault.Core.Abstractions.Security;

public interface IVaultEncryptionService
{
    EncryptionResult Encrypt(byte[] plaintext, string pin);
    byte[] Decrypt(byte[] encryptedPayload, string encryptionMeta, string pin);
}
