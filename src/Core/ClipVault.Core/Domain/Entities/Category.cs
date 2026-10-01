namespace ClipVault.Core.Domain.Entities;

public sealed class Category
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = string.Empty;
    public string? Color { get; init; }
    public int SortOrder { get; init; }
}
