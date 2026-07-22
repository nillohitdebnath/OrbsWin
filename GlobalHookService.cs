using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OrbsWin;

/*
 * =====================================================================================
 * EXPLANATION: CallNextHookEx AND WHY SKIPPING IT IS DANGEROUS
 * =====================================================================================
 * 
 * What CallNextHookEx does:
 * -------------------------
 * In Windows, low-level hooks (WH_KEYBOARD_LL, WH_MOUSE_LL) form a chain of hook
 * procedures managed by the operating system. When a keyboard or mouse event occurs, 
 * Windows passes the event details down this chain. Calling CallNextHookEx passes the 
 * hook information to the next hook procedure in the chain (or to default Windows 
 * input processing if this is the last hook).
 * 
 * Why skipping CallNextHookEx is dangerous:
 * ----------------------------------------
 * 1. Input Swallowing / Interruption: Returning a non-zero value or omitting 
 *    CallNextHookEx prevents subsequent hooks and Windows itself from receiving the 
 *    input event. This causes mouse clicks, key presses, and system hotkeys to be 
 *    completely blocked or lost system-wide.
 * 2. System Instability & Freezes: If an input hook swallows mouse or keyboard messages 
 *    or fails to pass control back to the Windows message pump promptly, the OS 
 *    may experience noticeable input lag, unresponsive windows, or input freezing across 
 *    all running applications.
 * 3. Violating Windows Hook Architecture: Low-level hooks inspect global events. 
 *    Unless an application explicitly intends to intercept/suppress a specific hotkey, 
 *    it must always delegate to CallNextHookEx to preserve normal OS functionality.
 * =====================================================================================
 */

public class GlobalHookService : IDisposable
{
    public event EventHandler? HotkeyHoldStarted;
    public event EventHandler? HotkeyHoldEnded;

    public event EventHandler<Point>? ClickHoldStarted;
    public event EventHandler<Point>? ClickHoldEnded;

    private IntPtr _keyboardHookId = IntPtr.Zero;
    private IntPtr _mouseHookId = IntPtr.Zero;

    // Delegates kept alive to prevent Garbage Collector cleanup
    private readonly LowLevelProc _keyboardProc;
    private readonly LowLevelProc _mouseProc;

    // State tracking for Hotkey (Ctrl + Shift)
    private bool _isCtrlDown;
    private bool _isShiftDown;
    private bool _isHotkeyHolding;

    // State tracking for Mouse Left-Click Hold (>200ms hold, minimal movement)
    private bool _isLeftMouseDown;
    private bool _isMouseHolding;
    private Point _mouseDownPosition;
    private readonly System.Windows.Forms.Timer _mouseHoldTimer;
    private const int HoldThresholdMs = 200;
    private const int MoveTolerancePixels = 5;

    public GlobalHookService()
    {
        _keyboardProc = KeyboardHookCallback;
        _mouseProc = MouseHookCallback;

        _mouseHoldTimer = new System.Windows.Forms.Timer
        {
            Interval = HoldThresholdMs
        };
        _mouseHoldTimer.Tick += OnMouseHoldTimerTick;
    }

    public void Start()
    {
        try
        {
            using Process currentProcess = Process.GetCurrentProcess();
            using ProcessModule? mainModule = currentProcess.MainModule;

            IntPtr moduleHandle = mainModule != null ? NativeMethods.GetModuleHandle(mainModule.ModuleName) : IntPtr.Zero;

            _keyboardHookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);
            if (_keyboardHookId == IntPtr.Zero)
            {
                int errorCode = Marshal.GetLastWin32Error();
                Debug.WriteLine($"[GlobalHookService] Failed to set WH_KEYBOARD_LL hook. Win32 Error: {errorCode}");
            }

            _mouseHookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseProc, moduleHandle, 0);
            if (_mouseHookId == IntPtr.Zero)
            {
                int errorCode = Marshal.GetLastWin32Error();
                Debug.WriteLine($"[GlobalHookService] Failed to set WH_MOUSE_LL hook. Win32 Error: {errorCode}");
            }

