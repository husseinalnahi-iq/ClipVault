using ClipVault.Infrastructure.Persistence;
using ClipVault.Infrastructure.Repositories;
using ClipVault.Infrastructure.Security;
using ClipVault.UI.Services;

AppDataPaths paths = new();
SqliteConnectionFactory factory = new(paths);
SqliteSettingsRepository settingsRepository = new(factory);
SqliteClipboardItemRepository clipboardRepository = new(factory);
PinSecurityService pinSecurityService = new(settingsRepository);
StartupRegistrationService startupRegistrationService = new();

List<string> steps = [];

await settingsRepository.SetValueAsync("general.run_at_startup", "true");
await startupRegistrationService.SetEnabledAsync(true);
steps.Add("Run at startup = true");

await settingsRepository.SetValueAsync("general.run_at_startup", "false");
await startupRegistrationService.SetEnabledAsync(false);
steps.Add("Run at startup = false");

await settingsRepository.SetValueAsync("capture.ignore_duplicates", "true");
await settingsRepository.SetValueAsync("capture.ignore_duplicates", "false");
steps.Add("Ignore duplicates toggled");

foreach (string retention in new[] { "100", "500", "1000" })
{
    await settingsRepository.SetValueAsync("capture.retention_limit", retention);
}
steps.Add("Retention values applied");

foreach (string timeout in new[] { "5", "15", "30", "never" })
{
    await settingsRepository.SetValueAsync("vault.auto_lock_timeout", timeout);
}
steps.Add("Auto-lock timeout values applied");

await pinSecurityService.SetPinAsync("1234");
bool pinOk = await pinSecurityService.VerifyPinAsync("1234");
bool pinBad = await pinSecurityService.VerifyPinAsync("9999");
if (!pinOk || pinBad)
{
    throw new InvalidOperationException("PIN verification produced unexpected result.");
}
steps.Add("PIN set and verified");

await clipboardRepository.ClearAsync();
steps.Add("Clear history executed");

Console.WriteLine("Settings QA integration run completed successfully.");
foreach (string step in steps)
{
    Console.WriteLine($" - {step}");
}
