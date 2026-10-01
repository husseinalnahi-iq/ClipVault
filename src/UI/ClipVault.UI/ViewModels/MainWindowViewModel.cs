using ClipVault.Core.Abstractions.Repositories;
using ClipVault.Core.Abstractions.Security;
using ClipVault.Core.Domain.Entities;
using ClipVault.Core.Domain.Enums;
using ClipVault.Infrastructure.Persistence;
using ClipVault.UI.Services;
using ClipVault.UI.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ClipVault.UI.ViewModels;

public enum ClearHistoryScope
{
    RecentVisible,
    Favorites,
    All
}

public partial class MainWindowViewModel : ObservableObject
{
    private const int DefaultRecentItemsLimit = 15;

    private enum VaultOperation
    {
        None,
        Lock,
        Unlock
    }

    private readonly IClipboardItemRepository _clipboardRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IVaultEncryptionService _vaultEncryptionService;
    private readonly IPinSecurityService _pinSecurityService;
    private readonly IClipboardMonitorService _clipboardMonitorService;
    private readonly IAutoPasteService _autoPasteService;
    private readonly IStartupRegistrationService _startupRegistrationService;
    private readonly IGlobalHotkeyService _globalHotkeyService;
    private readonly AppDataPaths _paths;

    private readonly SemaphoreSlim _gate = new(1, 1);

    private VaultOperation _pendingVaultOperation = VaultOperation.None;
    private string? _pendingVaultItemId;
    private IntPtr _previousForegroundWindow;
    private bool _suppressSettingWrites;
    private uint _currentHotkeyModifiers = NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT;
    private uint _currentHotkeyVk = NativeMethods.VK_V;
    private uint _recordedModifiers;
    private uint _recordedVk;

    [ObservableProperty]
    private NavigationTab _activeTab = NavigationTab.Recent;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _showSettings;

    [ObservableProperty]
    private ClipboardItemViewModel? _selectedItem;

    [ObservableProperty]
    private bool _isAddModalOpen;

    [ObservableProperty]
    private string _newItemContent = string.Empty;

    [ObservableProperty]
    private string? _newItemTitle;

    [ObservableProperty]
    private string? _newItemCategoryId;

    [ObservableProperty]
    private bool _newItemIsLocked;

    [ObservableProperty]
    private bool _newItemIsFavorite;

    [ObservableProperty]
    private bool _isUnlockModalOpen;

    [ObservableProperty]
    private string _unlockPin = string.Empty;

    [ObservableProperty]
    private string _unlockError = string.Empty;

    [ObservableProperty]
    private bool _unlockSuccess;

    [ObservableProperty]
    private string _unlockModalTitle = "Unlock Vault Item";

    [ObservableProperty]
    private bool _runAtStartup = true;

    [ObservableProperty]
    private bool _ignoreDuplicates = true;

    [ObservableProperty]
    private string _recentItemsLimit = "15";

    [ObservableProperty]
    private string _autoLockTimeout = "15";

    [ObservableProperty]
    private string _retentionLimit = "500";

    [ObservableProperty]
    private string _pinSetup = string.Empty;

    [ObservableProperty]
    private string _pinConfirm = string.Empty;

    [ObservableProperty]
    private string _pinStatus = string.Empty;

    [ObservableProperty]
    private bool _isEditItemModalOpen;

    [ObservableProperty]
    private ClipboardItemViewModel? _editingItem;

    [ObservableProperty]
    private string? _editItemTitle;

    [ObservableProperty]
    private string? _editItemCategoryId;

    [ObservableProperty]
    private string _newCategoryName = string.Empty;

    [ObservableProperty]
    private string _renameCategoryName = string.Empty;

    [ObservableProperty]
    private CategoryItemViewModel? _selectedCategory;

    [ObservableProperty]
    private string _categoryStatusMessage = string.Empty;

    [ObservableProperty]
    private bool _isCategoryManagerOpen;

    [ObservableProperty]
    private bool _isClearHistoryModalOpen;

    [ObservableProperty]
    private ClearHistoryScope _selectedClearScope = ClearHistoryScope.RecentVisible;

    [ObservableProperty]
    private string _clearHistoryMessage = string.Empty;

    [ObservableProperty]
    private string _globalHotkeyDisplay = "Ctrl + Shift + V";

    [ObservableProperty]
    private bool _isHotkeyModalOpen;

    [ObservableProperty]
    private string _recordedHotkeyDisplay = string.Empty;

    public bool CanConfirmHotkey => _recordedVk != 0;

