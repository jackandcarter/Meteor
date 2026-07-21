require("global")

function init()
	return "/Director/AfterQuestWarpDirector";
end

function onEventStarted(player, director, eventType, eventName)
	if (player:HasQuest(110006) == true) then
		local quest = player:GetQuest(110006);
		quest:OnNotice(player);
	else
		-- The acknowledgement still has to close when no routed quest exists,
		-- otherwise the client remains event-locked behind the loading veil.
		player:EndEvent();
	end
end

function main()
end
