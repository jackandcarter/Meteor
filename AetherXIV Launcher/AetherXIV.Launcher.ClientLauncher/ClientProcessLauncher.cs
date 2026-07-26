using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using AetherXIV.Launcher.Core;

namespace AetherXIV.Launcher.ClientLauncher;

internal static class ClientProcessLauncher
{
    private const int MaxVisibleLogicalProcessors = 15;
    private const uint EncryptionTimePatchRva = 0x9A15E3;
    private const uint LobbyHostNameRva = 0xB90110;
    private const uint LobbyHostNamePatchSize = 0x14;
    private const ushort DosSignature = 0x5A4D;
    private const uint PeSignature = 0x00004550;
    private const ushort Pe32Magic = 0x010B;
    private const ushort ImageFileRelocationsStripped = 0x0001;

    private static readonly byte[] EncryptionTimePatchBytes = [0xB8, 0x12, 0xE8, 0xE0, 0x50];

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

        bool useLegacyNativePath = !Environment.Is64BitProcess;
        string commandLine = useLegacyNativePath
            ? BuildLegacyNativeCommandLine(options.GamePath, token)
            : BuildWineCommandLine(options.GamePath, token);
        log?.Invoke(useLegacyNativePath
            ? "game_command_line_style=legacy_native_path_plus_launch_argument"
            : "game_command_line_style=wine_quoted_path_plus_launch_argument");
        log?.Invoke($"game_command_line_length={commandLine.Length}");
        if (options.Umbra.Enabled)
        {
            UmbraInjector.SetUmbraEnvironment(options.Umbra);
            Environment.SetEnvironmentVariable("AETHER_UMBRA_HELPER_LOG", options.LogPath);
        }

        log?.Invoke("create_process_start=true");
        Stopwatch launchStopwatch = Stopwatch.StartNew();
        StringBuilder mutableCommandLine = new(commandLine, 1024);
        NativeMethods.ProcessCreationFlags creationFlags =
            NativeMethods.ProcessCreationFlags.CREATE_SUSPENDED
            | NativeMethods.ProcessCreationFlags.NORMAL_PRIORITY_CLASS;
        bool success;
        NativeMethods.PROCESS_INFORMATION processInfo;
        if (useLegacyNativePath)
        {
            log?.Invoke("create_process_api=CreateProcessA");
            success = NativeMethods.CreateProcessA(
                null,
                mutableCommandLine,
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                creationFlags,
                IntPtr.Zero,
                options.WorkingDirectory,
                ref startupInfo,
                out processInfo);
        }
        else
        {
            log?.Invoke("create_process_api=CreateProcessW");
            success = NativeMethods.CreateProcessW(
                null,
                mutableCommandLine,
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                creationFlags,
                IntPtr.Zero,
                options.WorkingDirectory,
                ref startupInfo,
                out processInfo);
        }

        if (!success)
            throw new Win32Exception();

        log?.Invoke("create_process_success=true");
        log?.Invoke($"created_process_id={processInfo.dwProcessId}");
        log?.Invoke($"created_thread_id={processInfo.dwThreadId}");
        TryCapGameProcessAffinity(processInfo.hProcess, affinityMask, log);

        bool resumed = false;
        try
        {
            log?.Invoke("memory_patch_sequence_start=true");
            PeImageLayout imageLayout = ReadPeImageLayout(options.GamePath);
            uint imageBase = SelectSupportedImageBase(imageLayout);
            log?.Invoke($"pe_preferred_image_base=0x{imageLayout.PreferredImageBase:X8}");
            log?.Invoke($"pe_size_of_image=0x{imageLayout.SizeOfImage:X8}");
            log?.Invoke($"pe_relocations_stripped={imageLayout.RelocationsStripped.ToString().ToLowerInvariant()}");
            log?.Invoke(
                $"pe_base_relocation_table=0x{imageLayout.BaseRelocationTableRva:X8}:0x{imageLayout.BaseRelocationTableSize:X8}");
            log?.Invoke("image_base_source=pe_fixed_preferred");
            log?.Invoke($"loaded_image_base=0x{imageBase:X8}");
            ApplyPatches(processInfo.hProcess, imageBase, lobbyHost, log);
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
            resumed = true;

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
        catch
        {
            if (!resumed)
            {
                log?.Invoke("terminate_suspended_process_start=true");
                bool terminated = NativeMethods.TerminateProcess(processInfo.hProcess, 1);
                log?.Invoke($"terminate_suspended_process_success={terminated}");
                if (!terminated)
                    log?.Invoke($"terminate_suspended_process_error={new Win32Exception().Message}");
            }

            throw;
        }
        finally
        {
            if (processInfo.hThread != IntPtr.Zero)
                NativeMethods.CloseHandle(processInfo.hThread);
            if (processInfo.hProcess != IntPtr.Zero)
                NativeMethods.CloseHandle(processInfo.hProcess);
        }
    }

