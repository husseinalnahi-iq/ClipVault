using ClipVault.Core.Abstractions.Repositories;
using ClipVault.Core.Abstractions.Security;
using ClipVault.Core.Domain.Entities;
using ClipVault.Core.Domain.Enums;
using ClipVault.Infrastructure.Persistence;
using ClipVault.UI.Services;
using ClipVault.UI.ViewModels;
using System.Windows;

List<(string Name, Func<Task> Test)> tests =
[
    ("Protected items load past retention limit", ProtectedItemsLoadPastRetentionLimitAsync),
    ("Retention prunes only ordinary items", RetentionPrunesOnlyOrdinaryItemsAsync),
    ("Removed favorite returns to Recent", RemovedFavoriteReturnsToRecentAsync),
    ("Removed category returns to Recent", RemovedCategoryReturnsToRecentAsync)
];

List<string> failures = [];
foreach ((string name, Func<Task> test) in tests)
{
    try
    {
        await test();
        Console.WriteLine($"PASS: {name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{name}: {ex.Message}");
        Console.WriteLine($"FAIL: {name} - {ex.Message}");
    }
}

if (failures.Count > 0)
{
    throw new InvalidOperationException(string.Join(Environment.NewLine, failures));
}

Console.WriteLine("Retention protection QA completed successfully.");

static async Task ProtectedItemsLoadPastRetentionLimitAsync()
{
    List<ClipboardItem> items = [];
    DateTime now = DateTime.UtcNow;
    for (int i = 0; i < 100; i++)
    {
        items.Add(Item($"ordinary-{i}", now.AddMinutes(-i)));
    }

    items.Add(Item("saved-favorite", now.AddDays(-2), isFavorite: true));
    items.Add(Item("saved-category", now.AddDays(-3), categoryId: "important"));

    InMemoryClipboardRepository repository = new(items);
    MainWindowViewModel viewModel = CreateViewModel(repository);

    await viewModel.InitializeAsync();

    Assert(viewModel.AllItems.Any(item => item.Id == "saved-favorite"), "An older favorite must remain loaded.");
    Assert(viewModel.AllItems.Any(item => item.Id == "saved-category"), "An older categorized item must remain loaded.");
}

static async Task RetentionPrunesOnlyOrdinaryItemsAsync()
{
    InMemoryClipboardRepository repository = new([]);
    MainWindowViewModel viewModel = CreateViewModel(repository);
    DateTime now = DateTime.UtcNow;

    for (int i = 0; i < 101; i++)
    {
        viewModel.AllItems.Add(ClipboardItemViewModel.FromEntity(Item($"ordinary-{i}", now.AddMinutes(-i))));
    }

    viewModel.AllItems.Add(ClipboardItemViewModel.FromEntity(Item("protected-category", now.AddDays(-2), categoryId: "important")));
    await viewModel.ChangeRetentionLimitCommand.ExecuteAsync("100");

    Assert(viewModel.AllItems.Any(item => item.Id == "protected-category"), "Retention must never prune categorized items.");
    Assert(!repository.DeletedIds.Contains("protected-category"), "Retention must never delete categorized items from storage.");
    Assert(viewModel.AllItems.Count(item => !item.IsFavorite && string.IsNullOrWhiteSpace(item.CategoryId)) == 100,
        "Retention should keep its ordinary-history limit.");
}

static async Task RemovedFavoriteReturnsToRecentAsync()
{
    InMemoryClipboardRepository repository = new([]);
    MainWindowViewModel viewModel = CreateViewModel(repository);
    DateTime now = DateTime.UtcNow;

    for (int i = 0; i < 15; i++)
    {
        viewModel.AllItems.Add(ClipboardItemViewModel.FromEntity(Item($"recent-{i}", now.AddMinutes(-i))));
    }

    ClipboardItemViewModel saved = ClipboardItemViewModel.FromEntity(Item("favorite-to-remove", now.AddDays(-4), isFavorite: true));
    viewModel.AllItems.Add(saved);
    viewModel.ActiveTab = NavigationTab.Recent;

    await viewModel.ToggleFavoriteCommand.ExecuteAsync(saved);

    Assert(!saved.IsFavorite, "The favorite flag should be removed.");
    Assert(viewModel.VisibleItems.Any(item => item.Id == saved.Id), "Removing the final protection must return the item to Recent.");
}

static async Task RemovedCategoryReturnsToRecentAsync()
{
    InMemoryClipboardRepository repository = new([]);
    MainWindowViewModel viewModel = CreateViewModel(repository);
    DateTime now = DateTime.UtcNow;

    for (int i = 0; i < 15; i++)
    {
        viewModel.AllItems.Add(ClipboardItemViewModel.FromEntity(Item($"recent-{i}", now.AddMinutes(-i))));
    }

    ClipboardItemViewModel saved = ClipboardItemViewModel.FromEntity(Item("category-to-remove", now.AddDays(-4), categoryId: "important"));
    viewModel.AllItems.Add(saved);
    viewModel.OpenEditItemModalCommand.Execute(saved);
    viewModel.EditItemCategoryId = null;

    await viewModel.UpdateItemMetadataCommand.ExecuteAsync(null);

    Assert(string.IsNullOrWhiteSpace(saved.CategoryId), "The category should be removed.");
    Assert(viewModel.VisibleItems.Any(item => item.Id == saved.Id), "Removing the final category protection must return the item to Recent.");
}

