#ifndef UNICODE
#define UNICODE
#endif
#ifndef _UNICODE
#define _UNICODE
#endif

#include <windows.h>
#include <d3d9.h>

#include "imgui.h"
#include "backends/imgui_impl_dx9.h"
#include "backends/imgui_impl_win32.h"

extern IMGUI_IMPL_API LRESULT ImGui_ImplWin32_WndProcHandler(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam);
void DrawUmbraSdkIcon(ImDrawList* drawList, int icon, ImVec2 center, float size, ImU32 color);

#if !defined(_MSC_VER)
extern "C" void* memset(void* destination, int value, unsigned int count)
{
    volatile unsigned char* target = static_cast<volatile unsigned char*>(destination);
    while (count-- > 0)
        *target++ = static_cast<unsigned char>(value);
    return destination;
}
#endif

namespace
{
    struct UmbraTheme;
    using hostfxr_handle = void*;
    using hostfxr_initialize_for_runtime_config_fn = int(__cdecl*)(const wchar_t*, const void*, hostfxr_handle*);
    using hostfxr_get_runtime_delegate_fn = int(__cdecl*)(hostfxr_handle, int, void**);
    using hostfxr_close_fn = int(__cdecl*)(hostfxr_handle);
    using load_assembly_and_get_function_pointer_fn = int(__cdecl*)(
        const wchar_t*,
        const wchar_t*,
        const wchar_t*,
        const wchar_t*,
        void*,
        void**);
    using umbra_bootstrap_fn = int(__stdcall*)();
    using coreclr_initialize_fn = int(__stdcall*)(
        const char*,
        const char*,
        int,
        const char**,
        const char**,
        void**,
        unsigned int*);
    using coreclr_create_delegate_fn = int(__stdcall*)(
        void*,
        unsigned int,
        const char*,
        const char*,
        const char*,
        void**);
    using coreclr_bootstrap_fn = int(__stdcall*)(void*, int);
    using umbra_render_bridge_fn = int(__stdcall*)(const void*, int);
    using direct3d_create9_fn = IDirect3D9* (WINAPI*)(UINT);
    using idirect3d9_create_device_fn = HRESULT (STDMETHODCALLTYPE*)(
        IDirect3D9*,
        UINT,
        D3DDEVTYPE,
        HWND,
        DWORD,
        D3DPRESENT_PARAMETERS*,
        IDirect3DDevice9**);
    using idirect3ddevice9_present_fn = HRESULT (STDMETHODCALLTYPE*)(
        IDirect3DDevice9*,
        const RECT*,
        const RECT*,
        HWND,
        const RGNDATA*);
    using idirect3ddevice9_reset_fn = HRESULT (STDMETHODCALLTYPE*)(
        IDirect3DDevice9*,
        D3DPRESENT_PARAMETERS*);
    using idirect3ddevice9_end_scene_fn = HRESULT (STDMETHODCALLTYPE*)(
        IDirect3DDevice9*);
    using idirect3dswapchain9_present_fn = HRESULT (STDMETHODCALLTYPE*)(
        IDirect3DSwapChain9*,
        const RECT*,
        const RECT*,
        HWND,
        const RGNDATA*,
        DWORD);
    using get_async_key_state_fn = SHORT (WINAPI*)(int);
    using get_cursor_pos_fn = BOOL (WINAPI*)(LPPOINT);
    using screen_to_client_fn = BOOL (WINAPI*)(HWND, LPPOINT);

    struct JumpHook
    {
        void* target;
        void* replacement;
        void* trampoline;
        BYTE original[5];
        bool hasOriginal;
        bool installed;
    };

    enum UmbraRenderEventKind : DWORD
    {
        UmbraRenderFrame = 1,
        UmbraRenderBeforeReset = 2,
        UmbraRenderAfterReset = 3
    };

    struct UmbraRenderEventV1
    {
        DWORD size;
        DWORD abiVersion;
        DWORD kind;
        DWORD frameNumber;
        float deltaSeconds;
        DWORD viewportWidth;
        DWORD viewportHeight;
        DWORD reserved;
    };

    constexpr DWORD BufferChars = 32768;
    constexpr DWORD CoreClrPropertyBytes = 262144;
    constexpr DWORD Dx9HookWaitMs = 120000;
    constexpr DWORD Dx9HookPollMs = 100;
    constexpr int HostFxrDelegateLoadAssemblyAndGetFunctionPointer = 5;
    constexpr int IDirect3D9CreateDeviceIndex = 16;
    constexpr int IDirect3DDevice9ResetIndex = 16;
    constexpr int IDirect3DDevice9PresentIndex = 17;
    constexpr int IDirect3DDevice9EndSceneIndex = 42;
    constexpr int IDirect3DSwapChain9PresentIndex = 3;
    constexpr DWORD OverlayFvf = D3DFVF_XYZRHW | D3DFVF_DIFFUSE;
    constexpr DWORD OverlayMaxVertices = 24576;
    constexpr DWORD ToastVisibleMs = 30000;
    constexpr DWORD UmbraDockCollapseMs = 8000;
    constexpr DWORD UmbraRenderBridgeAbiVersion = 1;
    const wchar_t* UnmanagedCallersOnlyMethod = reinterpret_cast<const wchar_t*>(-1);

    struct OverlayVertex
    {
        float x;
        float y;
        float z;
        float rhw;
        D3DCOLOR color;
    };

    struct OverlayRect
    {
        int x;
        int y;
        int width;
        int height;
    };

    JumpHook Direct3DCreate9Hook{};
    direct3d_create9_fn OriginalDirect3DCreate9 = nullptr;
    idirect3d9_create_device_fn OriginalCreateDevice = nullptr;
    idirect3ddevice9_present_fn OriginalPresent = nullptr;
    idirect3ddevice9_reset_fn OriginalReset = nullptr;
    idirect3ddevice9_end_scene_fn OriginalEndScene = nullptr;
    idirect3dswapchain9_present_fn OriginalSwapChainPresent = nullptr;
    volatile LONG Direct3DCreate9Observed = 0;
    volatile LONG CreateDeviceObserved = 0;
    volatile LONG DeviceHooked = 0;
    volatile LONG SwapChainHooked = 0;
    volatile LONG PresentFrameCount = 0;
    volatile LONG SwapChainPresentFrameCount = 0;
    volatile LONG EndSceneFrameCount = 0;
    volatile LONG ResetCount = 0;
    volatile LONG NativeMarkerEnabled = 0;
    volatile LONG NativeReadyLogged = 0;
    volatile LONG NativeUiShellLogged = 0;
    volatile LONG NativeUiViewportLogged = 0;
    volatile LONG NativeLibraryRenderedLogged = 0;
    volatile LONG ImGuiInitializedLogged = 0;
    volatile LONG ImGuiRenderLogged = 0;
    volatile LONG ImGuiFirstFrameDiagnosticsClaimed = 0;
    volatile LONG ImGuiWndProcHookLogged = 0;
    volatile LONG ManagedRenderBridgeReadyLogged = 0;
    volatile LONG ManagedRenderBridgeFailureLogged = 0;
    volatile LONG ManagedFrameNumber = 0;
    volatile LONG ManagedUiCallbackActive = 0;
    HMODULE UmbraModule = nullptr;
    ImFont* UmbraUiFont = nullptr;
    umbra_render_bridge_fn ManagedRenderBridge = nullptr;
    DWORD ManagedLastFrameTicks = 0;
    DWORD ManagedRenderThreadId = 0;
    int ManagedUiWindowDepth = 0;
    int ManagedUiChildDepth = 0;
    HWND GameWindow = nullptr;
    WNDPROC OriginalGameWndProc = nullptr;
    bool GameWndProcHooked = false;
    OverlayVertex OverlayVertices[OverlayMaxVertices]{};
    DWORD OverlayVertexCount = 0;
    DWORD OverlayStartTicks = 0;
    bool SettingsWindowOpen = false;
    bool PluginInstallerOpen = false;
    bool UmbraDockExpanded = true;
    bool LastMouseDown = false;
    bool LastInsertDown = false;
    bool LastF9Down = false;
    bool LastF10Down = false;
    bool LastF11Down = false;
    int MouseX = -1;
    int MouseY = -1;
    bool MouseClicked = false;
    bool MouseDown = false;
    bool ImGuiInitialized = false;
    bool DebugLoggingEnabled = true;
    bool DevUiEnabled = false;
    bool DevBridgeEnabled = false;
    bool DevBridgeControlKnown = false;
    bool ShowPluginExecutionWarning = false;
    int UmbraThemeIndex = 0;
    int UmbraLibrarySection = 0;
    int UmbraLibrarySelectedCard = 0;
    int UmbraSettingsSection = 0;
    int UmbraDeveloperLogLevel = 1;
    bool UmbraLibraryGridView = false;
    bool UmbraDeveloperBarVisible = false;
    bool UmbraDeveloperLogOpen = false;
    bool UmbraDeveloperMetricsVisible = false;
    volatile LONG UmbraSettingsWindowRenderedLogged = 0;
    volatile LONG UmbraDeveloperBarRenderedLogged = 0;
    DWORD UmbraDockLastInteractionTicks = 0;
    DWORD DevBridgeLastControlCheckTicks = 0;
    DWORD UmbraDeveloperLogRefreshTicks = 0;
    wchar_t DevBridgeControlPath[BufferChars]{};
    char UmbraDeveloperLogBuffer[65536]{};
    get_async_key_state_fn User32GetAsyncKeyState = nullptr;
    get_cursor_pos_fn User32GetCursorPos = nullptr;
    screen_to_client_fn User32ScreenToClient = nullptr;

    IDirect3D9* WINAPI HookedDirect3DCreate9(UINT sdkVersion);
    HRESULT STDMETHODCALLTYPE HookedCreateDevice(
        IDirect3D9* self,
        UINT adapter,
        D3DDEVTYPE deviceType,
        HWND focusWindow,
        DWORD behaviorFlags,
        D3DPRESENT_PARAMETERS* presentationParameters,
        IDirect3DDevice9** returnedDeviceInterface);
    HRESULT STDMETHODCALLTYPE HookedPresent(
        IDirect3DDevice9* self,
        const RECT* sourceRect,
        const RECT* destRect,
        HWND destWindowOverride,
        const RGNDATA* dirtyRegion);
    HRESULT STDMETHODCALLTYPE HookedReset(
        IDirect3DDevice9* self,
        D3DPRESENT_PARAMETERS* presentationParameters);
    HRESULT STDMETHODCALLTYPE HookedEndScene(IDirect3DDevice9* self);
    HRESULT STDMETHODCALLTYPE HookedSwapChainPresent(
        IDirect3DSwapChain9* self,
        const RECT* sourceRect,
        const RECT* destRect,
        HWND destWindowOverride,
        const RGNDATA* dirtyRegion,
        DWORD flags);
    bool HookUmbraWindowProc();
    bool ResolveDevBridgeControlPath(wchar_t* output, DWORD outputChars);
    void RefreshDevBridgeControlState(bool force);
    void WriteDevBridgeControlState(bool enabled);
    void ParentDirectory(const wchar_t* path, wchar_t* output, DWORD outputChars);
    void CombinePath(const wchar_t* left, const wchar_t* right, wchar_t* output, DWORD outputChars);
    int NotifyManagedRenderEvent(UmbraRenderEventKind kind, const D3DVIEWPORT9* viewport = nullptr);
    void DrawUmbraWindowAccent(const UmbraTheme& theme);

    DWORD StringLength(const wchar_t* value)
    {
        return value == nullptr ? 0 : static_cast<DWORD>(lstrlenW(value));
    }

    DWORD AnsiLength(const char* value)
    {
        if (value == nullptr)
            return 0;

        DWORD length = 0;
        while (value[length] != '\0')
            length++;
        return length;
    }

    void CopyString(wchar_t* destination, DWORD destinationChars, const wchar_t* source)
    {
        if (destinationChars == 0)
            return;

        if (source == nullptr)
            source = L"";

        lstrcpynW(destination, source, static_cast<int>(destinationChars));
        destination[destinationChars - 1] = L'\0';
    }

    void AppendString(wchar_t* destination, DWORD destinationChars, const wchar_t* source)
    {
        DWORD used = StringLength(destination);
        if (used >= destinationChars)
            return;

        CopyString(destination + used, destinationChars - used, source);
    }

    void AppendAnsi(char* destination, DWORD destinationBytes, const char* source)
    {
        if (destinationBytes == 0 || source == nullptr)
            return;

        DWORD used = AnsiLength(destination);
        if (used >= destinationBytes)
            return;

        DWORD index = 0;
        while (source[index] != '\0' && used + index + 1 < destinationBytes)
        {
            destination[used + index] = source[index];
            index++;
        }

        destination[used + index] = '\0';
    }

    void AppendUtf8Wide(char* destination, DWORD destinationBytes, const wchar_t* source)
    {
        if (destinationBytes == 0 || source == nullptr)
            return;

        DWORD used = AnsiLength(destination);
        if (used + 1 >= destinationBytes)
            return;

        int remaining = static_cast<int>(destinationBytes - used);
        int written = WideCharToMultiByte(CP_UTF8, 0, source, -1, destination + used, remaining, nullptr, nullptr);
        if (written <= 0)
            destination[used] = '\0';
    }

    void UIntToWide(unsigned long value, wchar_t* buffer, DWORD bufferChars)
    {
        if (bufferChars == 0)
            return;

        wchar_t temp[16]{};
        DWORD index = 0;
        do
        {
            temp[index++] = static_cast<wchar_t>(L'0' + (value % 10));
            value /= 10;
        } while (value != 0 && index < 16);

        DWORD out = 0;
        while (index > 0 && out + 1 < bufferChars)
            buffer[out++] = temp[--index];
        buffer[out] = L'\0';
    }

    void IntToHex(int value, wchar_t* buffer, DWORD bufferChars)
    {
        static const wchar_t Digits[] = L"0123456789ABCDEF";
        if (bufferChars < 3)
            return;

        unsigned int source = static_cast<unsigned int>(value);
        buffer[0] = L'0';
        buffer[1] = L'x';

        bool started = false;
        DWORD out = 2;
        for (int shift = 28; shift >= 0 && out + 1 < bufferChars; shift -= 4)
        {
            unsigned int nibble = (source >> shift) & 0xF;
            if (nibble != 0 || started || shift == 0)
            {
                started = true;
                buffer[out++] = Digits[nibble];
            }
        }

        buffer[out] = L'\0';
    }

    DWORD ParseUInt(const wchar_t* value)
    {
        DWORD result = 0;
        if (value == nullptr)
            return 0;

        while (*value >= L'0' && *value <= L'9')
        {
            result = (result * 10) + static_cast<DWORD>(*value - L'0');
            value++;
        }

        return result;
    }

    void WriteWide(HANDLE file, const wchar_t* value)
    {
        if (file == INVALID_HANDLE_VALUE || value == nullptr)
            return;

        int required = WideCharToMultiByte(CP_UTF8, 0, value, -1, nullptr, 0, nullptr, nullptr);
        if (required <= 1)
            return;

        char stackBuffer[2048]{};
        if (required <= static_cast<int>(sizeof(stackBuffer)))
        {
            WideCharToMultiByte(CP_UTF8, 0, value, -1, stackBuffer, required, nullptr, nullptr);
            DWORD written = 0;
            WriteFile(file, stackBuffer, static_cast<DWORD>(required - 1), &written, nullptr);
            return;
        }

        char* heapBuffer = static_cast<char*>(HeapAlloc(GetProcessHeap(), 0, static_cast<SIZE_T>(required)));
        if (heapBuffer == nullptr)
            return;

        WideCharToMultiByte(CP_UTF8, 0, value, -1, heapBuffer, required, nullptr, nullptr);
        DWORD written = 0;
        WriteFile(file, heapBuffer, static_cast<DWORD>(required - 1), &written, nullptr);
        HeapFree(GetProcessHeap(), 0, heapBuffer);
    }

    void AppendLogValue(HANDLE file, const wchar_t* key, const wchar_t* value)
    {
        WriteWide(file, key);
        WriteWide(file, L"=");
        WriteWide(file, value);
        WriteWide(file, L"\n");
    }

    void AppendLogLiteral(HANDLE file, const wchar_t* line)
    {
        WriteWide(file, line);
        WriteWide(file, L"\n");
    }

    void AppendLogUInt(HANDLE file, const wchar_t* key, unsigned long value)
    {
        wchar_t buffer[32]{};
        UIntToWide(value, buffer, 32);
        AppendLogValue(file, key, buffer);
    }

    void AppendLogHex(HANDLE file, const wchar_t* key, int value)
    {
        wchar_t buffer[32]{};
        IntToHex(value, buffer, 32);
        AppendLogValue(file, key, buffer);
    }

    HANDLE OpenAppendFile(const wchar_t* path)
    {
        if (path == nullptr || path[0] == L'\0')
            return INVALID_HANDLE_VALUE;

        return CreateFileW(
            path,
            FILE_APPEND_DATA,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            nullptr,
            OPEN_ALWAYS,
            FILE_ATTRIBUTE_NORMAL,
            nullptr);
    }

    bool GetEnvironmentValue(const wchar_t* name, wchar_t* buffer, DWORD bufferChars)
    {
        if (bufferChars == 0)
            return false;

        buffer[0] = L'\0';
        DWORD written = GetEnvironmentVariableW(name, buffer, bufferChars);
        if (written == 0 || written >= bufferChars)
        {
            buffer[0] = L'\0';
            return false;
        }

        return true;
    }

    bool GetUmbraEnvironmentValue(const wchar_t* suffix, wchar_t* buffer, DWORD bufferChars)
    {
        wchar_t primary[128]{};
        CopyString(primary, 128, L"AETHER_UMBRA_");
        AppendString(primary, 128, suffix);
        return GetEnvironmentValue(primary, buffer, bufferChars);
    }

    bool IsTruthy(const wchar_t* value)
    {
        return lstrcmpW(value, L"1") == 0
            || lstrcmpiW(value, L"true") == 0
            || lstrcmpiW(value, L"yes") == 0;
    }

    bool IsWine()
    {
        HMODULE ntdll = GetModuleHandleW(L"ntdll.dll");
        return ntdll != nullptr && GetProcAddress(ntdll, "wine_get_version") != nullptr;
    }

    HANDLE OpenBootstrapLog()
    {
        wchar_t logPath[BufferChars]{};
        DWORD requestedLogError = 0;
        if (GetUmbraEnvironmentValue(L"LOG", logPath, BufferChars))
        {
            HANDLE log = OpenAppendFile(logPath);
            if (log != INVALID_HANDLE_VALUE)
                return log;
            requestedLogError = GetLastError();
        }

        wchar_t helperLogPath[BufferChars]{};
        if (GetUmbraEnvironmentValue(L"HELPER_LOG", helperLogPath, BufferChars))
        {
            HANDLE log = OpenAppendFile(helperLogPath);
            if (log != INVALID_HANDLE_VALUE)
            {
                AppendLogValue(log, L"umbra_bootstrap_log_fallback", L"helper_log");
                AppendLogValue(log, L"umbra_bootstrap_requested_log", logPath);
                AppendLogUInt(log, L"umbra_bootstrap_requested_log_error", requestedLogError);
                return log;
            }
        }

        HANDLE fallback = OpenAppendFile(L"Z:\\private\\tmp\\umbra-bootstrap-fallback.log");
        if (fallback != INVALID_HANDLE_VALUE)
        {
            AppendLogValue(fallback, L"umbra_bootstrap_log_fallback", L"Z:\\private\\tmp\\umbra-bootstrap-fallback.log");
            AppendLogValue(fallback, L"umbra_bootstrap_requested_log", logPath);
            AppendLogValue(fallback, L"umbra_bootstrap_helper_log", helperLogPath);
            return fallback;
        }

        fallback = OpenAppendFile(L"umbra-bootstrap-fallback.log");
        if (fallback != INVALID_HANDLE_VALUE)
        {
            AppendLogValue(fallback, L"umbra_bootstrap_log_fallback", L"working_directory");
            AppendLogValue(fallback, L"umbra_bootstrap_requested_log", logPath);
            AppendLogValue(fallback, L"umbra_bootstrap_helper_log", helperLogPath);
        }

        return fallback;
    }

    void CopyBytes(BYTE* destination, const BYTE* source, DWORD count)
    {
        for (DWORD index = 0; index < count; index++)
            destination[index] = source[index];
    }

    void AppendDx9LogLiteral(const wchar_t* line)
    {
        HANDLE log = OpenBootstrapLog();
        if (log == INVALID_HANDLE_VALUE)
            return;

        AppendLogLiteral(log, line);
        CloseHandle(log);
    }

    void AppendDx9LogUInt(const wchar_t* key, unsigned long value)
    {
        HANDLE log = OpenBootstrapLog();
        if (log == INVALID_HANDLE_VALUE)
            return;

        AppendLogUInt(log, key, value);
        CloseHandle(log);
    }

    void AppendDx9LogHex(const wchar_t* key, int value)
    {
        HANDLE log = OpenBootstrapLog();
        if (log == INVALID_HANDLE_VALUE)
            return;

        AppendLogHex(log, key, value);
        CloseHandle(log);
    }

    bool WriteRelativeJump(void* source, void* destination)
    {
        BYTE* patch = static_cast<BYTE*>(source);
        DWORD relative = static_cast<DWORD>(
            reinterpret_cast<ULONG_PTR>(destination) - reinterpret_cast<ULONG_PTR>(patch) - 5);
        patch[0] = 0xE9;
        *reinterpret_cast<DWORD*>(patch + 1) = relative;
        return true;
    }

    bool InstallJumpHook(HANDLE log, JumpHook& hook, void* target, void* replacement, const wchar_t* installedLine)
    {
        if (hook.installed)
            return true;

        if (target == nullptr || replacement == nullptr)
            return false;

        hook.target = target;
        hook.replacement = replacement;
        if (!hook.hasOriginal)
        {
            CopyBytes(hook.original, static_cast<const BYTE*>(target), 5);
            hook.hasOriginal = true;
        }

        if (hook.trampoline == nullptr)
        {
            BYTE* trampoline = static_cast<BYTE*>(VirtualAlloc(
                nullptr,
                10,
                MEM_COMMIT | MEM_RESERVE,
                PAGE_EXECUTE_READWRITE));
            if (trampoline == nullptr)
            {
                AppendLogUInt(log, L"umbra_dx9_hook_trampoline_error", GetLastError());
                return false;
            }

            CopyBytes(trampoline, hook.original, 5);
            WriteRelativeJump(trampoline + 5, static_cast<BYTE*>(target) + 5);
            hook.trampoline = trampoline;
        }

        DWORD oldProtect = 0;
        if (!VirtualProtect(target, 5, PAGE_EXECUTE_READWRITE, &oldProtect))
        {
            AppendLogUInt(log, L"umbra_dx9_hook_virtualprotect_error", GetLastError());
            return false;
        }

        WriteRelativeJump(target, replacement);
        DWORD ignored = 0;
        VirtualProtect(target, 5, oldProtect, &ignored);
        FlushInstructionCache(GetCurrentProcess(), target, 5);
        hook.installed = true;
        AppendLogLiteral(log, installedLine);
        return true;
    }

    bool HookDirect3DCreate9Import(HANDLE log)
    {
        HMODULE module = GetModuleHandleW(nullptr);
        if (module == nullptr)
            return false;

        BYTE* base = reinterpret_cast<BYTE*>(module);
        auto dosHeader = reinterpret_cast<IMAGE_DOS_HEADER*>(base);
        if (dosHeader->e_magic != IMAGE_DOS_SIGNATURE)
            return false;

        auto ntHeaders = reinterpret_cast<IMAGE_NT_HEADERS*>(base + dosHeader->e_lfanew);
        if (ntHeaders->Signature != IMAGE_NT_SIGNATURE)
            return false;

        IMAGE_DATA_DIRECTORY importDirectory =
            ntHeaders->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
        if (importDirectory.VirtualAddress == 0)
            return false;

        auto descriptor = reinterpret_cast<IMAGE_IMPORT_DESCRIPTOR*>(
            base + importDirectory.VirtualAddress);
        for (; descriptor->Name != 0; descriptor++)
        {
            const char* dllName = reinterpret_cast<const char*>(base + descriptor->Name);
            if (lstrcmpiA(dllName, "d3d9.dll") != 0)
                continue;

            IMAGE_THUNK_DATA* nameThunk = reinterpret_cast<IMAGE_THUNK_DATA*>(
                base + (descriptor->OriginalFirstThunk != 0
                    ? descriptor->OriginalFirstThunk
                    : descriptor->FirstThunk));
            IMAGE_THUNK_DATA* addressThunk = reinterpret_cast<IMAGE_THUNK_DATA*>(
                base + descriptor->FirstThunk);

            for (; nameThunk->u1.AddressOfData != 0; nameThunk++, addressThunk++)
            {
                if (IMAGE_SNAP_BY_ORDINAL(nameThunk->u1.Ordinal))
                    continue;

                auto importByName = reinterpret_cast<IMAGE_IMPORT_BY_NAME*>(
                    base + nameThunk->u1.AddressOfData);
                const char* importName = reinterpret_cast<const char*>(importByName->Name);
                if (lstrcmpA(importName, "Direct3DCreate9") != 0)
                    continue;

                void** slot = reinterpret_cast<void**>(&addressThunk->u1.Function);
                if (*slot == reinterpret_cast<void*>(&HookedDirect3DCreate9))
                    return true;

                if (OriginalDirect3DCreate9 == nullptr)
                    OriginalDirect3DCreate9 = reinterpret_cast<direct3d_create9_fn>(*slot);

                DWORD oldProtect = 0;
                if (!VirtualProtect(slot, sizeof(void*), PAGE_EXECUTE_READWRITE, &oldProtect))
                {
                    AppendLogUInt(log, L"umbra_dx9_iat_virtualprotect_error", GetLastError());
                    return false;
                }

                *slot = reinterpret_cast<void*>(&HookedDirect3DCreate9);
                DWORD ignored = 0;
                VirtualProtect(slot, sizeof(void*), oldProtect, &ignored);
                FlushInstructionCache(GetCurrentProcess(), slot, sizeof(void*));
                AppendLogLiteral(log, L"umbra_dx9_direct3dcreate9_hook_strategy=iat");
                return true;
            }
        }

        return false;
    }

    bool PatchVTableSlot(void** slot, void* replacement, void** original)
    {
        if (slot == nullptr || replacement == nullptr || original == nullptr)
            return false;

        if (*slot == replacement)
            return true;

        if (*original == nullptr)
            *original = *slot;

        DWORD oldProtect = 0;
        if (!VirtualProtect(slot, sizeof(void*), PAGE_EXECUTE_READWRITE, &oldProtect))
            return false;

        *slot = replacement;
        DWORD ignored = 0;
        VirtualProtect(slot, sizeof(void*), oldProtect, &ignored);
        FlushInstructionCache(GetCurrentProcess(), slot, sizeof(void*));
        return true;
    }

    void ResolveUser32Input()
    {
        if (User32GetAsyncKeyState != nullptr
            && User32GetCursorPos != nullptr
            && User32ScreenToClient != nullptr)
        {
            return;
        }

        HMODULE user32 = GetModuleHandleW(L"user32.dll");
        if (user32 == nullptr)
            user32 = LoadLibraryW(L"user32.dll");
        if (user32 == nullptr)
            return;

        User32GetAsyncKeyState = reinterpret_cast<get_async_key_state_fn>(
            GetProcAddress(user32, "GetAsyncKeyState"));
        User32GetCursorPos = reinterpret_cast<get_cursor_pos_fn>(
            GetProcAddress(user32, "GetCursorPos"));
        User32ScreenToClient = reinterpret_cast<screen_to_client_fn>(
            GetProcAddress(user32, "ScreenToClient"));
    }

    bool IsRectHot(const OverlayRect& rect)
    {
        return MouseX >= rect.x
            && MouseY >= rect.y
            && MouseX < rect.x + rect.width
            && MouseY < rect.y + rect.height;
    }

