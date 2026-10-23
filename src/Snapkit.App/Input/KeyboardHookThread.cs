using System.Runtime.InteropServices;
using Snapkit.Core.Input;

namespace Snapkit.App.Input;

/// <summary>
/// Глобальный хук клавиатуры (WH_KEYBOARD_LL) на собственном потоке с циклом сообщений.
/// Обработчик отвечает мгновенно и никогда не ждёт UI-поток: иначе Windows снимет хук,
/// а у пользователя начнутся задержки при наборе.
/// </summary>
public sealed partial class KeyboardHookThread(KeyRouter router, Action<KeyAction> onAction) : IDisposable
{
    private readonly object _gate = new();
    private readonly ManualResetEventSlim _started = new();
    private Thread? _thread;
    private uint _threadId;
    private nint _hook;
    // Делегат хранится в поле, иначе сборщик мусора освободит его, пока Windows его вызывает
    private HookProc? _proc;

    public void Start()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "Snapkit.KeyboardHook" };
        _thread.Start();
        _started.Wait(TimeSpan.FromSeconds(3));
        if (_hook == 0) throw new InvalidOperationException("Не удалось установить перехват клавиатуры.");
    }

    public void Dispose()
    {
        if (_threadId != 0) PostThreadMessageW(_threadId, WM_QUIT, 0, 0);
        _thread?.Join(TimeSpan.FromSeconds(2));
    }

    private void Run()
    {
        _threadId = GetCurrentThreadId();
        _proc = OnHook;
        _hook = SetWindowsHookExW(WH_KEYBOARD_LL, Marshal.GetFunctionPointerForDelegate(_proc), GetModuleHandleW(null), 0);
        _started.Set();
        if (_hook == 0) return;

        while (GetMessageW(out var message, 0, 0, 0) > 0)
        {
            TranslateMessage(in message);
            DispatchMessageW(in message);
        }
        UnhookWindowsHookEx(_hook);
    }

    private nint OnHook(int code, nint wParam, nint lParam)
    {
        if (code >= 0)
        {
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            var input = new KeyInput(
                (int)data.vkCode,
                IsDown: wParam is WM_KEYDOWN or WM_SYSKEYDOWN,
                IsInjected: (data.flags & LLKHF_INJECTED) != 0,
                IsOwn: data.dwExtraInfo == InputSender.Marker);

            KeyDecision decision;
            try
            {
                lock (_gate) decision = router.Process(input);
            }
            catch
            {
                // Ошибка в маршрутизации не должна ломать клавиатуру пользователю
                decision = default;
            }

            if (decision.Action != KeyAction.None) onAction(decision.Action);
            if (decision.Suppress) return 1;
        }
        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    private delegate nint HookProc(int code, nint wParam, nint lParam);

    private const int WH_KEYBOARD_LL = 13;
    private const nint WM_KEYDOWN = 0x0100;
    private const nint WM_SYSKEYDOWN = 0x0104;
    private const uint WM_QUIT = 0x0012;
    private const uint LLKHF_INJECTED = 0x10;

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public nint hwnd;
        public uint message;
        public nint wParam;
        public nint lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint SetWindowsHookExW(int idHook, nint lpfn, nint hMod, uint dwThreadId);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnhookWindowsHookEx(nint hhk);

    [LibraryImport("user32.dll")]
    private static partial nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint GetModuleHandleW(string? lpModuleName);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetCurrentThreadId();

    [LibraryImport("user32.dll")]
    private static partial int GetMessageW(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool TranslateMessage(in MSG lpMsg);

    [LibraryImport("user32.dll")]
    private static partial nint DispatchMessageW(in MSG lpMsg);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostThreadMessageW(uint idThread, uint msg, nint wParam, nint lParam);
}
