namespace ClipVault.Core.Abstractions.Security;

public interface IPinSecurityService
{
    Task<bool> HasPinAsync(CancellationToken cancellationToken = default);
    Task SetPinAsync(string pin, CancellationToken cancellationToken = default);
    Task<bool> VerifyPinAsync(string pin, CancellationToken cancellationToken = default);
}
