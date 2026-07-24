require ("global")

function onSpawn(player, npc)	
	npc:SetQuestGraphic(player, 0x2);	
end

function onEventStarted(player, npc, triggerName)
	local man0u1Quest = player:GetQuest("Man0u1");
	local pos = player:GetPos();
	
	if (man0u1Quest ~= nil and man0u1Quest:GetSequence() == 0) then
		callClientFunction(player, "delegateEvent", player, man0u1Quest, "processEvent010");
		-- Retail gives the Adventurers' Guild pearl during this briefing and
		-- immediately invites the player to try it before leaving for camp.
		man0u1Quest:NewNpcLsMsg(1);
		man0u1Quest:StartSequence(5);
		player:EndEvent();
		GetWorldManager():DoZoneChange(player, 175, nil, 0, 15, pos[0], pos[1], pos[2], pos[3]);
		return;
	elseif (man0u1Quest ~= nil and man0u1Quest:GetSequence() == 5) then
		-- Reconnect/re-talk safety while the Quicksand private area is still
		-- loaded. This is Momodi's retail "attune at Camp Black Brush" reminder.
		callClientFunction(player, "delegateEvent", player, man0u1Quest, "processEvent010_2");
		player:EndEvent();
		GetWorldManager():DoZoneChange(player, 175, nil, 0, 15, pos[0], pos[1], pos[2], pos[3]);
		return;
	end
	
	player:EndEvent();
	
end
