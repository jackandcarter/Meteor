using System.Text.Json;
using AetherXIV.Protocol;

namespace AetherXIV.Protocol.Tests;

public sealed class ChocoboTraceFixtureTests
{
    [Fact]
    public void OfficialMountFixturePinsAppearanceSpeedMusicAndPacketOrder()
    {
        using JsonDocument document = LoadFixture();
        JsonElement root = document.RootElement;
        JsonElement mount = root.GetProperty("mount");
        uint actorId = 0x029B2941;

        Assert.Equal("mount_unmount_chocobo.pcapng", root.GetProperty("capture").GetString());
        Assert.Equal(64, root.GetProperty("captureSha256").GetString()!.Length);
        Assert.Equal(
            ["0x000C", "0x0197", "0x013C", "0x00D0", "0x00D0", "0x00D0", "0x0157", "0x0139", "0x0131"],
            mount.GetProperty("opcodeOrder").EnumerateArray().Select(item => item.GetString() ?? String.Empty).ToArray());

        SetMusicPacket music = new SetMusicPacketCodec().Decode(SubPacket.Create(
            PacketOpcode.SetMusic, actorId, Convert.FromHexString(mount.GetProperty("musicPayloadHex").GetString()!)));
        SetCurrentMountChocoboPacket appearance = new SetCurrentMountChocoboPacketCodec().Decode(SubPacket.Create(
            PacketOpcode.SetCurrentMountChocobo, actorId, Convert.FromHexString(mount.GetProperty("appearancePayloadHex").GetString()!)));
        SetActorSpeedPacket speed = new SetActorSpeedPacketCodec().Decode(SubPacket.Create(
            PacketOpcode.SetActorSpeed, actorId, Convert.FromHexString(mount.GetProperty("speedPayloadHex").GetString()!)));

        Assert.Equal((ushort)83, music.MusicId);
        Assert.Equal(SetMusicPacketCodec.EffectFadeIn, music.TrackMode);
        Assert.Equal(0u, appearance.RentalExpiresAt);
        Assert.Equal(0, appearance.RentalMinutesLeft);
        Assert.Equal(0x1F, appearance.AppearanceId);
        Assert.Equal((0.0f, 3.6f, 9.0f, 9.0f), (speed.Stop, speed.Walk, speed.Run, speed.Active));
    }

    [Fact]
    public void OfficialDismountFixturePinsPassiveStateAndDefaultSpeed()
    {
        using JsonDocument document = LoadFixture();
        JsonElement dismount = document.RootElement.GetProperty("dismount");
        uint actorId = 0x029B2941;
        Assert.Equal(
            ["0x000C", "0x0134", "0x013C", "0x00D0", "0x00D0", "0x00D0", "0x0157", "0x0139"],
            dismount.GetProperty("opcodeOrder").EnumerateArray().Select(item => item.GetString() ?? String.Empty).ToArray());

        SetActorStatePacket state = new SetActorStatePacketCodec().Decode(SubPacket.Create(
            PacketOpcode.SetActorState, actorId, Convert.FromHexString(dismount.GetProperty("statePayloadHex").GetString()!)));
        SetActorSpeedPacket speed = new SetActorSpeedPacketCodec().Decode(SubPacket.Create(
            PacketOpcode.SetActorSpeed, actorId, Convert.FromHexString(dismount.GetProperty("speedPayloadHex").GetString()!)));
        SetMusicPacket music = new SetMusicPacketCodec().Decode(SubPacket.Create(
            PacketOpcode.SetMusic, actorId, Convert.FromHexString(dismount.GetProperty("musicPayloadHex").GetString()!)));

        Assert.Equal((ushort)57, music.MusicId);
        Assert.Equal(SetMusicPacketCodec.EffectFadeIn, music.TrackMode);
        Assert.Equal(0u, state.MainState);
        Assert.Equal(0xBFu, state.SubState);
        Assert.Equal(SetActorSpeedPacket.LegacyDefault, speed);
    }

    private static JsonDocument LoadFixture()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "tests", "fixtures", "trace-evidence", "world-mount-unmount-chocobo-observed.json");
            if (File.Exists(candidate))
                return JsonDocument.Parse(File.ReadAllBytes(candidate));
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Could not locate chocobo trace fixture.");
    }
}