    bool IsKeyPressed(int virtualKey, bool& lastDown)
    {
        ResolveUser32Input();
        if (User32GetAsyncKeyState == nullptr)
            return false;

        bool down = (User32GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        bool pressed = down && !lastDown;
        lastDown = down;
        return pressed;
    }

    void UpdateOverlayInput()
    {
        ResolveUser32Input();

        MouseClicked = false;
        if (User32GetCursorPos != nullptr
            && User32ScreenToClient != nullptr
            && User32GetAsyncKeyState != nullptr
            && GameWindow != nullptr)
        {
            POINT point{};
            if (User32GetCursorPos(&point) && User32ScreenToClient(GameWindow, &point))
            {
                MouseX = point.x;
                MouseY = point.y;
            }

            bool mouseDown = (User32GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
            MouseClicked = mouseDown && !LastMouseDown;
            MouseDown = mouseDown;
            LastMouseDown = mouseDown;
        }

        if (IsKeyPressed(VK_INSERT, LastInsertDown))
        {
            SettingsWindowOpen = !SettingsWindowOpen;
            if (SettingsWindowOpen)
                PluginInstallerOpen = false;
            UmbraDockExpanded = true;
            UmbraDockLastInteractionTicks = GetTickCount();
        }
        if (IsKeyPressed(VK_F9, LastF9Down))
        {
            SettingsWindowOpen = !SettingsWindowOpen;
            if (SettingsWindowOpen)
                PluginInstallerOpen = false;
            UmbraDockExpanded = true;
            UmbraDockLastInteractionTicks = GetTickCount();
        }
        if (IsKeyPressed(VK_F10, LastF10Down))
        {
            PluginInstallerOpen = !PluginInstallerOpen;
            if (PluginInstallerOpen)
                SettingsWindowOpen = false;
            UmbraDockExpanded = true;
            UmbraDockLastInteractionTicks = GetTickCount();
        }
        if (IsKeyPressed(VK_F11, LastF11Down) && DevUiEnabled)
        {
            UmbraDeveloperBarVisible = !UmbraDeveloperBarVisible;
            UmbraDockExpanded = true;
            UmbraDockLastInteractionTicks = GetTickCount();
        }
    }

    void OverlayBegin()
    {
        OverlayVertexCount = 0;
    }

    void OverlayAddVertex(float x, float y, D3DCOLOR color)
    {
        if (OverlayVertexCount >= OverlayMaxVertices)
            return;

        OverlayVertices[OverlayVertexCount++] = OverlayVertex{ x, y, 0.0f, 1.0f, color };
    }

    void OverlayAddRect(float x, float y, float width, float height, D3DCOLOR color)
    {
        if (width <= 0.0f || height <= 0.0f || OverlayVertexCount + 6 >= OverlayMaxVertices)
            return;

        float right = x + width;
        float bottom = y + height;
        OverlayAddVertex(x, y, color);
        OverlayAddVertex(right, y, color);
        OverlayAddVertex(right, bottom, color);
        OverlayAddVertex(x, y, color);
        OverlayAddVertex(right, bottom, color);
        OverlayAddVertex(x, bottom, color);
    }

    void OverlayAddBorder(const OverlayRect& rect, D3DCOLOR color)
    {
        OverlayAddRect(static_cast<float>(rect.x), static_cast<float>(rect.y), static_cast<float>(rect.width), 1.0f, color);
        OverlayAddRect(static_cast<float>(rect.x), static_cast<float>(rect.y + rect.height - 1), static_cast<float>(rect.width), 1.0f, color);
        OverlayAddRect(static_cast<float>(rect.x), static_cast<float>(rect.y), 1.0f, static_cast<float>(rect.height), color);
        OverlayAddRect(static_cast<float>(rect.x + rect.width - 1), static_cast<float>(rect.y), 1.0f, static_cast<float>(rect.height), color);
    }

    void OverlayAddPanel(const OverlayRect& rect, D3DCOLOR fill, D3DCOLOR border)
    {
        OverlayAddRect(static_cast<float>(rect.x + 3), static_cast<float>(rect.y + 4), static_cast<float>(rect.width), static_cast<float>(rect.height), D3DCOLOR_ARGB(120, 0, 0, 0));
        OverlayAddRect(static_cast<float>(rect.x), static_cast<float>(rect.y), static_cast<float>(rect.width), static_cast<float>(rect.height), fill);
        OverlayAddBorder(rect, border);
    }

    void OverlayFlush(IDirect3DDevice9* device)
    {
        if (device == nullptr || OverlayVertexCount < 3)
            return;

        device->DrawPrimitiveUP(
            D3DPT_TRIANGLELIST,
            OverlayVertexCount / 3,
            OverlayVertices,
            sizeof(OverlayVertex));
    }

    void GetGlyphRows(char value, BYTE rows[7])
    {
        for (int index = 0; index < 7; index++)
            rows[index] = 0;

        if (value >= 'a' && value <= 'z')
            value = static_cast<char>(value - ('a' - 'A'));

        switch (value)
        {
            case 'A': rows[0]=0x0E; rows[1]=0x11; rows[2]=0x11; rows[3]=0x1F; rows[4]=0x11; rows[5]=0x11; rows[6]=0x11; break;
            case 'B': rows[0]=0x1E; rows[1]=0x11; rows[2]=0x11; rows[3]=0x1E; rows[4]=0x11; rows[5]=0x11; rows[6]=0x1E; break;
            case 'C': rows[0]=0x0E; rows[1]=0x11; rows[2]=0x10; rows[3]=0x10; rows[4]=0x10; rows[5]=0x11; rows[6]=0x0E; break;
            case 'D': rows[0]=0x1E; rows[1]=0x11; rows[2]=0x11; rows[3]=0x11; rows[4]=0x11; rows[5]=0x11; rows[6]=0x1E; break;
            case 'E': rows[0]=0x1F; rows[1]=0x10; rows[2]=0x10; rows[3]=0x1E; rows[4]=0x10; rows[5]=0x10; rows[6]=0x1F; break;
            case 'F': rows[0]=0x1F; rows[1]=0x10; rows[2]=0x10; rows[3]=0x1E; rows[4]=0x10; rows[5]=0x10; rows[6]=0x10; break;
            case 'G': rows[0]=0x0E; rows[1]=0x11; rows[2]=0x10; rows[3]=0x17; rows[4]=0x11; rows[5]=0x11; rows[6]=0x0E; break;
            case 'H': rows[0]=0x11; rows[1]=0x11; rows[2]=0x11; rows[3]=0x1F; rows[4]=0x11; rows[5]=0x11; rows[6]=0x11; break;
            case 'I': rows[0]=0x0E; rows[1]=0x04; rows[2]=0x04; rows[3]=0x04; rows[4]=0x04; rows[5]=0x04; rows[6]=0x0E; break;
            case 'J': rows[0]=0x01; rows[1]=0x01; rows[2]=0x01; rows[3]=0x01; rows[4]=0x11; rows[5]=0x11; rows[6]=0x0E; break;
            case 'K': rows[0]=0x11; rows[1]=0x12; rows[2]=0x14; rows[3]=0x18; rows[4]=0x14; rows[5]=0x12; rows[6]=0x11; break;
            case 'L': rows[0]=0x10; rows[1]=0x10; rows[2]=0x10; rows[3]=0x10; rows[4]=0x10; rows[5]=0x10; rows[6]=0x1F; break;
            case 'M': rows[0]=0x11; rows[1]=0x1B; rows[2]=0x15; rows[3]=0x15; rows[4]=0x11; rows[5]=0x11; rows[6]=0x11; break;
            case 'N': rows[0]=0x11; rows[1]=0x19; rows[2]=0x15; rows[3]=0x13; rows[4]=0x11; rows[5]=0x11; rows[6]=0x11; break;
            case 'O': rows[0]=0x0E; rows[1]=0x11; rows[2]=0x11; rows[3]=0x11; rows[4]=0x11; rows[5]=0x11; rows[6]=0x0E; break;
            case 'P': rows[0]=0x1E; rows[1]=0x11; rows[2]=0x11; rows[3]=0x1E; rows[4]=0x10; rows[5]=0x10; rows[6]=0x10; break;
            case 'Q': rows[0]=0x0E; rows[1]=0x11; rows[2]=0x11; rows[3]=0x11; rows[4]=0x15; rows[5]=0x12; rows[6]=0x0D; break;
            case 'R': rows[0]=0x1E; rows[1]=0x11; rows[2]=0x11; rows[3]=0x1E; rows[4]=0x14; rows[5]=0x12; rows[6]=0x11; break;
            case 'S': rows[0]=0x0F; rows[1]=0x10; rows[2]=0x10; rows[3]=0x0E; rows[4]=0x01; rows[5]=0x01; rows[6]=0x1E; break;
            case 'T': rows[0]=0x1F; rows[1]=0x04; rows[2]=0x04; rows[3]=0x04; rows[4]=0x04; rows[5]=0x04; rows[6]=0x04; break;
            case 'U': rows[0]=0x11; rows[1]=0x11; rows[2]=0x11; rows[3]=0x11; rows[4]=0x11; rows[5]=0x11; rows[6]=0x0E; break;
            case 'V': rows[0]=0x11; rows[1]=0x11; rows[2]=0x11; rows[3]=0x11; rows[4]=0x11; rows[5]=0x0A; rows[6]=0x04; break;
            case 'W': rows[0]=0x11; rows[1]=0x11; rows[2]=0x11; rows[3]=0x15; rows[4]=0x15; rows[5]=0x15; rows[6]=0x0A; break;
            case 'X': rows[0]=0x11; rows[1]=0x11; rows[2]=0x0A; rows[3]=0x04; rows[4]=0x0A; rows[5]=0x11; rows[6]=0x11; break;
            case 'Y': rows[0]=0x11; rows[1]=0x11; rows[2]=0x0A; rows[3]=0x04; rows[4]=0x04; rows[5]=0x04; rows[6]=0x04; break;
            case 'Z': rows[0]=0x1F; rows[1]=0x01; rows[2]=0x02; rows[3]=0x04; rows[4]=0x08; rows[5]=0x10; rows[6]=0x1F; break;
            case '0': rows[0]=0x0E; rows[1]=0x11; rows[2]=0x13; rows[3]=0x15; rows[4]=0x19; rows[5]=0x11; rows[6]=0x0E; break;
            case '1': rows[0]=0x04; rows[1]=0x0C; rows[2]=0x04; rows[3]=0x04; rows[4]=0x04; rows[5]=0x04; rows[6]=0x0E; break;
            case '2': rows[0]=0x0E; rows[1]=0x11; rows[2]=0x01; rows[3]=0x02; rows[4]=0x04; rows[5]=0x08; rows[6]=0x1F; break;
            case '3': rows[0]=0x1E; rows[1]=0x01; rows[2]=0x01; rows[3]=0x0E; rows[4]=0x01; rows[5]=0x01; rows[6]=0x1E; break;
            case '4': rows[0]=0x02; rows[1]=0x06; rows[2]=0x0A; rows[3]=0x12; rows[4]=0x1F; rows[5]=0x02; rows[6]=0x02; break;
            case '5': rows[0]=0x1F; rows[1]=0x10; rows[2]=0x10; rows[3]=0x1E; rows[4]=0x01; rows[5]=0x01; rows[6]=0x1E; break;
            case '6': rows[0]=0x06; rows[1]=0x08; rows[2]=0x10; rows[3]=0x1E; rows[4]=0x11; rows[5]=0x11; rows[6]=0x0E; break;
            case '7': rows[0]=0x1F; rows[1]=0x01; rows[2]=0x02; rows[3]=0x04; rows[4]=0x08; rows[5]=0x08; rows[6]=0x08; break;
            case '8': rows[0]=0x0E; rows[1]=0x11; rows[2]=0x11; rows[3]=0x0E; rows[4]=0x11; rows[5]=0x11; rows[6]=0x0E; break;
            case '9': rows[0]=0x0E; rows[1]=0x11; rows[2]=0x11; rows[3]=0x0F; rows[4]=0x01; rows[5]=0x02; rows[6]=0x0C; break;
            case ':': rows[1]=0x04; rows[2]=0x04; rows[4]=0x04; rows[5]=0x04; break;
            case '.': rows[5]=0x04; rows[6]=0x04; break;
            case '-': rows[3]=0x0E; break;
            case '/': rows[0]=0x01; rows[1]=0x02; rows[2]=0x02; rows[3]=0x04; rows[4]=0x08; rows[5]=0x08; rows[6]=0x10; break;
            case '+': rows[1]=0x04; rows[2]=0x04; rows[3]=0x1F; rows[4]=0x04; rows[5]=0x04; break;
            case '!': rows[0]=0x04; rows[1]=0x04; rows[2]=0x04; rows[3]=0x04; rows[5]=0x04; break;
            default: break;
        }
    }

    void OverlayAddText(int x, int y, const char* text, int scale, D3DCOLOR color)
    {
        if (text == nullptr || scale <= 0)
            return;

        int cursorX = x;
        for (DWORD charIndex = 0; text[charIndex] != '\0'; charIndex++)
        {
            char value = text[charIndex];
            if (value == ' ')
            {
                cursorX += 4 * scale;
                continue;
            }

            BYTE rows[7]{};
            GetGlyphRows(value, rows);
            for (int row = 0; row < 7; row++)
            {
                for (int column = 0; column < 5; column++)
                {
                    if ((rows[row] & (1 << (4 - column))) != 0)
                    {
                        OverlayAddRect(
                            static_cast<float>(cursorX + column * scale),
                            static_cast<float>(y + row * scale),
                            static_cast<float>(scale),
                            static_cast<float>(scale),
                            color);
                    }
                }
            }

            cursorX += 6 * scale;
        }
    }

    void DrawIcon(const OverlayRect& rect, const char* label, bool active, bool hot)
    {
        D3DCOLOR fill = active
            ? D3DCOLOR_ARGB(224, 24, 112, 160)
            : (hot ? D3DCOLOR_ARGB(224, 36, 55, 68) : D3DCOLOR_ARGB(212, 18, 27, 34));
        D3DCOLOR border = active
            ? D3DCOLOR_ARGB(255, 0, 204, 255)
            : D3DCOLOR_ARGB(220, 80, 105, 120);

        OverlayAddPanel(rect, fill, border);
        OverlayAddText(rect.x + 10, rect.y + 8, label, 2, D3DCOLOR_ARGB(255, 236, 250, 255));
    }

    void DrawBottomRightToasts(int viewportWidth, int viewportHeight)
    {
        if (OverlayStartTicks == 0)
            OverlayStartTicks = GetTickCount();

        DWORD elapsed = GetTickCount() - OverlayStartTicks;
        if (elapsed > ToastVisibleMs)
            return;

        const int width = 330;
        const int height = 34;
        const int gap = 8;
        int x = viewportWidth - width - 18;
        int y = viewportHeight - ((height + gap) * 3) - 18;

        const char* messages[] =
        {
            "UMBRA FRAMEWORK READY",
            "NATIVE DX9 UI ACTIVE",
            "PLUGIN EXECUTION DISABLED"
        };

        for (int index = 0; index < 3; index++)
        {
            OverlayRect rect{ x, y + index * (height + gap), width, height };
            D3DCOLOR border = index == 2
                ? D3DCOLOR_ARGB(240, 245, 185, 65)
                : D3DCOLOR_ARGB(240, 0, 204, 255);
            OverlayAddPanel(rect, D3DCOLOR_ARGB(220, 12, 18, 24), border);
            OverlayAddText(rect.x + 12, rect.y + 10, messages[index], 2, D3DCOLOR_ARGB(255, 238, 246, 250));
        }
    }

    void DrawSettingsWindow()
    {
        OverlayRect rect{ 8, 48, 370, 176 };
        OverlayRect closeRect{ rect.x + rect.width - 28, rect.y + 8, 18, 18 };
        OverlayAddPanel(rect, D3DCOLOR_ARGB(232, 10, 18, 25), D3DCOLOR_ARGB(240, 0, 204, 255));
        OverlayAddText(rect.x + 14, rect.y + 14, "UMBRA SETTINGS", 2, D3DCOLOR_ARGB(255, 236, 250, 255));
        OverlayAddPanel(closeRect, D3DCOLOR_ARGB(220, 35, 46, 54), D3DCOLOR_ARGB(210, 100, 120, 132));
        OverlayAddText(closeRect.x + 5, closeRect.y + 4, "X", 1, D3DCOLOR_ARGB(255, 240, 245, 248));

        OverlayAddText(rect.x + 18, rect.y + 48, "DEBUG LOGGING: ON", 2, D3DCOLOR_ARGB(245, 182, 231, 255));
        OverlayAddText(rect.x + 18, rect.y + 72, "DEV UI: ON", 2, D3DCOLOR_ARGB(245, 182, 231, 255));
        OverlayAddText(rect.x + 18, rect.y + 96, "SAFE MODE: OFF", 2, D3DCOLOR_ARGB(245, 182, 231, 255));
        OverlayAddText(rect.x + 18, rect.y + 120, "DX9 HOOK: READY", 2, D3DCOLOR_ARGB(245, 182, 231, 255));
        OverlayAddText(rect.x + 18, rect.y + 144, "IMGUI: PENDING", 2, D3DCOLOR_ARGB(245, 210, 218, 132));

        if (MouseClicked && IsRectHot(closeRect))
            SettingsWindowOpen = false;
    }

    void DrawPluginInstallerWindow()
    {
        OverlayRect rect{ 390, 48, 500, 216 };
        OverlayRect closeRect{ rect.x + rect.width - 28, rect.y + 8, 18, 18 };
        OverlayAddPanel(rect, D3DCOLOR_ARGB(232, 10, 18, 25), D3DCOLOR_ARGB(240, 122, 190, 86));
        OverlayAddText(rect.x + 14, rect.y + 14, "PLUGIN INSTALLER", 2, D3DCOLOR_ARGB(255, 238, 250, 238));
        OverlayAddPanel(closeRect, D3DCOLOR_ARGB(220, 35, 46, 54), D3DCOLOR_ARGB(210, 100, 120, 132));
        OverlayAddText(closeRect.x + 5, closeRect.y + 4, "X", 1, D3DCOLOR_ARGB(255, 240, 245, 248));

        OverlayRect installedTab{ rect.x + 18, rect.y + 48, 108, 26 };
        OverlayRect supportedTab{ rect.x + 132, rect.y + 48, 112, 26 };
        OverlayRect availableTab{ rect.x + 250, rect.y + 48, 112, 26 };
        OverlayRect updatesTab{ rect.x + 368, rect.y + 48, 90, 26 };
        OverlayAddPanel(installedTab, D3DCOLOR_ARGB(220, 28, 50, 38), D3DCOLOR_ARGB(220, 122, 190, 86));
        OverlayAddPanel(supportedTab, D3DCOLOR_ARGB(180, 18, 28, 34), D3DCOLOR_ARGB(140, 80, 105, 120));
        OverlayAddPanel(availableTab, D3DCOLOR_ARGB(180, 18, 28, 34), D3DCOLOR_ARGB(140, 80, 105, 120));
        OverlayAddPanel(updatesTab, D3DCOLOR_ARGB(180, 18, 28, 34), D3DCOLOR_ARGB(140, 80, 105, 120));
        OverlayAddText(installedTab.x + 9, installedTab.y + 8, "INSTALLED", 1, D3DCOLOR_ARGB(255, 238, 250, 238));
        OverlayAddText(supportedTab.x + 9, supportedTab.y + 8, "SUPPORTED", 1, D3DCOLOR_ARGB(230, 210, 225, 230));
        OverlayAddText(availableTab.x + 9, availableTab.y + 8, "AVAILABLE", 1, D3DCOLOR_ARGB(230, 210, 225, 230));
        OverlayAddText(updatesTab.x + 9, updatesTab.y + 8, "UPDATES", 1, D3DCOLOR_ARGB(230, 210, 225, 230));

        OverlayAddText(rect.x + 24, rect.y + 96, "SEARCH:", 2, D3DCOLOR_ARGB(245, 190, 215, 220));
        OverlayRect searchBox{ rect.x + 118, rect.y + 90, 330, 26 };
        OverlayAddPanel(searchBox, D3DCOLOR_ARGB(185, 5, 10, 14), D3DCOLOR_ARGB(150, 80, 105, 120));
        OverlayAddText(rect.x + 24, rect.y + 132, "NO PLUGINS INSTALLED", 2, D3DCOLOR_ARGB(245, 210, 225, 230));
        OverlayAddText(rect.x + 24, rect.y + 156, "SUPPORTED REPOS: 0", 2, D3DCOLOR_ARGB(245, 210, 225, 230));
        OverlayAddText(rect.x + 24, rect.y + 180, "INSTALL UI PENDING", 2, D3DCOLOR_ARGB(245, 245, 210, 132));

        if (MouseClicked && IsRectHot(closeRect))
            PluginInstallerOpen = false;
    }

    struct UmbraTheme
    {
        const char* name;
        ImVec4 windowBg;
        ImVec4 childBg;
        ImVec4 popupBg;
        ImVec4 titleBg;
        ImVec4 titleBgActive;
        ImVec4 border;
        ImVec4 accent;
        ImVec4 accentHover;
        ImVec4 accentActive;
        ImVec4 button;
        ImVec4 buttonHovered;
        ImVec4 buttonActive;
        ImVec4 frameBg;
        ImVec4 frameHovered;
        ImVec4 frameActive;
        ImVec4 tab;
        ImVec4 tabHovered;
        ImVec4 tabSelected;
        ImVec4 text;
        ImVec4 mutedText;
        ImVec4 warning;
        ImVec4 danger;
        ImVec4 shadow;
        ImVec4 toastBg;
    };

    struct UmbraThemeTuning
    {
        float windowOpacity;
        float fontSize;
        float uiScale;
        float rounding;
        bool gradientEnabled;
        ImVec4 gradientStart;
        ImVec4 gradientMiddle;
        ImVec4 gradientEnd;
    };

    UmbraTheme CustomUmbraTheme{};
    UmbraThemeTuning UmbraThemeTunings[4]{};
    bool UmbraAppearanceDefaultsInitialized = false;
    bool UmbraCustomThemeInitialized = false;
    bool UmbraAppearanceDirty = false;
    wchar_t UmbraAppearanceSettingsPath[BufferChars]{};

    const UmbraTheme& GetUmbraTheme()
    {
        static const UmbraTheme themes[] =
        {
            {
                "Aether Glass",
                ImVec4(0.025f, 0.045f, 0.062f, 0.76f),
                ImVec4(0.040f, 0.070f, 0.090f, 0.58f),
                ImVec4(0.025f, 0.045f, 0.062f, 0.94f),
                ImVec4(0.025f, 0.060f, 0.082f, 0.86f),
                ImVec4(0.035f, 0.150f, 0.205f, 0.94f),
                ImVec4(0.35f, 0.85f, 1.00f, 0.54f),
                ImVec4(0.18f, 0.82f, 1.00f, 1.00f),
                ImVec4(0.36f, 0.92f, 1.00f, 1.00f),
                ImVec4(0.09f, 0.58f, 0.82f, 1.00f),
                ImVec4(0.070f, 0.245f, 0.315f, 0.88f),
                ImVec4(0.070f, 0.405f, 0.520f, 0.96f),
                ImVec4(0.035f, 0.580f, 0.750f, 1.00f),
                ImVec4(0.055f, 0.090f, 0.115f, 0.78f),
                ImVec4(0.080f, 0.190f, 0.235f, 0.90f),
                ImVec4(0.090f, 0.300f, 0.365f, 0.96f),
                ImVec4(0.035f, 0.090f, 0.115f, 0.82f),
                ImVec4(0.070f, 0.360f, 0.460f, 0.96f),
                ImVec4(0.050f, 0.220f, 0.290f, 0.96f),
                ImVec4(0.92f, 0.98f, 1.00f, 1.00f),
                ImVec4(0.58f, 0.68f, 0.74f, 1.00f),
                ImVec4(1.00f, 0.78f, 0.28f, 1.00f),
                ImVec4(1.00f, 0.34f, 0.38f, 1.00f),
                ImVec4(0.00f, 0.03f, 0.05f, 0.46f),
                ImVec4(0.025f, 0.045f, 0.060f, 0.86f),
            },
            {
                "Dalamud Dark",
                ImVec4(0.050f, 0.052f, 0.068f, 0.86f),
                ImVec4(0.070f, 0.072f, 0.092f, 0.72f),
                ImVec4(0.045f, 0.046f, 0.060f, 0.96f),
                ImVec4(0.060f, 0.060f, 0.080f, 0.94f),
                ImVec4(0.115f, 0.090f, 0.185f, 0.98f),
                ImVec4(0.46f, 0.40f, 0.78f, 0.50f),
                ImVec4(0.54f, 0.46f, 1.00f, 1.00f),
                ImVec4(0.68f, 0.60f, 1.00f, 1.00f),
                ImVec4(0.38f, 0.30f, 0.82f, 1.00f),
                ImVec4(0.140f, 0.130f, 0.210f, 0.90f),
                ImVec4(0.220f, 0.190f, 0.340f, 0.96f),
                ImVec4(0.330f, 0.280f, 0.550f, 1.00f),
                ImVec4(0.095f, 0.095f, 0.122f, 0.86f),
                ImVec4(0.150f, 0.140f, 0.210f, 0.92f),
                ImVec4(0.220f, 0.190f, 0.310f, 0.98f),
                ImVec4(0.085f, 0.082f, 0.110f, 0.86f),
                ImVec4(0.240f, 0.200f, 0.390f, 0.96f),
                ImVec4(0.180f, 0.145f, 0.300f, 0.96f),
                ImVec4(0.94f, 0.94f, 0.98f, 1.00f),
                ImVec4(0.62f, 0.62f, 0.70f, 1.00f),
                ImVec4(1.00f, 0.78f, 0.34f, 1.00f),
                ImVec4(1.00f, 0.36f, 0.48f, 1.00f),
                ImVec4(0.02f, 0.02f, 0.04f, 0.52f),
                ImVec4(0.050f, 0.052f, 0.068f, 0.90f),
            },
            {
                "Aether Ivory",
                ImVec4(0.86f, 0.88f, 0.86f, 0.74f),
                ImVec4(0.96f, 0.96f, 0.92f, 0.58f),
                ImVec4(0.88f, 0.88f, 0.84f, 0.96f),
                ImVec4(0.70f, 0.73f, 0.72f, 0.88f),
                ImVec4(0.86f, 0.78f, 0.56f, 0.96f),
                ImVec4(0.58f, 0.48f, 0.30f, 0.48f),
                ImVec4(0.86f, 0.60f, 0.22f, 1.00f),
                ImVec4(0.98f, 0.74f, 0.36f, 1.00f),
                ImVec4(0.70f, 0.43f, 0.16f, 1.00f),
                ImVec4(0.72f, 0.62f, 0.44f, 0.76f),
                ImVec4(0.84f, 0.70f, 0.48f, 0.86f),
                ImVec4(0.92f, 0.64f, 0.28f, 0.96f),
                ImVec4(0.78f, 0.78f, 0.72f, 0.62f),
                ImVec4(0.86f, 0.80f, 0.64f, 0.80f),
                ImVec4(0.93f, 0.76f, 0.48f, 0.92f),
                ImVec4(0.76f, 0.75f, 0.70f, 0.78f),
                ImVec4(0.90f, 0.72f, 0.44f, 0.90f),
                ImVec4(0.86f, 0.64f, 0.34f, 0.92f),
                ImVec4(0.10f, 0.12f, 0.13f, 1.00f),
                ImVec4(0.30f, 0.34f, 0.36f, 1.00f),
                ImVec4(0.80f, 0.48f, 0.08f, 1.00f),
                ImVec4(0.72f, 0.16f, 0.18f, 1.00f),
                ImVec4(0.02f, 0.02f, 0.01f, 0.32f),
                ImVec4(0.88f, 0.88f, 0.84f, 0.88f),
            },
        };

        if (UmbraThemeIndex == 3)
            return CustomUmbraTheme;
        if (UmbraThemeIndex < 0 || UmbraThemeIndex >= static_cast<int>(sizeof(themes) / sizeof(themes[0])))
            UmbraThemeIndex = 0;
        return themes[UmbraThemeIndex];
    }

    const char* const* GetUmbraThemeNames()
    {
        static const char* names[] = { "Aether Glass", "Dalamud Dark", "Aether Ivory", "Custom" };
        return names;
    }

    int GetUmbraThemeCount()
    {
        return 4;
    }

    UmbraThemeTuning& GetUmbraThemeTuning()
    {
        int index = UmbraThemeIndex;
        if (index < 0 || index >= 4)
            index = 0;
        return UmbraThemeTunings[index];
    }

    float ClampUmbraFloat(float value, float minimum, float maximum)
    {
        return value < minimum ? minimum : value > maximum ? maximum : value;
    }

    bool ResolveUmbraAppearanceSettingsPath()
    {
        if (UmbraAppearanceSettingsPath[0] != L'\0')
            return true;
        wchar_t pluginDirectory[BufferChars]{};
        wchar_t umbraDirectory[BufferChars]{};
        if (!GetUmbraEnvironmentValue(L"PLUGIN_DIR", pluginDirectory, BufferChars))
            return false;
        ParentDirectory(pluginDirectory, umbraDirectory, BufferChars);
        CombinePath(umbraDirectory, L"umbra-ui.ini", UmbraAppearanceSettingsPath, BufferChars);
        return UmbraAppearanceSettingsPath[0] != L'\0';
    }

    int ReadUmbraProfileInt(const wchar_t* section, const wchar_t* key, int fallback)
    {
        if (!ResolveUmbraAppearanceSettingsPath())
            return fallback;
        return static_cast<int>(GetPrivateProfileIntW(section, key, fallback, UmbraAppearanceSettingsPath));
    }

    void WriteUmbraProfileInt(const wchar_t* section, const wchar_t* key, int value)
    {
        if (!ResolveUmbraAppearanceSettingsPath())
            return;
        wchar_t buffer[32]{};
        if (value < 0)
        {
            buffer[0] = L'-';
            UIntToWide(static_cast<unsigned long>(-value), buffer + 1, 31);
        }
        else
        {
            UIntToWide(static_cast<unsigned long>(value), buffer, 32);
        }
        WritePrivateProfileStringW(section, key, buffer, UmbraAppearanceSettingsPath);
    }

    float ReadUmbraProfileFloat(const wchar_t* section, const wchar_t* key, float fallback)
    {
        int scaledFallback = static_cast<int>(fallback * 1000.0f + 0.5f);
        return static_cast<float>(ReadUmbraProfileInt(section, key, scaledFallback)) / 1000.0f;
    }

    void WriteUmbraProfileFloat(const wchar_t* section, const wchar_t* key, float value)
    {
        WriteUmbraProfileInt(section, key, static_cast<int>(value * 1000.0f + 0.5f));
    }

    ImVec4 ReadUmbraProfileColor(const wchar_t* section, const wchar_t* prefix, const ImVec4& fallback)
    {
        wchar_t key[64]{};
        wsprintfW(key, L"%sR", prefix);
        float r = static_cast<float>(ReadUmbraProfileInt(section, key, static_cast<int>(fallback.x * 255.0f + 0.5f))) / 255.0f;
        wsprintfW(key, L"%sG", prefix);
        float g = static_cast<float>(ReadUmbraProfileInt(section, key, static_cast<int>(fallback.y * 255.0f + 0.5f))) / 255.0f;
        wsprintfW(key, L"%sB", prefix);
        float b = static_cast<float>(ReadUmbraProfileInt(section, key, static_cast<int>(fallback.z * 255.0f + 0.5f))) / 255.0f;
        wsprintfW(key, L"%sA", prefix);
        float a = static_cast<float>(ReadUmbraProfileInt(section, key, static_cast<int>(fallback.w * 255.0f + 0.5f))) / 255.0f;
        return ImVec4(
            ClampUmbraFloat(r, 0.0f, 1.0f),
            ClampUmbraFloat(g, 0.0f, 1.0f),
            ClampUmbraFloat(b, 0.0f, 1.0f),
            ClampUmbraFloat(a, 0.0f, 1.0f));
    }

    void WriteUmbraProfileColor(const wchar_t* section, const wchar_t* prefix, const ImVec4& color)
    {
        wchar_t key[64]{};
        wsprintfW(key, L"%sR", prefix);
        WriteUmbraProfileInt(section, key, static_cast<int>(ClampUmbraFloat(color.x, 0.0f, 1.0f) * 255.0f + 0.5f));
        wsprintfW(key, L"%sG", prefix);
        WriteUmbraProfileInt(section, key, static_cast<int>(ClampUmbraFloat(color.y, 0.0f, 1.0f) * 255.0f + 0.5f));
        wsprintfW(key, L"%sB", prefix);
        WriteUmbraProfileInt(section, key, static_cast<int>(ClampUmbraFloat(color.z, 0.0f, 1.0f) * 255.0f + 0.5f));
        wsprintfW(key, L"%sA", prefix);
        WriteUmbraProfileInt(section, key, static_cast<int>(ClampUmbraFloat(color.w, 0.0f, 1.0f) * 255.0f + 0.5f));
    }

    void InitializeUmbraAppearanceDefaults()
    {
        if (UmbraAppearanceDefaultsInitialized)
            return;

        int previousIndex = UmbraThemeIndex;
        for (int index = 0; index < 3; index++)
        {
            UmbraThemeIndex = index;
            const UmbraTheme& theme = GetUmbraTheme();
            UmbraThemeTuning& tuning = UmbraThemeTunings[index];
            tuning.windowOpacity = 1.0f;
            tuning.fontSize = 16.0f;
            tuning.uiScale = 1.0f;
            tuning.rounding = 12.0f;
            tuning.gradientEnabled = true;
            tuning.gradientStart = ImVec4(theme.windowBg.x, theme.windowBg.y, theme.windowBg.z, 0.96f);
            tuning.gradientMiddle = ImVec4(theme.titleBgActive.x, theme.titleBgActive.y, theme.titleBgActive.z, 0.78f);
            tuning.gradientEnd = ImVec4(theme.accentActive.x * 0.28f, theme.accentActive.y * 0.28f, theme.accentActive.z * 0.34f, 0.72f);
        }
        UmbraThemeIndex = 0;
        CustomUmbraTheme = GetUmbraTheme();
        CustomUmbraTheme.name = "Custom";
        UmbraThemeTunings[3] = UmbraThemeTunings[0];
        UmbraThemeIndex = previousIndex >= 0 && previousIndex < 4 ? previousIndex : 0;
        UmbraAppearanceDefaultsInitialized = true;
    }

    void CloneCurrentUmbraThemeToCustom()
    {
        int sourceIndex = UmbraThemeIndex >= 0 && UmbraThemeIndex < 3 ? UmbraThemeIndex : 0;
        int previousIndex = UmbraThemeIndex;
        UmbraThemeIndex = sourceIndex;
        CustomUmbraTheme = GetUmbraTheme();
        CustomUmbraTheme.name = "Custom";
        UmbraThemeTunings[3] = UmbraThemeTunings[sourceIndex];
        UmbraThemeIndex = previousIndex;
        UmbraCustomThemeInitialized = true;
        UmbraAppearanceDirty = true;
    }

    void LoadUmbraAppearanceSettings()
    {
        InitializeUmbraAppearanceDefaults();
        if (!ResolveUmbraAppearanceSettingsPath())
            return;

        static const wchar_t* sections[] = {
            L"Appearance.AetherGlass",
            L"Appearance.DalamudDark",
            L"Appearance.AetherIvory",
            L"Appearance.Custom"
        };
        for (int index = 0; index < 4; index++)
        {
            UmbraThemeTuning& tuning = UmbraThemeTunings[index];
            tuning.windowOpacity = ClampUmbraFloat(ReadUmbraProfileFloat(sections[index], L"WindowOpacity", tuning.windowOpacity), 0.25f, 1.0f);
            tuning.fontSize = ClampUmbraFloat(ReadUmbraProfileFloat(sections[index], L"FontSize", tuning.fontSize), 13.0f, 22.0f);
            tuning.uiScale = ClampUmbraFloat(ReadUmbraProfileFloat(sections[index], L"UiScale", tuning.uiScale), 0.75f, 1.40f);
            tuning.rounding = ClampUmbraFloat(ReadUmbraProfileFloat(sections[index], L"Rounding", tuning.rounding), 0.0f, 20.0f);
            tuning.gradientEnabled = ReadUmbraProfileInt(sections[index], L"GradientEnabled", tuning.gradientEnabled ? 1 : 0) != 0;
            tuning.gradientStart = ReadUmbraProfileColor(sections[index], L"GradientStart", tuning.gradientStart);
            tuning.gradientMiddle = ReadUmbraProfileColor(sections[index], L"GradientMiddle", tuning.gradientMiddle);
            tuning.gradientEnd = ReadUmbraProfileColor(sections[index], L"GradientEnd", tuning.gradientEnd);
        }

        UmbraCustomThemeInitialized = ReadUmbraProfileInt(L"Appearance", L"CustomInitialized", 0) != 0;
        if (UmbraCustomThemeInitialized)
        {
            const wchar_t* section = L"Appearance.Custom";
            CustomUmbraTheme.windowBg = ReadUmbraProfileColor(section, L"Window", CustomUmbraTheme.windowBg);
            CustomUmbraTheme.childBg = ReadUmbraProfileColor(section, L"Child", CustomUmbraTheme.childBg);
            CustomUmbraTheme.titleBg = ReadUmbraProfileColor(section, L"Title", CustomUmbraTheme.titleBg);
            CustomUmbraTheme.titleBgActive = ReadUmbraProfileColor(section, L"TitleActive", CustomUmbraTheme.titleBgActive);
            CustomUmbraTheme.border = ReadUmbraProfileColor(section, L"Border", CustomUmbraTheme.border);
            CustomUmbraTheme.accent = ReadUmbraProfileColor(section, L"Accent", CustomUmbraTheme.accent);
            CustomUmbraTheme.accentHover = ReadUmbraProfileColor(section, L"AccentHover", CustomUmbraTheme.accentHover);
            CustomUmbraTheme.accentActive = ReadUmbraProfileColor(section, L"AccentActive", CustomUmbraTheme.accentActive);
            CustomUmbraTheme.button = ReadUmbraProfileColor(section, L"Button", CustomUmbraTheme.button);
            CustomUmbraTheme.buttonHovered = ReadUmbraProfileColor(section, L"ButtonHover", CustomUmbraTheme.buttonHovered);
            CustomUmbraTheme.buttonActive = ReadUmbraProfileColor(section, L"ButtonActive", CustomUmbraTheme.buttonActive);
            CustomUmbraTheme.frameBg = ReadUmbraProfileColor(section, L"Secondary", CustomUmbraTheme.frameBg);
            CustomUmbraTheme.text = ReadUmbraProfileColor(section, L"Text", CustomUmbraTheme.text);
            CustomUmbraTheme.mutedText = ReadUmbraProfileColor(section, L"MutedText", CustomUmbraTheme.mutedText);
        }

        UmbraThemeIndex = ReadUmbraProfileInt(L"Appearance", L"ActiveTheme", 0);
        if (UmbraThemeIndex < 0 || UmbraThemeIndex >= 4 || (UmbraThemeIndex == 3 && !UmbraCustomThemeInitialized))
            UmbraThemeIndex = 0;
        DevUiEnabled = ReadUmbraProfileInt(L"Developer", L"Enabled", DevUiEnabled ? 1 : 0) != 0;
        UmbraDeveloperLogLevel = ReadUmbraProfileInt(L"Developer", L"LogLevel", UmbraDeveloperLogLevel);
        if (UmbraDeveloperLogLevel < 0 || UmbraDeveloperLogLevel > 4)
            UmbraDeveloperLogLevel = 1;
        UmbraAppearanceDirty = false;
    }

    void SaveUmbraAppearanceSettings()
    {
        if (!ResolveUmbraAppearanceSettingsPath())
            return;
        static const wchar_t* sections[] = {
            L"Appearance.AetherGlass",
            L"Appearance.DalamudDark",
            L"Appearance.AetherIvory",
            L"Appearance.Custom"
        };
        WriteUmbraProfileInt(L"Appearance", L"ActiveTheme", UmbraThemeIndex);
        WriteUmbraProfileInt(L"Appearance", L"CustomInitialized", UmbraCustomThemeInitialized ? 1 : 0);
        for (int index = 0; index < 4; index++)
        {
            const UmbraThemeTuning& tuning = UmbraThemeTunings[index];
            WriteUmbraProfileFloat(sections[index], L"WindowOpacity", tuning.windowOpacity);
            WriteUmbraProfileFloat(sections[index], L"FontSize", tuning.fontSize);
            WriteUmbraProfileFloat(sections[index], L"UiScale", tuning.uiScale);
            WriteUmbraProfileFloat(sections[index], L"Rounding", tuning.rounding);
            WriteUmbraProfileInt(sections[index], L"GradientEnabled", tuning.gradientEnabled ? 1 : 0);
            WriteUmbraProfileColor(sections[index], L"GradientStart", tuning.gradientStart);
            WriteUmbraProfileColor(sections[index], L"GradientMiddle", tuning.gradientMiddle);
            WriteUmbraProfileColor(sections[index], L"GradientEnd", tuning.gradientEnd);
        }
        const wchar_t* custom = L"Appearance.Custom";
        WriteUmbraProfileColor(custom, L"Window", CustomUmbraTheme.windowBg);
        WriteUmbraProfileColor(custom, L"Child", CustomUmbraTheme.childBg);
        WriteUmbraProfileColor(custom, L"Title", CustomUmbraTheme.titleBg);
        WriteUmbraProfileColor(custom, L"TitleActive", CustomUmbraTheme.titleBgActive);
        WriteUmbraProfileColor(custom, L"Border", CustomUmbraTheme.border);
        WriteUmbraProfileColor(custom, L"Accent", CustomUmbraTheme.accent);
        WriteUmbraProfileColor(custom, L"AccentHover", CustomUmbraTheme.accentHover);
        WriteUmbraProfileColor(custom, L"AccentActive", CustomUmbraTheme.accentActive);
        WriteUmbraProfileColor(custom, L"Button", CustomUmbraTheme.button);
        WriteUmbraProfileColor(custom, L"ButtonHover", CustomUmbraTheme.buttonHovered);
        WriteUmbraProfileColor(custom, L"ButtonActive", CustomUmbraTheme.buttonActive);
        WriteUmbraProfileColor(custom, L"Secondary", CustomUmbraTheme.frameBg);
        WriteUmbraProfileColor(custom, L"Text", CustomUmbraTheme.text);
        WriteUmbraProfileColor(custom, L"MutedText", CustomUmbraTheme.mutedText);
        WriteUmbraProfileInt(L"Developer", L"Enabled", DevUiEnabled ? 1 : 0);
        WriteUmbraProfileInt(L"Developer", L"LogLevel", UmbraDeveloperLogLevel);
        WritePrivateProfileStringW(nullptr, nullptr, nullptr, UmbraAppearanceSettingsPath);
        UmbraAppearanceDirty = false;
        AppendDx9LogLiteral(L"umbra_appearance_settings_saved=true");
    }

    ImU32 ColorU32(const ImVec4& color)
    {
        return ImGui::GetColorU32(color);
    }

    void ConfigureUmbraImGuiStyle()
    {
        const UmbraTheme& theme = GetUmbraTheme();
        const UmbraThemeTuning& tuning = GetUmbraThemeTuning();
        const float uiScale = tuning.uiScale;
        ImGui::StyleColorsDark();
        ImGuiStyle& style = ImGui::GetStyle();
        style.FontSizeBase = tuning.fontSize;
        style.WindowRounding = tuning.rounding * uiScale;
        style.ChildRounding = (tuning.rounding > 2.0f ? tuning.rounding - 2.0f : tuning.rounding) * uiScale;
        style.FrameRounding = (tuning.rounding > 4.0f ? tuning.rounding - 4.0f : tuning.rounding) * uiScale;
        style.PopupRounding = (tuning.rounding > 2.0f ? tuning.rounding - 2.0f : tuning.rounding) * uiScale;
        style.ScrollbarRounding = (tuning.rounding > 4.0f ? tuning.rounding - 4.0f : tuning.rounding) * uiScale;
        style.GrabRounding = (tuning.rounding > 4.0f ? tuning.rounding - 4.0f : tuning.rounding) * uiScale;
        style.TabRounding = (tuning.rounding > 4.0f ? tuning.rounding - 4.0f : tuning.rounding) * uiScale;
        style.WindowBorderSize = 1.0f;
        style.FrameBorderSize = 1.0f;
        style.PopupBorderSize = 1.0f;
        style.WindowPadding = ImVec2(18.0f * uiScale, 16.0f * uiScale);
        style.FramePadding = ImVec2(12.0f * uiScale, 9.0f * uiScale);
        style.ItemSpacing = ImVec2(12.0f * uiScale, 10.0f * uiScale);
        style.ItemInnerSpacing = ImVec2(9.0f * uiScale, 7.0f * uiScale);
        style.ScrollbarSize = 11.0f * uiScale;
        style.GrabMinSize = 12.0f * uiScale;
        style.AntiAliasedLines = true;
        style.AntiAliasedFill = true;

        ImVec4* colors = style.Colors;
        colors[ImGuiCol_WindowBg] = theme.windowBg;
        colors[ImGuiCol_WindowBg].w = ClampUmbraFloat(theme.windowBg.w * tuning.windowOpacity, 0.10f, 1.0f);
        colors[ImGuiCol_ChildBg] = theme.childBg;
        colors[ImGuiCol_ChildBg].w = ClampUmbraFloat(theme.childBg.w * tuning.windowOpacity, 0.08f, 1.0f);
        colors[ImGuiCol_PopupBg] = theme.popupBg;
        colors[ImGuiCol_PopupBg].w = ClampUmbraFloat(theme.popupBg.w * tuning.windowOpacity, 0.15f, 1.0f);
        colors[ImGuiCol_Border] = theme.border;
        colors[ImGuiCol_BorderShadow] = ImVec4(0.0f, 0.0f, 0.0f, 0.0f);
        colors[ImGuiCol_Text] = theme.text;
        colors[ImGuiCol_TextDisabled] = theme.mutedText;
        colors[ImGuiCol_TitleBg] = theme.titleBg;
        colors[ImGuiCol_TitleBgCollapsed] = theme.titleBg;
        colors[ImGuiCol_TitleBgActive] = theme.titleBgActive;
        colors[ImGuiCol_Button] = theme.button;
        colors[ImGuiCol_ButtonHovered] = theme.buttonHovered;
        colors[ImGuiCol_ButtonActive] = theme.buttonActive;
        colors[ImGuiCol_FrameBg] = theme.frameBg;
        colors[ImGuiCol_FrameBgHovered] = theme.frameHovered;
        colors[ImGuiCol_FrameBgActive] = theme.frameActive;
        colors[ImGuiCol_Header] = theme.frameBg;
        colors[ImGuiCol_HeaderHovered] = theme.frameHovered;
        colors[ImGuiCol_HeaderActive] = theme.frameActive;
        colors[ImGuiCol_CheckMark] = theme.accent;
        colors[ImGuiCol_SliderGrab] = theme.accent;
        colors[ImGuiCol_SliderGrabActive] = theme.accentActive;
        colors[ImGuiCol_Tab] = theme.tab;
        colors[ImGuiCol_TabHovered] = theme.tabHovered;
        colors[ImGuiCol_TabSelected] = theme.tabSelected;
        colors[ImGuiCol_TabSelectedOverline] = theme.accent;
        colors[ImGuiCol_Separator] = theme.border;
        colors[ImGuiCol_SeparatorHovered] = theme.accentHover;
        colors[ImGuiCol_SeparatorActive] = theme.accentActive;
        colors[ImGuiCol_ResizeGrip] = ImVec4(theme.accent.x, theme.accent.y, theme.accent.z, 0.22f);
        colors[ImGuiCol_ResizeGripHovered] = ImVec4(theme.accentHover.x, theme.accentHover.y, theme.accentHover.z, 0.50f);
        colors[ImGuiCol_ResizeGripActive] = ImVec4(theme.accentActive.x, theme.accentActive.y, theme.accentActive.z, 0.80f);
    }

    void DrawUmbraWindowGradient()
    {
        const UmbraThemeTuning& tuning = GetUmbraThemeTuning();
        if (!tuning.gradientEnabled)
            return;
        ImVec2 pos = ImGui::GetWindowPos();
        ImVec2 size = ImGui::GetWindowSize();
        if (size.x <= 2.0f || size.y <= 2.0f)
            return;
        ImVec2 middle(pos.x + size.x - 1.0f, pos.y + size.y * 0.48f);
        ImVec4 start = tuning.gradientStart;
        ImVec4 mid = tuning.gradientMiddle;
        ImVec4 end = tuning.gradientEnd;
        start.w *= tuning.windowOpacity;
        mid.w *= tuning.windowOpacity;
        end.w *= tuning.windowOpacity;
        ImDrawList* drawList = ImGui::GetWindowDrawList();
        drawList->AddRectFilledMultiColor(
            ImVec2(pos.x + 1.0f, pos.y + 1.0f),
            middle,
            ColorU32(start),
            ColorU32(mid),
            ColorU32(mid),
            ColorU32(start));
        drawList->AddRectFilledMultiColor(
            ImVec2(pos.x + 1.0f, middle.y),
            ImVec2(pos.x + size.x - 1.0f, pos.y + size.y - 1.0f),
            ColorU32(mid),
            ColorU32(end),
            ColorU32(end),
            ColorU32(mid));
    }

    void LoadUmbraUiFont()
    {
        ImGuiIO& io = ImGui::GetIO();

        wchar_t modulePath[BufferChars]{};
        wchar_t moduleDirectory[BufferChars]{};
        wchar_t assetPath[BufferChars]{};
        char assetPathUtf8[BufferChars * 3]{};
        if (UmbraModule != nullptr
            && GetModuleFileNameW(UmbraModule, modulePath, BufferChars) > 0)
        {
            ParentDirectory(modulePath, moduleDirectory, BufferChars);
            CombinePath(moduleDirectory, L"Assets\\fonts\\Inter-Regular.ttf", assetPath, BufferChars);
            AppendUtf8Wide(assetPathUtf8, sizeof(assetPathUtf8), assetPath);
        }

        ImFontConfig fontConfig{};
        fontConfig.SizePixels = 16.0f;
        fontConfig.PixelSnapH = false;
        fontConfig.Flags |= ImFontFlags_NoLoadError;
        UmbraUiFont = assetPathUtf8[0] == '\0'
            ? nullptr
            : io.Fonts->AddFontFromFileTTF(assetPathUtf8, 16.0f, &fontConfig);

        if (UmbraUiFont != nullptr)
        {
            io.FontDefault = UmbraUiFont;
            AppendDx9LogLiteral(L"umbra_ui_font=inter_regular");
            AppendDx9LogLiteral(L"umbra_ui_font_loaded=true");
            return;
        }

        ImFontConfig fallbackConfig{};
        fallbackConfig.SizePixels = 16.0f;
        fallbackConfig.PixelSnapH = false;
        UmbraUiFont = io.Fonts->AddFontDefaultVector(&fallbackConfig);
        io.FontDefault = UmbraUiFont;
        AppendDx9LogLiteral(L"umbra_ui_font=imgui_vector_fallback");
        AppendDx9LogLiteral(L"umbra_ui_font_loaded=false");
    }

    bool InitializeUmbraImGui(IDirect3DDevice9* device)
    {
        if (ImGuiInitialized)
            return true;
        if (device == nullptr)
            return false;

        IMGUI_CHECKVERSION();
        ImGui::CreateContext();
        ImGuiIO& io = ImGui::GetIO();
        io.IniFilename = nullptr;
        io.LogFilename = nullptr;
        io.ConfigFlags |= ImGuiConfigFlags_NoMouseCursorChange;
        LoadUmbraAppearanceSettings();
        LoadUmbraUiFont();
        ConfigureUmbraImGuiStyle();

        if (GameWindow == nullptr)
        {
            AppendDx9LogLiteral(L"umbra_imgui_init_failed=missing_hwnd");
            ImGui::DestroyContext();
            return false;
        }

        if (!ImGui_ImplWin32_Init(GameWindow))
        {
            AppendDx9LogLiteral(L"umbra_imgui_init_failed=win32_backend");
            ImGui::DestroyContext();
            return false;
        }

        HookUmbraWindowProc();

        if (!ImGui_ImplDX9_Init(device))
        {
            AppendDx9LogLiteral(L"umbra_imgui_init_failed=dx9_backend");
            ImGui_ImplWin32_Shutdown();
            ImGui::DestroyContext();
            return false;
        }

        ImGuiInitialized = true;
        if (InterlockedCompareExchange(&ImGuiInitializedLogged, 1, 0) == 0)
        {
            AppendDx9LogLiteral(L"umbra_imgui_backend=win32_dx9");
            AppendDx9LogLiteral(L"umbra_imgui_initialized=true");
        }

        return true;
    }

    bool IsImGuiMouseMessage(UINT message)
    {
        switch (message)
        {
            case WM_MOUSEMOVE:
            case WM_NCMOUSEMOVE:
            case WM_LBUTTONDOWN:
            case WM_LBUTTONUP:
            case WM_LBUTTONDBLCLK:
            case WM_RBUTTONDOWN:
            case WM_RBUTTONUP:
            case WM_RBUTTONDBLCLK:
            case WM_MBUTTONDOWN:
            case WM_MBUTTONUP:
            case WM_MBUTTONDBLCLK:
            case WM_XBUTTONDOWN:
            case WM_XBUTTONUP:
            case WM_XBUTTONDBLCLK:
            case WM_MOUSEWHEEL:
            case WM_MOUSEHWHEEL:
                return true;
            default:
                return false;
        }
    }

    bool IsImGuiKeyboardMessage(UINT message)
    {
        switch (message)
        {
            case WM_KEYDOWN:
            case WM_KEYUP:
            case WM_SYSKEYDOWN:
            case WM_SYSKEYUP:
            case WM_CHAR:
            case WM_SYSCHAR:
                return true;
            default:
                return false;
        }
    }

    LRESULT CALLBACK UmbraWindowProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam)
    {
        if (message == WM_NCDESTROY)
        {
            WNDPROC original = OriginalGameWndProc;
            if (GameWndProcHooked && original != nullptr)
            {
                SetWindowLongPtrW(hwnd, GWLP_WNDPROC, reinterpret_cast<LONG_PTR>(original));
                GameWndProcHooked = false;
                OriginalGameWndProc = nullptr;
                AppendDx9LogLiteral(L"umbra_imgui_wndproc_restored=true");
                return CallWindowProcW(original, hwnd, message, wParam, lParam);
            }

            return original != nullptr
                ? CallWindowProcW(original, hwnd, message, wParam, lParam)
                : DefWindowProcW(hwnd, message, wParam, lParam);
        }

        if (ImGuiInitialized && ImGui::GetCurrentContext() != nullptr)
        {
            LRESULT imguiResult = ImGui_ImplWin32_WndProcHandler(hwnd, message, wParam, lParam);
            ImGuiIO& io = ImGui::GetIO();
            if ((io.WantCaptureMouse && IsImGuiMouseMessage(message))
                || (io.WantCaptureKeyboard && IsImGuiKeyboardMessage(message)))
            {
                return imguiResult != 0 ? imguiResult : 1;
            }
        }

        if (OriginalGameWndProc != nullptr)
            return CallWindowProcW(OriginalGameWndProc, hwnd, message, wParam, lParam);

        return DefWindowProcW(hwnd, message, wParam, lParam);
    }

    bool HookUmbraWindowProc()
    {
        if (GameWndProcHooked)
            return true;
        if (GameWindow == nullptr)
            return false;

        SetLastError(0);
        LONG_PTR previous = SetWindowLongPtrW(
            GameWindow,
            GWLP_WNDPROC,
            reinterpret_cast<LONG_PTR>(&UmbraWindowProc));
        if (previous == 0 && GetLastError() != 0)
        {
            AppendDx9LogUInt(L"umbra_imgui_wndproc_hook_error", GetLastError());
            return false;
        }

        OriginalGameWndProc = reinterpret_cast<WNDPROC>(previous);
        GameWndProcHooked = true;
        if (InterlockedCompareExchange(&ImGuiWndProcHookLogged, 1, 0) == 0)
            AppendDx9LogLiteral(L"umbra_imgui_wndproc_hooked=true");
        return true;
    }

    void DrawUmbraSigilGlyph(ImDrawList* drawList, ImVec2 center, float radius, const UmbraTheme& theme, bool active)
    {
        ImU32 accent = ColorU32(active ? theme.accentHover : theme.accent);
        ImU32 muted = ColorU32(ImVec4(theme.text.x, theme.text.y, theme.text.z, 0.86f));
        ImU32 cutout = ColorU32(ImVec4(theme.windowBg.x, theme.windowBg.y, theme.windowBg.z, 0.95f));

        drawList->AddCircle(center, radius * 0.74f, accent, 40, 1.7f);
        drawList->AddCircleFilled(ImVec2(center.x - radius * 0.06f, center.y - radius * 0.02f), radius * 0.42f, accent, 32);
        drawList->AddCircleFilled(ImVec2(center.x + radius * 0.14f, center.y - radius * 0.08f), radius * 0.38f, cutout, 32);
        drawList->AddLine(ImVec2(center.x - radius * 0.54f, center.y + radius * 0.50f), ImVec2(center.x + radius * 0.48f, center.y - radius * 0.42f), muted, 1.5f);
        drawList->AddCircleFilled(ImVec2(center.x + radius * 0.48f, center.y - radius * 0.42f), radius * 0.10f, muted, 12);
    }

    void DrawUmbraSettingsGlyph(ImDrawList* drawList, ImVec2 center, const UmbraTheme& theme)
    {
        ImU32 color = ColorU32(theme.text);
        ImU32 accent = ColorU32(theme.accent);
        drawList->AddCircle(center, 7.2f, color, 24, 1.5f);
        drawList->AddCircleFilled(center, 2.4f, accent, 16);
        drawList->AddLine(ImVec2(center.x - 11.0f, center.y), ImVec2(center.x - 7.0f, center.y), color, 1.3f);
        drawList->AddLine(ImVec2(center.x + 7.0f, center.y), ImVec2(center.x + 11.0f, center.y), color, 1.3f);
        drawList->AddLine(ImVec2(center.x, center.y - 11.0f), ImVec2(center.x, center.y - 7.0f), color, 1.3f);
        drawList->AddLine(ImVec2(center.x, center.y + 7.0f), ImVec2(center.x, center.y + 11.0f), color, 1.3f);
        drawList->AddLine(ImVec2(center.x - 7.8f, center.y - 7.8f), ImVec2(center.x - 5.0f, center.y - 5.0f), color, 1.3f);
        drawList->AddLine(ImVec2(center.x + 5.0f, center.y + 5.0f), ImVec2(center.x + 7.8f, center.y + 7.8f), color, 1.3f);
        drawList->AddLine(ImVec2(center.x + 7.8f, center.y - 7.8f), ImVec2(center.x + 5.0f, center.y - 5.0f), color, 1.3f);
        drawList->AddLine(ImVec2(center.x - 5.0f, center.y + 5.0f), ImVec2(center.x - 7.8f, center.y + 7.8f), color, 1.3f);
    }

    void DrawUmbraPluginGlyph(ImDrawList* drawList, ImVec2 center, const UmbraTheme& theme)
    {
        ImU32 color = ColorU32(theme.text);
        ImU32 accent = ColorU32(theme.accent);
        ImVec2 plugMin(center.x - 8.0f, center.y - 5.0f);
        ImVec2 plugMax(center.x + 5.0f, center.y + 7.0f);
        drawList->AddRectFilled(plugMin, plugMax, ColorU32(ImVec4(theme.accent.x, theme.accent.y, theme.accent.z, 0.28f)), 3.0f);
        drawList->AddRect(plugMin, plugMax, color, 3.0f, 0, 1.3f);
        drawList->AddLine(ImVec2(center.x + 5.0f, center.y + 1.0f), ImVec2(center.x + 11.0f, center.y + 1.0f), color, 1.5f);
        drawList->AddLine(ImVec2(center.x + 11.0f, center.y + 1.0f), ImVec2(center.x + 11.0f, center.y - 7.0f), color, 1.5f);
        drawList->AddLine(ImVec2(center.x - 5.0f, center.y - 8.0f), ImVec2(center.x - 5.0f, center.y - 5.0f), accent, 1.6f);
        drawList->AddLine(ImVec2(center.x + 1.0f, center.y - 8.0f), ImVec2(center.x + 1.0f, center.y - 5.0f), accent, 1.6f);
    }

    void DrawUmbraThemeGlyph(ImDrawList* drawList, ImVec2 center, const UmbraTheme& theme)
    {
        drawList->AddCircleFilled(ImVec2(center.x - 5.5f, center.y - 4.0f), 4.2f, ColorU32(theme.accent), 16);
        drawList->AddCircleFilled(ImVec2(center.x + 5.5f, center.y - 4.0f), 4.2f, ColorU32(theme.warning), 16);
        drawList->AddCircleFilled(ImVec2(center.x, center.y + 6.0f), 4.2f, ColorU32(theme.mutedText), 16);
    }

    bool DrawUmbraDockButton(const char* id, int glyph, const char* tooltip, bool active, ImVec2 size)
    {
        const UmbraTheme& theme = GetUmbraTheme();
        bool pressed = ImGui::InvisibleButton(id, size);
        bool hovered = ImGui::IsItemHovered();
        ImVec2 min = ImGui::GetItemRectMin();
        ImVec2 max = ImGui::GetItemRectMax();
        ImVec2 center((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f);
        ImDrawList* drawList = ImGui::GetWindowDrawList();

        ImVec4 fill = active ? theme.buttonActive : (hovered ? theme.buttonHovered : theme.button);
        drawList->AddRectFilled(ImVec2(min.x + 2.0f, min.y + 3.0f), ImVec2(max.x + 2.0f, max.y + 3.0f), ColorU32(theme.shadow), 9.0f);
        drawList->AddRectFilled(min, max, ColorU32(fill), 9.0f);
        drawList->AddRect(min, max, ColorU32(active ? theme.accentHover : theme.border), 9.0f, 0, hovered ? 1.7f : 1.1f);

        if (glyph == 0)
            DrawUmbraSigilGlyph(drawList, center, size.x * 0.43f, theme, active || hovered);
        else if (glyph == 1)
            DrawUmbraSettingsGlyph(drawList, center, theme);
        else if (glyph == 2)
            DrawUmbraPluginGlyph(drawList, center, theme);
        else
            DrawUmbraSdkIcon(drawList, 16, center, size.x * 0.52f, ColorU32(theme.warning));

        if (hovered && tooltip != nullptr)
            ImGui::SetTooltip("%s", tooltip);
        return pressed;
    }

    void DrawUmbraImGuiDock()
    {
        DWORD now = GetTickCount();
        if (UmbraDockLastInteractionTicks == 0)
            UmbraDockLastInteractionTicks = now;

        const UmbraTheme& theme = GetUmbraTheme();
        const UmbraThemeTuning& tuning = GetUmbraThemeTuning();
        ImGuiWindowFlags flags =
            ImGuiWindowFlags_NoDecoration |
            ImGuiWindowFlags_NoMove |
            ImGuiWindowFlags_NoSavedSettings |
            ImGuiWindowFlags_NoFocusOnAppearing |
            ImGuiWindowFlags_NoNav |
            ImGuiWindowFlags_AlwaysAutoResize;

        ImGui::SetNextWindowPos(ImVec2(8.0f, UmbraDeveloperBarVisible && DevUiEnabled ? 40.0f : 8.0f), ImGuiCond_Always);
        ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(7.0f, 7.0f));
        ImGui::PushStyleVar(ImGuiStyleVar_ItemSpacing, ImVec2(7.0f, 0.0f));
        ImGui::PushStyleColor(ImGuiCol_WindowBg, ImVec4(theme.windowBg.x, theme.windowBg.y, theme.windowBg.z, (UmbraDockExpanded ? 0.68f : 0.36f) * tuning.windowOpacity));
        ImGui::PushStyleColor(ImGuiCol_Border, theme.border);

        bool dockHovered = false;
        if (ImGui::Begin("##UmbraDock", nullptr, flags))
        {
            dockHovered = ImGui::IsWindowHovered(ImGuiHoveredFlags_AllowWhenBlockedByActiveItem);
            if (DrawUmbraDockButton("##UmbraRoot", 0, "Umbra", SettingsWindowOpen || PluginInstallerOpen || UmbraDeveloperBarVisible, ImVec2(39.0f, 39.0f)))
            {
                UmbraDockExpanded = !UmbraDockExpanded;
                UmbraDockLastInteractionTicks = now;
            }

            if (UmbraDockExpanded)
            {
                ImGui::SameLine();
                if (DrawUmbraDockButton("##UmbraPluginsButton", 2, "Plugin Manager", PluginInstallerOpen, ImVec2(34.0f, 34.0f)))
                {
                    PluginInstallerOpen = !PluginInstallerOpen;
                    if (PluginInstallerOpen)
                        SettingsWindowOpen = false;
                    UmbraDockLastInteractionTicks = now;
                }
                ImGui::SameLine();
                if (DrawUmbraDockButton("##UmbraSettingsButton", 1, "Umbra Settings", SettingsWindowOpen, ImVec2(34.0f, 34.0f)))
                {
                    SettingsWindowOpen = !SettingsWindowOpen;
                    if (SettingsWindowOpen)
                        PluginInstallerOpen = false;
                    UmbraDockLastInteractionTicks = now;
                }
                if (DevUiEnabled)
                {
                    ImGui::SameLine();
                    if (DrawUmbraDockButton("##UmbraDeveloperButton", 3, "Developer Menu", UmbraDeveloperBarVisible, ImVec2(34.0f, 34.0f)))
                    {
                        UmbraDeveloperBarVisible = !UmbraDeveloperBarVisible;
                        UmbraDockLastInteractionTicks = now;
                    }
                }
            }
        }
        ImGui::End();

        ImGui::PopStyleColor(2);
        ImGui::PopStyleVar(2);

        if (dockHovered || SettingsWindowOpen || PluginInstallerOpen || UmbraDeveloperBarVisible)
            UmbraDockLastInteractionTicks = now;
        if (UmbraDockExpanded
            && !SettingsWindowOpen
            && !PluginInstallerOpen
            && !UmbraDeveloperBarVisible
            && now - UmbraDockLastInteractionTicks > UmbraDockCollapseMs)
        {
            UmbraDockExpanded = false;
        }
    }

    void RefreshUmbraDeveloperLog(bool force)
    {
        DWORD now = GetTickCount();
        if (!force && now - UmbraDeveloperLogRefreshTicks < 500)
            return;
        UmbraDeveloperLogRefreshTicks = now;
        wchar_t logPath[BufferChars]{};
        if (!GetUmbraEnvironmentValue(L"LOG", logPath, BufferChars))
        {
            CopyString(logPath, BufferChars, L"Z:\\private\\tmp\\umbra-bootstrap-fallback.log");
        }
        HANDLE file = CreateFileW(logPath, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
        if (file == INVALID_HANDLE_VALUE)
        {
            lstrcpyA(UmbraDeveloperLogBuffer, "Umbra log is not available yet.");
            return;
        }
        LARGE_INTEGER size{};
        if (!GetFileSizeEx(file, &size))
        {
            CloseHandle(file);
            return;
        }
        const DWORD capacity = static_cast<DWORD>(sizeof(UmbraDeveloperLogBuffer) - 1);
        DWORD bytesToRead = size.QuadPart > capacity ? capacity : static_cast<DWORD>(size.QuadPart);
        LARGE_INTEGER offset{};
        offset.QuadPart = size.QuadPart - bytesToRead;
        SetFilePointerEx(file, offset, nullptr, FILE_BEGIN);
        DWORD bytesRead = 0;
        ReadFile(file, UmbraDeveloperLogBuffer, bytesToRead, &bytesRead, nullptr);
        CloseHandle(file);
        UmbraDeveloperLogBuffer[bytesRead] = '\0';
        if (offset.QuadPart > 0)
        {
            DWORD start = 0;
            while (start < bytesRead && UmbraDeveloperLogBuffer[start] != '\n')
                start++;
            if (start < bytesRead)
            {
                start++;
                MoveMemory(UmbraDeveloperLogBuffer, UmbraDeveloperLogBuffer + start, bytesRead - start + 1);
            }
        }
    }

    void DrawUmbraDeveloperBar(const D3DVIEWPORT9& viewport)
    {
        if (!DevUiEnabled || !UmbraDeveloperBarVisible)
            return;
        if (InterlockedCompareExchange(&UmbraDeveloperBarRenderedLogged, 1, 0) == 0)
            AppendDx9LogLiteral(L"umbra_developer_bar_rendered=true");
        const UmbraTheme& theme = GetUmbraTheme();
        ImGuiWindowFlags flags =
            ImGuiWindowFlags_NoDecoration |
            ImGuiWindowFlags_NoMove |
            ImGuiWindowFlags_NoSavedSettings |
            ImGuiWindowFlags_NoNav |
            ImGuiWindowFlags_MenuBar;
        ImGui::SetNextWindowPos(ImVec2(0.0f, 0.0f), ImGuiCond_Always);
        ImGui::SetNextWindowSize(ImVec2(static_cast<float>(viewport.Width), 32.0f), ImGuiCond_Always);
        ImGui::PushStyleVar(ImGuiStyleVar_WindowRounding, 0.0f);
        ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(8.0f, 3.0f));
        ImGui::PushStyleColor(ImGuiCol_WindowBg, ImVec4(theme.titleBg.x, theme.titleBg.y, theme.titleBg.z, 0.97f));
        if (ImGui::Begin("##UmbraDeveloperBar", nullptr, flags) && ImGui::BeginMenuBar())
        {
            if (ImGui::BeginMenu("Umbra"))
            {
                ImGui::MenuItem("Developer Log", nullptr, &UmbraDeveloperLogOpen);
                if (ImGui::BeginMenu("Debug Level"))
                {
                    const char* levels[] = { "Trace", "Debug", "Information", "Warning", "Error" };
                    for (int index = 0; index < 5; index++)
                    {
                        if (ImGui::MenuItem(levels[index], nullptr, UmbraDeveloperLogLevel == index))
                        {
                            UmbraDeveloperLogLevel = index;
                            UmbraAppearanceDirty = true;
                        }
                    }
                    ImGui::EndMenu();
                }
                ImGui::MenuItem("DX9 Metrics", nullptr, &UmbraDeveloperMetricsVisible);
                if (ImGui::MenuItem("Refresh Log Now"))
                    RefreshUmbraDeveloperLog(true);
                ImGui::Separator();
                if (ImGui::MenuItem("Open Umbra Settings"))
                {
                    SettingsWindowOpen = true;
                    UmbraSettingsSection = 2;
                    PluginInstallerOpen = false;
                }
                if (ImGui::MenuItem("Save Developer Preferences"))
                    SaveUmbraAppearanceSettings();
                ImGui::Separator();
                if (ImGui::MenuItem("Close Developer Menu"))
                    UmbraDeveloperBarVisible = false;
                ImGui::EndMenu();
            }
            ImGui::TextColored(theme.mutedText, "Umbra API 2.0");
            ImGui::SameLine();
            ImGui::TextColored(ManagedRenderBridge ? theme.accent : theme.warning, "%s", ManagedRenderBridge ? "managed host ready" : "native host / managed waiting");
            if (UmbraDeveloperMetricsVisible)
            {
                ImGui::SameLine();
                ImGui::TextColored(theme.mutedText, "Present %ld  Reset %ld  Viewport %lux%lu",
                    InterlockedCompareExchange(&SwapChainPresentFrameCount, 0, 0),
                    InterlockedCompareExchange(&ResetCount, 0, 0),
                    static_cast<unsigned long>(viewport.Width),
                    static_cast<unsigned long>(viewport.Height));
            }
            ImGui::EndMenuBar();
        }
        ImGui::End();
        ImGui::PopStyleColor();
        ImGui::PopStyleVar(2);
    }

    void DrawUmbraDeveloperLogWindow()
    {
        if (!DevUiEnabled || !UmbraDeveloperLogOpen)
            return;
        RefreshUmbraDeveloperLog(false);
        ImGuiIO& io = ImGui::GetIO();
        float maximumWidth = io.DisplaySize.x - 40.0f;
        float maximumHeight = io.DisplaySize.y - 80.0f;
        if (maximumWidth < 320.0f) maximumWidth = 320.0f;
        if (maximumHeight < 240.0f) maximumHeight = 240.0f;
        float minimumWidth = maximumWidth < 620.0f ? maximumWidth : 620.0f;
        float minimumHeight = maximumHeight < 360.0f ? maximumHeight : 360.0f;
        ImGui::SetNextWindowSize(ImVec2(maximumWidth > 980.0f ? 980.0f : maximumWidth, maximumHeight > 580.0f ? 580.0f : maximumHeight), ImGuiCond_FirstUseEver);
        ImGui::SetNextWindowSizeConstraints(ImVec2(minimumWidth, minimumHeight), ImVec2(maximumWidth, maximumHeight));
        if (!ImGui::Begin("Umbra Developer Log", &UmbraDeveloperLogOpen, ImGuiWindowFlags_NoSavedSettings))
        {
            ImGui::End();
            return;
        }
        const UmbraTheme& theme = GetUmbraTheme();
        DrawUmbraWindowGradient();
        DrawUmbraWindowAccent(theme);
        const char* levels[] = { "Trace", "Debug", "Information", "Warning", "Error" };
        ImGui::TextColored(theme.accent, "Live log");
        ImGui::SameLine();
        ImGui::TextColored(theme.mutedText, "level: %s · refresh: 500 ms", levels[UmbraDeveloperLogLevel]);
        ImGui::SameLine();
        if (ImGui::Button("Refresh"))
            RefreshUmbraDeveloperLog(true);
        ImGui::Separator();
        if (ImGui::BeginChild("##UmbraDeveloperLogScroll", ImVec2(0.0f, 0.0f), ImGuiChildFlags_Borders, ImGuiWindowFlags_HorizontalScrollbar))
        {
            ImGui::PushStyleColor(ImGuiCol_Text, theme.mutedText);
            ImGui::TextUnformatted(UmbraDeveloperLogBuffer);
            ImGui::PopStyleColor();
        }
        ImGui::EndChild();
        ImGui::End();
    }

    void DrawUmbraWindowAccent(const UmbraTheme& theme)
    {
        ImVec2 pos = ImGui::GetWindowPos();
        ImVec2 size = ImGui::GetWindowSize();
        ImDrawList* drawList = ImGui::GetWindowDrawList();
        drawList->AddRect(ImVec2(pos.x + 0.5f, pos.y + 0.5f), ImVec2(pos.x + size.x - 0.5f, pos.y + size.y - 0.5f), ColorU32(theme.border), 9.0f, 0, 1.0f);
    }

    bool DrawUmbraSettingsNavigation(const char* id, const char* label, int icon, bool selected)
    {
        const UmbraTheme& theme = GetUmbraTheme();
        float width = ImGui::GetContentRegionAvail().x;
        bool pressed = ImGui::InvisibleButton(id, ImVec2(width, 42.0f));
        bool hovered = ImGui::IsItemHovered();
        ImVec2 min = ImGui::GetItemRectMin();
        ImVec2 max = ImGui::GetItemRectMax();
        ImDrawList* drawList = ImGui::GetWindowDrawList();
        if (selected || hovered)
        {
            drawList->AddRectFilled(min, max, ColorU32(selected
                ? ImVec4(theme.accent.x, theme.accent.y, theme.accent.z, 0.16f)
                : ImVec4(theme.frameHovered.x, theme.frameHovered.y, theme.frameHovered.z, 0.58f)), 8.0f);
            drawList->AddRect(min, max, ColorU32(selected ? theme.accent : theme.border), 8.0f, 0, selected ? 1.3f : 1.0f);
        }
        DrawUmbraSdkIcon(drawList, icon, ImVec2(min.x + 20.0f, min.y + 21.0f), 18.0f, ColorU32(selected ? theme.accentHover : theme.mutedText));
        drawList->AddText(ImVec2(min.x + 42.0f, min.y + 12.0f), ColorU32(selected ? theme.text : theme.mutedText), label);
        return pressed;
    }

    void DrawUmbraAppearancePreview()
    {
        const UmbraTheme& theme = GetUmbraTheme();
        UmbraThemeTuning& tuning = GetUmbraThemeTuning();
        ImGui::Dummy(ImVec2(ImGui::GetContentRegionAvail().x, 92.0f));
        ImVec2 min = ImGui::GetItemRectMin();
        ImVec2 max = ImGui::GetItemRectMax();
        ImVec2 middle(max.x, min.y + (max.y - min.y) * 0.5f);
        ImDrawList* drawList = ImGui::GetWindowDrawList();
        if (tuning.gradientEnabled)
        {
            drawList->AddRectFilledMultiColor(min, middle, ColorU32(tuning.gradientStart), ColorU32(tuning.gradientMiddle), ColorU32(tuning.gradientMiddle), ColorU32(tuning.gradientStart));
            drawList->AddRectFilledMultiColor(ImVec2(min.x, middle.y), max, ColorU32(tuning.gradientMiddle), ColorU32(tuning.gradientEnd), ColorU32(tuning.gradientEnd), ColorU32(tuning.gradientMiddle));
        }
        else
        {
            drawList->AddRectFilled(min, max, ColorU32(theme.windowBg), tuning.rounding);
        }
        drawList->AddRect(min, max, ColorU32(theme.border), tuning.rounding, 0, 1.4f);
        drawList->AddRectFilled(ImVec2(min.x + 18.0f, min.y + 18.0f), ImVec2(min.x + 148.0f, min.y + 57.0f), ColorU32(theme.button), 8.0f);
        drawList->AddRect(ImVec2(min.x + 18.0f, min.y + 18.0f), ImVec2(min.x + 148.0f, min.y + 57.0f), ColorU32(theme.accent), 8.0f);
        drawList->AddText(ImVec2(min.x + 43.0f, min.y + 29.0f), ColorU32(theme.text), "Theme preview");
        drawList->AddRectFilled(ImVec2(min.x + 164.0f, min.y + 18.0f), ImVec2(max.x - 18.0f, min.y + 72.0f), ColorU32(theme.childBg), 8.0f);
        drawList->AddText(ImVec2(min.x + 180.0f, min.y + 29.0f), ColorU32(theme.text), GetUmbraThemeNames()[UmbraThemeIndex]);
        drawList->AddText(ImVec2(min.x + 180.0f, min.y + 50.0f), ColorU32(theme.mutedText), "Buttons, panels, text and transparency");
    }

    bool DrawUmbraColorEditor(const char* label, ImVec4* color, bool alpha = true)
    {
        ImGuiColorEditFlags flags = ImGuiColorEditFlags_DisplayRGB | ImGuiColorEditFlags_InputRGB;
        if (alpha)
            flags |= ImGuiColorEditFlags_AlphaBar | ImGuiColorEditFlags_AlphaPreviewHalf;
        else
            flags |= ImGuiColorEditFlags_NoAlpha;
        return ImGui::ColorEdit4(label, &color->x, flags);
    }

    void DrawUmbraAppearanceSettings()
    {
        const UmbraTheme& headerTheme = GetUmbraTheme();
        ImGui::PushFont(nullptr, 24.0f);
        ImGui::TextUnformatted("Appearance");
        ImGui::PopFont();
        ImGui::TextColored(headerTheme.mutedText, "Tune each preset or build one reusable Custom profile.");
        ImGui::Spacing();

        int selectedTheme = UmbraThemeIndex;
        ImGui::SetNextItemWidth(280.0f);
        if (ImGui::Combo("Theme profile", &selectedTheme, GetUmbraThemeNames(), GetUmbraThemeCount()))
        {
            if (selectedTheme == 3 && !UmbraCustomThemeInitialized)
                CloneCurrentUmbraThemeToCustom();
            UmbraThemeIndex = selectedTheme;
            UmbraAppearanceDirty = true;
            ConfigureUmbraImGuiStyle();
        }
        const UmbraTheme& theme = GetUmbraTheme();
        ImGui::SameLine();
        ImGui::TextColored(theme.mutedText, "%s", UmbraThemeIndex == 3 ? "editable palette" : "preset palette + profile tuning");
        DrawUmbraAppearancePreview();
        ImGui::Spacing();

        UmbraThemeTuning& tuning = GetUmbraThemeTuning();
        bool changed = false;
        float opacityPercent = tuning.windowOpacity * 100.0f;
        if (ImGui::SliderFloat("Window opacity", &opacityPercent, 25.0f, 100.0f, "%.0f%%", ImGuiSliderFlags_AlwaysClamp))
        {
            tuning.windowOpacity = opacityPercent / 100.0f;
            changed = true;
        }
        changed |= ImGui::SliderFloat("Font size", &tuning.fontSize, 13.0f, 22.0f, "%.1f px", ImGuiSliderFlags_AlwaysClamp);
        changed |= ImGui::SliderFloat("Interface scale", &tuning.uiScale, 0.75f, 1.40f, "%.2fx", ImGuiSliderFlags_AlwaysClamp);
        changed |= ImGui::SliderFloat("Corner rounding", &tuning.rounding, 0.0f, 20.0f, "%.0f px", ImGuiSliderFlags_AlwaysClamp);
        changed |= ImGui::Checkbox("Blend a multi-color window gradient", &tuning.gradientEnabled);
        if (tuning.gradientEnabled)
        {
            ImGui::Indent();
            changed |= DrawUmbraColorEditor("Gradient start", &tuning.gradientStart);
            changed |= DrawUmbraColorEditor("Gradient middle", &tuning.gradientMiddle);
            changed |= DrawUmbraColorEditor("Gradient end", &tuning.gradientEnd);
            ImGui::Unindent();
        }

        if (UmbraThemeIndex == 3)
        {
            ImGui::Separator();
            ImGui::PushFont(nullptr, 19.0f);
            ImGui::TextUnformatted("Custom palette");
            ImGui::PopFont();
            ImGui::TextColored(theme.mutedText, "Custom is a single saved profile. Preset colors remain unchanged.");
            static int customBase = 0;
            const char* presetNames[] = { "Aether Glass", "Dalamud Dark", "Aether Ivory" };
            ImGui::SetNextItemWidth(210.0f);
            ImGui::Combo("Base preset", &customBase, presetNames, 3);
            ImGui::SameLine();
            if (ImGui::Button("Copy preset into Custom"))
            {
                int previous = UmbraThemeIndex;
                UmbraThemeIndex = customBase;
                CloneCurrentUmbraThemeToCustom();
                UmbraThemeIndex = previous;
                changed = true;
            }
            changed |= DrawUmbraColorEditor("Primary window", &CustomUmbraTheme.windowBg);
            changed |= DrawUmbraColorEditor("Panel background", &CustomUmbraTheme.childBg);
            changed |= DrawUmbraColorEditor("Window title", &CustomUmbraTheme.titleBg);
            changed |= DrawUmbraColorEditor("Active title", &CustomUmbraTheme.titleBgActive);
            changed |= DrawUmbraColorEditor("Toggle / selection accent", &CustomUmbraTheme.accent);
            changed |= DrawUmbraColorEditor("Accent hover", &CustomUmbraTheme.accentHover);
            changed |= DrawUmbraColorEditor("Button", &CustomUmbraTheme.button);
            changed |= DrawUmbraColorEditor("Button hover", &CustomUmbraTheme.buttonHovered);
            changed |= DrawUmbraColorEditor("Button active", &CustomUmbraTheme.buttonActive);
            changed |= DrawUmbraColorEditor("Secondary controls", &CustomUmbraTheme.frameBg);
            changed |= DrawUmbraColorEditor("Border", &CustomUmbraTheme.border);
            changed |= DrawUmbraColorEditor("Primary text", &CustomUmbraTheme.text);
            changed |= DrawUmbraColorEditor("Secondary text", &CustomUmbraTheme.mutedText);
        }

        if (changed)
        {
            UmbraAppearanceDirty = true;
            ConfigureUmbraImGuiStyle();
        }
        ImGui::Spacing();
        if (ImGui::Button(UmbraAppearanceDirty ? "Save appearance profile" : "Appearance profile saved"))
            SaveUmbraAppearanceSettings();
        ImGui::SameLine();
        ImGui::TextColored(theme.mutedText, "Stored in Umbra/umbra-ui.ini");
    }

    void DrawUmbraGeneralSettings()
    {
        const UmbraTheme& theme = GetUmbraTheme();
        ImGui::PushFont(nullptr, 24.0f);
        ImGui::TextUnformatted("General");
        ImGui::PopFont();
        ImGui::TextColored(theme.mutedText, "Framework behavior and plugin-manager preferences.");
        ImGui::Spacing();
        ImGui::Checkbox("Debug logging", &DebugLoggingEnabled);
        if (ImGui::Checkbox("Enable developer options", &DevUiEnabled))
        {
            if (!DevUiEnabled)
            {
                UmbraDeveloperBarVisible = false;
                UmbraDeveloperLogOpen = false;
            }
            UmbraAppearanceDirty = true;
        }
        ImGui::Checkbox("Show framework readiness notifications", &ShowPluginExecutionWarning);
        ImGui::Separator();
        ImGui::TextColored(theme.accent, "Plugin manager");
        ImGui::TextWrapped("The library uses responsive columns and enforces minimum and maximum window sizes. Interface scale is controlled per appearance profile.");
    }

    void DrawUmbraDeveloperSettings()
    {
        const UmbraTheme& theme = GetUmbraTheme();
        ImGui::PushFont(nullptr, 24.0f);
        ImGui::TextUnformatted("Developer");
        ImGui::PopFont();
        ImGui::TextColored(theme.mutedText, "Live framework diagnostics and plugin-development tools.");
        ImGui::Spacing();
        if (ImGui::Checkbox("Show Developer Menu in the Umbra dock", &DevUiEnabled))
        {
            UmbraAppearanceDirty = true;
            if (!DevUiEnabled)
            {
                UmbraDeveloperBarVisible = false;
                UmbraDeveloperLogOpen = false;
            }
        }
        const char* levels[] = { "Trace", "Debug", "Information", "Warning", "Error" };
        if (ImGui::Combo("Default log level", &UmbraDeveloperLogLevel, levels, 5))
            UmbraAppearanceDirty = true;
        RefreshDevBridgeControlState(false);
        bool devBridge = DevBridgeEnabled;
        if (ImGui::Checkbox("Enable read-only Umbra Dev Bridge", &devBridge))
            WriteDevBridgeControlState(devBridge);
        ImGui::SameLine();
        ImGui::TextColored(devBridge ? theme.accent : theme.mutedText, "%s", devBridge ? "localhost access requested" : "off");
        ImGui::Separator();
        ImGui::TextWrapped("When enabled, the dock exposes Developer Menu. It opens a top-screen menu bar with live log, severity selection, DX9 metrics, startup diagnostics and plugin-host state.");
        if (UmbraAppearanceDirty && ImGui::Button("Save developer preferences"))
            SaveUmbraAppearanceSettings();
    }

    void DrawUmbraDiagnosticsSettings()
    {
        const UmbraTheme& theme = GetUmbraTheme();
        ImGui::PushFont(nullptr, 24.0f);
        ImGui::TextUnformatted("Diagnostics");
        ImGui::PopFont();
        ImGui::TextColored(theme.mutedText, "Current native rendering and managed-host state.");
        ImGui::Spacing();
        if (ImGui::BeginChild("##UmbraRuntimeStatus", ImVec2(0.0f, 260.0f), ImGuiChildFlags_Borders | ImGuiChildFlags_AlwaysUseWindowPadding))
        {
            ImGui::TextUnformatted("DX9 hook"); ImGui::SameLine(210.0f); ImGui::TextColored(theme.accent, "ready");
            ImGui::TextUnformatted("Render callback"); ImGui::SameLine(210.0f); ImGui::TextColored(theme.accent, "SwapChain Present");
            ImGui::TextUnformatted("ImGui backend"); ImGui::SameLine(210.0f); ImGui::TextColored(theme.accent, "Win32 + DX9");
            ImGui::TextUnformatted("Managed plugin host"); ImGui::SameLine(210.0f); ImGui::TextColored(ManagedRenderBridge ? theme.accent : theme.warning, "%s", ManagedRenderBridge ? "active" : "waiting");
            ImGui::TextUnformatted("Developer bridge"); ImGui::SameLine(210.0f); ImGui::TextColored(DevBridgeEnabled ? theme.accent : theme.mutedText, "%s", DevBridgeEnabled ? "requested" : "off");
            ImGui::TextUnformatted("Rendered frames"); ImGui::SameLine(210.0f); ImGui::Text("%ld", InterlockedCompareExchange(&ManagedFrameNumber, 0, 0));
            ImGui::TextUnformatted("Device resets"); ImGui::SameLine(210.0f); ImGui::Text("%ld", InterlockedCompareExchange(&ResetCount, 0, 0));
        }
        ImGui::EndChild();
        if (DevUiEnabled && ImGui::Button("Open Developer Menu"))
            UmbraDeveloperBarVisible = true;
    }

    void DrawUmbraImGuiSettingsWindow()
    {
        if (InterlockedCompareExchange(&UmbraSettingsWindowRenderedLogged, 1, 0) == 0)
            AppendDx9LogLiteral(L"umbra_settings_window_rendered=true");
        ImGuiIO& io = ImGui::GetIO();
        const UmbraThemeTuning& tuning = GetUmbraThemeTuning();
        float desiredWidth = 820.0f * tuning.uiScale;
        float desiredHeight = 650.0f * tuning.uiScale;
        float maximumWidth = io.DisplaySize.x - 36.0f;
        float maximumHeight = io.DisplaySize.y - 48.0f;
        if (maximumWidth < 320.0f) maximumWidth = 320.0f;
        if (maximumHeight < 280.0f) maximumHeight = 280.0f;
        float minimumWidth = 680.0f * tuning.uiScale;
        float minimumHeight = 500.0f * tuning.uiScale;
        if (minimumWidth > maximumWidth) minimumWidth = maximumWidth;
        if (minimumHeight > maximumHeight) minimumHeight = maximumHeight;
        if (desiredWidth > maximumWidth) desiredWidth = maximumWidth;
        if (desiredHeight > maximumHeight) desiredHeight = maximumHeight;
        if (desiredWidth < minimumWidth) desiredWidth = minimumWidth;
        if (desiredHeight < minimumHeight) desiredHeight = minimumHeight;
        ImGui::SetNextWindowPos(ImVec2(18.0f, 58.0f), ImGuiCond_FirstUseEver);
        ImGui::SetNextWindowSize(ImVec2(desiredWidth, desiredHeight), ImGuiCond_FirstUseEver);
        ImGui::SetNextWindowSizeConstraints(ImVec2(minimumWidth, minimumHeight), ImVec2(maximumWidth, maximumHeight));
        if (!ImGui::Begin("Umbra Settings", &SettingsWindowOpen, ImGuiWindowFlags_NoSavedSettings))
        {
            ImGui::End();
            return;
        }

        const UmbraTheme& theme = GetUmbraTheme();
        DrawUmbraWindowGradient();
        DrawUmbraWindowAccent(theme);
        ImGui::PushStyleColor(ImGuiCol_ChildBg, ImVec4(theme.titleBg.x, theme.titleBg.y, theme.titleBg.z, 0.86f * tuning.windowOpacity));
        if (ImGui::BeginChild("##UmbraSettingsSidebar", ImVec2(178.0f, 0.0f), ImGuiChildFlags_Borders | ImGuiChildFlags_AlwaysUseWindowPadding))
        {
            ImGui::PushFont(nullptr, 21.0f);
            ImGui::TextUnformatted("Settings");
            ImGui::PopFont();
            ImGui::TextColored(theme.mutedText, "Umbra API 2.0");
            ImGui::Spacing();
            if (DrawUmbraSettingsNavigation("##SettingsGeneral", "General", 6, UmbraSettingsSection == 0)) UmbraSettingsSection = 0;
            if (DrawUmbraSettingsNavigation("##SettingsAppearance", "Appearance", 13, UmbraSettingsSection == 1)) UmbraSettingsSection = 1;
            if (DrawUmbraSettingsNavigation("##SettingsDeveloper", "Developer", 16, UmbraSettingsSection == 2)) UmbraSettingsSection = 2;
            if (DrawUmbraSettingsNavigation("##SettingsDiagnostics", "Diagnostics", 7, UmbraSettingsSection == 3)) UmbraSettingsSection = 3;
        }
        ImGui::EndChild();
        ImGui::PopStyleColor();
        ImGui::SameLine(0.0f, 12.0f);
        if (ImGui::BeginChild("##UmbraSettingsContent", ImVec2(0.0f, 0.0f), ImGuiChildFlags_Borders | ImGuiChildFlags_AlwaysUseWindowPadding))
        {
            if (UmbraSettingsSection == 1)
                DrawUmbraAppearanceSettings();
            else if (UmbraSettingsSection == 2)
                DrawUmbraDeveloperSettings();
            else if (UmbraSettingsSection == 3)
                DrawUmbraDiagnosticsSettings();
            else
                DrawUmbraGeneralSettings();
        }
        ImGui::EndChild();
        ImGui::End();
    }

    bool DrawUmbraLibraryButton(
        const char* id,
        const char* label,
        int icon,
        float width,
        bool primary,
        bool enabled = true)
    {
        const UmbraTheme& theme = GetUmbraTheme();
        const float height = 38.0f;
        bool pressed = ImGui::InvisibleButton(id, ImVec2(width, height)) && enabled;
        bool hovered = ImGui::IsItemHovered() && enabled;
        ImVec2 min = ImGui::GetItemRectMin();
        ImVec2 max = ImGui::GetItemRectMax();
        ImDrawList* drawList = ImGui::GetWindowDrawList();
        ImVec4 fill = enabled
            ? (primary ? (hovered ? theme.accentHover : theme.accentActive) : (hovered ? theme.buttonHovered : theme.button))
            : ImVec4(theme.frameBg.x, theme.frameBg.y, theme.frameBg.z, 0.48f);
        ImVec4 border = enabled
            ? (primary ? theme.accentHover : (hovered ? theme.accent : theme.border))
            : ImVec4(theme.border.x, theme.border.y, theme.border.z, 0.42f);
        ImVec4 foreground = enabled ? theme.text : theme.mutedText;
        drawList->AddRectFilled(ImVec2(min.x + 2.0f, min.y + 3.0f), ImVec2(max.x + 2.0f, max.y + 3.0f), ColorU32(theme.shadow), 8.0f);
        drawList->AddRectFilled(min, max, ColorU32(fill), 8.0f);
        drawList->AddRect(min, max, ColorU32(border), 8.0f, 0, hovered ? 1.6f : 1.0f);
        if (primary && enabled)
            drawList->AddLine(ImVec2(min.x + 8.0f, min.y + 1.0f), ImVec2(max.x - 8.0f, min.y + 1.0f), ColorU32(ImVec4(1, 1, 1, 0.22f)), 1.0f);

        ImVec2 textSize = ImGui::CalcTextSize(label);
        float iconSize = icon > 0 ? 17.0f : 0.0f;
        float contentWidth = textSize.x + (iconSize > 0.0f ? iconSize + 8.0f : 0.0f);
        float x = min.x + (width - contentWidth) * 0.5f;
        if (iconSize > 0.0f)
        {
            DrawUmbraSdkIcon(drawList, icon, ImVec2(x + iconSize * 0.5f, min.y + height * 0.5f), iconSize, ColorU32(foreground));
            x += iconSize + 8.0f;
        }
        drawList->AddText(ImVec2(x, min.y + (height - textSize.y) * 0.5f), ColorU32(foreground), label);
        return pressed;
    }

    void DrawUmbraLibraryBadge(const char* label, const ImVec4& color, int icon = 0)
    {
        const float height = 24.0f;
        const float iconSize = icon > 0 ? 12.0f : 0.0f;
        ImVec2 textSize = ImGui::CalcTextSize(label);
        float width = textSize.x + 16.0f + (iconSize > 0.0f ? iconSize + 5.0f : 0.0f);
        ImGui::Dummy(ImVec2(width, height));
        ImVec2 min = ImGui::GetItemRectMin();
        ImVec2 max = ImGui::GetItemRectMax();
        ImDrawList* drawList = ImGui::GetWindowDrawList();
        drawList->AddRectFilled(min, max, ColorU32(ImVec4(color.x, color.y, color.z, 0.14f)), 6.0f);
        drawList->AddRect(min, max, ColorU32(ImVec4(color.x, color.y, color.z, 0.64f)), 6.0f, 0, 1.0f);
        float x = min.x + 8.0f;
        if (iconSize > 0.0f)
        {
            DrawUmbraSdkIcon(drawList, icon, ImVec2(x + iconSize * 0.5f, min.y + height * 0.5f), iconSize, ColorU32(color));
            x += iconSize + 5.0f;
        }
        drawList->AddText(ImVec2(x, min.y + (height - textSize.y) * 0.5f), ColorU32(color), label);
    }

    void DrawUmbraLibraryArtwork(int icon, const ImVec4& accent, float size)
    {
        const UmbraTheme& theme = GetUmbraTheme();
        ImGui::Dummy(ImVec2(size, size));
        ImVec2 min = ImGui::GetItemRectMin();
        ImVec2 max = ImGui::GetItemRectMax();
        ImDrawList* drawList = ImGui::GetWindowDrawList();
        drawList->AddRectFilled(ImVec2(min.x + 3.0f, min.y + 4.0f), ImVec2(max.x + 3.0f, max.y + 4.0f), ColorU32(theme.shadow), 11.0f);
        drawList->AddRectFilled(min, max, ColorU32(ImVec4(accent.x * 0.32f, accent.y * 0.32f, accent.z * 0.40f, 0.98f)), 11.0f);
        drawList->AddRectFilled(ImVec2(min.x + 5.0f, min.y + 5.0f), ImVec2(max.x - 5.0f, max.y - 5.0f), ColorU32(ImVec4(accent.x, accent.y, accent.z, 0.25f)), 8.0f);
        drawList->AddRect(min, max, ColorU32(ImVec4(accent.x, accent.y, accent.z, 0.92f)), 11.0f, 0, 1.5f);
        DrawUmbraSdkIcon(drawList, icon, ImVec2((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f), size * 0.48f, ColorU32(ImVec4(0.96f, 0.94f, 1.0f, 1.0f)));
    }

    bool DrawUmbraLibraryNav(const char* id, const char* label, int icon, bool active)
    {
        const UmbraTheme& theme = GetUmbraTheme();
        float width = ImGui::GetContentRegionAvail().x;
        bool pressed = ImGui::InvisibleButton(id, ImVec2(width, 42.0f));
        bool hovered = ImGui::IsItemHovered();
        ImVec2 min = ImGui::GetItemRectMin();
        ImVec2 max = ImGui::GetItemRectMax();
        ImDrawList* drawList = ImGui::GetWindowDrawList();
        if (active || hovered)
        {
            ImVec4 fill = active ? ImVec4(theme.accent.x, theme.accent.y, theme.accent.z, 0.16f) : ImVec4(theme.buttonHovered.x, theme.buttonHovered.y, theme.buttonHovered.z, 0.64f);
            drawList->AddRectFilled(min, max, ColorU32(fill), 8.0f);
            drawList->AddRect(min, max, ColorU32(active ? theme.accent : theme.border), 8.0f, 0, active ? 1.3f : 1.0f);
        }
        DrawUmbraSdkIcon(drawList, icon, ImVec2(min.x + 20.0f, min.y + 21.0f), 18.0f, ColorU32(active ? theme.accentHover : theme.mutedText));
        drawList->AddText(ImVec2(min.x + 42.0f, min.y + 12.0f), ColorU32(active ? theme.text : theme.mutedText), label);
        return pressed;
    }

    bool DrawUmbraLibraryTopTab(const char* id, const char* label, int icon, bool active, float width)
    {
        const UmbraTheme& theme = GetUmbraTheme();
        bool pressed = ImGui::InvisibleButton(id, ImVec2(width, 48.0f));
        bool hovered = ImGui::IsItemHovered();
        ImVec2 min = ImGui::GetItemRectMin();
        ImVec2 max = ImGui::GetItemRectMax();
        ImDrawList* drawList = ImGui::GetWindowDrawList();
        if (active || hovered)
            drawList->AddRectFilled(min, max, ColorU32(ImVec4(theme.accent.x, theme.accent.y, theme.accent.z, active ? 0.14f : 0.07f)), 8.0f);
        if (active)
            drawList->AddRectFilled(ImVec2(min.x, max.y - 2.0f), ImVec2(max.x, max.y), ColorU32(theme.accent), 2.0f);
        DrawUmbraSdkIcon(drawList, icon, ImVec2(min.x + 22.0f, min.y + 23.0f), 17.0f, ColorU32(active ? theme.accentHover : theme.mutedText));
        ImVec2 textSize = ImGui::CalcTextSize(label);
        drawList->AddText(ImVec2(min.x + 40.0f, min.y + (48.0f - textSize.y) * 0.5f), ColorU32(active ? theme.text : theme.mutedText), label);
        return pressed;
    }

    void DrawUmbraLibraryToggle(const char* id, const char* label, bool* value)
    {
        const UmbraTheme& theme = GetUmbraTheme();
        bool pressed = ImGui::InvisibleButton(id, ImVec2(ImGui::GetContentRegionAvail().x, 34.0f));
        if (pressed)
            *value = !*value;
        ImVec2 min = ImGui::GetItemRectMin();
        ImDrawList* drawList = ImGui::GetWindowDrawList();
        drawList->AddText(ImVec2(min.x, min.y + 8.0f), ColorU32(theme.text), label);
        float trackX = min.x + ImGui::GetItemRectSize().x - 46.0f;
        ImVec4 track = *value ? theme.accentActive : theme.frameBg;
        drawList->AddRectFilled(ImVec2(trackX, min.y + 5.0f), ImVec2(trackX + 44.0f, min.y + 29.0f), ColorU32(track), 12.0f);
        drawList->AddRect(ImVec2(trackX, min.y + 5.0f), ImVec2(trackX + 44.0f, min.y + 29.0f), ColorU32(*value ? theme.accentHover : theme.border), 12.0f);
        float knobX = *value ? trackX + 32.0f : trackX + 12.0f;
        drawList->AddCircleFilled(ImVec2(knobX, min.y + 17.0f), 8.0f, ColorU32(ImVec4(0.96f, 0.96f, 1.0f, 1.0f)), 24);
    }

    void DrawUmbraLibraryCard(
        int cardIndex,
        const char* name,
        const char* author,
        const char* description,
        const char* version,
        int icon,
        const ImVec4& artworkColor,
        bool installed)
    {
        const UmbraTheme& theme = GetUmbraTheme();
        bool selected = UmbraLibrarySelectedCard == cardIndex;
        char childId[48]{};
        wsprintfA(childId, "##UmbraLibraryCard%d", cardIndex);
        ImGui::PushStyleColor(ImGuiCol_ChildBg, selected ? ImVec4(theme.buttonActive.x, theme.buttonActive.y, theme.buttonActive.z, 0.42f) : theme.childBg);
        ImGui::PushStyleColor(ImGuiCol_Border, selected ? theme.accent : theme.border);
        ImGui::PushStyleVar(ImGuiStyleVar_ChildRounding, 10.0f);
        if (ImGui::BeginChild(childId, ImVec2(0.0f, 164.0f), ImGuiChildFlags_Borders | ImGuiChildFlags_AlwaysUseWindowPadding))
        {
            DrawUmbraLibraryArtwork(icon, artworkColor, 96.0f);
            ImGui::SameLine(0.0f, 16.0f);
            ImGui::BeginGroup();
            ImGui::PushFont(nullptr, 21.0f);
            ImGui::TextUnformatted(name);
            ImGui::PopFont();
            ImGui::TextColored(theme.mutedText, "by %s", author);
            ImGui::PushTextWrapPos(ImGui::GetCursorPosX() + ImGui::GetContentRegionAvail().x - 12.0f);
            ImGui::TextColored(theme.mutedText, "%s", description);
            ImGui::PopTextWrapPos();
            DrawUmbraLibraryBadge(installed ? "Verified" : "SDK Preview", installed ? ImVec4(0.42f, 0.92f, 0.28f, 1.0f) : theme.accent, installed ? 9 : 15);
            ImGui::SameLine();
            DrawUmbraLibraryBadge("API 2.0", theme.accent, 15);
            ImGui::SameLine();
            DrawUmbraLibraryBadge(version, theme.mutedText);
            ImGui::EndGroup();

            ImGui::SetCursorPos(ImVec2(ImGui::GetWindowWidth() - 142.0f, ImGui::GetWindowHeight() - 53.0f));
            char buttonId[48]{};
            wsprintfA(buttonId, "##UmbraCardDetails%d", cardIndex);
            if (DrawUmbraLibraryButton(buttonId, selected ? "Selected" : "Details", selected ? 15 : 7, 122.0f, selected))
                UmbraLibrarySelectedCard = cardIndex;
        }
        ImGui::EndChild();
        ImGui::PopStyleVar();
        ImGui::PopStyleColor(2);
    }

    void DrawUmbraLibraryEmptyState(const char* title, const char* message, int icon)
    {
        const UmbraTheme& theme = GetUmbraTheme();
        ImGui::PushStyleColor(ImGuiCol_ChildBg, theme.childBg);
        ImGui::PushStyleColor(ImGuiCol_Border, theme.border);
        if (ImGui::BeginChild("##UmbraLibraryEmpty", ImVec2(0.0f, 250.0f), ImGuiChildFlags_Borders | ImGuiChildFlags_AlwaysUseWindowPadding))
        {
            float center = ImGui::GetWindowWidth() * 0.5f;
            ImGui::SetCursorPosX(center - 30.0f);
            DrawUmbraLibraryArtwork(icon, theme.accent, 60.0f);
            ImGui::PushFont(nullptr, 21.0f);
            ImVec2 titleSize = ImGui::CalcTextSize(title);
            ImGui::SetCursorPosX(center - titleSize.x * 0.5f);
            ImGui::TextUnformatted(title);
            ImGui::PopFont();
            ImVec2 messageSize = ImGui::CalcTextSize(message);
            ImGui::SetCursorPosX(center - messageSize.x * 0.5f);
            ImGui::TextColored(theme.mutedText, "%s", message);
            ImGui::SetCursorPosX(center - 64.0f);
            DrawUmbraLibraryButton("##UmbraEmptyRefresh", "Refresh", 18, 128.0f, true, false);
        }
        ImGui::EndChild();
        ImGui::PopStyleColor(2);
    }

    void DrawUmbraLibraryDetailsPanel(float width, float height)
    {
        const UmbraTheme& theme = GetUmbraTheme();
        const bool sdk = UmbraLibrarySelectedCard == 0;
        const bool manager = UmbraLibrarySelectedCard == 1;
        const char* title = sdk ? "Umbra Plugin SDK" : manager ? "Plugin Manager" : "Sample Plugin Template";
        const char* description = sdk
            ? "API 2.0 contracts, lifecycle services and graphical UI components for Umbra plugins."
            : manager
                ? "The native library shell remains available while the managed runtime initializes."
                : "A starter plugin demonstrating manifests, lifecycle callbacks and styled components.";
        int icon = sdk ? 12 : manager ? 2 : 3;
        ImVec4 art = sdk ? theme.accent : manager ? ImVec4(0.18f, 0.68f, 0.82f, 1.0f) : ImVec4(0.78f, 0.52f, 0.16f, 1.0f);

        ImGui::PushStyleColor(ImGuiCol_ChildBg, theme.childBg);
        ImGui::PushStyleColor(ImGuiCol_Border, theme.border);
        ImGui::PushStyleVar(ImGuiStyleVar_ChildRounding, 10.0f);
        if (ImGui::BeginChild("##UmbraLibraryDetails", ImVec2(width, height), ImGuiChildFlags_Borders | ImGuiChildFlags_AlwaysUseWindowPadding))
        {
            DrawUmbraLibraryArtwork(icon, art, 76.0f);
            ImGui::SameLine(0.0f, 14.0f);
            ImGui::BeginGroup();
            ImGui::PushFont(nullptr, 22.0f);
            ImGui::TextWrapped("%s", title);
            ImGui::PopFont();
            ImGui::TextColored(theme.mutedText, "by AetherXIV");
            DrawUmbraLibraryBadge("Verified", ImVec4(0.42f, 0.92f, 0.28f, 1.0f), 9);
            ImGui::EndGroup();
            ImGui::Spacing();
            DrawUmbraLibraryBadge("API 2.0", theme.accent, 15);
            ImGui::SameLine();
            DrawUmbraLibraryBadge("No IPC", theme.mutedText);
            ImGui::Separator();
            ImGui::TextWrapped("%s", description);
            ImGui::Spacing();

            ImGui::TextColored(theme.mutedText, "Version");
            ImGui::SameLine(ImGui::GetWindowWidth() - 74.0f);
            ImGui::TextUnformatted(sdk ? "2.0" : "Built-in");
            ImGui::TextColored(theme.mutedText, "Updated");
            ImGui::SameLine(ImGui::GetWindowWidth() - 92.0f);
            ImGui::TextUnformatted("Bundled");
            ImGui::TextColored(theme.mutedText, "Category");
            ImGui::SameLine(ImGui::GetWindowWidth() - 132.0f);
            ImGui::TextUnformatted(sdk ? "Developer Tools" : "Framework");
            ImGui::TextColored(theme.mutedText, "Author");
            ImGui::SameLine(ImGui::GetWindowWidth() - 92.0f);
            ImGui::TextUnformatted("AetherXIV");
            ImGui::Separator();
            ImGui::PushFont(nullptr, 18.0f);
            ImGui::TextUnformatted("Foundation status");
            ImGui::PopFont();
            ImGui::BulletText("Graphical component library available");
            ImGui::BulletText("DX9 render backend active");
            ImGui::BulletText("Plugin API version 2.0");
            ImGui::BulletText("Managed Wine host isolation pending");

            ImGui::SetCursorPosY(ImGui::GetWindowHeight() - 60.0f);
            DrawUmbraLibraryButton("##UmbraIncludedButton", sdk ? "Included with Umbra" : "Framework component", 9, ImGui::GetContentRegionAvail().x, true, false);
        }
        ImGui::EndChild();
        ImGui::PopStyleVar();
        ImGui::PopStyleColor(2);
    }

    void DrawUmbraLibrarySettingsContent()
    {
        const UmbraTheme& theme = GetUmbraTheme();
        ImGui::PushFont(nullptr, 24.0f);
        ImGui::TextUnformatted("Plugin Settings");
        ImGui::PopFont();
        ImGui::TextColored(theme.mutedText, "Configure the framework UI and development features.");
        ImGui::Spacing();
        ImGui::PushStyleColor(ImGuiCol_ChildBg, theme.childBg);
        if (ImGui::BeginChild("##UmbraSettingsCard", ImVec2(0.0f, 230.0f), ImGuiChildFlags_Borders | ImGuiChildFlags_AlwaysUseWindowPadding))
        {
            ImGui::PushFont(nullptr, 19.0f);
            ImGui::TextUnformatted("Interface & development");
            ImGui::PopFont();
            ImGui::Separator();
            DrawUmbraLibraryToggle("##LibraryDebug", "Debug logging", &DebugLoggingEnabled);
            DrawUmbraLibraryToggle("##LibraryDevUi", "Developer UI", &DevUiEnabled);
            RefreshDevBridgeControlState(false);
            bool devBridge = DevBridgeEnabled;
            DrawUmbraLibraryToggle("##LibraryDevBridge", "Umbra Dev Bridge", &devBridge);
            if (devBridge != DevBridgeEnabled)
                WriteDevBridgeControlState(devBridge);
            ImGui::Separator();
            ImGui::SetNextItemWidth(-1.0f);
            if (ImGui::Combo("##LibraryTheme", &UmbraThemeIndex, GetUmbraThemeNames(), GetUmbraThemeCount()))
                ConfigureUmbraImGuiStyle();
        }
        ImGui::EndChild();
        ImGui::PopStyleColor();
    }

    void DrawUmbraLibraryRepositoriesContent()
    {
        const UmbraTheme& theme = GetUmbraTheme();
        ImGui::PushFont(nullptr, 24.0f);
        ImGui::TextUnformatted("Repositories");
        ImGui::PopFont();
        ImGui::TextColored(theme.mutedText, "Manage supported and custom plugin catalog sources.");
        ImGui::Spacing();
        ImGui::PushStyleColor(ImGuiCol_ChildBg, theme.childBg);
        if (ImGui::BeginChild("##UmbraRepositoryCard", ImVec2(0.0f, 176.0f), ImGuiChildFlags_Borders | ImGuiChildFlags_AlwaysUseWindowPadding))
        {
            DrawUmbraLibraryArtwork(5, theme.accent, 72.0f);
            ImGui::SameLine(0.0f, 16.0f);
            ImGui::BeginGroup();
            ImGui::PushFont(nullptr, 19.0f);
            ImGui::TextUnformatted("AetherXIV Supported Repository");
            ImGui::PopFont();
            ImGui::TextColored(theme.mutedText, "http://127.0.0.1:8080/launcher/umbra/plugin-catalog");
            DrawUmbraLibraryBadge("Configured", ImVec4(0.42f, 0.92f, 0.28f, 1.0f), 15);
            ImGui::SameLine();
            DrawUmbraLibraryBadge("Awaiting managed catalog service", theme.warning, 16);
            ImGui::EndGroup();
            ImGui::SetCursorPos(ImVec2(ImGui::GetWindowWidth() - 154.0f, ImGui::GetWindowHeight() - 54.0f));
            DrawUmbraLibraryButton("##RepositoryRefresh", "Refresh", 18, 134.0f, true, false);
        }
        ImGui::EndChild();
        ImGui::PopStyleColor();
    }

    void DrawUmbraImGuiPluginInstallerWindow()
    {
        ImGuiIO& io = ImGui::GetIO();
        const UmbraThemeTuning& tuning = GetUmbraThemeTuning();
        float maximumWidth = io.DisplaySize.x - 36.0f;
        float maximumHeight = io.DisplaySize.y - (UmbraDeveloperBarVisible ? 68.0f : 42.0f);
        if (maximumWidth < 360.0f) maximumWidth = 360.0f;
        if (maximumHeight < 300.0f) maximumHeight = 300.0f;
        float minimumWidth = 900.0f * tuning.uiScale;
        float minimumHeight = 590.0f * tuning.uiScale;
        if (minimumWidth > maximumWidth) minimumWidth = maximumWidth;
        if (minimumHeight > maximumHeight) minimumHeight = maximumHeight;
        float width = 1460.0f * tuning.uiScale;
        float height = 840.0f * tuning.uiScale;
        if (width > maximumWidth) width = maximumWidth;
        if (height > maximumHeight) height = maximumHeight;
        if (width < minimumWidth) width = minimumWidth;
        if (height < minimumHeight) height = minimumHeight;
        ImGui::SetNextWindowPos(ImVec2((io.DisplaySize.x - width) * 0.5f, (io.DisplaySize.y - height) * 0.5f), ImGuiCond_FirstUseEver);
        ImGui::SetNextWindowSize(ImVec2(width, height), ImGuiCond_FirstUseEver);
        ImGui::SetNextWindowSizeConstraints(ImVec2(minimumWidth, minimumHeight), ImVec2(maximumWidth, maximumHeight));
        ImGuiWindowFlags flags = ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoCollapse | ImGuiWindowFlags_NoSavedSettings;
        if (!ImGui::Begin("Umbra Plugin Library###UmbraNativePluginLibrary", &PluginInstallerOpen, flags))
        {
            ImGui::End();
            return;
        }

        const UmbraTheme& theme = GetUmbraTheme();
        DrawUmbraWindowGradient();
        DrawUmbraWindowAccent(theme);
        if (InterlockedCompareExchange(&NativeLibraryRenderedLogged, 1, 0) == 0)
            AppendDx9LogLiteral(L"umbra_native_plugin_library_concept_rendered=true");
        ImGui::PushStyleVar(ImGuiStyleVar_ChildRounding, 9.0f);
        ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(16.0f, 14.0f));

        ImGui::PushStyleColor(ImGuiCol_ChildBg, ImVec4(theme.titleBg.x, theme.titleBg.y, theme.titleBg.z, 0.94f));
        if (ImGui::BeginChild("##UmbraLibrarySidebar", ImVec2(220.0f, 0.0f), ImGuiChildFlags_Borders | ImGuiChildFlags_AlwaysUseWindowPadding))
        {
            ImGui::Dummy(ImVec2(46.0f, 54.0f));
            ImVec2 logoMin = ImGui::GetItemRectMin();
            DrawUmbraSigilGlyph(ImGui::GetWindowDrawList(), ImVec2(logoMin.x + 23.0f, logoMin.y + 25.0f), 22.0f, theme, true);
            ImGui::SameLine(0.0f, 12.0f);
            ImGui::BeginGroup();
            ImGui::PushFont(nullptr, 25.0f);
            ImGui::TextUnformatted("Umbra");
            ImGui::PopFont();
            ImGui::TextColored(theme.mutedText, "Plugin Library");
            ImGui::EndGroup();
            ImGui::Spacing();
            if (DrawUmbraLibraryNav("##SideBrowse", "Browse", 2, UmbraLibrarySection == 0)) UmbraLibrarySection = 0;
            if (DrawUmbraLibraryNav("##SideCategories", "Categories", 11, false)) UmbraLibrarySection = 0;
            if (DrawUmbraLibraryNav("##SideCollections", "Collections", 9, false)) UmbraLibrarySection = 0;
            ImGui::Spacing();
            ImGui::Separator();
            ImGui::Spacing();
            if (DrawUmbraLibraryNav("##SideSettings", "Settings", 6, UmbraLibrarySection == 4)) UmbraLibrarySection = 4;
            if (DrawUmbraLibraryNav("##SideAbout", "About", 7, UmbraLibrarySection == 5)) UmbraLibrarySection = 5;

            ImGui::SetCursorPosY(ImGui::GetWindowHeight() - 154.0f);
            ImGui::PushStyleColor(ImGuiCol_ChildBg, ImVec4(theme.frameBg.x, theme.frameBg.y, theme.frameBg.z, 0.58f));
            if (ImGui::BeginChild("##UmbraVerifiedCard", ImVec2(0.0f, 132.0f), ImGuiChildFlags_Borders | ImGuiChildFlags_AlwaysUseWindowPadding))
            {
                DrawUmbraLibraryBadge("Verified", ImVec4(0.42f, 0.92f, 0.28f, 1.0f), 9);
                ImGui::TextUnformatted("Umbra Framework");
                ImGui::TextColored(theme.mutedText, "API 2.0");
                ImGui::TextColored(theme.accent, "Graphical SDK active");
            }
            ImGui::EndChild();
            ImGui::PopStyleColor();
        }
        ImGui::EndChild();
        ImGui::PopStyleColor();

        ImGui::SameLine(0.0f, 12.0f);
        if (ImGui::BeginChild("##UmbraLibraryMain", ImVec2(0.0f, 0.0f), ImGuiChildFlags_None))
        {
            if (DrawUmbraLibraryTopTab("##TopDiscover", "Discover", 2, UmbraLibrarySection == 0, 130.0f)) UmbraLibrarySection = 0;
            ImGui::SameLine(0.0f, 4.0f);
            if (DrawUmbraLibraryTopTab("##TopInstalled", "Installed", 3, UmbraLibrarySection == 1, 130.0f)) UmbraLibrarySection = 1;
            ImGui::SameLine(0.0f, 4.0f);
            if (DrawUmbraLibraryTopTab("##TopUpdates", "Updates", 4, UmbraLibrarySection == 2, 124.0f)) UmbraLibrarySection = 2;
            ImGui::SameLine(0.0f, 4.0f);
            if (DrawUmbraLibraryTopTab("##TopRepos", "Repositories", 5, UmbraLibrarySection == 3, 160.0f)) UmbraLibrarySection = 3;
            ImGui::SameLine();
            ImGui::SetCursorPosX(ImGui::GetWindowWidth() - 48.0f);
            if (DrawUmbraLibraryButton("##UmbraLibraryClose", "", 17, 38.0f, false))
                PluginInstallerOpen = false;
            ImGui::Separator();

            if (UmbraLibrarySection <= 2)
            {
                static char search[192]{};
                static int category = 0;
                static int author = 0;
                static int sort = 0;
                const char* categories[] = { "All Categories", "Developer Tools", "User Interface" };
                const char* authors[] = { "All Authors", "AetherXIV" };
                const char* sorts[] = { "Featured", "Name", "Recently Updated" };
                float toolbarWidth = ImGui::GetContentRegionAvail().x;
                bool compactToolbar = toolbarWidth < 940.0f;
                ImGui::SetNextItemWidth(compactToolbar ? toolbarWidth : 280.0f);
                ImGui::InputTextWithHint("##UmbraLibrarySearch", "Search plugins", search, sizeof(search));
                if (compactToolbar)
                    ImGui::Spacing();
                else
                    ImGui::SameLine();
                ImGui::SetNextItemWidth(compactToolbar ? 154.0f : 166.0f);
                ImGui::Combo("##UmbraCategory", &category, categories, 3);
                ImGui::SameLine();
                ImGui::SetNextItemWidth(150.0f);
                ImGui::Combo("##UmbraAuthor", &author, authors, 2);
                ImGui::SameLine();
                ImGui::SetNextItemWidth(150.0f);
                ImGui::Combo("##UmbraSort", &sort, sorts, 3);
                ImGui::SameLine();
                if (DrawUmbraLibraryButton("##UmbraGrid", "", 13, 38.0f, UmbraLibraryGridView)) UmbraLibraryGridView = true;
                ImGui::SameLine(0.0f, 4.0f);
                if (DrawUmbraLibraryButton("##UmbraList", "", 14, 38.0f, !UmbraLibraryGridView)) UmbraLibraryGridView = false;
                ImGui::Spacing();

                ImVec2 availableContent = ImGui::GetContentRegionAvail();
                const float paneGap = 12.0f;
                float usableWidth = availableContent.x - paneGap;
                if (usableWidth < 2.0f)
                    usableWidth = 2.0f;
                float detailWidth = availableContent.x * 0.36f;
                if (detailWidth < 280.0f)
                    detailWidth = 280.0f;
                else if (detailWidth > 360.0f)
                    detailWidth = 360.0f;
                if (detailWidth >= usableWidth)
                {
                    detailWidth = usableWidth * 0.42f;
                    if (detailWidth < 1.0f)
                        detailWidth = 1.0f;
                }
                float listWidth = usableWidth - detailWidth;
                if (listWidth < 1.0f)
                    listWidth = 1.0f;
                float listHeight = 0.0f;
                if (ImGui::BeginChild("##UmbraLibraryList", ImVec2(listWidth, listHeight), ImGuiChildFlags_None))
                {
                    ImGui::PushFont(nullptr, 24.0f);
                    ImGui::TextUnformatted(UmbraLibrarySection == 0 ? "Framework Components" : UmbraLibrarySection == 1 ? "Installed Plugins" : "Plugin Updates");
                    ImGui::PopFont();
                    ImGui::TextColored(theme.mutedText, "%s", UmbraLibrarySection == 0
                        ? "The graphical foundation available before repository metadata loads."
                        : UmbraLibrarySection == 1
                            ? "Manage built-in and third-party Umbra components."
                            : "Installed versions are compared against configured repositories.");
                    ImGui::Spacing();
                    if (UmbraLibrarySection == 2)
                    {
                        DrawUmbraLibraryEmptyState("No updates available", "Repository comparison will resume with the managed catalog service.", 4);
                    }
                    else
                    {
                        DrawUmbraLibraryCard(0, "Umbra Plugin SDK", "AetherXIV", "API 2.0 contracts and a reusable graphical component toolkit.", "2.0", 12, theme.accent, true);
                        ImGui::Spacing();
                        DrawUmbraLibraryCard(1, "Plugin Manager", "AetherXIV", "Native plugin library shell, repository browser and lifecycle controls.", "Built-in", 2, ImVec4(0.18f, 0.68f, 0.82f, 1.0f), true);
                        ImGui::Spacing();
                        if (UmbraLibrarySection == 0)
                            DrawUmbraLibraryCard(2, "Sample Plugin Template", "AetherXIV", "Starter manifest, lifecycle hooks and styled SDK component examples.", "SDK Sample", 3, ImVec4(0.78f, 0.52f, 0.16f, 1.0f), false);
                    }
                }
                ImGui::EndChild();
                ImGui::SameLine(0.0f, paneGap);
                DrawUmbraLibraryDetailsPanel(detailWidth, listHeight);
            }
            else if (UmbraLibrarySection == 3)
            {
                DrawUmbraLibraryRepositoriesContent();
            }
            else if (UmbraLibrarySection == 4)
            {
                DrawUmbraLibrarySettingsContent();
            }
            else
            {
                ImGui::PushFont(nullptr, 24.0f);
                ImGui::TextUnformatted("About Umbra");
                ImGui::PopFont();
                ImGui::TextColored(theme.mutedText, "A plugin framework and SDK for the FINAL FANTASY XIV 1.23b client.");
                ImGui::Spacing();
                DrawUmbraLibraryBadge("Umbra API 2.0", theme.accent, 1);
                ImGui::SameLine();
                DrawUmbraLibraryBadge("DX9 Ready", ImVec4(0.42f, 0.92f, 0.28f, 1.0f), 15);
                ImGui::Separator();
                ImGui::TextWrapped("This native graphical shell stays responsive independently of plugin runtime startup. Third-party plugin execution and repository installation will connect when the managed Wine host is isolated from the game render process.");
            }
        }
        ImGui::EndChild();

        ImGui::PopStyleVar(2);
        ImGui::End();
    }

    void DrawUmbraImGuiToast(const char* name, const char* message, const ImVec4& accent, float x, float y)
    {
        const UmbraTheme& theme = GetUmbraTheme();
        ImGuiWindowFlags flags =
            ImGuiWindowFlags_NoDecoration |
            ImGuiWindowFlags_NoMove |
            ImGuiWindowFlags_NoSavedSettings |
            ImGuiWindowFlags_NoFocusOnAppearing |
            ImGuiWindowFlags_NoNav;

        ImGui::SetNextWindowPos(ImVec2(x, y), ImGuiCond_Always);
        ImGui::SetNextWindowSize(ImVec2(340.0f, 42.0f), ImGuiCond_Always);
        ImGui::SetNextWindowBgAlpha(theme.toastBg.w);
        ImGui::PushStyleColor(ImGuiCol_WindowBg, theme.toastBg);
        ImGui::PushStyleColor(ImGuiCol_Border, accent);
        ImGui::PushStyleVar(ImGuiStyleVar_WindowRounding, 8.0f);
        ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(12.0f, 10.0f));
        if (ImGui::Begin(name, nullptr, flags))
        {
            ImDrawList* drawList = ImGui::GetWindowDrawList();
            ImVec2 pos = ImGui::GetWindowPos();
            ImVec2 size = ImGui::GetWindowSize();
            drawList->AddRectFilled(ImVec2(pos.x + 6.0f, pos.y + 9.0f), ImVec2(pos.x + 9.0f, pos.y + size.y - 9.0f), ColorU32(accent), 2.0f);
            ImGui::Indent(9.0f);
            ImGui::TextColored(accent, "%s", message);
            ImGui::Unindent(9.0f);
        }
        ImGui::End();
        ImGui::PopStyleVar(2);
        ImGui::PopStyleColor();
        ImGui::PopStyleColor();
    }

