using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using AetherXIV.Launcher.Core;

namespace AetherXIV.Launcher.ClientLauncher;

internal static class ClientProcessLauncher
{
    private const int MaxVisibleLogicalProcessors = 15;
    private const uint ImageBase = 0x400000;
    private const uint EncryptionTimePatchAddress = ImageBase + 0x9A15E3;
    private const uint LobbyHostNameAddress = ImageBase + 0xB90110;
    private const uint LobbyHostNamePatchSize = 0x14;
    private const uint BootSkipPatchAddress = 0x403698;
    private const uint LaunchFlag1PatchAddress = ImageBase + 0xBB952B;
    private const uint LaunchFlag2PatchAddress = ImageBase + 0xBB95D3;

    private static readonly byte[] EncryptionTimeOriginalBytes = [0xE8, 0x3D, 0x41, 0xC3, 0xFF];
    private static readonly byte[] EncryptionTimePatchBytes = [0xB8, 0x12, 0xE8, 0xE0, 0x50];
    private static readonly byte[] LobbyHostOriginalBytes = Encoding.ASCII.GetBytes("lobby01.ffxiv.com\0\0\0");
    private static readonly byte[] BootSkipOriginalBytes =
    [
        0x6A, 0xFF, 0xFF, 0x15, 0xCC, 0xE1, 0xF3, 0x00,
        0x50, 0xFF, 0x15, 0xA8, 0xE1, 0xF3, 0x00, 0x6A,
        0xFF, 0xFF, 0x15, 0xAC, 0xE1, 0xF3, 0x00, 0x50,
        0xFF, 0x15, 0xB0, 0xE1, 0xF3, 0x00
    ];
    private static readonly byte[] BootSkipPatchBytes = Enumerable.Repeat<byte>(0x90, 30).ToArray();
    private static readonly byte[] LaunchFlag1OriginalBytes = [0x00, 0xD0];
    private static readonly byte[] LaunchFlag2OriginalBytes = [0x00, 0x00];
    private static readonly byte[] LaunchFlagPatchBytes = [0xB5, 0x01];