            Debug.WriteLine("[GlobalHookService] Hooks initialized successfully.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GlobalHookService] Exception during hook setup: {ex.Message}");
        }
    }

    public void Stop()
    {
        _mouseHoldTimer.Stop();

        if (_keyboardHookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHookId);
            _keyboardHookId = IntPtr.Zero;
            Debug.WriteLine("[GlobalHookService] Keyboard hook unhooked.");
        }

        if (_mouseHookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHookId);
            _mouseHookId = IntPtr.Zero;
            Debug.WriteLine("[GlobalHookService] Mouse hook unhooked.");
        }

        ResetState();
    }

    private void ResetState()
    {
        _isCtrlDown = false;
        _isShiftDown = false;
        _isHotkeyHolding = false;
        _isLeftMouseDown = false;
        _isMouseHolding = false;
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int message = wParam.ToInt32();
            NativeMethods.KBDLLHOOKSTRUCT kbdStruct = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);

            bool isKeyDown = message == NativeMethods.WM_KEYDOWN || message == NativeMethods.WM_SYSKEYDOWN;
            bool isKeyUp = message == NativeMethods.WM_KEYUP || message == NativeMethods.WM_SYSKEYUP;

            if (isKeyDown || isKeyUp)
            {
                Keys key = (Keys)kbdStruct.vkCode;
                bool stateChanged = ProcessKeyChange(key, isKeyDown);

                if (stateChanged)
                {
                    bool bothDown = _isCtrlDown && _isShiftDown;

                    if (bothDown && !_isHotkeyHolding)
                    {
                        _isHotkeyHolding = true;
                        HotkeyHoldStarted?.Invoke(this, EventArgs.Empty);
                    }
                    else if (!bothDown && _isHotkeyHolding)
                    {
                        _isHotkeyHolding = false;
                        HotkeyHoldEnded?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }

        return NativeMethods.CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
    }

    private bool ProcessKeyChange(Keys key, bool isDown)
    {
        if (key is Keys.LControlKey or Keys.RControlKey or Keys.ControlKey or Keys.Control)
        {
            if (_isCtrlDown != isDown)
            {
                _isCtrlDown = isDown;
                return true;
            }
        }
        else if (key is Keys.LShiftKey or Keys.RShiftKey or Keys.ShiftKey or Keys.Shift)
        {
            if (_isShiftDown != isDown)
            {
                _isShiftDown = isDown;
                return true;
            }
        }

        return false;
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int message = wParam.ToInt32();
            NativeMethods.MSLLHOOKSTRUCT mouseStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
            Point currentPoint = new Point(mouseStruct.pt.x, mouseStruct.pt.y);

            switch (message)
            {
                case NativeMethods.WM_LBUTTONDOWN:
                    _isLeftMouseDown = true;
                    _isMouseHolding = false;
                    _mouseDownPosition = currentPoint;
                    _mouseHoldTimer.Stop();
                    _mouseHoldTimer.Start();
                    break;

                case NativeMethods.WM_MOUSEMOVE:
                    if (_isLeftMouseDown && !_isMouseHolding)
                    {
                        int deltaX = Math.Abs(currentPoint.X - _mouseDownPosition.X);
                        int deltaY = Math.Abs(currentPoint.Y - _mouseDownPosition.Y);
                        if (deltaX > MoveTolerancePixels || deltaY > MoveTolerancePixels)
                        {
                            // Moved beyond tolerance before hold threshold — cancel hold timer
                            _mouseHoldTimer.Stop();
                        }
                    }
                    break;

                case NativeMethods.WM_LBUTTONUP:
                    _mouseHoldTimer.Stop();

                    if (_isMouseHolding)
                    {
                        _isMouseHolding = false;
                        ClickHoldEnded?.Invoke(this, currentPoint);
                    }

                    _isLeftMouseDown = false;
                    break;
            }
        }

        return NativeMethods.CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
    }

    private void OnMouseHoldTimerTick(object? sender, EventArgs e)
    {
        _mouseHoldTimer.Stop();

        if (_isLeftMouseDown && !_isMouseHolding)
        {
            _isMouseHolding = true;
            ClickHoldStarted?.Invoke(this, _mouseDownPosition);
        }
    }

    public void Dispose()
    {
        Stop();
        _mouseHoldTimer.Dispose();
        GC.SuppressFinalize(this);
    }

    private delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

    private static class NativeMethods
    {
        public const int WH_KEYBOARD_LL = 13;
        public const int WH_MOUSE_LL = 14;

        public const int WM_KEYDOWN = 0x0100;
        public const int WM_KEYUP = 0x0101;
        public const int WM_SYSKEYDOWN = 0x0104;
        public const int WM_SYSKEYUP = 0x0105;

        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_LBUTTONUP = 0x0202;
        public const int WM_MOUSEMOVE = 0x0200;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
