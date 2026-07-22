using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;

namespace OrbsWin;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private NotifyIcon? _notifyIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _notifyIcon = new NotifyIcon
        {
            Text = "OrbsWin",
            Visible = true,
            Icon = LoadOrCreateIcon()
        };

        var contextMenu = new ContextMenuStrip();

        var settingsItem = new ToolStripMenuItem("Settings", null, (sender, args) =>
        {
            // Stub: does nothing yet
        });

        var startWithWindowsItem = new ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = true,
            Checked = false
        };
        startWithWindowsItem.Click += (sender, args) =>
        {
            // Stub: does nothing yet
        };

        var quitItem = new ToolStripMenuItem("Quit", null, (sender, args) =>
        {
            Shutdown();
        });

        contextMenu.Items.Add(settingsItem);
        contextMenu.Items.Add(startWithWindowsItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(quitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    private Icon LoadOrCreateIcon()
    {
        string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "tray-icon.ico");
        
        if (File.Exists(iconPath))
        {
            try
            {
                return new Icon(iconPath);
            }
            catch
            {
                // Fallback if loading fails
            }
        }

        // Generate fallback icon dynamically if missing or invalid
        using Bitmap bitmap = new Bitmap(16, 16);
        using Graphics g = Graphics.FromImage(bitmap);
        g.Clear(Color.Transparent);
        using (SolidBrush brush = new SolidBrush(Color.DodgerBlue))
        {
            g.FillEllipse(brush, 1, 1, 14, 14);
        }
        IntPtr hIcon = bitmap.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        base.OnExit(e);
    }
}
