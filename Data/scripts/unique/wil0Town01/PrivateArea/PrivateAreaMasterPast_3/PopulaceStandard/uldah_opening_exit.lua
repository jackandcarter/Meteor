require ("global")
require ("quests/man/man0u0")

function onSpawn(player, npc)	
	npc:SetQuestGraphic(player, 0x3);	
end

function onEventStarted(player, npc)	
	local man0u1Quest = GetStaticActor("Man0u1");
	if (man0u1Quest ~= nil) then
		callClientFunction(player, "delegateEvent", player, man0u1Quest, "processEventMomodiStart");
		player:SendGameMessage(man0u1Quest, 329, 0x20);
		player:SendGameMessage(man0u1Quest, 330, 0x20);
	end

	player:ReplaceQuest(110009, 110010);
	player:EndEvent();
	GetWorldManager():DoZoneChange(player, 175, "PrivateAreaMasterPast", 4, 15, -75.242, 195.009, 74.572, -0.046);
end
