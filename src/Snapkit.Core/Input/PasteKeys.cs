namespace Snapkit.Core.Input;

/// <summary>
/// Программные нажатия для вставки. Зажатые Shift, Alt и Win не должны влиять на Ctrl+V:
/// в Chrome и Claude Desktop Ctrl+Shift+V вставляет только текст, и картинки пропадут.
/// </summary>
public static class PasteKeys
{
    private static readonly (Mod Mod, int Vk)[] Released =
    [
        (Mod.LShift, Vk.LShift), (Mod.RShift, Vk.RShift),
        (Mod.LAlt, Vk.LAlt), (Mod.RAlt, Vk.RAlt),
        (Mod.LWin, Vk.LWin), (Mod.RWin, Vk.RWin),
    ];

    /// <summary>Отпустить мешающие модификаторы, затем Ctrl+V (Ctrl — только если он ещё не нажат).</summary>
    public static IReadOnlyList<KeyStroke> CtrlV(Mod logicalHeld)
    {
        var strokes = new List<KeyStroke>();
        foreach (var (mod, vk) in Released)
        {
            if (logicalHeld.HasFlag(mod)) strokes.Add(new KeyStroke(vk, Up: true));
        }

        var ctrlHeld = ModKeys.HasCtrl(logicalHeld);
        if (!ctrlHeld) strokes.Add(new KeyStroke(Vk.LCtrl, Up: false));
        strokes.Add(new KeyStroke(Vk.V, Up: false));
        strokes.Add(new KeyStroke(Vk.V, Up: true));
        if (!ctrlHeld) strokes.Add(new KeyStroke(Vk.LCtrl, Up: true));
        return strokes;
    }

    /// <summary>
    /// После вставки: вернуть Shift, который пользователь всё ещё держит рукой,
    /// и отпустить Ctrl, который считается нажатым, хотя на клавиатуре отпущен.
    /// </summary>
    public static IReadOnlyList<KeyStroke> Restore(Mod physicalHeld, Mod logicalNow)
    {
        var strokes = new List<KeyStroke>();
        foreach (var (mod, vk) in new[] { (Mod.LShift, Vk.LShift), (Mod.RShift, Vk.RShift) })
        {
            if (physicalHeld.HasFlag(mod) && !logicalNow.HasFlag(mod)) strokes.Add(new KeyStroke(vk, Up: false));
        }
        foreach (var (mod, vk) in new[] { (Mod.LCtrl, Vk.LCtrl), (Mod.RCtrl, Vk.RCtrl) })
        {
            if (logicalNow.HasFlag(mod) && !physicalHeld.HasFlag(mod)) strokes.Add(new KeyStroke(vk, Up: true));
        }
        return strokes;
    }
}