    void DrawUmbraImGuiToasts(const D3DVIEWPORT9& viewport)
    {
        if (OverlayStartTicks == 0)
            OverlayStartTicks = GetTickCount();

        DWORD elapsed = GetTickCount() - OverlayStartTicks;
        if (elapsed > ToastVisibleMs)
            return;

        float width = static_cast<float>(viewport.Width);
        float height = static_cast<float>(viewport.Height);
        float x = width - 358.0f;
        float y = height - 158.0f;
        const UmbraTheme& theme = GetUmbraTheme();
        DrawUmbraImGuiToast("##UmbraToastReady", "Umbra framework ready", theme.accent, x, y);
        DrawUmbraImGuiToast("##UmbraToastNative", "Native DX9 UI active", ImVec4(0.30f, 0.95f, 0.55f, 1.0f), x, y + 50.0f);
        if (ShowPluginExecutionWarning)
            DrawUmbraImGuiToast("##UmbraToastPlugins", "Plugin execution disabled", theme.warning, x, y + 100.0f);
    }

    int NotifyManagedRenderEvent(UmbraRenderEventKind kind, const D3DVIEWPORT9* viewport)
    {
        umbra_render_bridge_fn callback = ManagedRenderBridge;
        if (callback == nullptr)
            return 1;

        UmbraRenderEventV1 renderEvent{};
        renderEvent.size = sizeof(renderEvent);
        renderEvent.abiVersion = UmbraRenderBridgeAbiVersion;
        renderEvent.kind = static_cast<DWORD>(kind);

        if (kind == UmbraRenderFrame)
        {
            DWORD now = GetTickCount();
            DWORD elapsed = ManagedLastFrameTicks == 0 ? 0 : now - ManagedLastFrameTicks;
            ManagedLastFrameTicks = now;
            if (elapsed > 250)
                elapsed = 250;

            renderEvent.frameNumber = static_cast<DWORD>(InterlockedIncrement(&ManagedFrameNumber));
            renderEvent.deltaSeconds = static_cast<float>(elapsed) / 1000.0f;
            if (viewport != nullptr)
            {
                renderEvent.viewportWidth = viewport->Width;
            renderEvent.viewportHeight = viewport->Height;
            renderEvent.reserved = PluginInstallerOpen ? 1u : 0u;
            }

            ManagedRenderThreadId = GetCurrentThreadId();
            ManagedUiWindowDepth = 0;
            ManagedUiChildDepth = 0;
            InterlockedExchange(&ManagedUiCallbackActive, 1);
        }

        int result = callback(&renderEvent, sizeof(renderEvent));

        if (kind == UmbraRenderFrame)
        {
            while (ManagedUiChildDepth > 0)
            {
                ImGui::EndChild();
                ManagedUiChildDepth--;
            }
            while (ManagedUiWindowDepth > 0)
            {
                ImGui::End();
                ManagedUiWindowDepth--;
            }
            InterlockedExchange(&ManagedUiCallbackActive, 0);
        }

        if (result == 0 && InterlockedCompareExchange(&ManagedRenderBridgeReadyLogged, 1, 0) == 0)
        {
            AppendDx9LogLiteral(L"umbra_managed_render_bridge_ready=true");
            AppendDx9LogUInt(L"umbra_managed_render_bridge_abi", UmbraRenderBridgeAbiVersion);
        }
        else if (result < 0 && InterlockedCompareExchange(&ManagedRenderBridgeFailureLogged, 1, 0) == 0)
        {
            AppendDx9LogHex(L"umbra_managed_render_bridge_failure", result);
        }

        return result;
    }