    internal static string BuildLegacyNativeCommandLine(string gamePath, GameLaunchToken token) =>
        $"{gamePath}{token.LaunchArgument}";

    internal static string BuildWineCommandLine(string gamePath, GameLaunchToken token) =>
        $"{CommandLineArguments.Quote(gamePath)}{token.LaunchArgument}";

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

    internal static PeImageLayout ReadPeImageLayout(string gamePath)
    {
        using FileStream stream = File.OpenRead(gamePath);
        using BinaryReader reader = new(stream, Encoding.ASCII, leaveOpen: false);

        if (stream.Length < 0x40)
            throw new InvalidOperationException("Client executable is too small to contain a PE header.");
        if (reader.ReadUInt16() != DosSignature)
            throw new InvalidOperationException("Client executable has an invalid DOS header.");

        stream.Position = 0x3C;
        int peHeaderOffset = reader.ReadInt32();
        const int minimumPeHeaderSize = 24 + 140;
        if (peHeaderOffset < 0 || peHeaderOffset > stream.Length - minimumPeHeaderSize)
            throw new InvalidOperationException("Client executable has an invalid PE header offset.");

        stream.Position = peHeaderOffset;
        if (reader.ReadUInt32() != PeSignature)
            throw new InvalidOperationException("Client executable has an invalid PE signature.");

        stream.Position = peHeaderOffset + 20;
        ushort optionalHeaderSize = reader.ReadUInt16();
        ushort characteristics = reader.ReadUInt16();
        if (optionalHeaderSize < 140)
            throw new InvalidOperationException("Client executable has an incomplete PE32 optional header.");

        long optionalHeaderOffset = peHeaderOffset + 24L;
        stream.Position = optionalHeaderOffset;
        if (reader.ReadUInt16() != Pe32Magic)
            throw new InvalidOperationException("Client executable is not a supported PE32 image.");

        stream.Position = optionalHeaderOffset + 28;
        uint preferredImageBase = reader.ReadUInt32();
        stream.Position = optionalHeaderOffset + 56;
        uint sizeOfImage = reader.ReadUInt32();
        stream.Position = optionalHeaderOffset + 92;
        uint numberOfDataDirectories = reader.ReadUInt32();

        uint relocationTableRva = 0;
        uint relocationTableSize = 0;
        if (numberOfDataDirectories > 5)
        {
            stream.Position = optionalHeaderOffset + 96 + (5 * 8);
            relocationTableRva = reader.ReadUInt32();
            relocationTableSize = reader.ReadUInt32();
        }

        return new PeImageLayout(
            preferredImageBase,
            sizeOfImage,
            (characteristics & ImageFileRelocationsStripped) != 0,
            relocationTableRva,
            relocationTableSize);
    }

    internal static uint SelectSupportedImageBase(PeImageLayout imageLayout)
    {
        if (!imageLayout.IsFixedAddress)
        {
            throw new InvalidOperationException(
                "The supported client executable must use a fixed image base.");
        }

        if (imageLayout.PreferredImageBase == 0 || imageLayout.SizeOfImage == 0)
            throw new InvalidOperationException("Client executable has an invalid fixed image layout.");

        uint encryptionPatchEnd = checked(EncryptionTimePatchRva + (uint)EncryptionTimePatchBytes.Length);
        uint lobbyPatchEnd = checked(LobbyHostNameRva + LobbyHostNamePatchSize);
        if (encryptionPatchEnd > imageLayout.SizeOfImage || lobbyPatchEnd > imageLayout.SizeOfImage)
        {
            throw new InvalidOperationException(
                "Client executable does not contain the required patch locations.");
        }

        return imageLayout.PreferredImageBase;
    }

