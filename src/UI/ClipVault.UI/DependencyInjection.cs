using ClipVault.UI.Services;
using ClipVault.UI.Tray;
using ClipVault.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ClipVault.UI;

public static class DependencyInjection
{
    public static IServiceCollection AddClipVaultUiServices(this IServiceCollection services)
    {
        services.AddSingleton<IClipboardMonitorService, ClipboardMonitorService>();
        services.AddSingleton<IGlobalHotkeyService, GlobalHotkeyService>();
        services.AddSingleton<IAutoPasteService, AutoPasteService>();
        services.AddSingleton<IStartupRegistrationService, StartupRegistrationService>();
        services.AddSingleton<ISystemTrayService, SystemTrayService>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();

        return services;
    }
}
