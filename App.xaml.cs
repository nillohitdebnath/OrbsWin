using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using OrbsWin.Services;
using OrbsWin.Tools;

namespace OrbsWin;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private NotifyIcon? _notifyIcon;
    private GlobalHookService? _globalHookService;
    private WheelWindow? _activeWheelWindow;
    private ToolStripMenuItem? _caffeineMenuItem;
    private ToolStripMenuItem? _startWithWindowsMenuItem;
    private SettingsWindow? _activeSettingsWindow;

    private List<WheelItem> _cachedItems = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ReloadConfig();

        _notifyIcon = new NotifyIcon
        {
            Text = "OrbsWin",
            Visible = true,
            Icon = LoadOrCreateIcon()
        };

        var contextMenu = new ContextMenuStrip();

        _caffeineMenuItem = new ToolStripMenuItem("Caffeine (Prevent Sleep)")
        {
            CheckOnClick = true,
            Checked = false
        };
        _caffeineMenuItem.Click += (sender, args) =>
        {
            ToggleCaffeineState();
        };

        var settingsItem = new ToolStripMenuItem("Settings", null, (sender, args) =>
        {
            OpenSettingsWindow();
        });

        _startWithWindowsMenuItem = new ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = true,
            Checked = StartupService.IsStartWithWindowsEnabled()
        };
        _startWithWindowsMenuItem.Click += (sender, args) =>
        {
            bool enable = _startWithWindowsMenuItem.Checked;
            StartupService.SetStartWithWindows(enable);
        };

        var quitItem = new ToolStripMenuItem("Quit", null, (sender, args) =>
        {
            CleanupHooksAndNotifyIcon();
            Shutdown();
        });

        contextMenu.Items.Add(_caffeineMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(settingsItem);
        contextMenu.Items.Add(_startWithWindowsMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(quitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;

        InitializeGlobalHooks();
        InitializeClipboardMonitor();
    }

    public void ReloadConfig()
    {
        _cachedItems = WheelConfigService.LoadConfig();
        Debug.WriteLine($"[App] Loaded {_cachedItems.Count} top-level wheel items from config.");
    }

    public void SyncAutostartMenuItem(bool isEnabled)
    {
        if (_startWithWindowsMenuItem != null)
        {
            _startWithWindowsMenuItem.Checked = isEnabled;
        }
    }

    public void OpenSettingsWindow()
    {
        Dispatcher.Invoke(() =>
        {
            if (_activeSettingsWindow != null && _activeSettingsWindow.IsLoaded)
            {
                _activeSettingsWindow.Activate();
                return;
            }

            _activeSettingsWindow = new SettingsWindow();
            _activeSettingsWindow.Closed += (s, args) =>
            {
                _activeSettingsWindow = null;
            };
            _activeSettingsWindow.Show();
            _activeSettingsWindow.Activate();
        });
    }

    private void InitializeClipboardMonitor()
    {
        // Dummy hidden window to anchor HWND clipboard format listener
        Window anchorWindow = new Window
        {
            Width = 0,
            Height = 0,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            Visibility = Visibility.Hidden
        };
        anchorWindow.SourceInitialized += (s, e) =>
        {
            ClipboardMonitorService.Instance.Start(anchorWindow);
        };
        anchorWindow.Show();
        anchorWindow.Hide();
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

            _activeWheelWindow = new WheelWindow(centerPosition, _cachedItems);
            
            _activeWheelWindow.ItemSelected += (s, selectedItem) =>
            {
                Debug.WriteLine($"[WheelWindow] Item Selected: {selectedItem.Name} (Type: {selectedItem.ItemType})");
                _activeWheelWindow = null;

                System.Drawing.Point releasePos = System.Windows.Forms.Cursor.Position;
                ActionExecutor.Execute(selectedItem, releasePos, ShowBalloonNotification, ToggleCaffeineState);
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

    private void ToggleCaffeineState()
    {
        bool isEnabled = CaffeineToggleService.Instance.Toggle(ShowBalloonNotification);
        if (_caffeineMenuItem != null)
        {
            _caffeineMenuItem.Checked = isEnabled;
        }
    }

    public void ShowBalloonNotification(string title, string text)
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.ShowBalloonTip(3000, title, text, ToolTipIcon.Info);
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
        CaffeineToggleService.Instance.Disable();
        ClipboardMonitorService.Instance.Stop();

        if (_activeSettingsWindow != null)
        {
            _activeSettingsWindow.Close();
            _activeSettingsWindow = null;
        }

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