    bool RenderUmbraImGui(IDirect3DDevice9* device, const D3DVIEWPORT9& viewport)
    {
        const bool diagnoseFirstFrame =
            InterlockedCompareExchange(&ImGuiFirstFrameDiagnosticsClaimed, 1, 0) == 0;
        if (diagnoseFirstFrame)
            AppendDx9LogLiteral(L"umbra_imgui_first_frame_stage=begin");

        if (!InitializeUmbraImGui(device))
            return false;
        if (diagnoseFirstFrame)
            AppendDx9LogLiteral(L"umbra_imgui_first_frame_stage=initialized");

        UpdateOverlayInput();
        ConfigureUmbraImGuiStyle();
        if (diagnoseFirstFrame)
            AppendDx9LogLiteral(L"umbra_imgui_first_frame_stage=style_ready");

        ImGui_ImplDX9_NewFrame();
        if (diagnoseFirstFrame)
            AppendDx9LogLiteral(L"umbra_imgui_first_frame_stage=dx9_new_frame");
        ImGui_ImplWin32_NewFrame();
        if (diagnoseFirstFrame)
            AppendDx9LogLiteral(L"umbra_imgui_first_frame_stage=win32_new_frame");
        ImGui::NewFrame();
        if (diagnoseFirstFrame)
            AppendDx9LogLiteral(L"umbra_imgui_first_frame_stage=imgui_new_frame");

        if (!DevUiEnabled)
        {
            UmbraDeveloperBarVisible = false;
            UmbraDeveloperLogOpen = false;
        }
        DrawUmbraDeveloperBar(viewport);
        DrawUmbraImGuiDock();
        DrawUmbraImGuiToasts(viewport);
        DrawUmbraDeveloperLogWindow();
        if (SettingsWindowOpen)
            DrawUmbraImGuiSettingsWindow();
        if (diagnoseFirstFrame)
            AppendDx9LogLiteral(L"umbra_imgui_first_frame_stage=native_ui_drawn");
        int managedResult = NotifyManagedRenderEvent(UmbraRenderFrame, &viewport);
        if (diagnoseFirstFrame)
            AppendDx9LogLiteral(L"umbra_imgui_first_frame_stage=managed_callback_complete");
        if (PluginInstallerOpen && managedResult != 0)
            DrawUmbraImGuiPluginInstallerWindow();

        ImGui::Render();
        if (diagnoseFirstFrame)
            AppendDx9LogLiteral(L"umbra_imgui_first_frame_stage=draw_data_ready");
        ImGui_ImplDX9_RenderDrawData(ImGui::GetDrawData());
        if (diagnoseFirstFrame)
            AppendDx9LogLiteral(L"umbra_imgui_first_frame_stage=draw_data_submitted");

        if (InterlockedCompareExchange(&ImGuiRenderLogged, 1, 0) == 0)
        {
            AppendDx9LogLiteral(L"umbra_imgui_frame_rendered=true");
            AppendDx9LogLiteral(L"umbra_ui_icons_rendered=true");
            AppendDx9LogLiteral(L"umbra_toast_stack_rendered=true");
            AppendDx9LogLiteral(L"umbra_ready=true");
        }

        return true;
    }

