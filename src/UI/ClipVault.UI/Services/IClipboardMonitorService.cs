using System.Windows;

namespace ClipVault.UI.Services;

public interface IClipboardMonitorService
{
    event EventHandler<ClipboardCapturedEventArgs>? ClipboardCaptured;

    void Start(Window window);
    void Stop();
}
