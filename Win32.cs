using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Penopticon;

internal static class Win32
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const int VK_LBUTTON = 0x01;
    private const int VK_RBUTTON = 0x02;
    private const int VK_MBUTTON = 0x04;

    private static IntPtr _cachedWindow;
    private static string? _cachedName;

    /// <summary>
    /// Process name (without ".exe") owning the foreground window, or null when there is
    /// no foreground window or its process cannot be inspected.
    /// </summary>
    public static string? GetForegroundProcessName()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return null;
        if (hwnd == _cachedWindow)
            return _cachedName;

        _cachedWindow = hwnd;
        _cachedName = null;
        GetWindowThreadProcessId(hwnd, out var pid);
        if (pid != 0)
        {
            try
            {
                using var process = Process.GetProcessById((int)pid);
                _cachedName = process.ProcessName;
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
        }
        return _cachedName;
    }

    /// <summary>
    /// True while any mouse button (including a pen tip promoted to a click) is held.
    /// </summary>
    public static bool AnyMouseButtonDown() =>
        IsDown(VK_LBUTTON) || IsDown(VK_RBUTTON) || IsDown(VK_MBUTTON);

    private static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;
}
