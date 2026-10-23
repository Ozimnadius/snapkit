using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Snapkit.App.Interop;

/// <summary>
/// Активное окно. Windows не даёт фоновой программе забрать фокус, а снимок по Print Screen
/// начинается, когда активна чужая программа. Поэтому поток окна снимка на миг подключается
/// к потоку активного окна — так Windows разрешает переключить фокус.
/// </summary>
internal static partial class Foreground
{
    public static nint Current => GetForegroundWindow();

    /// <summary>Активное окно принадлежит самому Snapkit.</summary>
    public static bool IsOwn()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == 0) return false;
        GetWindowThreadProcessId(hwnd, out var pid);
        return pid == (uint)Environment.ProcessId;
    }

    /// <summary>Процесс, которому принадлежит окно.</summary>
    public static int ProcessOf(nint hwnd)
    {
        GetWindowThreadProcessId(hwnd, out var pid);
        return (int)pid;
    }

    public static void Activate(Window window)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        var foreground = GetForegroundWindow();
        var foregroundThread = foreground == 0 ? 0 : GetWindowThreadProcessId(foreground, out _);
        var ownThread = GetCurrentThreadId();
        var attached = foregroundThread != 0 && foregroundThread != ownThread && AttachThreadInput(ownThread, foregroundThread, true);
        try
        {
            SetForegroundWindow(hwnd);
            BringWindowToTop(hwnd);
            window.Activate();
            window.Focus();
        }
        finally
        {
            if (attached) AttachThreadInput(ownThread, foregroundThread, false);
        }
    }

    public static void Restore(nint hwnd)
    {
        if (hwnd != 0) SetForegroundWindow(hwnd);
    }

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool BringWindowToTop(nint hWnd);

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetCurrentThreadId();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);
}
