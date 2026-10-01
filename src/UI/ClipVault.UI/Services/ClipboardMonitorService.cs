using ClipVault.Core.Domain.Enums;
using ClipVault.Infrastructure.Persistence;
using ClipVault.UI.Win32;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace ClipVault.UI.Services;

public sealed class ClipboardMonitorService : IClipboardMonitorService
{
    private readonly AppDataPaths _paths;
    private readonly TimeSpan _duplicateWindow = TimeSpan.FromMilliseconds(700);

    private HwndSource? _hwndSource;
    private IntPtr _windowHandle;
    private string? _lastFingerprint;
    private DateTime _lastCaptureUtc;

    public ClipboardMonitorService(AppDataPaths paths)
    {
        _paths = paths;
        _paths.EnsureCreated();
    }

    public event EventHandler<ClipboardCapturedEventArgs>? ClipboardCaptured;

    public void Start(Window window)
    {
        if (_windowHandle != IntPtr.Zero)
        {
            return;
        }

        _windowHandle = new WindowInteropHelper(window).EnsureHandle();
        _hwndSource = HwndSource.FromHwnd(_windowHandle);
        _hwndSource?.AddHook(WndProc);

        if (!NativeMethods.AddClipboardFormatListener(_windowHandle))
        {
            throw new InvalidOperationException($"AddClipboardFormatListener failed with {Marshal.GetLastWin32Error()}.");
        }
    }

    public void Stop()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.RemoveClipboardFormatListener(_windowHandle);
        _hwndSource?.RemoveHook(WndProc);
        _hwndSource = null;
        _windowHandle = IntPtr.Zero;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_CLIPBOARDUPDATE)
        {
            TryCaptureClipboard();
        }

        return IntPtr.Zero;
    }

    private void TryCaptureClipboard()
    {
        try
        {
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    if (System.Windows.Clipboard.ContainsText())
                    {
                        CaptureText(System.Windows.Clipboard.GetText());
                        return;
                    }

                    if (System.Windows.Clipboard.ContainsImage())
                    {
                        BitmapSource? image = System.Windows.Clipboard.GetImage();
                        if (image is not null)
                        {
                            CaptureImage(image);
                        }

                        return;
                    }

                    return;
                }
                catch (COMException)
                {
                    Thread.Sleep(30);
                }
            }
        }
        catch
        {
            // Ignore clipboard access errors to keep listener resilient.
        }
    }

    private void CaptureText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        string fingerprint = $"text:{text}";
        if (IsDuplicate(fingerprint))
        {
            return;
        }

        ClipboardCaptured?.Invoke(this, new ClipboardCapturedEventArgs(ClipboardItemType.Text, text, null));
    }

    private void CaptureImage(BitmapSource image)
    {
        string fileName = $"clip_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.png";
        string filePath = Path.Combine(_paths.ImagesDirectory, fileName);

        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(image));

        byte[] pngBytes;
        using (MemoryStream memory = new())
        {
            encoder.Save(memory);
            pngBytes = memory.ToArray();
        }

        string fingerprint;
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hash = sha256.ComputeHash(pngBytes);
            fingerprint = $"img:{Convert.ToHexString(hash)}";
        }

        if (IsDuplicate(fingerprint))
        {
            return;
        }

        File.WriteAllBytes(filePath, pngBytes);
        ClipboardCaptured?.Invoke(this, new ClipboardCapturedEventArgs(ClipboardItemType.Image, null, filePath));
    }

    private bool IsDuplicate(string fingerprint)
    {
        DateTime now = DateTime.UtcNow;
        if (string.Equals(_lastFingerprint, fingerprint, StringComparison.Ordinal) && now - _lastCaptureUtc <= _duplicateWindow)
        {
            return true;
        }

        _lastFingerprint = fingerprint;
        _lastCaptureUtc = now;
        return false;
    }
}
