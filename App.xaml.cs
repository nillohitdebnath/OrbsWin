using System;
using System.Diagnostics;
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
    private GlobalHookService? _globalHookService;

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
            CleanupHooksAndNotifyIcon();
            Shutdown();
        });

        contextMenu.Items.Add(settingsItem);
        contextMenu.Items.Add(startWithWindowsItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(quitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;

        InitializeGlobalHooks();
    }

    private void InitializeGlobalHooks()
    {
        try
        {
            _globalHookService = new GlobalHookService();

            _globalHookService.HotkeyHoldStarted += (sender, args) =>
            {
                System.Drawing.Point cursorPos = System.Windows.Forms.Cursor.Position;
                Debug.WriteLine($"[HotkeyHoldStarted] Ctrl+Shift held down at Cursor Position: {cursorPos}");
            };

            _globalHookService.HotkeyHoldEnded += (sender, args) =>
            {
                Debug.WriteLine("[HotkeyHoldEnded] Ctrl+Shift released.");
            };

            _globalHookService.ClickHoldStarted += (sender, point) =>
            {
                Debug.WriteLine($"[ClickHoldStarted] Left Click Hold started at Cursor Position: {point}");
            };

            _globalHookService.ClickHoldEnded += (sender, point) =>
            {
                Debug.WriteLine($"[ClickHoldEnded] Left Click Hold ended at Cursor Position: {point}");
            };

            _globalHookService.Start();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[App] Failed to initialize GlobalHookService: {ex.Message}");
        }
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

    private void CleanupHooksAndNotifyIcon()
    {
        if (_globalHookService != null)
        {
            _globalHookService.Dispose();
            _globalHookService = null;
        }

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        CleanupHooksAndNotifyIcon();
        base.OnExit(e);
    }
}