    public static ClientLaunchResult Launch(
        LaunchOptions options,
        GameLaunchToken token,
        string lobbyHost,
        Action<string>? log = null)
    {
        NativeMethods.STARTUPINFO startupInfo = new()
        {
            cb = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.STARTUPINFO>()
        };

        UIntPtr? affinityMask = TryCapCurrentProcessAffinity(log);

        string commandLine = BuildGameCommandLine(options, token, log);
        log?.Invoke($"game_command_line_length={commandLine.Length}");
        if (options.Umbra.Enabled)
        {
            UmbraInjector.SetUmbraEnvironment(options.Umbra);
            Environment.SetEnvironmentVariable("AETHER_UMBRA_HELPER_LOG", options.LogPath);
        }

        log?.Invoke("create_process_start=true");
        Stopwatch launchStopwatch = Stopwatch.StartNew();
        bool success = NativeMethods.CreateProcess(
            options.GamePath,
            commandLine,
            IntPtr.Zero,
            IntPtr.Zero,
            false,
            NativeMethods.ProcessCreationFlags.CREATE_SUSPENDED,
            IntPtr.Zero,
            options.WorkingDirectory,
            ref startupInfo,
            out NativeMethods.PROCESS_INFORMATION processInfo);

        if (!success)
            throw new Win32Exception();

        log?.Invoke("create_process_success=true");
        log?.Invoke($"created_process_id={processInfo.dwProcessId}");
        log?.Invoke($"created_thread_id={processInfo.dwThreadId}");
        TryCapGameProcessAffinity(processInfo.hProcess, affinityMask, log);

        try
        {
            log?.Invoke("memory_patch_sequence_start=true");
            log?.Invoke($"assumed_image_base=0x{ImageBase:X8}");
            ApplyPatches(processInfo.hProcess, lobbyHost, log);
            log?.Invoke("memory_patch_sequence_complete=true");

            if (options.Umbra.Enabled)
            {
                log?.Invoke("umbra_injection_start=true");
                bool umbraInjected = UmbraInjector.TryInject(
                    processInfo.hProcess,
                    processInfo.dwProcessId,
                    options,
                    log);
                log?.Invoke($"umbra_injected={umbraInjected}");
            }

            log?.Invoke("resume_thread_start=true");
            uint resumeResult = NativeMethods.ResumeThread(processInfo.hThread);
            log?.Invoke($"resume_thread_result={resumeResult}");
            if (resumeResult == NativeMethods.ResumeThreadFailed)
                throw new Win32Exception();

            log?.Invoke("observation_wait_start=true");
            uint waitResult = NativeMethods.WaitForSingleObject(
                processInfo.hProcess,
                options.ObservationTimeoutMilliseconds);
            launchStopwatch.Stop();
            log?.Invoke($"observation_wait_result=0x{waitResult:X8}");
            log?.Invoke($"observation_elapsed_ms={launchStopwatch.ElapsedMilliseconds}");
            if (waitResult == NativeMethods.WaitObject0)
            {
                if (!NativeMethods.GetExitCodeProcess(processInfo.hProcess, out uint exitCode))
                    throw new Win32Exception();

                log?.Invoke($"observed_exit_code={exitCode}");
                log?.Invoke($"observed_exit_code_hex=0x{exitCode:X8}");
                return new ClientLaunchResult(
                    processInfo.dwProcessId,
                    processInfo.dwThreadId,
                    true,
                    exitCode);
            }

            if (waitResult == NativeMethods.WaitFailed)
                throw new Win32Exception();

            if (waitResult != NativeMethods.WaitTimeout)
                throw new InvalidOperationException($"Unexpected WaitForSingleObject result 0x{waitResult:X8}.");

            log?.Invoke("game_still_running_after_observation=true");
            return new ClientLaunchResult(
                processInfo.dwProcessId,
                processInfo.dwThreadId,
                false,
                null);
        }
        finally
        {
            if (processInfo.hThread != IntPtr.Zero)
                NativeMethods.CloseHandle(processInfo.hThread);
            if (processInfo.hProcess != IntPtr.Zero)
                NativeMethods.CloseHandle(processInfo.hProcess);
        }
    }

    private static string BuildGameCommandLine(
        LaunchOptions options,
        GameLaunchToken token,
        Action<string>? log)
    {
        string style = Environment.GetEnvironmentVariable("AETHERXIV_GAME_COMMAND_LINE_STYLE") ?? "";
        if (string.Equals(style, "path-plus-args", StringComparison.OrdinalIgnoreCase))
        {
            log?.Invoke("game_command_line_style=application_name_plus_launch_argument");
            return $"{CommandLineArguments.Quote(options.GamePath)}{token.LaunchArgument}";
        }

        log?.Invoke("game_command_line_style=legacy_launch_argument_only");
        return token.LaunchArgument;
    }

    private static UIntPtr? TryCapCurrentProcessAffinity(Action<string>? log)
    {
        try
        {
            IntPtr currentProcess = NativeMethods.GetCurrentProcess();
            if (!NativeMethods.GetProcessAffinityMask(
                    currentProcess,
                    out UIntPtr processMask,
                    out UIntPtr systemMask))
            {
                log?.Invoke($"current_affinity_probe_failed={new Win32Exception().Message}");
                return null;
            }

            log?.Invoke($"current_affinity_mask=0x{ToUInt64(processMask):X}");
            log?.Invoke($"system_affinity_mask=0x{ToUInt64(systemMask):X}");
            UIntPtr capped = CapAffinityMask(processMask, MaxVisibleLogicalProcessors);
            if (capped == processMask)
            {
                log?.Invoke("affinity_cap_applied=false");
                return null;
            }

            log?.Invoke($"affinity_cap_target=0x{ToUInt64(capped):X}");
            if (!NativeMethods.SetProcessAffinityMask(currentProcess, capped))
            {
                log?.Invoke($"current_affinity_cap_failed={new Win32Exception().Message}");
                return null;
            }

            log?.Invoke("current_affinity_cap_applied=true");
            return capped;
        }
        catch (Exception ex)
        {
            log?.Invoke($"current_affinity_cap_error={ex.Message}");
            return null;
        }
    }

