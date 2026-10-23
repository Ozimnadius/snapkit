using System.Runtime.InteropServices;
using Snapkit.Core.Input;

namespace Snapkit.App.Input;

/// <summary>Какие модификаторы нажаты с точки зрения Windows — для сверки состояния перехвата.</summary>
internal static partial class ModifierState
{
    private static readonly (Mod Mod, int Vk)[] Keys =
    [
        (Mod.LShift, Vk.LShift), (Mod.RShift, Vk.RShift),
        (Mod.LCtrl, Vk.LCtrl), (Mod.RCtrl, Vk.RCtrl),
        (Mod.LAlt, Vk.LAlt), (Mod.RAlt, Vk.RAlt),
        (Mod.LWin, Vk.LWin), (Mod.RWin, Vk.RWin),
    ];

    public static Mod Read()
    {
        var state = Mod.None;
        foreach (var (mod, vk) in Keys)
        {
            if ((GetAsyncKeyState(vk) & 0x8000) != 0) state |= mod;
        }
        return state;
    }

    [LibraryImport("user32.dll")]
    private static partial short GetAsyncKeyState(int vKey);
}
