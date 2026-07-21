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
        Assert.Contains("currentParty:AddTransientMembers(papalymo.actorId, yda.actorId)", script, StringComparison.Ordinal);
        Assert.DoesNotContain("currentParty:AddMember(", script, StringComparison.Ordinal);

        string director = ReadDataScript("directors", "Quest", "QuestDirectorMan0g001.lua");
        Assert.DoesNotContain("director:StartContentGroup()", director, StringComparison.Ordinal);
        Assert.DoesNotContain("AddAlliesToPlayerParty", director, StringComparison.Ordinal);

        string activateCommand = ReadDataScript("commands", "ActivateCommand.lua");
        Assert.Contains("player:EmitContentProgressSignal(\"playerActive\")", activateCommand, StringComparison.Ordinal);
        Assert.DoesNotContain("sendSignal(\"playerActive\")", activateCommand, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentEntryDefersNoticeEventUntilTheActorReloadIsAcknowledged()
    {
        string yda = ReadDataScript("unique", "fst0Battle03", "PopulaceStandard", "yda.lua");

        AssertOrdered(yda,
            "director:StartDirector(false)",
            "player:DeferContentKickEvent(director, \"noticeEvent\", true)",
            "player:SetLoginDirector(director)",
            "DoZoneChangeContent(player, contentArea");
        Assert.DoesNotContain("player:KickEvent(director, \"noticeEvent\", true)", yda, StringComparison.Ordinal);
    }

    [Fact]
    public void ArrowReloadCommandAcknowledgesTheClientEvent()
    {
        string script = ReadDataScript("commands", "ArrowReloadCommand.lua");

        Assert.Contains("function onEventStarted", script, StringComparison.Ordinal);
        Assert.Contains("player:EndEvent()", script, StringComparison.Ordinal);
        Assert.DoesNotContain("player.Ability", script, StringComparison.Ordinal);
        Assert.DoesNotContain("player.WeaponSkill", script, StringComparison.Ordinal);
    }

    [Fact]
    public void CarlineHandoffClosesTheSourceEventAndDefersTheDestinationKick()
    {
        string script = ReadDataScript(
            "unique", "fst0Town01", "PrivateArea", "PrivateAreaMasterPast_1",
            "PopulaceStandard", "gridania_opening_exit.lua");

        AssertOrdered(script,
            "\"processEvent100\"",
            "player:ReplaceQuest(110005, 110006)",
            "ownedMan0g1Quest:NewNpcLsMsg(1)",
            "ownedMan0g1Quest:NextPhase(5)",
            "player:EndEvent()",
            "CreateDirector(\"AfterQuestWarpDirector\", false)",
            "player:SetLoginDirector(director)",
            "player:DeferContentKickEvent(director, \"noticeEvent\", true)",
            "DoZoneChange(player, 155, \"PrivateAreaMasterPast\", 2");

        string director = ReadDataScript("directors", "AfterQuestWarpDirector.lua");
        Assert.Contains("/Director/AfterQuestWarpDirector", director, StringComparison.Ordinal);
        Assert.Contains("quest:OnNotice(player)", director, StringComparison.Ordinal);
    }

    [Fact]
    public void PostWarpDirectorRoutesTheSuccessorQuestIntoTheSynchronousTutorial()
    {
        string quest = ReadDataScript("quests", "man", "man0g1.lua");

        Assert.Contains("quest:GetPhase() == 5", quest, StringComparison.Ordinal);
        AssertOrdered(
            quest,
            "player:RunEventFunction(\"delegateEvent\", player, quest, \"processEventTu_001\")",
            "player:EndEvent()");
        Assert.DoesNotContain("callClientFunction", quest, StringComparison.Ordinal);
    }

    [Fact]
    public void GridaniaAllyPoolsUseTheirRetailActorClasses()
    {
        string sql = File.ReadAllText(Path.Combine(Directory.GetParent(FindDataRoot())!.FullName, "sql", "server_battlenpc_pools.sql"));

        Assert.Contains("VALUES (3,2290006,'yda'", sql, StringComparison.Ordinal);
        Assert.Contains("VALUES (4,2290005,'papalymo'", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void MapRuntimeUsesTheConfirmedZoneDirectorAndBattleContracts()
    {
        string repositoryRoot = FindRepositoryRoot();
        string worldManager = File.ReadAllText(Path.Combine(repositoryRoot, "src", "AetherXIV.Core.Map", "WorldManager.cs"));
        string area = File.ReadAllText(Path.Combine(repositoryRoot, "src", "AetherXIV.Core.Map", "Actors", "Area", "Area.cs"));
        string battleNpc = File.ReadAllText(Path.Combine(repositoryRoot, "src", "AetherXIV.Core.Map", "Actors", "Chara", "Npc", "BattleNpc.cs"));
        string player = File.ReadAllText(Path.Combine(repositoryRoot, "src", "AetherXIV.Core.Map", "Actors", "Chara", "Player", "Player.cs"));
        string director = File.ReadAllText(Path.Combine(repositoryRoot, "src", "AetherXIV.Core.Map", "Actors", "Director", "Director.cs"));
        string contentGroup = File.ReadAllText(Path.Combine(repositoryRoot, "src", "AetherXIV.Core.Map", "Actors", "Group", "ContentGroup.cs"));
        string battleNpcController = File.ReadAllText(Path.Combine(repositoryRoot, "src", "AetherXIV.Core.Map", "Actors", "Chara", "Ai", "Controllers", "BattleNpcController.cs"));

        Assert.Equal(2, CountOccurrences(worldManager, "SendZoneInstanceSnapshot(this)"));
        Assert.Contains("player.SendInstanceUpdate(true);", worldManager, StringComparison.Ordinal);
        string contentZoneChange = worldManager.Substring(
            worldManager.IndexOf("public void DoZoneChangeContent", StringComparison.Ordinal),
            worldManager.IndexOf("public void DoZoneIn", StringComparison.Ordinal) -
            worldManager.IndexOf("public void DoZoneChangeContent", StringComparison.Ordinal));
        Assert.DoesNotContain("SendZoneInstanceSnapshot(this)", contentZoneChange, StringComparison.Ordinal);
        string ordinaryZoneChange = worldManager.Substring(
            worldManager.IndexOf("public void DoZoneChange(Player player, uint destinationZoneId", StringComparison.Ordinal),
            worldManager.IndexOf("public void DoZoneChangeContent", StringComparison.Ordinal) -
            worldManager.IndexOf("public void DoZoneChange(Player player, uint destinationZoneId", StringComparison.Ordinal));
        Assert.Contains("ZoneTransitionReloadPolicy.Select", ordinaryZoneChange, StringComparison.Ordinal);
        Assert.Contains("ZoneTransitionReloadRecipe.ResidentGeometry", ordinaryZoneChange, StringComparison.Ordinal);
        Assert.Contains("DeleteAllActorsPacket.BuildPacket", ordinaryZoneChange, StringComparison.Ordinal);
        Assert.Contains("_0xE2Packet.BuildPacket(player.actorId, 0x10)", ordinaryZoneChange, StringComparison.Ordinal);
        Assert.Contains("_0xE2Packet.BuildPacket(player.actorId, 0x2)", ordinaryZoneChange, StringComparison.Ordinal);
        Assert.Contains("director.zoneId == zoneId && !director.IsDeleted()", player, StringComparison.Ordinal);
        Assert.Contains("if (loginInitDirector == director)", player, StringComparison.Ordinal);
        Assert.Contains("loginInitDirector = null;", player, StringComparison.Ordinal);

        string packetProcessor = File.ReadAllText(Path.Combine(repositoryRoot, "src", "AetherXIV.Core.Map", "PacketProcessor.cs"));
        Assert.Contains("session.GetActor().ReleaseDeferredContentKickEvent();", packetProcessor, StringComparison.Ordinal);
        Assert.Contains("public void DeferContentKickEvent", player, StringComparison.Ordinal);
        Assert.Contains("public void ReleaseDeferredContentKickEvent", player, StringComparison.Ordinal);
        AssertOrdered(worldManager,
            "reason = \"phase-10-post-battle\"",
            "Director director = contentArea.GetContentDirector();",
            "player.SetLoginDirector(director);",
            "player.DeferContentKickEvent(director, \"noticeEvent\", true);",
            "reason = \"persisted-private-content-reconstructed\"");
        Assert.Contains("target.currentContentGroup != player.currentContentGroup", area, StringComparison.Ordinal);
        Assert.Contains("int engaged = EngageAlliesForPlayer(player);", area, StringComparison.Ordinal);
        Assert.DoesNotContain("foreach (BattleNpc enemy in enemies)", area, StringComparison.Ordinal);
        Assert.Contains("QueuePackets(directorEventStatusPackets);", player, StringComparison.Ordinal);
        Assert.Contains("QueuePackets(director.GetSetEventStatusPackets());", player, StringComparison.Ordinal);
        Assert.Contains("p.QueuePackets(GetSetEventStatusPackets());", director, StringComparison.Ordinal);
        Assert.Contains("npcWork.hateType = NpcWork.HATE_TYPE_PASSIVE;", battleNpc, StringComparison.Ordinal);
        Assert.DoesNotContain("npcWork.hateType = NpcWork.HATE_TYPE_ENGAGED_PARTY;", battleNpc, StringComparison.Ordinal);
        Assert.Contains("GridaniaOpeningTutorialPolicy.IsLiveContentCombat(owner, hatedTarget)", battleNpcController, StringComparison.Ordinal);
        Assert.Contains("SetEnmityIndicatorPacket.BuildPacket", battleNpcController, StringComparison.Ordinal);
        Assert.Contains("GridaniaOpeningTutorialPolicy.IsLiveContentCombat(tutorialNpc, this)", player, StringComparison.Ordinal);
        AssertOrdered(battleNpc, "base.OnDespawn();", "contentGroup.RemoveMember(actorId);");
        AssertOrdered(contentGroup, "npc.OnDespawn();", "npc.Despawn();");
        Assert.Contains("if (isDeleting || isDeleted)", contentGroup, StringComparison.Ordinal);
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

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AetherXIV.sln")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src", "AetherXIV.Core.Map")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the AetherXIV repository root.");
    }
}
