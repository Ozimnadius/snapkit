using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
using Snapkit.App.Clipboarding;

namespace Snapkit.App.Tests;

/// <summary>
/// ClipboardService на отдельном STA-потоке с циклом сообщений — как в программе.
/// Без цикла сообщений Windows не смогла бы запросить у него отложенные данные.
/// </summary>
internal sealed class ClipboardTestHost : IDisposable
{
    private readonly Thread _thread;
    private readonly Dispatcher _dispatcher;

    public ClipboardTestHost()
    {
        Dispatcher? dispatcher = null;
        ClipboardService? service = null;
        using var ready = new ManualResetEventSlim();
        _thread = new Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            service = new ClipboardService();
            ready.Set();
            Dispatcher.Run();
        });
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.IsBackground = true;
        _thread.Start();
        ready.Wait();
        _dispatcher = dispatcher!;
        Service = service!;
    }

    public ClipboardService Service { get; }

    /// <summary>Выполнить асинхронное действие на потоке сервиса и дождаться его.</summary>
    public void Run(Func<Task> action) => _dispatcher.InvokeAsync(action).Task.Unwrap().GetAwaiter().GetResult();

    public void Dispose()
    {
        _dispatcher.Invoke(Service.Dispose);
        _dispatcher.InvokeShutdown();
        _thread.Join();
    }
}

/// <summary>«Чужая программа»: своё окно, читает и пишет буфер обмена напрямую через Win32.</summary>
internal sealed partial class ForeignClipboard : IDisposable
{
    public const uint CF_UNICODETEXT = 13;
    private readonly nint _hwnd = CreateWindowExW(0, "STATIC", "SnapkitTestReader", 0, 0, 0, 0, 0, HWND_MESSAGE, 0, 0, 0);

    public static uint Format(string name) => RegisterClipboardFormatW(name);

    public byte[]? Read(uint format)
    {
        if (!Open()) throw new InvalidOperationException("Буфер обмена занят");
        try
        {
            var handle = GetClipboardData(format);
            if (handle == 0) return null;
            var size = (int)GlobalSize(handle);
            var pointer = GlobalLock(handle);
            try
            {
                var bytes = new byte[size];
                Marshal.Copy(pointer, bytes, 0, size);
                return bytes;
            }
            finally { GlobalUnlock(handle); }
        }
        finally { CloseClipboard(); }
    }

    public bool HasFormat(string name) => IsClipboardFormatAvailable(Format(name));

    /// <summary>Прочитать формат, открыв буфер без своего окна — так делает Chrome.</summary>
    public void ReadAnonymously(uint format)
    {
        var opened = false;
        for (var i = 0; i < 20 && !(opened = OpenClipboard(0)); i++) Thread.Sleep(25);
        if (!opened) throw new InvalidOperationException("Буфер обмена занят");
        try { GetClipboardData(format); }
        finally { CloseClipboard(); }
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsClipboardFormatAvailable(uint format);

    public string? ReadText()
        => Read(CF_UNICODETEXT) is { } bytes ? Encoding.Unicode.GetString(bytes).TrimEnd('\0') : null;

    public void WriteText(string text)
    {
        if (!Open()) throw new InvalidOperationException("Буфер обмена занят");
        try
        {
            EmptyClipboard();
            var bytes = Encoding.Unicode.GetBytes(text + "\0");
            var handle = GlobalAlloc(GMEM_MOVEABLE, (nuint)bytes.Length);
            Marshal.Copy(bytes, 0, GlobalLock(handle), bytes.Length);
            GlobalUnlock(handle);
            SetClipboardData(CF_UNICODETEXT, handle);
        }
        finally { CloseClipboard(); }
    }

    /// <summary>Открыть буфер и держать его, пока не вызван Release.</summary>
    public void Hold()
    {
        if (!Open()) throw new InvalidOperationException("Буфер обмена занят");
    }

    public void Release() => CloseClipboard();

    public void Dispose() => DestroyWindow(_hwnd);

    private bool Open()
    {
        for (var i = 0; i < 20; i++)
        {
            if (OpenClipboard(_hwnd)) return true;
            Thread.Sleep(25);
        }
        return false;
    }

    private const uint GMEM_MOVEABLE = 0x0002;
    private static readonly nint HWND_MESSAGE = -3;

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint CreateWindowExW(uint exStyle, string className, string windowName, uint style,
        int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyWindow(nint hWnd);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint RegisterClipboardFormatW(string name);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenClipboard(nint hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseClipboard();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EmptyClipboard();

    [LibraryImport("user32.dll")]
    private static partial nint GetClipboardData(uint format);

    [LibraryImport("user32.dll")]
    private static partial nint SetClipboardData(uint format, nint hMem);

    [LibraryImport("kernel32.dll")]
    private static partial nint GlobalAlloc(uint flags, nuint bytes);

    [LibraryImport("kernel32.dll")]
    private static partial nint GlobalLock(nint hMem);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalUnlock(nint hMem);

    [LibraryImport("kernel32.dll")]
    private static partial nuint GlobalSize(nint hMem);
}
