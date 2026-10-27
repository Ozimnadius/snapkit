namespace Snapkit.Core.Input;

/// <summary>Коды виртуальных клавиш Windows, которые нужны программе.</summary>
public static class Vk
{
    public const int LShift = 0xA0, RShift = 0xA1, LCtrl = 0xA2, RCtrl = 0xA3,
        LAlt = 0xA4, RAlt = 0xA5, LWin = 0x5B, RWin = 0x5C,
        V = 0x56, Snapshot = 0x2C, Escape = 0x1B;
}

/// <summary>Модификаторы с учётом стороны клавиатуры.</summary>
[Flags]
public enum Mod { None = 0, LShift = 1, RShift = 2, LCtrl = 4, RCtrl = 8, LAlt = 16, RAlt = 32, LWin = 64, RWin = 128 }

/// <summary>Модификаторы горячей клавиши — без стороны.</summary>
[Flags]
public enum HotkeyMods { None = 0, Ctrl = 1, Alt = 2, Shift = 4, Win = 8 }

public readonly record struct Hotkey(HotkeyMods Modifiers, int Vk);

/// <summary>
/// Событие клавиатуры из хука. IsInjected — нажатие сгенерировано программой
/// (например, AutoHotkey); IsOwn — его сгенерировал сам Snapkit.
/// </summary>
public readonly record struct KeyInput(int Vk, bool IsDown, bool IsInjected, bool IsOwn);

public enum KeyAction { None, CaptureRegion, CaptureFullScreen, PasteBatch }

/// <summary>Suppress — не пропускать нажатие дальше в систему.</summary>
public readonly record struct KeyDecision(bool Suppress, KeyAction Action);

/// <summary>
/// Условия перехвата Ctrl+V (раздел 7.2 описания проекта). HasPasteContent — в скопированном пакете
/// есть что вставить (снимки ленты или один скопированный снимок).
/// </summary>
public readonly record struct PasteContext(bool HasPasteContent, bool PackageIsCurrent, bool ForegroundIsOwn)
{
    public bool AllowsIntercept => HasPasteContent && PackageIsCurrent && !ForegroundIsOwn;
}

/// <summary>Одно программное нажатие или отпускание клавиши.</summary>
public readonly record struct KeyStroke(int Vk, bool Up);

internal static class ModKeys
{
    public static readonly (Mod Mod, int Vk)[] All =
    [
        (Mod.LShift, Vk.LShift), (Mod.RShift, Vk.RShift),
        (Mod.LCtrl, Vk.LCtrl), (Mod.RCtrl, Vk.RCtrl),
        (Mod.LAlt, Vk.LAlt), (Mod.RAlt, Vk.RAlt),
        (Mod.LWin, Vk.LWin), (Mod.RWin, Vk.RWin),
    ];

    public static Mod FromVk(int vk)
    {
        foreach (var (mod, key) in All) if (key == vk) return mod;
        return Mod.None;
    }

    public static bool HasCtrl(Mod m) => (m & (Mod.LCtrl | Mod.RCtrl)) != 0;
    public static bool HasAlt(Mod m) => (m & (Mod.LAlt | Mod.RAlt)) != 0;
    public static bool HasShift(Mod m) => (m & (Mod.LShift | Mod.RShift)) != 0;
    public static bool HasWin(Mod m) => (m & (Mod.LWin | Mod.RWin)) != 0;

    public static HotkeyMods Summary(Mod m)
        => (HasCtrl(m) ? HotkeyMods.Ctrl : 0) | (HasAlt(m) ? HotkeyMods.Alt : 0)
         | (HasShift(m) ? HotkeyMods.Shift : 0) | (HasWin(m) ? HotkeyMods.Win : 0);
}
