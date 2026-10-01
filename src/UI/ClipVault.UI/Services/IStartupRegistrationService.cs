namespace ClipVault.UI.Services;

public interface IStartupRegistrationService
{
    bool IsEnabled();
    Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default);
}