    private static void TryCapGameProcessAffinity(
        IntPtr processHandle,
        UIntPtr? affinityMask,
        Action<string>? log)
    {
        if (affinityMask is not UIntPtr mask)
            return;

        if (NativeMethods.SetProcessAffinityMask(processHandle, mask))
        {
            log?.Invoke($"game_affinity_cap_applied=true");
            log?.Invoke($"game_affinity_mask=0x{ToUInt64(mask):X}");
        }
        else
        {
            log?.Invoke($"game_affinity_cap_failed={new Win32Exception().Message}");
        }
    }

    private static UIntPtr CapAffinityMask(UIntPtr mask, int maxProcessors)
    {
        ulong source = ToUInt64(mask);
        if (source == 0 || CountSetBits(source) <= maxProcessors)
            return mask;

        ulong capped = 0;
        int taken = 0;
        for (int bit = 0; bit < IntPtr.Size * 8; bit++)
        {
            ulong bitMask = 1UL << bit;
            if ((source & bitMask) == 0)
                continue;

            capped |= bitMask;
            taken++;
            if (taken == maxProcessors)
                break;
        }

        return new UIntPtr(capped);
    }

    private static int CountSetBits(ulong value)
    {
        int count = 0;
        while (value != 0)
        {
            value &= value - 1;
            count++;
        }

        return count;
    }

    private static ulong ToUInt64(UIntPtr value)
    {
        return UIntPtr.Size == 8 ? value.ToUInt64() : value.ToUInt32();
    }

    private static void ApplyPatches(IntPtr processHandle, string lobbyHost, Action<string>? log)
    {
        ApplyPatch(
            processHandle,
            "encryption_time",
            EncryptionTimePatchAddress,
            EncryptionTimePatchBytes,
            EncryptionTimeOriginalBytes,
            log);

        if ((uint)lobbyHost.Length + 1 > LobbyHostNamePatchSize)
            throw new InvalidOperationException("Lobby host name is too long for the 1.23b client patch location.");

        log?.Invoke($"lobby_host_patch_length={lobbyHost.Length + 1}");
        byte[] lobbyHostPatch = new byte[LobbyHostNamePatchSize];
        byte[] lobbyHostBytes = Encoding.ASCII.GetBytes(lobbyHost);
        Buffer.BlockCopy(lobbyHostBytes, 0, lobbyHostPatch, 0, lobbyHostBytes.Length);
        ApplyPatch(processHandle, "lobby_host", LobbyHostNameAddress, lobbyHostPatch, LobbyHostOriginalBytes, log);

        ApplyPatch(processHandle, "boot_skip", BootSkipPatchAddress, BootSkipPatchBytes, BootSkipOriginalBytes, log);
        ApplyPatch(processHandle, "launch_flag_1", LaunchFlag1PatchAddress, LaunchFlagPatchBytes, LaunchFlag1OriginalBytes, log);
        ApplyPatch(processHandle, "launch_flag_2", LaunchFlag2PatchAddress, LaunchFlagPatchBytes, LaunchFlag2OriginalBytes, log);
    }

