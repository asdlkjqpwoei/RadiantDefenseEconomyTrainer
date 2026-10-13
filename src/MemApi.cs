using System;
using System.Runtime.InteropServices;

/// <summary>Win32 内存访问 P/Invoke 封装（针对 UWP/AppContainer 目标进程做了访问权限裁剪）。</summary>
public static class MemApi
{
    public const uint PROCESS_VM_READ = 0x0010;
    public const uint PROCESS_VM_WRITE = 0x0020;
    public const uint PROCESS_VM_OPERATION = 0x0008;
    public const uint PROCESS_QUERY_INFORMATION = 0x0400;
    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    public const uint MEM_COMMIT = 0x1000;
    public const uint PAGE_READONLY = 0x02;
    public const uint PAGE_READWRITE = 0x04;
    public const uint PAGE_WRITECOPY = 0x08;
    public const uint PAGE_EXECUTE = 0x10;
    public const uint PAGE_EXECUTE_READ = 0x20;
    public const uint PAGE_EXECUTE_READWRITE = 0x40;
    public const uint PAGE_EXECUTE_WRITECOPY = 0x80;
    public const uint PAGE_GUARD = 0x100;

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr h);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadProcessMemory(IntPtr h, IntPtr addr, byte[] buf, IntPtr size, out IntPtr read);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WriteProcessMemory(IntPtr h, IntPtr addr, byte[] buf, IntPtr size, out IntPtr written);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool VirtualQueryEx(IntPtr h, IntPtr addr, out MEMORY_BASIC_INFORMATION mbi, IntPtr size);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool VirtualProtectEx(IntPtr h, IntPtr addr, IntPtr size, uint newProtect, out uint oldProtect);

    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    // ---- SeDebugPrivilege（帮助打开受保护目标进程，失败不影响继续尝试） ----
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr h, uint access, out IntPtr tok);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool LookupPrivilegeValue(string sys, string name, out LUID luid);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(IntPtr tok, bool disableAll, ref TOKEN_PRIVILEGES tp, int len, IntPtr prev, IntPtr ret);

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID { public uint Low; public int High; }

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_PRIVILEGES { public int Count; public LUID Luid; public uint Attr; }

    private const uint SE_PRIVILEGE_ENABLED = 0x2;
    private const uint TOKEN_ADJUST_PRIVILEGES = 0x20;
    private const uint TOKEN_QUERY = 0x8;

    public static void EnableDebugPrivilege()
    {
        try
        {
            IntPtr tok;
            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out tok)) return;
            try
            {
                LUID luid;
                if (!LookupPrivilegeValue(null, "SeDebugPrivilege", out luid)) return;
                TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES();
                tp.Count = 1;
                tp.Luid = luid;
                tp.Attr = SE_PRIVILEGE_ENABLED;
                AdjustTokenPrivileges(tok, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
            }
            finally { CloseHandle(tok); }
        }
        catch { }
    }

    /// <summary>打开目标进程（优先最小权限掩码，兼容 UWP/AppContainer 进程）。</summary>
    public static IntPtr OpenTarget(int pid)
    {
        EnableDebugPrivilege();
        uint access = PROCESS_QUERY_INFORMATION | PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION;
        IntPtr h = OpenProcess(access, false, pid);
        if (h == IntPtr.Zero)
            h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION, false, pid);
        return h;
    }

    public static bool Read(IntPtr h, long addr, byte[] buf)
    {
        IntPtr read;
        return ReadProcessMemory(h, (IntPtr)addr, buf, (IntPtr)buf.Length, out read) && (long)read == buf.LongLength;
    }

    public static bool Write(IntPtr h, long addr, byte[] buf)
    {
        int error, restoreError;
        return Write(h, addr, buf, out error, out restoreError);
    }

    public static bool Write(IntPtr h, long addr, byte[] buf, out int error, out int restoreError)
    {
        error = 0;
        restoreError = 0;
        if (TryWrite(h, addr, buf, out error)) return true;

        int initialError = error;
        uint writableProtect = PAGE_READWRITE;
        MEMORY_BASIC_INFORMATION mbi;
        int mbiSize = Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION));
        if (VirtualQueryEx(h, (IntPtr)addr, out mbi, (IntPtr)mbiSize))
        {
            uint currentProtect = mbi.Protect & 0xFF;
            if (currentProtect == PAGE_EXECUTE || currentProtect == PAGE_EXECUTE_READ ||
                currentProtect == PAGE_EXECUTE_READWRITE || currentProtect == PAGE_EXECUTE_WRITECOPY)
                writableProtect = PAGE_EXECUTE_READWRITE;
        }

        uint oldProtect;
        if (!VirtualProtectEx(h, (IntPtr)addr, (IntPtr)buf.Length, writableProtect, out oldProtect))
        {
            int protectError = Marshal.GetLastWin32Error();
            error = protectError != 0 ? protectError : initialError;
            return false;
        }

        int retryError;
        bool written = TryWrite(h, addr, buf, out retryError);
        uint ignored;
        if (!VirtualProtectEx(h, (IntPtr)addr, (IntPtr)buf.Length, oldProtect, out ignored))
            restoreError = Marshal.GetLastWin32Error();

        error = written ? 0 : retryError;
        return written;
    }

    private static bool TryWrite(IntPtr h, long addr, byte[] buf, out int error)
    {
        IntPtr written;
        bool ok = WriteProcessMemory(h, (IntPtr)addr, buf, (IntPtr)buf.Length, out written);
        error = ok ? 0 : Marshal.GetLastWin32Error();
        if (ok && (long)written == buf.LongLength) return true;
        if (ok) error = 299;
        return false;
    }
}
