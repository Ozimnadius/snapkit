using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Snapkit.App.Interop;

/// <summary>Вызовы Win32, которых нет в WPF.</summary>
internal static partial class NativeMethods
{
    public const int GWL_EXSTYLE = -20;
    public const nint WS_EX_TOOLWINDOW = 0x80;
    public const nint WS_EX_NOACTIVATE = 0x08000000;

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static partial nint GetWindowLongPtr(nint hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    public static partial nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);
}

internal static class WindowExtensions
{
    /// <summary>Окно не забирает фокус при показе и клике и не видно в Alt+Tab.</summary>
    public static void MakeNoActivate(this Window window)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        var style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE,
            style | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW);
    }
}
