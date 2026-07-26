using System.Runtime.InteropServices;
using System.Text;

namespace AetherXIV.Launcher.ClientLauncher;

internal static partial class NativeMethods
{
    [DllImport("kernel32.dll")]
    internal static extern uint GetTickCount();

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool GetProcessAffinityMask(
        IntPtr hProcess,
        out UIntPtr lpProcessAffinityMask,
        out UIntPtr lpSystemAffinityMask);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool SetProcessAffinityMask(
        IntPtr hProcess,
        UIntPtr dwProcessAffinityMask);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern bool WriteProcessMemory(
        IntPtr hProcess,
        IntPtr lpBaseAddress,
        byte[] lpBuffer,
        nuint nSize,
        out nuint lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool VirtualProtectEx(
        IntPtr hProcess,
        IntPtr lpAddress,
        nuint dwSize,
        uint flNewProtect,
        out uint lpflOldProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr VirtualAllocEx(
        IntPtr hProcess,
        IntPtr lpAddress,
        UIntPtr dwSize,
        AllocationType flAllocationType,
        MemoryProtectionFlags flProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool VirtualFreeEx(
        IntPtr hProcess,
        IntPtr lpAddress,
        UIntPtr dwSize,
        FreeType dwFreeType);

    [DllImport("kernel32.dll", EntryPoint = "CreateProcessA", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Ansi)]
    internal static extern bool CreateProcessA(
        string? lpApplicationName,
        StringBuilder lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        ProcessCreationFlags dwCreationFlags,
        IntPtr lpEnvironment,
        string lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern bool CreateProcessW(
        string? lpApplicationName,
        StringBuilder lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        ProcessCreationFlags dwCreationFlags,
        IntPtr lpEnvironment,
        string lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern uint ResumeThread(IntPtr hThread);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr CreateRemoteThread(
        IntPtr hProcess,
        IntPtr lpThreadAttributes,
        uint dwStackSize,
        IntPtr lpStartAddress,
        IntPtr lpParameter,
        uint dwCreationFlags,
        out uint lpThreadId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    internal static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

    internal const uint WaitObject0 = 0x00000000;
    internal const uint WaitTimeout = 0x00000102;
    internal const uint WaitFailed = 0xFFFFFFFF;
    internal const uint ResumeThreadFailed = 0xFFFFFFFF;
    [Flags]
    internal enum ProcessCreationFlags : uint
    {
        CREATE_SUSPENDED = 0x00000004,
        NORMAL_PRIORITY_CLASS = 0x00000020
    }

    [Flags]
    internal enum MemoryProtectionFlags : uint
    {
        PAGE_READONLY = 0x02,
        PAGE_READWRITE = 0x04,
        PAGE_WRITECOPY = 0x08,
        PAGE_EXECUTE = 0x10,
        PAGE_EXECUTE_READ = 0x20,
        PAGE_EXECUTE_READWRITE = 0x40,
        PAGE_EXECUTE_WRITECOPY = 0x80,
        PAGE_GUARD = 0x100,
        PAGE_NOCACHE = 0x200,
        PAGE_WRITECOMBINE = 0x400
    }

    [Flags]
    internal enum AllocationType : uint
    {
        MEM_COMMIT = 0x00001000,
        MEM_RESERVE = 0x00002000
    }

    [Flags]
    internal enum FreeType : uint
    {
        MEM_RELEASE = 0x00008000
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct STARTUPINFO
    {
        internal uint cb;
        internal string? lpReserved;
        internal string? lpDesktop;
        internal string? lpTitle;
        internal uint dwX;
        internal uint dwY;
        internal uint dwXSize;
        internal uint dwYSize;
        internal uint dwXCountChars;
        internal uint dwYCountChars;
        internal uint dwFillAttribute;
        internal uint dwFlags;
        internal short wShowWindow;
        internal short cbReserved2;
        internal IntPtr lpReserved2;
        internal IntPtr hStdInput;
        internal IntPtr hStdOutput;
        internal IntPtr hStdError;
    }

    internal struct PROCESS_INFORMATION
    {
        internal IntPtr hProcess;
        internal IntPtr hThread;
        internal uint dwProcessId;
        internal uint dwThreadId;
    }

}
