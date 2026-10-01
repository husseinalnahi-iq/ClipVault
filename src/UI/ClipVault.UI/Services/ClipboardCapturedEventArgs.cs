using ClipVault.Core.Domain.Enums;

namespace ClipVault.UI.Services;

public sealed class ClipboardCapturedEventArgs : EventArgs
{
    public ClipboardCapturedEventArgs(ClipboardItemType type, string? textContent, string? imagePath)
    {
        Type = type;
        TextContent = textContent;
        ImagePath = imagePath;
    }

    public ClipboardItemType Type { get; }
    public string? TextContent { get; }
    public string? ImagePath { get; }
}
