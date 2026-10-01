using ClipVault.UI.Win32;
using System.Runtime.InteropServices;

namespace ClipVault.UI.Services;

public sealed class AutoPasteService : IAutoPasteService
{
    public IntPtr CaptureForegroundWindow() => NativeMethods.GetForegroundWindow();

    public async Task PasteToWindowAsync(IntPtr handle, CancellationToken cancellationToken = default)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.ShowWindow(handle, NativeMethods.SW_RESTORE);
        NativeMethods.SetForegroundWindow(handle);

        await Task.Delay(70, cancellationToken);

        NativeMethods.INPUT[] inputs =
        [
            CreateKeyInput(0x11, false),
            CreateKeyInput((ushort)NativeMethods.VK_V, false),
            CreateKeyInput((ushort)NativeMethods.VK_V, true),
            CreateKeyInput(0x11, true)
        ];

        uint result = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        if (result != inputs.Length)
        {
            throw new InvalidOperationException($"SendInput failed with {Marshal.GetLastWin32Error()}.");
        }
    }

    private static NativeMethods.INPUT CreateKeyInput(ushort virtualKey, bool keyUp)
    {
        return new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = virtualKey,
                    wScan = 0,
                    dwFlags = keyUp ? NativeMethods.KEYEVENTF_KEYUP : 0,
                    time = 0,
                    dwExtraInfo = 0
                }
            }
        };
    }
}
