require("global")
require("tutorial")
require("quest")

ENABLE_GL_TUTORIAL = false;

SEQ_005 = 5;
SEQ_010 = 10;
SEQ_012 = 12;
SEQ_015 = 15;
SEQ_040 = 40;
SEQ_050 = 50;
SEQ_055 = 55;
SEQ_060 = 60;
SEQ_065 = 65;
SEQ_070 = 70;
SEQ_071 = 71;
SEQ_072 = 72;
SEQ_075 = 75;
SEQ_080 = 80;
SEQ_085 = 85;
SEQ_090 = 90;
SEQ_095 = 95;
SEQ_100 = 100;
SEQ_105 = 105;

MIOUNNE = 1000230;
HEREWARD = 1000231;
SOILEINE = 1700030;
CONJURERS_BRIDGE = 1099046;
SWETHYNA = 1000028;
OPYLTYL = 1000236;
FUFUCHA = 1000237;
POWLE = 1000238;
SANSA = 1000239;
NICOLLAUX = 1000409;
AUNILLE = 1000410;
ELYN = 1000411;
RYD = 1000412;
KIDS_TRIGGER = 1090201;
GATE_TRIGGER = 1090202;
STUMP_TRIGGER = 1090203;
STUMP_EXIT_TRIGGER = 1090204;
BTN_TRIGGER = 1090046;
WILLELDA = 1000242;
BURCHARD = 1000243;
NUALA = 1000681;

YDA_SCENE = 1000009;
PAPALYMO_SCENE = 1000010;
O_APP_PESI = 1000033;
HETZKIN = 1000460;
GUGULA = 1000513;
BIDDY = 1000737;
INGRAM = 1000372;
CHALLINIE = 1001072;

COUNTER_LEATHERWORKERS = 0;
COUNTER_CONJURERS = 1;
COUNTER_GUILD_HANDOFF = 2;

GUILD_NOT_STARTED = 0;
GUILD_STARTED = 1;
GUILD_SCENE = 2;
GUILD_COMPLETE = 3;

FLAG_AUNILLE_EMOTE = 1;
FLAG_POWLE_EMOTE = 2;
FLAG_SANSA_EMOTE = 3;
FLAG_NICOLLAUX_EMOTE = 4;
FLAG_RYD_EMOTE = 5;
FLAG_ELYN_EMOTE = 6;
ALL_GROWERY_EMOTES = 0x7E;
FLAG_ESCORT_HANDOFF = 7;

GROWERY_CHILDREN = {
	[AUNILLE] = {FLAG_AUNILLE_EMOTE, "processEvent140_1", "processEvent141_1", "processEvent142_1", 8, 21071},
	[POWLE] = {FLAG_POWLE_EMOTE, "processEvent140_2", "processEvent141_2", "processEvent142_2", 7, 21061},
	[SANSA] = {FLAG_SANSA_EMOTE, "processEvent140_3", "processEvent141_3", "processEvent142_3", 5, 21041},
	[NICOLLAUX] = {FLAG_NICOLLAUX_EMOTE, "processEvent140_4", "processEvent141_4", "processEvent142_4", 6, 21051},
	[RYD] = {FLAG_RYD_EMOTE, "processEvent140_5", "processEvent141_5", "processEvent142_5", 1, 21001},
	[ELYN] = {FLAG_ELYN_EMOTE, "processEvent140_6", "processEvent141_6", "processEvent142_6", 22, 21211}
};

NPCLS_MSGS = {
	{330},
	{332, 333, 334, 335},
	{131, 132, 133},
	{210, 211, 212, 213, 214, 215},
	{322, 323, 324}
};

CONJURERS_SCENE_EVENTS = {
	[YDA_SCENE] = "processEvent130_4",
	[PAPALYMO_SCENE] = "processEvent130_5",
	[O_APP_PESI] = "processEvent130_3",
	[SOILEINE] = "processEvent130_9",
	[HETZKIN] = "processEvent130_10",
	[GUGULA] = "processEvent130_7",
	[BIDDY] = "processEvent130_8",
	[INGRAM] = "processEvent130_6",
	[CHALLINIE] = "processEvent130_2"
};

