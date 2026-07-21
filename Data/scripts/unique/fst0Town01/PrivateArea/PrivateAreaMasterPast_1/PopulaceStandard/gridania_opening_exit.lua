require ("global")
require ("quests/man/man0g0")

function onSpawn(player, npc)	
	npc:SetQuestGraphic(player, 0x3);	
end

function onEventStarted(player, npc)
	man0g1Quest = GetStaticActor("Man0g1");		
	callClientFunction(player, "delegateEvent", player, man0g1Quest, "processEvent100");
	player:ReplaceQuest(110005, 110006);
	local ownedMan0g1Quest = player:GetQuest(110006);
	ownedMan0g1Quest:NewNpcLsMsg(1);
	ownedMan0g1Quest:NextPhase(5);
	player:SendGameMessage(GetStaticActor("Man0g1"), 353, 0x20);
	player:SendGameMessage(GetStaticActor("Man0g1"), 354, 0x20);

	-- Close the source event before the inline same-map reload. A trailing
	-- EndEvent races the replacement actor table and can address the deleted
	-- PrivateAreaMasterPast/1 owner after the destination bundle has begun.
	player:EndEvent();

	-- The destination-scoped director is included in the replacement bundle.
	-- Its notice kick is held until the client acknowledges that bundle, which
	-- clears the cutscene veil without addressing an owner erased by 0x0007.
	local director = GetWorldManager():GetZone(155):CreateDirector("AfterQuestWarpDirector", false);
	director:StartDirector(true);
	player:AddDirector(director);
	player:SetLoginDirector(director);
	player:DeferContentKickEvent(director, "noticeEvent", true);
	GetWorldManager():DoZoneChange(player, 155, "PrivateAreaMasterPast", 2, 15, 67.034, 4, -1205.6497, -1.074);
end
