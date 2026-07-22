using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OrbsWin.Tools;

public class CaffeineToggleService
{
    public static CaffeineToggleService Instance { get; } = new();

    public bool IsEnabled { get; private set; }

    private CaffeineToggleService() { }

    public bool Toggle(Action<string, string> showNotification)
    {
        IsEnabled = !IsEnabled;

        if (IsEnabled)
        {
            uint result = NativeMethods.SetThreadExecutionState(
                NativeMethods.ES_CONTINUOUS |
                NativeMethods.ES_SYSTEM_REQUIRED |
                NativeMethods.ES_DISPLAY_REQUIRED);

            if (result == 0)
            {
                IsEnabled = false;
                Debug.WriteLine("[CaffeineToggleService] Failed to set thread execution state.");
                showNotification("Caffeine Failed", "Could not prevent system sleep.");
            }
            else
            {
                Debug.WriteLine("[CaffeineToggleService] Caffeine enabled.");
                showNotification("Caffeine Enabled", "System sleep and display turn-off are now prevented.");
            }
        }
        else
        {
            NativeMethods.SetThreadExecutionState(NativeMethods.ES_CONTINUOUS);
            Debug.WriteLine("[CaffeineToggleService] Caffeine disabled.");
            showNotification("Caffeine Disabled", "System sleep behavior restored to normal.");
        }

        return IsEnabled;
    }

    public void Disable()
    {
        if (IsEnabled)
        {
            NativeMethods.SetThreadExecutionState(NativeMethods.ES_CONTINUOUS);
            IsEnabled = false;
        }
    }

    private static class NativeMethods
    {
        public const uint ES_SYSTEM_REQUIRED = 0x00000001;
        public const uint ES_DISPLAY_REQUIRED = 0x00000002;
        public const uint ES_CONTINUOUS = 0x80000000;

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern uint SetThreadExecutionState(uint esFlags);
    }
}
