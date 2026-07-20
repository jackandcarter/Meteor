namespace AetherXIV.Data.Tests;

public sealed class GridaniaOpeningTutorialScriptTests
{
    [Fact]
    public void DirectorPreservesRetailEventAndBattleOrdering()
    {
        string script = ReadDataScript("directors", "Quest", "QuestDirectorMan0g001.lua");

        AssertOrdered(script,
            "\"processTtrBtl001\"",
            "GetPlayerSignal(player, \"playerActive\")",
            "\"processTtrBtl002\"",
            "GetPlayerSignal(player, \"playerAttack\")",
            "EngageContentBattleForPlayer",
            "GetPlayerSignal(player, \"tpOver1000\")",
            "GetPlayerSignal(player, \"weaponskillUsed\")",
            "SetBattleNpcMinimumHpLock(0)",
            "IsContentBattleComplete",
            "\"attention\"",
            "kickEventContinue(player, actor, \"noticeEvent\", \"noticeEvent\")",
            "\"processEvent020_1\"",
            "man0g0Quest:NextPhase(10)",
            "ContentFinished()",
            "actor:EndDirector()",
            "DoZoneChange(player, 155");

        Assert.DoesNotContain("player:SendMessage", script, StringComparison.Ordinal);
        Assert.DoesNotContain("waitForSignal(\"mobkill\")", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentCreatesTheReviewedRetailRosterAndLocksItForTargeting()
    {
        string script = ReadDataScript("content", "SimpleContent30010.lua");

        foreach (int id in new[] { 3, 4, 5, 6, 7 })
            Assert.Contains($"SpawnBattleNpcById({id}", script, StringComparison.Ordinal);

        Assert.Equal(4, CountOccurrences(script, ":ChangeState(2)"));
        Assert.Equal(6, CountOccurrences(script, "MinimumHpLock, 1"));
        Assert.Contains("SpawnActor(1090384, \"openingstoper\"", script, StringComparison.Ordinal);
        Assert.Contains("director:AddMember(papalymo)", script, StringComparison.Ordinal);
        Assert.Contains("director:AddMember(yda)", script, StringComparison.Ordinal);
        Assert.Contains("currentParty:AddMember(papalymo.actorId)", script, StringComparison.Ordinal);
        Assert.Contains("currentParty:AddMember(yda.actorId)", script, StringComparison.Ordinal);

        string director = ReadDataScript("directors", "Quest", "QuestDirectorMan0g001.lua");
        Assert.DoesNotContain("director:StartContentGroup()", director, StringComparison.Ordinal);
        Assert.DoesNotContain("AddAlliesToPlayerParty", director, StringComparison.Ordinal);

        string activateCommand = ReadDataScript("commands", "ActivateCommand.lua");
        Assert.Contains("player:EmitContentProgressSignal(\"playerActive\")", activateCommand, StringComparison.Ordinal);
        Assert.DoesNotContain("sendSignal(\"playerActive\")", activateCommand, StringComparison.Ordinal);
    }

    [Fact]
    public void GridaniaAllyPoolsUseTheirRetailActorClasses()
    {
        string sql = File.ReadAllText(Path.Combine(Directory.GetParent(FindDataRoot())!.FullName, "sql", "server_battlenpc_pools.sql"));

        Assert.Contains("VALUES (3,2290006,'yda'", sql, StringComparison.Ordinal);
        Assert.Contains("VALUES (4,2290005,'papalymo'", sql, StringComparison.Ordinal);
    }

    private static void AssertOrdered(string text, params string[] tokens)
    {
        int previous = -1;
        foreach (string token in tokens)
        {
            int current = text.IndexOf(token, previous + 1, StringComparison.Ordinal);
            Assert.True(current > previous, $"Expected '{token}' after offset {previous}.");
            previous = current;
        }
    }

    private static int CountOccurrences(string text, string token)
    {
        int count = 0;
        int offset = 0;
        while ((offset = text.IndexOf(token, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += token.Length;
        }
        return count;
    }

    private static string ReadDataScript(params string[] parts)
    {
        string path = parts.Aggregate(FindDataRoot(), Path.Combine);
        return File.ReadAllText(path);
    }

    private static string FindDataRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "Data", "scripts");
            if (Directory.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository Data/scripts directory.");
    }
}