    void RenderUmbraOverlay(IDirect3DDevice9* device)
    {
        if (device == nullptr)
            return;

        D3DVIEWPORT9 viewport{};
        if (FAILED(device->GetViewport(&viewport)))
            return;

        if (RenderUmbraImGui(device, viewport))
            return;

        UpdateOverlayInput();

        OverlayRect settingsIcon{ 8, 8, 32, 32 };
        OverlayRect pluginsIcon{ 48, 8, 32, 32 };
        if (MouseClicked && IsRectHot(settingsIcon))
            SettingsWindowOpen = !SettingsWindowOpen;
        if (MouseClicked && IsRectHot(pluginsIcon))
            PluginInstallerOpen = !PluginInstallerOpen;

        IDirect3DStateBlock9* stateBlock = nullptr;
        if (SUCCEEDED(device->CreateStateBlock(D3DSBT_ALL, &stateBlock)) && stateBlock != nullptr)
            stateBlock->Capture();

        device->SetTexture(0, nullptr);
        device->SetFVF(OverlayFvf);
        device->SetTextureStageState(0, D3DTSS_COLOROP, D3DTOP_SELECTARG1);
        device->SetTextureStageState(0, D3DTSS_COLORARG1, D3DTA_DIFFUSE);
        device->SetTextureStageState(0, D3DTSS_ALPHAOP, D3DTOP_SELECTARG1);
        device->SetTextureStageState(0, D3DTSS_ALPHAARG1, D3DTA_DIFFUSE);
        device->SetTextureStageState(1, D3DTSS_COLOROP, D3DTOP_DISABLE);
        device->SetTextureStageState(1, D3DTSS_ALPHAOP, D3DTOP_DISABLE);
        device->SetRenderState(D3DRS_ALPHABLENDENABLE, TRUE);
        device->SetRenderState(D3DRS_SRCBLEND, D3DBLEND_SRCALPHA);
        device->SetRenderState(D3DRS_DESTBLEND, D3DBLEND_INVSRCALPHA);
        device->SetRenderState(D3DRS_ALPHATESTENABLE, FALSE);
        device->SetRenderState(D3DRS_LIGHTING, FALSE);
        device->SetRenderState(D3DRS_ZENABLE, FALSE);
        device->SetRenderState(D3DRS_ZWRITEENABLE, FALSE);
        device->SetRenderState(D3DRS_STENCILENABLE, FALSE);
        device->SetRenderState(D3DRS_FOGENABLE, FALSE);
        device->SetRenderState(D3DRS_CULLMODE, D3DCULL_NONE);
        device->SetRenderState(D3DRS_SCISSORTESTENABLE, FALSE);

        OverlayBegin();
        if (NativeMarkerEnabled != 0)
        {
            OverlayAddRect(8.0f, 8.0f, 24.0f, 24.0f, D3DCOLOR_ARGB(220, 4, 8, 12));
            OverlayAddRect(10.0f, 10.0f, 20.0f, 20.0f, D3DCOLOR_ARGB(230, 0, 180, 255));
        }

        DrawIcon(settingsIcon, "S", SettingsWindowOpen, IsRectHot(settingsIcon));
        DrawIcon(pluginsIcon, "P", PluginInstallerOpen, IsRectHot(pluginsIcon));
        OverlayAddText(90, 18, "UMBRA", 2, D3DCOLOR_ARGB(235, 220, 236, 244));
        DrawBottomRightToasts(static_cast<int>(viewport.Width), static_cast<int>(viewport.Height));
        if (SettingsWindowOpen)
            DrawSettingsWindow();
        if (PluginInstallerOpen)
            DrawPluginInstallerWindow();
        OverlayFlush(device);

        if (stateBlock != nullptr)
        {
            stateBlock->Apply();
            stateBlock->Release();
        }

        if (InterlockedCompareExchange(&NativeUiShellLogged, 1, 0) == 0)
        {
            AppendDx9LogLiteral(L"umbra_native_ui_shell_initialized=true");
            AppendDx9LogLiteral(L"umbra_ui_icons_rendered=true");
            AppendDx9LogLiteral(L"umbra_toast_stack_rendered=true");
            AppendDx9LogLiteral(User32GetAsyncKeyState != nullptr
                ? L"umbra_ui_input_polling=enabled"
                : L"umbra_ui_input_polling=unavailable");
        }

        if (InterlockedCompareExchange(&NativeUiViewportLogged, 1, 0) == 0)
        {
            AppendDx9LogUInt(L"umbra_ui_viewport_width", viewport.Width);
            AppendDx9LogUInt(L"umbra_ui_viewport_height", viewport.Height);
        }
    }