function queueGuildHandoff(player, quest)
	local data = quest:GetData();
	if (player:GetZoneID() == 206 and
		data:GetCounter(COUNTER_LEATHERWORKERS) >= GUILD_STARTED and
		data:GetCounter(COUNTER_CONJURERS) >= GUILD_COMPLETE and
		data:GetCounter(COUNTER_GUILD_HANDOFF) == 0) then
		-- Persist the latch before presenting the linkpearl. A reconnect can
		-- never duplicate the retail handoff, while a disconnect before the
		-- public-zone acknowledgement leaves it recoverable.
		data:SetCounter(COUNTER_GUILD_HANDOFF, 1);
		quest:NewNpcLsMsg(1);
	end
end

-- The first post-opening Man0g1 continuation. The destination-scoped
-- AfterQuestWarpDirector invokes this after the PA/1 -> PA/2 replacement
-- bundle is acknowledged, when it is safe to address the successor quest.
function onNotice(player, quest, target)
	if (quest:GetSequence() == SEQ_005) then
		-- This tutorial is synchronous and sends no EventUpdate. Using the raw
		-- event function is required so EndEvent runs instead of parking a Lua
		-- coroutine forever behind the NPC-linkpearl overlay.
		player:RunEventFunction("delegateEvent", player, quest, "processEventTu_001");
	end

	player:EndEvent();
end

function isObjectivesComplete(player, quest)
	return false;
end

