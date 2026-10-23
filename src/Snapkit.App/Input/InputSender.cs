using System.ComponentModel;
using System.Runtime.InteropServices;
using Snapkit.Core.Input;

namespace Snapkit.App.Input;

/// <summary>
/// Программные нажатия клавиш одним вызовом SendInput — так их не перемешает с вводом пользователя.
/// Каждое нажатие помечено Marker, чтобы свой же хук клавиатуры его не перехватил.
/// </summary>
public static partial class InputSender
{
    /// <summary>«SNKT» — метка нажатий Snapkit в dwExtraInfo.</summary>
    public const nuint Marker = 0x534E4B54;

    public static void Send(IReadOnlyList<KeyStroke> strokes)
    {
        if (strokes.Count == 0) return;
        var inputs = new INPUT[strokes.Count];
        for (var i = 0; i < strokes.Count; i++)
        {
            var (vk, up) = (strokes[i].Vk, strokes[i].Up);
            inputs[i] = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = (ushort)vk,
                        wScan = (ushort)MapVirtualKeyW((uint)vk, MAPVK_VK_TO_VSC),
                        dwFlags = (up ? KEYEVENTF_KEYUP : 0) | (IsExtended(vk) ? KEYEVENTF_EXTENDEDKEY : 0),
                        dwExtraInfo = Marker,
                    },
                },
            };
        }
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length) throw new Win32Exception(Marshal.GetLastPInvokeError(), "Windows не приняла программные нажатия клавиш.");
    }

    private static bool IsExtended(int vk) => vk is Vk.RCtrl or Vk.RAlt or Vk.LWin or Vk.RWin;

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint MAPVK_VK_TO_VSC = 0;

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial uint SendInput(uint cInputs, [In] INPUT[] pInputs, int cbSize);

    [LibraryImport("user32.dll")]
    private static partial uint MapVirtualKeyW(uint uCode, uint uMapType);
}
