using AetherXIV.Core.Common;
using AetherXIV.Core.Map.actors.chara.ai;
using AetherXIV.Core.Map.lua;
using AetherXIV.Core.Map.packets.receive;

namespace AetherXIV.Core.Map.Tests;

public sealed class LegacyWarningRegressionTests
{
    [Fact]
    public void NewActionQueueReportsEmpty()
    {
        Assert.True(new ActionQueue(null!).IsEmpty);
    }

    [Fact]
    public void CommonBlowfishPreservesLegacySignedKeyScheduleVector()
    {
        byte[] key = Convert.FromHexString("B4EE3F6C016F5BD971500DB185A2AB43");
        byte[] plain = Convert.FromHexString(
            "0C6900E000000000D0ED450200000000" +
            "C0EDDFFFAFF7F7AF10EFDFFF7FFDFFFF" +
            "428263520100000010EFDFFF53616D70" +
            "6C652053616D706C652052756E52756E" +
            "0000000000000000");
        byte[] expectedCipher = Convert.FromHexString(
            "03FFF6CD28A310F39484E920A6ACA740" +
            "9AAB875CD3B98894ADAAF6D0CE262E73" +
            "FDEBB73A85B2215E6CEAC9309D737D2F" +
            "01A5A5034B8F4E623F9DFABB1FD09B6F" +
            "0410D0F11267315E");

        byte[] encrypted = plain.ToArray();
        Blowfish cipher = new(key);
        cipher.Encipher(encrypted, 0, encrypted.Length);

        Assert.Equal(expectedCipher, encrypted);

        cipher.Decipher(encrypted, 0, encrypted.Length);
        Assert.Equal(plain, encrypted);
    }

    [Fact]
    public void StatusEffectUsesItsLoadedScriptAndHandlesMissingFunctions()
    {
        StatusEffect effect = new(223001, "test", 0, 0, 0, false, false, false, 0, 0)
        {
            script = new LuaScript()
        };
        effect.script.DoString("function result(value) return value + 1 end");

        Assert.Equal(42, effect.CallLuaFunction("result", 41));
        Assert.Equal(-1, effect.CallLuaFunction("missing"));
    }

    [Fact]
    public void TruncatedClientPacketsAreMarkedInvalid()
    {
        Assert.True(new PingPacket(new byte[3]).invalidPacket);
        Assert.True(new UpdatePlayerPositionPacket(new byte[25]).invalidPacket);
    }
}