    internal static uint ResolvePatchAddress(uint imageBase, uint relativeVirtualAddress) =>
        checked(imageBase + relativeVirtualAddress);

    private static void ApplyPatches(IntPtr processHandle, uint imageBase, string lobbyHost, Action<string>? log)
    {
        ApplyPatch(
            processHandle,
            "encryption_time",
            ResolvePatchAddress(imageBase, EncryptionTimePatchRva),
            EncryptionTimePatchBytes,
            log);

        if ((uint)lobbyHost.Length + 1 > LobbyHostNamePatchSize)
            throw new InvalidOperationException("Lobby host name is too long for the 1.23b client patch location.");

        log?.Invoke($"lobby_host_patch_length={lobbyHost.Length + 1}");
        byte[] lobbyHostBytes = Encoding.ASCII.GetBytes(lobbyHost);
        byte[] lobbyHostPatch = new byte[lobbyHostBytes.Length + 1];
        Buffer.BlockCopy(lobbyHostBytes, 0, lobbyHostPatch, 0, lobbyHostBytes.Length);
        ApplyPatch(processHandle, "lobby_host", ResolvePatchAddress(imageBase, LobbyHostNameRva), lobbyHostPatch, log);
    }

    private static void ApplyPatch(
        IntPtr processHandle,
        string patchName,
        uint address,
        byte[] patchBytes,
        Action<string>? log)
    {
        log?.Invoke($"patch_start={patchName}");
        log?.Invoke($"patch_address=0x{address:X8}");
        log?.Invoke($"patch_length={patchBytes.Length}");
        log?.Invoke($"virtual_protect_start={patchName}");
        if (!NativeMethods.VirtualProtectEx(
                processHandle,
                (IntPtr)(long)address,
                (nuint)patchBytes.Length,
                (uint)NativeMethods.MemoryProtectionFlags.PAGE_READWRITE,
                out uint oldProtect))
        {
            throw new Win32Exception();
        }

        log?.Invoke($"virtual_protect_done={patchName}");
        log?.Invoke("writable_protection=0x00000004");
        log?.Invoke($"old_protection=0x{oldProtect:X8}");

        log?.Invoke($"write_process_memory_start={patchName}");
        if (!NativeMethods.WriteProcessMemory(
            processHandle,
            (IntPtr)(long)address,
            patchBytes,
            (nuint)patchBytes.Length,
            out nuint nativeBytesWritten))
        {
            throw new Win32Exception();
        }

        int bytesWritten = checked((int)nativeBytesWritten);
        log?.Invoke($"write_process_memory_done={patchName}");
        log?.Invoke($"bytes_written={bytesWritten}");
        if (bytesWritten != patchBytes.Length)
            throw new InvalidOperationException("Incomplete client memory patch write.");

        log?.Invoke($"virtual_protect_restore_start={patchName}");
        if (!NativeMethods.VirtualProtectEx(
                processHandle,
                (IntPtr)(long)address,
                (nuint)patchBytes.Length,
                oldProtect,
                out _))
        {
            throw new Win32Exception();
        }

        log?.Invoke($"virtual_protect_restore_done={patchName}");
        log?.Invoke($"patch_done={patchName}");
    }
}

internal readonly record struct PeImageLayout(
    uint PreferredImageBase,
    uint SizeOfImage,
    bool RelocationsStripped,
    uint BaseRelocationTableRva,
    uint BaseRelocationTableSize)
{
    internal bool IsFixedAddress =>
        RelocationsStripped
        || BaseRelocationTableRva == 0
        || BaseRelocationTableSize == 0;
}

internal sealed record ClientLaunchResult(
    uint ProcessId,
    uint ThreadId,
    bool ExitedDuringObservation,
    uint? ExitCode);