    void RenderNativeMarker(IDirect3DDevice9* device)
    {
        if (NativeMarkerEnabled == 0 || device == nullptr)
            return;

        D3DRECT border{ 8, 8, 32, 32 };
        D3DRECT inner{ 10, 10, 30, 30 };
        device->Clear(1, &border, D3DCLEAR_TARGET, D3DCOLOR_ARGB(220, 4, 8, 12), 1.0f, 0);
        device->Clear(1, &inner, D3DCLEAR_TARGET, D3DCOLOR_ARGB(230, 0, 180, 255), 1.0f, 0);
    }

    void LogNativeReady(const wchar_t* observedLine)
    {
        AppendDx9LogLiteral(observedLine);
        if (InterlockedCompareExchange(&NativeReadyLogged, 1, 0) != 0)
            return;

        AppendDx9LogLiteral(NativeMarkerEnabled != 0
            ? L"umbra_native_overlay_marker_rendered=true"
            : L"umbra_native_overlay_marker_rendered=false");
        AppendDx9LogLiteral(L"umbra_ready_native=true");
    }

    bool HookSwapChain(IDirect3DSwapChain9* swapChain)
    {
        if (swapChain == nullptr)
            return false;

        void** vtable = *reinterpret_cast<void***>(swapChain);
        if (vtable == nullptr)
            return false;

        void* originalPresent = reinterpret_cast<void*>(OriginalSwapChainPresent);
        bool presentHooked = PatchVTableSlot(
            &vtable[IDirect3DSwapChain9PresentIndex],
            reinterpret_cast<void*>(&HookedSwapChainPresent),
            &originalPresent);
        OriginalSwapChainPresent = reinterpret_cast<idirect3dswapchain9_present_fn>(originalPresent);

        if (presentHooked && InterlockedCompareExchange(&SwapChainHooked, 1, 0) == 0)
            AppendDx9LogLiteral(L"umbra_dx9_swapchain_present_hooked=true");

        return presentHooked;
    }

    bool HookPrimarySwapChain(IDirect3DDevice9* device)
    {
        if (device == nullptr)
            return false;

        IDirect3DSwapChain9* swapChain = nullptr;
        HRESULT result = device->GetSwapChain(0, &swapChain);
        if (FAILED(result) || swapChain == nullptr)
        {
            AppendDx9LogHex(L"umbra_dx9_get_swapchain_result", result);
            return false;
        }

        bool hooked = HookSwapChain(swapChain);
        swapChain->Release();
        return hooked;
    }

    bool HookDevice(IDirect3DDevice9* device)
    {
        if (device == nullptr)
            return false;

        void** vtable = *reinterpret_cast<void***>(device);
        if (vtable == nullptr)
            return false;

        void* originalReset = reinterpret_cast<void*>(OriginalReset);
        bool resetHooked = PatchVTableSlot(
            &vtable[IDirect3DDevice9ResetIndex],
            reinterpret_cast<void*>(&HookedReset),
            &originalReset);
        OriginalReset = reinterpret_cast<idirect3ddevice9_reset_fn>(originalReset);

        void* originalPresent = reinterpret_cast<void*>(OriginalPresent);
        bool presentHooked = PatchVTableSlot(
            &vtable[IDirect3DDevice9PresentIndex],
            reinterpret_cast<void*>(&HookedPresent),
            &originalPresent);
        OriginalPresent = reinterpret_cast<idirect3ddevice9_present_fn>(originalPresent);

        void* originalEndScene = reinterpret_cast<void*>(OriginalEndScene);
        bool endSceneHooked = PatchVTableSlot(
            &vtable[IDirect3DDevice9EndSceneIndex],
            reinterpret_cast<void*>(&HookedEndScene),
            &originalEndScene);
        OriginalEndScene = reinterpret_cast<idirect3ddevice9_end_scene_fn>(originalEndScene);
        bool swapChainHooked = HookPrimarySwapChain(device);

        if (resetHooked && presentHooked && endSceneHooked && DeviceHooked == 0)
        {
            DeviceHooked = 1;
            AppendDx9LogLiteral(L"umbra_dx9_device_hooked=true");
            AppendDx9LogLiteral(L"umbra_dx9_present_hooked=true");
            AppendDx9LogLiteral(L"umbra_dx9_reset_hooked=true");
            AppendDx9LogLiteral(L"umbra_dx9_end_scene_hooked=true");
            AppendDx9LogLiteral(swapChainHooked
                ? L"umbra_dx9_primary_swapchain_hooked=true"
                : L"umbra_dx9_primary_swapchain_hooked=false");
        }

        return resetHooked && presentHooked && endSceneHooked;
    }

    bool HookDirect3D9Object(IDirect3D9* direct3D)
    {
        if (direct3D == nullptr)
            return false;

        void** vtable = *reinterpret_cast<void***>(direct3D);
        if (vtable == nullptr)
            return false;

        void* originalCreateDevice = reinterpret_cast<void*>(OriginalCreateDevice);
        bool hooked = PatchVTableSlot(
            &vtable[IDirect3D9CreateDeviceIndex],
            reinterpret_cast<void*>(&HookedCreateDevice),
            &originalCreateDevice);
        OriginalCreateDevice = reinterpret_cast<idirect3d9_create_device_fn>(originalCreateDevice);
        if (hooked)
            AppendDx9LogLiteral(L"umbra_dx9_create_device_hooked=true");
        return hooked;
    }

    IDirect3D9* WINAPI HookedDirect3DCreate9(UINT sdkVersion)
    {
        direct3d_create9_fn original = OriginalDirect3DCreate9 != nullptr
            ? OriginalDirect3DCreate9
            : reinterpret_cast<direct3d_create9_fn>(Direct3DCreate9Hook.trampoline);
        if (original == nullptr)
            return nullptr;

        IDirect3D9* direct3D = original(sdkVersion);
        if (Direct3DCreate9Observed == 0)
        {
            Direct3DCreate9Observed = 1;
            AppendDx9LogLiteral(L"umbra_dx9_direct3dcreate9_observed=true");
        }

        HookDirect3D9Object(direct3D);
        return direct3D;
    }

    HRESULT STDMETHODCALLTYPE HookedCreateDevice(
        IDirect3D9* self,
        UINT adapter,
        D3DDEVTYPE deviceType,
        HWND focusWindow,
        DWORD behaviorFlags,
        D3DPRESENT_PARAMETERS* presentationParameters,
        IDirect3DDevice9** returnedDeviceInterface)
    {
        if (OriginalCreateDevice == nullptr)
            return E_FAIL;

        if (CreateDeviceObserved == 0)
        {
            CreateDeviceObserved = 1;
            AppendDx9LogLiteral(L"umbra_dx9_create_device_observed=true");
        }

        HRESULT result = OriginalCreateDevice(
            self,
            adapter,
            deviceType,
            focusWindow,
            behaviorFlags,
            presentationParameters,
            returnedDeviceInterface);
        AppendDx9LogHex(L"umbra_dx9_create_device_result", result);

        if (focusWindow != nullptr)
            GameWindow = focusWindow;
        else if (presentationParameters != nullptr && presentationParameters->hDeviceWindow != nullptr)
            GameWindow = presentationParameters->hDeviceWindow;

        if (SUCCEEDED(result) && returnedDeviceInterface != nullptr && *returnedDeviceInterface != nullptr)
            HookDevice(*returnedDeviceInterface);

        return result;
    }

