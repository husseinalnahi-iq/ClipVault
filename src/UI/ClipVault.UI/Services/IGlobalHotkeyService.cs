using System.Windows;

namespace ClipVault.UI.Services;

public interface IGlobalHotkeyService
{
    event EventHandler? HotkeyPressed;

    uint CurrentModifiers { get; }
    uint CurrentVk { get; }

    void Configure(uint modifiers, uint vk);
    void Register(Window window);
    void Unregister();
    void Reregister(uint modifiers, uint vk);
}
