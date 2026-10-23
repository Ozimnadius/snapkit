using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Interop;
using Snapkit.App.Interop;
using Snapkit.App.Shell;

namespace Snapkit.App.Clipboarding;

public enum ClipboardKind { None, Package, SingleCapture, PasteStep }

/// <summary>Что положить в буфер. Пустые части не публикуются.</summary>
public sealed record ClipboardPayload(byte[]? Png, byte[]? Dib, string? Text);

public sealed class ClipboardBusyException() : Exception("Буфер обмена занят другой программой.");

/// <summary>
/// Буфер обмена через Win32 с отложенной выдачей: в буфер кладутся только названия форматов,
/// а данные Windows запрашивает у нас (WM_RENDERFORMAT), когда кто-то их читает.
/// Так видно, какой процесс забрал картинку — это и есть подтверждение вставки.
/// Создаётся на UI-потоке: скрытому окну нужен цикл сообщений.
/// </summary>
public sealed partial class ClipboardService : IDisposable
{
    private const int OpenAttempts = 10;
    private static readonly TimeSpan OpenRetryDelay = TimeSpan.FromMilliseconds(30);

    private readonly HwndSource _source;
    private readonly nint _hwnd;
    private readonly uint _pngFormat;
    private readonly uint _excludeFormat;
    private readonly uint _viewerIgnoreFormat;
    private readonly object _expectGate = new();

    private ClipboardPayload? _payload;
    // Читается с потока хука клавиатуры
    private volatile int _kind;
    private (int Pid, TaskCompletionSource<bool> Done)? _expected;

    public ClipboardService()
    {
        _source = new HwndSource(new HwndSourceParameters("SnapkitClipboard")
        {
            ParentWindow = HWND_MESSAGE,
            WindowStyle = 0,
            Width = 0,
            Height = 0,
        });
        _source.AddHook(WndProc);
        _hwnd = _source.Handle;
        _pngFormat = RegisterClipboardFormatW("PNG");
        _excludeFormat = RegisterClipboardFormatW("ExcludeClipboardContentFromMonitorProcessing");
        _viewerIgnoreFormat = RegisterClipboardFormatW("Clipboard Viewer Ignore");
        AddClipboardFormatListener(_hwnd);
    }

    /// <summary>Содержимое буфера обмена изменилось (любой программой).</summary>
    public event EventHandler? ClipboardChanged;