    HRESULT STDMETHODCALLTYPE HookedPresent(
        IDirect3DDevice9* self,
        const RECT* sourceRect,
        const RECT* destRect,
        HWND destWindowOverride,
        const RGNDATA* dirtyRegion)
    {
        LONG frame = InterlockedIncrement(&PresentFrameCount);

        if (SwapChainHooked == 0)
            RenderUmbraOverlay(self);
        if (frame <= 2)
            AppendDx9LogUInt(L"umbra_dx9_present_frame", static_cast<unsigned long>(frame));
        if (frame == 2)
            LogNativeReady(L"umbra_dx9_present_observed=true");

        if (OriginalPresent == nullptr)
            return E_FAIL;

        return OriginalPresent(self, sourceRect, destRect, destWindowOverride, dirtyRegion);
    }

    HRESULT STDMETHODCALLTYPE HookedSwapChainPresent(
        IDirect3DSwapChain9* self,
        const RECT* sourceRect,
        const RECT* destRect,
        HWND destWindowOverride,
        const RGNDATA* dirtyRegion,
        DWORD flags)
    {
        LONG frame = InterlockedIncrement(&SwapChainPresentFrameCount);

        IDirect3DDevice9* device = nullptr;
        if (self != nullptr && SUCCEEDED(self->GetDevice(&device)) && device != nullptr)
        {
            RenderUmbraOverlay(device);
            device->Release();
        }

        if (frame <= 2)
            AppendDx9LogUInt(L"umbra_dx9_swapchain_present_frame", static_cast<unsigned long>(frame));
        if (frame == 2)
            LogNativeReady(L"umbra_dx9_swapchain_present_observed=true");

        if (OriginalSwapChainPresent == nullptr)
            return E_FAIL;

        return OriginalSwapChainPresent(self, sourceRect, destRect, destWindowOverride, dirtyRegion, flags);
    }

    HRESULT STDMETHODCALLTYPE HookedEndScene(IDirect3DDevice9* self)
    {
        LONG frame = InterlockedIncrement(&EndSceneFrameCount);

        if (frame <= 2)
            AppendDx9LogUInt(L"umbra_dx9_end_scene_frame", static_cast<unsigned long>(frame));
        if (frame == 2)
            LogNativeReady(L"umbra_dx9_end_scene_observed=true");

        if (OriginalEndScene == nullptr)
            return E_FAIL;

        return OriginalEndScene(self);
    }

    HRESULT STDMETHODCALLTYPE HookedReset(
        IDirect3DDevice9* self,
        D3DPRESENT_PARAMETERS* presentationParameters)
    {
        LONG resetCount = InterlockedIncrement(&ResetCount);
        InterlockedExchange(&PresentFrameCount, 0);
        InterlockedExchange(&SwapChainPresentFrameCount, 0);
        InterlockedExchange(&EndSceneFrameCount, 0);
        AppendDx9LogUInt(L"umbra_dx9_reset_count", static_cast<unsigned long>(resetCount));

        if (presentationParameters != nullptr && presentationParameters->hDeviceWindow != nullptr)
            GameWindow = presentationParameters->hDeviceWindow;

        if (OriginalReset == nullptr)
            return E_FAIL;

        NotifyManagedRenderEvent(UmbraRenderBeforeReset);
        if (ImGuiInitialized)
            ImGui_ImplDX9_InvalidateDeviceObjects();

        HRESULT result = OriginalReset(self, presentationParameters);
        AppendDx9LogHex(L"umbra_dx9_reset_result", result);
        if (SUCCEEDED(result) && ImGuiInitialized)
        {
            HookPrimarySwapChain(self);
            ImGui_ImplDX9_CreateDeviceObjects();
            AppendDx9LogLiteral(L"umbra_imgui_device_objects_recreated=true");
            NotifyManagedRenderEvent(UmbraRenderAfterReset);
        }

        return result;
    }

    bool StartDx9HookLayer(HANDLE log)
    {
        wchar_t nativeMarker[32]{};
        if (GetUmbraEnvironmentValue(L"NATIVE_MARKER", nativeMarker, 32))
        {
            NativeMarkerEnabled = IsTruthy(nativeMarker) ? 1 : 0;
        }

        AppendLogLiteral(log, L"umbra_dx9_hook_layer=starting");
        AppendLogLiteral(log, NativeMarkerEnabled != 0
            ? L"umbra_native_overlay_marker_enabled=true"
            : L"umbra_native_overlay_marker_enabled=false");

        HMODULE d3d9 = nullptr;
        DWORD waited = 0;
        while (waited <= Dx9HookWaitMs)
        {
            d3d9 = GetModuleHandleW(L"d3d9.dll");
            if (d3d9 != nullptr)
                break;

            Sleep(Dx9HookPollMs);
            waited += Dx9HookPollMs;
        }

        if (d3d9 == nullptr)
        {
            AppendLogUInt(log, L"umbra_dx9_d3d9_wait_timeout_ms", waited);
            AppendLogLiteral(log, L"umbra_dx9_hook_layer=not_installed");
            return false;
        }

        AppendLogUInt(log, L"umbra_dx9_d3d9_wait_ms", waited);
        bool importHooked = HookDirect3DCreate9Import(log);
        if (importHooked)
        {
            AppendLogLiteral(log, L"umbra_dx9_direct3dcreate9_hooked=true");
            AppendLogLiteral(log, L"umbra_dx9_hook_layer=installed");
            return true;
        }

        AppendLogLiteral(log, L"umbra_dx9_direct3dcreate9_import_hooked=false");
        void* create9 = reinterpret_cast<void*>(GetProcAddress(d3d9, "Direct3DCreate9"));
        if (create9 == nullptr)
        {
            AppendLogUInt(log, L"umbra_dx9_direct3dcreate9_error", GetLastError());
            AppendLogLiteral(log, L"umbra_dx9_hook_layer=not_installed");
            return false;
        }

        bool hooked = InstallJumpHook(
            log,
            Direct3DCreate9Hook,
            create9,
            reinterpret_cast<void*>(&HookedDirect3DCreate9),
            L"umbra_dx9_direct3dcreate9_hooked=true");
        if (hooked)
            OriginalDirect3DCreate9 = reinterpret_cast<direct3d_create9_fn>(Direct3DCreate9Hook.trampoline);
        AppendLogLiteral(log, hooked
            ? L"umbra_dx9_hook_layer=installed"
            : L"umbra_dx9_hook_layer=not_installed");
        return hooked;
    }

    bool FileExists(const wchar_t* path)
    {
        DWORD attributes = GetFileAttributesW(path);
        return attributes != INVALID_FILE_ATTRIBUTES && (attributes & FILE_ATTRIBUTE_DIRECTORY) == 0;
    }

    void ParentDirectory(const wchar_t* path, wchar_t* output, DWORD outputChars)
    {
        CopyString(output, outputChars, path);
        DWORD length = StringLength(output);
        while (length > 0)
        {
            wchar_t current = output[length - 1];
            if (current == L'\\' || current == L'/')
            {
                output[length - 1] = L'\0';
                return;
            }

            length--;
        }

        output[0] = L'\0';
    }

    void CombinePath(const wchar_t* left, const wchar_t* right, wchar_t* output, DWORD outputChars)
    {
        CopyString(output, outputChars, left);
        DWORD length = StringLength(output);
        if (length > 0 && output[length - 1] != L'\\' && output[length - 1] != L'/')
            AppendString(output, outputChars, L"\\");
        AppendString(output, outputChars, right);
    }