function onStateChange(player, quest, sequence)
	-- Phase 5 still exits the confirmed Canopy private-area handler. Quest
	-- ownership begins after Bentbranch so it cannot steal that exit event.
	if (sequence == SEQ_010 or sequence == SEQ_012) then
		quest:SetENpc(MIOUNNE, QFLAG_TALK);
	elseif (sequence == SEQ_015) then
		local data = quest:GetData();
		if (player:GetZoneID() == 206) then
			if (data:GetCounter(COUNTER_LEATHERWORKERS) == GUILD_NOT_STARTED) then
				quest:SetENpc(HEREWARD, QFLAG_TALK);
			end
			if (data:GetCounter(COUNTER_CONJURERS) == GUILD_NOT_STARTED) then
				quest:SetENpc(SOILEINE, QFLAG_TALK);
			elseif (data:GetCounter(COUNTER_CONJURERS) == GUILD_STARTED or
				data:GetCounter(COUNTER_CONJURERS) == GUILD_SCENE) then
				-- GUILD_SCENE in the public zone means the client disconnected or
				-- the server stopped after the scene was acknowledged but before
				-- Instance 13 completed its zone handoff. Re-enable the retail push
				-- so the transition is replayable instead of stranding the quest.
				quest:SetENpc(CONJURERS_BRIDGE, QFLAG_PUSH, false, true);
			else
				-- This actor is a quest-only volume whose native push condition is
				-- enabled when the public zone is reconstructed. It was not present
				-- in Instance 13, so the normal stale-ENPC sweep cannot disable it
				-- on the return trip; publish an explicit inert presentation.
				quest:SetENpc(CONJURERS_BRIDGE, QFLAG_OFF, false, false);
			end
			queueGuildHandoff(player, quest);
		elseif (player:GetZoneID() == 155 and data:GetCounter(COUNTER_CONJURERS) == GUILD_SCENE) then
			quest:SetENpc(SWETHYNA, QFLAG_TALK);
			for actorClassId, eventName in pairs(CONJURERS_SCENE_EVENTS) do
				quest:SetENpc(actorClassId, QFLAG_OFF, true);
			end
		end
	elseif (sequence == SEQ_040) then
		if (player:GetZoneID() == 206) then
			quest:SetENpc(OPYLTYL, QFLAG_TALK);
		end
	elseif (sequence == SEQ_050) then
		if (player:GetZoneID() == 206) then
			-- Public-area re-entry for a player who left Instance 14.
			quest:SetENpc(OPYLTYL, QFLAG_TALK);
		elseif (player:GetZoneID() == 155) then
			local data = quest:GetData();
			for actorClassId, child in pairs(GROWERY_CHILDREN) do
				local pending = not data:GetFlag(child[1]);
				quest:SetENpc(actorClassId, pending and QFLAG_TALK or QFLAG_OFF, true, false, pending);
			end
			quest:SetENpc(FUFUCHA, QFLAG_OFF, true);
		end
	elseif (sequence == SEQ_055) then
		if (player:GetZoneID() == 206) then
			-- Public-area re-entry for a player who left Instance 15.
			quest:SetENpc(OPYLTYL, QFLAG_TALK);
		elseif (player:GetZoneID() == 155) then
			quest:SetENpc(KIDS_TRIGGER, QFLAG_PUSH, false, true);
			quest:SetENpc(FUFUCHA, QFLAG_OFF, true);
			for actorClassId, child in pairs(GROWERY_CHILDREN) do
				quest:SetENpc(actorClassId, QFLAG_OFF, true);
			end
		end
	elseif (sequence == SEQ_060) then
		if (player:GetZoneID() == 155) then
			quest:SetENpc(GATE_TRIGGER, QFLAG_PUSH, false, true);
		end
	elseif (sequence == SEQ_065) then
		local data = quest:GetData();
		if (player:GetPrivateAreaName() == "SimpleContentMan0g101") then
			-- Area-change quest refreshes are expected after the map scene.
			-- Keep the live escort phase; only a public-area restart with no
			-- handoff latch should return to the White Wolf Gate trigger.
			if (data:GetFlag(FLAG_ESCORT_HANDOFF)) then
				data:ClearFlag(FLAG_ESCORT_HANDOFF);
			end
		elseif (data:GetFlag(FLAG_ESCORT_HANDOFF)) then
			data:ClearFlag(FLAG_ESCORT_HANDOFF);
		else
			quest:StartSequence(SEQ_060);
		end
	elseif (sequence == SEQ_070) then
		if (player:GetZoneID() == 150) then
			quest:SetENpc(STUMP_TRIGGER, QFLAG_PUSH, false, true);
		end
	elseif (sequence == SEQ_071) then
		if (player:GetZoneID() == 150) then
			if (player.privateAreaType == 0) then
				-- Recovery for an interruption after processEvent181 saved phase 71
				-- but before the PA/0 -> PA/1 replacement completed.
				quest:SetENpc(STUMP_TRIGGER, QFLAG_PUSH, false, true);
			else
				quest:SetENpc(STUMP_EXIT_TRIGGER, QFLAG_PUSH, false, true);
			end
		end
	elseif (sequence == SEQ_072) then
		if (player:GetZoneID() == 150 and player.privateAreaType == 1) then
			-- Recovery for an interruption after processEvent182 saved phase 72.
			quest:SetENpc(STUMP_EXIT_TRIGGER, QFLAG_PUSH, false, true);
		elseif (player:GetZoneID() == 206) then
			quest:SetENpc(BTN_TRIGGER, QFLAG_PUSH, false, true);
		end
	elseif (sequence == SEQ_080) then
		if (player:GetZoneID() == 206) then
			quest:SetENpc(WILLELDA, QFLAG_TALK);
		end
	elseif (sequence == SEQ_085) then
		if (player:GetZoneID() == 206) then
			quest:SetENpc(BURCHARD, QFLAG_TALK);
		end
	elseif (sequence == SEQ_090) then
		if (player:GetZoneID() == 206) then
			-- Resume the public -> PA/3 handoff without replaying processEvent200.
			quest:SetENpc(BURCHARD, QFLAG_TALK);
		elseif (player:GetZoneID() == 155) then
			quest:SetENpc(BURCHARD, QFLAG_TALK, true);
		end
	elseif (sequence == SEQ_095) then
		if (player:GetZoneID() == 155) then
			if (player.privateAreaType == 3) then
				-- Resume the PA/3 -> PA/4 handoff without replaying processEvent210.
				quest:SetENpc(BURCHARD, QFLAG_TALK, true);
			else
				quest:SetENpc(NUALA, QFLAG_TALK, true);
			end
		end
	elseif (sequence == SEQ_100) then
		if (player:GetZoneID() == 155 and player.privateAreaType == 4) then
			-- The linkpearl handoff is queued after Nuala's scene. If the session
			-- ends before the public-area replacement, keep its transition anchor.
			quest:SetENpc(NUALA, QFLAG_TALK, true);
		end
	elseif (sequence == SEQ_105) then
		if (player:GetZoneID() == 155 and player.privateAreaType == 4) then
			-- The final linkpearl can complete before a delayed PA/4 -> public
			-- replacement. Keep Nuala available to resume that handoff.
			quest:SetENpc(NUALA, QFLAG_TALK, true);
		elseif (player:GetZoneID() == 206) then
			quest:SetENpc(MIOUNNE, QFLAG_REWARD);
		end
	end
end

