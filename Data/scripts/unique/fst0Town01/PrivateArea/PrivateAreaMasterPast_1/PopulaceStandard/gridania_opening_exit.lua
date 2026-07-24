require ("global")
require ("quests/man/man0g0")

function onSpawn(player, npc)	
	npc:SetQuestGraphic(player, 0x3);	
end

function onEventStarted(player, npc)
	man0g1Quest = GetStaticActor("Man0g1");		
	callClientFunction(player, "delegateEvent", player, man0g1Quest, "processEvent100");
	player:ReplaceQuest(110005, 110006);

	-- Close the source event before the inline same-map reload. A trailing
	-- EndEvent races the replacement actor table and can address the deleted
	-- PrivateAreaMasterPast/1 owner after the destination bundle has begun.
	player:EndEvent();

	-- Retail lands at the Roost with Man0g1 still at sequence 0. Miounne's
	-- first conversation grants the linkpearl and advances the quest; starting
	-- either here makes the tutorial flash before the player has received it.
	GetWorldManager():DoZoneChange(player, 155, "PrivateAreaMasterPast", 2, 15, 67.034, 4, -1205.6497, -1.074);
end