    bool ContainsAscii(const char* haystack, DWORD haystackLength, const char* needle)
    {
        if (haystack == nullptr || needle == nullptr)
            return false;

        DWORD needleLength = AnsiLength(needle);
        if (needleLength == 0 || haystackLength < needleLength)
            return false;

        for (DWORD index = 0; index <= haystackLength - needleLength; index++)
        {
            bool matched = true;
            for (DWORD needleIndex = 0; needleIndex < needleLength; needleIndex++)
            {
                if (haystack[index + needleIndex] != needle[needleIndex])
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
                return true;
        }

        return false;
    }

    void EnsureDirectoryTree(const wchar_t* directory)
    {
        if (directory == nullptr || directory[0] == L'\0')
            return;

        wchar_t parent[BufferChars]{};
        ParentDirectory(directory, parent, BufferChars);
        if (parent[0] != L'\0' && GetFileAttributesW(parent) == INVALID_FILE_ATTRIBUTES)
            EnsureDirectoryTree(parent);

        CreateDirectoryW(directory, nullptr);
    }

    bool ResolveDevBridgeControlPath(wchar_t* output, DWORD outputChars)
    {
        if (outputChars == 0)
            return false;

        output[0] = L'\0';
        if (GetUmbraEnvironmentValue(L"DEV_BRIDGE_CONTROL", output, outputChars))
            return true;

        wchar_t cacheDirectory[BufferChars]{};
        if (GetUmbraEnvironmentValue(L"CACHE_DIR", cacheDirectory, BufferChars))
        {
            wchar_t bridgeDirectory[BufferChars]{};
            CombinePath(cacheDirectory, L"DevBridge", bridgeDirectory, BufferChars);
            CombinePath(bridgeDirectory, L"control.json", output, outputChars);
            return true;
        }

        wchar_t pluginDirectory[BufferChars]{};
        if (GetUmbraEnvironmentValue(L"PLUGIN_DIR", pluginDirectory, BufferChars))
        {
            wchar_t umbraDirectory[BufferChars]{};
            wchar_t cacheFromPlugin[BufferChars]{};
            wchar_t bridgeDirectory[BufferChars]{};
            ParentDirectory(pluginDirectory, umbraDirectory, BufferChars);
            if (umbraDirectory[0] == L'\0')
                return false;

            CombinePath(umbraDirectory, L"Cache", cacheFromPlugin, BufferChars);
            CombinePath(cacheFromPlugin, L"DevBridge", bridgeDirectory, BufferChars);
            CombinePath(bridgeDirectory, L"control.json", output, outputChars);
            return true;
        }

        return false;
    }

    bool ReadDevBridgeControlState(bool* enabled)
    {
        if (enabled == nullptr)
            return false;

        if (DevBridgeControlPath[0] == L'\0' && !ResolveDevBridgeControlPath(DevBridgeControlPath, BufferChars))
            return false;

        HANDLE file = CreateFileW(
            DevBridgeControlPath,
            GENERIC_READ,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            nullptr,
            OPEN_EXISTING,
            FILE_ATTRIBUTE_NORMAL,
            nullptr);
        if (file == INVALID_HANDLE_VALUE)
            return false;

        char buffer[2048]{};
        DWORD read = 0;
        BOOL ok = ReadFile(file, buffer, sizeof(buffer) - 1, &read, nullptr);
        CloseHandle(file);
        if (!ok)
            return false;

        *enabled = ContainsAscii(buffer, read, "\"enabled\": true")
            || ContainsAscii(buffer, read, "\"enabled\":true");
        return true;
    }

    void RefreshDevBridgeControlState(bool force)
    {
        DWORD now = GetTickCount();
        if (!force && now - DevBridgeLastControlCheckTicks < 1000)
            return;

        DevBridgeLastControlCheckTicks = now;
        bool enabled = false;
        if (ReadDevBridgeControlState(&enabled))
        {
            DevBridgeEnabled = enabled;
            DevBridgeControlKnown = true;
        }
    }

    void WriteDevBridgeControlState(bool enabled)
    {
        if (DevBridgeControlPath[0] == L'\0' && !ResolveDevBridgeControlPath(DevBridgeControlPath, BufferChars))
            return;

        wchar_t parent[BufferChars]{};
        ParentDirectory(DevBridgeControlPath, parent, BufferChars);
        EnsureDirectoryTree(parent);

        SYSTEMTIME time{};
        GetSystemTime(&time);
        wchar_t timeText[64]{};
        wsprintfW(
            timeText,
            L"%04u-%02u-%02uT%02u:%02u:%02uZ",
            time.wYear,
            time.wMonth,
            time.wDay,
            time.wHour,
            time.wMinute,
            time.wSecond);

        HANDLE file = CreateFileW(
            DevBridgeControlPath,
            GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            nullptr,
            CREATE_ALWAYS,
            FILE_ATTRIBUTE_NORMAL,
            nullptr);
        if (file == INVALID_HANDLE_VALUE)
            return;

        WriteWide(file, L"{\n  \"enabled\": ");
        WriteWide(file, enabled ? L"true" : L"false");
        WriteWide(file, L",\n  \"port\": 8797,\n  \"updated_at\": \"");
        WriteWide(file, timeText);
        WriteWide(file, L"\"\n}\n");
        CloseHandle(file);

        DevBridgeEnabled = enabled;
        DevBridgeControlKnown = true;
    }

    bool BuildTrustedPlatformAssemblies(const wchar_t* assemblyDirectory, char* output, DWORD outputBytes)
    {
        if (outputBytes == 0)
            return false;

        output[0] = '\0';

        wchar_t searchPath[BufferChars]{};
        CombinePath(assemblyDirectory, L"*.dll", searchPath, BufferChars);

        WIN32_FIND_DATAW findData{};
        HANDLE find = FindFirstFileW(searchPath, &findData);
        if (find == INVALID_HANDLE_VALUE)
            return false;

        do
        {
            if ((findData.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0)
                continue;

            wchar_t assemblyPath[BufferChars]{};
            CombinePath(assemblyDirectory, findData.cFileName, assemblyPath, BufferChars);
            if (output[0] != '\0')
                AppendAnsi(output, outputBytes, ";");
            AppendUtf8Wide(output, outputBytes, assemblyPath);
        } while (FindNextFileW(find, &findData));

        FindClose(find);
        return output[0] != '\0';
    }

    void ReplaceExtension(const wchar_t* path, const wchar_t* extension, wchar_t* output, DWORD outputChars)
    {
        CopyString(output, outputChars, path);
        DWORD length = StringLength(output);
        DWORD slash = 0;
        DWORD dot = 0;

        for (DWORD index = 0; index < length; index++)
        {
            if (output[index] == L'\\' || output[index] == L'/')
                slash = index + 1;
            else if (output[index] == L'.')
                dot = index;
        }

        if (dot < slash)
            dot = length;

        output[dot] = L'\0';
        AppendString(output, outputChars, extension);
    }

    void ResolveAssemblyPath(const wchar_t* frameworkPath, wchar_t* output, DWORD outputChars)
    {
        CopyString(output, outputChars, frameworkPath);
        DWORD length = StringLength(output);
        if (length >= 4 && lstrcmpiW(output + length - 4, L".exe") == 0)
        {
            wchar_t dllPath[BufferChars]{};
            ReplaceExtension(output, L".dll", dllPath, BufferChars);
            if (FileExists(dllPath))
                CopyString(output, outputChars, dllPath);
        }
    }

    HMODULE LoadHostFxr(HANDLE log, const wchar_t* assemblyPath)
    {
        wchar_t explicitPath[BufferChars]{};
        if (GetUmbraEnvironmentValue(L"HOSTFXR", explicitPath, BufferChars))
        {
            HMODULE module = LoadLibraryW(explicitPath);
            if (module != nullptr)
            {
                AppendLogValue(log, L"umbra_hostfxr", explicitPath);
                return module;
            }
        }

        wchar_t assemblyDirectory[BufferChars]{};
        wchar_t candidate[BufferChars]{};
        ParentDirectory(assemblyPath, assemblyDirectory, BufferChars);
        if (assemblyDirectory[0] != L'\0')
        {
            CombinePath(assemblyDirectory, L"hostfxr.dll", candidate, BufferChars);
            HMODULE module = LoadLibraryW(candidate);
            if (module != nullptr)
            {
                AppendLogValue(log, L"umbra_hostfxr", candidate);
                return module;
            }
        }

        HMODULE module = LoadLibraryW(L"hostfxr.dll");
        if (module != nullptr)
        {
            AppendLogLiteral(log, L"umbra_hostfxr=hostfxr.dll");
            return module;
        }

        AppendLogUInt(log, L"umbra_hostfxr_load_failed", GetLastError());
        return nullptr;
    }

    bool StartManagedFrameworkWithCoreClr(HANDLE log, const wchar_t* assemblyPath)
    {
        AppendLogLiteral(log, L"umbra_coreclr_fallback=true");

        wchar_t assemblyDirectory[BufferChars]{};
        wchar_t coreClrPath[BufferChars]{};
        ParentDirectory(assemblyPath, assemblyDirectory, BufferChars);
        if (assemblyDirectory[0] == L'\0')
        {
            AppendLogLiteral(log, L"umbra_coreclr_failed=missing_assembly_directory");
            return false;
        }

        CombinePath(assemblyDirectory, L"coreclr.dll", coreClrPath, BufferChars);
        if (!FileExists(coreClrPath))
        {
            AppendLogLiteral(log, L"umbra_coreclr_failed=missing_coreclr");
            return false;
        }

        HMODULE coreClr = LoadLibraryW(coreClrPath);
        if (coreClr == nullptr)
        {
            AppendLogUInt(log, L"umbra_coreclr_load_failed", GetLastError());
            return false;
        }

        AppendLogValue(log, L"umbra_coreclr", coreClrPath);
        auto initialize = reinterpret_cast<coreclr_initialize_fn>(
            GetProcAddress(coreClr, "coreclr_initialize"));
        auto createDelegate = reinterpret_cast<coreclr_create_delegate_fn>(
            GetProcAddress(coreClr, "coreclr_create_delegate"));
        if (initialize == nullptr || createDelegate == nullptr)
        {
            AppendLogLiteral(log, L"umbra_coreclr_export_failed=true");
            return false;
        }

        HANDLE heap = GetProcessHeap();
        char* trustedPlatformAssemblies = static_cast<char*>(HeapAlloc(heap, 0, CoreClrPropertyBytes));
        char* appPaths = static_cast<char*>(HeapAlloc(heap, 0, CoreClrPropertyBytes));
        char* exePath = static_cast<char*>(HeapAlloc(heap, 0, CoreClrPropertyBytes));
        if (trustedPlatformAssemblies == nullptr || appPaths == nullptr || exePath == nullptr)
        {
            AppendLogLiteral(log, L"umbra_coreclr_failed=allocation");
            if (trustedPlatformAssemblies != nullptr)
                HeapFree(heap, 0, trustedPlatformAssemblies);
            if (appPaths != nullptr)
                HeapFree(heap, 0, appPaths);
            if (exePath != nullptr)
                HeapFree(heap, 0, exePath);
            return false;
        }

        appPaths[0] = '\0';
        exePath[0] = '\0';
        if (!BuildTrustedPlatformAssemblies(assemblyDirectory, trustedPlatformAssemblies, CoreClrPropertyBytes))
        {
            AppendLogLiteral(log, L"umbra_coreclr_failed=empty_tpa");
            HeapFree(heap, 0, trustedPlatformAssemblies);
            HeapFree(heap, 0, appPaths);
            HeapFree(heap, 0, exePath);
            return false;
        }

        AppendUtf8Wide(appPaths, CoreClrPropertyBytes, assemblyDirectory);
        AppendUtf8Wide(exePath, CoreClrPropertyBytes, assemblyPath);
        AppendLogUInt(log, L"umbra_coreclr_tpa_length", AnsiLength(trustedPlatformAssemblies));

        const char* propertyKeys[] =
        {
            "TRUSTED_PLATFORM_ASSEMBLIES",
            "APP_PATHS",
            "APP_NI_PATHS",
            "NATIVE_DLL_SEARCH_DIRECTORIES",
            "APP_CONTEXT_BASE_DIRECTORY"
        };
        const char* propertyValues[] =
        {
            trustedPlatformAssemblies,
            appPaths,
            appPaths,
            appPaths,
            appPaths
        };

        void* hostHandle = nullptr;
        unsigned int domainId = 0;
        int rc = initialize(
            exePath,
            "Aether.Umbra",
            static_cast<int>(sizeof(propertyKeys) / sizeof(propertyKeys[0])),
            propertyKeys,
            propertyValues,
            &hostHandle,
            &domainId);
        AppendLogHex(log, L"umbra_coreclr_initialize", rc);
        if (rc != 0 || hostHandle == nullptr)
        {
            HeapFree(heap, 0, trustedPlatformAssemblies);
            HeapFree(heap, 0, appPaths);
            HeapFree(heap, 0, exePath);
            return false;
        }

        void* entryPoint = nullptr;
        rc = createDelegate(
            hostHandle,
            domainId,
            "Aether.Umbra.Framework",
            "Aether.Umbra.Framework.UmbraInProcessEntryPoint",
            "UmbraBootstrapCoreClr",
            &entryPoint);
        AppendLogHex(log, L"umbra_coreclr_create_delegate", rc);
        if (rc != 0 || entryPoint == nullptr)
        {
            HeapFree(heap, 0, trustedPlatformAssemblies);
            HeapFree(heap, 0, appPaths);
            HeapFree(heap, 0, exePath);
            return false;
        }

        void* renderBridge = nullptr;
        rc = createDelegate(
            hostHandle,
            domainId,
            "Aether.Umbra.Framework",
            "Aether.Umbra.Framework.UmbraManagedRenderEntryPoint",
            "UmbraRenderBridgeCoreClr",
            &renderBridge);
        AppendLogHex(log, L"umbra_coreclr_render_bridge_delegate", rc);
        if (rc == 0 && renderBridge != nullptr)
        {
            AppendLogLiteral(log, L"umbra_managed_render_bridge_resolved=true");
            AppendLogLiteral(log, L"umbra_managed_render_bridge_state=waiting_for_bootstrap");
        }
        else
        {
            AppendLogLiteral(log, L"umbra_managed_render_bridge_resolved=false");
        }

        wchar_t managedLogPath[BufferChars]{};
        GetUmbraEnvironmentValue(L"LOG", managedLogPath, BufferChars);
        AppendLogValue(log, L"umbra_coreclr_managed_log_arg", managedLogPath);
        AppendLogLiteral(log, L"umbra_coreclr_in_process_start=true");
        int managedResult = reinterpret_cast<coreclr_bootstrap_fn>(entryPoint)(
            managedLogPath,
            static_cast<int>((StringLength(managedLogPath) + 1) * sizeof(wchar_t)));
        AppendLogUInt(log, L"umbra_coreclr_in_process_result", static_cast<unsigned long>(managedResult));

        // Resolving a CoreCLR delegate does not mean the runtime is ready to
        // accept calls from the DX9 render thread. In particular, Wine can
        // block indefinitely while entering the first managed bootstrap
        // method. Publishing the delegate before that call completed caused
        // Present() to enter CoreCLR concurrently and freeze the game on a
        // black frame. Keep native rendering independent until managed
        // bootstrap has returned successfully.
        if (managedResult == 0 && renderBridge != nullptr)
        {
            ManagedRenderBridge = reinterpret_cast<umbra_render_bridge_fn>(renderBridge);
            AppendLogLiteral(log, L"umbra_managed_render_bridge_published=true");
        }
        else if (renderBridge != nullptr)
        {
            AppendLogLiteral(log, L"umbra_managed_render_bridge_published=false");
        }

        HeapFree(heap, 0, trustedPlatformAssemblies);
        HeapFree(heap, 0, appPaths);
        HeapFree(heap, 0, exePath);
        return managedResult == 0;
    }

    bool StartManagedFrameworkInProcess(HANDLE log, const wchar_t* frameworkPath)
    {
        if (frameworkPath == nullptr || frameworkPath[0] == L'\0')
        {
            AppendLogLiteral(log, L"umbra_framework_host_skipped=missing_framework_path");
            return false;
        }

        wchar_t managedOnWine[32]{};
        if (IsWine()
            && (!GetUmbraEnvironmentValue(L"ENABLE_MANAGED_ON_WINE", managedOnWine, 32)
                || !IsTruthy(managedOnWine)))
        {
            AppendLogLiteral(log, L"umbra_framework_host_skipped=wine_x86_managed_host_disabled");
            AppendLogLiteral(log, L"umbra_framework_host_note=x86_dotnet_self_contained_hangs_under_current_wine");
            return false;
        }

        wchar_t assemblyPath[BufferChars]{};
        wchar_t runtimeConfigPath[BufferChars]{};
        ResolveAssemblyPath(frameworkPath, assemblyPath, BufferChars);
        ReplaceExtension(assemblyPath, L".runtimeconfig.json", runtimeConfigPath, BufferChars);
        AppendLogValue(log, L"umbra_framework_assembly", assemblyPath);
        AppendLogValue(log, L"umbra_framework_runtimeconfig", runtimeConfigPath);

        if (!FileExists(assemblyPath))
        {
            AppendLogLiteral(log, L"umbra_framework_host_failed=missing_assembly");
            return false;
        }

        if (!FileExists(runtimeConfigPath))
        {
            AppendLogLiteral(log, L"umbra_framework_host_failed=missing_runtimeconfig");
            return false;
        }

        HMODULE hostfxr = LoadHostFxr(log, assemblyPath);
        if (hostfxr == nullptr)
            return false;

        auto initialize = reinterpret_cast<hostfxr_initialize_for_runtime_config_fn>(
            GetProcAddress(hostfxr, "hostfxr_initialize_for_runtime_config"));
        auto getDelegate = reinterpret_cast<hostfxr_get_runtime_delegate_fn>(
            GetProcAddress(hostfxr, "hostfxr_get_runtime_delegate"));
        auto close = reinterpret_cast<hostfxr_close_fn>(
            GetProcAddress(hostfxr, "hostfxr_close"));

        if (initialize == nullptr || getDelegate == nullptr || close == nullptr)
        {
            AppendLogLiteral(log, L"umbra_hostfxr_export_failed=true");
            return false;
        }

        hostfxr_handle context = nullptr;
        int rc = initialize(runtimeConfigPath, nullptr, &context);
        AppendLogHex(log, L"umbra_hostfxr_initialize", rc);
        if (rc != 0 || context == nullptr)
            return StartManagedFrameworkWithCoreClr(log, assemblyPath);

        void* loadAssembly = nullptr;
        rc = getDelegate(context, HostFxrDelegateLoadAssemblyAndGetFunctionPointer, &loadAssembly);
        AppendLogHex(log, L"umbra_hostfxr_get_delegate", rc);
        close(context);
        if (rc != 0 || loadAssembly == nullptr)
            return false;

        auto loadAssemblyAndGetFunctionPointer =
            reinterpret_cast<load_assembly_and_get_function_pointer_fn>(loadAssembly);
        void* entryPoint = nullptr;
        AppendLogLiteral(log, L"umbra_framework_entrypoint_resolve_start=true");
        rc = loadAssemblyAndGetFunctionPointer(
            assemblyPath,
            L"Aether.Umbra.Framework.UmbraInProcessEntryPoint, Aether.Umbra.Framework",
            L"UmbraBootstrap",
            UnmanagedCallersOnlyMethod,
            nullptr,
            &entryPoint);
        AppendLogHex(log, L"umbra_framework_entrypoint_resolve", rc);
        if (rc != 0 || entryPoint == nullptr)
            return false;

        void* renderBridge = nullptr;
        rc = loadAssemblyAndGetFunctionPointer(
            assemblyPath,
            L"Aether.Umbra.Framework.UmbraManagedRenderEntryPoint, Aether.Umbra.Framework",
            L"UmbraRenderBridge",
            UnmanagedCallersOnlyMethod,
            nullptr,
            &renderBridge);
        AppendLogHex(log, L"umbra_framework_render_bridge_resolve", rc);
        if (rc == 0 && renderBridge != nullptr)
        {
            AppendLogLiteral(log, L"umbra_managed_render_bridge_resolved=true");
            AppendLogLiteral(log, L"umbra_managed_render_bridge_state=waiting_for_bootstrap");
        }
        else
        {
            AppendLogLiteral(log, L"umbra_managed_render_bridge_resolved=false");
        }

        AppendLogLiteral(log, L"umbra_framework_in_process_start=true");
        int managedResult = reinterpret_cast<umbra_bootstrap_fn>(entryPoint)();
        AppendLogUInt(log, L"umbra_framework_in_process_result", static_cast<unsigned long>(managedResult));

        if (managedResult == 0 && renderBridge != nullptr)
        {
            ManagedRenderBridge = reinterpret_cast<umbra_render_bridge_fn>(renderBridge);
            AppendLogLiteral(log, L"umbra_managed_render_bridge_published=true");
        }
        else if (renderBridge != nullptr)
        {
            AppendLogLiteral(log, L"umbra_managed_render_bridge_published=false");
        }
        return managedResult == 0;
    }

    DWORD WINAPI UmbraBootstrapThread(LPVOID)
    {
        wchar_t delayText[32]{};
        if (GetUmbraEnvironmentValue(L"LOAD_DELAY_MS", delayText, 32))
            Sleep(ParseUInt(delayText));

        HANDLE log = OpenBootstrapLog();
        if (log == INVALID_HANDLE_VALUE)
            return 0;

        wchar_t frameworkPath[BufferChars]{};
        wchar_t pluginDirectory[BufferChars]{};
        wchar_t safeMode[32]{};
        wchar_t repositoryUrls[BufferChars]{};
        wchar_t repositoriesJson[BufferChars]{};
        GetUmbraEnvironmentValue(L"FRAMEWORK", frameworkPath, BufferChars);
        GetUmbraEnvironmentValue(L"PLUGIN_DIR", pluginDirectory, BufferChars);
        GetUmbraEnvironmentValue(L"SAFE_MODE", safeMode, 32);
        GetUmbraEnvironmentValue(L"REPOSITORY_URLS", repositoryUrls, BufferChars);
        GetUmbraEnvironmentValue(L"REPOSITORIES_JSON", repositoriesJson, BufferChars);

        AppendLogLiteral(log, L"umbra_bootstrap_loaded=true");
        AppendLogLiteral(log, L"umbra_dllmain_process_attach=true");
        AppendLogValue(log, L"umbra_framework", frameworkPath);
        AppendLogValue(log, L"umbra_plugin_dir", pluginDirectory);
        AppendLogValue(log, L"umbra_safe_mode", safeMode);
        AppendLogValue(log, L"umbra_repository_urls", repositoryUrls);
        AppendLogValue(log, L"umbra_repositories_json", repositoriesJson);
        AppendLogLiteral(log, L"umbra_host_mode=in_process");
        AppendLogLiteral(log, L"umbra_dx9_hook_layer=pending");
        AppendLogLiteral(log, L"umbra_imgui_backend=pending");
        AppendLogLiteral(
            log,
            IsTruthy(safeMode)
                ? L"umbra_plugin_execution_enabled=false_safe_mode"
                : L"umbra_plugin_execution_enabled=true");
        StartDx9HookLayer(log);
        bool hosted = StartManagedFrameworkInProcess(log, frameworkPath);
        AppendLogLiteral(log, hosted ? L"umbra_framework_hosted=true" : L"umbra_framework_hosted=false");
        wchar_t diagnosticFlag[32]{};
        if (!hosted
            && GetUmbraEnvironmentValue(L"ALLOW_OUT_OF_PROCESS_DIAGNOSTIC", diagnosticFlag, 32)
            && IsTruthy(diagnosticFlag))
        {
            AppendLogLiteral(log, L"umbra_out_of_process_diagnostic_requested_but_removed=true");
        }

        CloseHandle(log);
        return 0;
    }
}

bool IsManagedUiCallAvailable()
{
    return InterlockedCompareExchange(&ManagedUiCallbackActive, 0, 0) != 0
        && ManagedRenderThreadId == GetCurrentThreadId()
        && ImGui::GetCurrentContext() != nullptr;
}

ImVec4 UmbraUiToneColor(int tone)
{
    const UmbraTheme& theme = GetUmbraTheme();
    if (tone == 1)
        return theme.mutedText;
    if (tone == 2)
        return theme.accent;
    if (tone == 3)
        return theme.warning;
    if (tone == 4)
        return ImVec4(1.0f, 0.34f, 0.38f, 1.0f);
    if (tone == 5)
        return ImVec4(0.30f, 0.95f, 0.55f, 1.0f);
    return theme.text;
}

const char* UmbraUiVisibleLabelEnd(const char* label)
{
    if (label == nullptr)
        return nullptr;
    const char* cursor = label;
    while (*cursor != '\0')
    {
        if (cursor[0] == '#' && cursor[1] == '#')
            return cursor;
        cursor++;
    }
    return cursor;
}

void DrawUmbraSdkIcon(ImDrawList* drawList, int icon, ImVec2 center, float size, ImU32 color)
{
    if (drawList == nullptr || icon <= 0)
        return;

    const UmbraTheme& theme = GetUmbraTheme();
    float r = size * 0.5f;
    float line = size >= 22.0f ? 1.8f : 1.45f;
    if (icon == 1)
    {
        DrawUmbraSigilGlyph(drawList, center, r, theme, true);
        return;
    }
    if (icon == 6)
    {
        DrawUmbraSettingsGlyph(drawList, center, theme);
        return;
    }
    if (icon == 12)
    {
        DrawUmbraPluginGlyph(drawList, center, theme);
        return;
    }

    if (icon == 2)
    {
        drawList->AddCircle(center, r * 0.80f, color, 28, line);
        ImVec2 needle[3] = {
            ImVec2(center.x + r * 0.38f, center.y - r * 0.48f),
            ImVec2(center.x + r * 0.06f, center.y + r * 0.10f),
            ImVec2(center.x - r * 0.38f, center.y + r * 0.48f)
        };
        drawList->AddPolyline(needle, 3, color, ImDrawFlags_None, line * 1.35f);
    }
    else if (icon == 3 || icon == 11)
    {
        drawList->AddRect(
            ImVec2(center.x - r * 0.82f, center.y - r * 0.42f),
            ImVec2(center.x + r * 0.82f, center.y + r * 0.58f),
            color,
            2.5f,
            0,
            line);
        drawList->AddLine(
            ImVec2(center.x - r * 0.72f, center.y - r * 0.42f),
            ImVec2(center.x - r * 0.30f, center.y - r * 0.72f),
            color,
            line);
        drawList->AddLine(
            ImVec2(center.x - r * 0.30f, center.y - r * 0.72f),
            ImVec2(center.x + r * 0.08f, center.y - r * 0.72f),
            color,
            line);
    }
    else if (icon == 4 || icon == 18)
    {
        drawList->AddCircle(center, r * 0.70f, color, 28, line);
        drawList->AddTriangleFilled(
            ImVec2(center.x + r * 0.74f, center.y - r * 0.18f),
            ImVec2(center.x + r * 0.78f, center.y - r * 0.72f),
            ImVec2(center.x + r * 0.30f, center.y - r * 0.48f),
            color);
    }
    else if (icon == 5)
    {
        for (int row = -1; row <= 1; row++)
            drawList->AddRect(
                ImVec2(center.x - r * 0.70f, center.y + row * r * 0.48f - r * 0.18f),
                ImVec2(center.x + r * 0.70f, center.y + row * r * 0.48f + r * 0.18f),
                color,
                r * 0.18f,
                0,
                line);
    }
    else if (icon == 7)
    {
        drawList->AddCircle(center, r * 0.76f, color, 28, line);
        drawList->AddCircleFilled(ImVec2(center.x, center.y - r * 0.30f), r * 0.08f, color, 10);
        drawList->AddLine(ImVec2(center.x, center.y - r * 0.02f), ImVec2(center.x, center.y + r * 0.42f), color, line);
    }
    else if (icon == 8)
    {
        drawList->AddCircle(ImVec2(center.x - r * 0.14f, center.y - r * 0.14f), r * 0.48f, color, 24, line);
        drawList->AddLine(ImVec2(center.x + r * 0.20f, center.y + r * 0.20f), ImVec2(center.x + r * 0.72f, center.y + r * 0.72f), color, line * 1.15f);
    }
    else if (icon == 9)
    {
        ImVec2 points[5] = {
            ImVec2(center.x, center.y - r * 0.82f),
            ImVec2(center.x + r * 0.66f, center.y - r * 0.48f),
            ImVec2(center.x + r * 0.52f, center.y + r * 0.34f),
            ImVec2(center.x, center.y + r * 0.82f),
            ImVec2(center.x - r * 0.52f, center.y + r * 0.34f)
        };
        drawList->AddPolyline(points, 5, color, ImDrawFlags_Closed, line);
    }
    else if (icon == 10)
    {
        drawList->AddLine(ImVec2(center.x, center.y - r * 0.75f), ImVec2(center.x, center.y + r * 0.24f), color, line);
        drawList->AddLine(ImVec2(center.x - r * 0.36f, center.y - r * 0.04f), ImVec2(center.x, center.y + r * 0.32f), color, line);
        drawList->AddLine(ImVec2(center.x + r * 0.36f, center.y - r * 0.04f), ImVec2(center.x, center.y + r * 0.32f), color, line);
        drawList->AddLine(ImVec2(center.x - r * 0.68f, center.y + r * 0.62f), ImVec2(center.x + r * 0.68f, center.y + r * 0.62f), color, line);
    }
    else if (icon == 13)
    {
        for (int y = 0; y < 2; y++)
            for (int x = 0; x < 2; x++)
                drawList->AddRectFilled(
                    ImVec2(center.x + (x == 0 ? -r * 0.72f : r * 0.10f), center.y + (y == 0 ? -r * 0.72f : r * 0.10f)),
                    ImVec2(center.x + (x == 0 ? -r * 0.10f : r * 0.72f), center.y + (y == 0 ? -r * 0.10f : r * 0.72f)),
                    color,
                    2.0f);
    }
    else if (icon == 14)
    {
        for (int row = -1; row <= 1; row++)
        {
            drawList->AddCircleFilled(ImVec2(center.x - r * 0.65f, center.y + row * r * 0.52f), r * 0.09f, color, 10);
            drawList->AddLine(ImVec2(center.x - r * 0.38f, center.y + row * r * 0.52f), ImVec2(center.x + r * 0.72f, center.y + row * r * 0.52f), color, line);
        }
    }
    else if (icon == 15)
    {
        drawList->AddLine(ImVec2(center.x - r * 0.70f, center.y), ImVec2(center.x - r * 0.16f, center.y + r * 0.52f), color, line * 1.35f);
        drawList->AddLine(ImVec2(center.x - r * 0.16f, center.y + r * 0.52f), ImVec2(center.x + r * 0.76f, center.y - r * 0.58f), color, line * 1.35f);
    }
    else if (icon == 16)
    {
        drawList->AddTriangle(ImVec2(center.x, center.y - r * 0.82f), ImVec2(center.x + r * 0.82f, center.y + r * 0.68f), ImVec2(center.x - r * 0.82f, center.y + r * 0.68f), color, line);
        drawList->AddLine(ImVec2(center.x, center.y - r * 0.30f), ImVec2(center.x, center.y + r * 0.20f), color, line);
        drawList->AddCircleFilled(ImVec2(center.x, center.y + r * 0.46f), r * 0.07f, color, 8);
    }
    else if (icon == 17)
    {
        drawList->AddCircle(center, r * 0.76f, color, 28, line);
        drawList->AddLine(ImVec2(center.x - r * 0.36f, center.y - r * 0.36f), ImVec2(center.x + r * 0.36f, center.y + r * 0.36f), color, line);
        drawList->AddLine(ImVec2(center.x + r * 0.36f, center.y - r * 0.36f), ImVec2(center.x - r * 0.36f, center.y + r * 0.36f), color, line);
    }
    else if (icon == 19)
    {
        drawList->AddRect(ImVec2(center.x - r * 0.48f, center.y - r * 0.34f), ImVec2(center.x + r * 0.48f, center.y + r * 0.72f), color, 2.0f, 0, line);
        drawList->AddLine(ImVec2(center.x - r * 0.64f, center.y - r * 0.50f), ImVec2(center.x + r * 0.64f, center.y - r * 0.50f), color, line);
        drawList->AddLine(ImVec2(center.x - r * 0.22f, center.y - r * 0.72f), ImVec2(center.x + r * 0.22f, center.y - r * 0.72f), color, line);
    }
    else if (icon == 20)
    {
        drawList->AddCircle(center, r * 0.70f, color, 28, line);
        drawList->AddLine(ImVec2(center.x, center.y - r * 0.88f), ImVec2(center.x, center.y + r * 0.06f), color, line * 1.25f);
    }
    else
    {
        drawList->AddCircle(center, r * 0.68f, color, 24, line);
    }
}

extern "C" __declspec(dllexport) void __stdcall UmbraUiSetNextWindowSize(float width, float height, int firstUseOnly)
{
    if (IsManagedUiCallAvailable())
        ImGui::SetNextWindowSize(ImVec2(width, height), firstUseOnly != 0 ? ImGuiCond_FirstUseEver : ImGuiCond_Always);
}

extern "C" __declspec(dllexport) float __stdcall UmbraUiGetAvailableContentWidth()
{
    if (!IsManagedUiCallAvailable() || ManagedUiWindowDepth <= 0)
        return 0.0f;

    return ImGui::GetContentRegionAvail().x;
}

extern "C" __declspec(dllexport) float __stdcall UmbraUiGetContentRegionWidth()
{
    if (!IsManagedUiCallAvailable() || ManagedUiWindowDepth <= 0)
        return 0.0f;

    return ImGui::GetWindowContentRegionMax().x - ImGui::GetWindowContentRegionMin().x;
}

extern "C" __declspec(dllexport) int __stdcall UmbraUiBeginWindow(const char* title, int* isOpen)
{
    if (!IsManagedUiCallAvailable() || title == nullptr || isOpen == nullptr)
        return 0;

    bool open = *isOpen != 0;
    bool visible = ImGui::Begin(title, &open, ImGuiWindowFlags_NoSavedSettings);
    *isOpen = open ? 1 : 0;
    ManagedUiWindowDepth++;
    return visible ? 1 : 0;
}

extern "C" __declspec(dllexport) void __stdcall UmbraUiEndWindow()
{
    if (!IsManagedUiCallAvailable() || ManagedUiWindowDepth <= 0 || ManagedUiChildDepth > 0)
        return;

    ImGui::End();
    ManagedUiWindowDepth--;
}

extern "C" __declspec(dllexport) int __stdcall UmbraUiBeginChild(const char* id, float height, int border)
{
    if (!IsManagedUiCallAvailable() || ManagedUiWindowDepth <= 0 || id == nullptr)
        return 0;

    bool visible = ImGui::BeginChild(id, ImVec2(0.0f, height), border != 0);
    ManagedUiChildDepth++;
    return visible ? 1 : 0;
}

extern "C" __declspec(dllexport) int __stdcall UmbraUiBeginPanel(
    const char* id,
    float width,
    float height,
    int style)
{
    if (!IsManagedUiCallAvailable() || ManagedUiWindowDepth <= 0 || id == nullptr)
        return 0;

    const UmbraTheme& theme = GetUmbraTheme();
    ImVec4 background = theme.childBg;
    ImVec4 border = theme.border;
    if (style == 1)
        background = ImVec4(theme.frameBg.x, theme.frameBg.y, theme.frameBg.z, 0.92f);
    else if (style == 2)
        background = ImVec4(theme.windowBg.x, theme.windowBg.y, theme.windowBg.z, 0.98f);
    else if (style == 3)
        background = ImVec4(theme.popupBg.x, theme.popupBg.y, theme.popupBg.z, 0.98f);
    else if (style == 4)
    {
        background = ImVec4(theme.buttonActive.x, theme.buttonActive.y, theme.buttonActive.z, 0.72f);
        border = theme.accent;
    }

    ImGui::PushStyleColor(ImGuiCol_ChildBg, background);
    ImGui::PushStyleColor(ImGuiCol_Border, border);
    ImGui::PushStyleVar(ImGuiStyleVar_ChildRounding, style == 2 ? 0.0f : 10.0f);
    ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(16.0f, 14.0f));
    bool visible = ImGui::BeginChild(
        id,
        ImVec2(width, height),
        ImGuiChildFlags_Borders | ImGuiChildFlags_AlwaysUseWindowPadding,
        ImGuiWindowFlags_NoSavedSettings);
    ImGui::PopStyleVar(2);
    ImGui::PopStyleColor(2);
    ManagedUiChildDepth++;
    return visible ? 1 : 0;
}

extern "C" __declspec(dllexport) void __stdcall UmbraUiEndChild()
{
    if (!IsManagedUiCallAvailable() || ManagedUiChildDepth <= 0)
        return;

    ImGui::EndChild();
    ManagedUiChildDepth--;
}

extern "C" __declspec(dllexport) void __stdcall UmbraUiText(const char* text)
{
    if (!IsManagedUiCallAvailable() || ManagedUiWindowDepth <= 0)
        return;

    ImGui::TextUnformatted(text == nullptr ? "" : text);
}

extern "C" __declspec(dllexport) void __stdcall UmbraUiTextTone(int tone, const char* text)
{
    if (!IsManagedUiCallAvailable() || ManagedUiWindowDepth <= 0)
        return;

    ImGui::TextColored(UmbraUiToneColor(tone), "%s", text == nullptr ? "" : text);
}

extern "C" __declspec(dllexport) void __stdcall UmbraUiTextStyled(int tone, int style, const char* text)
{
    if (!IsManagedUiCallAvailable() || ManagedUiWindowDepth <= 0)
        return;

    float size = 16.0f;
    if (style == 1)
        size = 13.0f;
    else if (style == 2)
        size = 19.0f;
    else if (style == 3)
        size = 26.0f;
    ImGui::PushFont(nullptr, size);
    ImGui::PushStyleColor(ImGuiCol_Text, UmbraUiToneColor(tone));
    ImGui::TextWrapped("%s", text == nullptr ? "" : text);
    ImGui::PopStyleColor();
    ImGui::PopFont();
}

extern "C" __declspec(dllexport) int __stdcall UmbraUiInputText(
    const char* label,
    const char* hint,
    char* buffer,
    int capacity)
{
    if (!IsManagedUiCallAvailable()
        || ManagedUiWindowDepth <= 0
        || label == nullptr
        || buffer == nullptr
        || capacity < 2)
    {
        return 0;
    }

    ImGui::SetNextItemWidth(-1.0f);
    return ImGui::InputTextWithHint(
        label,
        hint == nullptr ? "" : hint,
        buffer,
        static_cast<size_t>(capacity)) ? 1 : 0;
}

extern "C" __declspec(dllexport) int __stdcall UmbraUiButton(const char* label)
{
    if (!IsManagedUiCallAvailable() || ManagedUiWindowDepth <= 0 || label == nullptr)
        return 0;

    return ImGui::Button(label) ? 1 : 0;
}

extern "C" __declspec(dllexport) int __stdcall UmbraUiButtonStyled(
    const char* label,
    int style,
    int icon,
    float requestedWidth,
    float requestedHeight)
{
    if (!IsManagedUiCallAvailable() || ManagedUiWindowDepth <= 0 || label == nullptr)
        return 0;

    const UmbraTheme& theme = GetUmbraTheme();
    const char* textEnd = UmbraUiVisibleLabelEnd(label);
    ImVec2 textSize = ImGui::CalcTextSize(label, textEnd);
    float iconSize = icon == 0 ? 0.0f : 18.0f;
    float width = requestedWidth > 0.0f
        ? requestedWidth
        : textSize.x + 28.0f + (iconSize > 0.0f ? iconSize + 9.0f : 0.0f);
    float height = requestedHeight > 0.0f ? requestedHeight : 38.0f;
    bool pressed = ImGui::InvisibleButton(label, ImVec2(width, height));
    bool hovered = ImGui::IsItemHovered();
    bool active = ImGui::IsItemActive();
    ImVec2 min = ImGui::GetItemRectMin();
    ImVec2 max = ImGui::GetItemRectMax();
    ImDrawList* drawList = ImGui::GetWindowDrawList();

    ImVec4 fill = theme.button;
    ImVec4 border = theme.border;
    ImVec4 foreground = theme.text;
    if (style == 1)
    {
        fill = active ? theme.accentActive : hovered ? theme.accentHover : theme.buttonActive;
        border = hovered ? theme.accentHover : theme.accent;
        foreground = ImVec4(1.0f, 1.0f, 1.0f, 1.0f);
    }
    else if (style == 2)
    {
        fill = hovered ? ImVec4(theme.accent.x, theme.accent.y, theme.accent.z, 0.14f) : ImVec4(0, 0, 0, 0);
        border = hovered ? theme.accent : ImVec4(0, 0, 0, 0);
    }
    else if (style == 3)
    {
        fill = active || hovered ? theme.buttonHovered : ImVec4(theme.frameBg.x, theme.frameBg.y, theme.frameBg.z, 0.42f);
        border = active ? theme.accent : hovered ? theme.accentHover : ImVec4(theme.border.x, theme.border.y, theme.border.z, 0.58f);
        foreground = active ? theme.accentHover : theme.text;
    }
    else if (style == 4)
    {
        fill = active ? ImVec4(0.68f, 0.10f, 0.18f, 0.92f) : hovered ? ImVec4(0.52f, 0.08f, 0.15f, 0.90f) : ImVec4(0.32f, 0.06f, 0.11f, 0.84f);
        border = ImVec4(1.0f, 0.30f, 0.38f, 0.86f);
    }
    else if (hovered)
    {
        fill = active ? theme.buttonActive : theme.buttonHovered;
        border = theme.accentHover;
    }

    drawList->AddRectFilled(ImVec2(min.x + 2.0f, min.y + 3.0f), ImVec2(max.x + 2.0f, max.y + 3.0f), ColorU32(theme.shadow), 8.0f);
    drawList->AddRectFilled(min, max, ColorU32(fill), 8.0f);
    drawList->AddRect(min, max, ColorU32(border), 8.0f, 0, hovered ? 1.6f : 1.0f);
    if (style == 1)
        drawList->AddLine(ImVec2(min.x + 8.0f, min.y + 1.0f), ImVec2(max.x - 8.0f, min.y + 1.0f), ColorU32(ImVec4(1, 1, 1, 0.22f)), 1.0f);

    float contentWidth = textSize.x + (iconSize > 0.0f ? iconSize + 9.0f : 0.0f);
    float contentX = min.x + (width - contentWidth) * 0.5f;
    if (iconSize > 0.0f)
    {
        DrawUmbraSdkIcon(drawList, icon, ImVec2(contentX + iconSize * 0.5f, min.y + height * 0.5f), iconSize, ColorU32(foreground));
        contentX += iconSize + 9.0f;
    }
    drawList->AddText(ImVec2(contentX, min.y + (height - textSize.y) * 0.5f), ColorU32(foreground), label, textEnd);
    return pressed ? 1 : 0;
}

extern "C" __declspec(dllexport) int __stdcall UmbraUiCheckbox(const char* label, int* value)
{
    if (!IsManagedUiCallAvailable() || ManagedUiWindowDepth <= 0 || label == nullptr || value == nullptr)
        return 0;

    bool checked = *value != 0;
    bool changed = ImGui::Checkbox(label, &checked);
    *value = checked ? 1 : 0;
    return changed ? 1 : 0;
}

extern "C" __declspec(dllexport) int __stdcall UmbraUiToggle(const char* label, int* value)
{
    if (!IsManagedUiCallAvailable() || ManagedUiWindowDepth <= 0 || label == nullptr || value == nullptr)
        return 0;

    const UmbraTheme& theme = GetUmbraTheme();
    const char* textEnd = UmbraUiVisibleLabelEnd(label);
    ImVec2 textSize = ImGui::CalcTextSize(label, textEnd);
    const float switchWidth = 44.0f;
    const float switchHeight = 24.0f;
    bool pressed = ImGui::InvisibleButton(label, ImVec2(switchWidth + 10.0f + textSize.x, switchHeight));
    if (pressed)
        *value = *value == 0 ? 1 : 0;

    bool enabled = *value != 0;
    bool hovered = ImGui::IsItemHovered();
    ImVec2 min = ImGui::GetItemRectMin();
    ImDrawList* drawList = ImGui::GetWindowDrawList();
    ImVec4 track = enabled
        ? (hovered ? theme.accentHover : theme.accentActive)
        : (hovered ? theme.frameHovered : theme.frameBg);
    drawList->AddRectFilled(min, ImVec2(min.x + switchWidth, min.y + switchHeight), ColorU32(track), switchHeight * 0.5f);
    drawList->AddRect(min, ImVec2(min.x + switchWidth, min.y + switchHeight), ColorU32(enabled ? theme.accentHover : theme.border), switchHeight * 0.5f, 0, 1.0f);
    float knobX = enabled ? min.x + switchWidth - switchHeight * 0.5f : min.x + switchHeight * 0.5f;
    drawList->AddCircleFilled(ImVec2(knobX, min.y + switchHeight * 0.5f), 8.0f, ColorU32(ImVec4(0.96f, 0.96f, 1.0f, 1.0f)), 24);
    drawList->AddText(
        ImVec2(min.x + switchWidth + 10.0f, min.y + (switchHeight - textSize.y) * 0.5f),
        ColorU32(theme.text),
        label,
        textEnd);
    return pressed ? 1 : 0;
}

extern "C" __declspec(dllexport) void __stdcall UmbraUiSameLine()
{
    if (IsManagedUiCallAvailable() && ManagedUiWindowDepth > 0)
        ImGui::SameLine();
}

extern "C" __declspec(dllexport) void __stdcall UmbraUiSeparator()
{
    if (IsManagedUiCallAvailable() && ManagedUiWindowDepth > 0)
        ImGui::Separator();
}

extern "C" __declspec(dllexport) void __stdcall UmbraUiSpacing(float height)
{
    if (IsManagedUiCallAvailable() && ManagedUiWindowDepth > 0)
        ImGui::Dummy(ImVec2(0.0f, height));
}

extern "C" __declspec(dllexport) void __stdcall UmbraUiIcon(int icon, int tone, float size)
{
    if (!IsManagedUiCallAvailable() || ManagedUiWindowDepth <= 0)
        return;
    ImGui::Dummy(ImVec2(size, size));
    ImVec2 min = ImGui::GetItemRectMin();
    DrawUmbraSdkIcon(
        ImGui::GetWindowDrawList(),
        icon,
        ImVec2(min.x + size * 0.5f, min.y + size * 0.5f),
        size,
        ColorU32(UmbraUiToneColor(tone)));
}

extern "C" __declspec(dllexport) void __stdcall UmbraUiBadge(const char* text, int tone, int icon)
{
    if (!IsManagedUiCallAvailable() || ManagedUiWindowDepth <= 0 || text == nullptr)
        return;
    ImVec2 textSize = ImGui::CalcTextSize(text);
    float iconSize = icon == 0 ? 0.0f : 13.0f;
    float width = textSize.x + 16.0f + (iconSize > 0.0f ? iconSize + 5.0f : 0.0f);
    float height = 25.0f;
    ImGui::Dummy(ImVec2(width, height));
    ImVec2 min = ImGui::GetItemRectMin();
    ImVec2 max = ImGui::GetItemRectMax();
    ImVec4 color = UmbraUiToneColor(tone);
    ImDrawList* drawList = ImGui::GetWindowDrawList();
    drawList->AddRectFilled(min, max, ColorU32(ImVec4(color.x, color.y, color.z, 0.14f)), 6.0f);
    drawList->AddRect(min, max, ColorU32(ImVec4(color.x, color.y, color.z, 0.62f)), 6.0f, 0, 1.0f);
    float x = min.x + 8.0f;
    if (iconSize > 0.0f)
    {
        DrawUmbraSdkIcon(drawList, icon, ImVec2(x + iconSize * 0.5f, min.y + height * 0.5f), iconSize, ColorU32(color));
        x += iconSize + 5.0f;
    }
    drawList->AddText(ImVec2(x, min.y + (height - textSize.y) * 0.5f), ColorU32(color), text);
}

extern "C" __declspec(dllexport) void __stdcall UmbraUiArtwork(const char* seed, int icon, float size)
{
    if (!IsManagedUiCallAvailable() || ManagedUiWindowDepth <= 0)
        return;
    unsigned long hash = 2166136261u;
    const char* cursor = seed == nullptr ? "" : seed;
    while (*cursor != '\0')
    {
        hash ^= static_cast<unsigned char>(*cursor++);
        hash *= 16777619u;
    }
    static const ImVec4 palette[] = {
        ImVec4(0.48f, 0.25f, 0.88f, 1.0f),
        ImVec4(0.10f, 0.52f, 0.66f, 1.0f),
        ImVec4(0.72f, 0.48f, 0.10f, 1.0f),
        ImVec4(0.62f, 0.16f, 0.46f, 1.0f),
        ImVec4(0.12f, 0.56f, 0.42f, 1.0f)
    };
    ImVec4 accent = palette[hash % (sizeof(palette) / sizeof(palette[0]))];
    ImGui::Dummy(ImVec2(size, size));
    ImVec2 min = ImGui::GetItemRectMin();
    ImVec2 max = ImGui::GetItemRectMax();
    ImDrawList* drawList = ImGui::GetWindowDrawList();
    drawList->AddRectFilled(ImVec2(min.x + 3.0f, min.y + 4.0f), ImVec2(max.x + 3.0f, max.y + 4.0f), ColorU32(GetUmbraTheme().shadow), 11.0f);
    drawList->AddRectFilled(min, max, ColorU32(ImVec4(accent.x * 0.34f, accent.y * 0.34f, accent.z * 0.40f, 0.98f)), 11.0f);
    drawList->AddRectFilled(ImVec2(min.x + 5.0f, min.y + 5.0f), ImVec2(max.x - 5.0f, max.y - 5.0f), ColorU32(ImVec4(accent.x, accent.y, accent.z, 0.28f)), 8.0f);
    drawList->AddRect(min, max, ColorU32(ImVec4(accent.x, accent.y, accent.z, 0.90f)), 11.0f, 0, 1.5f);
    DrawUmbraSdkIcon(drawList, icon, ImVec2((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f), size * 0.46f, ColorU32(ImVec4(0.96f, 0.94f, 1.0f, 1.0f)));
}

extern "C" __declspec(dllexport) void __stdcall UmbraUiSetPluginManagerOpen(int isOpen)
{
    if (IsManagedUiCallAvailable())
        PluginInstallerOpen = isOpen != 0;
}

extern "C" BOOL WINAPI DllMain(HMODULE module, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        UmbraModule = module;
        DisableThreadLibraryCalls(module);
        HANDLE thread = CreateThread(nullptr, 0, UmbraBootstrapThread, nullptr, 0, nullptr);
        if (thread != nullptr)
            CloseHandle(thread);
    }

    return TRUE;
}
