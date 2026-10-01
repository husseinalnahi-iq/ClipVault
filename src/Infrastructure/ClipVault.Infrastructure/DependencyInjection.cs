using ClipVault.Core.Abstractions.Repositories;
using ClipVault.Core.Abstractions.Security;
using ClipVault.Infrastructure.Persistence;
using ClipVault.Infrastructure.Repositories;
using ClipVault.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;

namespace ClipVault.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddClipVaultInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<AppDataPaths>();
        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<SqliteDatabaseInitializer>();
        services.AddSingleton<IClipboardItemRepository, SqliteClipboardItemRepository>();
        services.AddSingleton<ICategoryRepository, SqliteCategoryRepository>();
        services.AddSingleton<ISettingsRepository, SqliteSettingsRepository>();
        services.AddSingleton<IVaultEncryptionService, VaultEncryptionService>();
        services.AddSingleton<IPinSecurityService, PinSecurityService>();

        return services;
    }
}
