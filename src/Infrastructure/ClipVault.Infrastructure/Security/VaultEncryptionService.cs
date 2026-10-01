using ClipVault.Core.Abstractions.Security;
using ClipVault.Core.Domain.Models;
using System.Security.Cryptography;
using System.Text.Json;

namespace ClipVault.Infrastructure.Security;

public sealed class VaultEncryptionService : IVaultEncryptionService
{
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 210_000;

    public EncryptionResult Encrypt(byte[] plaintext, string pin)
    {
        if (string.IsNullOrWhiteSpace(pin))
        {
            throw new ArgumentException("PIN cannot be empty.", nameof(pin));
        }

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
        byte[] key = DeriveKey(pin, salt, Iterations, KeySize);
        byte[] cipher = new byte[plaintext.Length];
        byte[] tag = new byte[TagSize];

        try
        {
            using AesGcm aes = new(key, TagSize);
            aes.Encrypt(nonce, plaintext, cipher, tag);

            byte[] payload = new byte[cipher.Length + tag.Length];
            Buffer.BlockCopy(cipher, 0, payload, 0, cipher.Length);
            Buffer.BlockCopy(tag, 0, payload, cipher.Length, tag.Length);

            VaultCryptoMetadata metadata = new()
            {
                Salt = Convert.ToBase64String(salt),
                Nonce = Convert.ToBase64String(nonce),
                Iterations = Iterations,
                KeySizeBytes = KeySize
            };

            return new EncryptionResult(payload, JsonSerializer.Serialize(metadata));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public byte[] Decrypt(byte[] encryptedPayload, string encryptionMeta, string pin)
    {
        if (string.IsNullOrWhiteSpace(pin))
        {
            throw new ArgumentException("PIN cannot be empty.", nameof(pin));
        }

        VaultCryptoMetadata? metadata = JsonSerializer.Deserialize<VaultCryptoMetadata>(encryptionMeta);
        if (metadata is null)
        {
            throw new InvalidOperationException("Invalid encryption metadata.");
        }

        byte[] salt = Convert.FromBase64String(metadata.Salt);
        byte[] nonce = Convert.FromBase64String(metadata.Nonce);
        byte[] key = DeriveKey(pin, salt, metadata.Iterations, metadata.KeySizeBytes);

        try
        {
            if (encryptedPayload.Length < TagSize)
            {
                throw new InvalidOperationException("Encrypted payload is invalid.");
            }

            int cipherLength = encryptedPayload.Length - TagSize;
            byte[] cipher = new byte[cipherLength];
            byte[] tag = new byte[TagSize];
            Buffer.BlockCopy(encryptedPayload, 0, cipher, 0, cipherLength);
            Buffer.BlockCopy(encryptedPayload, cipherLength, tag, 0, TagSize);

            byte[] plaintext = new byte[cipherLength];
            using AesGcm aes = new(key, TagSize);
            aes.Decrypt(nonce, cipher, tag, plaintext);

            return plaintext;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static byte[] DeriveKey(string pin, byte[] salt, int iterations, int keySize)
    {
        return Rfc2898DeriveBytes.Pbkdf2(pin, salt, iterations, HashAlgorithmName.SHA256, keySize);
    }
}
