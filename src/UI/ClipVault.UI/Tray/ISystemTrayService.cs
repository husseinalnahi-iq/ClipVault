namespace ClipVault.UI.Tray;

public interface ISystemTrayService : IDisposable
{
    void Initialize(Action onOpen, Action onOpenSettings, Action onExit);
}
