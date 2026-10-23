using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Snapkit.App.Interop;

/// <summary>
/// Родство процессов. У Chrome и Claude Desktop окно принадлежит главному процессу,
/// а буфер обмена может прочитать один из его дочерних процессов — это тоже получатель.
/// </summary>
internal static partial class ProcessTree
{
    private const int MaxDepth = 6;

    /// <summary>pid — это ancestorPid или его потомок (не глубже нескольких поколений).</summary>
    public static bool IsSameOrDescendant(int pid, int ancestorPid)
    {
        if (pid <= 0 || ancestorPid <= 0) return false;
        if (pid == ancestorPid) return true;
        var parents = ParentMap();
        var current = pid;
        for (var depth = 0; depth < MaxDepth && parents.TryGetValue(current, out var parent) && parent > 0; depth++)
        {
            if (parent == ancestorPid) return true;
            current = parent;
        }
        return false;
    }

    /// <summary>Имя процесса для журнала; пустая строка, если процесса уже нет.</summary>
    public static string NameOf(int pid)
    {
        try { return Process.GetProcessById(pid).ProcessName; }
        catch (Exception) { return ""; }
    }

    private static Dictionary<int, int> ParentMap()
    {
        var map = new Dictionary<int, int>();
        var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == INVALID_HANDLE_VALUE) return map;
        try
        {
            var entry = new PROCESSENTRY32W { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32W>() };
            for (var ok = Process32FirstW(snapshot, ref entry); ok; ok = Process32NextW(snapshot, ref entry))
                map[(int)entry.th32ProcessID] = (int)entry.th32ParentProcessID;
        }
        finally
        {
            CloseHandle(snapshot);
        }
        return map;
    }

    private const uint TH32CS_SNAPPROCESS = 0x00000002;
    private static readonly nint INVALID_HANDLE_VALUE = -1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PROCESSENTRY32W
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public nint th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32FirstW(nint hSnapshot, ref PROCESSENTRY32W lppe);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32NextW(nint hSnapshot, ref PROCESSENTRY32W lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint hObject);
}
