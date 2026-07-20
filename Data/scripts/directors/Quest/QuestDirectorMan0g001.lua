require ("global")
require ("tutorial")
require ("modifiers")
require ("quests/man/man0g0")

-- Gridania opening battle (Man0g0 / SimpleContent30010).
-- The ordering here follows the client event contract recovered from the
-- official trace and the shipped Man0g0 client script. In particular, each
-- delegate is allowed to finish before the next server-side transition.

function init()
	return "/Director/Quest/QuestDirectorMan0g001";
end

function onEventStarted(player, actor, triggerName)
	local man0g0Quest = player:GetQuest("Man0g0");
	if man0g0Quest == nil then
		player:EndEvent();
		return;
	end

	player:SetMod(modifiersGlobal.MinimumHpLock, 1);
	startTutorialMode(player);

	if player:IsDiscipleOfWar() then
		-- Active-mode prompt, followed by the enemy-targeting prompt.
		callClientFunction(player, "delegateEvent", player, man0g0Quest, "processTtrBtl001", nil, nil, nil);
		player:EndEvent();
		waitForSignal("playerActive");
		wait(1);
		kickEventContinue(player, actor, "noticeEvent", "noticeEvent");
		callClientFunction(player, "delegateEvent", player, man0g0Quest, "processTtrBtl002", nil, nil, nil);
		player:EndEvent();

		-- Engage after the first attack, but keep the wolves at 1 HP until the
		-- weaponskill lesson is complete. Releasing them here lets Yda or
		-- Papalymo kill every target while the client is still waiting for TP.
		waitForSignal("playerAttack");
		player:GetZone():EngageContentBattleForPlayer(player);
		closeTutorialWidget(player);
		showTutorialSuccessWidget(player, 9055);
		openTutorialWidget(player, CONTROLLER_KEYBOARD, TUTORIAL_TP);
		waitForSignal("tpOver1000");
		player:SetMod(modifiersGlobal.MinimumTpLock, 1000);
		closeTutorialWidget(player);
		openTutorialWidget(player, CONTROLLER_KEYBOARD, TUTORIAL_WEAPONSKILLS);
		waitForSignal("weaponskillUsed");
		player:GetZone():SetBattleNpcMinimumHpLock(0);
		player:SetMod(modifiersGlobal.MinimumTpLock, 0);
		closeTutorialWidget(player);
		showTutorialSuccessWidget(player, 9065);
	elseif player:IsDiscipleOfMagic() then
		callClientFunction(player, "delegateEvent", player, man0g0Quest, "processTtrBtlMagic001", nil, nil, nil);
		player:EndEvent();
		wait(1);
		kickEventContinue(player, actor, "noticeEvent", "noticeEvent");
		player:GetZone():EngageContentBattleForPlayer(player);
		player:GetZone():SetBattleNpcMinimumHpLock(0);
		closeTutorialWidget(player);
		openTutorialWidget(player, CONTROLLER_KEYBOARD, TUTORIAL_DEFEATENEMY);
	else
		-- Opening characters should be DoW or DoM. Keep a recoverable path
		-- for imported characters instead of leaving the content locked.
		waitForSignal("playerAttack");
		player:GetZone():EngageContentBattleForPlayer(player);
		player:GetZone():SetBattleNpcMinimumHpLock(0);
	end

	-- This signal is emitted once by PrivateAreaContent when all three
	-- hostile members of this player's content group are dead. The state
	-- check also covers the rare case where the allies land the final blow
	-- while the player is dismissing the weaponskill tutorial.
	if not player:GetZone():IsContentBattleComplete() then
		waitForSignal(player:GetZone():GetBattleCompleteSignal(player));
	end
	wait(3);
	closeTutorialWidget(player);
	if player:IsDiscipleOfMagic() then
		showTutorialSuccessWidget(player, 9050);
	end

	player:SetMod(modifiersGlobal.MinimumHpLock, 0);
	player:SetMod(modifiersGlobal.MinimumTpLock, 0);
	player:SendDataPacket("attention", GetWorldMaster(), "", 51073, 2);
	wait(2);
	player:Disengage(0x0000);
	player:ChangeMusic(7);
	player:ChangeState(0);

	-- Reopen noticeEvent before processEvent020_1. Without an active event
	-- owner, the client drops the post-battle chain immediately.
	kickEventContinue(player, actor, "noticeEvent", "noticeEvent");
	callClientFunction(player, "delegateEvent", player, man0g0Quest, "processEvent020_1", nil, nil, nil);
	man0g0Quest:NextPhase(10);
	player:EndEvent();

	player:GetZone():ContentFinished();
	actor:EndDirector();
	GetWorldManager():DoZoneChange(player, 155, "PrivateAreaMasterPast", 1, 15, 175.38, -1.21, -1156.51, -2.1);
end

function onUpdate(deltaTime, area)
end

function onTalkEvent(player, npc)
end

function onPushEvent(player, npc)
end

function onCommandEvent(player, command)
end

function onEventUpdate(player, npc)
end

function onCommand(player, command)
end

-- The group is activated by SendZoneInPackets after the player has entered
-- the private area. Starting it here emits a stale-zone roster before the
-- instance actors exist on the client.
function main(director, contentGroup)
end