    public MainWindowViewModel(
        IClipboardItemRepository clipboardRepository,
        ICategoryRepository categoryRepository,
        ISettingsRepository settingsRepository,
        IVaultEncryptionService vaultEncryptionService,
        IPinSecurityService pinSecurityService,
        IClipboardMonitorService clipboardMonitorService,
        IAutoPasteService autoPasteService,
        IStartupRegistrationService startupRegistrationService,
        IGlobalHotkeyService globalHotkeyService,
        AppDataPaths paths)
    {
        _clipboardRepository = clipboardRepository;
        _categoryRepository = categoryRepository;
        _settingsRepository = settingsRepository;
        _vaultEncryptionService = vaultEncryptionService;
        _pinSecurityService = pinSecurityService;
        _clipboardMonitorService = clipboardMonitorService;
        _autoPasteService = autoPasteService;
        _startupRegistrationService = startupRegistrationService;
        _globalHotkeyService = globalHotkeyService;
        _paths = paths;

        _clipboardMonitorService.ClipboardCaptured += ClipboardMonitorServiceOnClipboardCaptured;
    }

    public ObservableCollection<ClipboardItemViewModel> AllItems { get; } = [];
    public ObservableCollection<ClipboardItemViewModel> VisibleItems { get; } = [];
    public ObservableCollection<CategoryGroupViewModel> CategoryGroups { get; } = [];
    public ObservableCollection<CategoryItemViewModel> Categories { get; } = [];
    public ObservableCollection<CategoryItemViewModel> CategoryChoices { get; } = [];

    public IReadOnlyList<string> RecentItemsLimitOptions { get; } = ["15", "30", "50", "100"];
    public IReadOnlyList<string> RetentionOptions { get; } = ["100", "500", "1000"];
    public IReadOnlyList<string> AutoLockTimeoutOptions { get; } = ["5", "15", "30", "never"];
    public bool IsCategoryTab => ActiveTab == NavigationTab.Categories;
    public bool IsFlatListTab => !IsCategoryTab;
    public bool HasVisibleItems => VisibleItems.Count > 0;

