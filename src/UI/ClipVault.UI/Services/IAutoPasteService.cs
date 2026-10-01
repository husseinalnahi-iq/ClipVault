namespace ClipVault.UI.Services;

public interface IAutoPasteService
{
    IntPtr CaptureForegroundWindow();
    Task PasteToWindowAsync(IntPtr handle, CancellationToken cancellationToken = default);
}
