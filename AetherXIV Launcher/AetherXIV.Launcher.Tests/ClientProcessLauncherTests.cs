using System.Buffers.Binary;
using AetherXIV.Launcher.ClientLauncher;
using AetherXIV.Launcher.Core;

namespace AetherXIV.Launcher.Tests;

public sealed class ClientProcessLauncherTests
{
    [Fact]
    public void WriteProcessMemoryInteropUsesPointerSizedLengths()
    {
        System.Reflection.MethodInfo? method = typeof(NativeMethods).GetMethod(
            "WriteProcessMemory",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        System.Reflection.ParameterInfo[] parameters = method.GetParameters();

        Assert.Equal(typeof(UIntPtr), parameters[3].ParameterType);
        Assert.Equal(typeof(UIntPtr).MakeByRefType(), parameters[4].ParameterType);
    }

    [Theory]
    [InlineData(0x00400000u, 0x009A15E3u, 0x00DA15E3u)]
    [InlineData(0x00600000u, 0x009A15E3u, 0x00FA15E3u)]
    public void PatchAddressUsesLoadedImageBase(uint imageBase, uint rva, uint expected)
    {
        Assert.Equal(expected, ClientProcessLauncher.ResolvePatchAddress(imageBase, rva));
    }

    [Fact]
    public void SupportedClientPeLayoutUsesItsFixedPreferredBase()
    {
        byte[] image = new byte[0x200];
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(0x00), 0x5A4D);
        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(0x3C), 0x80);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x80), 0x00004550);
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(0x80 + 20), 0x00E0);
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(0x80 + 22), 0x0103);
        int optionalHeader = 0x80 + 24;
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(optionalHeader), 0x010B);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(optionalHeader + 28), 0x00400000);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(optionalHeader + 56), 0x00F99000);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(optionalHeader + 92), 16);

        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.exe");
        try
        {
            File.WriteAllBytes(path, image);
            PeImageLayout layout = ClientProcessLauncher.ReadPeImageLayout(path);

            Assert.Equal(0x00400000u, layout.PreferredImageBase);
            Assert.Equal(0x00F99000u, layout.SizeOfImage);
            Assert.True(layout.RelocationsStripped);
            Assert.True(layout.IsFixedAddress);
            Assert.Equal(0x00400000u, ClientProcessLauncher.SelectSupportedImageBase(layout));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RelocatableClientLayoutIsRejectedInsteadOfGuessingAnImageBase()
    {
        PeImageLayout layout = new(
            PreferredImageBase: 0x00400000,
            SizeOfImage: 0x00F99000,
            RelocationsStripped: false,
            BaseRelocationTableRva: 0x00F00000,
            BaseRelocationTableSize: 0x1000);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => ClientProcessLauncher.SelectSupportedImageBase(layout));
        Assert.Contains("fixed image base", error.Message, StringComparison.Ordinal);
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
    public void NativePatchInteropMatchesLegacyClientSurface()
    {
        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;

        Assert.NotNull(typeof(NativeMethods).GetMethod("CreateProcessA", flags));
        Assert.NotNull(typeof(NativeMethods).GetMethod("CreateProcessW", flags));
        Assert.NotNull(typeof(NativeMethods).GetMethod("VirtualProtectEx", flags));
        Assert.NotNull(typeof(NativeMethods).GetMethod("WriteProcessMemory", flags));
        Assert.Null(typeof(NativeMethods).GetMethod("GetThreadContext", flags));
        Assert.Null(typeof(NativeMethods).GetMethod("Wow64GetThreadContext", flags));
        Assert.Null(typeof(NativeMethods).GetMethod("ReadProcessMemory", flags));
        Assert.Null(typeof(NativeMethods).GetMethod("VirtualQueryEx", flags));
        Assert.Null(typeof(NativeMethods).GetMethod("FlushInstructionCache", flags));
        Assert.Equal(0x04u, (uint)NativeMethods.MemoryProtectionFlags.PAGE_READWRITE);
    }
}
