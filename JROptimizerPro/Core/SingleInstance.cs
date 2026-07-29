using System.Runtime.InteropServices;

namespace JROptimizerPro.Core;

internal static class SingleInstance
{
    public const string MutexName = "JR_Optimizer_Pro_2_SingleInstance";

    public static void RestoreExistingWindow()
    {
        EnumWindows((handle, _) =>
        {
            var length = GetWindowTextLength(handle);
            if (length <= 0)
                return true;

            var title = new System.Text.StringBuilder(length + 1);
            GetWindowText(handle, title, title.Capacity);
            if (!title.ToString().StartsWith("JR Optimizer Pro", StringComparison.OrdinalIgnoreCase))
                return true;

            ShowWindow(handle, 9);
            SetForegroundWindow(handle);
            return false;
        }, IntPtr.Zero);
    }

    private delegate bool EnumWindowsProc(IntPtr handle, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr handle, System.Text.StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr handle, int command);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr handle);
}
