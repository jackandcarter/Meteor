#ifndef UNICODE
#define UNICODE
#endif
#ifndef _UNICODE
#define _UNICODE
#endif

#include <windows.h>
#include <tlhelp32.h>

#include <cstdlib>
#include <cstdint>
#include <cstdio>
#include <cwchar>
#include <string>
#include <vector>

namespace
{
constexpr DWORD kInjectionTimeoutMs = 10000;

std::string ToUtf8(const std::wstring& value)
{
    if (value.empty())
        return {};

    int size = WideCharToMultiByte(CP_UTF8, 0, value.c_str(), -1, nullptr, 0, nullptr, nullptr);
    if (size <= 1)
        return {};

    std::string result(static_cast<size_t>(size - 1), '\0');
    WideCharToMultiByte(CP_UTF8, 0, value.c_str(), -1, result.data(), size, nullptr, nullptr);
    return result;
}

void AppendLog(const std::wstring& logPath, const std::string& line)
{
    if (logPath.empty())
        return;

    HANDLE file = CreateFileW(
        logPath.c_str(),
        FILE_APPEND_DATA,
        FILE_SHARE_READ | FILE_SHARE_WRITE,
        nullptr,
        OPEN_ALWAYS,
        FILE_ATTRIBUTE_NORMAL,
        nullptr);

    if (file == INVALID_HANDLE_VALUE)
        return;

    std::string withNewline = line + "\r\n";
    DWORD written = 0;
    WriteFile(file, withNewline.data(), static_cast<DWORD>(withNewline.size()), &written, nullptr);
    CloseHandle(file);
}

void AppendLog(const std::wstring& logPath, const std::wstring& key, const std::wstring& value)
{
    AppendLog(logPath, ToUtf8(key) + "=" + ToUtf8(value));
}

void AppendLastError(const std::wstring& logPath, const char* key)
{
    AppendLog(logPath, std::string(key) + "=" + std::to_string(GetLastError()));
}

std::string Hex(uint64_t value)
{
    char buffer[32]{};
    std::snprintf(buffer, sizeof(buffer), "0x%llX", static_cast<unsigned long long>(value));
    return buffer;
}

std::wstring GetArgumentValue(const std::vector<std::wstring>& args, const std::wstring& name)
{
    for (size_t i = 0; i + 1 < args.size(); i++)
    {
        if (args[i] == name)
            return args[i + 1];
    }

    return {};
}

DWORD ParsePid(const std::wstring& value)
{
    if (value.empty())
        return 0;

    wchar_t* end = nullptr;
    unsigned long parsed = wcstoul(value.c_str(), &end, 10);
    if (end == value.c_str() || *end != L'\0' || parsed == 0)
        return 0;

    return static_cast<DWORD>(parsed);
}

std::wstring FullPath(const std::wstring& path)
{
    DWORD needed = GetFullPathNameW(path.c_str(), 0, nullptr, nullptr);
    if (needed == 0)
        return path;

    std::wstring result(needed, L'\0');
    DWORD written = GetFullPathNameW(path.c_str(), needed, result.data(), nullptr);
    if (written == 0 || written >= needed)
        return path;

    result.resize(written);
    return result;
}

uintptr_t FindRemoteModuleBase(DWORD processId, const wchar_t* moduleName, const std::wstring& logPath)
{
    HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, processId);
    if (snapshot == INVALID_HANDLE_VALUE)
    {
        AppendLastError(logPath, "native_injector_module_snapshot_error");
        return 0;
    }

    MODULEENTRY32W entry{};
    entry.dwSize = sizeof(entry);
    BOOL hasEntry = Module32FirstW(snapshot, &entry);
    while (hasEntry)
    {
        if (_wcsicmp(entry.szModule, moduleName) == 0)
        {
            uintptr_t base = reinterpret_cast<uintptr_t>(entry.modBaseAddr);
            CloseHandle(snapshot);
            return base;
        }

        hasEntry = Module32NextW(snapshot, &entry);
    }

    AppendLog(logPath, "native_injector_module_not_found=kernel32.dll");
    CloseHandle(snapshot);
    return 0;
}

int RunRemoteLoadLibrary(
    HANDLE process,
    void* remotePath,
    LPTHREAD_START_ROUTINE remoteLoadLibrary,
    const std::wstring& logPath)
{
    DWORD threadId = 0;
    HANDLE thread = CreateRemoteThread(process, nullptr, 0, remoteLoadLibrary, remotePath, 0, &threadId);
    if (thread == nullptr)
    {
        AppendLastError(logPath, "native_injector_create_remote_thread_error");
        return 26;
    }

    AppendLog(logPath, "native_injector_remote_thread_id=" + std::to_string(threadId));
    DWORD waitResult = WaitForSingleObject(thread, kInjectionTimeoutMs);
    AppendLog(logPath, "native_injector_wait_result=" + Hex(waitResult));
    if (waitResult != WAIT_OBJECT_0)
    {
        if (waitResult == WAIT_FAILED)
            AppendLastError(logPath, "native_injector_wait_error");
        CloseHandle(thread);
        return 27;
    }

    DWORD threadExitCode = 0;
    if (GetExitCodeThread(thread, &threadExitCode))
        AppendLog(logPath, "native_injector_thread_exit_code=" + Hex(threadExitCode));
    else
        AppendLastError(logPath, "native_injector_get_thread_exit_error");

    CloseHandle(thread);
    if (threadExitCode == 0)
    {
        AppendLog(logPath, "native_injector_failed=loadlibrary_returned_null");
        return 28;
    }

    return 0;
}

