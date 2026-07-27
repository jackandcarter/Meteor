using AetherXIV.Launcher.Core;

namespace AetherXIV.Launcher.Tests;

public sealed class WindowsFfxivLaunchRedirectTests
{
    [Theory]
    [InlineData(@"D:\Downloads\NovumLauncher\NovumLauncher.exe")]
    [InlineData(@"""C:\Program Files\Novum\NovumLauncher.exe"" --intercept")]
    [InlineData("NovumLauncher.exe")]
    [InlineData(@"%USERPROFILE%\Downloads\NovumLauncher\NOVUMLAUNCHER.EXE")]
    public void RecognizesNovumDebuggerCommands(string debuggerCommand)
    {
        Assert.True(WindowsFfxivLaunchRedirects.IsNovumDebuggerCommand(debuggerCommand));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"D:\Downloads\NotNovumLauncher.exe")]
    [InlineData(@"D:\Downloads\OtherLauncher\Launcher.exe")]
    public void RejectsUnrelatedDebuggerCommands(string? debuggerCommand)
    {
        Assert.False(WindowsFfxivLaunchRedirects.IsNovumDebuggerCommand(debuggerCommand));
    }

    [Fact]
    public void RepairTargetsOnlyLegacyFfxivExecutables()
    {
        Assert.Equal(
            ["ffxivboot.exe", "ffxivlogin.exe", "ffxivgame.exe"],
            WindowsFfxivLaunchRedirects.FfxivExecutableNames);
    }
}
