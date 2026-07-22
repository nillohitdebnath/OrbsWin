using System;
using System.Collections.Generic;
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
    private WheelWindow? _activeWheelWindow;

    private readonly List<WheelItem> _testWheelItems = new()
    {
        new WheelItem("Calculator"),
        new WheelItem("Timer"),
        new WheelItem("Clipboard"),
        new WheelItem("Color Picker"),
        new WheelItem("Notes", null, new List<WheelItem>
        {
            new WheelItem("New Note"),
            new WheelItem("Recent Notes"),
            new WheelItem("Search Notes")
        }),
        new WheelItem("Settings")
    };

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
                Debug.WriteLine($"[HotkeyHoldStarted] Triggered at {cursorPos}");
                ShowWheelWindow(cursorPos);
            };

            _globalHookService.ClickHoldStarted += (sender, point) =>
            {
                Debug.WriteLine($"[ClickHoldStarted] Triggered at {point}");
                ShowWheelWindow(point);
            };

            _globalHookService.Start();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[App] Failed to initialize GlobalHookService: {ex.Message}");
        }
    }

    private void ShowWheelWindow(System.Drawing.Point centerPosition)
    {
        Dispatcher.Invoke(() =>
        {
            if (_activeWheelWindow != null)
            {
                _activeWheelWindow.Close();
                _activeWheelWindow = null;
            }

            _activeWheelWindow = new WheelWindow(centerPosition, _testWheelItems);
            
            _activeWheelWindow.ItemSelected += (s, selectedItem) =>
            {
                Debug.WriteLine($"[WheelWindow] Item Selected: {selectedItem.Name}");
                _activeWheelWindow = null;
            };

            _activeWheelWindow.SelectionCancelled += (s, args) =>
            {
                Debug.WriteLine("[WheelWindow] Selection Cancelled.");
                _activeWheelWindow = null;
            };

            _activeWheelWindow.Closed += (s, args) =>
            {
                _activeWheelWindow = null;
            };

            _activeWheelWindow.Show();
            _activeWheelWindow.Activate();
        });
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
        if (_activeWheelWindow != null)
        {
            _activeWheelWindow.Close();
            _activeWheelWindow = null;
        }

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
