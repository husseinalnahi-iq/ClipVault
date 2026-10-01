using ClipVault.Infrastructure;
using ClipVault.Infrastructure.Persistence;
using ClipVault.UI.Tray;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClipVault.UI;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private ISystemTrayService? _trayService;

    public bool IsExitRequested { get; private set; }

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddClipVaultInfrastructure();
                services.AddClipVaultUiServices();
            })
            .Build();

        await _host.StartAsync();

        SqliteDatabaseInitializer initializer = _host.Services.GetRequiredService<SqliteDatabaseInitializer>();
        await initializer.InitializeAsync();

        MainWindow mainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;

        _trayService = _host.Services.GetRequiredService<ISystemTrayService>();
        _trayService.Initialize(
            onOpen: () => OpenMainWindow(false),
            onOpenSettings: () => OpenMainWindow(true),
            onExit: ExitApplication);

        mainWindow.Show();
    }

    public void ExitApplication()
    {
        if (IsExitRequested)
        {
            return;
        }

        IsExitRequested = true;
        Shutdown();
    }

    private void OpenMainWindow(bool openSettings)
    {
        if (MainWindow is MainWindow window)
        {
            window.ShowFromBackground(openSettings);
        }
    }

    protected override async void OnExit(System.Windows.ExitEventArgs e)
    {
        _trayService?.Dispose();

        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(2));
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
