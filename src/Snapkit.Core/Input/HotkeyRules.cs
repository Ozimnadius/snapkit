using Snapkit.Core.Settings;

namespace Snapkit.Core.Input;

/// <summary>Что можно назначить на снимок и какие клавиши к какому действию (раздел 2.3 описания этапа 4).</summary>
public static class HotkeyRules
{
    /// <summary>Сколько сочетаний на одно действие.</summary>
    public const int PerAction = 2;

    /// <summary>null — сочетание годится; иначе текст для пользователя.</summary>
    public static string? Check(Hotkey hotkey)
    {
        if (!HotkeyParser.IsSupportedKey(hotkey.Vk)) return "Эту клавишу назначить нельзя";
        // Print Screen и F1–F12 можно и без модификаторов, остальное — только с Ctrl, Alt или Shift
        var standsAlone = hotkey.Vk == Vk.Snapshot || hotkey.Vk is >= 0x70 and <= 0x7B;
        var hasModifier = (hotkey.Modifiers & (HotkeyMods.Ctrl | HotkeyMods.Alt | HotkeyMods.Shift)) != 0;
        return standsAlone || hasModifier ? null : "Добавьте Ctrl, Alt или Shift";
    }

    /// <summary>Ошибки по полям (до двух на действие); повтор в любом другом поле — «Это сочетание уже назначено».</summary>
    public static IReadOnlyList<string?> CheckAll(IReadOnlyList<Hotkey?> fields)
    {
        var errors = new string?[fields.Count];
        for (var i = 0; i < fields.Count; i++)
        {
            if (fields[i] is not { } hotkey) continue;
            errors[i] = Check(hotkey)
                        ?? (fields.Take(i).Contains(hotkey) ? "Это сочетание уже назначено" : null);
        }
        return errors;
    }

    /// <summary>Какие клавиши к какому действию: выключенное действие и пустые поля не попадают.</summary>
    public static IReadOnlyDictionary<Hotkey, KeyAction> BuildMap(AppSettings settings)
    {
        var map = new Dictionary<Hotkey, KeyAction>();
        if (settings.CaptureHotkeysEnabled) Add(map, HotkeyParser.ParseList(settings.CaptureHotkeys), KeyAction.CaptureRegion);
        if (settings.FullScreenHotkeysEnabled)
            Add(map, HotkeyParser.ParseList(settings.FullScreenHotkeys, orPrintScreen: false), KeyAction.CaptureFullScreen);
        return map;
    }

    // При повторе сочетания остаётся первое действие
    private static void Add(Dictionary<Hotkey, KeyAction> map, IReadOnlyList<Hotkey> hotkeys, KeyAction action)
    {
        foreach (var hotkey in hotkeys.Take(PerAction)) map.TryAdd(hotkey, action);
    }
}
