using ClipVault.Core.Domain.Enums;

namespace ClipVault.Core.Domain.Entities;

public sealed class ClipboardItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public ClipboardItemType Type { get; init; }
    public string? Title { get; init; }
    public string? TextContent { get; init; }
    public string? ImagePath { get; init; }
    public bool IsFavorite { get; init; }
    public string? CategoryId { get; init; }
    public bool IsLocked { get; init; }
    public byte[]? EncryptedPayload { get; init; }
    public string? EncryptionMeta { get; init; }
}