    private static void ApplyPatch(
        IntPtr processHandle,
        string patchName,
        uint address,
        byte[] patchBytes,
        byte[] expectedOriginalBytes,
        Action<string>? log)
    {
        log?.Invoke($"patch_start={patchName}");
        log?.Invoke($"patch_address=0x{address:X8}");
        log?.Invoke($"patch_length={patchBytes.Length}");
        byte[] beforeBytes = ReadPatchBytes(processHandle, address, patchBytes.Length, patchName, "before", log);
        ValidatePatchSignature(patchName, beforeBytes, expectedOriginalBytes, patchBytes, log);
        log?.Invoke($"virtual_protect_start={patchName}");
        if (!NativeMethods.VirtualProtectEx(
            processHandle,
            (IntPtr)address,
            (uint)patchBytes.Length,
            (uint)NativeMethods.MemoryProtectionFlags.PAGE_READWRITE,
            out uint oldProtect))
        {
            throw new Win32Exception();
        }

        log?.Invoke($"virtual_protect_done={patchName}");
        log?.Invoke($"old_protection=0x{oldProtect:X8}");

        try
        {
            log?.Invoke($"write_process_memory_start={patchName}");
            if (!NativeMethods.WriteProcessMemory(
                processHandle,
                (IntPtr)address,
                patchBytes,
                (uint)patchBytes.Length,
                out int bytesWritten))
            {
                throw new Win32Exception();
            }

            log?.Invoke($"write_process_memory_done={patchName}");
            log?.Invoke($"bytes_written={bytesWritten}");
            log?.Invoke($"patch_expected_bytes={ToHex(patchBytes)}");

            if (bytesWritten != patchBytes.Length)
                throw new InvalidOperationException("Incomplete client memory patch write.");
        }
        finally
        {
            log?.Invoke($"virtual_protect_restore_start={patchName}");
            NativeMethods.VirtualProtectEx(
                processHandle,
                (IntPtr)address,
                (uint)patchBytes.Length,
                oldProtect,
                out _);
            log?.Invoke($"virtual_protect_restore_done={patchName}");
        }

        byte[] afterBytes = ReadPatchBytes(processHandle, address, patchBytes.Length, patchName, "after", log);
        log?.Invoke($"patch_changed={beforeBytes.Length == afterBytes.Length && !beforeBytes.SequenceEqual(afterBytes)}");
        log?.Invoke($"patch_done={patchName}");
    }

    private static byte[] ReadPatchBytes(
        IntPtr processHandle,
        uint address,
        int length,
        string patchName,
        string phase,
        Action<string>? log)
    {
        byte[] bytes = new byte[length];
        if (!NativeMethods.ReadProcessMemory(
                processHandle,
                (IntPtr)address,
                bytes,
                (uint)bytes.Length,
                out int bytesRead))
        {
            log?.Invoke($"patch_{phase}_read_failed={patchName}:{new Win32Exception().Message}");
            return [];
        }

        if (bytesRead != bytes.Length)
        {
            log?.Invoke($"patch_{phase}_read_short={patchName}:{bytesRead}/{bytes.Length}");
            bytes = bytes[..Math.Max(0, bytesRead)];
        }

        log?.Invoke($"patch_{phase}_bytes={patchName}:{ToHex(bytes)}");
        return bytes;
    }

    private static void ValidatePatchSignature(
        string patchName,
        byte[] actualBytes,
        byte[] expectedOriginalBytes,
        byte[] patchBytes,
        Action<string>? log)
    {
        if (actualBytes.SequenceEqual(expectedOriginalBytes))
        {
            log?.Invoke($"patch_signature={patchName}:original");
            return;
        }

        if (actualBytes.SequenceEqual(patchBytes))
        {
            log?.Invoke($"patch_signature={patchName}:already_patched");
            return;
        }

        log?.Invoke($"patch_signature={patchName}:unexpected");
        log?.Invoke($"patch_expected_original_bytes={patchName}:{ToHex(expectedOriginalBytes)}");
        throw new InvalidOperationException(
            $"Unexpected client bytes at {patchName} patch location. "
            + "Verify this is the supported FFXIV 1.23b client executable.");
    }

    private static string ToHex(byte[] bytes)
    {
        if (bytes.Length == 0)
            return "";

        StringBuilder builder = new(bytes.Length * 2);
        foreach (byte value in bytes)
            builder.Append(value.ToString("X2"));

        return builder.ToString();
    }
}

internal sealed record ClientLaunchResult(
    uint ProcessId,
    uint ThreadId,
    bool ExitedDuringObservation,
    uint? ExitCode);
