require ("global")
require ("tutorial")
require ("modifiers")
require ("quests/man/man0u0")

--processTtrBtl001: Active Mode Tutorial
--processTtrBtl002: Targetting Tutorial (After active mode done)
--processTtrBtl003: Auto Attack Done
--processTtrBtl004: Tutorial Complete

function init()
	return "/Director/Quest/QuestDirectorMan0u001";
end

function onEventStarted(player, actor, triggerName)	

	man0u0Quest = player:GetQuest("Man0u0");
	player:SetMod(modifiersGlobal.MinimumHpLock, 1);
	startTutorialMode(player);
	callClientFunction(player, "delegateEvent", player, man0u0Quest, "processTtrBtl001", nil, nil, nil);
	player:EndEvent();
	waitForSignal("playerActive");
	wait(1); --If this isn't here, the scripts bugs out. TODO: Find a better alternative.
	kickEventContinue(player, actor, "noticeEvent", "noticeEvent");	
	callClientFunction(player, "delegateEvent", player, man0u0Quest, "processTtrBtl002", nil, nil, nil);
	player:EndEvent();

	if player:IsDiscipleOfWar() then
		waitForSignal("playerAttack");
		closeTutorialWidget(player);
		showTutorialSuccessWidget(player, 9055); --Open TutorialSuccessWidget for attacking enemy
		openTutorialWidget(player, CONTROLLER_KEYBOARD, TUTORIAL_TP);
		waitForSignal("tpOver1000");
		player:SetMod(modifiersGlobal.MinimumTpLock, 1000);
		closeTutorialWidget(player);
		openTutorialWidget(player, CONTROLLER_KEYBOARD, TUTORIAL_WEAPONSKILLS);
		waitForSignal("weaponskillUsed");
		player:SetMod(modifiersGlobal.MinimumTpLock, 0);
		closeTutorialWidget(player);
		showTutorialSuccessWidget(player, 9065); --Open TutorialSuccessWidget for weapon skill
	elseif player:IsDiscipleOfMagic() then
		openTutorialWidget(player, CONTROLLER_KEYBOARD, TUTORIAL_CASTING);
		waitForSignal("spellUsed");
		closeTutorialWidget(player);
	elseif player:IsDiscipleOfHand() then
		waitForSignal("abilityUsed");
	elseif player:IsDiscipleOfLand() then
		waitForSignal("abilityUsed");
	end

	player:GetZone():SetBattleNpcMinimumHpLock(0);
	
	waitForSignal("mobkill");
	worldMaster = GetWorldMaster();
	player:SetMod(modifiersGlobal.MinimumHpLock, 0);
	player:SetMod(modifiersGlobal.MinimumTpLock, 0);
	player:SendDataPacket("attention", worldMaster, "", 51073, 3);
	wait(7);
	player:ChangeMusic(7);
	player:ChangeState(0); 
	kickEventContinue(player, actor, "noticeEvent", "noticeEvent");
	callClientFunction(player, "delegateEvent", player, man0u0Quest, "processEvent020", nil, nil, nil);	
	
	man0u0Quest:NextPhase(10);	
	player:EndEvent();	
	
	player:GetZone():ContentFinished();
	GetWorldManager():DoZoneChange(player, 175, "PrivateAreaMasterPast", 3, 15, -22.81, 196, 87.82, 2.98);
end