    public event Action? RequestHideWindow;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await LoadCategoriesAsync(cancellationToken);
        await LoadSettingsAsync(cancellationToken);
        await ReloadItemsAsync(cancellationToken);
    }

    public void SetPreviousForegroundWindow(IntPtr handle)
    {
        _previousForegroundWindow = handle;
    }

    private async void ClipboardMonitorServiceOnClipboardCaptured(object? sender, ClipboardCapturedEventArgs e)
    {
        await _gate.WaitAsync();
        try
        {
            await SaveCapturedItemAsync(e);
        }
        finally
        {
            _gate.Release();
        }
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        await ReloadItemsAsync();
    }

    [RelayCommand]
    private void SelectTab(NavigationTab tab)
    {
        ActiveTab = tab;
    }

    [RelayCommand]
    private void ToggleSettings()
    {
        ShowSettings = !ShowSettings;
    }

    [RelayCommand]
    private void OpenHotkeyModal()
    {
        _recordedModifiers = 0;
        _recordedVk = 0;
        RecordedHotkeyDisplay = string.Empty;
        OnPropertyChanged(nameof(CanConfirmHotkey));
        IsHotkeyModalOpen = true;
    }

    [RelayCommand]
    private async Task ConfirmHotkey()
    {
        if (_recordedVk == 0)
        {
            return;
        }

        _currentHotkeyModifiers = _recordedModifiers;
        _currentHotkeyVk = _recordedVk;
        GlobalHotkeyDisplay = HotkeyToDisplay(_currentHotkeyModifiers, _currentHotkeyVk);
        _globalHotkeyService.Reregister(_currentHotkeyModifiers, _currentHotkeyVk);
        await _settingsRepository.SetValueAsync(SettingKeys.GlobalHotkey, HotkeyToDbString(_currentHotkeyModifiers, _currentHotkeyVk));
        IsHotkeyModalOpen = false;
    }

    [RelayCommand]
    private void CancelHotkey()
    {
        _recordedModifiers = 0;
        _recordedVk = 0;
        RecordedHotkeyDisplay = string.Empty;
        IsHotkeyModalOpen = false;
    }

    public void UpdateRecordedHotkey(uint modifiers, uint vk)
    {
        _recordedModifiers = modifiers;
        _recordedVk = vk;
        RecordedHotkeyDisplay = HotkeyToDisplay(modifiers, vk);
        OnPropertyChanged(nameof(CanConfirmHotkey));
    }

    [RelayCommand]
    private void ToggleCategoryManager()
    {
        IsCategoryManagerOpen = !IsCategoryManagerOpen;
    }

    [RelayCommand]
    private void OpenAddModal()
    {
        IsAddModalOpen = true;
        if (SelectedItem is not null)
        {
            NewItemCategoryId = SelectedItem.CategoryId;
        }
    }

    [RelayCommand]
    private void CloseAddModal()
    {
        IsAddModalOpen = false;
        ResetNewItemDraft();
    }

    [RelayCommand]
    private async Task AddManualItemAsync()
    {
        if (string.IsNullOrWhiteSpace(NewItemContent))
        {
            return;
        }

        ClipboardItem item = new()
        {
            Id = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = DateTime.UtcNow,
            Type = ClipboardItemType.Text,
            Title = string.IsNullOrWhiteSpace(NewItemTitle) ? null : NewItemTitle.Trim(),
            TextContent = NewItemContent.Trim(),
            IsFavorite = NewItemIsFavorite,
            CategoryId = NewItemCategoryId,
            IsLocked = false
        };

        if (NewItemIsLocked)
        {
            if (!await _pinSecurityService.VerifyPinAsync(UnlockPin))
            {
                UnlockError = "Enter a valid PIN in the unlock modal before creating locked items.";
                IsUnlockModalOpen = true;
                UnlockModalTitle = "Authorize Vault Action";
                return;
            }

            byte[] plain = Encoding.UTF8.GetBytes(item.TextContent ?? string.Empty);
            var encrypted = _vaultEncryptionService.Encrypt(plain, UnlockPin);
            item = new ClipboardItem
            {
                Id = item.Id,
                CreatedAtUtc = item.CreatedAtUtc,
                Type = item.Type,
                IsLocked = true,
                IsFavorite = item.IsFavorite,
                CategoryId = item.CategoryId,
                Title = item.Title,
                TextContent = null,
                ImagePath = null,
                EncryptedPayload = encrypted.EncryptedPayload,
                EncryptionMeta = encrypted.EncryptionMeta
            };
        }

        await _clipboardRepository.UpsertAsync(item);
        AllItems.Insert(0, ClipboardItemViewModel.FromEntity(item));
        ApplyFilterAndGrouping();

        IsAddModalOpen = false;
        ResetNewItemDraft();
        await EnforceRetentionAsync();
    }

    [RelayCommand]
    private void OpenEditItemModal(ClipboardItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        EditingItem = item;
        EditItemTitle = item.Title;
        EditItemCategoryId = item.CategoryId;
        IsEditItemModalOpen = true;
    }

    [RelayCommand]
    private void CloseEditItemModal()
    {
        IsEditItemModalOpen = false;
        EditingItem = null;
        EditItemTitle = null;
        EditItemCategoryId = null;
    }

    [RelayCommand]
    private async Task UpdateItemMetadataAsync()
    {
        if (EditingItem is null)
        {
            return;
        }

        bool wasProtected = IsProtected(EditingItem);
        EditingItem.Title = string.IsNullOrWhiteSpace(EditItemTitle) ? null : EditItemTitle.Trim();
        EditingItem.CategoryId = string.IsNullOrWhiteSpace(EditItemCategoryId) ? null : EditItemCategoryId;
        ReturnToRecentIfProtectionRemoved(EditingItem, wasProtected);
        await _clipboardRepository.UpsertAsync(EditingItem.ToEntity());
        CloseEditItemModal();
        ApplyFilterAndGrouping();
    }

    [RelayCommand]
    private async Task AssignCategoryAsync(ClipboardItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        bool wasProtected = IsProtected(item);
        item.CategoryId = string.IsNullOrWhiteSpace(EditItemCategoryId) ? null : EditItemCategoryId;
        ReturnToRecentIfProtectionRemoved(item, wasProtected);
        await _clipboardRepository.UpsertAsync(item.ToEntity());
        ApplyFilterAndGrouping();
    }

    [RelayCommand]
    private async Task CreateCategoryAsync()
    {
        CategoryStatusMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(NewCategoryName))
        {
            CategoryStatusMessage = "Category name is required.";
            return;
        }

        if (await _categoryRepository.ExistsByNameAsync(NewCategoryName))
        {
            CategoryStatusMessage = "Category already exists.";
            return;
        }

        await _categoryRepository.CreateAsync(NewCategoryName.Trim());
        NewCategoryName = string.Empty;
        await LoadCategoriesAsync();
        ApplyFilterAndGrouping();
    }

    [RelayCommand]
    private async Task RenameSelectedCategoryAsync()
    {
        CategoryStatusMessage = string.Empty;
        if (SelectedCategory is null || string.IsNullOrWhiteSpace(RenameCategoryName))
        {
            return;
        }

        await _categoryRepository.RenameAsync(SelectedCategory.Id, RenameCategoryName.Trim());
        await LoadCategoriesAsync();
        ApplyFilterAndGrouping();
    }

    [RelayCommand]
    private async Task DeleteSelectedCategoryAsync()
    {
        CategoryStatusMessage = string.Empty;
        if (SelectedCategory is null)
        {
            return;
        }

        try
        {
            string deletedId = SelectedCategory.Id;
            await _categoryRepository.DeleteAsync(deletedId);

            foreach (ClipboardItemViewModel item in AllItems.Where(i => i.CategoryId == deletedId))
            {
                bool wasProtected = IsProtected(item);
                item.CategoryId = null;
                ReturnToRecentIfProtectionRemoved(item, wasProtected);
                await _clipboardRepository.UpsertAsync(item.ToEntity());
            }

            SelectedCategory = null;
            RenameCategoryName = string.Empty;
            await LoadCategoriesAsync();
            ApplyFilterAndGrouping();
        }
        catch
        {
            CategoryStatusMessage = "Could not delete category right now. Please try again.";
        }
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync(ClipboardItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        bool wasProtected = IsProtected(item);
        item.IsFavorite = !item.IsFavorite;
        ReturnToRecentIfProtectionRemoved(item, wasProtected);
        await _clipboardRepository.UpsertAsync(item.ToEntity());
        ApplyFilterAndGrouping();
    }

    [RelayCommand]
    private async Task DeleteItemAsync(ClipboardItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            if (item.Type == ClipboardItemType.Image)
            {
                TryDeleteFile(item.ImagePath);
            }

            await _clipboardRepository.DeleteAsync(item.Id);
            AllItems.Remove(item);
            ApplyFilterAndGrouping();
        }
        catch
        {
            // Keep the app responsive if a delete operation fails.
        }
    }

    [RelayCommand]
    private async Task CopyItemAsync(ClipboardItemViewModel? item)
    {
        if (item is null || item.IsLocked)
        {
            return;
        }

        if (item.Type == ClipboardItemType.Text && !string.IsNullOrEmpty(item.TextContent))
        {
            System.Windows.Clipboard.SetText(item.TextContent);
            return;
        }

        if (item.Type == ClipboardItemType.Image && !string.IsNullOrWhiteSpace(item.ImagePath) && File.Exists(item.ImagePath))
        {
            BitmapImage bitmap = new();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(item.ImagePath, UriKind.Absolute);
            bitmap.EndInit();
            System.Windows.Clipboard.SetImage(bitmap);
        }

        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task PasteSelectedAsync()
    {
        if (SelectedItem is null || SelectedItem.IsLocked)
        {
            return;
        }

        await CopyItemAsync(SelectedItem);
        RequestHideWindow?.Invoke();

        await _autoPasteService.PasteToWindowAsync(_previousForegroundWindow);
    }

    [RelayCommand]
    private async Task ToggleLockAsync(ClipboardItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        _pendingVaultItemId = item.Id;
        _pendingVaultOperation = item.IsLocked ? VaultOperation.Unlock : VaultOperation.Lock;
        UnlockModalTitle = item.IsLocked ? "Unlock Vault Item" : "Lock Vault Item";
        UnlockPin = string.Empty;
        UnlockError = string.Empty;
        UnlockSuccess = false;
        IsUnlockModalOpen = true;

        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SubmitUnlockAsync()
    {
        if (string.IsNullOrWhiteSpace(_pendingVaultItemId) || _pendingVaultOperation == VaultOperation.None)
        {
            return;
        }

        bool valid = await _pinSecurityService.VerifyPinAsync(UnlockPin);
        if (!valid)
        {
            UnlockError = "Incorrect PIN. Please try again.";
            UnlockSuccess = false;
            return;
        }

        ClipboardItemViewModel? item = AllItems.FirstOrDefault(i => i.Id == _pendingVaultItemId);
        if (item is null)
        {
            UnlockError = "Item no longer exists.";
            return;
        }

        if (_pendingVaultOperation == VaultOperation.Lock)
        {
            await LockItemAsync(item, UnlockPin);
        }
        else
        {
            await UnlockItemAsync(item, UnlockPin);
        }

        UnlockSuccess = true;
        await Task.Delay(500);

        IsUnlockModalOpen = false;
        UnlockPin = string.Empty;
        UnlockError = string.Empty;
        UnlockSuccess = false;
        _pendingVaultOperation = VaultOperation.None;
        _pendingVaultItemId = null;
        ApplyFilterAndGrouping();
    }

    [RelayCommand]
    private void CancelUnlock()
    {
        IsUnlockModalOpen = false;
        UnlockPin = string.Empty;
        UnlockError = string.Empty;
        UnlockSuccess = false;
        _pendingVaultOperation = VaultOperation.None;
        _pendingVaultItemId = null;
    }

    [RelayCommand]
    private void OpenClearHistoryModal()
    {
        SelectedClearScope = ClearHistoryScope.RecentVisible;
        RefreshClearHistoryMessage();
        IsClearHistoryModalOpen = true;
    }

    [RelayCommand]
    private void CancelClearHistory()
    {
        IsClearHistoryModalOpen = false;
    }

    [RelayCommand]
    private void SetClearHistoryScope(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return;
        }

        if (Enum.TryParse(scope, true, out ClearHistoryScope parsed))
        {
            SelectedClearScope = parsed;
        }
    }

    [RelayCommand]
    private async Task ChangeRetentionLimitAsync(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        RetentionLimit = value;
        await _settingsRepository.SetValueAsync(SettingKeys.RetentionLimit, RetentionLimit);
        await EnforceRetentionAsync();
        ApplyFilterAndGrouping();
    }

    [RelayCommand]
    private async Task ChangeRecentItemsLimitAsync(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        RecentItemsLimit = value;
        await _settingsRepository.SetValueAsync(SettingKeys.RecentItemsLimit, RecentItemsLimit);
        ApplyFilterAndGrouping();
    }

    [RelayCommand]
    private async Task ChangeAutoLockTimeoutAsync(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        AutoLockTimeout = value;
        await _settingsRepository.SetValueAsync(SettingKeys.AutoLockTimeout, AutoLockTimeout);
    }

    [RelayCommand]
    private async Task SavePinAsync()
    {
        PinStatus = string.Empty;

        if (string.IsNullOrWhiteSpace(PinSetup) || string.IsNullOrWhiteSpace(PinConfirm))
        {
            PinStatus = "PIN is required.";
            return;
        }

        if (PinSetup != PinConfirm)
        {
            PinStatus = "PIN values do not match.";
            return;
        }

        await _pinSecurityService.SetPinAsync(PinSetup);
        PinStatus = "Vault PIN updated.";
        PinSetup = string.Empty;
        PinConfirm = string.Empty;
    }

    [RelayCommand]
    private async Task ConfirmClearHistoryAsync()
    {
        List<ClipboardItemViewModel> toDelete = GetClearHistoryTargets().ToList();
        if (toDelete.Count == 0)
        {
            IsClearHistoryModalOpen = false;
            return;
        }

        foreach (ClipboardItemViewModel item in toDelete)
        {
            try
            {
                if (item.Type == ClipboardItemType.Image)
                {
                    TryDeleteFile(item.ImagePath);
                }

                await _clipboardRepository.DeleteAsync(item.Id);
                AllItems.Remove(item);
            }
            catch
            {
                // Continue deleting remaining items even if one item fails.
            }
        }

        IsClearHistoryModalOpen = false;
        ApplyFilterAndGrouping();
    }

    partial void OnSearchQueryChanged(string value)
    {
        ApplyFilterAndGrouping();
        if (IsClearHistoryModalOpen)
        {
            RefreshClearHistoryMessage();
        }
    }
    partial void OnActiveTabChanged(NavigationTab value)
    {
        OnPropertyChanged(nameof(IsCategoryTab));
        OnPropertyChanged(nameof(IsFlatListTab));
        ApplyFilterAndGrouping();
        if (IsClearHistoryModalOpen)
        {
            RefreshClearHistoryMessage();
        }
    }

    private async Task LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        _suppressSettingWrites = true;

        string? runAtStartupRaw = await _settingsRepository.GetValueAsync(SettingKeys.RunAtStartup, cancellationToken);
        bool isStartupFirstRun = runAtStartupRaw is null;
        RunAtStartup = ParseBool(runAtStartupRaw, defaultValue: true);

        IgnoreDuplicates = ParseBool(await _settingsRepository.GetValueAsync(SettingKeys.IgnoreDuplicates, cancellationToken), defaultValue: true);
        RecentItemsLimit = await _settingsRepository.GetValueAsync(SettingKeys.RecentItemsLimit, cancellationToken) ?? DefaultRecentItemsLimit.ToString();
        AutoLockTimeout = await _settingsRepository.GetValueAsync(SettingKeys.AutoLockTimeout, cancellationToken) ?? "15";
        RetentionLimit = await _settingsRepository.GetValueAsync(SettingKeys.RetentionLimit, cancellationToken) ?? "500";

        string? hotkeyRaw = await _settingsRepository.GetValueAsync(SettingKeys.GlobalHotkey, cancellationToken);
        ParseHotkeyFromDb(hotkeyRaw, out _currentHotkeyModifiers, out _currentHotkeyVk);
        GlobalHotkeyDisplay = HotkeyToDisplay(_currentHotkeyModifiers, _currentHotkeyVk);
        _globalHotkeyService.Configure(_currentHotkeyModifiers, _currentHotkeyVk);

        _suppressSettingWrites = false;

        if (isStartupFirstRun)
        {
            await PersistRunAtStartupAsync(true);
        }
    }

    private async Task ReloadItemsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ClipboardItem> items = await _clipboardRepository.GetAllAsync(cancellationToken);

        AllItems.Clear();
        foreach (ClipboardItem item in items)
        {
            AllItems.Add(ClipboardItemViewModel.FromEntity(item));
        }

        ApplyFilterAndGrouping();
    }

    private async Task SaveCapturedItemAsync(ClipboardCapturedEventArgs captured)
    {
        if (IgnoreDuplicates)
        {
            ClipboardItemViewModel? latest = AllItems.FirstOrDefault();
            if (latest is not null && IsSameContent(latest, captured))
            {
                return;
            }
        }

        ClipboardItem item = new()
        {
            Id = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = DateTime.UtcNow,
            Type = captured.Type,
            TextContent = captured.TextContent,
            ImagePath = captured.ImagePath,
            IsFavorite = false,
            CategoryId = null,
            IsLocked = false
        };

        await _clipboardRepository.UpsertAsync(item);
        AllItems.Insert(0, ClipboardItemViewModel.FromEntity(item));

        await EnforceRetentionAsync();
        ApplyFilterAndGrouping();
    }

    private async Task EnforceRetentionAsync()
    {
        int limit = ParseRetentionLimit();
        while (AllItems.Count(item => !IsProtected(item)) > limit)
        {
            ClipboardItemViewModel? toRemove = AllItems
                .Where(item => !IsProtected(item))
                .OrderBy(item => item.CreatedAtUtc)
                .FirstOrDefault();

            if (toRemove is null)
            {
                return;
            }

            if (toRemove.Type == ClipboardItemType.Image)
            {
                TryDeleteFile(toRemove.ImagePath);
            }

            await _clipboardRepository.DeleteAsync(toRemove.Id);
            AllItems.Remove(toRemove);
        }
    }

    private static bool IsProtected(ClipboardItemViewModel item)
    {
        return item.IsFavorite || !string.IsNullOrWhiteSpace(item.CategoryId);
    }

    private static void ReturnToRecentIfProtectionRemoved(ClipboardItemViewModel item, bool wasProtected)
    {
        if (wasProtected && !IsProtected(item))
        {
            item.CreatedAtUtc = DateTime.UtcNow;
        }
    }

    private async Task LockItemAsync(ClipboardItemViewModel item, string pin)
    {
        if (item.IsLocked)
        {
            return;
        }

        byte[] plain;
        string? imagePathToDelete = null;

        if (item.Type == ClipboardItemType.Text)
        {
            plain = Encoding.UTF8.GetBytes(item.TextContent ?? string.Empty);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(item.ImagePath) || !File.Exists(item.ImagePath))
            {
                throw new InvalidOperationException("Image not found for locking.");
            }

            plain = await File.ReadAllBytesAsync(item.ImagePath);
            imagePathToDelete = item.ImagePath;
        }

        var encrypted = _vaultEncryptionService.Encrypt(plain, pin);

        item.IsLocked = true;
        item.EncryptedPayload = encrypted.EncryptedPayload;
        item.EncryptionMeta = encrypted.EncryptionMeta;
        item.TextContent = null;
        item.ImagePath = null;

        if (!string.IsNullOrWhiteSpace(imagePathToDelete) && File.Exists(imagePathToDelete))
        {
            TryDeleteFile(imagePathToDelete);
        }

        await _clipboardRepository.UpsertAsync(item.ToEntity());
    }

    private async Task UnlockItemAsync(ClipboardItemViewModel item, string pin)
    {
        if (!item.IsLocked || item.EncryptedPayload is null || string.IsNullOrWhiteSpace(item.EncryptionMeta))
        {
            return;
        }

        byte[] plain = _vaultEncryptionService.Decrypt(item.EncryptedPayload, item.EncryptionMeta, pin);

        item.IsLocked = false;
        item.EncryptedPayload = null;
        item.EncryptionMeta = null;

        if (item.Type == ClipboardItemType.Text)
        {
            item.TextContent = Encoding.UTF8.GetString(plain);
        }
        else
        {
            string fileName = $"unlock_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.png";
            string filePath = Path.Combine(_paths.ImagesDirectory, fileName);
            await File.WriteAllBytesAsync(filePath, plain);
            item.ImagePath = filePath;
        }

        await _clipboardRepository.UpsertAsync(item.ToEntity());
    }

    private void ApplyFilterAndGrouping()
    {
        foreach (ClipboardItemViewModel item in AllItems)
        {
            item.CategoryDisplayName = GetCategoryName(item.CategoryId);
        }

        IEnumerable<ClipboardItemViewModel> query = AllItems;

        if (ActiveTab == NavigationTab.Favorites)
        {
            query = query.Where(i => i.IsFavorite);
        }

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            string search = SearchQuery.Trim();
            query = query.Where(i =>
                (!string.IsNullOrWhiteSpace(i.TextContent) && i.TextContent.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(i.Title) && i.Title.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                GetCategoryName(i.CategoryId).Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (ActiveTab == NavigationTab.Categories)
        {
            HashSet<string> realIds = Categories.Select(c => c.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            query = query.Where(i => !string.IsNullOrWhiteSpace(i.CategoryId) && realIds.Contains(i.CategoryId));
        }

        List<ClipboardItemViewModel> filtered = query
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToList();

        if (ActiveTab == NavigationTab.Recent)
        {
            filtered = filtered.Take(ParseRecentItemsLimit()).ToList();
        }

        VisibleItems.Clear();
        foreach (ClipboardItemViewModel item in filtered)
        {
            VisibleItems.Add(item);
        }

        CategoryGroups.Clear();
        foreach (IGrouping<string, ClipboardItemViewModel> group in filtered.GroupBy(i => GetCategoryName(i.CategoryId)))
        {
            CategoryGroupViewModel categoryGroup = new()
            {
                Name = group.Key,
                IsExpanded = true
            };

            foreach (ClipboardItemViewModel item in group)
            {
                categoryGroup.Items.Add(item);
            }

            CategoryGroups.Add(categoryGroup);
        }

        if (SelectedItem is null || !filtered.Contains(SelectedItem))
        {
            SelectedItem = filtered.FirstOrDefault();
        }

        OnPropertyChanged(nameof(HasVisibleItems));
    }

    private bool IsSameContent(ClipboardItemViewModel existing, ClipboardCapturedEventArgs captured)
    {
        if (existing.Type != captured.Type || existing.IsLocked)
        {
            return false;
        }

        if (captured.Type == ClipboardItemType.Text)
        {
            return string.Equals(existing.TextContent, captured.TextContent, StringComparison.Ordinal);
        }

        return false;
    }

    private async Task LoadCategoriesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Category> categories = await _categoryRepository.GetAllAsync(cancellationToken);
        Categories.Clear();
        CategoryChoices.Clear();
        CategoryChoices.Add(new CategoryItemViewModel { Id = string.Empty, Name = "Uncategorized" });
        foreach (Category category in categories)
        {
            CategoryItemViewModel vm = new()
            {
                Id = category.Id,
                Name = category.Name,
                Color = category.Color,
                SortOrder = category.SortOrder
            };

            Categories.Add(vm);
            CategoryChoices.Add(new CategoryItemViewModel
            {
                Id = vm.Id,
                Name = vm.Name,
                Color = vm.Color,
                SortOrder = vm.SortOrder
            });
        }
    }

    private void ResetNewItemDraft()
    {
        NewItemTitle = string.Empty;
        NewItemContent = string.Empty;
        NewItemCategoryId = null;
        NewItemIsFavorite = false;
        NewItemIsLocked = false;
    }

    public string GetCategoryName(string? categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
        {
            return "Uncategorized";
        }

        return Categories.FirstOrDefault(c => c.Id == categoryId)?.Name ?? "Unknown Category";
    }

    private int ParseRetentionLimit()
    {
        return int.TryParse(RetentionLimit, out int limit) && limit > 0 ? limit : 500;
    }

    private int ParseRecentItemsLimit()
    {
        return int.TryParse(RecentItemsLimit, out int limit) && limit > 0 ? limit : DefaultRecentItemsLimit;
    }

    private static bool ParseBool(string? value, bool defaultValue)
    {
        if (value is null)
        {
            return defaultValue;
        }

        return bool.TryParse(value, out bool parsed) ? parsed : defaultValue;
    }

    private static string HotkeyToDisplay(uint modifiers, uint vk)
    {
        var parts = new System.Collections.Generic.List<string>();
        if ((modifiers & NativeMethods.MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((modifiers & NativeMethods.MOD_ALT) != 0)     parts.Add("Alt");
        if ((modifiers & NativeMethods.MOD_SHIFT) != 0)   parts.Add("Shift");
        parts.Add(VkToName(vk));
        return string.Join(" + ", parts);
    }

    private static string VkToName(uint vk)
    {
        if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();
        if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();
        if (vk >= 0x70 && vk <= 0x7B) return $"F{vk - 0x6F}";
        return vk switch
        {
            0x20 => "Space",
            0xDB => "[",
            0xDD => "]",
            0xBA => ";",
            0xDE => "'",
            0xBC => ",",
            0xBE => ".",
            0xBF => "/",
            0xC0 => "`",
            0xBD => "-",
            0xBB => "=",
            0xDC => "\\",
            _    => $"0x{vk:X2}"
        };
    }

    private static string HotkeyToDbString(uint modifiers, uint vk) => $"{modifiers}:{vk}";

    private static void ParseHotkeyFromDb(string? raw, out uint modifiers, out uint vk)
    {
        modifiers = NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT;
        vk = NativeMethods.VK_V;

        if (string.IsNullOrWhiteSpace(raw)) return;

        string[] parts = raw.Split(':');
        if (parts.Length == 2
            && uint.TryParse(parts[0], out uint m)
            && uint.TryParse(parts[1], out uint k)
            && k != 0)
        {
            modifiers = m;
            vk = k;
        }
    }

    private static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Ignore files in use or unavailable.
        }
        catch (UnauthorizedAccessException)
        {
            // Ignore permission errors and continue.
        }
    }

    partial void OnSelectedCategoryChanged(CategoryItemViewModel? value)
    {
        RenameCategoryName = value?.Name ?? string.Empty;
    }

    partial void OnSelectedClearScopeChanged(ClearHistoryScope value)
    {
        RefreshClearHistoryMessage();
    }

    partial void OnRunAtStartupChanged(bool value)
    {
        if (_suppressSettingWrites)
        {
            return;
        }

        _ = PersistRunAtStartupAsync(value);
    }

    partial void OnIgnoreDuplicatesChanged(bool value)
    {
        if (_suppressSettingWrites)
        {
            return;
        }

        _ = PersistIgnoreDuplicatesAsync(value);
    }

    private async Task PersistRunAtStartupAsync(bool value)
    {
        await _startupRegistrationService.SetEnabledAsync(value);
        await _settingsRepository.SetValueAsync(SettingKeys.RunAtStartup, value ? "true" : "false");
    }

    private async Task PersistIgnoreDuplicatesAsync(bool value)
    {
        await _settingsRepository.SetValueAsync(SettingKeys.IgnoreDuplicates, value ? "true" : "false");
    }

    private IEnumerable<ClipboardItemViewModel> GetClearHistoryTargets()
    {
        return SelectedClearScope switch
        {
            ClearHistoryScope.RecentVisible => BuildFilteredList(NavigationTab.Recent, includeRecentLimit: true),
            ClearHistoryScope.Favorites => BuildFilteredList(NavigationTab.Favorites, includeRecentLimit: false),
            _ => AllItems.ToList()
        };
    }

    private IEnumerable<ClipboardItemViewModel> BuildFilteredList(NavigationTab tab, bool includeRecentLimit)
    {
        IEnumerable<ClipboardItemViewModel> query = AllItems;

        if (tab == NavigationTab.Favorites)
        {
            query = query.Where(i => i.IsFavorite);
        }

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            string search = SearchQuery.Trim();
            query = query.Where(i =>
                (!string.IsNullOrWhiteSpace(i.TextContent) && i.TextContent.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(i.Title) && i.Title.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                GetCategoryName(i.CategoryId).Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        List<ClipboardItemViewModel> filtered = query
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToList();

        if (tab == NavigationTab.Recent && includeRecentLimit)
        {
            filtered = filtered.Take(ParseRecentItemsLimit()).ToList();
        }

        return filtered;
    }

    private void RefreshClearHistoryMessage()
    {
        int count = GetClearHistoryTargets().Count();
        ClearHistoryMessage = SelectedClearScope switch
        {
            ClearHistoryScope.RecentVisible => $"This will delete {count} item(s) from Recent (current filter).",
            ClearHistoryScope.Favorites => $"This will delete {count} favorite item(s) matching your current filter.",
            _ => $"This will delete all {count} item(s) from clipboard history."
        };
    }
}
