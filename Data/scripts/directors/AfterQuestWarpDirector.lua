require("global")

function init()
	return "/Director/AfterQuestWarpDirector";
end

function onEventStarted(player, director, eventType, eventName)
	if (player:HasQuest(110006) == true) then
		local quest = player:GetQuest(110006);
		if (quest ~= nil and quest:GetSequence() == 5) then
			-- This client tutorial is synchronous: it does not answer with an
			-- EventUpdate. Run it directly from the acknowledged destination
			-- event and close that event immediately.
			--
			-- Do not call quest:OnNotice here. That nests a second Lua VM
			-- dispatch inside this director coroutine. The VPS trace from
			-- 2026-07-24 stops exactly at that boundary: noticeEvent reaches
			-- the director, but neither RunEventFunction nor EndEvent is sent,
			-- leaving the client event-locked before the linkpearl can be read.
			player:RunEventFunction("delegateEvent", player, quest, "processEventTu_001");
		end
	end

	-- Every notice acknowledgement must close, including stale recovery kicks.
	player:EndEvent();
end

function main()
end