function onTalk(player, quest, npc)
	local sequence = quest:GetSequence();
	local classId = npc:GetActorClassId();

	if ((sequence == SEQ_005 or sequence == SEQ_010 or sequence == SEQ_012) and
		classId == MIOUNNE) then
		if (sequence == SEQ_005) then
			callClientFunction(player, "delegateEvent", player, quest, "processEvent110_2");
		elseif (sequence == SEQ_010) then
			callClientFunction(player, "delegateEvent", player, quest, "processEvent114");
			quest:StartSequence(SEQ_012);
		elseif (sequence == SEQ_012) then
			callClientFunction(player, "delegateEvent", player, quest, "processEvent115");
			quest:NewNpcLsMsg(1);
			quest:StartSequence(SEQ_015);
		end
	elseif (sequence == SEQ_015 and classId == HEREWARD) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent120");
		player:GetItemPackage(INVENTORY_CURRENCY):AddItem(1000001, 2000, 1);
		quest:GetData():SetCounter(COUNTER_LEATHERWORKERS, GUILD_STARTED);
		queueGuildHandoff(player, quest);
	elseif (sequence == SEQ_015 and classId == SOILEINE and
		quest:GetData():GetCounter(COUNTER_CONJURERS) == GUILD_NOT_STARTED) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent125");
		quest:GetData():SetCounter(COUNTER_CONJURERS, GUILD_STARTED);
	elseif (sequence == SEQ_015 and classId == SWETHYNA and
		quest:GetData():GetCounter(COUNTER_CONJURERS) == GUILD_SCENE) then
		local eventName = "processEvent135";
		if (quest:GetData():GetCounter(COUNTER_LEATHERWORKERS) >= GUILD_STARTED) then
			eventName = "processEvent136";
		end
		callClientFunction(player, "delegateEvent", player, quest, eventName);
		quest:GetData():SetCounter(COUNTER_CONJURERS, GUILD_COMPLETE);
		player:EndEvent();
		GetWorldManager():DoZoneChange(player, 206, nil, 0, 15, -352.15, 6.22, -1694.61, 0.24);
		return;
	elseif (sequence == SEQ_015 and CONJURERS_SCENE_EVENTS[classId] ~= nil) then
		callClientFunction(player, "delegateEvent", player, quest, CONJURERS_SCENE_EVENTS[classId]);
	elseif (sequence == SEQ_040 and classId == OPYLTYL) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent140");
		quest:StartSequence(SEQ_050);
		player:EndEvent();
		GetWorldManager():DoZoneChange(player, 155, "PrivateAreaMasterPast", 1, 15, -223.08, 12, -1498.546, -1.64);
		return;
	elseif ((sequence == SEQ_050 or sequence == SEQ_055) and classId == OPYLTYL and
		player:GetZoneID() == 206) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent1000_3");
		player:EndEvent();
		if (sequence == SEQ_050) then
			GetWorldManager():DoZoneChange(player, 155, "PrivateAreaMasterPast", 1, 15, -223.08, 12, -1498.546, -1.64);
		else
			GetWorldManager():DoZoneChange(player, 155, "PrivateAreaMasterPast", 2, 15, -227.84, 12, -1502.2, 0.51);
		end
		return;
	elseif (sequence == SEQ_050 and GROWERY_CHILDREN[classId] ~= nil) then
		local child = GROWERY_CHILDREN[classId];
		local eventName = child[2];
		if (quest:GetData():GetFlag(child[1])) then
			eventName = child[3];
		end
		callClientFunction(player, "delegateEvent", player, quest, eventName);
	elseif (sequence == SEQ_050 and classId == FUFUCHA) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent140_10");
	elseif (sequence == SEQ_055 and classId == FUFUCHA) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent150_2");
	elseif (sequence == SEQ_055 and GROWERY_CHILDREN[classId] ~= nil) then
		callClientFunction(player, "delegateEvent", player, quest, GROWERY_CHILDREN[classId][3]);
	elseif (sequence == SEQ_080 and classId == WILLELDA) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent190");
		quest:StartSequence(SEQ_085);
	elseif (sequence == SEQ_085 and classId == BURCHARD) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent200");
		quest:StartSequence(SEQ_090);
		player:EndEvent();
		GetWorldManager():DoZoneChange(player, 155, "PrivateAreaMasterPast", 3, 15,
			176.13, 27.5, -1581.84, -1.0);
		return;
	elseif (sequence == SEQ_090 and classId == BURCHARD and player:GetZoneID() == 206) then
		player:EndEvent();
		GetWorldManager():DoZoneChange(player, 155, "PrivateAreaMasterPast", 3, 15,
			176.13, 27.5, -1581.84, -1.0);
		return;
	elseif (sequence == SEQ_090 and classId == BURCHARD) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent210");
		quest:StartSequence(SEQ_095);
		player:EndEvent();
		GetWorldManager():DoZoneChange(player, 155, "PrivateAreaMasterPast", 4, 15,
			177.0, 27.5, -1581.0, 1.6);
		return;
	elseif (sequence == SEQ_095 and classId == BURCHARD and player.privateAreaType == 3) then
		player:EndEvent();
		GetWorldManager():DoZoneChange(player, 155, "PrivateAreaMasterPast", 4, 15,
			177.0, 27.5, -1581.0, 1.6);
		return;
	elseif (sequence == SEQ_095 and classId == NUALA) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent220");
		player:EndEvent();
		quest:NewNpcLsMsg(1);
		quest:StartSequence(SEQ_100);
		GetWorldManager():DoZoneChange(player, 206, nil, 0, 15,
			176.13, 27.5, -1581.84, -1.0);
		return;
	elseif ((sequence == SEQ_100 or sequence == SEQ_105) and classId == NUALA and
		player.privateAreaType == 4) then
		player:EndEvent();
		GetWorldManager():DoZoneChange(player, 206, nil, 0, 15,
			176.13, 27.5, -1581.84, -1.0);
		return;
	elseif (sequence == SEQ_105 and classId == MIOUNNE) then
		callClientFunction(player, "delegateEvent", player, quest, "processEventComplete");
		callClientFunction(player, "delegateEvent", player, quest, "sqrwa", 300, 1, 1, 2);
		player:EndEvent();
		player:CompleteQuest("Man0g1");
		return;
	end

	player:EndEvent();
	quest:UpdateENPCs();
