using System.Drawing;
using System.IO;
using System.Windows;
using Forms = System.Windows.Forms;

namespace ClipVault.UI.Tray;

public sealed class SystemTrayService : ISystemTrayService
{
    private Forms.NotifyIcon? _notifyIcon;

    public void Initialize(Action onOpen, Action onOpenSettings, Action onExit)
    {
        if (_notifyIcon is not null)
        {
            return;
        }

        Forms.ContextMenuStrip menu = new();
        menu.Items.Add("Open ClipVault", null, (_, _) => System.Windows.Application.Current.Dispatcher.Invoke(onOpen));
        menu.Items.Add("Settings", null, (_, _) => System.Windows.Application.Current.Dispatcher.Invoke(onOpenSettings));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => System.Windows.Application.Current.Dispatcher.Invoke(onExit));

        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "ClipVault",
            Icon = LoadTrayIcon(),
            Visible = true,
            ContextMenuStrip = menu
        };

        _notifyIcon.DoubleClick += (_, _) => System.Windows.Application.Current.Dispatcher.Invoke(onOpen);
    }

    public void Dispose()
    {
        if (_notifyIcon is null)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _notifyIcon = null;
    }

    private static Icon LoadTrayIcon()
    {
        try
        {
            string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Clipboard2.ico");
            if (!File.Exists(iconPath))
            {
                return Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty) ?? SystemIcons.Application;
            }

            return new Icon(iconPath);
        }
        catch
        {
            return Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty) ?? SystemIcons.Application;
        }
    }
}
