using ClipVault.UI.Services;
using ClipVault.UI.ViewModels;
using ClipVault.UI.Win32;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ClipVault.UI;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly IClipboardMonitorService _clipboardMonitorService;
    private readonly IGlobalHotkeyService _globalHotkeyService;
    private readonly IAutoPasteService _autoPasteService;

    private bool _initialized;

    public MainWindow(
        MainWindowViewModel viewModel,
        IClipboardMonitorService clipboardMonitorService,
        IGlobalHotkeyService globalHotkeyService,
        IAutoPasteService autoPasteService)
    {
        _viewModel = viewModel;
        _clipboardMonitorService = clipboardMonitorService;
        _globalHotkeyService = globalHotkeyService;
        _autoPasteService = autoPasteService;

        DataContext = _viewModel;
        InitializeComponent();

        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += OnClosed;
        PreviewKeyDown += OnPreviewKeyDown;

        _viewModel.RequestHideWindow += () => Dispatcher.Invoke(Hide);
        _globalHotkeyService.HotkeyPressed += GlobalHotkeyServiceOnHotkeyPressed;
    }

    public void ShowFromBackground(bool openSettings)
    {
        if (openSettings)
        {
            _viewModel.ShowSettings = true;
        }

        if (!IsVisible)
        {
            Show();
        }

        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        await _viewModel.InitializeAsync();
        _clipboardMonitorService.Start(this);
        _globalHotkeyService.Register(this);
    }

    private void GlobalHotkeyServiceOnHotkeyPressed(object? sender, EventArgs e)
    {
        IntPtr previousWindow = _autoPasteService.CaptureForegroundWindow();
        _viewModel.SetPreviousForegroundWindow(previousWindow);
        ShowFromBackground(false);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        bool exitRequested = (System.Windows.Application.Current as App)?.IsExitRequested == true;
        if (!exitRequested)
        {
            e.Cancel = true;
            Hide();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _globalHotkeyService.HotkeyPressed -= GlobalHotkeyServiceOnHotkeyPressed;
        _globalHotkeyService.Unregister();
        _clipboardMonitorService.Stop();
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Intercept keystrokes when the hotkey recording modal is open
        if (_viewModel.IsHotkeyModalOpen)
        {
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;

            // Ignore lone modifier keys
            if (key is Key.LeftCtrl or Key.RightCtrl
                    or Key.LeftShift or Key.RightShift
                    or Key.LeftAlt or Key.RightAlt
                    or Key.LWin or Key.RWin)
            {
                return;
            }

            if (key == Key.Escape)
            {
                _viewModel.CancelHotkeyCommand.Execute(null);
                e.Handled = true;
                return;
            }

            uint mods = 0;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) mods |= NativeMethods.MOD_CONTROL;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)   mods |= NativeMethods.MOD_SHIFT;
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)     mods |= NativeMethods.MOD_ALT;

            uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            _viewModel.UpdateRecordedHotkey(mods, vk);
            e.Handled = true;
            return;
        }

        // Ctrl+F → focus the search box
        if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            SearchTextBox.Focus();
            SearchTextBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key == System.Windows.Input.Key.Enter)
        {
            if (_viewModel.PasteSelectedCommand.CanExecute(null))
            {
                _viewModel.PasteSelectedCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void MinimizeButton_OnClick(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void RetentionLimitComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized)
        {
            return;
        }

        if (_viewModel.ChangeRetentionLimitCommand.CanExecute(_viewModel.RetentionLimit))
        {
            _viewModel.ChangeRetentionLimitCommand.Execute(_viewModel.RetentionLimit);
        }
    }

    private void RecentItemsLimitComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized)
        {
            return;
        }

        if (_viewModel.ChangeRecentItemsLimitCommand.CanExecute(_viewModel.RecentItemsLimit))
        {
            _viewModel.ChangeRecentItemsLimitCommand.Execute(_viewModel.RecentItemsLimit);
        }
    }

    private void AutoLockTimeoutComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized)
        {
            return;
        }

        if (_viewModel.ChangeAutoLockTimeoutCommand.CanExecute(_viewModel.AutoLockTimeout))
        {
            _viewModel.ChangeAutoLockTimeoutCommand.Execute(_viewModel.AutoLockTimeout);
        }
    }
}