end

function onPush(player, quest, npc)
	if (quest:GetSequence() == SEQ_015 and
		npc:GetActorClassId() == CONJURERS_BRIDGE and
		(quest:GetData():GetCounter(COUNTER_CONJURERS) == GUILD_STARTED or
			quest:GetData():GetCounter(COUNTER_CONJURERS) == GUILD_SCENE)) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent130");
		quest:GetData():SetCounter(COUNTER_CONJURERS, GUILD_SCENE);
		player:EndEvent();
		GetWorldManager():DoZoneChange(player, 155, "PrivateAreaMasterPast", 0, 15, -352.37, 6.24, -1698.2, 0.56);
		return;
	elseif (quest:GetSequence() == SEQ_055 and npc:GetActorClassId() == KIDS_TRIGGER) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent160");
		quest:StartSequence(SEQ_060);
		player:EndEvent();
		GetWorldManager():DoZoneChange(player, 206, nil, 0, 15, -209.46, 18.05, -1476.31, 1.47);
		return;
	elseif (quest:GetSequence() == SEQ_060 and npc:GetActorClassId() == GATE_TRIGGER) then
		local result = callClientFunction(player, "delegateEvent", player, quest, "contentsJoinAskInBasaClass");
		if (result == 1) then
			startMan0g1Content(player, quest);
			return;
		end
	elseif ((quest:GetSequence() == SEQ_070 or
		(quest:GetSequence() == SEQ_071 and player.privateAreaType == 0)) and
		npc:GetActorClassId() == STUMP_TRIGGER) then
		if (quest:GetSequence() == SEQ_070) then
			callClientFunction(player, "delegateEvent", player, quest, "processEvent181");
			quest:StartSequence(SEQ_071);
		end
		player:EndEvent();
		GetWorldManager():DoZoneChange(player, 150, "PrivateAreaMasterPast", 1, 15,
			-756.77, 22.77, -1092.33, 2.4);
		return;
	elseif ((quest:GetSequence() == SEQ_071 or
		(quest:GetSequence() == SEQ_072 and player.privateAreaType == 1)) and
		npc:GetActorClassId() == STUMP_EXIT_TRIGGER) then
		if (quest:GetSequence() == SEQ_071) then
			callClientFunction(player, "delegateEvent", player, quest, "processEvent182");
			quest:StartSequence(SEQ_072);
		end
		player:EndEvent();
		-- The stump duty is hosted by Central Shroud (150), but retail returns
		-- the player to the public Gridania area from which the escort began.
		-- Keeping zone 150 here leaves the client walking through Gridania
		-- geometry while the server continues to publish Shroud actors.
		GetWorldManager():DoZoneChange(player, 155, nil, 0, 15, -185, 6, -962, -3);
		return;
	elseif (quest:GetSequence() == SEQ_072 and npc:GetActorClassId() == BTN_TRIGGER) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent185");
		player:AddGil(3000);
		quest:NewNpcLsMsg(1);
		quest:StartSequence(SEQ_075);
	end

	player:EndEvent();
