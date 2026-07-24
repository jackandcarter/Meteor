require ("global")

function onSpawn(player, npc)
	local man0g1Quest = player:GetQuest("Man0g1");
	if (man0g1Quest ~= nil and
		(man0g1Quest:GetSequence() == 0 or man0g1Quest:GetSequence() == 5)) then
		npc:SetQuestGraphic(player, 0x2);
	end
end

function onEventStarted(player, npc, triggerName)
	local man0g1Quest = player:GetQuest("Man0g1");
	local pos = player:GetPos();
	
	if (man0g1Quest ~= nil and
		(man0g1Quest:GetSequence() == 0 or man0g1Quest:GetSequence() == 5)) then
		-- Historical Man0g1 sequence 0: speak to Miounne first, receive the
		-- Adventurers' Guild linkpearl, then arm its tutorial while leaving the
		-- private Roost. Sequence 5 is accepted here only to repair characters
		-- advanced prematurely by Build 21987 without requiring a DB edit.
		callClientFunction(player, "delegateEvent", player, man0g1Quest, "processEvent100_1");
		man0g1Quest:NewNpcLsMsg(1);
		man0g1Quest:StartSequence(5);
		player:EndEvent();

		local director = GetWorldManager():GetZone(155):CreateDirector("AfterQuestWarpDirector", false);
		director:StartDirector(true);
		player:AddDirector(director);
		player:SetLoginDirector(director);
		player:DeferContentKickEvent(director, "noticeEvent", true);
		man0g1Quest:UpdateENPCs();
		GetWorldManager():DoZoneChange(player, 155, nil, 0, 15, pos[0], pos[1], pos[2], pos[3]);
		return;
	end
	
	player:EndEvent();
	
end
