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
    public void CarlineHandoffLandsAtMiounneBeforeGrantingTheLinkpearl()
    {
        string script = ReadDataScript(
            "unique", "fst0Town01", "PrivateArea", "PrivateAreaMasterPast_1",
            "PopulaceStandard", "gridania_opening_exit.lua");

        AssertOrdered(script,
            "\"processEvent100\"",
            "player:ReplaceQuest(110005, 110006)",
            "player:EndEvent()",
            "DoZoneChange(player, 155, \"PrivateAreaMasterPast\", 2");
        Assert.DoesNotContain("NewNpcLsMsg", script, StringComparison.Ordinal);
        Assert.DoesNotContain("NextPhase", script, StringComparison.Ordinal);
        Assert.DoesNotContain("StartSequence", script, StringComparison.Ordinal);
        Assert.DoesNotContain("AfterQuestWarpDirector", script, StringComparison.Ordinal);
        Assert.DoesNotContain("SendGameMessage", script, StringComparison.Ordinal);
    }

    [Fact]
    public void FirstMiounneConversationGrantsTheLinkpearlThenStartsItsTutorial()
    {
        string script = ReadDataScript(
            "unique", "fst0Town01", "PrivateArea", "PrivateAreaMasterPast_2",
            "PopulaceStandard", "miounne.lua");

        Assert.Contains("man0g1Quest:GetSequence() == 0", script, StringComparison.Ordinal);
        Assert.Contains("man0g1Quest:GetSequence() == 5", script, StringComparison.Ordinal);
        AssertOrdered(script,
            "\"processEvent100_1\"",
            "man0g1Quest:NewNpcLsMsg(1)",
            "man0g1Quest:StartSequence(5)",
            "player:EndEvent()",
            "CreateDirector(\"AfterQuestWarpDirector\", false)",
            "director:StartDirector(true)",
            "player:AddDirector(director)",
            "player:SetLoginDirector(director)",
            "player:DeferContentKickEvent(director, \"noticeEvent\", true)",
            "man0g1Quest:UpdateENPCs()",
            "DoZoneChange(player, 155, nil, 0");

        string director = ReadDataScript("directors", "AfterQuestWarpDirector.lua");
        Assert.Contains("/Director/AfterQuestWarpDirector", director, StringComparison.Ordinal);
        Assert.Contains("quest:OnNotice(player)", director, StringComparison.Ordinal);
    }

    [Fact]
    public void PostWarpDirectorRoutesTheSuccessorQuestIntoTheSynchronousTutorial()
    {
        string quest = ReadDataScript("quests", "man", "man0g1.lua");
        string globals = ReadDataScript("global.lua");

        Assert.Contains("quest:GetSequence() == SEQ_005", quest, StringComparison.Ordinal);
        Assert.Contains("MESSAGE_TYPE_NPC_LINKSHELL  = 39", globals, StringComparison.Ordinal);
        AssertOrdered(
            quest,
            "player:RunEventFunction(\"delegateEvent\", player, quest, \"processEventTu_001\")",
            "player:EndEvent()");
        Assert.Contains("function onNpcLS", quest, StringComparison.Ordinal);
        AssertOrdered(
            quest,
            "player:SendGameMessageDisplayIDSender(quest, NPCLS_MSGS[msgPack][msgStep], MESSAGE_TYPE_NPC_LINKSHELL, 1300018)",
            "showTutorialSuccessWidget(player, 9080)",
            "endTutorialMode(player)",
            "player:EndEvent()");

        string repositoryRoot = FindRepositoryRoot();
        string processor = File.ReadAllText(Path.Combine(repositoryRoot, "src", "AetherXIV.Core.Map", "PacketProcessor.cs"));
        string player = File.ReadAllText(Path.Combine(repositoryRoot, "src", "AetherXIV.Core.Map", "Actors", "Chara", "Player", "Player.cs"));
        Assert.Contains("eventStart.ownerActorID == 0xA0F05E95", processor, StringComparison.Ordinal);
        Assert.Contains("StartNpcLinkshellEvent", player, StringComparison.Ordinal);
        Assert.Contains("25118", player, StringComparison.Ordinal);
        Assert.Contains("!wasOwned && (isCalling || isExtra)", player, StringComparison.Ordinal);
    }

    [Fact]
    public void PrematureBuild21987LinkpearlIsHiddenUntilMiounneIsSpokenTo()
    {
        string login = ReadDataScript("player.lua");

        Assert.Contains("local function repairPrematureGridaniaLinkpearl(player)", login, StringComparison.Ordinal);
        Assert.Contains("player:HasQuest(110006) == false", login, StringComparison.Ordinal);
        Assert.Contains("player:GetZoneID() ~= 155", login, StringComparison.Ordinal);
        Assert.Contains("player:GetPrivateAreaName() ~= \"PrivateAreaMasterPast\"", login, StringComparison.Ordinal);
        Assert.Contains("player.privateAreaType ~= 2", login, StringComparison.Ordinal);
        Assert.Contains("quest:GetSequence() ~= 5", login, StringComparison.Ordinal);
        Assert.Contains("quest:GetNpcLsFrom() == 0", login, StringComparison.Ordinal);
        AssertOrdered(
            login,
            "local function repairPrematureGridaniaLinkpearl(player)",
            "quest:EndOfNpcLsMsgs()");
        AssertOrdered(
            login,
            "setOpeningCheckpoint(player)",
            "repairPrematureGridaniaLinkpearl(player)");
        Assert.DoesNotContain("resumeGridaniaPostOpeningHandoff", login, StringComparison.Ordinal);

        string repositoryRoot = FindRepositoryRoot();
        string processor = File.ReadAllText(Path.Combine(
            repositoryRoot, "src", "AetherXIV.Core.Map", "PacketProcessor.cs"));
        AssertOrdered(
            processor,
            "\"onBeginLogin\"",
            "DoZoneIn(session.GetActor(), true, loginSpawnType)",
            "\"onLogin\"");
        AssertOrdered(
            processor,
            "session.GetActor().RefreshQuestENpcs()",
            "session.GetActor().ReleaseDeferredContentKickEvent()");
    }

    [Fact]
    public void AcceptedDestinationPositionAlsoReleasesDeferredTutorialNotice()
    {
        string player = LoadRepositoryFile(
            "src",
            "AetherXIV.Core.Map",
            "Actors",
            "Chara",
            "Player",
            "Player.cs");

        AssertOrdered(
            player,
            "public void CompleteZoneChange()",
            "SetZoneChanging(false);",
            "Database.SavePlayerPosition(this);",
            "ReleaseDeferredContentKickEvent();");
    }

    [Fact]
    public void BentbranchAndMiounneAdvanceTheConfirmedFiveThroughFifteenPath()
    {
        string aetheryte = ReadDataScript(
            "base", "chara", "npc", "object", "aetheryte", "AetheryteParent.lua");
        string quest = ReadDataScript("quests", "man", "man0g1.lua");

        AssertOrdered(
            aetheryte,
            "player:HasQuest(110006)",
            "quest:GetSequence() == SEQ_005",
            "\"processEvent013_2\"",
            "quest:StartSequence(SEQ_010)",
            "doNormalMenu(player, aetheryte)",
            "player:EndEvent()");

        Assert.Contains("function onStateChange", quest, StringComparison.Ordinal);
        Assert.Contains("quest:SetENpc(MIOUNNE, QFLAG_TALK)", quest, StringComparison.Ordinal);
        AssertOrdered(
            quest,
            "\"processEvent114\"",
            "quest:StartSequence(SEQ_012)",
            "\"processEvent115\"",
            "quest:NewNpcLsMsg(1)",
            "quest:StartSequence(SEQ_015)");
        Assert.Contains("{332, 333, 334, 335}", quest, StringComparison.Ordinal);
        Assert.Contains("quest:ReadNpcLsMsg()", quest, StringComparison.Ordinal);
        Assert.Contains("quest:EndOfNpcLsMsgs()", quest, StringComparison.Ordinal);

        string repositoryRoot = FindRepositoryRoot();
        string player = File.ReadAllText(Path.Combine(repositoryRoot, "src", "AetherXIV.Core.Map", "Actors", "Chara", "Player", "Player.cs"));
        Assert.Contains("DisableRetiredGridaniaOpeningTrigger", player, StringComparison.Ordinal);
        Assert.Contains("npc.GetActorClassId() != 1099046", player, StringComparison.Ordinal);
        Assert.Contains("SetEventStatus(npc, condition.conditionName, false, 2)", player, StringComparison.Ordinal);
    }

    [Fact]
    public void PhaseFifteenConvergesBothRetailGuildOrdersAndDefersTheHandoffUntilZoneIn()
    {
        string quest = ReadDataScript("quests", "man", "man0g1.lua");

        Assert.Contains("{131, 132, 133}", quest, StringComparison.Ordinal);
        Assert.Contains("data:GetCounter(COUNTER_LEATHERWORKERS) >= GUILD_STARTED", quest, StringComparison.Ordinal);
        Assert.Contains("data:GetCounter(COUNTER_CONJURERS) >= GUILD_COMPLETE", quest, StringComparison.Ordinal);
        Assert.Contains("data:GetCounter(COUNTER_CONJURERS) == GUILD_SCENE", quest, StringComparison.Ordinal);
        Assert.Contains("quest:SetENpc(CONJURERS_BRIDGE, QFLAG_OFF, false, false)", quest, StringComparison.Ordinal);
        AssertOrdered(
            quest,
            "data:SetCounter(COUNTER_GUILD_HANDOFF, 1)",
            "quest:NewNpcLsMsg(1)");
        AssertOrdered(
            quest,
            "\"processEvent120\"",
            "player:AddGil(2000)",
            "SetCounter(COUNTER_LEATHERWORKERS, GUILD_STARTED)");
        AssertOrdered(
            quest,
            "\"processEvent125\"",
            "SetCounter(COUNTER_CONJURERS, GUILD_STARTED)",
            "\"processEvent130\"",
            "SetCounter(COUNTER_CONJURERS, GUILD_SCENE)",
            "GetWorldManager():DoZoneChange(player, 155, \"PrivateAreaMasterPast\", 0");
        AssertOrdered(
            quest,
            "\"processEvent135\"",
            "\"processEvent136\"",
            "SetCounter(COUNTER_CONJURERS, GUILD_COMPLETE)",
            "GetWorldManager():DoZoneChange(player, 206, nil, 0");
        Assert.DoesNotContain("\n\t\tDoZoneChange(", quest, StringComparison.Ordinal);
        Assert.Contains("player:GetZoneID() == 206", quest, StringComparison.Ordinal);
        AssertOrdered(
            quest,
            "quest:EndOfNpcLsMsgs()",
            "quest:StartSequenceForNpcLs(SEQ_040)");
    }

    [Fact]
    public void StoryGilRewardsUseThePlayerCurrencyContract()
    {
        string repositoryRoot = FindRepositoryRoot();
        string player = LoadRepositoryFile(
            "src", "AetherXIV.Core.Map", "Actors", "Chara", "Player", "Player.cs");
        string quest = ReadDataScript("quests", "man", "man0g1.lua");

        Assert.Contains("public int AddGil(int amount)", player, StringComparison.Ordinal);
        Assert.Contains("GetItemPackage(ItemPackage.CURRENCY_CRYSTALS).AddItem(GilCatalogId, amount, 1)", player, StringComparison.Ordinal);
        Assert.Contains("player:AddGil(2000)", quest, StringComparison.Ordinal);
        Assert.Contains("player:AddGil(3000)", quest, StringComparison.Ordinal);

        string questRoot = Path.Combine(repositoryRoot, "Data", "scripts", "quests");
        foreach (string path in Directory.EnumerateFiles(questRoot, "*.lua", SearchOption.AllDirectories))
        {
            string script = File.ReadAllText(path);
            Assert.DoesNotContain(
                "GetItemPackage(INVENTORY_CURRENCY):AddItem(1000001",
                script,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ConjurersSceneUsesTheReviewedInstanceThirteenCastAndDialogue()
    {
        string quest = ReadDataScript("quests", "man", "man0g1.lua");
        string area = LoadRepositoryFile("src", "AetherXIV.Core.Map", "Actors", "Area", "Area.cs");

        foreach (string eventName in new[]
        {
            "processEvent130_2", "processEvent130_3", "processEvent130_4", "processEvent130_5",
            "processEvent130_6", "processEvent130_7", "processEvent130_8", "processEvent130_9",
            "processEvent130_10"
        })
            Assert.Contains(eventName, quest, StringComparison.Ordinal);

        Assert.Contains("location.uniqueId == \"conjurers_guild_scene_entry\"", area, StringComparison.Ordinal);
        Assert.Contains("npc.SetPushCircleRange(\"pushDefault\", 3.0f)", area, StringComparison.Ordinal);
    }

    [Fact]
    public void GroweryContinuationUsesTheRecoveredClientPhasesAndInstanceTransitions()
    {
        string quest = ReadDataScript("quests", "man", "man0g1.lua");

        AssertOrdered(
            quest,
            "\"processEvent140\"",
            "quest:StartSequence(SEQ_050)",
            "DoZoneChange(player, 155, \"PrivateAreaMasterPast\", 1, 15, -223.08, 12, -1498.546, -1.64)");
        foreach (string eventName in new[]
        {
            "processEvent140_1", "processEvent140_2", "processEvent140_3", "processEvent140_4", "processEvent140_5", "processEvent140_6",
            "processEvent142_1", "processEvent142_2", "processEvent142_3", "processEvent142_4", "processEvent142_5", "processEvent142_6"
        })
            Assert.Contains(eventName, quest, StringComparison.Ordinal);
        Assert.Contains("[POWLE] = {FLAG_POWLE_EMOTE, \"processEvent140_2\"", quest, StringComparison.Ordinal);
        Assert.Contains("[NICOLLAUX] = {FLAG_NICOLLAUX_EMOTE, \"processEvent140_4\"", quest, StringComparison.Ordinal);
        Assert.Contains("bit32.band(data:GetFlags(), ALL_GROWERY_EMOTES) == ALL_GROWERY_EMOTES", quest, StringComparison.Ordinal);
        AssertOrdered(
            quest,
            "\"processEvent150\"",
            "quest:StartSequence(SEQ_055)",
            "DoZoneChange(player, 155, \"PrivateAreaMasterPast\", 2, 15, -227.84, 12, -1502.2, 0.51)");
        AssertOrdered(
            quest,
            "\"processEvent160\"",
            "quest:StartSequence(SEQ_060)",
            "DoZoneChange(player, 206, nil, 0, 15, -209.46, 18.05, -1476.31, 1.47)");
    }

    [Fact]
    public void RemainingMan0g1ChainUsesContentDutyAndRecoveredEventOrder()
    {
        string quest = ReadDataScript("quests", "man", "man0g1.lua");
        string content = ReadDataScript("content", "SimpleContentMan0g101.lua");
        string director = ReadDataScript("directors", "Quest", "QuestDirectorMan0g101.lua");
        string actor = LoadRepositoryFile("src", "AetherXIV.Core.Map", "Actors", "Actor.cs");
        string contentArea = LoadRepositoryFile("src", "AetherXIV.Core.Map", "Actors", "Area", "PrivateAreaContent.cs");

        foreach (string eventName in new[]
        {
            "processEvent170", "processEvent180", "processEvent181", "processEvent182",
            "processEvent185", "processEvent190", "processEvent200", "processEvent210",
            "processEvent220", "processEventComplete"
        })
            Assert.Contains(eventName, quest + director, StringComparison.Ordinal);
        Assert.Contains("DoZoneChangeContent(player, area", quest, StringComparison.Ordinal);
        Assert.Contains("DoZoneChange(player, 150, \"PrivateAreaMasterPast\", 1, 15,", quest, StringComparison.Ordinal);
        Assert.Contains("DoZoneChange(player, 155, nil, 0, 15, -185, 6, -962, -3)", quest, StringComparison.Ordinal);
        Assert.DoesNotContain("DoZoneChange(player, 150, nil, 0, 15, -185, 6, -962, -3)", quest, StringComparison.Ordinal);
        Assert.Contains("DoZoneChange(player, 155, \"PrivateAreaMasterPast\", 4, 15,", quest, StringComparison.Ordinal);
        Assert.DoesNotContain("WarpToPrivateArea", quest, StringComparison.Ordinal);
        Assert.Contains("quest:GetSequence() == SEQ_071 and player.privateAreaType == 0", quest, StringComparison.Ordinal);
        Assert.Contains("quest:GetSequence() == SEQ_072 and player.privateAreaType == 1", quest, StringComparison.Ordinal);
        Assert.Contains("sequence == SEQ_090 and classId == BURCHARD and player:GetZoneID() == 206", quest, StringComparison.Ordinal);
        Assert.Contains("sequence == SEQ_095 and classId == BURCHARD and player.privateAreaType == 3", quest, StringComparison.Ordinal);
        Assert.Contains("elseif (sequence == SEQ_100) then", quest, StringComparison.Ordinal);
        Assert.Contains("(sequence == SEQ_005 or sequence == SEQ_010 or sequence == SEQ_012)", quest, StringComparison.Ordinal);
        Assert.Contains("elseif (player:GetZoneID() == 206) then\n\t\t\tquest:SetENpc(MIOUNNE, QFLAG_REWARD)", quest, StringComparison.Ordinal);
        Assert.Contains("DeferContentKickEvent(director, \"noticeEvent\", true)", quest, StringComparison.Ordinal);
        Assert.Contains("waitForSignal(player:GetZone():GetPlayerSignal(player, \"escortComplete\"))", director, StringComparison.Ordinal);
        Assert.Contains("GetWorldManager().SpawnBattleNpcById(34, area)", content, StringComparison.Ordinal);
        Assert.Contains("for spawnId = 36, 41", content, StringComparison.Ordinal);
        Assert.Contains("allyGlobal.EngageTarget(enemy, owner)", content, StringComparison.Ordinal);
        Assert.Contains("area:SetScriptState(stateKey(player, \"powleId\"), powle.actorId)", content, StringComparison.Ordinal);
        Assert.Contains("area:GetScriptState(stateKey(owner, \"powleId\"))", content, StringComparison.Ordinal);
        Assert.Contains("area:SpawnActor(ESCORT_RING_CLASS, \"escortAreaRange\"", content, StringComparison.Ordinal);
        Assert.Contains("ring:MoveTo(powle.positionX, powle.positionY, powle.positionZ, 0, 2)", content, StringComparison.Ordinal);
        Assert.Contains("for ally in area:GetAllies() do", content, StringComparison.Ordinal);
        Assert.Contains("for enemy in area:GetMonsters() do", content, StringComparison.Ordinal);
        Assert.Contains("not powle:IsAlive() or not sansa:IsAlive()", content, StringComparison.Ordinal);
        Assert.Contains("distance(sansa.positionX, sansa.positionZ, ARRIVAL_X, ARRIVAL_Z)", content, StringComparison.Ordinal);
        Assert.Contains("enemy.actorId ~= powleId and enemy.actorId ~= sansaId", content, StringComparison.Ordinal);
        Assert.Contains("enemy.actorId ~= ringId", content, StringComparison.Ordinal);
        Assert.Contains("local ROUTE = {", content, StringComparison.Ordinal);
        Assert.Contains("area:SetScriptState(stateKey(player, \"routeIndex\"), 2)", content, StringComparison.Ordinal);
        Assert.Contains("local ESCORT_BOUNDARY_RADIUS = 28.0", content, StringComparison.Ordinal);
        Assert.Contains("toPlayer <= ESCORT_BOUNDARY_RADIUS", content, StringComparison.Ordinal);
        Assert.DoesNotContain("PLAYER_LEASH", content, StringComparison.Ordinal);
        Assert.Contains("local waypoint = ROUTE[routeIndex]", content, StringComparison.Ordinal);
        Assert.Contains("Powle: Let's go! Off to the Twelveswood!", content, StringComparison.Ordinal);
        Assert.Contains("Powle: What was that? Did you see something?", content, StringComparison.Ordinal);
        Assert.Contains("Sansa: You did it! You did it!", content, StringComparison.Ordinal);
        Assert.Contains("emitBark(owner, BARKS.waveResume)", content, StringComparison.Ordinal);
        Assert.DoesNotContain("owner.positionX - powle.positionX", content, StringComparison.Ordinal);
        Assert.DoesNotContain("for index = 1, #allies", content, StringComparison.Ordinal);
        Assert.DoesNotContain("for index = 1, #liveEnemies", content, StringComparison.Ordinal);
        Assert.DoesNotContain("local states = {}", content, StringComparison.Ordinal);
        Assert.Contains("player:GetPrivateAreaName() == \"SimpleContentMan0g101\"", quest, StringComparison.Ordinal);
        Assert.Contains("public void MoveTo", actor, StringComparison.Ordinal);
        Assert.Contains("public void SetScriptState", contentArea, StringComparison.Ordinal);
        Assert.Contains("public long GetScriptState", contentArea, StringComparison.Ordinal);
        Assert.Contains("CallLuaFunctionForReturn(", contentArea, StringComparison.Ordinal);
        Assert.Contains("\"onUpdate\", true, contentUpdateTick, this", contentArea, StringComparison.Ordinal);

        string rangeActor = ReadDataScript("base", "chara", "npc", "object", "ContentPrivateAreaRange.lua");
        Assert.Contains("function init(npc)", rangeActor, StringComparison.Ordinal);
        Assert.Contains("function onEventStarted(player, npc, triggerName)", rangeActor, StringComparison.Ordinal);
        Assert.Contains("player:EndEvent()", rangeActor, StringComparison.Ordinal);
        AssertOrdered(quest,
            "\"processEventComplete\"",
            "\"sqrwa\", 300, 1, 1, 2",
            "player:CompleteQuest(\"Man0g1\")");
        AssertOrdered(quest,
            "quest:NewNpcLsMsg(1);\n\t\tquest:StartSequence(SEQ_100)",
            "quest:StartSequenceForNpcLs(SEQ_105)");
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

    private static string LoadRepositoryFile(params string[] parts)
    {
        return File.ReadAllText(parts.Aggregate(FindRepositoryRoot(), Path.Combine));
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