end

function onEmote(player, quest, npc, eventName)
	local classId = npc:GetActorClassId();
	local child = GROWERY_CHILDREN[classId];
	local data = quest:GetData();

	if (quest:GetSequence() ~= SEQ_050 or child == nil or data:GetFlag(child[1])) then
		player:EndEvent();
		return;
	end

	-- The command id selects the event condition; these are the matching
	-- actor-animation and description ids used by the client presentation.
	player:DoEmote(npc.actorId, child[5], child[6]);
	wait(2);
	callClientFunction(player, "delegateEvent", player, quest, child[4]);
	data:SetFlag(child[1]);

	if (bit32.band(data:GetFlags(), ALL_GROWERY_EMOTES) == ALL_GROWERY_EMOTES) then
		callClientFunction(player, "delegateEvent", player, quest, "processEvent150");
		quest:StartSequence(SEQ_055);
		player:EndEvent();
		GetWorldManager():DoZoneChange(player, 155, "PrivateAreaMasterPast", 2, 15, -227.84, 12, -1502.2, 0.51);
		return;
	end

	player:EndEvent();
	quest:UpdateENPCs();
end

function onNpcLS(player, quest, from, msgStep)
	local sequence = quest:GetSequence();
	local msgPack;

	if (from == 1) then
		if (sequence == SEQ_005) then
			msgPack = 1;
		elseif (sequence == SEQ_015) then
			if (quest:GetData():GetCounter(COUNTER_GUILD_HANDOFF) == 0) then
				msgPack = 2;
			else
				msgPack = 3;
			end
		elseif (sequence == SEQ_075 or sequence == SEQ_080) then
			msgPack = 4;
		elseif (sequence == SEQ_100 or sequence == SEQ_105) then
			msgPack = 5;
		end
	end

	if (msgPack ~= nil and NPCLS_MSGS[msgPack][msgStep] ~= nil) then
		player:SendGameMessageDisplayIDSender(quest, NPCLS_MSGS[msgPack][msgStep], MESSAGE_TYPE_NPC_LINKSHELL, 1300018);
		if (msgStep >= #NPCLS_MSGS[msgPack]) then
			quest:EndOfNpcLsMsgs();
			if (msgPack == 3) then
				quest:StartSequenceForNpcLs(SEQ_040);
			elseif (sequence == SEQ_075) then
				quest:StartSequenceForNpcLs(SEQ_080);
			elseif (sequence == SEQ_100) then
				quest:StartSequenceForNpcLs(SEQ_105);
			end
		else
			quest:ReadNpcLsMsg();
		end
	end

	if (sequence == SEQ_005) then
		showTutorialSuccessWidget(player, 9080);
		wait(3);
		closeTutorialWidget(player);
		endTutorialMode(player);
	end

	player:EndEvent();
end

function startMan0g1Content(player, quest)
	quest:GetData():SetFlag(FLAG_ESCORT_HANDOFF);
	quest:StartSequence(SEQ_065);

	local area = player:GetZone():CreateContentArea(player,
		"/Area/PrivateArea/Content/PrivateAreaMasterSimpleContent",
		"Man0g101", "SimpleContentMan0g101", "Quest/QuestDirectorMan0g101", 150);
	if (area == nil) then
		quest:StartSequence(SEQ_060);
		player:EndEvent();
		return;
	end

	local director = area:GetContentDirector();
	player:AddDirector(director);
	director:StartDirector(false);
	player:DeferContentKickEvent(director, "noticeEvent", true);
	player:SetLoginDirector(director);
	callClientFunction(player, "delegateEvent", player, quest, "processEvent170");
	player:EndEvent();
	GetWorldManager():DoZoneChangeContent(player, area,
		-194.73, 3.54, -1021.33, -1.642, 16);
end
