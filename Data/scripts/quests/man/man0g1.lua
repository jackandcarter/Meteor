require("global")

-- The first post-opening Man0g1 continuation. The destination-scoped
-- AfterQuestWarpDirector invokes this after the PA/1 -> PA/2 replacement
-- bundle is acknowledged, when it is safe to address the successor quest.
function onNotice(player, quest, target)
	if (quest:GetPhase() == 5) then
		-- This tutorial is synchronous and sends no EventUpdate. Using the raw
		-- event function is required so EndEvent runs instead of parking a Lua
		-- coroutine forever behind the NPC-linkpearl overlay.
		player:RunEventFunction("delegateEvent", player, quest, "processEventTu_001");
	end

	player:EndEvent();
end

function isObjectivesComplete(player, quest)
	return false;
end
