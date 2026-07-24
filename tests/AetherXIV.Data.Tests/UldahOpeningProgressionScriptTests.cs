namespace AetherXIV.Data.Tests;

public sealed class UldahOpeningProgressionScriptTests
{
    [Fact]
    public void QuicksandExitDoesNotCreateAnOwnerlessLinkpearlAlert()
    {
        string script = ReadDataScript(
            "unique", "wil0Town01", "PrivateArea", "PrivateAreaMasterPast_3",
            "PopulaceStandard", "uldah_opening_exit.lua");

        Assert.Contains("player:ReplaceQuest(110009, 110010)", script, StringComparison.Ordinal);
        Assert.DoesNotContain("SetNpcLS", script, StringComparison.Ordinal);
        Assert.DoesNotContain("NewNpcLsMsg", script, StringComparison.Ordinal);
        AssertOrdered(
            script,
            "\"processEventMomodiStart\"",
            "player:ReplaceQuest(110009, 110010)",
            "player:EndEvent()",
            "DoZoneChange(player, 175, \"PrivateAreaMasterPast\", 4");
    }

    [Fact]
    public void MomodiGrantsThePearlAndStartsTheCampLeg()
    {
        string script = ReadDataScript(
            "unique", "wil0Town01", "PrivateArea", "PrivateAreaMasterPast_4",
            "PopulaceStandard", "momodi.lua");

        AssertOrdered(
            script,
            "man0u1Quest:GetSequence() == 0",
            "\"processEvent010\"",
            "man0u1Quest:NewNpcLsMsg(1)",
            "man0u1Quest:StartSequence(5)",
            "player:EndEvent()",
            "DoZoneChange(player, 175, nil, 0");
    }

    [Fact]
    public void CampBlackBrushAttunementQueuesTheRealMomodiCall()
    {
        string aetheryte = ReadDataScript(
            "base", "chara", "npc", "object", "aetheryte", "AetheryteParent.lua");
        string quest = ReadDataScript("quests", "man", "man0u1.lua");

        Assert.Contains("aetheryteId == 1280032", aetheryte, StringComparison.Ordinal);
        AssertOrdered(
            aetheryte,
            "quest:GetData():SetFlag(MAN0U1_FLAG_CAMP_ATTUNED)",
            "quest:NewNpcLsMsg(1)");
        AssertOrdered(
            quest,
            "GetFlag(MAN0U1_FLAG_CAMP_ATTUNED)",
            "\"processEvent013\"",
            "quest:EndOfNpcLsMsgs()",
            "quest:StartSequenceForNpcLs(MAN0U1_SEQ_RETURN)");
    }

    [Fact]
    public void ExistingBuild21989CharactersRepairWithoutDatabaseEditing()
    {
        string login = ReadDataScript("player.lua");

        Assert.Contains("local function repairBuild21989UldahHandoff(player)", login, StringComparison.Ordinal);
        Assert.Contains("player:HasQuest(110010) == false", login, StringComparison.Ordinal);
        Assert.Contains("quest:GetSequence() ~= 0", login, StringComparison.Ordinal);
        Assert.Contains("player.privateAreaType == 4", login, StringComparison.Ordinal);
        AssertOrdered(
            login,
            "quest:NewNpcLsMsg(1)",
            "quest:StartSequence(5)");
        AssertOrdered(
            login,
            "repairPrematureGridaniaLinkpearl(player)",
            "repairBuild21989UldahHandoff(player)");
    }

    private static string ReadDataScript(params string[] relativeParts)
    {
        string repositoryRoot = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine(
            new[] { repositoryRoot, "Data", "scripts" }.Concat(relativeParts).ToArray()));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "Data", "scripts")))
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private static void AssertOrdered(string value, params string[] fragments)
    {
        int previous = -1;
        foreach (string fragment in fragments)
        {
            int current = value.IndexOf(fragment, previous + 1, StringComparison.Ordinal);
            Assert.True(current >= 0, $"Expected to find '{fragment}' after offset {previous}.");
            previous = current;
        }
    }
}
