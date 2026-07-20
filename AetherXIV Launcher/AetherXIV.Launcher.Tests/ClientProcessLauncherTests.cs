using System.Runtime.InteropServices;
using AetherXIV.Launcher.ClientLauncher;

namespace AetherXIV.Launcher.Tests;

public sealed class ClientProcessLauncherTests
{
    [Fact]
    public void X86ThreadContextMatchesWindowsAbi()
    {
        Assert.Equal(716, Marshal.SizeOf<NativeMethods.WOW64_CONTEXT>());
    }

    [Theory]
    [InlineData(0x00400000u, 0x009A15E3u, 0x00DA15E3u)]
    [InlineData(0x00600000u, 0x009A15E3u, 0x00FA15E3u)]
    [InlineData(0x00600000u, 0x00003698u, 0x00603698u)]
    public void PatchAddressUsesLoadedImageBase(uint imageBase, uint rva, uint expected)
    {
        Assert.Equal(expected, ClientProcessLauncher.ResolvePatchAddress(imageBase, rva));
    }
}
