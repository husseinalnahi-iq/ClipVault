using ClipVault.Core.Domain.Entities;
using ClipVault.Core.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClipVault.UI.ViewModels;

public partial class ClipboardItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private DateTime _createdAtUtc;

    [ObservableProperty]
    private ClipboardItemType _type;

    [ObservableProperty]
    private string? _title;

    [ObservableProperty]
    private string? _textContent;

    [ObservableProperty]
    private string? _imagePath;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private string? _categoryId;

    [ObservableProperty]
    private string _categoryDisplayName = "Uncategorized";

    [ObservableProperty]
    private bool _isLocked;

    [ObservableProperty]
    private byte[]? _encryptedPayload;

    [ObservableProperty]
    private string? _encryptionMeta;

    public string RelativeTime => BuildRelativeTime(CreatedAtUtc);
    public string? PreviewText => IsLocked ? "************" : TextContent;
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title)
        ? (Type == ClipboardItemType.Image ? "Image item" : "Untitled text")
        : Title!;
    public bool HasTitle => !string.IsNullOrWhiteSpace(Title);

    public static ClipboardItemViewModel FromEntity(ClipboardItem item)
    {
        return new ClipboardItemViewModel
        {
            Id = item.Id,
            CreatedAtUtc = item.CreatedAtUtc,
            Type = item.Type,
            Title = item.Title,
            TextContent = item.TextContent,
            ImagePath = item.ImagePath,
            IsFavorite = item.IsFavorite,
            CategoryId = item.CategoryId,
            IsLocked = item.IsLocked,
            EncryptedPayload = item.EncryptedPayload,
            EncryptionMeta = item.EncryptionMeta
        };
    }

    public ClipboardItem ToEntity()
    {
        return new ClipboardItem
        {
            Id = Id,
            CreatedAtUtc = CreatedAtUtc,
            Type = Type,
            Title = Title,
            TextContent = TextContent,
            ImagePath = ImagePath,
            IsFavorite = IsFavorite,
            CategoryId = CategoryId,
            IsLocked = IsLocked,
            EncryptedPayload = EncryptedPayload,
            EncryptionMeta = EncryptionMeta
        };
    }

    partial void OnCreatedAtUtcChanged(DateTime value) => OnPropertyChanged(nameof(RelativeTime));
    partial void OnTextContentChanged(string? value) => OnPropertyChanged(nameof(PreviewText));
    partial void OnIsLockedChanged(bool value) => OnPropertyChanged(nameof(PreviewText));

    partial void OnTitleChanged(string? value)
    {
        OnPropertyChanged(nameof(DisplayTitle));
        OnPropertyChanged(nameof(HasTitle));
    }

    private static string BuildRelativeTime(DateTime createdAtUtc)
    {
        TimeSpan delta = DateTime.UtcNow - createdAtUtc;

        if (delta < TimeSpan.FromMinutes(1))
        {
            return "Just now";
        }

        if (delta < TimeSpan.FromHours(1))
        {
            return $"{Math.Max(1, (int)delta.TotalMinutes)} mins ago";
        }

        if (delta < TimeSpan.FromDays(1))
        {
            return $"{Math.Max(1, (int)delta.TotalHours)} hours ago";
        }

        if (delta < TimeSpan.FromDays(2))
        {
            return "Yesterday";
        }

        return createdAtUtc.ToLocalTime().ToString("MMM d, yyyy");
    }
}