int Inject(DWORD processId, const std::wstring& dllPath, const std::wstring& logPath)
{
    std::wstring fullDllPath = FullPath(dllPath);
    AppendLog(logPath, "native_injector_started=true");
    AppendLog(logPath, "native_injector_process_bits=32");
    AppendLog(logPath, "native_injector_pid=" + std::to_string(processId));
    AppendLog(logPath, L"native_injector_dll", fullDllPath);

    DWORD attributes = GetFileAttributesW(fullDllPath.c_str());
    if (attributes == INVALID_FILE_ATTRIBUTES || (attributes & FILE_ATTRIBUTE_DIRECTORY) != 0)
    {
        AppendLog(logPath, "native_injector_failed=dll_missing");
        return 20;
    }

    HANDLE process = OpenProcess(
        PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ,
        FALSE,
        processId);
    if (process == nullptr)
    {
        AppendLastError(logPath, "native_injector_open_process_error");
        return 21;
    }

    std::wstring remoteString = fullDllPath + L'\0';
    SIZE_T remoteStringBytes = remoteString.size() * sizeof(wchar_t);
    void* remotePath = VirtualAllocEx(process, nullptr, remoteStringBytes, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
    if (remotePath == nullptr)
    {
        AppendLastError(logPath, "native_injector_virtual_alloc_error");
        CloseHandle(process);
        return 22;
    }

    SIZE_T bytesWritten = 0;
    BOOL wrote = WriteProcessMemory(process, remotePath, remoteString.data(), remoteStringBytes, &bytesWritten);
    if (!wrote || bytesWritten != remoteStringBytes)
    {
        AppendLastError(logPath, "native_injector_write_process_memory_error");
        VirtualFreeEx(process, remotePath, 0, MEM_RELEASE);
        CloseHandle(process);
        return 23;
    }

    HMODULE localKernel32 = GetModuleHandleW(L"kernel32.dll");
    FARPROC localLoadLibrary = localKernel32 == nullptr ? nullptr : GetProcAddress(localKernel32, "LoadLibraryW");
    if (localKernel32 == nullptr || localLoadLibrary == nullptr)
    {
        AppendLastError(logPath, "native_injector_local_loadlibrary_error");
        VirtualFreeEx(process, remotePath, 0, MEM_RELEASE);
        CloseHandle(process);
        return 24;
    }

    uintptr_t localKernel32Base = reinterpret_cast<uintptr_t>(localKernel32);
    uintptr_t localLoadLibraryAddress = reinterpret_cast<uintptr_t>(localLoadLibrary);
    AppendLog(logPath, "native_injector_remote_path=" + Hex(reinterpret_cast<uintptr_t>(remotePath)));

    AppendLog(logPath, "native_injector_address_mode=same_bitness_local_loadlibrary");
    AppendLog(logPath, "native_injector_local_kernel32_base=" + Hex(localKernel32Base));
    AppendLog(logPath, "native_injector_loadlibrary_address=" + Hex(localLoadLibraryAddress));
    int result = RunRemoteLoadLibrary(
        process,
        remotePath,
        reinterpret_cast<LPTHREAD_START_ROUTINE>(localLoadLibraryAddress),
        logPath);
    if (result == 0)
    {
        VirtualFreeEx(process, remotePath, 0, MEM_RELEASE);
        CloseHandle(process);
        AppendLog(logPath, "native_injector_complete=true");
        return 0;
    }

    AppendLog(logPath, "native_injector_same_bitness_result=" + std::to_string(result));
    uintptr_t remoteKernel32 = FindRemoteModuleBase(processId, L"kernel32.dll", logPath);
    if (remoteKernel32 != 0)
    {
        uintptr_t loadLibraryOffset = localLoadLibraryAddress - localKernel32Base;
        auto remoteLoadLibrary = reinterpret_cast<LPTHREAD_START_ROUTINE>(remoteKernel32 + loadLibraryOffset);
        AppendLog(logPath, "native_injector_address_mode=toolhelp_remote_kernel32");
        AppendLog(logPath, "native_injector_kernel32_base=" + Hex(remoteKernel32));
        AppendLog(logPath, "native_injector_loadlibrary_offset=" + Hex(loadLibraryOffset));
        result = RunRemoteLoadLibrary(process, remotePath, remoteLoadLibrary, logPath);
    }

    VirtualFreeEx(process, remotePath, 0, MEM_RELEASE);
    CloseHandle(process);
    return result;
}
}

int wmain(int argc, wchar_t** argv)
{
    std::vector<std::wstring> args;
    args.reserve(static_cast<size_t>(argc));
    for (int i = 0; i < argc; i++)
        args.emplace_back(argv[i]);

    std::wstring logPath = GetArgumentValue(args, L"--log");
    DWORD processId = ParsePid(GetArgumentValue(args, L"--pid"));
    std::wstring dllPath = GetArgumentValue(args, L"--dll");

    if (processId == 0 || dllPath.empty())
    {
        AppendLog(logPath, "native_injector_failed=invalid_arguments");
        return 10;
    }

    return Inject(processId, dllPath, logPath);
}