    /// <summary>
    /// Положить данные в буфер. excludeFromHistory — не сохранять в журнале Win+V и в менеджерах
    /// буфера вроде Clipdiary (промежуточные картинки при вставке пачки): иначе они читают картинку
    /// раньше чата, и вставку не удаётся подтвердить. Буфер занят дольше ~300 мс — ClipboardBusyException.
    /// </summary>
    public async Task PublishAsync(ClipboardPayload payload, ClipboardKind kind, bool excludeFromHistory)
    {
        for (var attempt = 1; !OpenClipboard(_hwnd); attempt++)
        {
            if (attempt >= OpenAttempts) throw new ClipboardBusyException();
            await Task.Delay(OpenRetryDelay);
        }
        try
        {
            EmptyClipboard();
            _payload = payload;
            _kind = (int)kind;
            if (payload.Png is not null) SetClipboardData(_pngFormat, 0);
            if (payload.Dib is not null) SetClipboardData(CF_DIB, 0);
            if (payload.Text is not null) SetClipboardData(CF_UNICODETEXT, 0);
            if (excludeFromHistory)
            {
                SetClipboardData(_excludeFormat, GlobalBytes(new byte[4]));
                SetClipboardData(_viewerIgnoreFormat, GlobalBytes(new byte[4]));
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    /// <summary>
    /// В буфере до сих пор наш пакет: после снимка пользователь ничего не копировал.
    /// Можно вызывать с любого потока.
    /// </summary>
    public bool IsPackageCurrent() => (ClipboardKind)_kind == ClipboardKind.Package && GetClipboardOwner() == _hwnd;

    /// <summary>
    /// Ждать, пока данные из буфера заберёт процесс targetPid или его дочерний процесс.
    /// Вызывать до PublishAsync. true — забрал; false — за время ожидания не забрал.
    /// </summary>
    public Task<bool> ExpectRender(int targetPid, TimeSpan timeout)
    {
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_expectGate) _expected = (targetPid, done);
        _ = Task.Delay(timeout).ContinueWith(_ =>
        {
            lock (_expectGate)
            {
                if (_expected?.Done == done) _expected = null;
            }
            done.TrySetResult(false);
        }, TaskScheduler.Default);
        return done.Task;
    }

    public void Dispose()
    {
        RemoveClipboardFormatListener(_hwnd);
        // При закрытии окна Windows попросит выдать все отложенные данные (WM_RENDERALLFORMATS)
        _source.Dispose();
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        switch ((uint)msg)
        {
            case WM_RENDERFORMAT:
                ConfirmIfExpected();
                Render((uint)wParam);
                handled = true;
                break;
            case WM_RENDERALLFORMATS:
                if (OpenClipboard(_hwnd))
                {
                    if (GetClipboardOwner() == _hwnd)
                    {
                        foreach (var format in new[] { _pngFormat, CF_DIB, CF_UNICODETEXT }) Render(format);
                    }
                    CloseClipboard();
                }
                handled = true;
                break;
            case WM_DESTROYCLIPBOARD:
                // Буфер очистил кто-то другой — нашего пакета там больше нет
                _kind = (int)ClipboardKind.None;
                handled = true;
                break;
            case WM_CLIPBOARDUPDATE:
                ClipboardChanged?.Invoke(this, EventArgs.Empty);
                handled = true;
                break;
        }
        return 0;
    }

    // Данные запросил процесс, который открыл буфер; если это получатель вставки
    // (или его дочерний процесс) — вставка подтверждена
    private void ConfirmIfExpected()
    {
        int expectedPid;
        lock (_expectGate)
        {
            if (_expected is not { } expected) return;
            expectedPid = expected.Pid;
        }

        var opener = GetOpenClipboardWindow();
        var pid = 0u;
        if (opener != 0) GetWindowThreadProcessId(opener, out pid);
        if (!ProcessTree.IsSameOrDescendant((int)pid, expectedPid))
        {
            // Для разбора на месте: кто на самом деле прочитал буфер во время вставки
            Log.Info($"Буфер во время вставки прочитал не получатель: {ProcessTree.NameOf((int)pid)} ({pid}), " +
                     $"ждали {ProcessTree.NameOf(expectedPid)} ({expectedPid})");
            return;
        }

        TaskCompletionSource<bool>? done = null;
        lock (_expectGate)
        {
            if (_expected is { } expected && expected.Pid == expectedPid)
            {
                done = expected.Done;
                _expected = null;
            }
        }
        done?.TrySetResult(true);
    }

    private void Render(uint format)
    {
        var payload = _payload;
        byte[]? bytes = null;
        if (format == _pngFormat) bytes = payload?.Png;
        else if (format == CF_DIB) bytes = payload?.Dib;
        else if (format == CF_UNICODETEXT && payload?.Text is { } text)
            bytes = Encoding.Unicode.GetBytes(text.ReplaceLineEndings("\r\n") + "\0");
        if (bytes is not null) SetClipboardData(format, GlobalBytes(bytes));
    }

    private static nint GlobalBytes(byte[] bytes)
    {
        var handle = GlobalAlloc(GMEM_MOVEABLE, (nuint)Math.Max(1, bytes.Length));
        var pointer = GlobalLock(handle);
        Marshal.Copy(bytes, 0, pointer, bytes.Length);
        GlobalUnlock(handle);
        return handle;
    }

    private static readonly nint HWND_MESSAGE = -3;
    private const uint CF_DIB = 8;
    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;
    private const uint WM_RENDERFORMAT = 0x0305;
    private const uint WM_RENDERALLFORMATS = 0x0306;
    private const uint WM_DESTROYCLIPBOARD = 0x0307;
    private const uint WM_CLIPBOARDUPDATE = 0x031D;

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint RegisterClipboardFormatW(string name);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenClipboard(nint hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseClipboard();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EmptyClipboard();

    [LibraryImport("user32.dll")]
    private static partial nint SetClipboardData(uint format, nint hMem);

    [LibraryImport("user32.dll")]
    private static partial nint GetClipboardOwner();

    [LibraryImport("user32.dll")]
    private static partial nint GetOpenClipboardWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AddClipboardFormatListener(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RemoveClipboardFormatListener(nint hwnd);

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [LibraryImport("kernel32.dll")]
    private static partial nint GlobalAlloc(uint flags, nuint bytes);

    [LibraryImport("kernel32.dll")]
    private static partial nint GlobalLock(nint hMem);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalUnlock(nint hMem);
}
