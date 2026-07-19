require ("global")

--[[

NpcLinkshellChatCommand Script

Handler for when a player clicks a npc ls to talk to. If adding new linkshells to the handle, make sure to add
it to the handler table (with correct offset), and that your function is above the handler. If padding is needed
to hit some ID, add "nils".

--]]


local function sendQuestLinkpearlMessage(player, questId, questName, textId, senderDisplayId)
	if (player:HasQuest(questId) == true) then
		local quest = player:GetQuest(questName);
		if (quest ~= nil) then
			player:SendGameMessage(quest, textId, 39, senderDisplayId, nil);
			return true;
		end
	end

	return false;
end

local function handleAdventurersGuild(player)
	if (sendQuestLinkpearlMessage(player, 110010, "Man0u1", 330, 1500014) == true) then
		return;
	end

	if (sendQuestLinkpearlMessage(player, 110006, "Man0g1", 330, 1300018) == true) then
		return;
	end
end

local function handlePathOfTheTwelve(player)
	player:SendMessage(0x20, "", "Test");
end

local npcLsHandlers = {
	handleAdventurersGuild,
	nil,
	nil,
	nil,
	nil,
	handlePathOfTheTwelve	
}

function onEventStarted(player, command, triggerName, npcLsId)		
	
	if (npcLsHandlers[npcLsId] ~= nil) then
		npcLsHandlers[npcLsId](player);
		player:SetNpcLS(npcLsId-1, NPCLS_ACTIVE);
	else
		player:SendMessage(0x20, "", "That Npc Linkshell is not implemented yet.");
	end	
	
	player:endEvent();	
	
end
