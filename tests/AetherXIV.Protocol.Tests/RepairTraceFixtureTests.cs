using System.Text.Json;
using AetherXIV.Protocol;

namespace AetherXIV.Protocol.Tests;

public sealed class RepairTraceFixtureTests
{
    [Fact]
    public void OfficialRepairFixturePinsDialogueAndTransactionPacketSequence()
    {
        using JsonDocument document = LoadFixture();
        JsonElement root = document.RootElement;

        Assert.Equal("repair_items.pcapng", root.GetProperty("capture").GetString());
        Assert.Equal(64, root.GetProperty("captureSha256").GetString()!.Length);
        Assert.Equal(
            [
                "talkWelcome",
                "selectItem", "selectItem", "selectItem", "selectItem",
                "confirmRepairItem", "confirmRepairItem", "confirmRepairItem",
                "confirmRepairItem", "confirmRepairItem", "confirmRepairItem",
                "talkWelcome",
                "finishTalkTurn"
            ],
            root.GetProperty("serverCallSequence")
                .EnumerateArray()
                .Select(item => item.GetString() ?? String.Empty)
                .ToArray());
        Assert.Equal(6, root.GetProperty("transactionCount").GetInt32());
        JsonElement selection = root.GetProperty("selectionComplete");
        Assert.Equal(-1, selection.GetProperty("slot").GetInt32());
        Assert.Equal(2, selection.GetProperty("listIndex").GetInt32());
        Assert.Equal(
            ["0x0148", "0x0169"],
            root.GetProperty("transactionPacketOrder")
                .EnumerateArray()
                .Select(item => item.GetString() ?? String.Empty)
                .ToArray());
    }

    [Fact]
    public void OfficialRepairResultMessageDecodesWithItemQualityAndRestoredPercent()
    {
        JsonElement observed;
        using (JsonDocument document = LoadFixture())
            observed = document.RootElement.GetProperty("repairResultMessage").Clone();

        GameMessageWithoutActorPacket packet = new GameMessageWithoutActorPacketCodec().Decode(
            SubPacket.Create(
                PacketOpcode.GameMessageWithoutActorX04,
                0x5FF80001,
                Convert.FromHexString(observed.GetProperty("payloadHex").GetString()!)));

        Assert.Equal(observed.GetProperty("textOwnerActorId").GetUInt32(), packet.TextOwnerActorId);
        Assert.Equal(observed.GetProperty("textId").GetUInt16(), packet.TextId);
        Assert.Equal(observed.GetProperty("logType").GetByte(), packet.LogType);
        Assert.Equal(
            [
                new LuaParameter(LuaParameterType.Int32, observed.GetProperty("itemId").GetInt32()),
                new LuaParameter(LuaParameterType.Int32, observed.GetProperty("quality").GetInt32()),
                new LuaParameter(LuaParameterType.Int32, observed.GetProperty("restoredPercent").GetInt32())
            ],
            packet.Parameters);
    }

    private static JsonDocument LoadFixture()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "tests",
                "fixtures",
                "trace-evidence",
                "world-repair-items-observed.json");
            if (File.Exists(candidate))
                return JsonDocument.Parse(File.ReadAllBytes(candidate));
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Could not locate repair trace fixture.");
    }
}
