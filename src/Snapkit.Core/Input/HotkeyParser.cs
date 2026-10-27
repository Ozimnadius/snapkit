namespace Snapkit.Core.Input;

/// <summary>
/// Сочетания клавиш в настройках: «PrintScreen», «Ctrl+F12», «Ctrl+Alt+S».
/// Модификаторы — Ctrl, Alt, Shift, Win; клавиша — Print Screen, F1–F24, буква A–Z или цифра 0–9.
/// </summary>
public static class HotkeyParser
{
    public static readonly Hotkey Default = new(HotkeyMods.None, Vk.Snapshot);

    public static Hotkey? Parse(string text)
    {
        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || parts.Any(p => p.Length == 0)) return null;

        var mods = HotkeyMods.None;
        foreach (var part in parts[..^1])
        {
            var mod = ModifierOf(part);
            if (mod is null || mods.HasFlag(mod.Value)) return null;
            mods |= mod.Value;
        }
        return KeyOf(parts[^1]) is { } vk ? new Hotkey(mods, vk) : null;
    }

    /// <summary>Список через запятую; непонятные пропускаются. Пустой — Print Screen, если orPrintScreen, иначе пустой.</summary>
    public static IReadOnlyList<Hotkey> ParseList(string text, bool orPrintScreen = true)
    {
        var hotkeys = text.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(Parse)
            .OfType<Hotkey>()
            .Distinct()
            .ToList();
        return hotkeys.Count > 0 || !orPrintScreen ? hotkeys : [Default];
    }

    /// <summary>Обратно в текст для файла: «Print Screen, Ctrl+F12» — разбирается снова тем же ParseList.</summary>
    public static string FormatList(IEnumerable<Hotkey> hotkeys) => string.Join(", ", hotkeys.Select(Display));

    /// <summary>Клавиша, которую понимает разбор: Print Screen, F1–F24, A–Z, 0–9.</summary>
    public static bool IsSupportedKey(int vk)
        => vk == Vk.Snapshot || vk is >= 0x70 and <= 0x87 || vk is >= 'A' and <= 'Z' || vk is >= '0' and <= '9';

    /// <summary>Как показывать пользователю: «Print Screen», «Ctrl+F12».</summary>
    public static string Display(Hotkey hotkey)
    {
        var parts = new List<string>();
        if (hotkey.Modifiers.HasFlag(HotkeyMods.Ctrl)) parts.Add("Ctrl");
        if (hotkey.Modifiers.HasFlag(HotkeyMods.Alt)) parts.Add("Alt");
        if (hotkey.Modifiers.HasFlag(HotkeyMods.Shift)) parts.Add("Shift");
        if (hotkey.Modifiers.HasFlag(HotkeyMods.Win)) parts.Add("Win");
        parts.Add(KeyName(hotkey.Vk));
        return string.Join("+", parts);
    }

    private static HotkeyMods? ModifierOf(string name) => name.ToLowerInvariant() switch
    {
        "ctrl" or "control" => HotkeyMods.Ctrl,
        "alt" => HotkeyMods.Alt,
        "shift" => HotkeyMods.Shift,
        "win" => HotkeyMods.Win,
        _ => null,
    };

    private static int? KeyOf(string name)
    {
        var lower = name.ToLowerInvariant().Replace(" ", "");
        if (lower is "printscreen" or "prtsc" or "prtscn") return Vk.Snapshot;
        if (lower.Length >= 2 && lower[0] == 'f' && int.TryParse(lower[1..], out var f) && f is >= 1 and <= 24) return 0x70 + f - 1;
        if (name.Length == 1 && char.ToUpperInvariant(name[0]) is var c and >= 'A' and <= 'Z') return c;
        if (name.Length == 1 && name[0] is >= '0' and <= '9') return name[0];
        return null;
    }

    private static string KeyName(int vk) => vk switch
    {
        Vk.Snapshot => "Print Screen",
        >= 0x70 and <= 0x87 => $"F{vk - 0x70 + 1}",
        _ => ((char)vk).ToString(),
    };
}
