using Microsoft.Win32;
using System.Diagnostics;

namespace ClipVault.UI.Services;

public sealed class StartupRegistrationService : IStartupRegistrationService
{
    private const string RunKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string AppName = "ClipVault";

    public bool IsEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        string? value = key?.GetValue(AppName) as string;
        return !string.IsNullOrWhiteSpace(value);
    }

    public Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (key is null)
        {
            return Task.CompletedTask;
        }

        if (enabled)
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(exePath))
            {
                key.SetValue(AppName, $"\"{exePath}\"");
            }
        }
        else
        {
            key.DeleteValue(AppName, throwOnMissingValue: false);
        }

        return Task.CompletedTask;
    }
}