static ClipboardItem Item(string id, DateTime createdAtUtc, bool isFavorite = false, string? categoryId = null)
{
    return new ClipboardItem
    {
        Id = id,
        CreatedAtUtc = createdAtUtc,
        Type = ClipboardItemType.Text,
        TextContent = id,
        IsFavorite = isFavorite,
        CategoryId = categoryId
    };
}

static MainWindowViewModel CreateViewModel(InMemoryClipboardRepository repository)
{
    return new MainWindowViewModel(
        repository,
        new InMemoryCategoryRepository(),
        new InMemorySettingsRepository(),
        new UnusedEncryptionService(),
        new AcceptingPinService(),
        new QuietClipboardMonitorService(),
        new QuietAutoPasteService(),
        new QuietStartupRegistrationService(),
        new QuietGlobalHotkeyService(),
        new AppDataPaths());
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

sealed class InMemoryClipboardRepository(IEnumerable<ClipboardItem> items) : IClipboardItemRepository
{
    private readonly List<ClipboardItem> _items = [.. items];

    public HashSet<string> DeletedIds { get; } = [];

    public Task<IReadOnlyList<ClipboardItem>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ClipboardItem>>(_items.OrderByDescending(item => item.CreatedAtUtc).Take(limit).ToList());

    public Task<IReadOnlyList<ClipboardItem>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ClipboardItem>>(_items.OrderByDescending(item => item.CreatedAtUtc).ToList());

    public Task<IReadOnlyList<ClipboardItem>> GetFavoritesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ClipboardItem>>(_items.Where(item => item.IsFavorite).OrderByDescending(item => item.CreatedAtUtc).ToList());

    public Task UpsertAsync(ClipboardItem item, CancellationToken cancellationToken = default)
    {
        _items.RemoveAll(existing => existing.Id == item.Id);
        _items.Add(item);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        DeletedIds.Add(id);
        _items.RemoveAll(item => item.Id == id);
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _items.Clear();
        return Task.CompletedTask;
    }
}

sealed class InMemoryCategoryRepository : ICategoryRepository
{
    private readonly Category _category = new() { Id = "important", Name = "Important" };

    public Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Category>>([_category]);

    public Task<Category> CreateAsync(string name, string? color = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new Category { Id = Guid.NewGuid().ToString("N"), Name = name, Color = color });

    public Task RenameAsync(string id, string newName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(false);
}

sealed class InMemorySettingsRepository : ISettingsRepository
{
    private readonly Dictionary<string, string> _values = new()
    {
        ["general.run_at_startup"] = "false",
        ["capture.retention_limit"] = "100",
        ["capture.recent_items_limit"] = "15"
    };

    public Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_values.TryGetValue(key, out string? value) ? value : null);

    public Task SetValueAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        if (value is null)
        {
            _values.Remove(key);
        }
        else
        {
            _values[key] = value;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyDictionary<string, string>>(_values);
}

sealed class UnusedEncryptionService : IVaultEncryptionService
{
    public EncryptionResult Encrypt(byte[] plaintext, string pin) => throw new NotSupportedException();
    public byte[] Decrypt(byte[] encryptedPayload, string encryptionMeta, string pin) => throw new NotSupportedException();
}

sealed class AcceptingPinService : IPinSecurityService
{
    public Task<bool> HasPinAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task SetPinAsync(string pin, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<bool> VerifyPinAsync(string pin, CancellationToken cancellationToken = default) => Task.FromResult(true);
}

sealed class QuietClipboardMonitorService : IClipboardMonitorService
{
    public event EventHandler<ClipboardCapturedEventArgs>? ClipboardCaptured
    {
        add { }
        remove { }
    }

    public void Start(Window window) { }
    public void Stop() { }
}

sealed class QuietAutoPasteService : IAutoPasteService
{
    public IntPtr CaptureForegroundWindow() => IntPtr.Zero;
    public Task PasteToWindowAsync(IntPtr handle, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

sealed class QuietStartupRegistrationService : IStartupRegistrationService
{
    public bool IsEnabled() => false;
    public Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

sealed class QuietGlobalHotkeyService : IGlobalHotkeyService
{
    public event EventHandler? HotkeyPressed
    {
        add { }
        remove { }
    }

    public uint CurrentModifiers { get; private set; }
    public uint CurrentVk { get; private set; }

    public void Configure(uint modifiers, uint vk)
    {
        CurrentModifiers = modifiers;
        CurrentVk = vk;
    }

    public void Register(Window window) { }
    public void Unregister() { }
    public void Reregister(uint modifiers, uint vk) => Configure(modifiers, vk);
}
