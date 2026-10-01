using ClipVault.UI.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ClipVault.UI.Services;

public sealed class GlobalHotkeyService : IGlobalHotkeyService
{
    private const int HotkeyId = 0xCC11;

    private HwndSource? _hwndSource;
    private IntPtr _windowHandle;

    private uint _currentModifiers = NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT;
    private uint _currentVk = NativeMethods.VK_V;

    public event EventHandler? HotkeyPressed;

    public uint CurrentModifiers => _currentModifiers;
    public uint CurrentVk => _currentVk;

    public void Configure(uint modifiers, uint vk)
    {
        _currentModifiers = modifiers;
        _currentVk = vk;
    }

    public void Register(Window window)
    {
        if (_windowHandle != IntPtr.Zero)
        {
            return;
        }

        _windowHandle = new WindowInteropHelper(window).EnsureHandle();
        _hwndSource = HwndSource.FromHwnd(_windowHandle);
        _hwndSource?.AddHook(WndProc);

        bool registered = NativeMethods.RegisterHotKey(_windowHandle, HotkeyId, _currentModifiers, _currentVk);
        if (!registered)
        {
            int errorCode = Marshal.GetLastWin32Error();

            // The hotkey can already be in use by another process (1409). Do not crash the app;
            // continue without global hotkey support for this session.
            _hwndSource?.RemoveHook(WndProc);
            _hwndSource = null;
            _windowHandle = IntPtr.Zero;
            Debug.WriteLine($"Global hotkey registration skipped (error {errorCode}).");
        }
    }

    public void Unregister()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.UnregisterHotKey(_windowHandle, HotkeyId);
        _hwndSource?.RemoveHook(WndProc);
        _hwndSource = null;
        _windowHandle = IntPtr.Zero;
    }

    public void Reregister(uint modifiers, uint vk)
    {
        if (_windowHandle == IntPtr.Zero)
        {
            // Not yet registered — just update the stored values for when Register is called
            _currentModifiers = modifiers;
            _currentVk = vk;
            return;
        }

        NativeMethods.UnregisterHotKey(_windowHandle, HotkeyId);

        _currentModifiers = modifiers;
        _currentVk = vk;

        bool registered = NativeMethods.RegisterHotKey(_windowHandle, HotkeyId, _currentModifiers, _currentVk);
        if (!registered)
        {
            int errorCode = Marshal.GetLastWin32Error();
            Debug.WriteLine($"Global hotkey re-registration failed (error {errorCode}).");
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
        }

        return IntPtr.Zero;
    }
}
