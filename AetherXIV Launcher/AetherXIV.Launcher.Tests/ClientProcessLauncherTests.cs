using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using AetherXIV.Launcher.ClientLauncher;
using AetherXIV.Launcher.Core;

namespace AetherXIV.Launcher.Tests;

public sealed class ClientProcessLauncherTests
{
    [Fact]
    public void X86ThreadContextMatchesWindowsAbi()
    {
        Assert.Equal(716, Marshal.SizeOf<NativeMethods.WOW64_CONTEXT>());
    }

    [Theory]
    [InlineData("ReadProcessMemory", 3, 4)]
    [InlineData("WriteProcessMemory", 3, 4)]
    public void ProcessMemoryInteropUsesPointerSizedLengths(
        string methodName,
        int sizeParameterIndex,
        int countParameterIndex)
    {
        System.Reflection.MethodInfo? method = typeof(NativeMethods).GetMethod(
            methodName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        System.Reflection.ParameterInfo[] parameters = method.GetParameters();

        Assert.Equal(typeof(UIntPtr), parameters[sizeParameterIndex].ParameterType);
        Assert.Equal(typeof(UIntPtr).MakeByRefType(), parameters[countParameterIndex].ParameterType);
    }

    [Theory]
    [InlineData(0x00400000u, 0x009A15E3u, 0x00DA15E3u)]
    [InlineData(0x00600000u, 0x009A15E3u, 0x00FA15E3u)]
    public void PatchAddressUsesLoadedImageBase(uint imageBase, uint rva, uint expected)
    {
        Assert.Equal(expected, ClientProcessLauncher.ResolvePatchAddress(imageBase, rva));
    }

    [Fact]
    public void LegacyNativeCommandLineUsesGamePathAndLaunchArgumentVerbatim()
    {
        GameLaunchToken token = new("test-token", 123);

        Assert.Equal(
            @"D:\Games\FINAL FANTASY XIV\ffxivgame.exe sqex0002test-token!////",
            ClientProcessLauncher.BuildLegacyNativeCommandLine(
                @"D:\Games\FINAL FANTASY XIV\ffxivgame.exe",
                token));
        Assert.Equal(
            @"""D:\Games\FINAL FANTASY XIV\ffxivgame.exe"" sqex0002test-token!////",
            ClientProcessLauncher.BuildWineCommandLine(
                @"D:\Games\FINAL FANTASY XIV\ffxivgame.exe",
                token));
    }

    [Fact]
    public void CreatedProcessPathComparisonAcceptsWindowsExtendedPrefixAndCase()
    {
        Assert.True(ClientProcessLauncher.PathsReferToSameLocation(
            @"D:\Games\FINAL FANTASY XIV\ffxivgame.exe",
            @"\\?\d:\games\final fantasy xiv\FFXIVGAME.EXE"));
    }

    [Fact]
    public void NativePatchInteropMatchesLegacyClientSurface()
    {
        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;

        Assert.NotNull(typeof(NativeMethods).GetMethod("GetThreadContext", flags));
        Assert.NotNull(typeof(NativeMethods).GetMethod("CreateProcessA", flags));
        Assert.NotNull(typeof(NativeMethods).GetMethod("CreateProcessW", flags));
        Assert.NotNull(typeof(NativeMethods).GetMethod("QueryFullProcessImageName", flags));
        Assert.NotNull(typeof(NativeMethods).GetMethod("ReadProcessMemory", flags));
        Assert.NotNull(typeof(NativeMethods).GetMethod("VirtualProtectEx", flags));
        Assert.NotNull(typeof(NativeMethods).GetMethod("WriteProcessMemory", flags));
        Assert.Null(typeof(NativeMethods).GetMethod("VirtualQueryEx", flags));
        Assert.Null(typeof(NativeMethods).GetMethod("FlushInstructionCache", flags));
        Assert.Equal(0x04u, (uint)NativeMethods.MemoryProtectionFlags.PAGE_READWRITE);
    }

    [Fact]
    public void ExplicitApplicationNamePreventsSpaceDelimitedExecutableSubstitutionOnWindows()
    {
        if (!OperatingSystem.IsWindows())
            return;

        string testRoot = Path.Combine(
            Path.GetTempPath(),
            $"AetherXivProcessTarget-{Guid.NewGuid():N}");
        string targetDirectory = Path.Combine(testRoot, "Client Path");
        string targetPath = Path.Combine(targetDirectory, "Target.exe");
        string decoyPath = Path.Combine(testRoot, "Client.exe");
        Directory.CreateDirectory(targetDirectory);
        File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), targetPath);
        File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), decoyPath);

        try
        {
            string ambiguousCommandLine = $"{targetPath} /c exit 0";
            string ambiguousImage = CreateSuspendedAndGetImagePath(null, ambiguousCommandLine, testRoot);
            Assert.True(ClientProcessLauncher.PathsReferToSameLocation(decoyPath, ambiguousImage));

            string explicitImage = CreateSuspendedAndGetImagePath(
                targetPath,
                ambiguousCommandLine,
                testRoot);
            Assert.True(ClientProcessLauncher.PathsReferToSameLocation(targetPath, explicitImage));
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private static string CreateSuspendedAndGetImagePath(
        string? applicationName,
        string commandLine,
        string workingDirectory)
    {
        NativeMethods.STARTUPINFO startupInfo = new()
        {
            cb = (uint)Marshal.SizeOf<NativeMethods.STARTUPINFO>()
        };
        StringBuilder mutableCommandLine = new(commandLine, 1024);
        if (!NativeMethods.CreateProcessA(
                applicationName,
                mutableCommandLine,
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                NativeMethods.ProcessCreationFlags.CREATE_SUSPENDED,
                IntPtr.Zero,
                workingDirectory,
                ref startupInfo,
                out NativeMethods.PROCESS_INFORMATION processInfo))
        {
            throw new Win32Exception();
        }

        try
        {
            StringBuilder imagePath = new(32768);
            uint imagePathLength = (uint)imagePath.Capacity;
            if (!NativeMethods.QueryFullProcessImageName(
                    processInfo.hProcess,
                    0,
                    imagePath,
                    ref imagePathLength))
            {
                throw new Win32Exception();
            }

            return imagePath.ToString();
        }
        finally
        {
            _ = NativeMethods.TerminateProcess(processInfo.hProcess, 0);
            _ = NativeMethods.CloseHandle(processInfo.hThread);
            _ = NativeMethods.CloseHandle(processInfo.hProcess);
        }
    }
}
